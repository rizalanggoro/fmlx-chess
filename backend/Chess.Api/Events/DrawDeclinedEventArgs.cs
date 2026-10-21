using Api.Interfaces.Models;

namespace Api.Events;

public class DrawDeclinedEventArgs(
    IPlayer offerer
) : EventArgs
{
    public IPlayer Offerer { get; } = offerer;
}
