using System.Text.Json.Serialization;

namespace CryptoTgShop.Models.Telegram;

public sealed class CallbackQuery
{
	[JsonPropertyName("id")]
	public string Id { get; init; } = string.Empty;

	[JsonPropertyName("data")]
	public string? Data { get; init; }

	[JsonPropertyName("message")]
	public TgMessage? Message { get; init; }
}


