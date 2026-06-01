namespace Solace.DB.Models.Player;

public sealed class Boosts
{
    public ActiveBoost?[] ActiveBoosts { get; init; }
    public ActiveMiniFig?[] ActiveMiniFigs { get; init; }
    public Dictionary<string, MiniFigRecord> MiniFigRecords { get; init; }

    public Boosts()
    {
        ActiveBoosts = new ActiveBoost[5];
        ActiveMiniFigs = new ActiveMiniFig[5];
        MiniFigRecords = [];
    }

    public ActiveBoost? Get(string instanceId)
        => ActiveBoosts.FirstOrDefault(activeBoost => activeBoost is not null && activeBoost.InstanceId == instanceId);

    public ActiveMiniFig? GetMiniFig(string instanceId)
        => ActiveMiniFigs.FirstOrDefault(activeMiniFig => activeMiniFig is not null && activeMiniFig.InstanceId == instanceId);

    public ActiveBoost[] Prune(long currentTime)
    {
        LinkedList<ActiveBoost> prunedBoosts = [];
        for (int index = 0; index < ActiveBoosts.Length; index++)
        {
            ActiveBoost? activeBoost = ActiveBoosts[index];
            if (activeBoost is not null && activeBoost.StartTime + activeBoost.Duration < currentTime)
            {
                ActiveBoosts[index] = null;
                prunedBoosts.AddLast(activeBoost);
            }
        }

        return [.. prunedBoosts];
    }

    public ActiveMiniFig[] PruneMiniFigs(long currentTime)
    {
        LinkedList<ActiveMiniFig> prunedMiniFigs = [];
        for (int index = 0; index < ActiveMiniFigs.Length; index++)
        {
            ActiveMiniFig? activeMiniFig = ActiveMiniFigs[index];
            if (activeMiniFig is not null && activeMiniFig.StartTime + activeMiniFig.Duration < currentTime)
            {
                ActiveMiniFigs[index] = null;
                prunedMiniFigs.AddLast(activeMiniFig);
            }
        }

        return [.. prunedMiniFigs];
    }

    public sealed record ActiveBoost(
        string InstanceId,
        string ItemId,
        long StartTime,
        long Duration
    );

    public sealed record ActiveMiniFig(
        string InstanceId,
        string ProductId,
        string TagId,
        long StartTime,
        long Duration
    );

    public sealed record MiniFigRecord(
        string ProductId,
        string TagId,
        long LastSeen,
        int Activations
    );
}
