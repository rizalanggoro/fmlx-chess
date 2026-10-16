namespace Api.Models;

public interface IPlayerTimer
{
    TimeSpan RemainingTime { get; }
    event EventHandler<TimerTickEventArgs> TimerTick;
    event EventHandler TimeExpired;
    void StartTimer();
    void StopTimer();
}
