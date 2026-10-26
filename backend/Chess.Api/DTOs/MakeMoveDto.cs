using Api.Enums;
using Api.ValueObjects;

namespace Api.DTOs;

public record MakeMoveDto(
    Position From,
    Position To,
    PieceType? PromotionType = null,
    PieceColor? Color = null
);
