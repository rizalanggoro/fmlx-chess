using Api.Models;

namespace Api.Events;

public class DrawOfferedEventArgs(
    IPlayer offerer
) : EventArgs
{
    public IPlayer Offerer { get; } = offerer;
}