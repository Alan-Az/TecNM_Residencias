using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public record ExternalLoginUserDto(
    Guid Id,
    string FirstNames,
    string PaternalLastName,
    string? MaternalLastName,
    string? GoogleEmail,
    string? MicrosoftEmail
);

public record SsoExchangeRequestDto(string Code, string? State = null);

public record SsoPublicConfigDto(
    string LoginPortalUrl,
    string PublicBaseUrl,
    string ModuleId,
    string FrontendBaseUrl
);

public interface IAuthService
{
    Task<Result<AuthTokenResponseDto>> LoginAsync(LoginRequestDto dto);
    Task<Result<AuthTokenResponseDto>> AuthenticateExternalUserAsync(ExternalLoginUserDto externalUser);
    Task<Result<AuthTokenResponseDto>> ExchangeLoginControlCodeAsync(string code);
    Task<Dictionary<long, string>> GetUserDisplayNamesByIdsAsync(List<long> userIds);
}
