using System.Text.Json.Serialization;
using Api.Models;

namespace Api.DTOs;

public record CreateRoomRequest([property: JsonRequired] RoomMode Mode, PieceColor? Color = null, string? Name = null);
