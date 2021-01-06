﻿using System;
using BCnEncoder.Shared;

namespace BCnEncoder.Encoder.Bc7
{
	internal static class Bc7Mode1Encoder
	{

		public static Bc7Block EncodeBlock(RawBlock4X4Rgba32 block, int startingVariation, int bestPartition)
		{
			Bc7Block output = new Bc7Block();
			const Bc7BlockType type = Bc7BlockType.Type1;


			ColorRgba32[] endpoints = new ColorRgba32[4];
			byte[] pBits = new byte[2];
			int[] partitionTable = Bc7Block.Subsets2PartitionTable[bestPartition];

			byte[] indices = new byte[16];

			int[] anchorIndices = new int[] {
				0,
				Bc7Block.Subsets2AnchorIndices[bestPartition]
			};

			for (int subset = 0; subset < 2; subset++) {
				
				Bc7EncodingHelpers.GetInitialUnscaledEndpointsForSubset(block, out var ep0, out var ep1,
					partitionTable, subset);
				ColorRgba32 scaledEp0 =
					Bc7EncodingHelpers.ScaleDownEndpoint(ep0, type, true, out byte pBit);
				ColorRgba32 scaledEp1 =
					Bc7EncodingHelpers.ScaleDownEndpoint(ep1, type, true, out pBit);

				Bc7EncodingHelpers.OptimizeSubsetEndpointsWithPBit(type, block, ref scaledEp0,
					ref scaledEp1, ref pBit, ref pBit, startingVariation, partitionTable, subset, true, false);

				ep0 = Bc7EncodingHelpers.ExpandEndpoint(type, scaledEp0, pBit);
				ep1 = Bc7EncodingHelpers.ExpandEndpoint(type, scaledEp1, pBit);
				Bc7EncodingHelpers.FillSubsetIndices(type, block,
					ep0,
					ep1,
					partitionTable, subset, indices);

				if ((indices[anchorIndices[subset]] & 0b100) > 0) //If anchor index most significant bit is 1, switch endpoints
				{
					var c = scaledEp0;

					scaledEp0 = scaledEp1;
					scaledEp1 = c;

					//redo indices
					ep0 = Bc7EncodingHelpers.ExpandEndpoint(type, scaledEp0, pBit);
					ep1 = Bc7EncodingHelpers.ExpandEndpoint(type, scaledEp1, pBit);
					Bc7EncodingHelpers.FillSubsetIndices(type, block,
						ep0,
						ep1,
						partitionTable, subset, indices);
				}

				endpoints[subset * 2] = scaledEp0;
				endpoints[subset * 2 + 1] = scaledEp1;
				pBits[subset] = pBit;
			}

			output.PackType1(bestPartition, new[]{
					new byte[]{endpoints[0].r, endpoints[0].g, endpoints[0].b},
					new byte[]{endpoints[1].r, endpoints[1].g, endpoints[1].b},
					new byte[]{endpoints[2].r, endpoints[2].g, endpoints[2].b},
					new byte[]{endpoints[3].r, endpoints[3].g, endpoints[3].b}
				},
				pBits,
				indices);

			return output;
		}
	}
}
