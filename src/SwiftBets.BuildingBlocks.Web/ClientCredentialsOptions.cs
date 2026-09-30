using System.ComponentModel.DataAnnotations;

namespace SwiftBets.BuildingBlocks.Web;

public sealed class ClientCredentialsOptions
{
    public const string SectionName = "ServiceIdentity";

    [Required]
    [Url]
    public string TokenEndpoint { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;
}
