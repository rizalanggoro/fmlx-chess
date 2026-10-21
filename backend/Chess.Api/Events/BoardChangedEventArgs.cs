using Api.Interfaces.Models;

namespace Api.Events;

public class BoardChangedEventArgs(
    IBoard board
) : EventArgs
{
    public IBoard Board { get; } = board;
}
