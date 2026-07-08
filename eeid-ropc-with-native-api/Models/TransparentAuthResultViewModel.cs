namespace eeid_ropc_with_native_api.Models;

public class TransparentAuthResultViewModel
{
    public bool Success { get; set; }

    public string? AuthenticationToken { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? Error { get; set; }

    public string? ErrorDescription { get; set; }

    public string? SubError { get; set; }

    public string? Stage { get; set; }

    public string? TraceId { get; set; }

    public string? CorrelationId { get; set; }

    public string? UserId { get; set; }
}