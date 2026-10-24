using Api.Enums;
using Api.Interfaces.Services;

namespace Api.Models;

public class Room
{
    public Room(
        string id,
        RoomMode mode,
        IChessGameEngine engine
    )
    {
        Id = id;
        Mode = mode;
        Engine = engine;
        WhiteJoined = BlackJoined = mode == RoomMode.HotSeat;
    }

    public string Id { get; }
    public RoomMode Mode { get; }
    public IChessGameEngine Engine { get; }
    public bool WhiteJoined { get; internal set; }
    public bool BlackJoined { get; internal set; }
    public string? WhiteName { get; internal set; }
    public string? BlackName { get; internal set; }

    public bool IsJoined(PieceColor color)
    {
        return color == PieceColor.White ? WhiteJoined : BlackJoined;
    }
}