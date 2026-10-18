using Api.DTOs;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("rooms")]
public class RoomsController(RoomRegistry rooms) : ControllerBase
{
    [HttpPost]
    public ActionResult<RoomSessionDto> Create([FromBody] CreateRoomRequest request)
    {
        try
        {
            var room = rooms.Create(request.Mode);
            if (room.Mode == RoomMode.Hotseat)
                room.Manager.StartGame();
            var token = room.Mode == RoomMode.Online ? room.ClaimWhite() : null;
            return Ok(new RoomSessionDto(room.Id, room.Mode, token is null ? null : PieceColor.White, token));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorDto("An error occurred while creating the room", ex.Message));
        }
    }

    [HttpPost("{roomId}/join")]
    public ActionResult<RoomSessionDto> Join(string roomId)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return NotFound(new ApiErrorDto("Room not found"));
            if (room.Mode != RoomMode.Online)
                return BadRequest(new ApiErrorDto("Only online rooms can be joined"));
            var token = room.ClaimBlack();
            if (token is null)
                return Conflict(new ApiErrorDto("Room is full"));
            if (room.Manager.State == GameState.NotStarted)
                room.Manager.StartGame();
            rooms.Notify();
            return Ok(new RoomSessionDto(room.Id, room.Mode, PieceColor.Black, token));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiErrorDto("An error occurred while joining the room", ex.Message));
        }
    }

    [HttpGet("{roomId}")]
    public ActionResult<RoomStatusDto> Status(string roomId)
    {
        if (rooms.Find(roomId) is not { } room)
            return NotFound(new ApiErrorDto("Room not found"));
        return Ok(new RoomStatusDto(room.Id, room.Mode, room.WhiteJoined, room.BlackJoined));
    }
}
