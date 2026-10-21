using Api.Interfaces.Models;
using Api.ValueObjects;

namespace Api.Events;

public class RoomSnapshotChangedEventArgs(
    IBoard board,
    IReadOnlyList<MoveHistoryEntry> history
) : EventArgs
{
    public IBoard Board { get; } = board;
    public IReadOnlyList<MoveHistoryEntry> History { get; } = history;
}