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
    PieceColor? CheckedColor,
    CastlingRights CastlingRights,
    Position? EnPassantTarget,
    int MoveCount,
    int WhiteRemainingSeconds,
    int BlackRemainingSeconds,
    IReadOnlyList<PieceType> CapturedByWhite,
    IReadOnlyList<PieceType> CapturedByBlack)
{
    public static GameStateDto FromGameRoom(GameRoom room)
    {
        var manager = room.Manager;
        return new GameStateDto(
            manager.State,
            new PlayerDto(manager.CurrentPlayer.Name, manager.CurrentPlayer.Color),
            manager.Result,
            manager.CheckedColor,
            manager.CastlingRights,
            manager.EnPassantTarget,
            manager.MoveHistory.Count,
            RemainingSeconds(manager, PieceColor.White),
            RemainingSeconds(manager, PieceColor.Black),
            CapturedTypes(manager, PieceColor.White),
            CapturedTypes(manager, PieceColor.Black));
    }

    private static IReadOnlyList<PieceType> CapturedTypes(IGameService manager, PieceColor color) =>
        manager.CapturedPiecesTracker[color].Select(p => p.Type).ToList();

    private static int RemainingSeconds(IGameService manager, PieceColor color) =>
        (int)manager.PlayerTimeTracker[color].RemainingTime.TotalSeconds;
}
