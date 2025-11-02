using System.Collections.Concurrent;

namespace CryptoTgShop.Services;

public enum WizardStep
{
	None,
	AwaitingType,
	AwaitingMessage,
	AwaitingPhoto
}

public sealed class AdminWizardState
{
	public WizardStep Step { get; set; }
	public string? Type { get; set; }
	public string? Message { get; set; }
}

public interface IAdminWizardStore
{
	AdminWizardState GetOrCreate(long chatId);
	void Clear(long chatId);
}

public sealed class AdminWizardStore : IAdminWizardStore
{
	private readonly ConcurrentDictionary<long, AdminWizardState> _state = new();

	public AdminWizardState GetOrCreate(long chatId)
	{
		return _state.GetOrAdd(chatId, _ => new AdminWizardState { Step = WizardStep.None });
	}

	public void Clear(long chatId)
	{
		_state.TryRemove(chatId, out _);
	}
}


