using Api.Enums;

namespace Api.Events;

public class CheckEventArgs(
    PieceColor checkedColor
) : EventArgs
{
    public PieceColor CheckedColor { get; } = checkedColor;
}