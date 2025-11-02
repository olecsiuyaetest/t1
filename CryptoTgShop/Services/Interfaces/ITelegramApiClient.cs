using System.Text.Json.Nodes;

namespace CryptoTgShop.Services.Interfaces;

public interface ITelegramApiClient
{
	Task SendMessageAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken);
	Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, bool showAlert, CancellationToken cancellationToken);
	Task<string> GetFilePathAsync(string fileId, CancellationToken cancellationToken);
	Task<Stream> DownloadFileAsync(string filePath, CancellationToken cancellationToken);
	Task SendPhotoAsync(long chatId, string photoUrl, string? caption, object? replyMarkup, CancellationToken cancellationToken);
}


