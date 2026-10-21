using Api.Enums;
using Api.Interfaces.Models;
using Api.Interfaces.Services;
using Api.Models;
using Api.ValueObjects;

namespace Api.DTOs;

public record GameStateDto(
    GameState State,
    PlayerDto CurrentPlayer,
    GameResult? Result,
    CastlingRights CastlingRights,
    Position? EnPassantTarget,
    int MoveCount,
    int WhiteRemainingSeconds,
    int BlackRemainingSeconds)
{
    public static GameStateDto From(GameRoom room)
    {
        var manager = room.Manager;
        return new GameStateDto(
            manager.State,
            new PlayerDto(manager.CurrentPlayer.Name, manager.CurrentPlayer.Color),
            manager.Result,
            manager.CastlingRights,
            manager.EnPassantTarget,
            manager.MoveHistory.Count,
            RemainingSeconds(manager, PieceColor.White),
            RemainingSeconds(manager, PieceColor.Black));
    }

    private static int RemainingSeconds(IGameManager manager, PieceColor color) =>
        (int)manager.PlayerTimeTracker[color].RemainingTime.TotalSeconds;
}
