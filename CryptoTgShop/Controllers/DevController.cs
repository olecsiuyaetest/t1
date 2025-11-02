using CryptoTgShop.Models.Telegram;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CryptoTgShop.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DevController : ControllerBase
{
	private readonly IOptions<TelegramOptions> _telegram;
	private readonly IOptions<BotTextOptions> _botText;
	private readonly IOptions<AdminOptions> _admin;
	private readonly IOptions<CloudinaryOptions> _cloudinary;
	private readonly IConfiguration _configuration;
	private readonly IUserMessageHandler _messageHandler;
	private static long _updateIdCounter = 1;
	private static long _messageIdCounter = 1;
	private static int _callbackIdCounter = 1;

	public DevController(
		IOptions<TelegramOptions> telegram,
		IOptions<BotTextOptions> botText,
		IOptions<AdminOptions> admin,
		IOptions<CloudinaryOptions> cloudinary,
		IConfiguration configuration,
		IUserMessageHandler messageHandler)
	{
		_telegram = telegram;
		_botText = botText;
		_admin = admin;
		_cloudinary = cloudinary;
		_configuration = configuration;
		_messageHandler = messageHandler;
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
		[FromQuery] long? messageId = null,
		CancellationToken cancellationToken)
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

		await _messageHandler.HandleUpdateAsync(update, cancellationToken).ConfigureAwait(false);
		return Ok(new { message = "Callback processed", update });
	}

	/// <summary>
	/// Emulate a message with photo from Telegram
	/// </summary>
	[HttpPost("emulate/photo")]
	public async Task<IActionResult> EmulatePhoto(
		[FromQuery] long chatId,
		[FromQuery] string fileId,
		[FromQuery] int width = 800,
		[FromQuery] int height = 600,
		CancellationToken cancellationToken)
	{
		var update = new Update
		{
			UpdateId = Interlocked.Increment(ref _updateIdCounter),
			Message = new TgMessage
			{
				MessageId = Interlocked.Increment(ref _messageIdCounter),
				Chat = new TgChat { Id = chatId },
				Text = null,
				Photo = new[]
				{
					new PhotoSize
					{
						FileId = fileId,
						Width = width,
						Height = height
					}
				}
			}
		};

		await _messageHandler.HandleUpdateAsync(update, cancellationToken).ConfigureAwait(false);
		return Ok(new { message = "Photo message processed", update });
	}

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
		[FromQuery] long chatId = 123456,
		CancellationToken cancellationToken)
	{
		return await EmulateMessage(chatId, "/start", cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Quick endpoint to test admin wizard trigger
	/// </summary>
	[HttpPost("test/admin")]
	public async Task<IActionResult> TestAdmin(
		[FromQuery] long chatId = 123456,
		CancellationToken cancellationToken)
	{
		var adminKey = _admin.Value.SecretKey;
		return await EmulateMessage(chatId, adminKey, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Quick endpoint to test category callback
	/// </summary>
	[HttpPost("test/category")]
	public async Task<IActionResult> TestCategory(
		[FromQuery] long chatId = 123456,
		[FromQuery] string category = "choco",
		CancellationToken cancellationToken)
	{
		return await EmulateCallback(chatId, $"cat:{category}", cancellationToken: cancellationToken).ConfigureAwait(false);
	}
}


