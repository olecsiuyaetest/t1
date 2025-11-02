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
	private readonly ILogger<TelegramApiClient> _logger;

	public TelegramApiClient(HttpClient httpClient, IOptions<TelegramOptions> telegramOptions, ILogger<TelegramApiClient> logger)
	{
		_httpClient = httpClient;
		_botToken = telegramOptions.Value.BotToken;
		_httpClient.BaseAddress = new Uri($"https://api.telegram.org/bot{_botToken}/");
		_httpClient.Timeout = TimeSpan.FromSeconds(10);
		_logger = logger;
		_logger.LogInformation("TelegramApiClient initialized. BaseAddress: {BaseAddress}, BotToken length: {TokenLength}", 
			_httpClient.BaseAddress, _botToken?.Length ?? 0);
	}

	public async Task SendMessageAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== TELEGRAM API: sendMessage START ==========");
		_logger.LogInformation("ChatId: {ChatId}", chatId);
		_logger.LogInformation("Text: {Text}", text);
		_logger.LogInformation("HasReplyMarkup: {HasReplyMarkup}", replyMarkup != null);
		
		var payload = new Dictionary<string, object?>
		{
			["chat_id"] = chatId,
			["text"] = text,
		};

		if (replyMarkup is not null)
		{
			payload["reply_markup"] = replyMarkup;
			try
			{
				var replyMarkupJson = JsonSerializer.Serialize(replyMarkup, SerializerOptions);
				_logger.LogInformation("ReplyMarkup JSON: {ReplyMarkup}", replyMarkupJson);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to serialize replyMarkup");
			}
		}

		try
		{
			var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
			_logger.LogInformation("Request payload JSON: {Payload}", payloadJson);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to serialize payload");
		}

		await PostJsonAsync("sendMessage", payload, cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("========== TELEGRAM API: sendMessage END ==========");
	}

	public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, bool showAlert, CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== TELEGRAM API: answerCallbackQuery START ==========");
		_logger.LogInformation("CallbackQueryId: {CallbackQueryId}", callbackQueryId);
		_logger.LogInformation("Text: {Text}", text ?? "<null>");
		_logger.LogInformation("ShowAlert: {ShowAlert}", showAlert);
		
		var payload = new Dictionary<string, object?>
		{
			["callback_query_id"] = callbackQueryId,
			["text"] = text,
			["show_alert"] = showAlert
		};

		try
		{
			var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
			_logger.LogInformation("Request payload JSON: {Payload}", payloadJson);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to serialize payload");
		}

		var task = PostJsonAsync("answerCallbackQuery", payload, cancellationToken);
		_logger.LogInformation("========== TELEGRAM API: answerCallbackQuery END ==========");
		return task;
	}

	private async Task PostJsonAsync(string path, object body, CancellationToken cancellationToken)
	{
		var json = JsonSerializer.Serialize(body, SerializerOptions);
		_logger.LogInformation("[PostJsonAsync] Path: {Path}", path);
		_logger.LogInformation("[PostJsonAsync] Request JSON: {Json}", json);
		_logger.LogInformation("[PostJsonAsync] Full URL: {Url}", new Uri(_httpClient.BaseAddress!, path));
		
		using var content = new StringContent(json, Encoding.UTF8, "application/json");
		_logger.LogInformation("[PostJsonAsync] Content-Type: {ContentType}, ContentLength: {ContentLength}", 
			content.Headers.ContentType, content.Headers.ContentLength);
		
		var startTime = DateTime.UtcNow;
		_logger.LogInformation("[PostJsonAsync] Sending request at {Time}...", startTime);
		
		using var response = await _httpClient.PostAsync(path, content, cancellationToken).ConfigureAwait(false);
		var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
		
		_logger.LogInformation("[PostJsonAsync] Response received. StatusCode: {StatusCode}, Duration: {Duration}ms", 
			response.StatusCode, duration);
		_logger.LogInformation("[PostJsonAsync] Response Headers:");
		foreach (var header in response.Headers)
		{
			_logger.LogInformation("  {HeaderName}: {HeaderValue}", header.Key, string.Join(", ", header.Value));
		}
		
		var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("[PostJsonAsync] Response Body: {ResponseBody}", responseBody);
		
		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("[PostJsonAsync] Request failed with status {StatusCode}. Response: {ResponseBody}", 
				response.StatusCode, responseBody);
		}
		
		response.EnsureSuccessStatusCode();
		_logger.LogInformation("[PostJsonAsync] Request succeeded");
	}

	public async Task<string> GetFilePathAsync(string fileId, CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== TELEGRAM API: getFile START ==========");
		_logger.LogInformation("FileId: {FileId}", fileId);
		
		var url = $"getFile?file_id={Uri.EscapeDataString(fileId)}";
		var fullUrl = new Uri(_httpClient.BaseAddress!, url);
		_logger.LogInformation("Request URL: {Url}", fullUrl);
		
		var startTime = DateTime.UtcNow;
		using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
		var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
		
		_logger.LogInformation("Response StatusCode: {StatusCode}, Duration: {Duration}ms", response.StatusCode, duration);
		
		response.EnsureSuccessStatusCode();
		
		var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("Response Body: {ResponseBody}", responseBody);
		
		using var stream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));
		var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
		var root = doc.RootElement;
		
		_logger.LogInformation("Parsed JSON root. Has 'ok': {HasOk}, Has 'result': {HasResult}", 
			root.TryGetProperty("ok", out _), root.TryGetProperty("result", out _));
		
		var ok = root.GetProperty("ok").GetBoolean();
		_logger.LogInformation("Response 'ok' field: {Ok}", ok);
		
		if (!ok)
		{
			var errorDescription = root.TryGetProperty("description", out var desc) ? desc.GetString() : "<unknown>";
			_logger.LogError("getFile failed. Description: {Description}", errorDescription);
			throw new InvalidOperationException($"getFile failed: {errorDescription}");
		}
		
		var filePath = root.GetProperty("result").GetProperty("file_path").GetString()!;
		_logger.LogInformation("File path retrieved: {FilePath}", filePath);
		_logger.LogInformation("========== TELEGRAM API: getFile END ==========");
		return filePath;
	}

	public async Task<Stream> DownloadFileAsync(string filePath, CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== TELEGRAM API: downloadFile START ==========");
		_logger.LogInformation("FilePath: {FilePath}", filePath);
		
		var fileUrl = new Uri($"https://api.telegram.org/file/bot{_botToken}/{filePath}");
		_logger.LogInformation("Download URL: {Url}", fileUrl);
		
		var startTime = DateTime.UtcNow;
		var response = await _httpClient.GetAsync(fileUrl, cancellationToken).ConfigureAwait(false);
		var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
		
		_logger.LogInformation("Response StatusCode: {StatusCode}, Duration: {Duration}ms", response.StatusCode, duration);
		_logger.LogInformation("Response ContentLength: {ContentLength}", response.Content.Headers.ContentLength);
		_logger.LogInformation("Response ContentType: {ContentType}", response.Content.Headers.ContentType);
		
		response.EnsureSuccessStatusCode();
		
		var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("File downloaded. Stream length: {Length}", stream.Length);
		_logger.LogInformation("========== TELEGRAM API: downloadFile END ==========");
		return stream;
	}

	public async Task SendPhotoAsync(long chatId, string photoUrl, string? caption, object? replyMarkup, CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== TELEGRAM API: sendPhoto START ==========");
		_logger.LogInformation("ChatId: {ChatId}", chatId);
		_logger.LogInformation("PhotoUrl: {PhotoUrl}", photoUrl);
		_logger.LogInformation("Caption: {Caption}", caption ?? "<null>");
		_logger.LogInformation("HasReplyMarkup: {HasReplyMarkup}", replyMarkup != null);
		
		var payload = new Dictionary<string, object?>
		{
			["chat_id"] = chatId,
			["photo"] = photoUrl,
			["caption"] = caption
		};
		if (replyMarkup is not null)
		{
			payload["reply_markup"] = replyMarkup;
			try
			{
				var replyMarkupJson = JsonSerializer.Serialize(replyMarkup, SerializerOptions);
				_logger.LogInformation("ReplyMarkup JSON: {ReplyMarkup}", replyMarkupJson);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to serialize replyMarkup");
			}
		}

		try
		{
			var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
			_logger.LogInformation("Request payload JSON: {Payload}", payloadJson);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to serialize payload");
		}

		await PostJsonAsync("sendPhoto", payload, cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("========== TELEGRAM API: sendPhoto END ==========");
	}
}


