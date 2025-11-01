namespace DicomGeneratorPro;

public sealed class RandomProvider
{
    public Random Rng { get; }
    public RandomProvider(int? seed) { Rng = seed.HasValue ? new Random(seed.Value) : new Random(); }
}