using CryptoTgShop.Models.Telegram;

namespace CryptoTgShop.Services.Interfaces;

public interface IUserMessageHandler
{
	Task HandleUpdateAsync(Update update, CancellationToken cancellationToken);
}


