using Api.ValueObjects;

namespace Api.Constants;

/// <summary>
///     Offsets used by the engine when generating moves or checking attacks,
///     shared so the same set is never written twice.
/// </summary>
public static class SquareOffsets
{
    public static readonly SquareOffset[] Knight =
    [
        new(1, 2), new(1, -2), new(-1, 2), new(-1, -2),
        new(2, 1), new(2, -1), new(-2, 1), new(-2, -1)
    ];

    public static readonly SquareOffset[] Diagonal =
    [
        new(1, 1), new(1, -1),
        new(-1, 1), new(-1, -1)
    ];

    public static readonly SquareOffset[] Straight =
    [
        new(1, 0), new(-1, 0),
        new(0, 1), new(0, -1)
    ];

    /// <summary>The eight neighbours: diagonal plus straight offsets.</summary>
    public static readonly SquareOffset[] King = [..Diagonal, ..Straight];

    /// <summary>A queen slides along the king's eight directions.</summary>
    public static readonly SquareOffset[] Queen = King;

    public static readonly int[] PawnCaptureColumns = [-1, 1];
}