using System;

namespace KwekDrop;

internal static class RangePlayback
{
    public const double MinSelection = 0.1, EndTolerance = 0.035, RestartMargin = 0.05;

    public static bool ShouldStop(double now, double end) => end >= 0 && now >= end - EndTolerance;

    public static double StartFor(double current, double start, double end) =>
        current < start || current >= end - RestartMargin ? start : current;

    public static (double Start, double End) Clamp(double start, double end, double duration)
    {
        if (duration < MinSelection)
            return (0, Math.Max(0, duration));

        double maxStart = Math.Max(0, duration - MinSelection);
        start = Math.Clamp(start, 0, maxStart);

        if (end <= 0)
            end = duration;

        double minEnd = Math.Min(duration, start + MinSelection);
        end = Math.Clamp(end, minEnd, duration);

        return (start, end);
    }

    public static bool Contains(double now, double start, double end) =>
        now >= start - 0.001 && now < end;
}
