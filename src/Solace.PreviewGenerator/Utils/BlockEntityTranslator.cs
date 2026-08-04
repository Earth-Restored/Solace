using Microsoft.Extensions.Logging;
using Solace.PreviewGenerator.BlockEntity;
using Solace.PreviewGenerator.NBT;
using Solace.PreviewGenerator.Registry;

namespace Solace.PreviewGenerator.Utils;

public static partial class BlockEntityTranslator
{
    public static NbtMap? TranslateBlockEntity(JavaBlocks.BedrockMapping.BlockEntityR blockEntityMapping, BlockEntityInfo? javaBlockEntityInfo, ILogger logger)
    {
        switch (blockEntityMapping.Type)
        {
            case "bed":
                {
                    var builder = NbtMap.Builder();
                    builder.PutString("id", "Bed");
                    builder.PutByte("color", ((JavaBlocks.BedrockMapping.BedBlockEntity)blockEntityMapping).Color switch
                    {
                        "white" => 0,
                        "orange" => 1,
                        "magenta" => 2,
                        "light_blue" => 3,
                        "yellow" => 4,
                        "lime" => 5,
                        "pink" => 6,
                        "gray" => 7,
                        "light_gray" => 8,
                        "cyan" => 9,
                        "purple" => 10,
                        "blue" => 11,
                        "brown" => 12,
                        "green" => 13,
                        "red" => 14,
                        "black" => 15,
                        _ => 0
                    });
                    return builder.Build();
                }
            case "flower_pot":
                {
                    var builder = NbtMap.Builder();
                    builder.PutString("id", "FlowerPot");
                    var contents = ((JavaBlocks.BedrockMapping.FlowerPotBlockEntity)blockEntityMapping).Contents;
                    if (contents is not null)
                    {
                        builder.PutCompound("PlantBlock", contents);
                    }

                    return builder.Build();
                }
            case "moving_block":
                {
                    var builder = NbtMap.Builder();

                    builder.PutString("id", "MovingBlock");

                    if (javaBlockEntityInfo is null)
                    {
                        LogNotSendingMovingBlockEntityDataUntilServerProvidesData(logger);
                        return null;
                    }

                    var javaNbt = javaBlockEntityInfo.Nbt!;

                    if (!javaNbt.Contains("blockStateId"))
                    {
                        LogMovingBlockEntityDataDidNotContainNumericBlockStateId(logger);
                        return null;
                    }

                    var javaBlockId = javaNbt.GetIntValue("blockStateId");
                    var bedrockMapping = JavaBlocks.GetBedrockMapping(javaBlockId);
                    if (bedrockMapping is null)
                    {
                        LogMovingBlockEntityContainedBlockWithNoMapping(logger, JavaBlocks.GetName(javaBlockId));
                        return null;
                    }

                    var movingBlockBuilder = NbtMap.Builder();
                    movingBlockBuilder.PutString("name", BedrockBlocks.GetName(bedrockMapping.Id)!);
                    movingBlockBuilder.PutCompound("states", BedrockBlocks.GetStateNbt(bedrockMapping.Id)!);
                    builder.PutCompound("movingBlock", movingBlockBuilder.Build());

                    if (bedrockMapping.Waterlogged)
                    {
                        var movingBlockExtraBuilder = NbtMap.Builder();
                        movingBlockExtraBuilder.PutString("name", BedrockBlocks.GetName(BedrockBlocks.WaterId)!);
                        movingBlockExtraBuilder.PutCompound("states", BedrockBlocks.GetStateNbt(BedrockBlocks.WaterId)!);
                        builder.PutCompound("movingBlockExtra", movingBlockExtraBuilder.Build());
                    }

                    if (bedrockMapping.BlockEntity is not null)
                    {
                        var blockEntityNbt = BlockEntityTranslator.TranslateBlockEntity(bedrockMapping.BlockEntity, null, logger);
                        if (blockEntityNbt is not null)
                        {
                            builder.PutCompound("movingEntity", blockEntityNbt.ToBuilder().PutInt("x", javaBlockEntityInfo.X).PutInt("y", javaBlockEntityInfo.Y).PutInt("z", javaBlockEntityInfo.Z).PutBoolean("isMovable", false).Build());
                        }
                    }

                    if (!javaNbt.Contains("basePos"))
                    {
                        LogMovingBlockEntityDataDidNotContainPistonBasePosition(logger);
                        return null;
                    }

                    var basePosTag = javaNbt.GetCompound("basePos");
                    builder.PutInt("pistonPosX", basePosTag.GetIntValue("x"));
                    builder.PutInt("pistonPosY", basePosTag.GetIntValue("y"));
                    builder.PutInt("pistonPosZ", basePosTag.GetIntValue("z"));

                    return builder.Build();
                }
            case "piston":
                {
                    var pistonBlockEntity = (JavaBlocks.BedrockMapping.PistonBlockEntity)blockEntityMapping;
                    var builder = NbtMap.Builder();
                    builder.PutString("id", "PistonArm");
                    builder.PutByte("State", (byte)(pistonBlockEntity.Extended ? 2 : 0));
                    builder.PutByte("NewState", (byte)(pistonBlockEntity.Extended ? 2 : 0));
                    builder.PutFloat("Progress", pistonBlockEntity.Extended ? 1.0f : 0.0f);
                    builder.PutFloat("LastProgress", pistonBlockEntity.Extended ? 1.0f : 0.0f);
                    builder.PutBoolean("Sticky", pistonBlockEntity.Sticky);
                    return builder.Build();
                }
            default:
                throw new InvalidOperationException();
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Not sending moving block entity data until server provides data")]
    private static partial void LogNotSendingMovingBlockEntityDataUntilServerProvidesData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moving block entity data did not contain numeric block state ID")]
    private static partial void LogMovingBlockEntityDataDidNotContainNumericBlockStateId(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moving block entity contained block with no mapping '{JavaName}'")]
    private static partial void LogMovingBlockEntityContainedBlockWithNoMapping(ILogger logger, string? JavaName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moving block entity data did not contain piston base position")]
    private static partial void LogMovingBlockEntityDataDidNotContainPistonBasePosition(ILogger logger);
}
