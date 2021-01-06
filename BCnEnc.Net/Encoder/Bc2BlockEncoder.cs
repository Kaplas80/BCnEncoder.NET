﻿using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BCnEncoder.Shared;

namespace BCnEncoder.Encoder
{
	internal class Bc2BlockEncoder : IBcBlockEncoder
	{

		public unsafe byte[] Encode(RawBlock4X4Rgba32[] blocks, int blockWidth, int blockHeight, CompressionQuality quality, bool parallel)
		{
			byte[] outputData = new byte[blockWidth * blockHeight * sizeof(Bc2Block)];
			fixed (byte* oDataBytes = outputData)
			{
				Bc2Block* oDataBlocks = (Bc2Block*)oDataBytes;
				int oDataBlocksLength = outputData.Length / sizeof(Bc2Block);

				for (int i = 0; i < oDataBlocksLength; i++)
				{
					oDataBlocks[i] = EncodeBlock(blocks[i], quality);
				}
			}

			return outputData;
		}

		private Bc2Block EncodeBlock(RawBlock4X4Rgba32 block, CompressionQuality quality)
		{
			switch (quality)
			{
				case CompressionQuality.Fast:
					return Bc2BlockEncoderFast.EncodeBlock(block);
				case CompressionQuality.Balanced:
					return Bc2BlockEncoderBalanced.EncodeBlock(block);
				case CompressionQuality.BestQuality:
					return Bc2BlockEncoderSlow.EncodeBlock(block);

				default:
					throw new ArgumentOutOfRangeException(nameof(quality), quality, null);
			}
		}

		public GlInternalFormat GetInternalFormat()
		{
			return GlInternalFormat.GL_COMPRESSED_RGBA_S3TC_DXT3_EXT;
		}

		public GLFormat GetBaseInternalFormat()
		{
			return GLFormat.GL_RGBA;
		}

		public DXGI_FORMAT GetDxgiFormat() {
			return DXGI_FORMAT.DXGI_FORMAT_BC2_UNORM;
		}

		#region Encoding private stuff

		private static Bc2Block TryColors(RawBlock4X4Rgba32 rawBlock, ColorRgb565 color0, ColorRgb565 color1, out float error, float rWeight = 0.3f, float gWeight = 0.6f, float bWeight = 0.1f)
		{
			Bc2Block output = new Bc2Block();

			var pixels = rawBlock.AsArray;

			output.color0 = color0;
			output.color1 = color1;

			var c0 = color0.ToColorRgb24();
			var c1 = color1.ToColorRgb24();

			ColorRgb24[] colors = new ColorRgb24[] {
					c0,
					c1,
					c0 * (2.0 / 3.0) + c1 * (1.0 / 3.0),
					c0 * (1.0 / 3.0) + c1 * (2.0 / 3.0)
				};

			error = 0;
			for (int i = 0; i < 16; i++)
			{
				var color = pixels[i];
				output.SetAlpha(i, color.A);
				output[i] = ColorChooser.ChooseClosestColor4(colors, color, rWeight, gWeight, bWeight, out var e);
				error += e;
			}

			return output;
		}


		#endregion

		#region Encoders

		private static class Bc2BlockEncoderFast
		{

			internal static Bc2Block EncodeBlock(RawBlock4X4Rgba32 rawBlock)
			{
				var pixels = rawBlock.AsArray;

				PcaVectors.Create(pixels, out var mean, out var principalAxis);
				PcaVectors.GetMinMaxColor565(pixels, mean, principalAxis, out var min, out var max);
				
				ColorRgb565 c0 = max;
				ColorRgb565 c1 = min;

				var output = TryColors(rawBlock, c0, c1, out var _);

				return output;
			}
		}

		private static class Bc2BlockEncoderBalanced {
			private const int maxTries = 24 * 2;
			private const float errorThreshold = 0.05f;

			internal static Bc2Block EncodeBlock(RawBlock4X4Rgba32 rawBlock)
			{
				var pixels = rawBlock.AsArray;

				PcaVectors.Create(pixels, out System.Numerics.Vector3 mean, out System.Numerics.Vector3 pa);
				PcaVectors.GetMinMaxColor565(pixels, mean, pa, out var min, out var max);

				var c0 = max;
				var c1 = min;

				Bc2Block best = TryColors(rawBlock, c0, c1, out float bestError);
				
				for (int i = 0; i < maxTries; i++) {
					ColorVariationGenerator.Variate565(c0, c1, i, out var newC0, out var newC1);

					var block = TryColors(rawBlock, newC0, newC1, out var error);
					
					if (error < bestError)
					{
						best = block;
						bestError = error;
						c0 = newC0;
						c1 = newC1;
					}

					if (bestError < errorThreshold) {
						break;
					}
				}

				return best;
			}
		}

		private static class Bc2BlockEncoderSlow
		{
			private const int maxTries = 9999;
			private const float errorThreshold = 0.01f;


			internal static Bc2Block EncodeBlock(RawBlock4X4Rgba32 rawBlock)
			{
				var pixels = rawBlock.AsArray;

				PcaVectors.Create(pixels, out System.Numerics.Vector3 mean, out System.Numerics.Vector3 pa);
				PcaVectors.GetMinMaxColor565(pixels, mean, pa, out var min, out var max);

				var c0 = max;
				var c1 = min;

				if (c0.data < c1.data)
				{
					var c = c0;
					c0 = c1;
					c1 = c;
				}

				Bc2Block best = TryColors(rawBlock, c0, c1, out float bestError);

				int lastChanged = 0;

				for (int i = 0; i < maxTries; i++) {
					ColorVariationGenerator.Variate565(c0, c1, i, out var newC0, out var newC1);

					if (newC0.data < newC1.data)
					{
						var c = newC0;
						newC0 = newC1;
						newC1 = c;
					}
					
					var block = TryColors(rawBlock, newC0, newC1, out var error);

					lastChanged++;

					if (error < bestError)
					{
						best = block;
						bestError = error;
						c0 = newC0;
						c1 = newC1;
						lastChanged = 0;
					}

					if (bestError < errorThreshold || lastChanged > ColorVariationGenerator.VarPatternCount) {
						break;
					}
				}

				return best;
			}
		}
		
		#endregion
	}
}
