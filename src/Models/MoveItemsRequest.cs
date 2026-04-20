using System.Collections.Generic;

namespace PixConvert.Models;

public sealed record MoveItemsRequest(
    IReadOnlyList<FileItem> ItemsToMove,
    int TargetIndex,
    bool IsBottom);
