using Api.Enums;
using Api.Models;
using Api.ValueObjects;

namespace Api.DTOs.Factories;

public static class LegalMovesDtoFactory
{
    public static LegalMovesDto FromRoom(Room room)
    {
        var engine = room.Engine;
        var moves = engine.State == GameState.InProgress
            ? engine.GetAllLegalMoves()
            : [];

        return new LegalMovesDto(engine.MoveHistory.Count, moves);
    }
}