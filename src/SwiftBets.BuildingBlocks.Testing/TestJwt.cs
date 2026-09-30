using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SwiftBets.BuildingBlocks.Testing;

/// <summary>Issues RS256 tokens from a throwaway key and points a host's bearer validation at it, with no metadata fetch.</summary>
public static class TestJwt
{
    public const string Issuer = "https://identity.test.swiftbets";
    public const string Audience = "swiftbets";

    private static readonly RsaSecurityKey Key = new(RSA.Create(2048)) { KeyId = "test-key" };

    public static string Issue(string subject, params string[] roles)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.RsaSha256),
        });
    }

    public static IServiceCollection UseTestJwt(this IServiceCollection services) =>
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Authority = null;
            options.RequireHttpsMetadata = false;
            var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
            configuration.SigningKeys.Add(Key);
            options.Configuration = configuration;
            options.TokenValidationParameters.ValidIssuer = Issuer;
            options.TokenValidationParameters.ValidAudience = Audience;
        });
}
