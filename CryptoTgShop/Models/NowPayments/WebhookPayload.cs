using System.Text.Json.Serialization;

namespace CryptoTgShop.Models.NowPayments;

public sealed class NowPaymentsWebhookPayload
{
	[JsonPropertyName("payment_id")]
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
	public long PaymentId { get; set; }

	[JsonPropertyName("invoice_id")]
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
	public long? InvoiceId { get; set; }

	[JsonPropertyName("payment_status")]
	public string PaymentStatus { get; set; } = string.Empty;

	[JsonPropertyName("pay_address")]
	public string? PayAddress { get; set; }

	[JsonPropertyName("price_amount")]
	public decimal? PriceAmount { get; set; }

	[JsonPropertyName("price_currency")]
	public string? PriceCurrency { get; set; }

	[JsonPropertyName("pay_amount")]
	public decimal? PayAmount { get; set; }

	[JsonPropertyName("actually_paid")]
	public decimal? ActuallyPaid { get; set; }

	[JsonPropertyName("actually_paid_at_fiat")]
	public decimal? ActuallyPaidAtFiat { get; set; }

	[JsonPropertyName("pay_currency")]
	public string? PayCurrency { get; set; }

	[JsonPropertyName("order_id")]
	public string? OrderId { get; set; }

	[JsonPropertyName("order_description")]
	public string? OrderDescription { get; set; }

	[JsonPropertyName("purchase_id")]
	public string? PurchaseId { get; set; }

	[JsonPropertyName("outcome_amount")]
	public decimal? OutcomeAmount { get; set; }

	[JsonPropertyName("outcome_currency")]
	public string? OutcomeCurrency { get; set; }

	[JsonPropertyName("parent_payment_id")]
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
	public long? ParentPaymentId { get; set; }

	[JsonPropertyName("payin_extra_id")]
	public string? PayinExtraId { get; set; }

	[JsonPropertyName("payment_extra_ids")]
	public object? PaymentExtraIds { get; set; }

	[JsonPropertyName("updated_at")]
	[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
	public long? UpdatedAt { get; set; }

	[JsonPropertyName("fee")]
	public FeeInfo? Fee { get; set; }
}

public sealed class FeeInfo
{
	[JsonPropertyName("currency")]
	public string? Currency { get; set; }

	[JsonPropertyName("depositFee")]
	public decimal? DepositFee { get; set; }

	[JsonPropertyName("serviceFee")]
	public decimal? ServiceFee { get; set; }

	[JsonPropertyName("withdrawalFee")]
	public decimal? WithdrawalFee { get; set; }
}

