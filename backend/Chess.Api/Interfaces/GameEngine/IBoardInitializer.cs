using Api.Interfaces.Models;

namespace Api.Interfaces.GameEngine;

public interface IBoardInitializer
{
    void Initialize(IBoard board);
}
