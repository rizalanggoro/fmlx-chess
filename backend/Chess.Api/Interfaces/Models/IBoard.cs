namespace Api.Interfaces.Models;

public interface IBoard
{
    /// <summary>
    /// Board tiles indexed as [row, column].
    /// </summary>
    IChessTile[,] Tiles { get; }
}
