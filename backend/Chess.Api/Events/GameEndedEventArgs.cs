using Api.ValueObjects;

namespace Api.Events;

public class GameEndedEventArgs(
    GameResult result
) : EventArgs
{
    public GameResult Result { get; } = result;
}