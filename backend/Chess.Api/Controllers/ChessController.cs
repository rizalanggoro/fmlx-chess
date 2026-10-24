using Api.DTOs;
using Api.DTOs.Requests;
using Api.Enums;
using Api.Interfaces.Models;
using Api.Interfaces.Services;
using Api.Models;
using Api.Utils;
using Api.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("rooms")]
public class ChessController(IRoomService roomService) : ControllerBase
{
    #region Rooms

    [HttpPost]
    public ActionResult<RoomSessionDto> Create([FromBody] CreateRoomRequest request)
    {
        try
        {
            var room = roomService.Create(request.Mode);
            if (room.Mode == RoomMode.HotSeat)
            {
                room.Manager.StartGame();
                return Ok(new RoomSessionDto(room.Id, room.Mode, null));
            }

            var color = request.Color ?? PieceColor.White;

            roomService.Claim(room, color, NameUtils.Normalize(request.PlayerName));
            roomService.NotifyLobby();

            return Ok(new RoomSessionDto(room.Id, room.Mode, color));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorDto("An error occurred while creating the room", ex.Message));
        }
    }

    [HttpGet]
    public ActionResult<IEnumerable<RoomStatusDto>> List()
    {
        return Ok(roomService.AvailableRooms());
    }

    [HttpPost("{roomId}/join")]
    public ActionResult<RoomSessionDto> Join(string roomId, [FromQuery] string? name, [FromQuery] PieceColor? color)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return NotFound(new ApiErrorDto("Room not found"));
            if (room.Mode != RoomMode.Online)
                return BadRequest(new ApiErrorDto("Only online rooms can be joined"));
            if (color is null)
                return BadRequest(new ApiErrorDto("Color is required"));
            if (!roomService.Claim(room, color.Value, NameUtils.Normalize(name)))
                return Conflict(new ApiErrorDto(
                    room is { WhiteJoined: true, BlackJoined: true } ? "Room is full" : "Seat is already taken"));
            if (room.Manager.State == GameState.NotStarted)
                room.Manager.StartGame();

            roomService.NotifyLobby();

            return Ok(new RoomSessionDto(room.Id, room.Mode, color));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorDto("An error occurred while joining the room", ex.Message));
        }
    }

    [HttpGet("{roomId}")]
    public ActionResult<RoomStatusDto> Status(string roomId)
    {
        if (roomService.FindRoom(roomId) is not { } room)
            return NotFound(new ApiErrorDto("Room not found"));

        return Ok(RoomStatusDto.FromGameRoom(room));
    }

    #endregion

    #region Game state

    [HttpGet("{roomId}/game")]
    public ActionResult<GameStateDto> GetState(string roomId)
    {
        if (roomService.FindRoom(roomId) is not { } room)
            return RoomNotFound();
        return Ok(GameStateDto.FromGameRoom(room));
    }

    [HttpGet("{roomId}/game/board")]
    public ActionResult<IEnumerable<TileDto>> GetBoard(string roomId)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();

            return Ok(room.Manager.GetBoard());
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving the board");
        }
    }

    [HttpGet("{roomId}/game/legal-moves")]
    [Obsolete("Use GET legal-moves/all instead.")]
    public ActionResult<IEnumerable<Move>> GetLegalMoves(string roomId, [FromQuery] int column, [FromQuery] int row)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            var from = new Position(row, column);
            if (!room.Manager.IsInBounds(from))
                return BadRequest(new ApiErrorDto("Position out of bounds"));
            return Ok(room.Manager.GetAllLegalMoves().Where(m => m.From.Equals(from)));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving legal moves");
        }
    }

    [HttpGet("{roomId}/game/legal-moves/all")]
    public ActionResult<LegalMovesDto> GetAllLegalMoves(string roomId)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            return Ok(LegalMovesDto.FromGameRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving legal moves");
        }
    }

    [HttpGet("{roomId}/game/history")]
    public ActionResult<IEnumerable<MoveHistoryEntry>> GetHistory(string roomId, [FromQuery] PieceColor? color)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            return Ok(color.HasValue ? room.Manager.GetMoveHistory(color.Value) : room.Manager.MoveHistory);
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving history");
        }
    }

    #endregion

    #region Game actions

    [HttpPost("{roomId}/game/start")]
    public ActionResult<GameStateDto> Start(string roomId)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            room.Manager.StartGame();
            return Ok(GameStateDto.FromGameRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while starting the game");
        }
    }

    [HttpPost("{roomId}/game/reset")]
    public ActionResult<GameStateDto> Reset(string roomId)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            room.Manager.ResetGame();
            return Ok(GameStateDto.FromGameRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while resetting the game");
        }
    }

    [HttpPost("{roomId}/game/moves")]
    public ActionResult<GameStateDto> MakeMove(string roomId, [FromBody] Move move, [FromQuery] PieceColor? color)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            if (room.Mode == RoomMode.Online)
            {
                if (color is null || !room.IsJoined(color.Value))
                    return NotAPlayer();
                if (color.Value != room.Manager.CurrentPlayer.Color)
                    return StatusCode(403, new ApiErrorDto("Not your turn"));
            }

            if (!room.Manager.TryMakeMove(move))
                return BadRequest(new ApiErrorDto("Illegal move"));
            return Ok(GameStateDto.FromGameRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while making the move");
        }
    }

    [HttpPost("{roomId}/game/resign")]
    public ActionResult<GameStateDto> Resign(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request) is not { } player)
                return NotAPlayer();
            room.Manager.Resign(player);
            return Ok(GameStateDto.FromGameRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while resigning");
        }
    }

    [HttpPost("{roomId}/game/draw/offer")]
    public ActionResult<GameStateDto> OfferDraw(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request) is not { } player)
                return NotAPlayer();
            room.Manager.OfferDraw(player);
            return Ok(GameStateDto.FromGameRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while offering a draw");
        }
    }

    [HttpPost("{roomId}/game/draw/respond")]
    public ActionResult<GameStateDto> RespondToDraw(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request) is not { } player)
                return NotAPlayer();
            room.Manager.RespondToDrawOffer(player, request.Accepted ?? false);
            return Ok(GameStateDto.FromGameRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while responding to the draw offer");
        }
    }

    [HttpPost("{roomId}/game/draw/claim-threefold")]
    public ActionResult<GameStateDto> ClaimThreefold(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request) is not { } player)
                return NotAPlayer();
            if (!room.Manager.ClaimDrawByThreefoldRepetition(player))
                return BadRequest(new ApiErrorDto("Threefold repetition claim not available"));
            return Ok(GameStateDto.FromGameRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while claiming the draw");
        }
    }

    [HttpPost("{roomId}/game/draw/claim-fifty")]
    public ActionResult<GameStateDto> ClaimFifty(string roomId, [FromBody] PlayerActionRequest request)
    {
        try
        {
            if (roomService.FindRoom(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request) is not { } player)
                return NotAPlayer();
            if (!room.Manager.ClaimDrawByFiftyMoveRule(player))
                return BadRequest(new ApiErrorDto("Fifty-move claim not available"));
            return Ok(GameStateDto.FromGameRoom(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while claiming the draw");
        }
    }

    #endregion

    #region Helpers

    private static IPlayer? ResolvePlayer(GameRoom room, PlayerActionRequest request)
    {
        if (room.Mode == RoomMode.Online && !room.IsJoined(request.Color))
            return null;
        return new Player(request.Name ?? request.Color.ToString(), request.Color);
    }

    private ObjectResult RoomNotFound()
    {
        return NotFound(new ApiErrorDto("Room not found"));
    }

    private ObjectResult NotAPlayer()
    {
        return StatusCode(403, new ApiErrorDto("You are not a player in this room"));
    }

    private ObjectResult Error(Exception ex, string message)
    {
        return StatusCode(500, new ApiErrorDto(message, ex.Message));
    }

    #endregion
}
