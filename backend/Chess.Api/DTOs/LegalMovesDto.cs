using Api.Enums;
using Api.Models;
using Api.ValueObjects;

namespace Api.DTOs;

public record LegalMovesDto(
    int MoveCount,
    IReadOnlyList<Move> Moves
);