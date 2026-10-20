using Api.ValueObjects;

namespace Api.Events;

public class SquareClickedEventArgs(
    Position selectedTilePosition
) : EventArgs
{
    public Position SelectedTilePosition { get; } = selectedTilePosition;
}