using System.Text.Json.Serialization;

namespace CryptoTgShop.Models.Telegram;

public sealed class TgMessage
{
	[JsonPropertyName("message_id")]
	public long MessageId { get; init; }

	[JsonPropertyName("chat")]
	public TgChat Chat { get; init; } = default!;

	[JsonPropertyName("text")]
	public string? Text { get; init; }

	[JsonPropertyName("photo")]
	public PhotoSize[]? Photo { get; init; }
}


