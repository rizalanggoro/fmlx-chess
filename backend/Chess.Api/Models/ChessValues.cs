namespace Api.Models;

public readonly record struct Position(int Column, int Row);

public record Move(Position From, Position To, PieceType? PromotionType = null);

public record MoveHistoryEntry(Move Move, PieceColor PlayedBy);

public readonly record struct CastlingRights(bool WhiteKingSide, bool WhiteQueenSide, bool BlackKingSide, bool BlackQueenSide);

public record MoveApplicationResult(IChessPiece MovingPiece, IChessPiece? CapturedPiece, Position? CapturedSquare);

public record GameResult(GameEndReason Reason, PieceColor? Winner);

public delegate PieceType PromotionSelectionHandler(PieceColor color);
