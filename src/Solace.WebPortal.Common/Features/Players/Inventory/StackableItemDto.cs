using System.Runtime.InteropServices;

namespace Solace.WebPortal.Common.Features.Players.Inventory;

[StructLayout(LayoutKind.Auto)]
public readonly record struct StackableItemDto(
    Guid ItemId,
    int Count
);
