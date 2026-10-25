using Api.DTOs;
using Api.DTOs.Factories;
using Api.DTOs.Requests;
using Api.Enums;
using Api.Interfaces.Models;
using Api.Interfaces.Services;
using Api.Models;
using Api.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("rooms")]
public class ChessController(IRoomService service) : ControllerBase
{
    #region Rooms

    /// <summary>
    ///     Creates a room. Hot-seat rooms start immediately with both seats open;
    ///     online rooms claim the creator's seat and wait for an opponent.
    /// </summary>
    [HttpPost]
    public ActionResult<RoomSessionDto> Create([FromBody] CreateRoomRequest request)
    {
        try
        {
            var room = service.CreateRoom(request.Mode);
            if (room.Mode == RoomMode.HotSeat)
            {
                service.StartGame(room);
                return Ok(new RoomSessionDto(room.Id, room.Mode, null));
            }

            var pieceColor = request.Color ?? PieceColor.White;

            service.ClaimRoom(room, pieceColor, NormalizeName(request.PlayerName));
            service.NotifyLobby();

            return Ok(
                new RoomSessionDto(
                    room.Id,
                    room.Mode,
                    pieceColor
                )
            );
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new ApiErrorDto(
                    "An error occurred while creating the room",
                    ex.Message
                )
            );
        }
    }

    /// <summary>
    ///     Lists online rooms that still have one free seat.
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<RoomStatusDto>> List()
    {
        return Ok(service.AvailableRooms());
    }

    /// <summary>
    ///     Claims a free seat in an online room and starts the game once both seats
    ///     are filled. Returns 409 when the requested seat is already taken.
    /// </summary>
    [HttpPost("{roomId}/join")]
    public ActionResult<RoomSessionDto> Join(string roomId, [FromQuery] string? name, [FromQuery] PieceColor? color)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return NotFound(new ApiErrorDto("Room not found"));

            if (room.Mode != RoomMode.Online)
                return BadRequest(new ApiErrorDto("Only online rooms can be joined"));

            if (color is null)
                return BadRequest(new ApiErrorDto("Color is required"));

            if (!service.ClaimRoom(room, color.Value, NormalizeName(name)))
                return Conflict(new ApiErrorDto(
                    room is { WhiteJoined: true, BlackJoined: true } ? "Room is full" : "Seat is already taken"));

            service.StartGameIfNotStarted(room);
            service.NotifyLobby();

            return Ok(new RoomSessionDto(room.Id, room.Mode, color));
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new ApiErrorDto(
                    "An error occurred while joining the room",
                    ex.Message
                )
            );
        }
    }

    /// <summary>
    ///     Returns the room id, mode, seat status, and player names.
    /// </summary>
    [HttpGet("{roomId}")]
    public ActionResult<RoomStatusDto> Status(string roomId)
    {
        if (service.GetRoom(roomId) is not { } room)
            return NotFound(
                new ApiErrorDto("Room not found")
            );

        return Ok(RoomStatusDtoFactory.FromRoom(room));
    }

    #endregion

    #region Game state

    /// <summary>
    ///     Returns the current game state: state, turn, result, castling rights,
    ///     clocks, captured pieces, and draw-claim availability.
    /// </summary>
    [HttpGet("{roomId}/game")]
    public ActionResult<GameStateDto> GetState(string roomId)
    {
        if (service.GetRoom(roomId) is not { } room)
            return RoomNotFound();

        return Ok(GameStateDtoFactory.FromRoom(room));
    }

    /// <summary>
    ///     Returns all 64 tiles with the piece occupying each one.
    /// </summary>
    [HttpGet("{roomId}/game/board")]
    public ActionResult<IEnumerable<TileDto>> GetBoard(string roomId)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            return Ok(service.GetBoard(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving the board");
        }
    }

    /// <summary>
    ///     Returns every legal move for the current player, or an empty list when
    ///     the game is not in progress.
    /// </summary>
    [HttpGet("{roomId}/game/legal-moves/all")]
    public ActionResult<LegalMovesDto> GetAllLegalMoves(string roomId)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            return Ok(LegalMovesDtoFactory.FromRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving legal moves");
        }
    }

    /// <summary>
    ///     Returns the move history, optionally filtered to the moves made by
    ///     <paramref name="color" />.
    /// </summary>
    [HttpGet("{roomId}/game/history")]
    public ActionResult<IEnumerable<MoveHistoryEntry>> GetHistory(string roomId, [FromQuery] PieceColor? color)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            return Ok(service.GetHistory(room, color));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving history");
        }
    }

    #endregion

    #region Game actions

    /// <summary>
    ///     Starts a fresh game, resetting the board, clocks, history, and captured
    ///     pieces.
    /// </summary>
    [HttpPost("{roomId}/game/start")]
    public ActionResult<GameStateDto> Start(string roomId)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            service.StartGame(room);

            return Ok(GameStateDtoFactory.FromRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while starting the game");
        }
    }

    /// <summary>
    ///     Puts the game back to <see cref="GameState.NotStarted" /> with a fresh
    ///     board, without starting a new game.
    /// </summary>
    [HttpPost("{roomId}/game/reset")]
    public ActionResult<GameStateDto> Reset(string roomId)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            service.ResetGame(room);

            return Ok(GameStateDtoFactory.FromRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while resetting the game");
        }
    }

    /// <summary>
    ///     Validates and plays a move. Online rooms require the <paramref name="color" />
    ///     of the player on turn; hot-seat rooms take the side that owns the piece.
    /// </summary>
    [HttpPost("{roomId}/game/moves")]
    public ActionResult<GameStateDto> MakeMove(string roomId, [FromBody] Move move, [FromQuery] PieceColor? color)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            if (Failure(service.MakeMove(room, move, color), "Illegal move") is { } failure)
                return failure;

            return Ok(GameStateDtoFactory.FromRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while making the move");
        }
    }

    /// <summary>
    ///     Resigns the game on behalf of the given player. Online rooms require a
    ///     seated color.
    /// </summary>
    [HttpPost("{roomId}/game/resign")]
    public ActionResult<GameStateDto> Resign(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            if (Failure(service.Resign(room, ResolvePlayer(request)), "Illegal action") is { } failure)
                return failure;

            return Ok(GameStateDtoFactory.FromRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while resigning");
        }
    }

    /// <summary>
    ///     Offers a draw on behalf of the given player; the opponent answers through
    ///     the respond endpoint.
    /// </summary>
    [HttpPost("{roomId}/game/draw/offer")]
    public ActionResult<GameStateDto> OfferDraw(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            if (Failure(service.OfferDraw(room, ResolvePlayer(request)), "Illegal action") is { } failure)
                return failure;

            return Ok(GameStateDtoFactory.FromRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while offering a draw");
        }
    }

    /// <summary>
    ///     Accepts or declines the pending draw offer from the given player.
    /// </summary>
    [HttpPost("{roomId}/game/draw/respond")]
    public ActionResult<GameStateDto> RespondToDraw(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            if (Failure(service.RespondToDrawOffer(room, ResolvePlayer(request), request.Accepted ?? false),
                    "Illegal action") is { } failure)
                return failure;

            return Ok(GameStateDtoFactory.FromRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while responding to the draw offer");
        }
    }

    /// <summary>
    ///     Claims a draw by threefold repetition. Returns 400 when the claim is not
    ///     available yet.
    /// </summary>
    [HttpPost("{roomId}/game/draw/claim-threefold")]
    public ActionResult<GameStateDto> ClaimThreefold(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            if (Failure(service.ClaimThreefoldRepetition(room, ResolvePlayer(request)),
                    "Threefold repetition claim not available") is { } failure)
                return failure;

            return Ok(GameStateDtoFactory.FromRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while claiming the draw");
        }
    }

    /// <summary>
    ///     Claims a draw by the fifty-move rule. Returns 400 when the claim is not
    ///     available yet.
    /// </summary>
    [HttpPost("{roomId}/game/draw/claim-fifty")]
    public ActionResult<GameStateDto> ClaimFifty(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (service.GetRoom(roomId) is not { } room)
                return RoomNotFound();

            if (Failure(service.ClaimFiftyMoveRule(room, ResolvePlayer(request)), "Fifty-move claim not available") is
                { } failure)
                return failure;

            return Ok(GameStateDtoFactory.FromRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while claiming the draw");
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    ///     Trims a player name and caps it at 24 characters; blank names become
    ///     <c>null</c>.
    /// </summary>
    private static string? NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, 24)];
    }

    /// <summary>
    ///     Builds the acting player from the request, falling back to the color name.
    /// </summary>
    private static IPlayer ResolvePlayer(PlayerActionRequest request)
    {
        return new Player(request.Name ?? request.Color.ToString(), request.Color);
    }

    /// <summary>
    ///     Maps a game action result to an HTTP error, or <c>null</c> when the action
    ///     succeeded.
    /// </summary>
    private ObjectResult? Failure(GameActionResult result, string invalidMessage)
    {
        return result switch
        {
            GameActionResult.NotAPlayer => StatusCode(403, new ApiErrorDto("You are not a player in this room")),
            GameActionResult.NotYourTurn => StatusCode(403, new ApiErrorDto("Not your turn")),
            GameActionResult.Invalid => BadRequest(new ApiErrorDto(invalidMessage)),
            _ => null
        };
    }

    /// <summary>
    ///     404 response for an unknown room id.
    /// </summary>
    private ObjectResult RoomNotFound()
    {
        return NotFound(new ApiErrorDto("Room not found"));
    }

    /// <summary>
    ///     500 response carrying the exception message.
    /// </summary>
    private ObjectResult Error(Exception ex, string message)
    {
        return StatusCode(500, new ApiErrorDto(message, ex.Message));
    }

    #endregion
}