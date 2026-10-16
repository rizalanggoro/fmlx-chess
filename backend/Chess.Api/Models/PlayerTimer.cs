namespace Api.Models;

public class PlayerTimer : IPlayerTimer
{
    public TimeSpan RemainingTime { get; private set; }

    private System.Threading.Timer? _countdownTimer;

    public event EventHandler<TimerTickEventArgs> TimerTick = delegate { };
    public event EventHandler TimeExpired = delegate { };

    public PlayerTimer(TimeSpan initialTime)
    {
        RemainingTime = initialTime;
    }

    public void StartTimer()
    {
        _countdownTimer?.Dispose();
        _countdownTimer = new System.Threading.Timer(_ => Tick(), null, 1000, 1000);
    }

    public void StopTimer()
    {
        _countdownTimer?.Dispose();
        _countdownTimer = null;
    }

    private void Tick()
    {
        RemainingTime -= TimeSpan.FromSeconds(1);
        if (RemainingTime <= TimeSpan.Zero)
        {
            RemainingTime = TimeSpan.Zero;
            StopTimer();
            TimeExpired.Invoke(this, EventArgs.Empty);
        }
        else
        {
            TimerTick.Invoke(this, new TimerTickEventArgs(PieceColor.White, RemainingTime));
        }
    }
}
