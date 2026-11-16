using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Students;
using Xunit;

namespace RTecNM_V2_Backend.Tests;

public class SsoAuthIntegrationTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IOptions<JwtSettings> CreateJwtOptions() => Options.Create(new JwtSettings
    {
        Secret = "test_secret_key_for_sso_integration_tests_minimum_32_chars",
        Issuer = "TecNM_Test",
        Audience = "TecNM_Test_Users",
        ExpirationMinutes = 60
    });

    [Fact]
    public async Task AuthenticateExternalUserAsync_ExistingEligibleStudent_LinksExternalIdAndReturnsJwt()
    {
        using var db = CreateInMemoryDbContext();
        var authRepo = new AuthRepository(db);
        var authService = new AuthService(authRepo, db, CreateJwtOptions());

        var user = new User
        {
            Email = "carlos.perez@monclova.tecnm.mx",
            Role = UserRole.Student,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var student = new Student
        {
            UserId = user.Id,
            ControlNumber = "20680100",
            FirstName = "Carlos",
            LastName = "Pérez",
            LastName2 = "López",
            CareerId = 1,
            HasComplementaryActivities = true,
            HasSocialService = true,
            HasSpecialRequirements = true,
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var externalId = Guid.NewGuid();
        var result = await authService.AuthenticateExternalUserAsync(new ExternalLoginUserDto(
            Id: externalId,
            FirstNames: "Carlos",
            PaternalLastName: "Pérez",
            MaternalLastName: "López",
            GoogleEmail: null,
            MicrosoftEmail: "Carlos.Perez@monclova.tecnm.mx"
        ));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Data!.Token));
        Assert.Equal("student", result.Data.User.Role);
        Assert.Equal("20680100", result.Data.User.ControlNumber);

        var reloaded = await db.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal(externalId, reloaded.ExternalUserId);
    }

    [Fact]
    public async Task AuthenticateExternalUserAsync_UnregisteredMicrosoftEmail_ReturnsForbidden()
    {
        using var db = CreateInMemoryDbContext();
        var authRepo = new AuthRepository(db);
        var authService = new AuthService(authRepo, db, CreateJwtOptions());

        var result = await authService.AuthenticateExternalUserAsync(new ExternalLoginUserDto(
            Id: Guid.NewGuid(),
            FirstNames: "Desconocido",
            PaternalLastName: "Prueba",
            MaternalLastName: null,
            GoogleEmail: null,
            MicrosoftEmail: "no.registrado@monclova.tecnm.mx"
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("no está dado de alta", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateExternalUserAsync_StudentWithActiveBlock_ReturnsForbiddenWithBlockReason()
    {
        using var db = CreateInMemoryDbContext();
        var authRepo = new AuthRepository(db);
        var authService = new AuthService(authRepo, db, CreateJwtOptions());

        var user = new User
        {
            Email = "bloqueado@monclova.tecnm.mx",
            Role = UserRole.Student,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var student = new Student
        {
            UserId = user.Id,
            ControlNumber = "20680200",
            FirstName = "Ana",
            LastName = "García",
            CareerId = 1,
            HasComplementaryActivities = true,
            HasSocialService = false,
            HasSpecialRequirements = true,
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        db.StudentBlocks.Add(new StudentBlock
        {
            StudentId = student.Id,
            Reason = "BLOQUEO AUTOMÁTICO: Falta liberar Servicio Social.",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await authService.AuthenticateExternalUserAsync(new ExternalLoginUserDto(
            Id: Guid.NewGuid(),
            FirstNames: "Ana",
            PaternalLastName: "García",
            MaternalLastName: null,
            GoogleEmail: null,
            MicrosoftEmail: "bloqueado@monclova.tecnm.mx"
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("Servicio Social", result.ErrorMessage);
    }

    [Fact]
    public async Task AuthenticateExternalUserAsync_StudentMissingRequirementsWithoutBlockRow_ReturnsForbidden()
    {
        using var db = CreateInMemoryDbContext();
        var authRepo = new AuthRepository(db);
        var authService = new AuthService(authRepo, db, CreateJwtOptions());

        var user = new User
        {
            Email = "sinreq@monclova.tecnm.mx",
            Role = UserRole.Student,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Students.Add(new Student
        {
            UserId = user.Id,
            ControlNumber = "20680201",
            FirstName = "Luis",
            LastName = "Mendoza",
            CareerId = 1,
            HasComplementaryActivities = false,
            HasSocialService = true,
            HasSpecialRequirements = true,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await authService.AuthenticateExternalUserAsync(new ExternalLoginUserDto(
            Id: Guid.NewGuid(),
            FirstNames: "Luis",
            PaternalLastName: "Mendoza",
            MaternalLastName: null,
            GoogleEmail: null,
            MicrosoftEmail: "sinreq@monclova.tecnm.mx"
        ));

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("Actividades Complementarias", result.ErrorMessage);
    }
}
