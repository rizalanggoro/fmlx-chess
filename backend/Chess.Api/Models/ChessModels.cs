namespace Api.Models;

public class ChessPiece : IChessPiece
{
    public PieceColor Color { get; }
    public PieceType Type { get; }

    public ChessPiece(PieceColor color, PieceType type)
    {
        Color = color;
        Type = type;
    }
}

public class ChessTile : IChessTile
{
    public IChessPiece? Piece { get; set; }
    public Position TilePosition { get; }

    public ChessTile(Position tilePosition)
    {
        TilePosition = tilePosition;
    }
}

public class Board : IBoard
{
    public IChessTile[,] Tiles { get; }

    public Board()
    {
        Tiles = new IChessTile[8, 8];
        for (var c = 0; c < 8; c++)
            for (var r = 0; r < 8; r++)
                Tiles[c, r] = new ChessTile(new Position(c, r));
    }

    public Board(IBoard source)
    {
        Tiles = new IChessTile[8, 8];
        for (var c = 0; c < 8; c++)
            for (var r = 0; r < 8; r++)
            {
                var s = source.Tiles[c, r];
                var t = new ChessTile(s.TilePosition);
                if (s.Piece is not null)
                    t.Piece = new ChessPiece(s.Piece.Color, s.Piece.Type);
                Tiles[c, r] = t;
            }
    }
}

public class Player : IPlayer
{
    public string Name { get; }
    public PieceColor Color { get; }

    public Player(string name, PieceColor color)
    {
        Name = name;
        Color = color;
    }
}

public class PlayerTimer : IPlayerTimer
{
    public TimeSpan RemainingTime { get; private set; }

    private System.Threading.Timer? countdownTimer;

    public event EventHandler<TimerTickEventArgs> TimerTick = delegate { };
    public event EventHandler TimeExpired = delegate { };

    public PlayerTimer(TimeSpan initialTime)
    {
        RemainingTime = initialTime;
    }

    public void StartTimer()
    {
        countdownTimer?.Dispose();
        countdownTimer = new System.Threading.Timer(_ => Tick(), null, 1000, 1000);
    }

    public void StopTimer()
    {
        countdownTimer?.Dispose();
        countdownTimer = null;
    }

    private void Tick()
    {
        RemainingTime -= TimeSpan.FromSeconds(1);
        if (RemainingTime <= TimeSpan.Zero)
        {
            RemainingTime = TimeSpan.Zero;
            StopTimer();
            TimeExpired.Invoke(this, EventArgs.Empty);
        }
        else
        {
            TimerTick.Invoke(this, new TimerTickEventArgs(PieceColor.WHITE, RemainingTime));
        }
    }
}
