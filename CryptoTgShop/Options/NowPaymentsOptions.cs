using System.ComponentModel.DataAnnotations;

namespace CryptoTgShop.Options;

public sealed class NowPaymentsOptions
{
	[Required]
	public string ApiKey { get; init; } = string.Empty;

	[Required]
	public string IpnSecretKey { get; init; } = string.Empty;

	[Required]
	[Url]
	public string BaseUrl { get; init; } = "https://api.nowpayments.io/v1";

	[Required]
	[Url]
	public string IpnCallbackUrl { get; init; } = string.Empty;

	[Required]
	public string PriceCurrency { get; init; } = "USD";

	[Required]
	public decimal PriceAmount { get; init; } = 10.0m;

	public string? SuccessUrl { get; init; }

	public string? CancelUrl { get; init; }
}

