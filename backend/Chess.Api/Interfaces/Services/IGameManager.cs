using Api.Enums;
using Api.Events;
using Api.Interfaces.Models;
using Api.ValueObjects;

namespace Api.Interfaces.Services;

public interface IGameManager
{
    IBoard GameBoard { get; }
    IPlayer CurrentPlayer { get; }
    GameState State { get; }
    GameResult? Result { get; }
    IReadOnlyList<MoveHistoryEntry> MoveHistory { get; }
    Dictionary<PieceColor, IPlayerTimer> PlayerTimeTracker { get; }
    CastlingRights CastlingRights { get; }
    Position? EnPassantTarget { get; }

    event EventHandler<PieceMovedEventArgs>? PieceMovedEventHandler;
    event EventHandler<BoardChangedEventArgs>? BoardChangedEventHandler;
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
    IReadOnlyList<Move> GetLegalMoves(Position from);
    IReadOnlyList<MoveHistoryEntry> GetMoveHistory(PieceColor color);
    void OfferDraw(IPlayer offerer);
    void RespondToDrawOffer(IPlayer responder, bool accepted);
    bool ClaimDrawByThreefoldRepetition(IPlayer player);
    bool ClaimDrawByFiftyMoveRule(IPlayer player);
    void Resign(IPlayer player);
}
