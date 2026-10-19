using Api.Enums;
using Api.Services;

namespace Api.Models;

public class GameRoom
{
    public string Id { get; }
    public RoomMode Mode { get; }
    public GameManager Manager { get; }
    public string? WhiteToken { get; private set; }
    public string? BlackToken { get; private set; }
    public string? WhiteName { get; private set; }
    public string? BlackName { get; private set; }

    public GameRoom(
        string id,
        RoomMode mode,
        GameManager manager)
    {
        Id = id;
        Mode = mode;
        Manager = manager;
    }

    public bool WhiteJoined => Mode == RoomMode.Hotseat || WhiteToken is not null;

    public bool BlackJoined => Mode == RoomMode.Hotseat || BlackToken is not null;

    public string? Claim(PieceColor color, string? name = null)
    {
        if (color == PieceColor.White)
        {
            if (WhiteToken is not null)
                return null;
            WhiteName = name;
            return WhiteToken = NewToken();
        }

        if (BlackToken is not null)
            return null;
        BlackName = name;
        return BlackToken = NewToken();
    }

    public (PieceColor Color, string Token)? ClaimFree(string? name = null)
    {
        if (WhiteToken is null)
            return (PieceColor.White, Claim(PieceColor.White, name)!);
        if (BlackToken is null)
            return (PieceColor.Black, Claim(PieceColor.Black, name)!);
        return null;
    }

    public PieceColor? ColorFor(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return null;
        if (token == WhiteToken)
            return PieceColor.White;
        if (token == BlackToken)
            return PieceColor.Black;
        return null;
    }

    private static string NewToken()
    {
        return Guid.NewGuid().ToString("N");
    }
}
