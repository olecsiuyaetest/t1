using CryptoTgShop.Models.Telegram;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.Extensions.Options;
using CryptoTgShop.Data;
using CryptoTgShop.Data.Entities;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoTgShop.Services;

public sealed class UserMessageHandler : IUserMessageHandler
{
	private readonly ITelegramApiClient _api;
	private readonly BotTextOptions _texts;
    private readonly AdminOptions _admin;
    private readonly IAdminWizardStore _wizard;
    private readonly IImageStorage _imageStorage;
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<UserMessageHandler> _logger;

	public UserMessageHandler(
		ITelegramApiClient api, 
		IOptions<BotTextOptions> texts, 
		IOptions<AdminOptions> admin, 
		IAdminWizardStore wizard, 
        IImageStorage imageStorage, 
        AppDbContext db,
        IServiceScopeFactory scopeFactory,
		ILogger<UserMessageHandler> logger)
	{
		_api = api;
		_texts = texts.Value;
        _admin = admin.Value;
        _wizard = wizard;
        _imageStorage = imageStorage;
        _db = db;
        _scopeFactory = scopeFactory;
		_logger = logger;
	}

	public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== HANDLE UPDATE START ==========");
		_logger.LogInformation("UpdateId: {UpdateId}", update.UpdateId);
		
		try
		{
			var updateJson = JsonSerializer.Serialize(update, new JsonSerializerOptions { WriteIndented = true });
			_logger.LogInformation("Full Update JSON: {UpdateJson}", updateJson);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to serialize update");
		}

		if (update.Message is { } message)
		{
			_logger.LogInformation("Processing as Message");
			await HandleMessageAsync(message, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("========== HANDLE UPDATE END ==========");
			return;
		}

		if (update.CallbackQuery is { } callback)
		{
			_logger.LogInformation("Processing as CallbackQuery");
			await HandleCallbackAsync(callback, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("========== HANDLE UPDATE END ==========");
			return;
		}

		_logger.LogWarning("Update contains neither Message nor CallbackQuery");
		_logger.LogInformation("========== HANDLE UPDATE END ==========");
	}

	private async Task HandleMessageAsync(TgMessage message, CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== HANDLE MESSAGE START ==========");
		_logger.LogInformation("MessageId: {MessageId}", message.MessageId);
		_logger.LogInformation("ChatId: {ChatId}", message.Chat.Id);
		_logger.LogInformation("Text: {Text}", message.Text ?? "<null>");
		_logger.LogInformation("HasPhoto: {HasPhoto}", message.Photo != null);
		
		if (message.Photo != null && message.Photo.Length > 0)
		{
			_logger.LogInformation("Photos: {Count}", message.Photo.Length);
			for (int i = 0; i < message.Photo.Length; i++)
			{
				var photo = message.Photo[i];
				_logger.LogInformation("  Photo[{Index}]: FileId={FileId}, Width={Width}, Height={Height}, Size={Size}", 
					i, photo.FileId, photo.Width, photo.Height, photo.Width * photo.Height);
			}
		}

		try
		{
			var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });
			_logger.LogInformation("Full Message JSON: {MessageJson}", messageJson);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to serialize message");
		}

		var text = message.Text ?? string.Empty;
		_logger.LogInformation("Processing text: '{Text}'", text);
		
		if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
		{
			_logger.LogInformation("Handling /start command");
			var keyboard = new
			{
				inline_keyboard = new[]
				{
					_texts.CategoryLabels.Select(label => new { text = label, callback_data = $"cat:{label}" }).ToArray()
				}
			};

			_logger.LogInformation("Category labels: {Labels}", string.Join(", ", _texts.CategoryLabels));
			_logger.LogInformation("Sending message with keyboard to ChatId: {ChatId}", message.Chat.Id);
			await _api.SendMessageAsync(message.Chat.Id, _texts.ChooseCategory, keyboard, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("========== HANDLE MESSAGE END ==========");
			return;
		}

        // Admin wizard trigger
        _logger.LogInformation("Checking admin secret key. Text matches: {Matches}", string.Equals(text, _admin.SecretKey, StringComparison.Ordinal));
        if (string.Equals(text, _admin.SecretKey, StringComparison.Ordinal))
        {
            _logger.LogInformation("Admin wizard triggered for ChatId: {ChatId}", message.Chat.Id);
            var state = _wizard.GetOrCreate(message.Chat.Id);
            _logger.LogInformation("Wizard state before: Step={Step}, Type={Type}, Message={Message}", 
                state.Step, state.Type ?? "<null>", state.Message ?? "<null>");
            state.Step = WizardStep.AwaitingType;
            _logger.LogInformation("Wizard state after: Step={Step}", state.Step);
            _logger.LogInformation("Sending AdminPromptType to ChatId: {ChatId}", message.Chat.Id);
            await _api.SendMessageAsync(message.Chat.Id, _texts.AdminPromptType, null, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("========== HANDLE MESSAGE END ==========");
            return;
        }

        // Wizard steps
        var current = _wizard.GetOrCreate(message.Chat.Id);
        _logger.LogInformation("Current wizard state: Step={Step}, Type={Type}, Message={Message}", 
            current.Step, current.Type ?? "<null>", current.Message ?? "<null>");
        
        if (current.Step == WizardStep.AwaitingType && !string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation("Wizard step: AwaitingType, received text: '{Text}'", text);
            current.Type = text.Trim();
            current.Step = WizardStep.AwaitingMessage;
            _logger.LogInformation("Wizard state updated: Step={Step}, Type={Type}", current.Step, current.Type);
            _logger.LogInformation("Sending AdminPromptMessage to ChatId: {ChatId}", message.Chat.Id);
            await _api.SendMessageAsync(message.Chat.Id, _texts.AdminPromptMessage, null, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("========== HANDLE MESSAGE END ==========");
            return;
        }

        if (current.Step == WizardStep.AwaitingMessage && !string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation("Wizard step: AwaitingMessage, received text: '{Text}'", text);
            current.Message = text.Trim();
            current.Step = WizardStep.AwaitingPhoto;
            _logger.LogInformation("Wizard state updated: Step={Step}, Message={Message}", current.Step, current.Message);
            _logger.LogInformation("Sending AdminPromptPhoto to ChatId: {ChatId}", message.Chat.Id);
            await _api.SendMessageAsync(message.Chat.Id, _texts.AdminPromptPhoto, null, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("========== HANDLE MESSAGE END ==========");
            return;
        }

        if (current.Step == WizardStep.AwaitingPhoto && message.Photo is { Length: > 0 })
        {
            _logger.LogInformation("Wizard step: AwaitingPhoto, received {Count} photos", message.Photo.Length);
            var photo = message.Photo.OrderByDescending(p => p.Width * p.Height).First();
            _logger.LogInformation("Selected largest photo: FileId={FileId}, Width={Width}, Height={Height}", 
                photo.FileId, photo.Width, photo.Height);
            
            _logger.LogInformation("Getting file path for FileId: {FileId}", photo.FileId);
            var filePath = await _api.GetFilePathAsync(photo.FileId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("File path received: {FilePath}", filePath);
            
            _logger.LogInformation("Downloading file from path: {FilePath}", filePath);
            using var fileStream = await _api.DownloadFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            var fileName = System.IO.Path.GetFileName(filePath);
            _logger.LogInformation("File downloaded, fileName: {FileName}, Stream length: {Length}", fileName, fileStream.Length);
            
            _logger.LogInformation("Uploading to image storage: fileName={FileName}", fileName);
            var url = await _imageStorage.UploadAsync(fileStream, fileName, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Image uploaded, URL: {Url}", url);

            var entity = new DataRecord
            {
                Message = current.Message ?? string.Empty,
                Type = current.Type ?? string.Empty,
                ImageUrl = url,
                IsUsed = false
            };
            _logger.LogInformation("Creating DataRecord: Type={Type}, Message={Message}, ImageUrl={ImageUrl}, IsUsed={IsUsed}", 
                entity.Type, entity.Message, entity.ImageUrl, entity.IsUsed);
            
            _db.DataRecords.Add(entity);
            _logger.LogInformation("Saving to database...");
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Database save completed. Entity ID (if available): {EntityId}", entity.Id);

            _logger.LogInformation("Clearing wizard state for ChatId: {ChatId}", message.Chat.Id);
            _wizard.Clear(message.Chat.Id);
            
            _logger.LogInformation("Sending AdminSavedText to ChatId: {ChatId}", message.Chat.Id);
            await _api.SendMessageAsync(message.Chat.Id, _texts.AdminSavedText, null, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("========== HANDLE MESSAGE END ==========");
            return;
        }

        _logger.LogInformation("Message did not match any handler. Step={Step}, HasText={HasText}, HasPhoto={HasPhoto}", 
            current.Step, !string.IsNullOrWhiteSpace(text), message.Photo != null);
        _logger.LogInformation("========== HANDLE MESSAGE END ==========");
	}

	private async Task HandleCallbackAsync(CallbackQuery callback, CancellationToken cancellationToken)
	{
		_logger.LogInformation("========== HANDLE CALLBACK START ==========");
		_logger.LogInformation("CallbackId: {CallbackId}", callback.Id);
		_logger.LogInformation("CallbackData: {Data}", callback.Data ?? "<null>");
		_logger.LogInformation("HasMessage: {HasMessage}", callback.Message != null);
		
		if (callback.Message != null)
		{
			_logger.LogInformation("Callback Message: MessageId={MessageId}, ChatId={ChatId}", 
				callback.Message.MessageId, callback.Message.Chat.Id);
		}

		try
		{
			var callbackJson = JsonSerializer.Serialize(callback, new JsonSerializerOptions { WriteIndented = true });
			_logger.LogInformation("Full CallbackQuery JSON: {CallbackJson}", callbackJson);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to serialize callback");
		}

		var data = callback.Data ?? string.Empty;
		_logger.LogInformation("Processing callback data: '{Data}'", data);
		
		if (!data.StartsWith("cat:", StringComparison.Ordinal))
		{
			_logger.LogWarning("Callback data does not start with 'cat:', answering callback without action");
			await _api.AnswerCallbackQueryAsync(callback.Id, null, false, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("========== HANDLE CALLBACK END ==========");
			return;
		}

		var category = data.Substring("cat:".Length);
		_logger.LogInformation("Extracted category: '{Category}'", category);
		
		var chatId = callback.Message?.Chat.Id;
		if (chatId is null)
		{
			_logger.LogWarning("Callback has no chat ID, answering callback without action");
			await _api.AnswerCallbackQueryAsync(callback.Id, null, false, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("========== HANDLE CALLBACK END ==========");
			return;
		}

		_logger.LogInformation("Checking database for available items. Category: {Category}", category);
		// Validate availability before proceeding with payment
		var anyAvailable = _db.DataRecords.Any(r => !r.IsUsed && r.Type == category);
		_logger.LogInformation("Available items for category '{Category}': {AnyAvailable}", category, anyAvailable);
		
		if (!anyAvailable)
		{
			var validationMessage = _texts.CategoryValidationMessage.Replace("{category}", category, StringComparison.OrdinalIgnoreCase);
			_logger.LogInformation("No items available, showing validation message: {Message}", validationMessage);
			await _api.AnswerCallbackQueryAsync(callback.Id, validationMessage, false, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("========== HANDLE CALLBACK END ==========");
			return;
		}

		var paymentLink = _texts.PaymentLinkTemplate.Replace("{category}", category, StringComparison.OrdinalIgnoreCase);
		_logger.LogInformation("Payment link generated: {PaymentLink}", paymentLink);
		_logger.LogInformation("Answering callback query and sending payment link to ChatId: {ChatId}", chatId.Value);
		await _api.AnswerCallbackQueryAsync(callback.Id, null, false, cancellationToken).ConfigureAwait(false);
		await _api.SendMessageAsync(chatId.Value, paymentLink, null, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Starting background task to send item after 10 seconds. Category: {Category}, ChatId: {ChatId}", category, chatId.Value);
		_ = Task.Run(async () =>
		{
			try
			{
				_logger.LogInformation("[Background] Waiting 10 seconds before sending item...");
				await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
				_logger.LogInformation("[Background] Delay completed, selecting item for category: {Category}", category);

                // Create a new DI scope for background DB operations
                using var scope = _scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Select a random available item for the category
                var record = scopedDb.DataRecords
                    .Where(r => !r.IsUsed && r.Type == category)
                    .OrderBy(r => Guid.NewGuid())
                    .FirstOrDefault();

				if (record is null)
				{
					_logger.LogWarning("[Background] No available items found for category: {Category}", category);
					var noItems = _texts.NoItemsForCategory.Replace("{category}", category, StringComparison.OrdinalIgnoreCase);
					await _api.SendMessageAsync(chatId.Value, noItems, null, CancellationToken.None).ConfigureAwait(false);
					_logger.LogInformation("[Background] Sent 'no items' message to ChatId: {ChatId}", chatId.Value);
					return;
				}

				_logger.LogInformation("[Background] Selected item: Type={Type}, Message={Message}, ImageUrl={ImageUrl}, IsUsed={IsUsed}", 
					record.Type, record.Message, record.ImageUrl, record.IsUsed);
				_logger.LogInformation("[Background] Sending photo to ChatId: {ChatId}", chatId.Value);
				await _api.SendPhotoAsync(chatId.Value, record.ImageUrl, record.Message, null, CancellationToken.None).ConfigureAwait(false);
				
				_logger.LogInformation("[Background] Marking item as used. Record ID: {Id}", record.Id);
                record.IsUsed = true;
                await scopedDb.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
				_logger.LogInformation("[Background] Item marked as used and saved to database");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[Background] Exception in background task: {ExceptionType}: {ExceptionMessage}\nStackTrace: {StackTrace}", 
					ex.GetType().Name, ex.Message, ex.StackTrace);
				// ignore background errors
			}
		});
		_logger.LogInformation("========== HANDLE CALLBACK END ==========");
	}
}


