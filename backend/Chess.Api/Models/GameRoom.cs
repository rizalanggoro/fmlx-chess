using Api.Enums;
using Api.Interfaces.Services;

namespace Api.Models;

public class GameRoom
{
    public GameRoom(
        string id,
        RoomMode mode,
        IGameManager manager
    )
    {
        Id = id;
        Mode = mode;
        Manager = manager;
        WhiteJoined = BlackJoined = mode == RoomMode.HotSeat;
    }

    public string Id { get; }
    public RoomMode Mode { get; }
    public IGameManager Manager { get; }
    public bool WhiteJoined { get; internal set; }
    public bool BlackJoined { get; internal set; }
    public string? WhiteName { get; internal set; }
    public string? BlackName { get; internal set; }

    public bool IsJoined(PieceColor color)
    {
        return color == PieceColor.White ? WhiteJoined : BlackJoined;
    }
}