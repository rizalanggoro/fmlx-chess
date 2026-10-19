using System.Text.Json.Serialization;
using Api.Enums;

namespace Api.Models;

public record Move(
    [property: JsonRequired] Position From,
    [property: JsonRequired] Position To,
    PieceType? PromotionType = null);

public delegate PieceType PromotionSelectionHandler(PieceColor color);
