using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    private readonly GameManager _games;

    public GameController(GameManager games)
    {
        _games = games;
    }

    [HttpGet]
    public ActionResult<object> GetState()
    {
        return Ok(State());
    }

    [HttpPost("start")]
    public ActionResult<object> Start()
    {
        try
        {
            _games.StartGame();
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while starting the game", error = ex.Message });
        }
    }

    [HttpPost("reset")]
    public ActionResult<object> Reset()
    {
        try
        {
            _games.ResetGame();
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while resetting the game", error = ex.Message });
        }
    }

    [HttpGet("board")]
    public ActionResult<object> GetBoard()
    {
        try
        {
            var tiles = new List<object>();
            for (var c = 0; c < 8; c++)
                for (var r = 0; r < 8; r++)
                {
                    var p = _games.GameBoard.Tiles[c, r].Piece;
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
    public ActionResult<IEnumerable<Move>> GetLegalMoves([FromQuery] int column, [FromQuery] int row)
    {
        try
        {
            var from = new Position(column, row);
            if (!_games.IsInBounds(from))
                return BadRequest(new { message = "Position out of bounds" });
            return Ok(_games.GetLegalMoves(from));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving legal moves", error = ex.Message });
        }
    }

    [HttpPost("moves")]
    public ActionResult<object> MakeMove([FromBody] Move move)
    {
        try
        {
            if (!_games.TryMakeMove(move))
                return BadRequest(new { message = "Illegal move" });
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while making the move", error = ex.Message });
        }
    }

    [HttpGet("history")]
    public ActionResult<IEnumerable<MoveHistoryEntry>> GetHistory([FromQuery] PieceColor? color)
    {
        try
        {
            return Ok(color.HasValue ? _games.GetMoveHistory(color.Value) : _games.MoveHistory);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving history", error = ex.Message });
        }
    }

    [HttpPost("resign")]
    public ActionResult<object> Resign([FromBody] PlayerActionRequest request)
    {
        try
        {
            _games.Resign(ToPlayer(request));
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while resigning", error = ex.Message });
        }
    }

    [HttpPost("draw/offer")]
    public ActionResult<object> OfferDraw([FromBody] PlayerActionRequest request)
    {
        try
        {
            _games.OfferDraw(ToPlayer(request));
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while offering a draw", error = ex.Message });
        }
    }

    [HttpPost("draw/respond")]
    public ActionResult<object> RespondToDraw([FromBody] PlayerActionRequest request)
    {
        try
        {
            _games.RespondToDrawOffer(ToPlayer(request), request.Accepted);
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while responding to the draw offer", error = ex.Message });
        }
    }

    [HttpPost("draw/claim-threefold")]
    public ActionResult<object> ClaimThreefold([FromBody] PlayerActionRequest request)
    {
        try
        {
            if (!_games.ClaimDrawByThreefoldRepetition(ToPlayer(request)))
                return BadRequest(new { message = "Threefold repetition claim not available" });
            return Ok(State());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while claiming the draw", error = ex.Message });
        }
    }

    [HttpPost("draw/claim-fifty")]
    public ActionResult<object> ClaimFifty([FromBody] PlayerActionRequest request)
    {
        try
        {
            if (!_games.ClaimDrawByFiftyMoveRule(ToPlayer(request)))
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
            state = _games.State,
            currentPlayer = _games.CurrentPlayer,
            result = _games.Result,
            castlingRights = _games.CastlingRights,
            enPassantTarget = _games.EnPassantTarget,
            moveCount = _games.MoveHistory.Count
        };
    }

    private static Player ToPlayer(PlayerActionRequest request)
    {
        return new Player(request.Name ?? request.Color.ToString(), request.Color);
    }
}

public record PlayerActionRequest(PieceColor Color, string? Name = null, bool Accepted = false);
