using Api.Enums;

namespace Api.Models;

public class PieceMovedEventArgs : EventArgs
{
    public Move Move { get; }

    public PieceMovedEventArgs(Move move)
    {
        Move = move;
    }
}

public class SquareClickedEventArgs : EventArgs
{
    public Position SelectedTilePosition { get; }

    public SquareClickedEventArgs(Position selectedTilePosition)
    {
        SelectedTilePosition = selectedTilePosition;
    }
}

public class PieceCapturedEventArgs : EventArgs
{
    public IChessPiece CapturedPiece { get; }
    public Position TilePosition { get; }

    public PieceCapturedEventArgs(
        IChessPiece capturedPiece,
        Position tilePosition)
    {
        CapturedPiece = capturedPiece;
        TilePosition = tilePosition;
    }
}

public class TurnChangedEventArgs : EventArgs
{
    public IPlayer NewCurrentPlayer { get; }

    public TurnChangedEventArgs(IPlayer newCurrentPlayer)
    {
        NewCurrentPlayer = newCurrentPlayer;
    }
}

public class CheckEventArgs : EventArgs
{
    public PieceColor CheckedColor { get; }

    public CheckEventArgs(PieceColor checkedColor)
    {
        CheckedColor = checkedColor;
    }
}

public class DrawOfferedEventArgs : EventArgs
{
    public IPlayer Offerer { get; }

    public DrawOfferedEventArgs(IPlayer offerer)
    {
        Offerer = offerer;
    }
}

public class TimerTickEventArgs : EventArgs
{
    public PieceColor Color { get; }
    public TimeSpan TimeRemaining { get; }

    public TimerTickEventArgs(
        PieceColor color,
        TimeSpan timeRemaining)
    {
        Color = color;
        TimeRemaining = timeRemaining;
    }
}

public class GameEndedEventArgs : EventArgs
{
    public GameResult Result { get; }

    public GameEndedEventArgs(GameResult result)
    {
        Result = result;
    }
}
