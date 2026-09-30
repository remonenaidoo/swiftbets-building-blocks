using System.ComponentModel.DataAnnotations;

namespace SwiftBets.BuildingBlocks.Web;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Issuer base URL; its /.well-known/openid-configuration points at the JWKS.</summary>
    [Required]
    public string Authority { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = "swiftbets";

    public bool RequireHttpsMetadata { get; set; } = true;
}
