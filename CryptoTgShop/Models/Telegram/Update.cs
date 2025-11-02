using System.Text.Json.Serialization;

namespace CryptoTgShop.Models.Telegram;

public sealed class Update
{
	[JsonPropertyName("update_id")]
	public long UpdateId { get; init; }

	[JsonPropertyName("message")]
	public TgMessage? Message { get; init; }

	[JsonPropertyName("callback_query")]
	public CallbackQuery? CallbackQuery { get; init; }
}


