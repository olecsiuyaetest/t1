using System.Text.Json.Serialization;

namespace CryptoTgShop.Models.Telegram;

public sealed class TgChat
{
	[JsonPropertyName("id")]
	public long Id { get; init; }
}


