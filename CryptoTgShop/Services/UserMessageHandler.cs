using CryptoTgShop.Models.Telegram;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.Extensions.Options;
using CryptoTgShop.Data;
using CryptoTgShop.Data.Entities;

namespace CryptoTgShop.Services;

public sealed class UserMessageHandler : IUserMessageHandler
{
	private readonly ITelegramApiClient _api;
	private readonly BotTextOptions _texts;
    private readonly AdminOptions _admin;
    private readonly IAdminWizardStore _wizard;
    private readonly IImageStorage _imageStorage;
    private readonly AppDbContext _db;

	public UserMessageHandler(ITelegramApiClient api, IOptions<BotTextOptions> texts, IOptions<AdminOptions> admin, IAdminWizardStore wizard, IImageStorage imageStorage, AppDbContext db)
	{
		_api = api;
		_texts = texts.Value;
        _admin = admin.Value;
        _wizard = wizard;
        _imageStorage = imageStorage;
        _db = db;
	}

	public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
	{
		if (update.Message is { } message)
		{
			await HandleMessageAsync(message, cancellationToken).ConfigureAwait(false);
			return;
		}

		if (update.CallbackQuery is { } callback)
		{
			await HandleCallbackAsync(callback, cancellationToken).ConfigureAwait(false);
			return;
		}
	}

	private async Task HandleMessageAsync(TgMessage message, CancellationToken cancellationToken)
	{
		var text = message.Text ?? string.Empty;
		if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
		{
			var keyboard = new
			{
				inline_keyboard = new[]
				{
					_texts.CategoryLabels.Select(label => new { text = label, callback_data = $"cat:{label}" }).ToArray()
				}
			};

			await _api.SendMessageAsync(message.Chat.Id, _texts.ChooseCategory, keyboard, cancellationToken).ConfigureAwait(false);
			return;
		}

        // Admin wizard trigger
        if (string.Equals(text, _admin.SecretKey, StringComparison.Ordinal))
        {
            var state = _wizard.GetOrCreate(message.Chat.Id);
            state.Step = WizardStep.AwaitingType;
            await _api.SendMessageAsync(message.Chat.Id, _texts.AdminPromptType, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Wizard steps
        var current = _wizard.GetOrCreate(message.Chat.Id);
        if (current.Step == WizardStep.AwaitingType && !string.IsNullOrWhiteSpace(text))
        {
            current.Type = text.Trim();
            current.Step = WizardStep.AwaitingMessage;
            await _api.SendMessageAsync(message.Chat.Id, _texts.AdminPromptMessage, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (current.Step == WizardStep.AwaitingMessage && !string.IsNullOrWhiteSpace(text))
        {
            current.Message = text.Trim();
            current.Step = WizardStep.AwaitingPhoto;
            await _api.SendMessageAsync(message.Chat.Id, _texts.AdminPromptPhoto, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (current.Step == WizardStep.AwaitingPhoto && message.Photo is { Length: > 0 })
        {
            var photo = message.Photo.OrderByDescending(p => p.Width * p.Height).First();
            var filePath = await _api.GetFilePathAsync(photo.FileId, cancellationToken).ConfigureAwait(false);
            using var fileStream = await _api.DownloadFileAsync(filePath, cancellationToken).ConfigureAwait(false);
            var fileName = System.IO.Path.GetFileName(filePath);
            var url = await _imageStorage.UploadAsync(fileStream, fileName, cancellationToken).ConfigureAwait(false);

            var entity = new DataRecord
            {
                Message = current.Message ?? string.Empty,
                Type = current.Type ?? string.Empty,
                ImageUrl = url,
                IsUsed = false
            };
            _db.DataRecords.Add(entity);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _wizard.Clear(message.Chat.Id);
            await _api.SendMessageAsync(message.Chat.Id, _texts.AdminSavedText, null, cancellationToken).ConfigureAwait(false);
            return;
        }
	}

	private async Task HandleCallbackAsync(CallbackQuery callback, CancellationToken cancellationToken)
	{
		var data = callback.Data ?? string.Empty;
		if (!data.StartsWith("cat:", StringComparison.Ordinal))
		{
			await _api.AnswerCallbackQueryAsync(callback.Id, null, false, cancellationToken).ConfigureAwait(false);
			return;
		}

		var category = data.Substring("cat:".Length);
		var chatId = callback.Message?.Chat.Id;
		if (chatId is null)
		{
			await _api.AnswerCallbackQueryAsync(callback.Id, null, false, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Check availability before payment
		var anyAvailable = _db.DataRecords.Any(r => !r.IsUsed && r.Type == category);
		if (!anyAvailable)
		{
			var noItemsText = _texts.NoItemsForCategory.Replace("{category}", category, StringComparison.OrdinalIgnoreCase);
			await _api.AnswerCallbackQueryAsync(callback.Id, null, false, cancellationToken).ConfigureAwait(false);
			await _api.SendMessageAsync(chatId.Value, noItemsText, null, cancellationToken).ConfigureAwait(false);
			return;
		}

		var paymentLink = _texts.PaymentLinkTemplate.Replace("{category}", category, StringComparison.OrdinalIgnoreCase);
		await _api.AnswerCallbackQueryAsync(callback.Id, null, false, cancellationToken).ConfigureAwait(false);
		await _api.SendMessageAsync(chatId.Value, paymentLink, null, cancellationToken).ConfigureAwait(false);

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);

				// Select a random available item for the category
				DataRecord? record;
				await using (var scope = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider()) { }
				record = _db.DataRecords
					.Where(r => !r.IsUsed && r.Type == category)
					.OrderBy(r => Guid.NewGuid())
					.FirstOrDefault();

				if (record is null)
				{
					var noItems = _texts.NoItemsForCategory.Replace("{category}", category, StringComparison.OrdinalIgnoreCase);
					await _api.SendMessageAsync(chatId.Value, noItems, null, CancellationToken.None).ConfigureAwait(false);
					return;
				}

				await _api.SendPhotoAsync(chatId.Value, record.ImageUrl, record.Message, null, CancellationToken.None).ConfigureAwait(false);
				record.IsUsed = true;
				await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch
			{
				// ignore background errors
			}
		});
	}
}


