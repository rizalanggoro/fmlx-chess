using Api.DTOs;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("[controller]")]
public class GameController(GameManager games) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GameStateDto>> GetState()
    {
        await Task.CompletedTask;
        return Ok(State());
    }

    [HttpPost("start")]
    public async Task<ActionResult<GameStateDto>> Start()
    {
        await Task.CompletedTask;
        try
        {
            games.StartGame();
            return Ok(State());
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while starting the game");
        }
    }

    [HttpPost("reset")]
    public async Task<ActionResult<GameStateDto>> Reset()
    {
        await Task.CompletedTask;
        try
        {
            games.ResetGame();
            return Ok(State());
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while resetting the game");
        }
    }

    [HttpGet("board")]
    public async Task<ActionResult<IEnumerable<TileDto>>> GetBoard()
    {
        await Task.CompletedTask;
        try
        {
            var tiles = new List<TileDto>();
            for (var c = 0; c < 8; c++)
                for (var r = 0; r < 8; r++)
                {
                    var p = games.GameBoard.Tiles[c, r].Piece;
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
    public async Task<ActionResult<IEnumerable<Move>>> GetLegalMoves([FromQuery] int column, [FromQuery] int row)
    {
        await Task.CompletedTask;
        try
        {
            var from = new Position(column, row);
            if (!games.IsInBounds(from))
                return BadRequest(new ApiErrorDto("Position out of bounds"));
            return Ok(games.GetLegalMoves(from));
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while retrieving legal moves");
        }
    }

    [HttpPost("moves")]
    public async Task<ActionResult<GameStateDto>> MakeMove([FromBody] Move move)
    {
        await Task.CompletedTask;
        try
        {
            if (!games.TryMakeMove(move))
                return BadRequest(new ApiErrorDto("Illegal move"));
            return Ok(State());
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while making the move");
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
            return Error(ex, "An error occurred while retrieving history");
        }
    }

    [HttpPost("resign")]
    public async Task<ActionResult<GameStateDto>> Resign([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            games.Resign(ToPlayer(request));
            return Ok(State());
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while resigning");
        }
    }

    [HttpPost("draw/offer")]
    public async Task<ActionResult<GameStateDto>> OfferDraw([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            games.OfferDraw(ToPlayer(request));
            return Ok(State());
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while offering a draw");
        }
    }

    [HttpPost("draw/respond")]
    public async Task<ActionResult<GameStateDto>> RespondToDraw([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            games.RespondToDrawOffer(ToPlayer(request), request.Accepted ?? false);
            return Ok(State());
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while responding to the draw offer");
        }
    }

    [HttpPost("draw/claim-threefold")]
    public async Task<ActionResult<GameStateDto>> ClaimThreefold([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            if (!games.ClaimDrawByThreefoldRepetition(ToPlayer(request)))
                return BadRequest(new ApiErrorDto("Threefold repetition claim not available"));
            return Ok(State());
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while claiming the draw");
        }
    }

    [HttpPost("draw/claim-fifty")]
    public async Task<ActionResult<GameStateDto>> ClaimFifty([FromBody] PlayerActionRequest request)
    {
        await Task.CompletedTask;
        try
        {
            if (!games.ClaimDrawByFiftyMoveRule(ToPlayer(request)))
                return BadRequest(new ApiErrorDto("Fifty-move claim not available"));
            return Ok(State());
        }
        catch (Exception ex)
        {
            return Error(ex, "An error occurred while claiming the draw");
        }
    }

    private GameStateDto State()
    {
        return new GameStateDto(
            games.State,
            new PlayerDto(games.CurrentPlayer.Name, games.CurrentPlayer.Color),
            games.Result,
            games.CastlingRights,
            games.EnPassantTarget,
            games.MoveHistory.Count);
    }

    private ObjectResult Error(Exception ex, string message)
    {
        return StatusCode(500, new ApiErrorDto(message, ex.Message));
    }

    private static Player ToPlayer(PlayerActionRequest request)
    {
        return new Player(request.Name ?? request.Color.ToString(), request.Color);
    }
}
