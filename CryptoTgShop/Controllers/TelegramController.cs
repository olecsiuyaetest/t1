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

	public TelegramController(IUserMessageHandler handler, IOptions<TelegramOptions> telegramOptions)
	{
		_handler = handler;
		_telegramOptions = telegramOptions.Value;
	}

	[HttpPost("webhook")]
	public async Task<IActionResult> Webhook([FromBody] Update update, CancellationToken cancellationToken)
	{
		if (!Request.Headers.TryGetValue(SecretHeaderName, out var header) || header.Count == 0)
		{
			return Forbid();
		}

		if (!string.Equals(header[0], _telegramOptions.SecretToken, StringComparison.Ordinal))
		{
			return Forbid();
		}

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


