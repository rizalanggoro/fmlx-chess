namespace Api.ValueObjects;

/// <summary>
///     A delta between two squares: how far a move shifts a piece on each axis.
///     The concrete values live in <see cref="Api.Constants.SquareOffsets" />.
/// </summary>
public readonly record struct SquareOffset(
    int Row,
    int Column
);