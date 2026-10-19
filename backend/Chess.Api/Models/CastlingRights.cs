namespace Api.Models;

public readonly record struct CastlingRights(
    bool WhiteKingSide,
    bool WhiteQueenSide,
    bool BlackKingSide,
    bool BlackQueenSide);
