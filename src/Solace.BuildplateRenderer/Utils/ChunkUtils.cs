// <copyright file="ChunkUtils.cs" company="BitcoderCZ">
// Copyright (c) BitcoderCZ. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using BitcoderCZ.Maths.Vectors;
using Cyotek.Data.Nbt;
using Solace.BuildplateRenderer.Models.ResourcePacks;

namespace Solace.BuildplateRenderer.Utils;

internal static class ChunkUtils
{
    public const int Width = 16;
    public const int Height = 256;
    public const int SubChunkSize = 16;

    public static readonly int[] EmptySubChunk = new int[Width * SubChunkSize * Width];

    public static readonly FrozenSet<string> InvisibleBlocks = new HashSet<string>(StringComparer.Ordinal)
    {
        "minecraft:air",
        "fountain:solid_air",
        "fountain:non_replaceable_air",
        "fountain:invisible_constraint",
        "fountain:blend_constraint",
        "fountain:border_constraint",
    }.ToFrozenSet(StringComparer.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 BlockToChunk(int2 blockPosition)
        => new(blockPosition.X >> 4, blockPosition.Y >> 4);

    public static int[] ReadBlockData(TagLongArray nbt)
    {
        if (nbt.Value.Length == 0)
        {
            return EmptySubChunk;
        }

        var resultData = GC.AllocateUninitializedArray<int>(Width * SubChunkSize * Width);

        var longArray = nbt.Value;

        var bits = 4;

        for (var b = 4; b <= 64; b++)
        {
            var vpl = 64 / b;
            var expectedLength = (4096 + vpl - 1) / vpl;

            if (expectedLength == longArray.Length)
            {
                bits = b;
                break;
            }
        }

        var valuesPerLong = 64 / bits;
        var mask = (1L << bits) - 1;

        var dataIndex = 0;

        for (var i = 0; i < longArray.Length; i++)
        {
            var value = longArray[i];

            for (var j = 0; j < valuesPerLong; j++)
            {
                if (dataIndex >= 4096)
                {
                    break;
                }

                resultData[dataIndex++] = (int)((value >> (j * bits)) & mask);
            }
        }

        return resultData;
    }

    public static BlockState? TagToBlockStateVisibleFromPool(TagCompound paletteEntry)
    {
        var blockName = ((TagString)paletteEntry["Name"]).Value;

        if (InvisibleBlocks.Contains(blockName))
        {
            return null;
        }

        if (blockName is "minecraft:water" or "minecraft:lava")
        {
            // TODO:
            return null;
        }

        var propertiesArray = ArrayPool<KeyValuePair<string, string>>.Shared.Rent(64);
        var propertiesArrayLength = 0;
        if (paletteEntry.Value.TryGetValue("Properties", out var propertiesTag))
        {
            foreach (var tag in ((TagCompound)propertiesTag).Value)
            {
                if (propertiesArrayLength >= propertiesArray.Length)
                {
                    ArrayPool<KeyValuePair<string, string>>.Shared.Return(propertiesArray);
                    propertiesArray = ArrayPool<KeyValuePair<string, string>>.Shared.Rent(propertiesArray.Length * 2);
                }

                propertiesArray[propertiesArrayLength++] = new(tag.Name, ((TagString)tag).Value);
            }
        }

        var blockState = BlockState.CreateNoCopy(blockName, propertiesArray, propertiesArrayLength);

        return blockState;
    }
}
