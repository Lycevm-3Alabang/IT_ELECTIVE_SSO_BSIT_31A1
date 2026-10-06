# IT ELECTIVE - SSO (BSIT 31A1)

A centralized Single Sign-On (SSO) Gateway. A user logs in once at the Gateway, and the Gateway sends them back to the client app they came from with a signed JWT that carries their email, groups, and access levels.

## Projects in this solution

| Project | Type | Role | Default URL |
|---|---|---|---|
| `Gateway` | ASP.NET Core MVC | SSO Gateway: login page, admin area, JWT creation | https://localhost:7281 |
| `MVC` | ASP.NET Core MVC | Client app #1 (`MvcClientApp`): Login with SSO, Profile, Admin-only page | https://localhost:7180 |
| `SsoClientApi` | ASP.NET Core minimal API | Client app #2 (`MockClientApp`): validates the JWT, exposes `GET /api/userinfo` | https://localhost:7080 |
| `Data` | Class library | `SsoDbContext`, migrations, `SeedData`, `AuditService`, `ReturnUrlValidator` | - |
| `Models` | Class library | Entities (`ApplicationUser`, `TenantApp`, `Group`, `UserGroup`, `AuditLog`) | - |
| `Tests` | xUnit | Unit and flow tests | - |

Requirements: .NET 10 SDK, Visual Studio 2022+ (or the `dotnet` CLI). The database is SQLite, so no database server is needed.

## 1. One-time setup

### 1.1 Restore and build

```
dotnet restore
dotnet build
```

### 1.2 Set the JWT signing key (required)

The Gateway signs tokens with a secret key, and every client app must use the **same** key to verify them. The key is NOT stored in `appsettings.json`; it goes in user-secrets. It must be at least 32 characters.

Pick one long random value and set it in all three projects:

```
dotnet user-secrets set "JwtSettings:SecretKey" "<your-long-random-value>" --project Gateway
dotnet user-secrets set "Sso:SecretKey"         "<your-long-random-value>" --project MVC
dotnet user-secrets set "Sso:SecretKey"         "<your-long-random-value>" --project SsoClientApi
```

If a project starts and says `SecretKey is missing`, this step was skipped or the values differ.

### 1.3 Set the default admin account

The Gateway creates an admin account on first run from the `DefaultAdmin` section. Set the password in user-secrets (the email is already in `Gateway/appsettings.Development.json`):

```
dotnet user-secrets set "DefaultAdmin:Password" "<admin-password>" --project Gateway
```

Check that the email in `appsettings.Development.json` (`DefaultAdmin:Email`) is the one you want to log in with.

### 1.4 Database

Nothing to do. When the Gateway starts it applies migrations (`Database.MigrateAsync()`), creates `Data/ssodatabase.db`, and runs `SeedData`. The seed is safe to run repeatedly.

On first run the seed creates:

- the `Admin` role and the default admin user
- a `TestApp` tenant app
- the `MvcClientApp` tenant app (return URL `https://localhost:7180/callback`) with groups `MvcClientApp-Admin` (level 0) and `MvcClientApp-Users` (level 1), both assigned to the default admin

## 2. Running the apps together

The Gateway must be running before any client app, because clients send the browser to it to log in.

### Option A: Visual Studio

1. Right-click the solution, then **Configure Startup Projects**.
2. Choose **Multiple startup projects** and set `Gateway` and `MVC` to **Start** (add `SsoClientApi` too if you want the API client). The repo's `ITELECTIVE_SSO.slnLaunch.user` already does this for Gateway + MVC.
3. Select the **https** launch profile for each, then press F5.

### Option B: Three terminals

```
dotnet run --project Gateway --launch-profile https
dotnet run --project MVC --launch-profile https
dotnet run --project SsoClientApi --launch-profile https
```

The `https` profiles use these ports. The values in each client's `appsettings.json` (`Sso:BaseUrl`, `Sso:ClientBaseUrl`) already match them.

| App | https | http |
|---|---|---|
| Gateway | 7281 | 5099 |
| MVC | 7180 | 5345 |
| SsoClientApi | 7080 | 5245 |

Trust the dev certificate once if the browser warns about HTTPS: `dotnet dev-certs https --trust`.

## 3. Trying the full SSO flow

### 3.1 MVC client

1. Open https://localhost:7180 and click **Login with SSO**.
2. You are sent to the Gateway login page (it says "Login to continue to MvcClientApp").
3. Sign in with the default admin account.
4. The Gateway redirects to `https://localhost:7180/callback?token=<jwt>`. The MVC app validates the token, creates its session, and shows the signed-in page.
5. The admin belongs to the level-0 group, so the Admin-only page opens. A user in a level-1 group does not get in.

### 3.2 SsoClientApi (mock client)

The mock API uses the app name `MockClientApp` with callback `https://localhost:7080/callback`. It is **not** created by the seed, so register it once:

1. Log in to the Gateway at https://localhost:7281 as the admin, then open **Apps** (`/Admin/TenantApps`) and create an app with Name `MockClientApp` and Return URL `https://localhost:7080/callback`.
2. Open **Groups** and create a group for it (for example `Admin`, level `0`, which is saved as `MockClientApp-Admin`).
3. Open the admin user's details and assign them to that group.
4. Open https://localhost:7080 and click **Login with SSO**.

After the callback, the token is kept in an HttpOnly cookie named `sso_token`. Call the protected endpoint from the browser (cookie) or with the token directly:

```
GET https://localhost:7080/api/userinfo
Authorization: Bearer <jwt>
```

Example response:

```json
{
  "sub": "3f2b8c7e-....",
  "email": "user@user.local",
  "tenantApp": "MockClientApp",
  "groups": ["MockClientApp-Admin"],
  "levels": { "MockClientApp-Admin": 0 },
  "expiresAt": "2026-10-06T22:00:00+00:00"
}
```

With no token, or an expired one, the API answers `401` with a JSON body like `{ "error": "token_expired", "message": "...", "loginUrl": "/login" }`. Expired tokens also send a `Token-Expired: true` header.

## 4. Things that are enforced

- Only registered, active apps can use the login page. An unknown return URL shows "Unapproved External App" and is written to the audit log.
- Suspended (inactive) accounts cannot log in and see "Account Suspended".
- 5 failed password attempts lock the account for 15 minutes.
- A token issued for one app is rejected by another app (the `tenant_app` claim is checked).
- Tokens last 9 hours (`JwtSettings:ExpiryHours`).
- New users and admin-reset users get a temporary password and must change it at the next login.

## 5. Token format

See [docs/jwt-claims.md](docs/jwt-claims.md).

## 6. Running the tests

```
dotnet test
```

## 7. Troubleshooting

| Problem | Likely cause |
|---|---|
| `SecretKey is missing` on startup | The user-secrets step (1.2) was skipped for that project |
| Client says `invalid_token` | The client's `Sso:SecretKey`, `Issuer`, or `Audience` differs from the Gateway's `JwtSettings` |
| "Unapproved External App" page | The app is not registered, is disabled, or its Return URL differs from the callback URL |
| Client says `wrong_app` | `Sso:AppName` does not match the tenant app name in the Gateway |
| Logged in but no groups or levels | The user is not assigned to any group of that app |
| Redirect or CORS problems | Ports differ from the table in section 2; update `Sso:BaseUrl`, `Sso:ClientBaseUrl`, and `Cors:AllowedOrigins` |
