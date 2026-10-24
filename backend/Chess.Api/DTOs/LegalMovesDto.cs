using Api.Enums;
using Api.Models;
using Api.ValueObjects;

namespace Api.DTOs;

public record LegalMovesDto(
    int MoveCount,
    IReadOnlyList<Move> Moves)
{
    public static LegalMovesDto FromGameRoom(GameRoom room)
    {
        var manager = room.Manager;
        var moves = manager.State == GameState.InProgress
            ? manager.GetAllLegalMoves()
            : Array.Empty<Move>();

        return new LegalMovesDto(manager.MoveHistory.Count, moves);
    }
}
