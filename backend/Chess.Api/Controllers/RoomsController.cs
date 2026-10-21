using Api.DTOs;
using Api.DTOs.Requests;
using Api.Enums;
using Api.Interfaces.Services;
using Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("rooms")]
public class RoomsController(IRoomRegistry rooms) : ControllerBase
{
    [HttpPost]
    public ActionResult<RoomSessionDto> Create([FromBody] CreateRoomRequest request)
    {
        try
        {
            var room = rooms.Create(request.Mode);
            if (room.Mode == RoomMode.HotSeat)
            {
                room.Manager.StartGame();
                return Ok(new RoomSessionDto(room.Id, room.Mode, null));
            }

            var color = request.Color ?? PieceColor.White;
            room.Claim(color, NormalizeName(request.Name));
            rooms.NotifyLobby();
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
        return Ok(rooms.OpenRooms());
    }

    [HttpPost("{roomId}/join")]
    public ActionResult<RoomSessionDto> Join(string roomId, [FromQuery] string? name)
    {
        try
        {
            if (rooms.Find(roomId) is not { } room)
                return NotFound(new ApiErrorDto("Room not found"));
            if (room.Mode != RoomMode.Online)
                return BadRequest(new ApiErrorDto("Only online rooms can be joined"));
            if (room.ClaimFree(NormalizeName(name)) is not { } seatColor)
                return Conflict(new ApiErrorDto("Room is full"));
            if (room.Manager.State == GameState.NotStarted)
                room.Manager.StartGame();
            rooms.NotifyRoom(room.Id);
            rooms.NotifyLobby();
            return Ok(new RoomSessionDto(room.Id, room.Mode, seatColor));
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
        return Ok(RoomStatusDto.From(room));
    }

    private static string? NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var trimmed = name.Trim();
        return trimmed.Length > 24 ? trimmed[..24] : trimmed;
    }
}