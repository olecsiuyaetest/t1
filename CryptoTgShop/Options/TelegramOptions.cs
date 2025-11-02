using System.ComponentModel.DataAnnotations;

namespace CryptoTgShop.Options;

public sealed class TelegramOptions
{
	[Required]
	public string BotToken { get; init; } = string.Empty;

	[Required]
	public string SecretToken { get; init; } = string.Empty;
}


