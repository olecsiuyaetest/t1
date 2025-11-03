namespace CryptoTgShop.Services.Interfaces;

public interface INowPaymentsApiClient
{
	Task<CreatePaymentResponse> CreatePaymentAsync(
		decimal priceAmount,
		string priceCurrency,
		string orderId,
		string orderDescription,
		CancellationToken cancellationToken = default);

	Task<PaymentStatusResponse?> GetPaymentStatusAsync(
		string paymentId,
		CancellationToken cancellationToken = default);
}

public sealed class CreatePaymentRequest
{
	public required decimal PriceAmount { get; init; }
	public required string PriceCurrency { get; init; }
	public required string PayCurrency { get; init; }
	public required string OrderId { get; init; }
	public required string OrderDescription { get; init; }
	public required string IpnCallbackUrl { get; init; }
}

public sealed class CreatePaymentResponse
{
	public required string PaymentId { get; init; }
	public required string PaymentStatus { get; init; }
	public required string PayAddress { get; init; }
	public required decimal PriceAmount { get; init; }
	public required string PriceCurrency { get; init; }
	public required string PayAmount { get; init; }
	public required string PayCurrency { get; init; }
	public required string OrderId { get; init; }
	public required string OrderDescription { get; init; }
	public required string PaymentUrl { get; init; }
	public long? ExpirationAt { get; init; }
}

public sealed class PaymentStatusResponse
{
	public required string PaymentId { get; init; }
	public required string PaymentStatus { get; init; }
	public required string PayAddress { get; init; }
	public decimal? PriceAmount { get; init; }
	public string? PriceCurrency { get; init; }
	public string? PayAmount { get; init; }
	public string? PayCurrency { get; init; }
	public string? OrderId { get; init; }
	public string? OrderDescription { get; init; }
	public long? ExpirationAt { get; init; }
}

