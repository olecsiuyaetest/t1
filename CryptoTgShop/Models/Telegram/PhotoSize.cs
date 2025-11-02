using System.Text.Json.Serialization;

namespace CryptoTgShop.Models.Telegram;

public sealed class PhotoSize
{
	[JsonPropertyName("file_id")]
	public string FileId { get; init; } = string.Empty;

	[JsonPropertyName("width")]
	public int Width { get; init; }

	[JsonPropertyName("height")]
	public int Height { get; init; }
}


