namespace Api.Models;

public interface IBoard
{
    IChessTile[,] Tiles { get; }
}
