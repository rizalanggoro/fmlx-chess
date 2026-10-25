using Api.DTOs;
using Api.Enums;
using Api.Events;
using Api.Interfaces.Models;
using Api.ValueObjects;

namespace Api.Interfaces.Services;

public interface IChessGameEngine
{
    IBoard Board { get; }
    IPlayer CurrentPlayer { get; }
    GameState State { get; }
    GameResult? Result { get; }
    PieceColor? CheckedColor { get; }
    IReadOnlyList<MoveHistoryEntry> MoveHistory { get; }
    Dictionary<PieceColor, IPlayerTimer> PlayerTimeTracker { get; }
    Dictionary<PieceColor, IReadOnlyList<IChessPiece>> CapturedPiecesTracker { get; }
    CastlingRights CastlingRights { get; }
    Position? EnPassantTarget { get; }
    bool CanClaimThreefoldRepetition { get; }
    bool CanClaimFiftyMoveRule { get; }

    // Dinonaktifkan: state room disiarkan lewat SnapshotChangedEventHandler. Lihat notes.md.
    // event EventHandler<PieceMovedEventArgs>? PieceMovedEventHandler;
    // Dinonaktifkan: klik kotak ditangani frontend. Lihat notes.md.
    // event EventHandler<SquareClickedEventArgs>? SquareClickedEventHandler;
    event EventHandler<SnapshotChangedEventArgs>? SnapshotChangedEventHandler;
    event EventHandler<PieceCapturedEventArgs>? PieceCapturedEventHandler;
    event EventHandler<TurnChangedEventArgs>? TurnChangedEventHandler;
    event EventHandler<CheckEventArgs>? CheckDeclaredEventHandler;
    event EventHandler<GameEndedEventArgs>? GameEndedEventHandler;
    event EventHandler<DrawOfferedEventArgs>? DrawOfferedEventHandler;
    event EventHandler<DrawDeclinedEventArgs>? DrawDeclinedEventHandler;
    event EventHandler<TimerTickEventArgs>? TimerTickedEventHandler;

    void StartGame();
    void ResetGame();
    bool TryMakeMove(Move move);
    bool IsInBounds(Position position);
    IReadOnlyList<Move> GetAllLegalMoves();
    IReadOnlyList<TileDto> GetBoard();

    IReadOnlyList<MoveHistoryEntry> GetMoveHistory(PieceColor color);
    void OfferDraw(IPlayer offerer);
    void RespondToDrawOffer(IPlayer responder, bool accepted);
    bool ClaimDrawByThreefoldRepetition(IPlayer player);
    bool ClaimDrawByFiftyMoveRule(IPlayer player);
    void Resign(IPlayer player);
}