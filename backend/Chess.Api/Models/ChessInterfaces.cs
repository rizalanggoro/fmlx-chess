namespace Api;

public interface IChessPiece
{
    PieceColor Color { get; }
    PieceType Type { get; }
}

public interface IBoard
{
    IChessTile[,] Tiles { get; }
}

public interface IChessTile
{
    IChessPiece? Piece { get; set; }
    Position TilePosition { get; }
}

public interface IPlayer
{
    string Name { get; }
    PieceColor Color { get; }
}

public interface IPlayerTimer
{
    TimeSpan RemainingTime { get; }
    event EventHandler<TimerTickEventArgs> TimerTick;
    event EventHandler TimeExpired;
    void StartTimer();
    void StopTimer();
}
