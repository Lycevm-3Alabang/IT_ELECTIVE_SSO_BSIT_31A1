# IT ELECTIVE - SSO: Documentation

This folder documents the SSO Gateway for developers who want to connect their own app to it, whatever stack the app uses (ASP.NET Core, PHP, Node, React, and so on).

To **run** the project, see the [root README](../README.md). This page explains what the system is and how its parts fit together.

## What the SSO Gateway does

- **Authenticates users once.** People log in on the Gateway's login page instead of on each app.
- **Redirects them back with a signed JWT.** The token tells the client app who the user is and what access they have.
- **Controls which apps may use it.** Only apps an admin has registered (and left enabled) can send users to the login page.

## Key terms

| Term | Meaning |
|---|---|
| **Gateway** | The central SSO web app (`Gateway` project). It owns the login page, the admin area, the user database, and JWT creation. |
| **Client app** | Any app that sends users to the Gateway to log in. Also called a **tenant app**. |
| **Tenant app** | A client app as registered in the Gateway: a unique **Name** and a **Return URL** (the app's callback address). |
| **Group** | A named set of permissions that belongs to **one** tenant app. Names are stored as `[AppName]-[GroupName]`, for example `SalesApp-Admin`. |
| **Level** | A whole number on each group. **0 is the highest power**; larger numbers mean less power. |
| **Admin** | A Gateway user in the `Admin` role. Admins manage users, apps, groups, and can read the audit log. |
| **Return URL** | The callback address of a client app. The Gateway only redirects to a Return URL that is registered and active. |

## How a login works

```
Client app                     SSO Gateway
    |                               |
    |-- 1. send browser to -------->|  /Auth/Login?returnUrl=<callback URL>
    |                               |-- 2. check the app is registered
    |                               |-- 3. user enters email + password
    |                               |-- 4. build JWT (user, groups, levels)
    |<-- 5. redirect to ------------|  <callback URL>?token=<jwt>
    |                               |
    |-- 6. validate the token       |
    |-- 7. create its own session   |
```

If the user is already signed in at the Gateway, step 3 is skipped and they go straight back to the app. That is the "single" in single sign-on.

The full flow, including error paths, is in [architecture.md](architecture.md).

## What a client app receives

A JWT signed with HMAC-SHA256, valid for **9 hours**, containing:

| Claim | Contents |
|---|---|
| `sub` | User ID (GUID) |
| `email` | User's email |
| `tenant_app` | The app the token was issued for |
| `groups` | Comma-separated group names for **this app only** |
| `levels` | JSON string mapping each group name to its level |
| `iat`, `nbf`, `exp` | Issued at, not before, expiry |
| `iss`, `aud` | `SSOGateway` and `SSOClientApps` |

The exact format and the validation steps a client must follow are in [jwt-claims.md](../jwt-claims.md).

## Connecting a new app

### Admin steps (in the Gateway)

1. Log in to the Gateway as an admin.
2. **Apps**: create the app with a unique **Name** and its **Return URL** (the callback address, for example `https://myapp.example.com/callback`).
3. **Groups**: create at least one group for the app and give it a level. The name is prefixed with the app name automatically.
4. **Users**: open a user's details and assign them to the group(s).

A user with no group for the app can still log in at the Gateway, but the token will contain empty `groups` and `levels`, and the sample clients refuse them.

### Client steps (in your app)

1. Send the browser to `{Gateway}/Auth/Login?returnUrl={URL-encoded Return URL}`. The value must match the registered Return URL exactly (the comparison ignores letter case and surrounding spaces).
2. Receive the callback at the Return URL with `?token=<jwt>`.
3. Validate the token: signature, issuer, audience, and expiry.
4. **Check that `tenant_app` equals your own app name.** Every client shares one signing key, so this claim is what stops a token meant for app A from being accepted by app B.
5. Read `email`, `groups`, and `levels`, and create your own session. Keep that session no longer than the token's `exp`.

The sample apps in this repository do exactly this: `MVC` (cookie session) and `SsoClientApi` (bearer token).

## Facts at a glance

| Item | Value |
|---|---|
| Tech stack | ASP.NET Core MVC (.NET 10), EF Core, SQLite |
| Identity | ASP.NET Core Identity (cookie-based, at the Gateway) |
| Client authentication | Cookie session in the client, created after validating the JWT |
| Token | JWT, HS256, 9 hours (`JwtSettings:ExpiryHours`) |
| Public sign-up | None. Only admins create accounts |
| Duplicate emails | Blocked |
| Failed logins | 5 failed attempts lock the account for 15 minutes |
| Temporary passwords | New and admin-reset users must change them at the next login |
| Audit trail | Logins, failed logins, rejected return URLs, and admin actions are logged |

## Repository layout

```
Gateway/        SSO Gateway: Auth controller, Admin area, JwtTokenService
Data/           SsoDbContext, migrations, SeedData, AuditService, ReturnUrlValidator
Models/         Entities: ApplicationUser, TenantApp, Group, UserGroup, AuditLog
MVC/            Sample client #1 (ASP.NET Core MVC, MvcClientApp)
SsoClientApi/   Sample client #2 (minimal API, MockClientApp)
Tests/          xUnit tests
docs/           This documentation
```

## Documentation index

| Document | Status |
|---|---|
| [README.md](README.md) (this page) | Available |
| [architecture.md](architecture.md): system diagram and flow | Available |
| [jwt-claims.md](../jwt-claims.md): claims structure with example payload | Available (currently at the repository root) |
| `error-codes.md`: all error messages and responses | Planned |
| Integration guides: ASP.NET MVC, PHP, React, Node.js | Planned |
| Copy-paste code examples and FAQ | Planned |
