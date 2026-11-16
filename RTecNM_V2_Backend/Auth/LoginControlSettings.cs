namespace TecNM.Residency.Auth;

public class LoginControlSettings
{
    public const string SectionName = "LoginControl";

    public string ModuleId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = "http://localhost:5140";
    public string InternalBaseUrl { get; set; } = "http://localhost:5140";
    public string LoginPortalUrl { get; set; } = "http://localhost:5173/login";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5175";
}
