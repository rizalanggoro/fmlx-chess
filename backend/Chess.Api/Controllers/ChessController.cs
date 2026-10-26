using Api.DTOs;
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
    public async Task<ActionResult<RoomSessionDto>> Create([FromBody] CreateRoomRequest request)
    {
        try
        {
            return Respond(await service.CreateRoom(request.Mode, request.Color, request.PlayerName));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while creating the room");
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
    public async Task<ActionResult<RoomSessionDto>> Join(string roomId, [FromQuery] string? name,
        [FromQuery] PieceColor? color)
    {
        try
        {
            return Respond(await service.JoinRoom(roomId, color, name));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while joining the room");
        }
    }

    /// <summary>
    ///     Returns the room id, mode, seat status, and player names.
    /// </summary>
    [HttpGet("{roomId}")]
    public ActionResult<RoomStatusDto> Status(string roomId)
    {
        return Respond(service.GetStatus(roomId));
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
        return Respond(service.GetState(roomId));
    }

    /// <summary>
    ///     Returns all 64 tiles with the piece occupying each one.
    /// </summary>
    [HttpGet("{roomId}/game/board")]
    public ActionResult<IReadOnlyList<TileDto>> GetBoard(string roomId)
    {
        try
        {
            return Respond(service.GetBoard(roomId));
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
            return Respond(service.GetLegalMoves(roomId));
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
    public ActionResult<IReadOnlyList<MoveHistoryEntry>> GetHistory(string roomId, [FromQuery] PieceColor? color)
    {
        try
        {
            return Respond(service.GetHistory(roomId, color));
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
            return Respond(service.StartGame(roomId));
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
            return Respond(service.ResetGame(roomId));
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
            return Respond(service.MakeMove(roomId, move, color));
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
            return Respond(service.Resign(roomId, ResolvePlayer(request)));
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
            return Respond(service.OfferDraw(roomId, ResolvePlayer(request)));
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
            return Respond(service.RespondToDrawOffer(roomId, ResolvePlayer(request), request.Accepted ?? false));
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
            return Respond(service.ClaimThreefoldRepetition(roomId, ResolvePlayer(request)));
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
            return Respond(service.ClaimFiftyMoveRule(roomId, ResolvePlayer(request)));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while claiming the draw");
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    ///     Builds the acting player from the request, falling back to the color name.
    /// </summary>
    private static IPlayer ResolvePlayer(PlayerActionRequest request)
    {
        return new Player(request.Name ?? request.Color.ToString(), request.Color);
    }

    /// <summary>
    ///     Maps a service result to its HTTP response.
    /// </summary>
    private ActionResult<T> Respond<T>(ServiceResult<T> result)
    {
        if (result.ErrorMessage is { } message)
            return StatusCode(result.StatusCode, new ApiErrorDto(message));

        return Ok(result.Data);
    }

    /// <summary>
    ///     500 response carrying the exception message.
    /// </summary>
    private ObjectResult Error(Exception ex, string message)
    {
        return StatusCode(
            StatusCodes.Status500InternalServerError,
            new ApiErrorDto(message, ex.Message)
        );
    }

    #endregion
}