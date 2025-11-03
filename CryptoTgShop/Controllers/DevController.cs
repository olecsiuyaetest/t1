using CryptoTgShop.Models.Telegram;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using CryptoTgShop.Data;
using CryptoTgShop.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CryptoTgShop.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DevController : ControllerBase
{
	private readonly IOptions<TelegramOptions> _telegram;
	private readonly IOptions<BotTextOptions> _botText;
	private readonly IOptions<AdminOptions> _admin;
	private readonly IOptions<CloudinaryOptions> _cloudinary;
	private readonly IOptions<NowPaymentsOptions> _nowPayments;
	private readonly IConfiguration _configuration;
	private readonly IUserMessageHandler _messageHandler;
	private readonly INowPaymentsApiClient _nowPaymentsApi;
	private readonly AppDbContext _db;
	private readonly ILogger<DevController> _logger;
	private static long _updateIdCounter = 1;
	private static long _messageIdCounter = 1;
	private static int _callbackIdCounter = 1;

	public DevController(
		IOptions<TelegramOptions> telegram,
		IOptions<BotTextOptions> botText,
		IOptions<AdminOptions> admin,
		IOptions<CloudinaryOptions> cloudinary,
		IOptions<NowPaymentsOptions> nowPayments,
		IConfiguration configuration,
		IUserMessageHandler messageHandler,
		INowPaymentsApiClient nowPaymentsApi,
		AppDbContext db,
		ILogger<DevController> logger)
	{
		_telegram = telegram;
		_botText = botText;
		_admin = admin;
		_cloudinary = cloudinary;
		_nowPayments = nowPayments;
		_configuration = configuration;
		_messageHandler = messageHandler;
		_nowPaymentsApi = nowPaymentsApi;
		_db = db;
		_logger = logger;
	}

	[HttpGet("ping")]
	public IActionResult Ping() => Ok("pong");

	[HttpGet("config")]
	public IActionResult Config()
	{
		var result = new
		{
			Telegram = _telegram.Value,
			BotText = _botText.Value,
			Admin = _admin.Value,
			Cloudinary = _cloudinary.Value,
			ConnectionString = _configuration.GetConnectionString("Postgres")
		};
		return Ok(result);
	}

	/// <summary>
	/// Emulate a text message from Telegram
	/// </summary>
	[HttpPost("emulate/message")]
	public async Task<IActionResult> EmulateMessage(
		[FromQuery] long chatId,
		[FromQuery] string text,
		CancellationToken cancellationToken)
	{
		var update = new Update
		{
			UpdateId = Interlocked.Increment(ref _updateIdCounter),
			Message = new TgMessage
			{
				MessageId = Interlocked.Increment(ref _messageIdCounter),
				Chat = new TgChat { Id = chatId },
				Text = text
			}
		};

		await _messageHandler.HandleUpdateAsync(update, cancellationToken).ConfigureAwait(false);
		return Ok(new { message = "Message processed", update });
	}

	/// <summary>
	/// Emulate a callback query from Telegram
	/// </summary>
	[HttpPost("emulate/callback")]
	public async Task<IActionResult> EmulateCallback(
		[FromQuery] long chatId,
		[FromQuery] string data,
		[FromQuery] long? messageId = null)
	{
		var callbackMessage = new TgMessage
		{
			MessageId = messageId ?? Interlocked.Increment(ref _messageIdCounter),
			Chat = new TgChat { Id = chatId },
			Text = null
		};

		var update = new Update
		{
			UpdateId = Interlocked.Increment(ref _updateIdCounter),
			CallbackQuery = new CallbackQuery
			{
				Id = $"dev_callback_{Interlocked.Increment(ref _callbackIdCounter)}",
				Data = data,
				Message = callbackMessage
			}
		};

		await _messageHandler.HandleUpdateAsync(update, CancellationToken.None).ConfigureAwait(false);
		return Ok(new { message = "Callback processed", update });
	}

	///// <summary>
	///// Emulate a message with photo from Telegram
	///// </summary>
	//[HttpPost("emulate/photo")]
	//public async Task<IActionResult> EmulatePhoto(
	//	[FromQuery] long chatId,
	//	[FromQuery] string fileId,
	//	[FromQuery] int width = 800,
	//	[FromQuery] int height = 600,
	//	CancellationToken cancellationToken)
	//{
	//	var update = new Update
	//	{
	//		UpdateId = Interlocked.Increment(ref _updateIdCounter),
	//		Message = new TgMessage
	//		{
	//			MessageId = Interlocked.Increment(ref _messageIdCounter),
	//			Chat = new TgChat { Id = chatId },
	//			Text = null,
	//			Photo = new[]
	//			{
	//				new PhotoSize
	//				{
	//					FileId = fileId,
	//					Width = width,
	//					Height = height
	//				}
	//			}
	//		}
	//	};

	//	await _messageHandler.HandleUpdateAsync(update, cancellationToken).ConfigureAwait(false);
	//	return Ok(new { message = "Photo message processed", update });
	//}

	/// <summary>
	/// Emulate a full Update object (most flexible)
	/// </summary>
	[HttpPost("emulate/update")]
	public async Task<IActionResult> EmulateUpdate(
		[FromBody] Update update,
		CancellationToken cancellationToken)
	{
		// Ensure update has a valid update_id
		var finalUpdate = new Update
		{
			UpdateId = update.UpdateId == 0 ? Interlocked.Increment(ref _updateIdCounter) : update.UpdateId,
			Message = update.Message,
			CallbackQuery = update.CallbackQuery
		};

		await _messageHandler.HandleUpdateAsync(finalUpdate, cancellationToken).ConfigureAwait(false);
		return Ok(new { message = "Update processed", update = finalUpdate });
	}

	/// <summary>
	/// Quick endpoint to test /start command
	/// </summary>
	[HttpPost("test/start")]
	public async Task<IActionResult> TestStart(
		[FromQuery] long chatId = 123456)
	{
		return await EmulateMessage(chatId, "/start", CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Quick endpoint to test admin wizard trigger
	/// </summary>
	[HttpPost("test/admin")]
	public async Task<IActionResult> TestAdmin(
		[FromQuery] long chatId = 123456)
	{
		var adminKey = _admin.Value.SecretKey;
		return await EmulateMessage(chatId, adminKey, CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Quick endpoint to test category callback
	/// </summary>
	[HttpPost("test/category")]
	public async Task<IActionResult> TestCategory(
		[FromQuery] long chatId = 123456,
		[FromQuery] string category = "choco")
	{
		return await EmulateCallback(chatId, $"cat:{category}").ConfigureAwait(false);
	}

	/// <summary>
	/// Generate a payment intent for testing
	/// </summary>
	[HttpPost("payment/create-intent")]
	public async Task<IActionResult> CreatePaymentIntent(
		[FromQuery] long? telegramChatId = null,
		[FromQuery] string? category = null,
		[FromQuery] decimal? priceAmount = null,
		[FromQuery] string? priceCurrency = null,
		[FromQuery] string? payCurrency = null,
		[FromQuery] string? orderDescription = null,
		[FromQuery] bool saveToDb = true,
		CancellationToken cancellationToken = default)
	{
		try
		{
			var options = _nowPayments.Value;
			var chatId = telegramChatId ?? 123456;
			var cat = category ?? "test";
			var amount = priceAmount ?? options.PriceAmount;
			var priceCurr = priceCurrency ?? options.PriceCurrency;
			var description = orderDescription ?? $"Test purchase: {cat}";

			// Generate unique order ID
			var orderId = $"DEV-ORDER-{chatId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

			_logger.LogInformation(
				"Creating dev payment intent: OrderId={OrderId}, Amount={Amount}, Currency={Currency}",
				orderId, amount, priceCurr);

			// Create payment via NowPayments API
			var paymentResponse = await _nowPaymentsApi.CreatePaymentAsync(
				amount,
				priceCurr,
				orderId,
				description,
				cancellationToken).ConfigureAwait(false);

			long? databaseId = null;

			// Optionally save to database
			if (saveToDb)
			{
				var payment = new Payment
				{
					TelegramChatId = chatId,
					Category = cat,
					PaymentId = paymentResponse.PaymentId,
					PaymentUrl = paymentResponse.PaymentUrl,
					PriceAmount = paymentResponse.PriceAmount,
					PriceCurrency = paymentResponse.PriceCurrency,
					PayCurrency = paymentResponse.PayCurrency,
					OrderId = paymentResponse.OrderId,
					Status = PaymentStatus.Pending,
					CreatedAtUtc = DateTime.UtcNow
				};

				_db.Payments.Add(payment);
				await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

				_logger.LogInformation("Payment saved to database. PaymentId: {PaymentId}, Id: {Id}", 
					paymentResponse.PaymentId, payment.Id);

				databaseId = payment.Id;
			}

			var result = new
			{
				success = true,
				payment = new
				{
					paymentId = paymentResponse.PaymentId,
					paymentStatus = paymentResponse.PaymentStatus,
					paymentUrl = paymentResponse.PaymentUrl,
					payAddress = paymentResponse.PayAddress,
					priceAmount = paymentResponse.PriceAmount,
					priceCurrency = paymentResponse.PriceCurrency,
					payAmount = paymentResponse.PayAmount,
					payCurrency = paymentResponse.PayCurrency,
					orderId = paymentResponse.OrderId,
					orderDescription = paymentResponse.OrderDescription,
					expirationAt = paymentResponse.ExpirationAt
				},
				savedToDatabase = saveToDb,
				databaseId = databaseId
			};

			return Ok(result);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create payment intent: {ExceptionType}: {ExceptionMessage}", 
				ex.GetType().Name, ex.Message);
			
			return StatusCode(500, new
			{
				success = false,
				error = ex.Message,
				errorType = ex.GetType().Name
			});
		}
	}

	/// <summary>
	/// Get payment status by payment ID
	/// </summary>
	[HttpGet("payment/status/{paymentId}")]
	public async Task<IActionResult> GetPaymentStatus(
		string paymentId,
		CancellationToken cancellationToken = default)
	{
		try
		{
			_logger.LogInformation("Getting payment status: PaymentId={PaymentId}", paymentId);

			var statusResponse = await _nowPaymentsApi.GetPaymentStatusAsync(paymentId, cancellationToken).ConfigureAwait(false);

			if (statusResponse == null)
			{
				return NotFound(new { success = false, message = "Payment not found" });
			}

			// Also check database
			var dbPayment = await _db.Payments
				.FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken).ConfigureAwait(false);

			return Ok(new
			{
				success = true,
				payment = new
				{
					paymentId = statusResponse.PaymentId,
					paymentStatus = statusResponse.PaymentStatus,
					payAddress = statusResponse.PayAddress,
					priceAmount = statusResponse.PriceAmount,
					priceCurrency = statusResponse.PriceCurrency,
					payAmount = statusResponse.PayAmount,
					payCurrency = statusResponse.PayCurrency,
					orderId = statusResponse.OrderId,
					orderDescription = statusResponse.OrderDescription,
					expirationAt = statusResponse.ExpirationAt
				},
				database = dbPayment != null ? new
				{
					id = dbPayment.Id,
					telegramChatId = dbPayment.TelegramChatId,
					category = dbPayment.Category,
					status = dbPayment.Status.ToString(),
					dataRecordId = dbPayment.DataRecordId,
					createdAtUtc = dbPayment.CreatedAtUtc,
					completedAtUtc = dbPayment.CompletedAtUtc
				} : null
			});
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get payment status: {ExceptionType}: {ExceptionMessage}", 
				ex.GetType().Name, ex.Message);
			
			return StatusCode(500, new
			{
				success = false,
				error = ex.Message,
				errorType = ex.GetType().Name
			});
		}
	}
}


