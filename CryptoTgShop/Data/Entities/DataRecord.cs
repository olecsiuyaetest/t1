namespace CryptoTgShop.Data.Entities;

public sealed class DataRecord
{
	public long Id { get; set; }
	public string Message { get; set; } = string.Empty;
	public string Type { get; set; } = string.Empty;
	public string ImageUrl { get; set; } = string.Empty;
	public bool IsUsed { get; set; }
	public DateTime CreatedAtUtc { get; set; }
}


