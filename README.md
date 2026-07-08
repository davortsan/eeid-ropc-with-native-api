# EEID Native Authentication Demo

## Overview

This project is an ASP.NET Core MVC proof of concept for Microsoft Entra External ID.

> IMPORTANT
> This transparent authentication approach is not recommended for real applications when the application itself controls or handles the user's username or email address and password.
> Use this project only for testing, validation, or isolated demo scenarios.

The application demonstrates a fully server-driven test flow that:

1. generates a random customer account,
2. creates that account in an External ID tenant through Microsoft Graph,
3. signs in with the generated email address and password by calling the Microsoft Entra Native Authentication API,
4. shows the outcome in the UI, including the generated credentials and the issued authentication token when sign-in succeeds.

The project is meant for testing and validation scenarios, not for production use as-is.

## What The Application Does

When the user clicks the button on the home page, the application performs the following steps:

1. Builds a random email address under the configured domain.
2. Builds a random strong password.
3. Requests an app-only Microsoft Graph access token.
4. Creates a local External ID customer user in the target tenant.
5. Starts a Native Authentication sign-in flow using the generated email address.
6. Selects the `password` challenge for the user.
7. Exchanges the password for tokens through the Native Authentication token endpoint.
8. Displays the result on a dedicated result page.

If authentication succeeds, the result page shows:

- the generated email address,
- the generated password,
- the created user ID,
- the authentication token issued by External ID.

If authentication fails, the result page shows:

- the generated email address,
- the generated password,
- the failure stage,
- the returned error,
- the returned suberror if present,
- the error description,
- trace and correlation IDs when available.

## Main Use Case

This repository is useful when you want to validate one or more of the following:

- Native Authentication is enabled correctly in a Microsoft Entra External ID tenant.
- The configured app registration can sign in through the raw Native Authentication endpoints.
- Microsoft Graph permissions are sufficient to create local customer users.
- Conditional Access or MFA policies are not unexpectedly blocking the native sign-in flow.
- A newly created local user can authenticate immediately with email and password.

## Project Structure

Key files and folders:

- `eeid-ropc-with-native-api/Program.cs`
	Registers MVC services, configuration binding, and the HTTP client used by the demo service.

- `eeid-ropc-with-native-api/Controllers/HomeController.cs`
	Exposes the home page and the `RunTransparentAuth` POST action that triggers the demo flow.

- `eeid-ropc-with-native-api/Services/ExternalIdDemoService.cs`
	Implements the end-to-end logic for Graph user creation and Native Authentication sign-in.

- `eeid-ropc-with-native-api/Services/ExternalIdDemoOptions.cs`
	Defines the configuration contract for tenant and app registration settings.

- `eeid-ropc-with-native-api/Models/TransparentAuthResultViewModel.cs`
	Holds the data displayed on the result page.

- `eeid-ropc-with-native-api/Views/Home/Index.cshtml`
	Contains the button that starts the flow.

- `eeid-ropc-with-native-api/Views/Home/TransparentAuthResult.cshtml`
	Displays the authentication result, credentials, and issued token.

- `eeid-ropc-with-native-api/appsettings.json`
	Contains the sample configuration for the demo.

## Sample Configuration

The project ships with placeholder values in `eeid-ropc-with-native-api/appsettings.json`.

```json
{
	"ExternalIdDemo": {
		"TenantDomain": "<YOUR_TENANT>.onmicrosoft.com",
		"TenantSubdomain": "<YOUR_TENANT>",
		"NativeAuthClientId": "<NATIVE_APP_CLIENT_ID>",
		"GraphClientId": "<GRAPH_APP_CLIENT_ID>",
		"GraphClientSecret": "<GRAPH_APP_CLIENT_SECRET>",
		"GraphScope": "https://graph.microsoft.com/.default",
		"UserEmailDomain": "<YOUR_TENANT>.onmicrosoft.com"
	}
}
```

Replace all placeholder values before running the demo.

## Native Authentication Flow Used

The application uses the email-and-password Native Authentication flow for Microsoft Entra External ID.

The flow is executed through these endpoints:

1. `oauth2/v2.0/initiate`
2. `oauth2/v2.0/challenge`
3. `oauth2/v2.0/token`

The application can optionally advertise Native Authentication capabilities through configuration.

If you need them, set `NativeAuthCapabilities` in configuration, for example:

- `registration_required`
- `mfa_required`

If the setting is empty or missing, the application doesn't send a `capabilities` parameter.

## Microsoft Graph Usage

The application uses Microsoft Graph to create a local customer account in the External ID tenant.

It creates a user with:

- `identities` containing an `emailAddress` sign-in type,
- a generated password,
- `DisablePasswordExpiration` password policy,
- a generated email in the configured tenant domain.

## Configuration

Configuration is stored under the `ExternalIdDemo` section in `eeid-ropc-with-native-api/appsettings.json`.

Current settings:

- `TenantDomain`
	The primary Entra External ID tenant domain, for example `contoso.onmicrosoft.com`.

- `TenantSubdomain`
	The tenant subdomain used in `ciamlogin.com`, for example `contoso`.

- `NativeAuthClientId`
	The client ID of the app registration used to call the Native Authentication endpoints.

- `NativeAuthCapabilities`
	Optional capability flags sent to the Native Authentication initiate endpoint. If omitted, no capabilities parameter is sent.

- `GraphClientId`
	The client ID of the confidential app used to request a Microsoft Graph access token. This can be different from the Native Authentication app registration.

- `GraphClientSecret`
	The client secret for the Graph app registration.

- `GraphScope`
	The scope used for app-only Graph access. This is typically `https://graph.microsoft.com/.default`.

- `UserEmailDomain`
	The domain used to generate random customer email addresses.

## Required Entra Setup

Before running the demo, make sure the tenant is configured correctly.

### External ID Tenant

- You need a Microsoft Entra External ID tenant.
- The tenant must support email and password as the sign-in method for the targeted user flow.

### Native Authentication App Registration

- The app must exist in the same tenant.
- Native Authentication must be enabled.
- Public client flows must be enabled if required by your configuration.
- The app must be associated with the correct sign-up/sign-in user flow.

### Microsoft Graph App Registration

- The app must be able to obtain an app-only token.
- It must have sufficient Microsoft Graph application permissions to create users.
- Admin consent must be granted.
- It must have a valid client secret configured in the application settings.

Typical permissions include one of these:

- `User.Create`
- `User.ReadWrite.All`
- `Directory.ReadWrite.All`

Use the least privileged permission that satisfies your test scenario.

### Conditional Access And MFA

Conditional Access can block this flow.

If the tenant requires MFA enrollment or MFA challenge completion for this app, the test can fail even if the code is correct. In that case, you must either:

- exclude the app registration from the blocking Conditional Access policy,
- change the tenant policy,
- or implement an interactive MFA-capable registration flow instead of a fully transparent sign-in.

## Running The Project

From the repository root:

```powershell
Set-Location .\eeid-ropc-with-native-api
dotnet run
```

The default local URLs are defined in `Properties/launchSettings.json`.

## Validating The Build

To verify that the project compiles:

```powershell
dotnet build .\eeid-ropc-with-native-api\eeid-ropc-with-native-api.csproj
```

## Expected Result

On a successful run, the UI should show:

- `OK`
- the random email address,
- the random password,
- the created user ID,
- the authentication token returned by EEID.

On a failed run, the UI should show enough diagnostic detail to identify whether the problem comes from:

- Graph permissions,
- Native Authentication configuration,
- tenant sign-in policy,
- Conditional Access,
- MFA registration requirements,
- invalid client or tenant settings.

## Troubleshooting

### Placeholder Configuration

If `appsettings.json` still contains placeholder values such as `<YOUR_TENANT>`, `<NATIVE_APP_CLIENT_ID>`, or `<GRAPH_APP_CLIENT_SECRET>`, the application now fails early with a configuration error instead of making invalid HTTP calls.

### HTML Instead Of JSON

If an endpoint returns HTML instead of JSON, the application now reports a clearer `invalid_response_format` error. In practice, this usually means one of these:

- the tenant domain or subdomain is wrong,
- the client ID is invalid for the target tenant,
- the request is hitting an unexpected endpoint,
- or configuration placeholders were not replaced.

### Conditional Access

If you see MFA enrollment or Conditional Access related errors, verify whether the app registration used by Native Authentication is included in any blocking policy. In the validated scenario for this project, excluding the Native Authentication app registration from the relevant Conditional Access policy allowed the transparent sign-in flow to complete successfully.

## Security Notes

This project currently displays sensitive runtime values for debugging purposes, including:

- generated passwords,
- authentication tokens,
- Graph app configuration from local settings.

That is acceptable for a controlled lab or proof-of-concept, but it is not appropriate for production.

Recommended next steps if you evolve this project:

1. Move secrets out of `appsettings.json` into user secrets, environment variables, or Azure Key Vault.
2. Avoid rendering raw tokens in the UI except in dedicated diagnostic environments.
3. Add automatic cleanup for test users created during execution.
4. Add structured logging around the exact stage of failure.

## Reference Scenario

In its current form, this project is designed as a practical diagnostic harness for Microsoft Entra External ID native email/password sign-in. It is especially useful when you need to answer a simple question quickly:

Can this tenant create a local user and authenticate that user through Native Authentication without browser interaction?
