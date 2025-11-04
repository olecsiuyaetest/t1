using Newtonsoft.Json;

namespace CryptoTgShop.Models.NowPayments;

public sealed class InvoiceResponse
{
	[JsonProperty("id")]
	public string Id { get; set; } = string.Empty;

	[JsonProperty("token_id")]
	public string? TokenId { get; set; }

	[JsonProperty("order_id")]
	public string? OrderId { get; set; }

	[JsonProperty("order_description")]
	public string? OrderDescription { get; set; }

	[JsonProperty("price_amount")]
	[JsonConverter(typeof(PriceAmountConverter))]
	public decimal PriceAmount { get; set; }

	[JsonProperty("price_currency")]
	public string? PriceCurrency { get; set; }

	[JsonProperty("pay_currency")]
	public string? PayCurrency { get; set; }

	[JsonProperty("ipn_callback_url")]
	public string? IpnCallbackUrl { get; set; }

	[JsonProperty("invoice_url")]
	public string? InvoiceUrl { get; set; }

	[JsonProperty("success_url")]
	public string? SuccessUrl { get; set; }

	[JsonProperty("cancel_url")]
	public string? CancelUrl { get; set; }

	[JsonProperty("customer_email")]
	public string? CustomerEmail { get; set; }

	[JsonProperty("partially_paid_url")]
	public string? PartiallyPaidUrl { get; set; }

	[JsonProperty("payout_currency")]
	public string? PayoutCurrency { get; set; }

	[JsonProperty("created_at")]
	public DateTime? CreatedAt { get; set; }

	[JsonProperty("updated_at")]
	public DateTime? UpdatedAt { get; set; }

	[JsonProperty("is_fixed_rate")]
	public bool? IsFixedRate { get; set; }

	[JsonProperty("is_fee_paid_by_user")]
	public bool? IsFeePaidByUser { get; set; }

	[JsonProperty("source")]
	public string? Source { get; set; }

	[JsonProperty("collect_user_data")]
	public bool? CollectUserData { get; set; }
}

/// <summary>
/// Custom converter to handle price_amount which can be either string or number
/// </summary>
public class PriceAmountConverter : JsonConverter<decimal>
{
	public override decimal ReadJson(JsonReader reader, Type objectType, decimal existingValue, bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.TokenType == JsonToken.String)
		{
			if (decimal.TryParse(reader.Value?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result))
			{
				return result;
			}
		}
		else if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
		{
			return Convert.ToDecimal(reader.Value);
		}

		throw new JsonSerializationException($"Unexpected token type {reader.TokenType} when parsing price_amount");
	}

	public override void WriteJson(JsonWriter writer, decimal value, JsonSerializer serializer)
	{
		writer.WriteValue(value);
	}
}

