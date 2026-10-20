using System.Text.Json.Serialization;
using Api.Enums;

namespace Api.ValueObjects;

public record Move(
    [property: JsonRequired] Position From,
    [property: JsonRequired] Position To,
    PieceType? PromotionType = null
);