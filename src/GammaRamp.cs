namespace GammaControl;

internal sealed class GammaRamp
{
    internal const int ChannelLength = 256;
    internal const int TotalLength = ChannelLength * 3;

    internal GammaRamp(ushort[] values)
    {
        if (values == null || values.Length != TotalLength)
        {
            throw new ArgumentException("A gamma ramp must contain 768 values.", nameof(values));
        }

        Values = (ushort[])values.Clone();
    }

    internal ushort[] Values { get; }

    internal GammaRamp Clone()
    {
        return new GammaRamp(Values);
    }

    internal int MaxDifference(GammaRamp other)
    {
        var maximum = 0;
        for (var index = 0; index < TotalLength; index++)
        {
            var difference = Math.Abs((int)Values[index] - other.Values[index]);
            if (difference > maximum)
            {
                maximum = difference;
            }
        }

        return maximum;
    }
}

internal static class GammaRampBuilder
{
    internal const double MinimumGamma = 0.20;
    internal const double MaximumGamma = 5.00;
    internal const int MaximumIdentityDeviation = 32768;

    internal static double Validate(double gamma)
    {
        if (double.IsNaN(gamma) || double.IsInfinity(gamma) ||
            gamma < MinimumGamma || gamma > MaximumGamma)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gamma),
                gamma,
                $"Gamma must be between {MinimumGamma:F2} and {MaximumGamma:F2}.");
        }

        return gamma;
    }

    internal static GammaRamp Build(double gamma)
    {
        Validate(gamma);
        var values = new ushort[GammaRamp.TotalLength];

        for (var index = 0; index < GammaRamp.ChannelLength; index++)
        {
            var normalized = index / 255.0;
            var corrected = Math.Pow(normalized, 1.0 / gamma);
            var rawValue = Math.Clamp(
                (int)Math.Round(corrected * ushort.MaxValue, MidpointRounding.AwayFromZero),
                ushort.MinValue,
                ushort.MaxValue);
            var identityValue = index * 257;
            var value = (ushort)Math.Clamp(
                rawValue,
                Math.Max(ushort.MinValue, identityValue - MaximumIdentityDeviation),
                Math.Min(ushort.MaxValue, identityValue + MaximumIdentityDeviation));

            values[index] = value;
            values[index + GammaRamp.ChannelLength] = value;
            values[index + (GammaRamp.ChannelLength * 2)] = value;
        }

        values[0] = 0;
        values[GammaRamp.ChannelLength] = 0;
        values[GammaRamp.ChannelLength * 2] = 0;
        values[GammaRamp.ChannelLength - 1] = ushort.MaxValue;
        values[(GammaRamp.ChannelLength * 2) - 1] = ushort.MaxValue;
        values[GammaRamp.TotalLength - 1] = ushort.MaxValue;
        return new GammaRamp(values);
    }

    internal static int ToSliderPosition(double gamma, int maximum = 1000)
    {
        Validate(gamma);
        if (maximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        var minimumLog = Math.Log(MinimumGamma);
        var maximumLog = Math.Log(MaximumGamma);
        var normalized = (Math.Log(gamma) - minimumLog) / (maximumLog - minimumLog);
        return Math.Clamp((int)Math.Round(normalized * maximum), 0, maximum);
    }

    internal static double FromSliderPosition(int position, int maximum = 1000)
    {
        if (maximum <= 0 || position < 0 || position > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        var minimumLog = Math.Log(MinimumGamma);
        var maximumLog = Math.Log(MaximumGamma);
        var normalized = position / (double)maximum;
        return Math.Exp(minimumLog + ((maximumLog - minimumLog) * normalized));
    }
}
