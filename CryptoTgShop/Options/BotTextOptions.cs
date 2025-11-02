using System.ComponentModel.DataAnnotations;

namespace CryptoTgShop.Options;

public sealed class BotTextOptions
{
	[Required]
	public string StartGreeting { get; init; } = string.Empty;

	[Required]
	public string ChooseCategory { get; init; } = string.Empty;

	[Required]
	public string SuccessText { get; init; } = string.Empty;

	[Required]
	[Url]
	public string PaymentLinkTemplate { get; init; } = string.Empty; // expects {category}

	[Required]
	public string[] CategoryLabels { get; init; } = Array.Empty<string>();

	// Admin wizard texts
	[Required]
	public string AdminPromptType { get; init; } = string.Empty;

	[Required]
	public string AdminPromptMessage { get; init; } = string.Empty;

	[Required]
	public string AdminPromptPhoto { get; init; } = string.Empty;

	[Required]
	public string AdminSavedText { get; init; } = string.Empty;

	[Required]
	public string NoItemsForCategory { get; init; } = string.Empty; // expects {category}
}


