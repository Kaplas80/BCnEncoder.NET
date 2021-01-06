﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BCnEncoder.Shared;

namespace BCnEncoder.Encoder
{
	internal class Bc5BlockEncoder : IBcBlockEncoder {

		public unsafe byte[] Encode(RawBlock4X4Rgba32[] blocks, int blockWidth, int blockHeight, CompressionQuality quality, bool parallel)
		{
			byte[] outputData = new byte[blockWidth * blockHeight * sizeof(Bc5Block)];
			fixed (byte* oDataBytes = outputData)
			{
				Bc5Block* oDataBlocks = (Bc5Block*)oDataBytes;
				int oDataBlocksLength = outputData.Length / sizeof(Bc5Block);

				for (int i = 0; i < oDataBlocksLength; i++)
				{
					oDataBlocks[i] = EncodeBlock(blocks[i], quality);
				}
			}

			return outputData;
		}

		private Bc5Block EncodeBlock(RawBlock4X4Rgba32 block, CompressionQuality quality) {
			Bc5Block output = new Bc5Block();
			byte[] reds = new byte[16];
			byte[] greens = new byte[16];
			var pixels = block.AsArray;
			for (int i = 0; i < 16; i++) {
				reds[i] = pixels[i].R;
				greens[i] = pixels[i].G;
			}

			int variations = 0;
			int errorThreshold = 0;
 			switch (quality) {
				case CompressionQuality.Fast:
					variations = 3;
					errorThreshold = 5;
					break;
				case CompressionQuality.Balanced:
					variations = 5;
					errorThreshold = 1;
					break;
				case CompressionQuality.BestQuality:
					variations = 8;
					errorThreshold = 0;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(quality), quality, null);
			}


            output =  FindValues(output, reds, variations, errorThreshold,
	            (blck, i, idx) => {
		            blck.SetRedIndex(i, idx);
		            return blck;
	            },
	            (blck, col) => {
		            blck.Red0 = col;
		            return blck;
	            },
	            (blck, col) => {
		            blck.Red1 = col;
		            return blck;
	            },
	            (blck) => {
		            return blck.Red0;
	            },
	            (blck) => {
		            return blck.Red1;
	            }
            );
            output =  FindValues(output, greens, variations, errorThreshold,
	            (blck, i, idx) => {
		            blck.SetGreenIndex(i, idx);
		            return blck;
	            },
	            (blck, col) => {
		            blck.Green0 = col;
		            return blck;
	            },
	            (blck, col) => {
		            blck.Green1 = col;
		            return blck;
	            },
	            (blck) => {
		            return blck.Green0;
	            },
	            (blck) => {
		            return blck.Green1;
	            });
            return output;
		}

		public GlInternalFormat GetInternalFormat() {
			return GlInternalFormat.GL_COMPRESSED_RED_GREEN_RGTC2_EXT;
		}

		public GLFormat GetBaseInternalFormat() {
			return GLFormat.GL_RG;
		}

		public DXGI_FORMAT GetDxgiFormat() {
			return DXGI_FORMAT.DXGI_FORMAT_BC5_UNORM;
		}

		#region Encoding private stuff

		private static int SelectIndices(ref Bc5Block block, byte[] pixels, 
			Func<Bc5Block, int, byte, Bc5Block> indexSetter,
			Func<Bc5Block, byte> col0Getter,
			Func<Bc5Block, byte> col1Getter)
		{
			int cumulativeError = 0;
			//var c0 = block.Red0;
			//var c1 = block.Red1;
			var c0 = col0Getter(block);
			var c1 = col1Getter(block);
			byte[] colors = c0 > c1
				? new byte[] {
					c0,
					c1,
					(byte) (6 / 7.0 * c0 + 1 / 7.0 * c1),
					(byte) (5 / 7.0 * c0 + 2 / 7.0 * c1),
					(byte) (4 / 7.0 * c0 + 3 / 7.0 * c1),
					(byte) (3 / 7.0 * c0 + 4 / 7.0 * c1),
					(byte) (2 / 7.0 * c0 + 5 / 7.0 * c1),
					(byte) (1 / 7.0 * c0 + 6 / 7.0 * c1),
				}
				: new byte[] {
					c0,
					c1,
					(byte) (4 / 5.0 * c0 + 1 / 5.0 * c1),
					(byte) (3 / 5.0 * c0 + 2 / 5.0 * c1),
					(byte) (2 / 5.0 * c0 + 3 / 5.0 * c1),
					(byte) (1 / 5.0 * c0 + 4 / 5.0 * c1),
					0,
					255
				};
			for (int i = 0; i < pixels.Length; i++)
			{
				byte bestIndex = 0;
				int bestError = Math.Abs(pixels[i] - colors[0]);
				for (byte j = 1; j < colors.Length; j++)
				{
					int error = Math.Abs(pixels[i] - colors[j]);
					if (error < bestError)
					{
						bestIndex = j;
						bestError = error;
					}

					if (bestError == 0) break;
				}

				block = indexSetter(block, i, bestIndex);
				//block.SetRedIndex(i, bestIndex);
				cumulativeError += bestError * bestError;
			}

			return cumulativeError;
		}

		private static Bc5Block FindValues(Bc5Block colorBlock, byte[] pixels, int variations, int errorThreshold, 
			Func<Bc5Block, int, byte, Bc5Block> indexSetter, 
			Func<Bc5Block, byte, Bc5Block> col0Setter,
			Func<Bc5Block, byte, Bc5Block> col1Setter,
			Func<Bc5Block, byte> col0Getter,
			Func<Bc5Block, byte> col1Getter) {

			//Find min and max alpha
			byte min = 255;
			byte max = 0;
			bool hasExtremeValues = false;
			for (int i = 0; i < pixels.Length; i++) {
				if (pixels[i] < 255 && pixels[i] > 0) {
					if (pixels[i] < min) min = pixels[i];
					if (pixels[i] > max) max = pixels[i];
				}
				else {
					hasExtremeValues = true;
				}
			}

			//everything is either fully black or fully red
			if (hasExtremeValues && min == 255 && max == 0) {
				//colorBlock.Red0 = 0;
				//colorBlock.Red1 = 255;
				colorBlock = col0Setter(colorBlock, 0);
				colorBlock = col1Setter(colorBlock, 255);
				var error = SelectIndices(ref colorBlock, pixels, indexSetter, col0Getter, col1Getter);
				Debug.Assert(0 == error);
				return colorBlock;
			}

			var best = colorBlock;
			//best.Red0 = max;
			//best.Red1 = min;
			best = col0Setter(best, max);
			best = col1Setter(best, min);
			int bestError = SelectIndices(ref best, pixels, indexSetter, col0Getter, col1Getter);
			if (bestError == 0) {
				return best;
			}

			for (byte i = (byte)variations; i > 0; i--) {
				{
					byte c0 = ByteHelper.ClampToByte(max - i);
					byte c1 = ByteHelper.ClampToByte(min + i);
					var block = colorBlock;
					//block.Red0 = hasExtremeValues ? c1 : c0;
					//block.Red1 = hasExtremeValues ? c0 : c1;
					block = col0Setter(block, hasExtremeValues ? c1 : c0);
					block = col1Setter(block, hasExtremeValues ? c0 : c1);
					int error = SelectIndices(ref block, pixels, indexSetter, col0Getter, col1Getter);
					if (error < bestError) {
						best = block;
						bestError = error;
						max = c0;
						min = c1;
					}
				}
				{
					byte c0 = ByteHelper.ClampToByte(max + i);
					byte c1 = ByteHelper.ClampToByte(min - i);
					var block = colorBlock;
					//block.Red0 = hasExtremeValues ? c1 : c0;
					//block.Red1 = hasExtremeValues ? c0 : c1;
					block = col0Setter(block, hasExtremeValues ? c1 : c0);
					block = col1Setter(block, hasExtremeValues ? c0 : c1);
					int error = SelectIndices(ref block, pixels, indexSetter, col0Getter, col1Getter);
					if (error < bestError) {
						best = block;
						bestError = error;
						max = c0;
						min = c1;
					}
				}
				{
					byte c0 = ByteHelper.ClampToByte(max);
					byte c1 = ByteHelper.ClampToByte(min - i);
					var block = colorBlock;
					//block.Red0 = hasExtremeValues ? c1 : c0;
					//block.Red1 = hasExtremeValues ? c0 : c1;
					block = col0Setter(block, hasExtremeValues ? c1 : c0);
					block = col1Setter(block, hasExtremeValues ? c0 : c1);
					int error = SelectIndices(ref block, pixels, indexSetter, col0Getter, col1Getter);
					if (error < bestError) {
						best = block;
						bestError = error;
						max = c0;
						min = c1;
					}
				}
				{
					byte c0 = ByteHelper.ClampToByte(max + i);
					byte c1 = ByteHelper.ClampToByte(min);
					var block = colorBlock;
					//block.Red0 = hasExtremeValues ? c1 : c0;
					//block.Red1 = hasExtremeValues ? c0 : c1;
					block = col0Setter(block, hasExtremeValues ? c1 : c0);
					block = col1Setter(block, hasExtremeValues ? c0 : c1);
					int error = SelectIndices(ref block, pixels, indexSetter, col0Getter, col1Getter);
					if (error < bestError) {
						best = block;
						bestError = error;
						max = c0;
						min = c1;
					}
				}
				{
					byte c0 = ByteHelper.ClampToByte(max);
					byte c1 = ByteHelper.ClampToByte(min + i);
					var block = colorBlock;
					//block.Red0 = hasExtremeValues ? c1 : c0;
					//block.Red1 = hasExtremeValues ? c0 : c1;
					block = col0Setter(block, hasExtremeValues ? c1 : c0);
					block = col1Setter(block, hasExtremeValues ? c0 : c1);
					int error = SelectIndices(ref block, pixels, indexSetter, col0Getter, col1Getter);
					if (error < bestError) {
						best = block;
						bestError = error;
						max = c0;
						min = c1;
					}
				}
				{
					byte c0 = ByteHelper.ClampToByte(max - i);
					byte c1 = ByteHelper.ClampToByte(min);
					var block = colorBlock;
					//block.Red0 = hasExtremeValues ? c1 : c0;
					//block.Red1 = hasExtremeValues ? c0 : c1;
					block = col0Setter(block, hasExtremeValues ? c1 : c0);
					block = col1Setter(block, hasExtremeValues ? c0 : c1);
					int error = SelectIndices(ref block, pixels, indexSetter, col0Getter, col1Getter);
					if (error < bestError) {
						best = block;
						bestError = error;
						max = c0;
						min = c1;
					}
				}

				if (bestError <= errorThreshold) {
					break;
				}
			}

			return best;
		}


		#endregion
	}
}
