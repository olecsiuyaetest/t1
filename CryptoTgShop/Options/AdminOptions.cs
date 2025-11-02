using System.ComponentModel.DataAnnotations;

namespace CryptoTgShop.Options;

public sealed class AdminOptions
{
	[Required]
	public string SecretKey { get; init; } = string.Empty;
}


