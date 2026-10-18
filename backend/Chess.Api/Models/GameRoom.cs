using Api.Services;

namespace Api.Models;

public class GameRoom
{
    public string Id { get; }
    public RoomMode Mode { get; }
    public GameManager Manager { get; }
    public string? WhiteToken { get; private set; }
    public string? BlackToken { get; private set; }

    public GameRoom(string id, RoomMode mode, GameManager manager)
    {
        Id = id;
        Mode = mode;
        Manager = manager;
    }

    public bool WhiteJoined => Mode == RoomMode.Hotseat || WhiteToken is not null;

    public bool BlackJoined => Mode == RoomMode.Hotseat || BlackToken is not null;

    public string ClaimWhite()
    {
        return WhiteToken ??= NewToken();
    }

    public string? ClaimBlack()
    {
        return BlackToken is null ? BlackToken = NewToken() : null;
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
