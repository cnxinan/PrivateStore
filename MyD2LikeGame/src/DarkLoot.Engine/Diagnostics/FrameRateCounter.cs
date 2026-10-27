namespace DarkLoot.Engine.Diagnostics;

public sealed class FrameRateCounter
{
    private TimeSpan _sampleStart;
    private TimeSpan _lastDraw;
    private int _frames;

    public double FramesPerSecond { get; private set; }

    public void RecordDraw(TimeSpan elapsed)
    {
        if (elapsed < _lastDraw)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Draw timestamps must be nondecreasing.");
        }

        _lastDraw = elapsed;
        _frames++;
        var interval = elapsed - _sampleStart;
        if (interval.TotalSeconds >= .5)
        {
            FramesPerSecond = _frames / interval.TotalSeconds;
            _sampleStart = elapsed;
            _frames = 0;
        }
    }
}
