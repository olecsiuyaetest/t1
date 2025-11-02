using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CryptoTgShop.Services;

public sealed class TelegramApiClient : ITelegramApiClient
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
	private readonly HttpClient _httpClient;
    private readonly string _botToken;

	public TelegramApiClient(HttpClient httpClient, IOptions<TelegramOptions> telegramOptions)
	{
		_httpClient = httpClient;
		_botToken = telegramOptions.Value.BotToken;
		_httpClient.BaseAddress = new Uri($"https://api.telegram.org/bot{_botToken}/");
		_httpClient.Timeout = TimeSpan.FromSeconds(10);
	}

	public async Task SendMessageAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken)
	{
		var payload = new Dictionary<string, object?>
		{
			["chat_id"] = chatId,
			["text"] = text,
		};

		if (replyMarkup is not null)
		{
			payload["reply_markup"] = replyMarkup;
		}

		await PostJsonAsync("sendMessage", payload, cancellationToken).ConfigureAwait(false);
	}

	public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, bool showAlert, CancellationToken cancellationToken)
	{
		var payload = new Dictionary<string, object?>
		{
			["callback_query_id"] = callbackQueryId,
			["text"] = text,
			["show_alert"] = showAlert
		};
		return PostJsonAsync("answerCallbackQuery", payload, cancellationToken);
	}

	private async Task PostJsonAsync(string path, object body, CancellationToken cancellationToken)
	{
		var json = JsonSerializer.Serialize(body, SerializerOptions);
		using var content = new StringContent(json, Encoding.UTF8, "application/json");
		using var response = await _httpClient.PostAsync(path, content, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
	}

	public async Task<string> GetFilePathAsync(string fileId, CancellationToken cancellationToken)
	{
		var url = $"getFile?file_id={Uri.EscapeDataString(fileId)}";
		using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
		var root = doc.RootElement;
		var ok = root.GetProperty("ok").GetBoolean();
		if (!ok) throw new InvalidOperationException("getFile failed");
		return root.GetProperty("result").GetProperty("file_path").GetString()!;
	}

	public async Task<Stream> DownloadFileAsync(string filePath, CancellationToken cancellationToken)
	{
		var fileUrl = new Uri($"https://api.telegram.org/file/bot{_botToken}/{filePath}");
		var response = await _httpClient.GetAsync(fileUrl, cancellationToken).ConfigureAwait(false);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
	}

	public async Task SendPhotoAsync(long chatId, string photoUrl, string? caption, object? replyMarkup, CancellationToken cancellationToken)
	{
		var payload = new Dictionary<string, object?>
		{
			["chat_id"] = chatId,
			["photo"] = photoUrl,
			["caption"] = caption
		};
		if (replyMarkup is not null)
		{
			payload["reply_markup"] = replyMarkup;
		}
		await PostJsonAsync("sendPhoto", payload, cancellationToken).ConfigureAwait(false);
	}
}


