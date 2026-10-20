using Api.Models;

namespace Api.Events;

public class TurnChangedEventArgs(
    IPlayer newCurrentPlayer
) : EventArgs
{
    public IPlayer NewCurrentPlayer { get; } = newCurrentPlayer;
}