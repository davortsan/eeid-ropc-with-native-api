# EEID-ROPC-with-Native-API

Prueba ASP.NET Core MVC para Microsoft Entra External ID que:

1. crea un usuario aleatorio en el tenant mediante Microsoft Graph,
2. intenta autenticarse contra Native Authentication API con email y password,
3. muestra el resultado junto con las credenciales usadas.

## Configuración necesaria

Completa la sección `ExternalIdDemo` en `appsettings.Development.json`:

- `TenantDomain`: dominio principal del tenant, por ejemplo `sevillaeid.onmicrosoft.com`.
- `TenantSubdomain`: subdominio usado en `ciamlogin.com`, por ejemplo `sevillaeid`.
- `NativeAuthClientId`: client ID de una app registrada como cliente público y habilitada para Native Authentication.
- `GraphClientId`: client ID de una app confidencial para llamar a Microsoft Graph.
- `GraphClientSecret`: secreto de la app confidencial de Graph.
- `UserEmailDomain`: dominio que se usará para generar el email aleatorio del usuario.

## Requisitos de Entra

- La app de Native Auth debe estar asociada al user flow de sign-in/sign-up del tenant External ID.
- La app de Native Auth debe tener habilitados los flujos de cliente público y Native Authentication.
- La app usada para Graph debe tener permisos de aplicación para crear usuarios, por ejemplo `User.Create` o `User.ReadWrite.All`, con admin consent.
- El método de autenticación del tenant debe permitir email con password para que el flujo `password redirect` funcione.

## Validación local

```powershell
dotnet build .\eeid-ropc-with-native-api\eeid-ropc-with-native-api.csproj
```
