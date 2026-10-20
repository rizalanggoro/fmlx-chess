using Api.Enums;
using Api.Services;

namespace Api.Models;

public class GameRoom
{
    public string Id { get; }
    public RoomMode Mode { get; }
    public GameManager Manager { get; }
    public bool WhiteJoined { get; private set; }
    public bool BlackJoined { get; private set; }
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
        WhiteJoined = BlackJoined = mode == RoomMode.HotSeat;
    }

    public bool Claim(PieceColor color, string? name = null)
    {
        if (color == PieceColor.White)
        {
            if (WhiteJoined)
                return false;
            WhiteJoined = true;
            WhiteName = name;
            return true;
        }

        if (BlackJoined)
            return false;
        BlackJoined = true;
        BlackName = name;
        return true;
    }

    public PieceColor? ClaimFree(string? name = null)
    {
        if (!WhiteJoined && Claim(PieceColor.White, name))
            return PieceColor.White;
        if (!BlackJoined && Claim(PieceColor.Black, name))
            return PieceColor.Black;
        return null;
    }

    public bool IsJoined(PieceColor color) =>
        color == PieceColor.White ? WhiteJoined : BlackJoined;
}
