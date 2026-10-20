using Api.Models;

namespace Api.Events;

public class GameEndedEventArgs(
    GameResult result
) : EventArgs
{
    public GameResult Result { get; } = result;
}