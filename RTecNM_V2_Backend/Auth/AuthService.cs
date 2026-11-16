using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly AppDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly LoginControlSettings _loginControlSettings;
    private readonly IHttpClientFactory? _httpClientFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class LoginControlExchangeResponse
    {
        public ExternalLoginUserDto? User { get; set; }
    }

    public AuthService(
        IAuthRepository authRepository,
        AppDbContext context,
        IOptions<JwtSettings> jwtSettings,
        IOptions<LoginControlSettings>? loginControlOptions = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        _authRepository = authRepository;
        _context = context;
        _jwtSettings = jwtSettings.Value;
        _loginControlSettings = loginControlOptions?.Value ?? new LoginControlSettings();
        _httpClientFactory = httpClientFactory;
    }

    public Task<Result<AuthTokenResponseDto>> LoginAsync(LoginRequestDto dto)
    {
        return Task.FromResult(Result<AuthTokenResponseDto>.Failure(
            "El acceso local por contraseña ha sido deshabilitado. Inicia sesión con tu cuenta institucional de Microsoft desde el Portal TecNM.",
            403));
    }

    public async Task<Result<AuthTokenResponseDto>> ExchangeLoginControlCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result<AuthTokenResponseDto>.Failure("Código de autorización SSO inválido.", 400);

        if (!Guid.TryParse(_loginControlSettings.ModuleId, out var moduleGuid) || string.IsNullOrWhiteSpace(_loginControlSettings.ApiKey))
            return Result<AuthTokenResponseDto>.Failure("La configuración SSO de LoginControl (ModuleId/ApiKey) no está establecida en el servidor.", 500);

        if (_httpClientFactory is null)
            return Result<AuthTokenResponseDto>.Failure("Servicio HttpClient no disponible para intercambio SSO.", 500);

        var exchangeUrl = $"{_loginControlSettings.InternalBaseUrl.TrimEnd('/')}/api/modules/exchange";
        var client = _httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, exchangeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _loginControlSettings.ApiKey);
        request.Content = JsonContent.Create(new { moduleId = moduleGuid, code = code.Trim() });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return Result<AuthTokenResponseDto>.Failure(
                "No fue posible validar el acceso con el Portal TecNM (LoginControl). Intenta ingresar nuevamente desde el portal.",
                401);
        }

        var payload = JsonSerializer.Deserialize<LoginControlExchangeResponse>(body, JsonOptions);
        if (payload?.User is null)
            return Result<AuthTokenResponseDto>.Failure("Respuesta inválida del servidor LoginControl.", 502);

        return await AuthenticateExternalUserAsync(payload.User);
    }

    public async Task<Result<AuthTokenResponseDto>> AuthenticateExternalUserAsync(ExternalLoginUserDto externalUser)
    {
        if (externalUser is null)
            return Result<AuthTokenResponseDto>.Failure("Datos de usuario externo inválidos.", 400);

        var microsoftEmail = externalUser.MicrosoftEmail?.Trim().ToLowerInvariant();
        var lookupEmail = !string.IsNullOrWhiteSpace(microsoftEmail)
            ? microsoftEmail
            : externalUser.GoogleEmail?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(lookupEmail))
            return Result<AuthTokenResponseDto>.Failure(
                "ACCESO DENEGADO: Tu sesión en LoginControl no cuenta con un correo institucional Microsoft (@monclova.tecnm.mx) verificado.",
                403);

        var user = await _authRepository.GetByExternalUserIdAsync(externalUser.Id);
        if (user is null)
        {
            user = await _authRepository.GetByEmailAsync(lookupEmail);
        }

        if (user is null)
        {
            return Result<AuthTokenResponseDto>.Failure(
                $"ACCESO DENEGADO: El correo institucional ({lookupEmail}) no está dado de alta en el Sistema de Residencias Profesionales. Contacta a División de Estudios Profesionales.",
                403);
        }

        if (!user.IsActive)
            return Result<AuthTokenResponseDto>.Failure("Cuenta desactivada. Contacte a la administracion", 403);

        // Vincular ExternalUserId y sincronizar datos de nombre desde LoginControl si vienen poblados
        var needsUpdate = user.ExternalUserId != externalUser.Id;
        user.ExternalUserId = externalUser.Id;

        if (!string.IsNullOrWhiteSpace(externalUser.FirstNames) && string.IsNullOrWhiteSpace(user.FirstName))
        {
            user.FirstName = externalUser.FirstNames.Trim();
            needsUpdate = true;
        }
        if (!string.IsNullOrWhiteSpace(externalUser.PaternalLastName) && string.IsNullOrWhiteSpace(user.LastName))
        {
            user.LastName = externalUser.PaternalLastName.Trim();
            needsUpdate = true;
        }
        if (!string.IsNullOrWhiteSpace(externalUser.MaternalLastName) && string.IsNullOrWhiteSpace(user.LastName2))
        {
            user.LastName2 = externalUser.MaternalLastName.Trim();
            needsUpdate = true;
        }

        if (needsUpdate)
        {
            await _authRepository.UpdateUserAsync(user);
        }

        return await BuildAuthTokenResponseAsync(user);
    }

    private async Task<Result<AuthTokenResponseDto>> BuildAuthTokenResponseAsync(User user)
    {
        if (user.Role == UserRole.Admin)
        {
            user.IsAdmin = true;
        }

        string? fullName = null;
        string? controlNumber = null;
        long? careerId = null;

        if (user.Role == UserRole.Student)
        {
            var student = await _context.Students
                .AsNoTracking()
                .Where(s => s.UserId == user.Id)
                .Select(s => new {
                    FullName = (s.FirstName + " " + s.LastName + (string.IsNullOrWhiteSpace(s.LastName2) ? "" : " " + s.LastName2)).Trim(),
                    s.ControlNumber,
                    s.CareerId,
                    s.HasComplementaryActivities,
                    s.HasSocialService,
                    s.HasSpecialRequirements,
                    Blocks = s.Blocks.Where(b => b.IsActive).Select(b => b.Reason).ToList()
                })
                .FirstOrDefaultAsync();

            if (student != null)
            {
                fullName = student.FullName;
                controlNumber = student.ControlNumber;
                careerId = student.CareerId;

                // Verificar bloqueos activos explícitos en student_blocks
                if (student.Blocks != null && student.Blocks.Count > 0)
                {
                    var blockReason = student.Blocks.First();
                    return Result<AuthTokenResponseDto>.Failure(blockReason, 403);
                }

                // Verificar requisitos obligatorios de residencia aunque no exista fila manual en student_blocks
                if (!student.HasComplementaryActivities || !student.HasSocialService || !student.HasSpecialRequirements)
                {
                    var missing = new List<string>();
                    if (!student.HasComplementaryActivities) missing.Add("Actividades Complementarias");
                    if (!student.HasSocialService) missing.Add("Servicio Social");
                    if (!student.HasSpecialRequirements) missing.Add("Requisitos Especiales (80% créditos / sin especiales)");

                    return Result<AuthTokenResponseDto>.Failure(
                        $"ACCESO DENEGADO: No cumples con los requisitos obligatorios para Residencias Profesionales (Falta: {string.Join(", ", missing)}).",
                        403);
                }
            }
        }
        else if (user.Role == UserRole.Advisor)
        {
            var advisor = await _context.Advisors
                .AsNoTracking()
                .Where(a => a.UserId == user.Id)
                .Select(a => a.FullName)
                .FirstOrDefaultAsync();

            fullName = advisor;
        }
        else if (user.Role == UserRole.CareerHead)
        {
            var careerHead = await _context.CareerHeads
                .AsNoTracking()
                .Where(ch => ch.UserId == user.Id && ch.IsActive)
                .Select(ch => new { ch.FullName, ch.CareerId })
                .FirstOrDefaultAsync();

            if (careerHead != null)
            {
                fullName = careerHead.FullName;
                careerId = careerHead.CareerId;
            }
        }

        var coordinatorCareerIds = new List<long>();
        var coordinatorCareerNames = new List<string>();
        if (user.Role == UserRole.Coordinator)
        {
            var ucList = await _context.UserCareers
                .AsNoTracking()
                .Include(uc => uc.Career)
                .Where(uc => uc.UserId == user.Id && uc.IsActive)
                .ToListAsync();

            coordinatorCareerIds = ucList.Select(x => x.CareerId).ToList();
            coordinatorCareerNames = ucList.Select(x => x.Career != null ? x.Career.Name : "").Where(n => !string.IsNullOrEmpty(n)).ToList();

            if (coordinatorCareerIds.Any())
            {
                careerId = coordinatorCareerIds.First();
            }

            var composed = $"{user.FirstName} {user.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(composed))
            {
                fullName = composed;
            }
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            var fallbackName = $"{user.FirstName} {user.LastName} {user.LastName2}".Trim();
            if (!string.IsNullOrWhiteSpace(fallbackName))
            {
                fullName = fallbackName;
            }
        }

        long? advisorId = null;
        if (user.Role == UserRole.Advisor || user.Role == UserRole.CareerHead)
        {
            advisorId = await _context.Advisors
                .AsNoTracking()
                .Where(a => a.UserId == user.Id && a.IsActive)
                .Select(a => (long?)a.Id)
                .FirstOrDefaultAsync();
        }

        var permissions = await GetUserPermissionSlugsAsync(user);
        var token = GenerateJwtToken(user, permissions, careerId, coordinatorCareerIds, advisorId);
        var expiresIn = (int)TimeSpan.FromMinutes(_jwtSettings.ExpirationMinutes).TotalSeconds;

        var roleCode = user.Role switch
        {
            UserRole.CareerHead => "jefecarrera",
            UserRole.Coordinator => "coordinadora",
            _ => user.Role.ToString().ToLowerInvariant()
        };

        var response = new AuthTokenResponseDto
        {
            Token = token,
            ExpiresIn = expiresIn,
            User = new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = fullName,
                ControlNumber = controlNumber,
                CareerId = careerId,
                AdvisorId = advisorId,
                CareerIds = coordinatorCareerIds,
                CareerNames = coordinatorCareerNames,
                Role = roleCode,
                IsActive = user.IsActive,
                IsAdmin = user.IsAdmin,
                Permissions = permissions
            }
        };

        return Result<AuthTokenResponseDto>.Success(response);
    }

    public async Task<Dictionary<long, string>> GetUserDisplayNamesByIdsAsync(List<long> userIds)
    {
        return await _authRepository.GetUserDisplayNamesByIdsAsync(userIds);
    }

    private async Task<List<string>> GetUserPermissionSlugsAsync(User user)
    {
        if (user.IsAdmin || user.Role == UserRole.Admin)
        {
            return new List<string> {
                "projects", "projects.proposals", "projects.review", "projects.delete",
                "students", "students.read", "students.manage",
                "advisors", "advisors.read", "advisors.manage",
                "activities", "activities.schedule",
                "evaluations", "evaluations.advisories", "evaluations.grading",
                "documents", "documents.digital", "documents.manage",
                "admin", "admin.reports", "admin.roles"
            };
        }

        var perms = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id && ur.IsActive && ur.Role!.IsActive)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.IsActive && rp.Permission!.IsActive && rp.Permission!.Module!.IsActive)
            .Select(rp => rp.Permission!.Slug)
            .Distinct()
            .ToListAsync();

        if (perms.Count == 0)
        {
            var roleCode = user.Role switch
            {
                UserRole.CareerHead => "jefecarrera",
                UserRole.Coordinator => "coordinadora",
                _ => user.Role.ToString().ToLowerInvariant()
            };

            perms = await _context.RolePermissions
                .Where(rp => rp.Role != null && rp.Role.Code == roleCode && rp.Role.IsActive && rp.IsActive && rp.Permission!.IsActive && rp.Permission!.Module!.IsActive)
                .Select(rp => rp.Permission!.Slug)
                .Distinct()
                .ToListAsync();
        }

        if (user.Role == UserRole.CareerHead)
        {
            var advisorPerms = await _context.RolePermissions
                .Where(rp => rp.Role != null && rp.Role.Code == "advisor" && rp.Role.IsActive && rp.IsActive && rp.Permission!.IsActive && rp.Permission!.Module!.IsActive)
                .Select(rp => rp.Permission!.Slug)
                .Distinct()
                .ToListAsync();

            if (advisorPerms.Count == 0)
            {
                advisorPerms = new List<string>
                {
                    "projects.advisor",
                    "activities.progress.validate",
                    "advisories.session.record",
                    "evaluations.grading",
                    "evaluations.grade.partial",
                    "evaluations.grade.final",
                    "evaluations.summary.view"
                };
            }

            perms = perms.Union(advisorPerms).Distinct().ToList();
        }

        return perms;
    }

    private string GenerateJwtToken(User user, List<string> permissions, long? careerId = null, List<long>? careerIds = null, long? advisorId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var roleCode = user.Role switch
        {
            UserRole.CareerHead => "jefecarrera",
            UserRole.Coordinator => "coordinadora",
            _ => user.Role.ToString().ToLowerInvariant()
        };

        var claimsList = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, roleCode),
            new("isAdmin", user.IsAdmin.ToString().ToLowerInvariant()),
            new("is_admin", user.IsAdmin.ToString().ToLowerInvariant())
        };

        if (user.Role == UserRole.CareerHead)
        {
            claimsList.Add(new Claim(ClaimTypes.Role, "careerhead"));
        }
        else if (user.Role == UserRole.Coordinator)
        {
            claimsList.Add(new Claim(ClaimTypes.Role, "coordinator"));
        }

        if (advisorId.HasValue && advisorId.Value > 0)
        {
            claimsList.Add(new Claim("advisor_id", advisorId.Value.ToString()));
            claimsList.Add(new Claim("AdvisorId", advisorId.Value.ToString()));
        }

        if (careerIds != null && careerIds.Any())
        {
            var joined = string.Join(",", careerIds);
            claimsList.Add(new Claim("CareerIds", joined));
            claimsList.Add(new Claim("career_ids", joined));
            foreach (var cid in careerIds)
            {
                claimsList.Add(new Claim("career_id", cid.ToString()));
                claimsList.Add(new Claim("CareerId", cid.ToString()));
            }
        }
        else if (careerId.HasValue && careerId.Value > 0)
        {
            claimsList.Add(new Claim("career_id", careerId.Value.ToString()));
            claimsList.Add(new Claim("careerId", careerId.Value.ToString()));
            claimsList.Add(new Claim("CareerIds", careerId.Value.ToString()));
        }

        foreach (var perm in permissions)
        {
            claimsList.Add(new Claim("Permission", perm));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claimsList,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
