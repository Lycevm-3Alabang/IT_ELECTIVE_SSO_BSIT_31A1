// Gateway/Services/AuthSecurityOptions.cs
using Microsoft.AspNetCore.Identity;

namespace Gateway.Services;

public static class AuthSecurityOptions
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static void ApplyLockout(LockoutOptions lockout)
    {
        lockout.AllowedForNewUsers = true;
        lockout.MaxFailedAccessAttempts = MaxFailedAttempts;
        lockout.DefaultLockoutTimeSpan = LockoutDuration;
    }
}