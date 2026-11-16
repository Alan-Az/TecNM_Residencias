using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace TecNM.Residency.Auth;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private const string SsoStateCookieName = "rtecnm_sso_state";
    private readonly IAuthService _authService;
    private readonly LoginControlSettings _loginControlSettings;

    public AuthController(
        IAuthService authService,
        IOptions<LoginControlSettings>? loginControlOptions = null)
    {
        _authService = authService;
        _loginControlSettings = loginControlOptions?.Value ?? new LoginControlSettings();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [AllowAnonymous]
    [HttpGet("sso/config")]
    public IActionResult GetSsoConfig()
    {
        return Ok(new SsoPublicConfigDto(
            _loginControlSettings.LoginPortalUrl,
            _loginControlSettings.PublicBaseUrl,
            _loginControlSettings.ModuleId,
            _loginControlSettings.FrontendBaseUrl
        ));
    }

    [AllowAnonymous]
    [HttpPost("sso/exchange")]
    public async Task<IActionResult> ExchangeSsoCode([FromBody] SsoExchangeRequestDto dto)
    {
        var result = await _authService.ExchangeLoginControlCodeAsync(dto.Code);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 403, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [AllowAnonymous]
    [HttpGet("sso")]
    [HttpGet("/acceso")]
    public async Task<IActionResult> HandleSsoHandshake(
        [FromQuery] string? ticket,
        [FromQuery] string? code,
        [FromQuery] string? state)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var frontendLoginUrl = $"{_loginControlSettings.FrontendBaseUrl.TrimEnd('/')}/login";

        // Fase 1: LoginControl redirige con ?ticket=<64 hex>
        if (!string.IsNullOrWhiteSpace(ticket) && string.IsNullOrWhiteSpace(code))
        {
            var generatedState = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            Response.Cookies.Append(SsoStateCookieName, generatedState, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromSeconds(120),
                Path = "/"
            });

            var authorizeUrl = $"{_loginControlSettings.PublicBaseUrl.TrimEnd('/')}/api/modules/authorize" +
                               $"?ticket={Uri.EscapeDataString(ticket.Trim())}&state={Uri.EscapeDataString(generatedState)}";
            return Redirect(authorizeUrl);
        }

        // Fase 2: LoginControl devuelve ?code=<64 hex>&state=...
        if (!string.IsNullOrWhiteSpace(code))
        {
            if (Request.Cookies.TryGetValue(SsoStateCookieName, out var expectedState))
            {
                Response.Cookies.Delete(SsoStateCookieName, new CookieOptions { Path = "/" });
                if (!string.Equals(expectedState, state, StringComparison.Ordinal))
                {
                    return Redirect($"{frontendLoginUrl}?sso_error={Uri.EscapeDataString("Estado SSO inválido o expirado. Intenta nuevamente desde el portal.")}");
                }
            }

            var result = await _authService.ExchangeLoginControlCodeAsync(code);
            if (!result.IsSuccess)
            {
                return Redirect($"{frontendLoginUrl}?sso_error={Uri.EscapeDataString(result.ErrorMessage ?? "Acceso denegado.")}");
            }

            var json = JsonSerializer.Serialize(result.Data, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            return Redirect($"{frontendLoginUrl}?sso_payload={Uri.EscapeDataString(encodedPayload)}");
        }

        return Redirect(frontendLoginUrl);
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return Ok(new { message = "Sesion cerrada correctamente" });
    }

    [Authorize(Roles = "admin,departmenthead")]
    [HttpGet("users/names")]
    public async Task<IActionResult> GetUserNames([FromQuery] string ids)
    {
        var idList = (ids ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s.Trim(), out var v) ? v : (long?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        var names = await _authService.GetUserDisplayNamesByIdsAsync(idList);
        return Ok(names);
    }
}
