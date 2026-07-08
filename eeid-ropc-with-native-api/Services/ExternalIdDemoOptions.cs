namespace eeid_ropc_with_native_api.Services;

public class ExternalIdDemoOptions
{
    public const string SectionName = "ExternalIdDemo";

    public string TenantDomain { get; set; } = "sevillaeid.onmicrosoft.com";

    public string TenantSubdomain { get; set; } = "sevillaeid";

    public string NativeAuthClientId { get; set; } = string.Empty;

    public string NativeAuthCapabilities { get; set; } = "registration_required mfa_required";

    public string GraphClientId { get; set; } = string.Empty;

    public string GraphClientSecret { get; set; } = string.Empty;

    public string GraphScope { get; set; } = "https://graph.microsoft.com/.default";

    public string UserEmailDomain { get; set; } = "sevillaeid.onmicrosoft.com";
}