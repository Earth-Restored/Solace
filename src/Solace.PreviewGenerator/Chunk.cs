using System.Globalization;
using Cyotek.Data.Nbt;
using Microsoft.Extensions.Logging;
using Solace.PreviewGenerator.BlockEntity;
using Solace.PreviewGenerator.NBT;
using Solace.PreviewGenerator.Registry;
using Solace.PreviewGenerator.Utils;

namespace Solace.PreviewGenerator;

internal sealed partial class Chunk
{
    public static Chunk? Read(TagCompound chunkTag, ILogger logger)
    {
        try
        {
            return new Chunk(chunkTag, logger);
        }
        catch (Exception exception)
        {
            LogFailedToReadCunk(logger, exception);
            return null;
        }
    }

    public readonly int ChunkX;
    public readonly int ChunkZ;

    public readonly int[] Blocks = new int[16 * 256 * 16];
    public readonly NbtMap?[] BlockEntities = new NbtMap[16 * 256 * 16];

    private Chunk(TagCompound chunkTag, ILogger logger)
    {
        ChunkX = chunkTag.GetIntValue("xPos");
        ChunkZ = chunkTag.GetIntValue("zPos");

        var blockEntityMappings = new JavaBlocks.BedrockMapping.BlockEntityR?[16 * 256 * 16];
        var extraDatas = new JavaBlocks.BedrockMapping.ExtraDataR?[16 * 256 * 16];

        Array.Fill(Blocks, BedrockBlocks.AirId);
        Array.Fill(BlockEntities, null);
        Array.Fill(blockEntityMappings, null);
        Array.Fill(extraDatas, null);

        HashSet<string> alreadyNotifiedMissingBlocks = [];
        for (var subchunkY = 0; subchunkY < 16; subchunkY++)
        {
            var sectionIndex = subchunkY + 4 + 1; // Java world height starts at -64, plus one section for bottommost lighting
            var sectionTag = (TagCompound)chunkTag.GetList("sections").Value[sectionIndex];

            var blockStatesTag = sectionTag.GetCompound("block_states");

            var paletteTag = blockStatesTag.GetList("palette");
            List<string> javaPalette = new(paletteTag.Count);
            foreach (var paletteEntryTag in paletteTag.Value)
            {
                javaPalette.Add(ReadPaletteEntry((TagCompound)paletteEntryTag));
            }

            int[] javaBlocks;
            if (javaPalette.Count == 0)
            {
                throw new IOException("Chunk section has empty palette");
            }

            if (!blockStatesTag.Contains("data"))
            {
                if (javaPalette.Count > 1)
                {
                    throw new IOException("Chunk section has palette with more than one entry and no data");
                }

                javaBlocks = new int[4096];
                Array.Fill(javaBlocks, 0);
            }
            else
            {
                javaBlocks = ReadBitArray(blockStatesTag.GetLongArray("data"), javaPalette.Count);
            }

            for (var x = 0; x < 16; x++)
            {
                for (var y = 0; y < 16; y++)
                {
                    for (var z = 0; z < 16; z++)
                    {
                        var javaName = javaPalette[javaBlocks[(y * 16 + z) * 16 + x]];

                        JavaBlocks.BedrockMapping? bedrockMapping = JavaBlocks.GetBedrockMapping(javaName);
                        if (bedrockMapping is null)
                        {
                            if (alreadyNotifiedMissingBlocks.Add(javaName))
                            {
                                LogChunkContainedBlockWithNoMapping(logger, javaName);
                            }
                        }

                        // TODO: how to handle waterlogged blocks???
                        var bedrockId = bedrockMapping is not null ? bedrockMapping.Id : BedrockBlocks.AirId;
                        Blocks[(x * 256 + y + subchunkY * 16) * 16 + z] = bedrockId;

                        var blockEntityMapping = bedrockMapping is not null && bedrockMapping.BlockEntity is not null ? bedrockMapping.BlockEntity : null;
                        var bedrockBlockEntityData = blockEntityMapping is not null ? BlockEntityTranslator.TranslateBlockEntity(blockEntityMapping, null, logger) : null;
                        if (bedrockBlockEntityData is not null)
                        {
                            bedrockBlockEntityData = bedrockBlockEntityData.ToBuilder().PutInt("x", x + ChunkX * 16).PutInt("y", y + subchunkY * 16).PutInt("z", z + ChunkZ * 16).PutBoolean("isMovable", false).Build();
                        }

                        BlockEntities[(x * 256 + y + subchunkY * 16) * 16 + z] = bedrockBlockEntityData;
                        blockEntityMappings[(x * 256 + y + subchunkY * 16) * 16 + z] = blockEntityMapping;

                        extraDatas[(x * 256 + y + subchunkY * 16) * 16 + z] = bedrockMapping?.ExtraData;
                    }
                }
            }
        }

        foreach (var blockEntityTag in chunkTag.GetList("block_entities").Value)
        {
            var blockEntityCompoundTag = (TagCompound)blockEntityTag;
            var x = GetChunkBlockOffset(blockEntityCompoundTag.GetIntValue("x"));
            var y = blockEntityCompoundTag.GetIntValue("y");
            var z = GetChunkBlockOffset(blockEntityCompoundTag.GetIntValue("z"));
            var type = blockEntityCompoundTag.GetStringValue("id");
            var blockEntityInfo = new BlockEntityInfo(x, y, z, BlockEntityType.FURNACE, blockEntityCompoundTag); // TODO: use proper type (currently this doesn't matter for any of our translator implementations)

            var blockEntityMapping = blockEntityMappings[(x * 256 + y) * 16 + z];
            if (blockEntityMapping is null)
            {
                LogIgnoringBlockEntityOfType(logger, type);
            }

            var bedrockBlockEntityData = blockEntityMapping is not null ? BlockEntityTranslator.TranslateBlockEntity(blockEntityMapping, blockEntityInfo, logger) : null;
            if (bedrockBlockEntityData is not null)
            {
                bedrockBlockEntityData = bedrockBlockEntityData.ToBuilder().PutInt("x", x + ChunkX * 16).PutInt("y", y).PutInt("z", z + ChunkZ * 16).PutBoolean("isMovable", false).Build();
            }

            BlockEntities[(x * 256 + y) * 16 + z] = bedrockBlockEntityData;
        }
    }

    // TODO: this relies on the state tags in the block names in the Java blocks registry matching the actual server names/values and to be sorted in alphabetical order, should verify/ensure that this is the case
    private static string ReadPaletteEntry(TagCompound paletteEntryTag)
    {
        var name = paletteEntryTag.GetStringValue("Name");

        List<string> properties = [];
        if (paletteEntryTag.Contains("Properties"))
        {
            foreach (var propertyTag in paletteEntryTag.GetCompound("Properties").Value)
            {
                properties.Add(propertyTag.Name + "=" + TagValueToString(propertyTag));
            }
        }

        properties.Sort(string.Compare);

        if (properties.Count > 0)
        {
            name = name + "[" + string.Join(",", properties.ToArray()) + "]";
        }

        return name;
    }

    private static int[] ReadBitArray(TagLongArray longArrayTag, int maxValue)
    {
        var @out = new int[4096];
        var outIndex = 0;

        long[] @in = longArrayTag.Value;
        var inIndex = 0;
        int inSubIndex;

        var bits = 64;
        for (var bits1 = 4; bits1 <= 64; bits1++)
        {
            if (maxValue <= (1 << bits1))
            {
                bits = bits1;
                break;
            }
        }

        var valuesPerLong = 64 / bits;

        var currentIn = @in[inIndex++];
        inSubIndex = 0;
        while (outIndex < @out.Length)
        {
            if (inSubIndex >= valuesPerLong)
            {
                currentIn = @in[inIndex++];
                inSubIndex = 0;
            }

            var value = (currentIn >> (inSubIndex++ * bits)) & ((1 << bits) - 1);
            @out[outIndex++] = (int)value;
        }

        return @out;
    }

    private static int GetChunkBlockOffset(int pos)
        => pos >= 0 ? pos % 16 : 15 - ((-pos - 1) % 16);

    private static string TagValueToString(Tag tag)
        => tag switch
        {
            TagByte @byte => @byte.Value.ToString(CultureInfo.InvariantCulture),
            TagDouble @double => @double.Value.ToString(CultureInfo.InvariantCulture),
            TagFloat @float => @float.Value.ToString(CultureInfo.InvariantCulture),
            TagInt @int => @int.Value.ToString(CultureInfo.InvariantCulture),
            TagLong @long => @long.Value.ToString(CultureInfo.InvariantCulture),
            TagShort @short => @short.Value.ToString(CultureInfo.InvariantCulture),
            TagString @string => @string.Value,
            _ => throw new ArgumentException($"Unsuported tag type '{tag.GetType()}'", nameof(tag)),
        };

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read chunk")]
    private static partial void LogFailedToReadCunk(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chunk contained block with no mapping '{JavaName}'")]
    private static partial void LogChunkContainedBlockWithNoMapping(ILogger logger, string JavaName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignoring block entity of type '{Type}'")]
    private static partial void LogIgnoringBlockEntityOfType(ILogger logger, string Type);
}
