using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.Extensions.Options;
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

		var body = new
		{
			price_amount = priceAmount,
			price_currency = priceCurrency.ToLowerInvariant(),
			order_id = orderId,
			order_description = orderDescription,
			ipn_callback_url = _options.IpnCallbackUrl,
			success_url = _options.SuccessUrl ?? "https://nowpayments.io",
			cancel_url = _options.CancelUrl ?? "https://nowpayments.io"
		};

		request.AddJsonBody(body);

		var response = await _client.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

		if (!response.IsSuccessful)
		{
			_logger.LogError(
				"NowPayments API error: Status={Status}, Content={Content}",
				response.StatusCode, response.Content);
			throw new HttpRequestException($"NowPayments API error: {response.StatusCode} - {response.Content}");
		}

		_logger.LogInformation("NowPayments invoice response: {Response}", response.Content);

		var jsonDoc = JsonDocument.Parse(response.Content ?? "{}");
		var root = jsonDoc.RootElement;

		// Helper to get value as string (handles both number and string types)
		string GetValueAsString(JsonElement element, string fieldName)
		{
			if (element.ValueKind == JsonValueKind.String)
			{
				return element.GetString() ?? throw new InvalidOperationException($"{fieldName} is null");
			}
			if (element.ValueKind == JsonValueKind.Number)
			{
				return element.GetDecimal().ToString("G", System.Globalization.CultureInfo.InvariantCulture);
			}
			if (element.ValueKind == JsonValueKind.Null)
			{
				return string.Empty;
			}
			throw new InvalidOperationException($"{fieldName} has unexpected type: {element.ValueKind}");
		}

		// Invoice response has invoice_id instead of payment_id, and invoice_url for the payment URL
		var paymentId = root.TryGetProperty("payment_id", out var paymentIdProp) 
			? paymentIdProp.GetString() 
			: root.TryGetProperty("invoice_id", out var invoiceIdProp) 
				? invoiceIdProp.GetString() 
				: throw new InvalidOperationException("payment_id/invoice_id is missing");

		var invoiceUrl = root.TryGetProperty("invoice_url", out var invoiceUrlProp) 
			? invoiceUrlProp.GetString() 
			: root.TryGetProperty("payment_url", out var paymentUrlProp) 
				? paymentUrlProp.GetString() 
				: throw new InvalidOperationException("invoice_url/payment_url is missing");

		var result = new CreatePaymentResponse
		{
			PaymentId = paymentId ?? throw new InvalidOperationException("payment_id/invoice_id is null"),
			PaymentStatus = root.TryGetProperty("payment_status", out var statusProp) 
				? statusProp.GetString() ?? "waiting" 
				: "waiting",
			PayAddress = root.TryGetProperty("pay_address", out var addressProp) 
				? addressProp.GetString() ?? string.Empty 
				: string.Empty,
			PriceAmount = root.TryGetProperty("price_amount", out var priceAmountProp) 
				? priceAmountProp.GetDecimal() 
				: priceAmount,
			PriceCurrency = root.TryGetProperty("price_currency", out var priceCurrencyProp) 
				? priceCurrencyProp.GetString() ?? priceCurrency 
				: priceCurrency,
			PayAmount = root.TryGetProperty("pay_amount", out var payAmountProp) 
				? GetValueAsString(payAmountProp, "pay_amount") 
				: string.Empty,
			PayCurrency = root.TryGetProperty("pay_currency", out var payCurrencyProp) 
				? payCurrencyProp.GetString() ?? string.Empty 
				: string.Empty,
			OrderId = root.TryGetProperty("order_id", out var orderIdProp) 
				? orderIdProp.GetString() ?? orderId 
				: orderId,
			OrderDescription = root.TryGetProperty("order_description", out var orderDescProp) 
				? orderDescProp.GetString() ?? orderDescription 
				: orderDescription,
			PaymentUrl = invoiceUrl ?? throw new InvalidOperationException("invoice_url/payment_url is null"),
			ExpirationAt = root.TryGetProperty("expiration_at", out var exp) 
				? exp.GetInt64() 
				: root.TryGetProperty("expiration_estimate_date", out var expEst) 
					? (expEst.ValueKind == JsonValueKind.String && DateTime.TryParse(expEst.GetString(), out var dateTime))
						? new DateTimeOffset(dateTime).ToUnixTimeSeconds()
						: null
					: null
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

