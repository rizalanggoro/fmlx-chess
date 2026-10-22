using Api.Enums;

namespace Api.DTOs;

public record PlayerDto(
    string Name,
    PieceColor Color
);