using Api.Enums;
using Api.Models;

namespace Api.DTOs;

public record RoomSessionDto(
    string Id,
    RoomMode Mode,
    PieceColor? Color,
    string? Token);
