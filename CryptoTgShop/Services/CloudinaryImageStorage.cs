using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CryptoTgShop.Options;
using CryptoTgShop.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CryptoTgShop.Services;

public sealed class CloudinaryImageStorage : IImageStorage
{
	private readonly Cloudinary _cloudinary;

	public CloudinaryImageStorage(IOptions<CloudinaryOptions> options)
	{
		var opt = options.Value;
		var account = new Account(opt.CloudName, opt.ApiKey, opt.ApiSecret);
		_cloudinary = new Cloudinary(account);
	}

	public async Task<string> UploadAsync(Stream content, string fileName, CancellationToken cancellationToken)
	{
		var uploadParams = new ImageUploadParams
		{
			File = new FileDescription(fileName, content),
			UseFilename = true,
			UniqueFilename = true,
			Overwrite = false
		};
		var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken).ConfigureAwait(false);
		if (result.StatusCode is not System.Net.HttpStatusCode.OK)
		{
			throw new InvalidOperationException("Cloudinary upload failed");
		}
		return result.SecureUrl?.ToString() ?? throw new InvalidOperationException("No URL returned");
	}
}


