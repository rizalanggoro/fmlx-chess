using Api.Models;

namespace Api.DTOs;

public record RoomStatusDto(string Id, RoomMode Mode, bool WhiteJoined, bool BlackJoined, string? WhiteName = null, string? BlackName = null);
