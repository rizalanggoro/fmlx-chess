using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController(GameManager games) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> GetState()
    {
        await Task.CompletedTask;
        return Ok(State());
    }

    [HttpPost("start")]
    public async Task<ActionResult<object>> Start()
    {
        await Task.CompletedTask;
        try
        {
            games.StartGame();
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while starting the game", error = ex.Message });
        }
    }

    [HttpPost("reset")]
    public async Task<ActionResult<object>> Reset()
    {
        await Task.CompletedTask;
        try
        {
            games.ResetGame();
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while resetting the game", error = ex.Message });
        }
    }

    [HttpGet("board")]
    public async Task<ActionResult<object>> GetBoard()
    {
        await Task.CompletedTask;
        try
        {
            var tiles = new List<object>();
            for (var c = 0; c < 8; c++)
                for (var r = 0; r < 8; r++)
                {
                    var p = games.GameBoard.Tiles[c, r].Piece;
                    tiles.Add(new { column = c, row = r, color = (PieceColor?)p?.Color, type = (PieceType?)p?.Type });
                }
            return Ok(tiles);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the board", error = ex.Message });
        }
    }

    [HttpGet("legal-moves")]
    public async Task<ActionResult<IEnumerable<Move>>> GetLegalMoves([FromQuery] int column, [FromQuery] int row)
    {
        await Task.CompletedTask;
        try
        {
            var from = new Position(column, row);
            if (!games.IsInBounds(from))
                return BadRequest(new { message = "Position out of bounds" });
            return Ok(games.GetLegalMoves(from));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving legal moves", error = ex.Message });
        }
    }

    [HttpPost("moves")]
    public async Task<ActionResult<object>> MakeMove([FromBody] Move move)
    {
        await Task.CompletedTask;
        try
        {
            if (!games.TryMakeMove(move))
                return BadRequest(new { message = "Illegal move" });
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while making the move", error = ex.Message });
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<MoveHistoryEntry>>> GetHistory([FromQuery] PieceColor? color)
    {
        await Task.CompletedTask;
        try
        {
            return Ok(color.HasValue ? games.GetMoveHistory(color.Value) : games.MoveHistory);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving history", error = ex.Message });
        }
    }

    [HttpPost("resign")]
    public async Task<ActionResult<object>> Resign([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            games.Resign(ToPlayer(request));
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while resigning", error = ex.Message });
        }
    }

    [HttpPost("draw/offer")]
    public async Task<ActionResult<object>> OfferDraw([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            games.OfferDraw(ToPlayer(request));
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while offering a draw", error = ex.Message });
        }
    }

    [HttpPost("draw/respond")]
    public async Task<ActionResult<object>> RespondToDraw([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            games.RespondToDrawOffer(ToPlayer(request), request.Accepted);
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while responding to the draw offer", error = ex.Message });
        }
    }

    [HttpPost("draw/claim-threefold")]
    public async Task<ActionResult<object>> ClaimThreefold([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            if (!games.ClaimDrawByThreefoldRepetition(ToPlayer(request)))
                return BadRequest(new { message = "Threefold repetition claim not available" });
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while claiming the draw", error = ex.Message });
        }
    }

    [HttpPost("draw/claim-fifty")]
    public async Task<ActionResult<object>> ClaimFifty([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            if (!games.ClaimDrawByFiftyMoveRule(ToPlayer(request)))
                return BadRequest(new { message = "Fifty-move claim not available" });
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while claiming the draw", error = ex.Message });
        }
    }

    private object State()
    {
        return new
        {
            state = games.State,
            currentPlayer = games.CurrentPlayer,
            result = games.Result,
            castlingRights = games.CastlingRights,
            enPassantTarget = games.EnPassantTarget,
            moveCount = games.MoveHistory.Count
        };
    }

    private static Player ToPlayer(PlayerActionRequest request)
    {
        return new Player(request.Name ?? request.Color.ToString(), request.Color);
    }
}

public record PlayerActionRequest(PieceColor Color, string? Name = null, bool Accepted = false);
