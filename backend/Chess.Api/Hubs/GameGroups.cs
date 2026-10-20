namespace Api.Hubs;

public static class GameGroups
{
    public const string Lobby = "lobby";

    public static string Room(string roomId) => $"room:{roomId}";
}
