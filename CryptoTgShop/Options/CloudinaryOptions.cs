using System.ComponentModel.DataAnnotations;

namespace CryptoTgShop.Options;

public sealed class CloudinaryOptions
{
	[Required]
	public string CloudName { get; init; } = string.Empty;

	[Required]
	public string ApiKey { get; init; } = string.Empty;

	[Required]
	public string ApiSecret { get; init; } = string.Empty;
}


