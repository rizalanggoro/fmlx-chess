namespace Api.Interfaces.Models;

public interface IBoard
{
    IChessTile[,] Tiles { get; }
}
