using CryptoTgShop.Models.Telegram;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.Extensions.Options;
using CryptoTgShop.Data;
using CryptoTgShop.Data.Entities;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

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
	private readonly INowPaymentsApiClient _nowPaymentsApi;
	private readonly NowPaymentsOptions _nowPaymentsOptions;
	private readonly ILogger<UserMessageHandler> _logger;

	public UserMessageHandler(
		ITelegramApiClient api, 
		IOptions<BotTextOptions> texts, 
		IOptions<AdminOptions> admin, 
		IAdminWizardStore wizard, 
        IImageStorage imageStorage, 
        AppDbContext db,
        IServiceScopeFactory scopeFactory,
		INowPaymentsApiClient nowPaymentsApi,
		IOptions<NowPaymentsOptions> nowPaymentsOptions,
		ILogger<UserMessageHandler> logger)
	{
		_api = api;
		_texts = texts.Value;
        _admin = admin.Value;
        _wizard = wizard;
        _imageStorage = imageStorage;
        _db = db;
        _scopeFactory = scopeFactory;
		_nowPaymentsApi = nowPaymentsApi;
		_nowPaymentsOptions = nowPaymentsOptions.Value;
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
		var anyAvailable = await _db.DataRecords.AnyAsync(r => !r.IsUsed && r.Type == category, cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("Available items for category '{Category}': {AnyAvailable}", category, anyAvailable);
		
		if (!anyAvailable)
		{
			var validationMessage = _texts.CategoryValidationMessage.Replace("{category}", category, StringComparison.OrdinalIgnoreCase);
			_logger.LogInformation("No items available, showing validation message: {Message}", validationMessage);
			await _api.AnswerCallbackQueryAsync(callback.Id, validationMessage, false, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("========== HANDLE CALLBACK END ==========");
			return;
		}

		// Create payment intent with NowPayments
		try
		{
			_logger.LogInformation("Creating payment intent. Category: {Category}, ChatId: {ChatId}", category, chatId.Value);
			
			// Generate unique order ID
			var orderId = $"ORDER-{chatId.Value}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
			var orderDescription = $"Purchase: {category}";

			_logger.LogInformation("Creating payment via NowPayments API. OrderId: {OrderId}", orderId);
			var paymentResponse = await _nowPaymentsApi.CreatePaymentAsync(
				_nowPaymentsOptions.PriceAmount,
				_nowPaymentsOptions.PriceCurrency,
				orderId,
				orderDescription,
				cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Payment created: PaymentId={PaymentId}, PaymentUrl={PaymentUrl}, Status={Status}",
				paymentResponse.PaymentId, paymentResponse.PaymentUrl, paymentResponse.PaymentStatus);

			// Save payment to database
			var payment = new Payment
			{
				TelegramChatId = chatId.Value,
				Category = category,
				PaymentId = paymentResponse.PaymentId,
				PaymentUrl = paymentResponse.PaymentUrl,
				PriceAmount = paymentResponse.PriceAmount,
				PriceCurrency = paymentResponse.PriceCurrency,
				PayCurrency = paymentResponse.PayCurrency,
				OrderId = paymentResponse.OrderId,
				Status = PaymentStatus.Pending,
				CreatedAtUtc = DateTime.UtcNow
			};

			_db.Payments.Add(payment);
			await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Payment saved to database. PaymentId: {PaymentId}", payment.PaymentId);

			// Send payment link to user
			var paymentMessage = $"Please complete your payment:\n{paymentResponse.PaymentUrl}\n\nAfter payment is confirmed, you will receive your item automatically.";
			await _api.AnswerCallbackQueryAsync(callback.Id, null, false, cancellationToken).ConfigureAwait(false);
			await _api.SendMessageAsync(chatId.Value, paymentMessage, null, cancellationToken).ConfigureAwait(false);

			_logger.LogInformation("Payment link sent to user. ChatId: {ChatId}", chatId.Value);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create payment intent: {ExceptionType}: {ExceptionMessage}", 
				ex.GetType().Name, ex.Message);
			
			var errorMessage = "Failed to create payment. Please try again later.";
			await _api.AnswerCallbackQueryAsync(callback.Id, errorMessage, false, cancellationToken).ConfigureAwait(false);
			await _api.SendMessageAsync(chatId.Value, errorMessage, null, cancellationToken).ConfigureAwait(false);
		}

		_logger.LogInformation("========== HANDLE CALLBACK END ==========");
	}
}


