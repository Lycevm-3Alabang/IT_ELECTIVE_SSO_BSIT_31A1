# IT ELECTIVE - SSO (BSIT 31A1)

A centralized Single Sign-On (SSO) Gateway. A user logs in once at the Gateway, and the Gateway sends them back to the client app they came from with a signed JWT that carries their email, groups, and access levels.

Documentation for developers who want to integrate an app lives in `docs/`.

## Projects in this solution

| Project | Type / Role | Default URL | 
| ----- | ----- | ----- | 
| **Gateway** | ASP.NET Core MVC (SSO Gateway: login page, admin area, JWT creation) | `https://localhost:7281` | 
| **MVC** | ASP.NET Core MVC (Client app #1 `MvcClientApp`: Login with SSO, Profile, Admin-only page) | `https://localhost:7180` | 
| **SsoClientApi** | ASP.NET Core Minimal API (Client app #2 `MockClientApp`: validates JWT, exposes `GET /api/userinfo`) | `https://localhost:7080` | 
| **Data** | Class library (`SsoDbContext`, migrations, `SeedData`, `AuditService`, `ReturnUrlValidator`) | — | 
| **Models** | Class library Entities (`ApplicationUser`, `TenantApp`, `Group`, `UserGroup`, `AuditLog`) | — | 
| **Tests** | xUnit (Unit and flow tests) | — | 

**Requirements:** .NET 10 SDK and either Visual Studio 2022+ or the `dotnet` CLI. The database is SQLite, so no database server is needed.

## Quick start (copy and paste)

Do this once on every PC you run the project on. The secrets are stored in your user profile, not in the repository, so they do not travel with a git clone, a zip, or a push.

### 1. Set the secrets

Open PowerShell in the solution folder (the one that contains `ITELECTIVE_SSO.slnx`) and run all four commands:

```
dotnet user-secrets set "JwtSettings:SecretKey" "pj4HCnNVNK9MZUz4GHILqKiPbe380GHze5GPx813kNUUFvNGBnbAX7Nvk1bsG4N8" --project Gateway
dotnet user-secrets set "Sso:SecretKey" "pj4HCnNVNK9MZUz4GHILqKiPbe380GHze5GPx813kNUUFvNGBnbAX7Nvk1bsG4N8" --project MVC
dotnet user-secrets set "Sso:SecretKey" "pj4HCnNVNK9MZUz4GHILqKiPbe380GHze5GPx813kNUUFvNGBnbAX7Nvk1bsG4N8" --project SsoClientApi
dotnet user-secrets set "DefaultAdmin:Password" "User@12345" --project Gateway

```

> **Why three keys?** The Gateway signs tokens with `JwtSettings:SecretKey`, and each client app verifies them with `Sso:SecretKey`. All three values must be identical, otherwise clients reject the token as `invalid_token`.
>
> *On macOS, Linux, or Git Bash, use single quotes instead of double quotes (the `!` in passwords breaks double-quoted strings in those shells).*

Check that it worked:

```
dotnet user-secrets list --project Gateway
dotnet user-secrets list --project MVC
dotnet user-secrets list --project SsoClientApi

```

You should see `JwtSettings:SecretKey` and `DefaultAdmin:Password` for Gateway, and `Sso:SecretKey` for the other two.

### 2. Trust the HTTPS dev certificate (first time only)

```
dotnet dev-certs https --trust

```

### 3. Run the apps

The Gateway must be running before any client app. Use three terminals:

```
dotnet run --project Gateway --launch-profile https
dotnet run --project MVC --launch-profile https
dotnet run --project SsoClientApi --launch-profile https

```

You can also press **F5** in Visual Studio instead (see *Running the apps together*). Either way works. You do not have to use the terminal, as long as Step 1 was done on that PC.

### 4. Log in

Open the Gateway directly or navigate via the client apps:

* **Gateway URL:** `https://localhost:7281`

Use one of the pre-configured accounts:

| Role | Username / Email | Password | 
| ----- | ----- | ----- | 
| **Admin User** | `user@user.local` | `User@12345` | 
| **Student User** | `student1@test.com` | `Temp@67890` | 

Then open `https://localhost:7180`, click **Login with SSO**, and sign in with an account above.

> **Classroom use only:** The keys and passwords above are shared on purpose so everyone can run the project. Never reuse them for production environments.

## Configuration reference

| Setting | Where it is set | Notes | 
| ----- | ----- | ----- | 
| `JwtSettings:SecretKey` | user-secrets, Gateway | At least 32 characters. The Gateway refuses to start without it. | 
| `Sso:SecretKey` | user-secrets, MVC & SsoClientApi | Must equal the Gateway's `JwtSettings:SecretKey`. | 
| `DefaultAdmin:Password` | user-secrets, Gateway | Overrides default admin password in settings. | 
| `DefaultAdmin:Email` | `Gateway/appsettings.Development.json` | `user@user.local` | 
| `JwtSettings:Issuer` / `Audience` / `ExpiryHours` | `Gateway/appsettings.json` | `SSOGateway` / `SSOClientApps` / `9` | 
| `Sso:BaseUrl`, `ClientBaseUrl`, `AppName`, `Issuer`, `Audience` | each client's `appsettings.json` | Pre-configured to match default ports. | 
| `Cors:AllowedOrigins` | `Gateway/appsettings*.json` | Origins of the client apps. | 

*Note: User-secrets are only loaded in the Development environment. Do not set `ASPNETCORE_ENVIRONMENT=Production` when running locally.*

## Database

When the Gateway starts, it automatically applies migrations (`Database.MigrateAsync()`) and seeds initial database configurations.

The pre-existing seed includes:

* The **Admin** role and default accounts (`user@user.local`, `student1@test.com`).

* A `TestApp` tenant app.

* The `MvcClientApp` tenant app (return URL `https://localhost:7180/callback`) with groups `MvcClientApp-Admin` (level 0) and `MvcClientApp-Users` (level 1).

## Running the apps together

The Gateway must be running before any client app because clients send the browser to it for authentication.

### Option A: Visual Studio

1. Right-click the solution, then select **Configure Startup Projects**.

2. Choose **Multiple startup projects** and set **Gateway** and **MVC** to *Start* (add **SsoClientApi** too if you want the API client).

3. Select the `https` launch profile for each, then press **F5**.

### Option B: Terminals

See **Step 3** of the Quick Start section.

## Ports

| App | HTTPS | HTTP | 
| ----- | ----- | ----- | 
| **Gateway** | `7281` | `5099` | 
| **MVC** | `7180` | `5345` | 
| **SsoClientApi** | `7080` | `5245` | 

## Trying the full SSO flow

### MVC client

1. Open `https://localhost:7180` and click **Login with SSO**.

2. You are sent to the Gateway login page (*"Login to continue to MvcClientApp"*).

3. Sign in with the admin account (`user@user.local` / `User@12345`).

4. The Gateway redirects to `https://localhost:7180/callback?token=<jwt>`. The MVC app validates the token, creates its session, and shows the signed-in page.

5. The admin belongs to the level-0 group, so the **Admin-only page** opens.

### SsoClientApi (mock client)

The mock API uses the app name `MockClientApp` with callback `https://localhost:7080/callback`. If registering a new tenant:

1. Log in to the Gateway at `https://localhost:7281` as admin, open **Apps** (`/Admin/TenantApps`), and create an app with Name `MockClientApp` and Return URL `https://localhost:7080/callback`.

2. Open **Groups** and create a group for it (e.g., `Admin`, level `0`, saved as `MockClientApp-Admin`).

3. Open user details and assign them to that group.

4. Open `https://localhost:7080` and click **Login with SSO**.

After the callback, the token is saved in an `HttpOnly` cookie named `sso_token`. Call the protected endpoint from the browser or with the bearer token:

```
GET https://localhost:7080/api/userinfo
Authorization: Bearer <jwt>

```

**Example response:**

```
{
  "sub": "3f2b8c7e-....",
  "email": "user@user.local",
  "tenantApp": "MockClientApp",
  "groups": ["MockClientApp-Admin"],
  "levels": { "MockClientApp-Admin": 0 },
  "expiresAt": "2026-10-06T22:00:00+00:00"
}

```

With no token or an expired one, the API answers `401` with a JSON response:
`{ "error": "token_expired", "message": "...", "loginUrl": "/login" }`

## Enforced Security Controls

* **App Verification:** Only registered, active apps can use the login page. An unknown return URL displays *"Unapproved External App"* and logs an audit event.

* **Account Suspension:** Inactive/suspended accounts cannot log in and see *"Account Suspended"*.

* **Lockout Policy:** 5 failed password attempts lock the account for 15 minutes.

* **App Isolation:** A token issued for one app is rejected by another app (verified via `tenant_app` claim).

* **Token Expiry:** Tokens expire in 9 hours (`JwtSettings:ExpiryHours`).

* **Password Reset:** New or reset users are required to change temporary passwords upon next login.

## Documentation

* `docs/README.md`: Overview of the SSO system and how to integrate an app.

* `docs/architecture.md`: System diagram, login flow, data model, and security controls.

* `jwt-claims.md`: Token structure and validation specifications for client apps.

## Running the tests

```
dotnet test

```

*Note: Tests use an internal built-in key and do not depend on `user-secrets`.*

## Troubleshooting

| Problem | Likely Cause / Solution | 
| ----- | ----- | 
| **`SecretKey` is missing on startup** | Step 1 was skipped for that project, or the app is not running in the `Development` environment. | 
| **Cannot log in with credentials** | Ensure you are using `User@12345` for `user@user.local` or `Temp@67890` for `student1@test.com`. | 
| **Client says `invalid_token`** | The client's `Sso:SecretKey`, `Issuer`, or `Audience` does not match the Gateway's `JwtSettings`. | 
| **"Unapproved External App" page** | The app is not registered, is disabled, or its Return URL differs from the callback URL. | 
| **Client says `wrong_app`** | `Sso:AppName` does not match the tenant app name registered in the Gateway. | 
| **Logged in but no groups or levels** | The user is not assigned to any group within that specific application. | 
| **Redirect or CORS problems** | Verified ports differ from the default table. Update `Sso:BaseUrl`, `Sso:ClientBaseUrl`, and `Cors:AllowedOrigins`. | 
| **Browser HTTPS warning** | Run `dotnet dev-certs https --trust`. | 
