using Api.Enums;
using Api.Interfaces.Services;
using Api.Models;
using Api.ValueObjects;

namespace Api.DTOs.Factories;

public static class GameStateDtoFactory
{
    public static GameStateDto FromRoom(Room room)
    {
        var engine = room.Engine;

        return new GameStateDto(
            engine.State,
            new PlayerDto(engine.CurrentPlayer.Name, engine.CurrentPlayer.Color),
            engine.Result,
            engine.CheckedColor,
            engine.CastlingRights,
            engine.EnPassantTarget,
            engine.MoveHistory.Count,
            RemainingSeconds(engine, PieceColor.White),
            RemainingSeconds(engine, PieceColor.Black),
            CapturedTypes(engine, PieceColor.White),
            CapturedTypes(engine, PieceColor.Black),
            engine.CanClaimThreefoldRepetition,
            engine.CanClaimFiftyMoveRule
        );
    }

    private static IReadOnlyList<PieceType> CapturedTypes(IChessGameEngine engine, PieceColor color)
    {
        return engine.CapturedPiecesTracker[color].Select(p => p.Type).ToList();
    }

    private static int RemainingSeconds(IChessGameEngine engine, PieceColor color)
    {
        return (int)engine.PlayerTimeTracker[color].RemainingTime.TotalSeconds;
    }
}
