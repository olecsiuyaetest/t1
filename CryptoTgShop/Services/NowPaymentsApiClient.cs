using CryptoTgShop.Models.NowPayments;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using System.Text.Json;

namespace CryptoTgShop.Services;

public sealed class NowPaymentsApiClient : INowPaymentsApiClient
{
	private readonly RestClient _client;
	private readonly NowPaymentsOptions _options;
	private readonly string _apiPath;
	private readonly ILogger<NowPaymentsApiClient> _logger;

	public NowPaymentsApiClient(
		IOptions<NowPaymentsOptions> options,
		ILogger<NowPaymentsApiClient> logger)
	{
		_options = options.Value;
		_logger = logger;

		// Extract base URL and API path from BaseUrl
		// e.g., "https://api-sandbox.nowpayments.io/v1" -> base: "https://api-sandbox.nowpayments.io", path: "/v1"
		var baseUrlUri = new Uri(_options.BaseUrl);
		var baseUrl = $"{baseUrlUri.Scheme}://{baseUrlUri.Host}";
		_apiPath = baseUrlUri.AbsolutePath.TrimEnd('/');

		var clientOptions = new RestClientOptions(baseUrl)
		{
			MaxTimeout = -1
		};

		_client = new RestClient(clientOptions);
	}

	public async Task<CreatePaymentResponse> CreatePaymentAsync(
		decimal priceAmount,
		string priceCurrency,
		string orderId,
		string orderDescription,
		CancellationToken cancellationToken = default)
	{
		_logger.LogInformation(
			"Creating invoice: Amount={Amount}, Currency={Currency}, OrderId={OrderId}",
			priceAmount, priceCurrency, orderId);

		var request = new RestRequest($"{_apiPath}/invoice", Method.Post);
		request.AddHeader("x-api-key", _options.ApiKey);
		request.AddHeader("Content-Type", "application/json");

		var body = @"{
" + "\n" +
@"  ""price_amount"": " + priceAmount.ToString(System.Globalization.CultureInfo.InvariantCulture) + @",
" + "\n" +
@"  ""price_currency"": """ + priceCurrency.ToLowerInvariant() + @""",
" + "\n" +
@"  ""order_id"": """ + orderId + @""",
" + "\n" +
@"  ""order_description"": """ + orderDescription + @""",
" + "\n" +
@"  ""ipn_callback_url"": """ + _options.IpnCallbackUrl + @""",
" + "\n" +
@"  ""success_url"": """ + (_options.SuccessUrl ?? "https://nowpayments.io") + @""",
" + "\n" +
@"  ""cancel_url"": """ + (_options.CancelUrl ?? "https://nowpayments.io") + @"""
" + "\n" +
@"}";

		request.AddStringBody(body, DataFormat.Json);

		var response = await _client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessful)
		{
			_logger.LogError(
				"NowPayments API error: Status={Status}, Content={Content}",
				response.StatusCode, response.Content);
			throw new HttpRequestException($"NowPayments API error: {response.StatusCode} - {response.Content}");
		}

		_logger.LogInformation("NowPayments invoice response: {Response}", response.Content);

		if (string.IsNullOrEmpty(response.Content))
		{
			throw new InvalidOperationException("NowPayments API returned empty response");
		}

		var invoiceResponse = JsonConvert.DeserializeObject<InvoiceResponse>(response.Content)
			?? throw new InvalidOperationException("Failed to deserialize invoice response");

		var result = new CreatePaymentResponse
		{
			PaymentId = invoiceResponse.Id,
			PaymentStatus = "waiting", // Invoice is just created, status is waiting
			PayAddress = string.Empty, // Not available in invoice creation response
			PriceAmount = invoiceResponse.PriceAmount > 0 ? invoiceResponse.PriceAmount : priceAmount,
			PriceCurrency = invoiceResponse.PriceCurrency ?? priceCurrency,
			PayAmount = string.Empty, // Not available until payment is selected
			PayCurrency = invoiceResponse.PayCurrency ?? string.Empty,
			OrderId = invoiceResponse.OrderId ?? orderId,
			OrderDescription = invoiceResponse.OrderDescription ?? orderDescription,
			PaymentUrl = invoiceResponse.InvoiceUrl,
			ExpirationAt = null // Not provided in invoice creation response
		};

		_logger.LogInformation(
			"Payment created: PaymentId={PaymentId}, PaymentUrl={PaymentUrl}, Status={Status}",
			result.PaymentId, result.PaymentUrl, result.PaymentStatus);

		return result;
	}

	public async Task<PaymentStatusResponse?> GetPaymentStatusAsync(
		string paymentId,
		CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Getting payment status: PaymentId={PaymentId}", paymentId);

		var request = new RestRequest($"{_apiPath}/payment/{paymentId}", Method.Get);
		request.AddHeader("x-api-key", _options.ApiKey);

		var response = await _client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

		if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			_logger.LogWarning("Payment not found: PaymentId={PaymentId}", paymentId);
			return null;
		}

		if (!response.IsSuccessful)
		{
			_logger.LogError(
				"NowPayments API error: Status={Status}, Content={Content}",
				response.StatusCode, response.Content);
			throw new HttpRequestException($"NowPayments API error: {response.StatusCode} - {response.Content}");
		}

		_logger.LogInformation("NowPayments status response: {Response}", response.Content);

		var jsonDoc = JsonDocument.Parse(response.Content ?? "{}");
		var root = jsonDoc.RootElement;

		// Helper to get pay_amount as string (handles both number and string types)
		string? GetPayAmountAsStringNullable(JsonElement element)
		{
			if (element.ValueKind == JsonValueKind.String)
			{
				return element.GetString();
			}
			if (element.ValueKind == JsonValueKind.Number)
			{
				return element.GetDecimal().ToString("G", System.Globalization.CultureInfo.InvariantCulture);
			}
			return null;
		}

		var result = new PaymentStatusResponse
		{
			PaymentId = root.GetProperty("payment_id").GetString() ?? throw new InvalidOperationException("payment_id is missing"),
			PaymentStatus = root.GetProperty("payment_status").GetString() ?? throw new InvalidOperationException("payment_status is missing"),
			PayAddress = root.GetProperty("pay_address").GetString() ?? throw new InvalidOperationException("pay_address is missing"),
			PriceAmount = root.TryGetProperty("price_amount", out var priceAmount) ? priceAmount.GetDecimal() : null,
			PriceCurrency = root.TryGetProperty("price_currency", out var priceCurrency) ? priceCurrency.GetString() : null,
			PayAmount = root.TryGetProperty("pay_amount", out var payAmount) ? GetPayAmountAsStringNullable(payAmount) : null,
			PayCurrency = root.TryGetProperty("pay_currency", out var payCurrency) ? payCurrency.GetString() : null,
			OrderId = root.TryGetProperty("order_id", out var orderId) ? orderId.GetString() : null,
			OrderDescription = root.TryGetProperty("order_description", out var orderDesc) ? orderDesc.GetString() : null,
			ExpirationAt = root.TryGetProperty("expiration_at", out var exp) ? exp.GetInt64() : null
		};

		return result;
	}
}

