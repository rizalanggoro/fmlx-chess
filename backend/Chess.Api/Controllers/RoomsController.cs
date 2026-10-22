using Api.DTOs;
using Api.DTOs.Requests;
using Api.Enums;
using Api.Interfaces.Services;
using Api.Utils;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("rooms")]
public class RoomsController(IRoomRegistry rooms, IRoomService roomService) : ControllerBase
{
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
            if (rooms.Find(roomId) is not { } room)
                return NotFound(new ApiErrorDto("Room not found"));
            if (room.Mode != RoomMode.Online)
                return BadRequest(new ApiErrorDto("Only online rooms can be joined"));
            if (color is null)
                return BadRequest(new ApiErrorDto("Color is required"));
            if (!roomService.Claim(room, color.Value, NameUtils.Normalize(name)))
                return Conflict(new ApiErrorDto(
                    room.WhiteJoined && room.BlackJoined ? "Room is full" : "Seat is already taken"));
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
        if (rooms.Find(roomId) is not { } room)
            return NotFound(new ApiErrorDto("Room not found"));
        return Ok(RoomStatusDto.FromGameRoom(room));
    }
}