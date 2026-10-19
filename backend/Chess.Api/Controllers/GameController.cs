using Api.DTOs;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("rooms/{roomId}/game")]
public class GameController(RoomRegistry rooms) : ControllerBase
{
    [HttpGet]
    public ActionResult<GameStateDto> GetState(string roomId)
    {
        if (rooms.Find(roomId) is not { } room)
            return RoomNotFound();
        return Ok(State(room));
    }

    [HttpPost("start")]
    public ActionResult<GameStateDto> Start(string roomId)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            room.Manager.StartGame();
            rooms.Notify();
            return Ok(State(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while starting the game");
        }
    }

    [HttpPost("reset")]
    public ActionResult<GameStateDto> Reset(string roomId)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            room.Manager.ResetGame();
            rooms.Notify();
            return Ok(State(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while resetting the game");
        }
    }

    [HttpGet("board")]
    public ActionResult<IEnumerable<TileDto>> GetBoard(string roomId)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            var tiles = new List<TileDto>();
            for (var c = 0; c < 8; c++)
                for (var r = 0; r < 8; r++)
                {
                    var p = room.Manager.GameBoard.Tiles[c, r].Piece;
                    tiles.Add(new TileDto(c, r, p?.Color, p?.Type));
                }
            return Ok(tiles);
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving the board");
        }
    }

    [HttpGet("legal-moves")]
    public ActionResult<IEnumerable<Move>> GetLegalMoves(string roomId, [FromQuery] int column, [FromQuery] int row)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            var from = new Position(column, row);
            if (!room.Manager.IsInBounds(from))
                return BadRequest(new ApiErrorDto("Position out of bounds"));
            return Ok(room.Manager.GetLegalMoves(from));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving legal moves");
        }
    }

    [HttpPost("moves")]
    public ActionResult<GameStateDto> MakeMove(string roomId, [FromBody] Move move, [FromQuery] string? token)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            if (room.Mode == RoomMode.Online)
            {
                var color = room.ColorFor(token);
                if (color is null)
                    return NotAPlayer();
                if (color != room.Manager.CurrentPlayer.Color)
                    return StatusCode(403, new ApiErrorDto("Not your turn"));
            }
            if (!room.Manager.TryMakeMove(move))
                return BadRequest(new ApiErrorDto("Illegal move"));
            return Ok(State(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while making the move");
        }
    }

    [HttpGet("history")]
    public ActionResult<IEnumerable<MoveHistoryEntry>> GetHistory(string roomId, [FromQuery] PieceColor? color)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            return Ok(color.HasValue ? room.Manager.GetMoveHistory(color.Value) : room.Manager.MoveHistory);
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving history");
        }
    }

    [HttpPost("resign")]
    public ActionResult<GameStateDto> Resign(string roomId, [FromBody] PlayerActionRequest request, [FromQuery] string? token)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request, token) is not { } player)
                return NotAPlayer();
            room.Manager.Resign(player);
            return Ok(State(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while resigning");
        }
    }

    [HttpPost("draw/offer")]
    public ActionResult<GameStateDto> OfferDraw(string roomId, [FromBody] PlayerActionRequest request, [FromQuery] string? token)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request, token) is not { } player)
                return NotAPlayer();
            room.Manager.OfferDraw(player);
            return Ok(State(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while offering a draw");
        }
    }

    [HttpPost("draw/respond")]
    public ActionResult<GameStateDto> RespondToDraw(string roomId, [FromBody] PlayerActionRequest request, [FromQuery] string? token)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request, token) is not { } player)
                return NotAPlayer();
            room.Manager.RespondToDrawOffer(player, request.Accepted ?? false);
            return Ok(State(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while responding to the draw offer");
        }
    }

    [HttpPost("draw/claim-threefold")]
    public ActionResult<GameStateDto> ClaimThreefold(string roomId, [FromBody] PlayerActionRequest request, [FromQuery] string? token)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request, token) is not { } player)
                return NotAPlayer();
            if (!room.Manager.ClaimDrawByThreefoldRepetition(player))
                return BadRequest(new ApiErrorDto("Threefold repetition claim not available"));
            return Ok(State(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while claiming the draw");
        }
    }

    [HttpPost("draw/claim-fifty")]
    public ActionResult<GameStateDto> ClaimFifty(string roomId, [FromBody] PlayerActionRequest request, [FromQuery] string? token)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return RoomNotFound();
            if (ResolvePlayer(room, request, token) is not { } player)
                return NotAPlayer();
            if (!room.Manager.ClaimDrawByFiftyMoveRule(player))
                return BadRequest(new ApiErrorDto("Fifty-move claim not available"));
            return Ok(State(room));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while claiming the draw");
        }
    }

    private GameStateDto State(GameRoom room)
    {
        var manager = room.Manager;
        return new GameStateDto(
            manager.State,
            new PlayerDto(manager.CurrentPlayer.Name, manager.CurrentPlayer.Color),
            manager.Result,
            manager.CastlingRights,
            manager.EnPassantTarget,
            manager.MoveHistory.Count,
            RemainingSeconds(manager, PieceColor.White),
            RemainingSeconds(manager, PieceColor.Black));
    }

    private static int RemainingSeconds(GameManager manager, PieceColor color)
    {
        return (int)manager.PlayerTimeTracker[color].RemainingTime.TotalSeconds;
    }

    private IPlayer? ResolvePlayer(GameRoom room, PlayerActionRequest request, string? token)
    {
        if (room.Mode != RoomMode.Online)
            return new Player(request.Name ?? request.Color.ToString(), request.Color);
        return room.ColorFor(token) is { } color ? new Player(color.ToString(), color) : null;
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
}
