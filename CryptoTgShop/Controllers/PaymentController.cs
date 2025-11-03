using CryptoTgShop.Data;
using CryptoTgShop.Data.Entities;
using CryptoTgShop.Models.NowPayments;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CryptoTgShop.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PaymentController : ControllerBase
{
	private const string SignatureHeaderName = "x-nowpayments-sig";
	private readonly AppDbContext _db;
	private readonly NowPaymentsOptions _options;
	private readonly ITelegramApiClient _telegramApi;
	private readonly INowPaymentsApiClient _nowPaymentsApi;
	private readonly ILogger<PaymentController> _logger;

	public PaymentController(
		AppDbContext db,
		IOptions<NowPaymentsOptions> options,
		ITelegramApiClient telegramApi,
		INowPaymentsApiClient nowPaymentsApi,
		ILogger<PaymentController> logger)
	{
		_db = db;
		_options = options.Value;
		_telegramApi = telegramApi;
		_nowPaymentsApi = nowPaymentsApi;
		_logger = logger;
	}

	[HttpPost("webhook")]
	public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== NOWPAYMENTS WEBHOOK START ==========");

		// Read raw body for signature verification
		Request.EnableBuffering();
		string rawBody;
		using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
		{
			rawBody = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
		}
		Request.Body.Position = 0;

		_logger.LogInformation("Webhook raw body: {Body}", rawBody);

		// Verify signature
		if (!Request.Headers.TryGetValue(SignatureHeaderName, out var signatureHeader) || signatureHeader.Count == 0)
		{
			_logger.LogWarning("Webhook rejected: Signature header '{HeaderName}' missing", SignatureHeaderName);
			return StatusCode(403);
		}

		var receivedSignature = signatureHeader[0];
		_logger.LogInformation("Received signature: {Signature}", receivedSignature);

		var expectedSignature = ComputeSignature(rawBody, _options.IpnSecretKey);
		_logger.LogInformation("Expected signature: {Signature}", expectedSignature);

		if (!string.Equals(receivedSignature, expectedSignature, StringComparison.Ordinal))
		{
			_logger.LogWarning("Webhook rejected: Signature mismatch");
			return StatusCode(403);
		}

		_logger.LogInformation("Signature verification passed");

		// Parse payload
		NowPaymentsWebhookPayload? payload;
		try
		{
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString | 
				                 System.Text.Json.Serialization.JsonNumberHandling.WriteAsString
			};
			payload = JsonSerializer.Deserialize<NowPaymentsWebhookPayload>(rawBody, options);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to deserialize webhook payload");
			return BadRequest("Invalid payload format");
		}

		if (payload == null || payload.PaymentId == 0)
		{
			_logger.LogWarning("Webhook payload is null or missing payment_id");
			return BadRequest("Invalid payload");
		}

		// Convert payment ID to string for database lookup (stored as string)
		var paymentIdString = payload.PaymentId.ToString();

		_logger.LogInformation(
			"Processing webhook: PaymentId={PaymentId}, InvoiceId={InvoiceId}, Status={Status}, OrderId={OrderId}",
			payload.PaymentId, payload.InvoiceId, payload.PaymentStatus, payload.OrderId);

		// Find payment in database by payment_id (stored as string)
		var payment = await _db.Payments
			.FirstOrDefaultAsync(p => p.PaymentId == paymentIdString, cancellationToken).ConfigureAwait(false);

		if (payment == null)
		{
			_logger.LogWarning("Payment not found in database: PaymentId={PaymentId}", payload.PaymentId);
			return Ok(); // Return OK to prevent NowPayments from retrying
		}

		_logger.LogInformation(
			"Found payment: Id={Id}, Status={Status}, ChatId={ChatId}, Category={Category}",
			payment.Id, payment.Status, payment.TelegramChatId, payment.Category);

		// Map NowPayments status to our enum
		var newStatus = MapPaymentStatus(payload.PaymentStatus);
		var previousStatus = payment.Status;

		// Update payment status
		payment.Status = newStatus;
		if (newStatus == PaymentStatus.Finished && payment.CompletedAtUtc == null)
		{
			payment.CompletedAtUtc = DateTime.UtcNow;
		}

		// If payment is confirmed/finished and no data record is linked yet
		if ((newStatus == PaymentStatus.Confirmed || newStatus == PaymentStatus.Finished) &&
			payment.DataRecordId == null)
		{
			// Find an available item for the category
			var availableRecord = await _db.DataRecords
				.Where(r => !r.IsUsed && r.Type == payment.Category)
				.OrderBy(r => Guid.NewGuid())
				.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

			if (availableRecord != null)
			{
				_logger.LogInformation(
					"Linking DataRecord: RecordId={RecordId}, Type={Type}, Message={Message}",
					availableRecord.Id, availableRecord.Type, availableRecord.Message);

				payment.DataRecordId = availableRecord.Id;
				availableRecord.IsUsed = true;

				// Send the item to the user
				try
				{
					_logger.LogInformation("Sending item to user: ChatId={ChatId}, ImageUrl={ImageUrl}",
						payment.TelegramChatId, availableRecord.ImageUrl);

					await _telegramApi.SendPhotoAsync(
						payment.TelegramChatId,
						availableRecord.ImageUrl,
						availableRecord.Message,
						null,
						cancellationToken).ConfigureAwait(false);

					_logger.LogInformation("Item sent successfully to ChatId={ChatId}", payment.TelegramChatId);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to send item to user: ChatId={ChatId}", payment.TelegramChatId);
					// Don't fail the webhook if sending fails - we can retry later
				}
			}
			else
			{
				_logger.LogWarning(
					"No available items for category: Category={Category}, PaymentId={PaymentId}",
					payment.Category, payment.PaymentId);

				// Notify user that item is out of stock
				try
				{
					var message = $"Payment received, but no items available for category '{payment.Category}'. Please contact support.";
					await _telegramApi.SendMessageAsync(
						payment.TelegramChatId,
						message,
						null,
						cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to send out-of-stock message to user");
				}
			}
		}

		await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

		_logger.LogInformation(
			"Payment updated: PaymentId={PaymentId}, PreviousStatus={PreviousStatus}, NewStatus={NewStatus}, DataRecordId={DataRecordId}",
			payment.PaymentId, previousStatus, newStatus, payment.DataRecordId);

		_logger.LogInformation("========== NOWPAYMENTS WEBHOOK END ==========");
		return Ok();
	}

	private static PaymentStatus MapPaymentStatus(string status)
	{
		return status.ToUpperInvariant() switch
		{
			"WAITING" => PaymentStatus.Waiting,
			"CONFIRMING" => PaymentStatus.Confirming,
			"CONFIRMED" => PaymentStatus.Confirmed,
			"SENDING" => PaymentStatus.Sending,
			"FINISHED" => PaymentStatus.Finished,
			"FAILED" => PaymentStatus.Failed,
			"REFUNDED" => PaymentStatus.Refunded,
			"EXPIRED" => PaymentStatus.Expired,
			_ => PaymentStatus.Pending
		};
	}

	private static string ComputeSignature(string body, string secretKey)
	{
		// Parse JSON and sort keys alphabetically
		var jsonDoc = JsonDocument.Parse(body);
		var sortedDict = new SortedDictionary<string, JsonElement>();

		foreach (var prop in jsonDoc.RootElement.EnumerateObject())
		{
			sortedDict[prop.Name] = prop.Value;
		}

		// Rebuild JSON with sorted keys
		var sortedJson = JsonSerializer.Serialize(sortedDict);

		// Compute HMAC-SHA512
		var keyBytes = Encoding.UTF8.GetBytes(secretKey);
		var dataBytes = Encoding.UTF8.GetBytes(sortedJson);
		var hashBytes = HMACSHA512.HashData(keyBytes, dataBytes);

		// Convert to hex string
		return Convert.ToHexString(hashBytes).ToLowerInvariant();
	}
}

