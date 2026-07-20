namespace Solace.Common.Utils;

public static class RandomExtensions
{
    extension(Random random)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Extension for random")]
        public float NextSingle(float min, float max)
        {
            if (min >= max)
            {
                throw new ArgumentOutOfRangeException(nameof(min), "Minimum value must be less than maximum value.");
            }

            var range = max - min;
            var sample = random.NextSingle() * range;
            return sample + min;
        }
    }
}
