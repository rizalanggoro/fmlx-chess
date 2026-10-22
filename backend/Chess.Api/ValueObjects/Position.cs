using System.Text.Json.Serialization;

namespace Api.ValueObjects;

public readonly record struct Position(
    [property: JsonRequired] int Column,
    [property: JsonRequired] int Row
)
{
    public static Position operator +(Position position, SquareOffset offset)
    {
        return new Position(
            position.Column + offset.Column,
            position.Row + offset.Row
        );
    }
}
