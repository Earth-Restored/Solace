namespace Solace.WebPortal.Utils;

internal static class LongIdGenerator
{
    private static readonly long EpochTicks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).UtcTicks;

    private const int RandomBits = 8;
    private const int SequenceBits = 13;
    private const int TimeBits = 64 - 1 - RandomBits - SequenceBits;
    private const int MaxRandomValue = 1 << RandomBits;
    private const ulong SequenceMask = (1ul << SequenceBits) - 1ul;
    private const ulong TimeMask = (1ul << TimeBits) - 1ul;

    private static readonly Lock Lock = new();

    private static ulong _lastTimestamp;
    private static ulong _sequence;

    public static long NextId()
    {
        lock (Lock)
        {
            var currentTimestamp = GetCurrentTimestamp();

            if (currentTimestamp < _lastTimestamp)
            {
                while (currentTimestamp < _lastTimestamp)
                {
                    Thread.SpinWait(10);
                    currentTimestamp = GetCurrentTimestamp();
                }
            }

            if (currentTimestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & SequenceMask;

                if (_sequence == 0)
                {
                    while (currentTimestamp <= _lastTimestamp)
                    {
                        Thread.SpinWait(10);
                        currentTimestamp = GetCurrentTimestamp();
                    }

                    _lastTimestamp = currentTimestamp;
                }
            }
            else
            {
                _sequence = 0;
                _lastTimestamp = currentTimestamp;
            }

            var random = (ulong)Random.Shared.Next(0, MaxRandomValue);

            return unchecked((long)(((currentTimestamp & TimeMask) << (RandomBits + SequenceBits)) | (_sequence << RandomBits) | random));
        }
    }

    private static ulong GetCurrentTimestamp()
        => unchecked((ulong)((DateTimeOffset.UtcNow.UtcTicks - EpochTicks) / TimeSpan.TicksPerMillisecond));
}
