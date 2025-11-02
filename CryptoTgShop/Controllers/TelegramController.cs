using CryptoTgShop.Models.Telegram;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CryptoTgShop.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TelegramController : ControllerBase
{
	private const string SecretHeaderName = "X-Telegram-Bot-Api-Secret-Token";
	private readonly IUserMessageHandler _handler;
	private readonly TelegramOptions _telegramOptions;
	private readonly ILogger<TelegramController> _logger;

	public TelegramController(
		IUserMessageHandler handler,
		IOptions<TelegramOptions> telegramOptions,
		ILogger<TelegramController> logger)
	{
		_handler = handler;
		_telegramOptions = telegramOptions.Value;
		_logger = logger;
	}

	[HttpPost("webhook")]
	public async Task<IActionResult> Webhook([FromBody] Update update, CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== WEBHOOK REQUEST START ==========");
		_logger.LogInformation("Request Method: {Method}", Request.Method);
		_logger.LogInformation("Request Path: {Path}", Request.Path);
		_logger.LogInformation("Request QueryString: {QueryString}", Request.QueryString);
		_logger.LogInformation("Request ContentType: {ContentType}", Request.ContentType);
		_logger.LogInformation("Request ContentLength: {ContentLength}", Request.ContentLength);

		// Log all headers
		_logger.LogInformation("--- All Request Headers ---");
		foreach (var (key, values) in Request.Headers)
		{
			_logger.LogInformation("Header[{HeaderName}] = {HeaderValue}", key, string.Join(", ", values));
		}

		// Log request body if available
		if (Request.Body.CanSeek)
		{
			Request.Body.Position = 0;
			using var reader = new StreamReader(Request.Body, leaveOpen: true);
			var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Request Body (raw): {RequestBody}", body);
			Request.Body.Position = 0;
		}

		// Log full update object
		try
		{
			var updateJson = JsonSerializer.Serialize(update, new JsonSerializerOptions { WriteIndented = true });
			_logger.LogInformation("Update Object (serialized): {UpdateJson}", updateJson);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to serialize update object");
		}

		// Log update details
		_logger.LogInformation("Update Details:");
		_logger.LogInformation("  UpdateId: {UpdateId}", update?.UpdateId ?? -1);
		_logger.LogInformation("  HasMessage: {HasMessage}", update?.Message != null);
		_logger.LogInformation("  HasCallbackQuery: {HasCallbackQuery}", update?.CallbackQuery != null);

		if (update?.Message != null)
		{
			_logger.LogInformation("  Message Details:");
			_logger.LogInformation("    MessageId: {MessageId}", update.Message.MessageId);
			_logger.LogInformation("    ChatId: {ChatId}", update.Message.Chat.Id);
			_logger.LogInformation("    Text: {Text}", update.Message.Text ?? "<null>");
			_logger.LogInformation("    HasPhoto: {HasPhoto}", update.Message.Photo != null);
			if (update.Message.Photo != null && update.Message.Photo.Length > 0)
			{
				_logger.LogInformation("    PhotoCount: {Count}", update.Message.Photo.Length);
				for (int i = 0; i < update.Message.Photo.Length; i++)
				{
					var photo = update.Message.Photo[i];
					_logger.LogInformation("      Photo[{Index}]: FileId={FileId}, Width={Width}, Height={Height}", 
						i, photo.FileId, photo.Width, photo.Height);
				}
			}
		}

		if (update?.CallbackQuery != null)
		{
			_logger.LogInformation("  CallbackQuery Details:");
			_logger.LogInformation("    CallbackId: {CallbackId}", update.CallbackQuery.Id);
			_logger.LogInformation("    Data: {Data}", update.CallbackQuery.Data ?? "<null>");
			_logger.LogInformation("    HasMessage: {HasMessage}", update.CallbackQuery.Message != null);
			if (update.CallbackQuery.Message != null)
			{
				_logger.LogInformation("    MessageId: {MessageId}", update.CallbackQuery.Message.MessageId);
				_logger.LogInformation("    ChatId: {ChatId}", update.CallbackQuery.Message.Chat.Id);
			}
		}

		// Check and log the specific secret token header
		if (!Request.Headers.TryGetValue(SecretHeaderName, out var header) || header.Count == 0)
		{
			_logger.LogWarning("Webhook rejected: Secret token header '{HeaderName}' missing or empty", SecretHeaderName);
			_logger.LogInformation("========== WEBHOOK REQUEST END (REJECTED) ==========");
			return StatusCode(403); // Forbidden
		}

		var receivedToken = header[0];
		_logger.LogInformation("Secret token header value: {SecretToken}", receivedToken);
		_logger.LogInformation("Received token length: {ReceivedLength}, Expected token length: {ExpectedLength}", 
			receivedToken.Length, _telegramOptions.SecretToken.Length);
		_logger.LogInformation("Expected token (first 10 chars): {TokenPreview}", 
			_telegramOptions.SecretToken.Length > 10 ? _telegramOptions.SecretToken.Substring(0, 10) + "..." : _telegramOptions.SecretToken);

		if (!string.Equals(receivedToken, _telegramOptions.SecretToken, StringComparison.Ordinal))
		{
			_logger.LogWarning("Webhook rejected: Secret token mismatch. Received length: {ReceivedLength}, Expected length: {ExpectedLength}",
				receivedToken.Length, _telegramOptions.SecretToken.Length);
			_logger.LogInformation("========== WEBHOOK REQUEST END (REJECTED) ==========");
			return StatusCode(403); // Forbidden
		}

		_logger.LogInformation("Secret token validation passed");

		try
		{
			_logger.LogInformation("Passing update to handler...");
			await _handler.HandleUpdateAsync(update, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Handler completed successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Exception in handler: {ExceptionType}: {ExceptionMessage}\nStackTrace: {StackTrace}", 
				ex.GetType().Name, ex.Message, ex.StackTrace);
			// swallow errors to keep Telegram from retry storms
		}

		_logger.LogInformation("========== WEBHOOK REQUEST END ==========");
		return Ok();
	}
}


