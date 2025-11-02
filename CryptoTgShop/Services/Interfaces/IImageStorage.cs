namespace CryptoTgShop.Services.Interfaces;

public interface IImageStorage
{
	Task<string> UploadAsync(Stream content, string fileName, CancellationToken cancellationToken);
}


