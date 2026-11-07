using System;
using System.Collections.Generic;

namespace FamilyMoney.Analytics;

/// <summary>«Круглые» деления осей: 1, 2, 5 × 10ⁿ.</summary>
public static class NiceTicks
{
    public static decimal Step(decimal range, int targetSteps = 4)
    {
        if (range <= 0m)
        {
            return 1m;
        }

        var raw = (double)range / Math.Max(1, targetSteps);
        var magnitude = Math.Pow(10d, Math.Floor(Math.Log10(raw)));
        var normalized = raw / magnitude;
        var nice = normalized <= 1d ? 1d
            : normalized <= 2d ? 2d
            : normalized <= 2.5d ? 2.5d
            : normalized <= 5d ? 5d
            : 10d;

        return (decimal)(nice * magnitude);
    }

    public static decimal RoundDown(decimal value, decimal step)
    {
        return step <= 0m ? value : Math.Floor(value / step) * step;
    }

    public static decimal RoundUp(decimal value, decimal step)
    {
        return step <= 0m ? value : Math.Ceiling(value / step) * step;
    }

    public static IReadOnlyList<decimal> Build(decimal min, decimal max, int targetSteps = 4)
    {
        if (max <= min)
        {
            return [min];
        }

        var step = Step(max - min, targetSteps);
        var result = new List<decimal>();
        var value = RoundUp(min, step);

        // Допуск на накопленную погрешность decimal-сложения.
        var limit = max + (step * 0.001m);
        while (value <= limit && result.Count < 64)
        {
            result.Add(value);
            value += step;
        }

        return result;
    }
}
