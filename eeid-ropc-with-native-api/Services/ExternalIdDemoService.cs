using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using eeid_ropc_with_native_api.Models;
using Microsoft.Extensions.Options;

namespace eeid_ropc_with_native_api.Services;

public interface IExternalIdDemoService
{
    Task<TransparentAuthResultViewModel> ExecuteAsync(CancellationToken cancellationToken);
}

public class ExternalIdDemoService : IExternalIdDemoService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ExternalIdDemoOptions _options;
    private readonly ILogger<ExternalIdDemoService> _logger;

    public ExternalIdDemoService(
        HttpClient httpClient,
        IOptions<ExternalIdDemoOptions> options,
        ILogger<ExternalIdDemoService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TransparentAuthResultViewModel> ExecuteAsync(CancellationToken cancellationToken)
    {
        var email = BuildRandomEmail();
        var password = BuildRandomPassword();

        try
        {
            ValidateConfiguration();

            var graphToken = await AcquireGraphAccessTokenAsync(cancellationToken);
            var userId = await CreateCustomerUserAsync(graphToken, email, password, cancellationToken);
            var authenticationToken = await SignInWithNativeAuthAsync(email, password, cancellationToken);

            return new TransparentAuthResultViewModel
            {
                Success = true,
                AuthenticationToken = authenticationToken,
                Email = email,
                Password = password,
                Stage = "token",
                UserId = userId
            };
        }
        catch (ExternalIdDemoException ex)
        {
            _logger.LogWarning(ex, "External ID demo failed during {Stage}", ex.Stage);

            return new TransparentAuthResultViewModel
            {
                Success = false,
                Email = email,
                Password = password,
                Error = ex.Error,
                ErrorDescription = ex.ErrorDescription,
                SubError = ex.SubError,
                Stage = ex.Stage,
                TraceId = ex.TraceId,
                CorrelationId = ex.CorrelationId,
                UserId = ex.UserId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure while executing External ID demo");

            return new TransparentAuthResultViewModel
            {
                Success = false,
                Email = email,
                Password = password,
                Error = "unexpected_error",
                ErrorDescription = ex.Message,
                Stage = "unexpected"
            };
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantDomain) ||
            string.IsNullOrWhiteSpace(_options.TenantSubdomain) ||
            string.IsNullOrWhiteSpace(_options.NativeAuthClientId) ||
            string.IsNullOrWhiteSpace(_options.GraphClientId) ||
            string.IsNullOrWhiteSpace(_options.GraphClientSecret) ||
            string.IsNullOrWhiteSpace(_options.UserEmailDomain))
        {
            throw new ExternalIdDemoException(
                stage: "configuration",
                error: "missing_configuration",
                errorDescription: "Completa la sección ExternalIdDemo en appsettings con TenantDomain, TenantSubdomain, NativeAuthClientId, GraphClientId, GraphClientSecret y UserEmailDomain.");
        }

        if (ContainsPlaceholder(_options.TenantDomain) ||
            ContainsPlaceholder(_options.TenantSubdomain) ||
            ContainsPlaceholder(_options.NativeAuthClientId) ||
            ContainsPlaceholder(_options.GraphClientId) ||
            ContainsPlaceholder(_options.GraphClientSecret) ||
            ContainsPlaceholder(_options.UserEmailDomain))
        {
            throw new ExternalIdDemoException(
                stage: "configuration",
                error: "placeholder_configuration",
                errorDescription: "appsettings.json still contains placeholder values. Replace <YOUR_DOMAIN>, NATIVE_AUTH_APP_CLIENT_ID, and NATIVE_AUTH_APP_CLIENT_SECRET with real tenant and app registration values.");
        }
    }

    private async Task<string> AcquireGraphAccessTokenAsync(CancellationToken cancellationToken)
    {
        var endpoint = $"https://login.microsoftonline.com/{_options.TenantDomain}/oauth2/v2.0/token";
        using var document = await PostFormAsync(
            endpoint,
            new Dictionary<string, string>
            {
                ["client_id"] = _options.GraphClientId,
                ["client_secret"] = _options.GraphClientSecret,
                ["grant_type"] = "client_credentials",
                ["scope"] = _options.GraphScope
            },
            stage: "graph_token",
            cancellationToken);

        return GetRequiredString(document.RootElement, "access_token", "graph_token");
    }

    private async Task<string> CreateCustomerUserAsync(
        string graphAccessToken,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://graph.microsoft.com/v1.0/users")
        {
            Content = new StringContent(BuildUserPayload(email, password), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", graphAccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateExceptionFromBody("graph_create_user", response.StatusCode, body);
        }

        using var document = JsonDocument.Parse(body);
        return GetRequiredString(document.RootElement, "id", "graph_create_user");
    }

    private async Task<string> SignInWithNativeAuthAsync(string email, string password, CancellationToken cancellationToken)
    {
        ExternalIdDemoException? lastException = null;

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                return await SignInWithNativeAuthOnceAsync(email, password, cancellationToken);
            }
            catch (ExternalIdDemoException ex) when (attempt < 4 && ex.Error is "user_not_found")
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(700 * attempt), cancellationToken);
            }
        }

        throw lastException ?? new ExternalIdDemoException("native_auth", "sign_in_failed", "No se pudo completar el inicio de sesión nativo.");
    }

    private async Task<string> SignInWithNativeAuthOnceAsync(string email, string password, CancellationToken cancellationToken)
    {
        var initiateUrl = BuildNativeAuthUrl("oauth2/v2.0/initiate");
        using var initiateDocument = await PostFormAsync(
            initiateUrl,
            BuildInitiateRequest(email),
            stage: "initiate",
            cancellationToken);

        var initiateToken = EnsureContinuationToken(initiateDocument.RootElement, "initiate");
        ThrowIfRedirect(initiateDocument.RootElement, "initiate");

        var challengeUrl = BuildNativeAuthUrl("oauth2/v2.0/challenge");
        using var challengeDocument = await PostFormAsync(
            challengeUrl,
            new Dictionary<string, string>
            {
                ["client_id"] = _options.NativeAuthClientId,
                ["challenge_type"] = "password redirect",
                ["continuation_token"] = initiateToken
            },
            stage: "challenge",
            cancellationToken);

        ThrowIfRedirect(challengeDocument.RootElement, "challenge");
        var selectedChallenge = challengeDocument.RootElement.TryGetProperty("challenge_type", out var challengeTypeElement)
            ? challengeTypeElement.GetString()
            : null;

        if (!string.Equals(selectedChallenge, "password", StringComparison.OrdinalIgnoreCase))
        {
            throw new ExternalIdDemoException(
                stage: "challenge",
                error: "unexpected_challenge_type",
                errorDescription: $"Se esperaba challenge_type=password y se recibió '{selectedChallenge ?? "null"}'.");
        }

        var challengeToken = EnsureContinuationToken(challengeDocument.RootElement, "challenge");

        var tokenUrl = BuildNativeAuthUrl("oauth2/v2.0/token");
        using var tokenDocument = await PostFormAsync(
            tokenUrl,
            new Dictionary<string, string>
            {
                ["client_id"] = _options.NativeAuthClientId,
                ["continuation_token"] = challengeToken,
                ["grant_type"] = "password",
                ["password"] = password,
                ["scope"] = "openid profile offline_access"
            },
            stage: "token",
            cancellationToken);

        ThrowIfRedirect(tokenDocument.RootElement, "token");
        return GetRequiredString(tokenDocument.RootElement, "id_token", "token");
    }

    private async Task<JsonDocument> PostFormAsync(
        string url,
        IReadOnlyDictionary<string, string> formValues,
        string stage,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(formValues)
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateExceptionFromBody(stage, response.StatusCode, body);
        }

        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
            var bodyPreview = body.Length > 240 ? body[..240] : body;

            throw new ExternalIdDemoException(
                stage: stage,
                error: "invalid_response_format",
                errorDescription: $"Expected JSON from '{url}', but received '{contentType}'. Response starts with: {bodyPreview}",
                innerException: ex);
        }
    }

    private Dictionary<string, string> BuildInitiateRequest(string email)
    {
        var formValues = new Dictionary<string, string>
        {
            ["client_id"] = _options.NativeAuthClientId,
            ["challenge_type"] = "password redirect",
            ["username"] = email
        };

        if (!string.IsNullOrWhiteSpace(_options.NativeAuthCapabilities))
        {
            formValues["capabilities"] = _options.NativeAuthCapabilities;
        }

        return formValues;
    }

    private void ThrowIfRedirect(JsonElement root, string stage)
    {
        if (!root.TryGetProperty("challenge_type", out var challengeTypeElement))
        {
            return;
        }

        var challengeType = challengeTypeElement.GetString();
        if (!string.Equals(challengeType, "redirect", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var redirectReason = root.TryGetProperty("redirect_reason", out var redirectReasonElement)
            ? redirectReasonElement.GetString()
            : null;

        throw new ExternalIdDemoException(
            stage: stage,
            error: "redirect_required",
            errorDescription: redirectReason ?? "Microsoft Entra exige desviar el flujo a autenticación web.");
    }

    private static string EnsureContinuationToken(JsonElement root, string stage)
    {
        return GetRequiredString(root, "continuation_token", stage);
    }

    private static string GetRequiredString(JsonElement root, string propertyName, string stage)
    {
        if (root.TryGetProperty(propertyName, out var property) && property.GetString() is { Length: > 0 } value)
        {
            return value;
        }

        throw new ExternalIdDemoException(stage, "missing_property", $"La respuesta no contiene la propiedad requerida '{propertyName}'.");
    }

    private ExternalIdDemoException CreateExceptionFromBody(string stage, HttpStatusCode statusCode, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            return new ExternalIdDemoException(
                stage: stage,
                error: TryGetString(root, "error") ?? $"http_{(int)statusCode}",
                errorDescription: TryGetString(root, "error_description") ?? body,
                subError: TryGetString(root, "suberror"),
                traceId: TryGetString(root, "trace_id"),
                correlationId: TryGetString(root, "correlation_id"),
                userId: TryGetString(root, "id"));
        }
        catch (JsonException)
        {
            return new ExternalIdDemoException(
                stage: stage,
                error: $"http_{(int)statusCode}",
                errorDescription: string.IsNullOrWhiteSpace(body) ? "Sin contenido en la respuesta." : body);
        }
    }

    private string BuildNativeAuthUrl(string relativePath)
    {
        return $"https://{_options.TenantSubdomain}.ciamlogin.com/{_options.TenantDomain}/{relativePath}";
    }

    private string BuildUserPayload(string email, string password)
    {
        var payload = new
        {
            accountEnabled = true,
            displayName = $"Native Auth Demo {Guid.NewGuid():N}"[..24],
            identities = new[]
            {
                new
                {
                    signInType = "emailAddress",
                    issuer = _options.TenantDomain,
                    issuerAssignedId = email
                }
            },
            mail = email,
            passwordProfile = new
            {
                password,
                forceChangePasswordNextSignIn = false
            },
            passwordPolicies = "DisablePasswordExpiration"
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private string BuildRandomEmail()
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        return $"nativeauth-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{suffix}@{_options.UserEmailDomain}";
    }

    private static string BuildRandomPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@$?_+-=";
        var all = upper + lower + digits + symbols;

        Span<char> buffer = stackalloc char[18];
        buffer[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        buffer[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        buffer[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        buffer[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

        for (var i = 4; i < buffer.Length; i++)
        {
            buffer[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[swapIndex]) = (buffer[swapIndex], buffer[i]);
        }

        return new string(buffer);
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) ? property.GetString() : null;
    }

    private static bool ContainsPlaceholder(string value)
    {
        return value.Contains("<YOUR_DOMAIN>", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("NATIVE_AUTH_APP_CLIENT_ID", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("NATIVE_AUTH_APP_CLIENT_SECRET", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ExternalIdDemoException : Exception
    {
        public ExternalIdDemoException(
            string stage,
            string error,
            string errorDescription,
            string? subError = null,
            string? traceId = null,
            string? correlationId = null,
            string? userId = null,
            Exception? innerException = null)
            : base(errorDescription, innerException)
        {
            Stage = stage;
            Error = error;
            ErrorDescription = errorDescription;
            SubError = subError;
            TraceId = traceId;
            CorrelationId = correlationId;
            UserId = userId;
        }

        public string Stage { get; }

        public string Error { get; }

        public string ErrorDescription { get; }

        public string? SubError { get; }

        public string? TraceId { get; }

        public string? CorrelationId { get; }

        public string? UserId { get; }
    }
}