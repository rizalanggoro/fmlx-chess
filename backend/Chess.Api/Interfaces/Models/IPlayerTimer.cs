namespace Api.Interfaces.Models;

/// <summary>
///     Model for one player's clock. Holds data only: the countdown logic lives
///     in GameManager.
/// </summary>
public interface IPlayerTimer
{
    TimeSpan RemainingTime { get; set; }
}
