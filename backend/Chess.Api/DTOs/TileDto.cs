using Api.Enums;
using Api.Models;

namespace Api.DTOs;

public record TileDto(int Column, int Row, PieceColor? Color, PieceType? Type);
