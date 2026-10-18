using System.Text.Json.Serialization;
using Api.Models;

namespace Api.DTOs;

public record PlayerActionRequest([property: JsonRequired] PieceColor Color, string? Name = null, bool? Accepted = null);
