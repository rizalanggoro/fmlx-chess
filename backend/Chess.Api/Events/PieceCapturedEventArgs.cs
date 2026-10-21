using Api.Interfaces.Models;
using Api.Models;
using Api.ValueObjects;

namespace Api.Events;

public class PieceCapturedEventArgs(
    IChessPiece capturedPiece,
    Position tilePosition
) : EventArgs
{
    public IChessPiece CapturedPiece { get; } = capturedPiece;
    public Position TilePosition { get; } = tilePosition;
}