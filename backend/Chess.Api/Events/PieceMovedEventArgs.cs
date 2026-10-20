using Api.ValueObjects;

namespace Api.Events;

public class PieceMovedEventArgs(
    Move move
) : EventArgs
{
    public Move Move { get; } = move;
}