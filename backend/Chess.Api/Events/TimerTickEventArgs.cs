using Api.Enums;

namespace Api.Events;

public class TimerTickEventArgs(
    PieceColor color,
    TimeSpan timeRemaining
) : EventArgs
{
    public PieceColor Color { get; } = color;
    public TimeSpan TimeRemaining { get; } = timeRemaining;
}