using System.Text.Json.Serialization;

namespace Api.Models;

public readonly record struct Position(
    [property: JsonRequired] int Column,
    [property: JsonRequired] int Row);
