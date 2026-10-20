using System.Text.Json.Serialization;

namespace Api.ValueObjects;

public readonly record struct Position(
    [property: JsonRequired] int Column,
    [property: JsonRequired] int Row
);