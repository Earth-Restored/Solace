using Cyotek.Data.Nbt;

namespace Solace.PreviewGenerator.BlockEntity;

public sealed class BlockEntityInfo
{
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public BlockEntityType Type { get; }
    public TagCompound? Nbt { get; }

    public BlockEntityInfo(int x, int y, int z, BlockEntityType type, TagCompound? nbt)
    {
        X = x;
        Y = y;
        Z = z;
        Type = type;
        Nbt = nbt;
    }
}
