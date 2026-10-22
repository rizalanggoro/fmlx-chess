using Api.Interfaces.Models;

namespace Api.Models;

/// <summary>
///     Data-only model for one player's clock. It carries the remaining time;
///     starting, stopping, and ticking the clock are handled by GameManager.
/// </summary>
public class PlayerTimer(TimeSpan initialTime) : IPlayerTimer
{
    public TimeSpan RemainingTime { get; set; } = initialTime;
}
