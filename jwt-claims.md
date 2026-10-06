# JWT Claims Structure

The Gateway creates one JWT per login, for one specific client app. It is passed to the client as `returnUrl?token=<jwt>`.

- Algorithm: HMAC-SHA256 (`HS256`), signed with `JwtSettings:SecretKey`
- Lifetime: 9 hours (`JwtSettings:ExpiryHours`)
- Source of truth: `Gateway/Services/JwtTokenService.cs`

## Example payload

```json
{
  "sub": "3f2b8c7e-5d14-4b0a-9f3a-1c2d3e4f5a6b",
  "email": "user@example.com",
  "tenant_app": "SalesApp",
  "groups": "SalesApp-Admin,SalesApp-Manager",
  "levels": "{\"SalesApp-Admin\":0,\"SalesApp-Manager\":1}",
  "iat": 1724130000,
  "nbf": 1724130000,
  "exp": 1724162400,
  "iss": "SSOGateway",
  "aud": "SSOClientApps"
}
```

The `levels` value is a JSON **string** inside the token, not a nested object. Parse it a second time (see "Reading the claims").

## Claims reference

| Claim | Type | Meaning | Example |
|---|---|---|---|
| `sub` | string | User ID (GUID) | `3f2b8c7e-...` |
| `email` | string | User's email address | `user@example.com` |
| `tenant_app` | string | Name of the registered app the token was issued for | `SalesApp` |
| `groups` | string | Comma-separated names of the user's groups **for this app only** | `SalesApp-Admin,SalesApp-Manager` |
| `levels` | string (JSON) | Map of group name to level. **0 is the highest power**; larger numbers mean less power. A group without a level counts as 99 | `{"SalesApp-Admin":0}` |
| `iat` | number | Issued at (Unix seconds, UTC) | `1724130000` |
| `nbf` | number | Not valid before (Unix seconds, UTC) | `1724130000` |
| `exp` | number | Expiry (Unix seconds, UTC); `iat` + 9 hours by default | `1724162400` |
| `iss` | string | Issuer; must equal `JwtSettings:Issuer` | `SSOGateway` |
| `aud` | string | Audience; must equal `JwtSettings:Audience` | `SSOClientApps` |

Notes:

- Group names are stored with the app prefix, in the form `[AppName]-[GroupName]`.
- Only groups belonging to the app in `tenant_app` are included. A user with groups in several apps gets a different token for each app.
- A user with no group for the app gets empty `groups` and `levels`.

## How a client must validate the token

1. Check the signature with the shared secret (`HS256` only).
2. Check `iss` equals `SSOGateway` and `aud` equals `SSOClientApps`.
3. Check `exp` has not passed (the sample clients allow 30 seconds of clock skew).
4. **Check `tenant_app` equals your own app name.** All client apps share one key and audience, so this claim is the only thing that stops a token meant for app A from working in app B.
5. Only then read `email`, `groups`, and `levels`.

## Reading the claims (C#)

```csharp
// Keep the original claim names (do not let .NET rename "sub" or "email")
options.MapInboundClaims = false;

var email = user.FindFirst("email")?.Value;
var app   = user.FindFirst("tenant_app")?.Value;

// groups: comma-separated string
var groups = (user.FindFirst("groups")?.Value ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// levels: JSON string -> dictionary
var levels = JsonSerializer.Deserialize<Dictionary<string, int>>(
    user.FindFirst("levels")?.Value ?? "{}") ?? new();

// Is the user a top-level admin for this app?
bool isAdmin = levels.Values.Any(l => l == 0);
```

## Client configuration that must match the Gateway

| Client setting (`Sso:` section) | Must equal |
|---|---|
| `SecretKey` | Gateway `JwtSettings:SecretKey` |
| `Issuer` | Gateway `JwtSettings:Issuer` |
| `Audience` | Gateway `JwtSettings:Audience` |
| `AppName` | The app's Name under Admin > Apps |
| `ClientBaseUrl` + `/callback` | The app's Return URL under Admin > Apps |

## Errors a client may see

| Situation | Sample client behavior |
|---|---|
| No token or bad token | `401`, `error: "unauthorized"` (browser pages redirect to `/login`) |
| Token expired | `401`, `error: "token_expired"`, header `Token-Expired: true` |
| Token issued for another app | Rejected (`wrong_app`) |
| Missing `token` in the callback | Redirect to `/?error=missing_token` |
