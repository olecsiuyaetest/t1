using CryptoTgShop.Options;
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

	public DevController(
		IOptions<TelegramOptions> telegram,
		IOptions<BotTextOptions> botText,
		IOptions<AdminOptions> admin,
		IOptions<CloudinaryOptions> cloudinary,
		IConfiguration configuration)
	{
		_telegram = telegram;
		_botText = botText;
		_admin = admin;
		_cloudinary = cloudinary;
		_configuration = configuration;
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
}


