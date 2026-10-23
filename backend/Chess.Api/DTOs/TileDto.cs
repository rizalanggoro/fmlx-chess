using Api.Enums;
using Api.Models;

namespace Api.DTOs;

public record TileDto(
    int Row,
    int Column,
    PieceColor? Color,
    PieceType? Type);
