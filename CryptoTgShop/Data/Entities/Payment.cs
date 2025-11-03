namespace CryptoTgShop.Data.Entities;

public sealed class Payment
{
	public long Id { get; set; }
	public long TelegramChatId { get; set; }
	public string Category { get; set; } = string.Empty;
	public string PaymentId { get; set; } = string.Empty; // NowPayments payment ID
	public string PaymentUrl { get; set; } = string.Empty;
	public decimal PriceAmount { get; set; }
	public string PriceCurrency { get; set; } = "USD";
	public string PayCurrency { get; set; } = string.Empty;
	public string OrderId { get; set; } = string.Empty; // Unique order ID
	public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
	public long? DataRecordId { get; set; } // Link to the DataRecord when payment is completed
	public DateTime CreatedAtUtc { get; set; }
	public DateTime? CompletedAtUtc { get; set; }
}

public enum PaymentStatus
{
	Pending = 0,
	Waiting = 1,
	Confirming = 2,
	Confirmed = 3,
	Sending = 4,
	Finished = 5,
	Failed = 6,
	Refunded = 7,
	Expired = 8
}

