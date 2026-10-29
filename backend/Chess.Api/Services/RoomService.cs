using Api.DTOs;
using Api.DTOs.Events;
using Api.DTOs.Factories;
using Api.Enums;
using Api.Events;
using Api.GameEngine;
using Api.Hubs;
using Api.Interfaces.GameEngine;
using Api.Interfaces.Models;
using Api.Interfaces.Services;
using Api.Models;
using Api.ValueObjects;
using Microsoft.AspNetCore.SignalR;

namespace Api.Services;

public class RoomService(
    IHubContext<GameHub> hub,
    IRoomRegistry registry,
    ILogger<RoomService> logger,
    IBoardInitializer boardInitializer
) : IRoomService
{
    #region Rooms

    public async Task<ServiceResult<RoomSessionDto>> CreateRoom(CreateRoomDto request)
    {
        string roomId;
        do
        {
            roomId = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        } while (registry.Get(roomId) is not null);

        var engine = new ChessGameEngine(
            new Board(),
            new Player("White", PieceColor.White),
            new Player("Black", PieceColor.Black),
            TimeSpan.FromMinutes(10),
            boardInitializer
        );
        var room = new Room(roomId, request.Mode, engine);

        engine.SnapshotChangedEventHandler += async (_, e) => await NotifySnapshot(room, e);
        engine.PieceCapturedEventHandler += async (_, e) => await NotifyPieceCaptured(room.Id, e);
        engine.CheckDeclaredEventHandler += async (_, e) => await NotifyCheck(room.Id, e);
        engine.GameEndedEventHandler += async (_, e) => await NotifyGameEnded(room.Id, e);
        engine.DrawOfferedEventHandler += async (_, e) => await NotifyDrawOffered(room.Id, e);
        engine.DrawDeclinedEventHandler += async (_, e) => await NotifyDrawDeclined(room.Id, e);
        engine.TimerTickedEventHandler += async (_, e) => await NotifyTimerTick(room.Id, e);

        registry.Create(room);

        if (request.Mode == RoomMode.HotSeat)
        {
            engine.StartGame();
            logger.LogInformation("HotSeat room {RoomId} created and game started", room.Id);
            return ServiceResult<RoomSessionDto>.Success(new RoomSessionDto(room.Id, room.Mode, null));
        }

        var pieceColor = request.Color ?? PieceColor.White;

        ClaimRoom(room, pieceColor, NormalizeName(request.PlayerName));
        logger.LogInformation("Online room {RoomId} created. Seat {Color} claimed by {PlayerName}", room.Id, pieceColor, NormalizeName(request.PlayerName) ?? "Anonymous");
        await NotifyLobby();

        return ServiceResult<RoomSessionDto>.Success(new RoomSessionDto(room.Id, room.Mode, pieceColor));
    }

    public async Task<ServiceResult<RoomSessionDto>> JoinRoom(string roomId, JoinRoomDto dto)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<RoomSessionDto>();

        if (room.Mode != RoomMode.Online)
        {
            logger.LogWarning("Reject join: Room {RoomId} is not in online mode", roomId);
            return ServiceResult<RoomSessionDto>.Error(
                StatusCodes.Status400BadRequest,
                "Only online rooms can be joined"
            );
        }

        if (dto.Color is null)
            return ServiceResult<RoomSessionDto>.Error(
                StatusCodes.Status400BadRequest,
                "Color is required"
            );

        if (!ClaimRoom(room, dto.Color.Value, NormalizeName(dto.Name)))
        {
            logger.LogWarning("Reject join: Seat {Color} in room {RoomId} is already taken or room is full", dto.Color.Value, roomId);
            return ServiceResult<RoomSessionDto>.Error(
                StatusCodes.Status409Conflict,
                room is { WhiteJoined: true, BlackJoined: true } ? "Room is full" : "Seat is already taken"
            );
        }

        logger.LogInformation("Player {PlayerName} joined room {RoomId} as {Color}", NormalizeName(dto.Name) ?? "Anonymous", roomId, dto.Color.Value);

        if (room.Engine.State == GameState.NotStarted)
        {
            room.Engine.StartGame();
            logger.LogInformation("Both seats filled in room {RoomId}. Online game started", roomId);
        }

        await NotifyLobby();

        return ServiceResult<RoomSessionDto>.Success(new RoomSessionDto(room.Id, room.Mode, dto.Color));
    }

    public ServiceResult<RoomStatusDto> GetStatus(string roomId)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<RoomStatusDto>();

        return ServiceResult<RoomStatusDto>.Success(RoomStatusDtoFactory.FromRoom(room));
    }

    public IReadOnlyList<RoomStatusDto> AvailableRooms()
    {
        return registry.All
            .Where(room => room.Mode == RoomMode.Online && room.WhiteJoined != room.BlackJoined)
            .OrderBy(room => room.Id)
            .Select(RoomStatusDtoFactory.FromRoom)
            .ToList();
    }

    private async Task NotifyLobby()
    {
        await SendSafeAsync("lobby", "LobbyChanged", new LobbyChangedDto(AvailableRooms()));
    }

    private static bool ClaimRoom(Room room, PieceColor color, string? name)
    {
        if (color == PieceColor.White)
        {
            if (room.WhiteJoined)
                return false;

            room.WhiteJoined = true;
            room.WhiteName = name;

            return true;
        }

        if (room.BlackJoined)
            return false;

        room.BlackJoined = true;
        room.BlackName = name;

        return true;
    }

    #endregion

    #region Game actions

    public ServiceResult<GameStateDto> StartGame(string roomId)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<GameStateDto>();

        room.Engine.StartGame();

        return ServiceResult<GameStateDto>.Success(GameStateDtoFactory.FromRoom(room));
    }

    public ServiceResult<GameStateDto> ResetGame(string roomId)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<GameStateDto>();

        room.Engine.ResetGame();

        return ServiceResult<GameStateDto>.Success(GameStateDtoFactory.FromRoom(room));
    }

    public ServiceResult<GameStateDto> MakeMove(string roomId, MakeMoveDto dto)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<GameStateDto>();

        if (room.Mode == RoomMode.Online)
        {
            if (dto.Color is null || !room.IsJoined(dto.Color.Value))
            {
                logger.LogWarning("Unauthorized move attempt in room {RoomId} by color {Color}", roomId, dto.Color);
                return NotAPlayer();
            }

            if (dto.Color.Value != room.Engine.CurrentPlayer.Color)
            {
                logger.LogWarning("Move attempted out of turn in room {RoomId} by {Color}. Current turn: {CurrentTurn}", roomId, dto.Color.Value, room.Engine.CurrentPlayer.Color);
                return ServiceResult<GameStateDto>.Error(
                    StatusCodes.Status403Forbidden,
                    "Not your turn"
                );
            }
        }

        var move = new Move(dto.From, dto.To, dto.PromotionType);
        var turnBeforeMove = room.Engine.CurrentPlayer.Color;
        if (!room.Engine.TryMakeMove(move))
        {
            logger.LogWarning("Illegal move attempted in room {RoomId} from [{FromRow},{FromCol}] to [{ToRow},{ToCol}] by {Color}", roomId, dto.From.Row, dto.From.Column, dto.To.Row, dto.To.Column, turnBeforeMove);
            return ServiceResult<GameStateDto>.Error(
                StatusCodes.Status400BadRequest,
                "Illegal move"
            );
        }

        logger.LogInformation("Move executed in room {RoomId} by {Color}: [{FromRow},{FromCol}] -> [{ToRow},{ToCol}] (Promotion: {PromotionType}). Next turn: {NextTurn}", roomId, turnBeforeMove, dto.From.Row, dto.From.Column, dto.To.Row, dto.To.Column, dto.PromotionType, room.Engine.CurrentPlayer.Color);

        return ServiceResult<GameStateDto>.Success(GameStateDtoFactory.FromRoom(room));
    }

    public ServiceResult<GameStateDto> Resign(string roomId, PlayerActionDto request)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<GameStateDto>();

        var player = ResolvePlayer(request);
        if (!IsPlayer(room, player))
            return NotAPlayer();

        room.Engine.Resign(player);
        logger.LogInformation("Player {Color} ({PlayerName}) resigned in room {RoomId}. Result: {Result}", player.Color, player.Name, roomId, room.Engine.Result);

        return ServiceResult<GameStateDto>.Success(GameStateDtoFactory.FromRoom(room));
    }

    public ServiceResult<GameStateDto> OfferDraw(string roomId, PlayerActionDto request)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<GameStateDto>();

        var player = ResolvePlayer(request);
        if (!IsPlayer(room, player))
            return NotAPlayer();

        room.Engine.OfferDraw(player);
        logger.LogInformation("Player {Color} offered a draw in room {RoomId}", player.Color, roomId);

        return ServiceResult<GameStateDto>.Success(GameStateDtoFactory.FromRoom(room));
    }

    public ServiceResult<GameStateDto> RespondToDrawOffer(string roomId, PlayerActionDto request)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<GameStateDto>();

        var player = ResolvePlayer(request);
        if (!IsPlayer(room, player))
            return NotAPlayer();

        var accepted = request.Accepted ?? false;
        room.Engine.RespondToDrawOffer(player, accepted);
        logger.LogInformation("Player {Color} responded to draw offer in room {RoomId}. Accepted: {Accepted}", player.Color, roomId, accepted);

        return ServiceResult<GameStateDto>.Success(GameStateDtoFactory.FromRoom(room));
    }

    public ServiceResult<GameStateDto> ClaimThreefoldRepetition(string roomId, PlayerActionDto request)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<GameStateDto>();

        var player = ResolvePlayer(request);
        if (!IsPlayer(room, player))
            return NotAPlayer();

        if (!room.Engine.ClaimDrawByThreefoldRepetition(player))
        {
            logger.LogWarning("Player {Color} failed to claim threefold repetition in room {RoomId}", player.Color, roomId);
            return ServiceResult<GameStateDto>.Error(
                StatusCodes.Status400BadRequest,
                "Threefold repetition claim not available"
            );
        }

        logger.LogInformation("Player {Color} claimed draw by threefold repetition in room {RoomId}", player.Color, roomId);
        return ServiceResult<GameStateDto>.Success(GameStateDtoFactory.FromRoom(room));
    }

    public ServiceResult<GameStateDto> ClaimFiftyMoveRule(string roomId, PlayerActionDto request)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<GameStateDto>();

        var player = ResolvePlayer(request);
        if (!IsPlayer(room, player))
            return NotAPlayer();

        if (!room.Engine.ClaimDrawByFiftyMoveRule(player))
        {
            logger.LogWarning("Player {Color} failed to claim fifty-move rule in room {RoomId}", player.Color, roomId);
            return ServiceResult<GameStateDto>.Error(
                StatusCodes.Status400BadRequest,
                "Fifty-move claim not available"
            );
        }

        logger.LogInformation("Player {Color} claimed draw by fifty-move rule in room {RoomId}", player.Color, roomId);
        return ServiceResult<GameStateDto>.Success(GameStateDtoFactory.FromRoom(room));
    }

    #endregion

    #region Game state

    public ServiceResult<GameStateDto> GetState(string roomId)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<GameStateDto>();

        return ServiceResult<GameStateDto>.Success(GameStateDtoFactory.FromRoom(room));
    }

    public ServiceResult<IReadOnlyList<TileDto>> GetBoard(string roomId)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<IReadOnlyList<TileDto>>();

        return ServiceResult<IReadOnlyList<TileDto>>.Success(room.Engine.GetBoard());
    }

    public ServiceResult<LegalMovesDto> GetLegalMoves(string roomId)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<LegalMovesDto>();

        return ServiceResult<LegalMovesDto>.Success(LegalMovesDtoFactory.FromRoom(room));
    }

    public ServiceResult<IReadOnlyList<MoveHistoryEntry>> GetHistory(string roomId, PieceColor? color)
    {
        if (registry.Get(roomId) is not { } room)
            return RoomNotFound<IReadOnlyList<MoveHistoryEntry>>();

        return ServiceResult<IReadOnlyList<MoveHistoryEntry>>.Success(
            color.HasValue ? room.Engine.GetMoveHistory(color.Value) : room.Engine.MoveHistory
        );
    }

    #endregion

    #region Helpers

    private static IPlayer ResolvePlayer(PlayerActionDto request)
    {
        return new Player(request.Name ?? request.Color.ToString(), request.Color);
    }

    private static bool IsPlayer(Room room, IPlayer player)
    {
        return room.Mode != RoomMode.Online || room.IsJoined(player.Color);
    }

    private static string? NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, 24)];
    }

    private static ServiceResult<T> RoomNotFound<T>()
    {
        return ServiceResult<T>.Error(
            StatusCodes.Status404NotFound,
            "Room not found"
        );
    }

    private static ServiceResult<GameStateDto> NotAPlayer()
    {
        return ServiceResult<GameStateDto>.Error(
            StatusCodes.Status403Forbidden,
            "You are not a player in this room"
        );
    }

    #endregion

    #region Broadcast

    private async Task NotifySnapshot(Room room, SnapshotChangedEventArgs e)
    {
        logger.LogInformation("Broadcasting snapshot for room {RoomId}. Current turn: {Turn}", room.Id, room.Engine.CurrentPlayer.Color);

        var board = new List<TileDto>(64);
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var p = e.Board.Tiles[r, c].Piece;
            board.Add(new TileDto(r, c, p?.Color, p?.Type));
        }

        await SendSafeAsync(
            $"room:{room.Id}",
            "SnapshotChanged",
            new SnapshotChangedDto(
                room.Id,
                RoomStatusDtoFactory.FromRoom(room),
                GameStateDtoFactory.FromRoom(room),
                board,
                e.History,
                LegalMovesDtoFactory.FromRoom(room)
            )
        );
    }

    private async Task NotifyCheck(string roomId, CheckEventArgs e)
    {
        logger.LogInformation("Check declared in room {RoomId} against {CheckedColor}", roomId, e.CheckedColor);
        await SendSafeAsync(
            $"room:{roomId}",
            "CheckDeclared",
            new CheckDeclaredDto(roomId, e.CheckedColor)
        );
    }

    private async Task NotifyPieceCaptured(string roomId, PieceCapturedEventArgs e)
    {
        logger.LogInformation("Piece captured in room {RoomId}: {PieceColor} {PieceType}", roomId, e.CapturedPiece.Color, e.CapturedPiece.Type);
        await SendSafeAsync(
            $"room:{roomId}",
            "PieceCaptured",
            new PieceCapturedDto(roomId, e.CapturedPiece.Color, e.CapturedPiece.Type)
        );
    }

    private async Task NotifyTimerTick(string roomId, TimerTickEventArgs e)
    {
        await SendSafeAsync(
            $"room:{roomId}",
            "TimerTick",
            new TimerTickDto(roomId, e.Color, (int)e.TimeRemaining.TotalSeconds)
        );
    }

    private async Task NotifyGameEnded(string roomId, GameEndedEventArgs e)
    {
        logger.LogInformation("Game ended in room {RoomId} with result: {Result}", roomId, e.Result);
        await SendSafeAsync(
            $"room:{roomId}",
            "GameEnded",
            new GameEndedDto(roomId, e.Result)
        );
    }

    private async Task NotifyDrawOffered(string roomId, DrawOfferedEventArgs e)
    {
        logger.LogInformation("Draw offered in room {RoomId} by {Color} ({Name})", roomId, e.Offerer.Color, e.Offerer.Name);
        await SendSafeAsync(
            $"room:{roomId}",
            "DrawOffered",
            new DrawOfferedDto(roomId, e.Offerer.Color, e.Offerer.Name)
        );
    }

    private async Task NotifyDrawDeclined(string roomId, DrawDeclinedEventArgs e)
    {
        logger.LogInformation("Draw declined in room {RoomId} by opponent of {Color}", roomId, e.Offerer.Color);
        await SendSafeAsync(
            $"room:{roomId}",
            "DrawDeclined",
            new DrawDeclinedDto(roomId, e.Offerer.Color)
        );
    }

    private async Task SendSafeAsync(string group, string method = "GameChanged", object? payload = null)
    {
        try
        {
            if (payload is null)
                await hub.Clients.Group(group).SendAsync(method);
            else
                await hub.Clients.Group(group).SendAsync(method, payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast {Method} to {Group}", method, group);
        }
    }

    #endregion
}