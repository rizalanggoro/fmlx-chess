using System.Text.Json.Serialization;
using Api.Enums;

namespace Api.DTOs;

public record PlayerActionDto(
    [property: JsonRequired] PieceColor Color,
    string? Name = null,
    bool? Accepted = null
);
