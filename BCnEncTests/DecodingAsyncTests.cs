using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using BCnEncTests.Support;
using Xunit;

namespace BCnEncTests
{
	public class DecodingAsyncTests
	{
		[Fact]
		public async Task DecodeAsync()
		{
			var decoder = new BcDecoder();
			var encoder = new BcEncoder();
			var original = ImageLoader.TestGradient1;

			var file = encoder.EncodeToKtx(original);
			var image = await decoder.DecodeToImageRgba32Async(file);

			TestHelper.AssertImagesEqual(original, image,encoder.OutputOptions.Quality);
			image.Dispose();
		}

		[Fact]
		public async Task DecodeAllMipMapsAsync()
		{
			var decoder = new BcDecoder();
			var encoder = new BcEncoder();
			var original = ImageLoader.TestGradient1;

			var file = encoder.EncodeToKtx(original);
			var images = await decoder.DecodeAllMipMapsToImageRgba32Async(file);

			TestHelper.AssertImagesEqual(original, images[0], encoder.OutputOptions.Quality);
			foreach(var img in images)
				img.Dispose();
		}

		[Fact]
		public async Task DecodeRawAsync()
		{
			var decoder = new BcDecoder();
			var encoder = new BcEncoder();
			var original = ImageLoader.TestGradient1;

			var file = encoder.EncodeToKtx(original);

			var ms = new MemoryStream(file.MipMaps[0].Faces[0].Data);

			var image = await decoder.DecodeRawToImageRgba32Async(ms,
				(int) file.MipMaps[0].Width, (int) file.MipMaps[0].Height, CompressionFormat.Bc1);

			TestHelper.AssertImagesEqual(original, image, encoder.OutputOptions.Quality);
			image.Dispose();
		}
	}
}
