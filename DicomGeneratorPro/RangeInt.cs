using System.Text.Json.Serialization;

namespace DicomGeneratorPro;

/// <summary>Inclusive integer range (Min..Max).</summary>
public sealed class RangeInt
{
    [JsonPropertyName("Min")] public int Min { get; set; }
    [JsonPropertyName("Max")] public int Max { get; set; }

    public RangeInt() {}
    public RangeInt(int min, int max) { if (max < min) throw new ArgumentException("Max must be >= Min"); Min=min; Max=max; }
    public int Sample(Random rnd) => rnd.Next(Min, Max + 1);
}