using Microsoft.IdentityModel.Tokens;
using Models.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Gateway.Services;

public class JwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    public string CreateToken(ApplicationUser user, string tenantAppName, List<Group> userGroupsForApp)
    {
        var jwt = _config.GetSection("JwtSettings");
        var secret = jwt["SecretKey"]!;
        var expiryHours = double.Parse(jwt["ExpiryHours"] ?? "9");

        var groupNames = string.Join(",", userGroupsForApp.Select(g => g.Name));
        var levels = userGroupsForApp.ToDictionary(g => g.Name ?? "", g => g.Level ?? 99);

        var now = DateTime.UtcNow;
        var iat = new DateTimeOffset(now).ToUnixTimeSeconds().ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new("tenant_app", tenantAppName),
            new("groups", groupNames),
            new("levels", JsonSerializer.Serialize(levels)),
            new(JwtRegisteredClaimNames.Iat, iat, ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            notBefore: now,
            expires: now.AddHours(expiryHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}