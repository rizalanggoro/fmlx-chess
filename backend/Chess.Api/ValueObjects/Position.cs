using System.Text.Json.Serialization;

namespace Api.ValueObjects;

public readonly record struct Position(
    [property: JsonRequired] int Row,
    [property: JsonRequired] int Column
)
{
    public static Position operator +(Position position, SquareOffset offset)
    {
        return new Position(
            position.Row + offset.Row,
            position.Column + offset.Column
        );
    }
}
