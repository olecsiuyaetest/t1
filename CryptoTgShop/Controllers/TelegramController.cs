using CryptoTgShop.Models.Telegram;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
		// Log all headers
		_logger.LogInformation("Webhook request received. All headers:");
		foreach (var (key, values) in Request.Headers)
		{
			_logger.LogInformation("Header: {HeaderName} = {HeaderValue}", key, string.Join(", ", values));
		}

		// Check and log the specific secret token header
		if (!Request.Headers.TryGetValue(SecretHeaderName, out var header) || header.Count == 0)
		{
			_logger.LogWarning("Webhook rejected: Secret token header '{HeaderName}' missing or empty", SecretHeaderName);
			return StatusCode(403); // Forbidden
		}

		var receivedToken = header[0];
		_logger.LogInformation("Secret token header value: {SecretToken}", receivedToken);
		_logger.LogInformation("Received token length: {ReceivedLength}, Expected token length: {ExpectedLength}", 
			receivedToken.Length, _telegramOptions.SecretToken.Length);

		if (!string.Equals(receivedToken, _telegramOptions.SecretToken, StringComparison.Ordinal))
		{
			_logger.LogWarning("Webhook rejected: Secret token mismatch. Received length: {ReceivedLength}, Expected length: {ExpectedLength}",
				receivedToken.Length, _telegramOptions.SecretToken.Length);
			return StatusCode(403); // Forbidden
		}

		_logger.LogInformation("Secret token validation passed");

		try
		{
			await _handler.HandleUpdateAsync(update, cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			// swallow errors to keep Telegram from retry storms
		}

		return Ok();
	}
}


