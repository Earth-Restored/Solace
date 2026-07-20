using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Solace.PreviewGenerator.NBT;
using Solace.PreviewGenerator.Utils;

namespace Solace.PreviewGenerator.Registry;

public static partial class JavaBlocks
{
    private static readonly Dictionary<int, string> map = [];
    private static readonly Dictionary<string, List<string>> nonVanillaStatesList = [];

    private static readonly Dictionary<int, BedrockMapping> bedrockMap = [];
    private static readonly Dictionary<string, BedrockMapping> bedrockMapByName = [];
    private static readonly Dictionary<string, BedrockMapping> bedrockNonVanillaMap = [];

    private static readonly Lock _initLock = new Lock();
    private static volatile bool _isInitialized;

    private static void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            lock (_initLock)
            {
                if (!_isInitialized)
                {
                    throw new InvalidOperationException("Data has not been initialized." + new StackFrame().ToString());
                }
            }
        }
    }

    public static void Initialize(string staticData, ILogger logger)
    {
        if (!_isInitialized)
        {
            lock (_initLock)
            {
                if (!_isInitialized)
                {
                    InitializeInternal(staticData, logger);
                    _isInitialized = true;
                }
            }
        }
    }

    private static void InitializeInternal(string staticData, ILogger logger)
    {
        DataFile.Load(Path.Combine(staticData, "registry", "blocks_java.json"), logger, jToken =>
        {
            var jArray = (JsonArray)jToken;

            foreach (var _element in jArray)
            {
                var element = _element as JsonObject;
                Debug.Assert(element is not null);

                var id = element["id"]!.GetValue<int>();
                var name = element["name"]!.GetValue<string>()!;
                if (!map.TryAdd(id, name))
                {
                    LogDuplicateJavaBlockId(logger, id);
                }

                try
                {
                    BedrockMapping? bedrockMapping = ReadBedrockMapping((JsonObject)element["bedrock"]!, jArray);
                    if (bedrockMapping is null)
                    {
                        LogIgnoringJavaBlock(logger, name);
                        continue;
                    }

                    bedrockMap[id] = bedrockMapping;
                    bedrockMapByName[name] = bedrockMapping;
                }
                catch (BedrockMappingFailException exception)
                {
                    LogCannotFindBedrockBlockForJavaBlock(logger, exception, name);
                }
            }
        });

        DataFile.Load(Path.Combine(staticData, "registry", "blocks_java_nonvanilla.json"), logger, jToken =>
        {
            var jArray = (JsonArray)jToken;

            foreach (var _element in jArray)
            {
                var element = _element as JsonObject;
                Debug.Assert(element is not null);

                var baseName = element["name"]!.GetValue<string>()!;

                var statesArray = (JsonArray)element["states"]!;
                var stateNames = new List<string>(statesArray.Count);
                foreach (var _stateElement in statesArray)
                {
                    var stateElement = _stateElement as JsonObject;
                    Debug.Assert(stateElement is not null);

                    var stateName = stateElement["name"]!.GetValue<string>()!;
                    stateNames.Add(stateName);

                    var name = baseName + stateName;

                    try
                    {
                        BedrockMapping? bedrockMapping = ReadBedrockMapping((JsonObject)stateElement["bedrock"]!, null);
                        if (bedrockMapping is null)
                        {
                            LogIgnoringJavaBlock(logger, name);
                            continue;
                        }

                        bedrockNonVanillaMap[name] = bedrockMapping;
                    }
                    catch (BedrockMappingFailException exception)
                    {
                        LogCannotFindBedrockBlockForJavaBlock(logger, exception, name);
                    }
                }

                if (!nonVanillaStatesList.TryAdd(baseName, stateNames))
                {
                    LogDuplicateJavaNonVanillaBlockName(logger, baseName);
                }
            }
        });
    }

    private static BedrockMapping? ReadBedrockMapping(JsonObject bedrockMappingObject, JsonArray? javaBlocksArray)
    {
        if (bedrockMappingObject.TryGetPropertyValue("ignore", out var ignoreToken) && ignoreToken!.GetValue<bool>())
        {
            return null;
        }

        var name = bedrockMappingObject["name"]!.GetValue<string>()!;

        SortedDictionary<string, object> state = [];
        if (bedrockMappingObject.TryGetPropertyValue("state", out var stateToken))
        {
            var stateObject = stateToken as JsonObject;
            Debug.Assert(stateObject is not null);

            foreach (var entry in stateObject)
            {
                var stateElement = entry.Value as JsonValue;
                Debug.Assert(stateElement is not null);
                var stateElementType = stateElement.GetValueKind();
                if (stateElementType == JsonValueKind.String)
                {
                    state[entry.Key] = stateElement.GetValue<string>()!;
                }
                else if (stateElementType == JsonValueKind.True)
                {
                    state[entry.Key] = 1;
                }
                else if (stateElementType == JsonValueKind.False)
                {
                    state[entry.Key] = 0;
                }
                else
                {
                    state[entry.Key] = stateElement.GetValue<int>();
                }
            }
        }

        var id = BedrockBlocks.GetId(name, state);
        if (id == -1)
        {
            throw new BedrockMappingFailException("Cannot find Bedrock block with provided name and state");
        }

        var waterlogged = bedrockMappingObject.TryGetPropertyValue("waterlogged", out var waterloggedToken) && waterloggedToken!.GetValue<bool>();

        BedrockMapping.BlockEntityR? blockEntity = null;
        if (bedrockMappingObject.TryGetPropertyValue("block_entity", out var blockEntityToken))
        {
            var blockEntityObject = blockEntityToken as JsonObject;
            Debug.Assert(blockEntityObject is not null);

            var type = blockEntityObject["type"]!.GetValue<string>()!;
            switch (type)
            {
                case "bed":
                    {
                        var color = blockEntityObject["color"]!.GetValue<string>()!;
                        blockEntity = new BedrockMapping.BedBlockEntity(type, color);
                    }

                    break;
                case "flower_pot":
                    {
                        NbtMap? contents = null;
                        if (blockEntityObject.TryGetPropertyValue("contents", out var contentsToken) && contentsToken!.GetValueKind() is not JsonValueKind.Null)
                        {
                            var contentsName = contentsToken.GetValue<string>()!;
                            if (javaBlocksArray is not null)
                            {
                                var element = javaBlocksArray
                                    .Where(element => ((JsonObject)element!)["name"]!.GetValue<string>() == contentsName)
                                    .Select(element => (JsonObject)((JsonObject)element!)["bedrock"]!)
                                    .First(element => !element.ContainsKey("ignore") || !element["ignore"]!.GetValue<bool>());

                                NbtMapBuilder builder = NbtMap.Builder();
                                builder.PutString("name", element["name"]!.GetValue<string>()!);
                                if (element.TryGetPropertyValue("state", out var stateToken2))
                                {
                                    Debug.Assert(stateToken2 is not null);

                                    NbtMapBuilder stateBuilder = NbtMap.Builder();
                                    foreach (var (key, stateElement) in (JsonObject)stateToken2)
                                    {
                                        Debug.Assert(stateElement is not null);

                                        var stateElementType = stateElement.GetValueKind();
                                        if (stateElementType == JsonValueKind.String)
                                        {
                                            stateBuilder.PutString(key, stateElement.GetValue<string>()!);
                                        }
                                        else if (stateElementType == JsonValueKind.True)
                                        {
                                            stateBuilder.PutInt(key, 1);
                                        }
                                        else if (stateElementType == JsonValueKind.False)
                                        {
                                            stateBuilder.PutInt(key, 0);
                                        }
                                        else
                                        {
                                            stateBuilder.PutInt(key, stateElement.GetValue<int>());
                                        }
                                    }

                                    builder.PutCompound("states", stateBuilder.Build());
                                }

                                contents = builder.Build();
                            }

                            if (contents is null)
                            {
                                throw new BedrockMappingFailException("Could not find contents for flower pot");
                            }
                        }

                        blockEntity = new BedrockMapping.FlowerPotBlockEntity(type, contents);
                    }

                    break;
                case "moving_block":
                    {
                        blockEntity = new BedrockMapping.BlockEntityR(type);
                    }

                    break;
                case "piston":
                    {
                        var sticky = blockEntityObject["sticky"]!.GetValue<bool>();
                        var extended = blockEntityObject["extended"]!.GetValue<bool>();
                        blockEntity = new BedrockMapping.PistonBlockEntity(type, sticky, extended);
                    }

                    break;
            }
        }

        BedrockMapping.ExtraDataR? extraData = null;
        if (bedrockMappingObject.TryGetPropertyValue("extra_data", out var extra_dataToken))
        {
            var extraDataObject = extra_dataToken as JsonObject;
            Debug.Assert(extraDataObject is not null);

            var type = extraDataObject["type"]!.GetValue<string>();
            switch (type)
            {
                case "note_block":
                    {
                        var pitch = extraDataObject["pitch"]!.GetValue<int>();
                        extraData = new BedrockMapping.NoteBlockExtraData(pitch);
                    }

                    break;
            }
        }

        return new BedrockMapping(id, waterlogged, blockEntity, extraData);
    }

    public sealed class BedrockMappingFailException : Exception
    {
        public BedrockMappingFailException()
            : base()
        {
        }

        public BedrockMappingFailException(string? message)
            : base(message)
        {
        }

        public BedrockMappingFailException(string? message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public static int GetMaxVanillaBlockId()
    {
        EnsureInitialized();

        if (map.Count == 0)
        {
            return -1;
        }
        else
        {
            return map.Keys.Max();
        }
    }

    public static IReadOnlyList<string>? GetStatesForNonVanillaBlock(string name)
    {
        EnsureInitialized();

        var states = nonVanillaStatesList.GetValueOrDefault(name);
        return states;
    }

    // [Obsolete]
    public static string? GetName(int id)
        => GetName(id, null);

    // [Obsolete]
    public static BedrockMapping? GetBedrockMapping(int javaId)
        => GetBedrockMapping(javaId, null);

    // TODO?: FabricRegistryManager
    public static string? GetName(int id, /*FabricRegistryManager?*/object? fabricRegistryManager)
    {
        EnsureInitialized();

        var name = map.GetValueOrDefault(id);
        if (name is null && fabricRegistryManager is not null)
        {
            name = null;//fabricRegistryManager.getBlockName(id);
        }

        return name;
    }

    // TODO?: FabricRegistryManager
    public static BedrockMapping? GetBedrockMapping(int javaId, /*FabricRegistryManager?*/object? fabricRegistryManager)
    {
        EnsureInitialized();

        BedrockMapping? bedrockMapping = bedrockMap.GetValueOrDefault(javaId);
        if (bedrockMapping is null && fabricRegistryManager is not null)
        {
            string? fabricName = null;//fabricRegistryManager.getBlockName(javaId);
            if (fabricName is not null)
            {
                bedrockMapping = bedrockNonVanillaMap.GetValueOrDefault(fabricName);
            }
        }

        return bedrockMapping;
    }

    public static BedrockMapping? GetBedrockMapping(string javaName)
    {
        EnsureInitialized();

        var bedrockMapping = bedrockMapByName.GetValueOrDefault(javaName) ?? bedrockNonVanillaMap.GetValueOrDefault(javaName);
        return bedrockMapping;
    }

    public sealed class BedrockMapping
    {
        public readonly int Id;
        public readonly bool Waterlogged;
        public readonly BlockEntityR? BlockEntity;
        public readonly ExtraDataR? ExtraData;

        public BedrockMapping(int id, bool waterlogged, BlockEntityR? blockEntity, ExtraDataR? extraData)
        {
            Id = id;
            Waterlogged = waterlogged;
            BlockEntity = blockEntity;
            ExtraData = extraData;
        }

        public class BlockEntityR
        {
            public readonly string Type;

            public BlockEntityR(string type)
            {
                Type = type;
            }
        }

        public class BedBlockEntity : BlockEntityR
        {
            public readonly string Color;

            public BedBlockEntity(string type, string color)
                : base(type)
            {
                Color = color;
            }
        }

        public class FlowerPotBlockEntity : BlockEntityR
        {
            public readonly NbtMap? Contents;

            public FlowerPotBlockEntity(string type, NbtMap? contents)
                : base(type)
            {
                Contents = contents;
            }
        }

        public class PistonBlockEntity : BlockEntityR
        {
            public readonly bool Sticky;
            public readonly bool Extended;

            public PistonBlockEntity(string type, bool sticky, bool extended)
                : base(type)
            {
                Sticky = sticky;
                Extended = extended;
            }
        }

        public abstract class ExtraDataR
        {
            protected ExtraDataR()
            {
                // empty
            }
        }

        public class NoteBlockExtraData : ExtraDataR
        {
            public readonly int Pitch;

            public NoteBlockExtraData(int pitch)
                : base()
            {
                Pitch = pitch;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Duplicate Java block ID '{Id}'")]
    private static partial void LogDuplicateJavaBlockId(ILogger logger, int Id);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignoring Java block '{Name}'")]
    private static partial void LogIgnoringJavaBlock(ILogger logger, string Name);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cannot find Bedrock block for Java block '{Name}'")]
    private static partial void LogCannotFindBedrockBlockForJavaBlock(ILogger logger, Exception exception, string Name);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Duplicate Java non-vanilla block name '{BaseName}'")]
    private static partial void LogDuplicateJavaNonVanillaBlockName(ILogger logger, string BaseName);
}
