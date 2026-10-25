using Api.Enums;
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
    IReadOnlyList<PieceType> CapturedByBlack,
    bool CanClaimThreefoldRepetition,
    bool CanClaimFiftyMoveRule
);