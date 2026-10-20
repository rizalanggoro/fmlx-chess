using Api.Enums;
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
    int BlackRemainingSeconds);
