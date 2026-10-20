using Api.Models;

namespace Api.Events;

public class PieceMovedEventArgs(
    Move move
) : EventArgs
{
    public Move Move { get; } = move;
}