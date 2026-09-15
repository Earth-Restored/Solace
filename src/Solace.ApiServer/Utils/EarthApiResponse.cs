namespace Solace.ApiServer.Utils;

internal sealed class EarthApiResponse
{
    public static EarthApiResponse Default { get; } = new(null);

    public IReadOnlyDictionary<string, int?>? Updates { get; init; }

    public EarthApiResponse(EarthUpdatesResponse? updates)
    {
        Updates = updates?.Map.ToDictionary();
    }
}

internal sealed class EarthApiResponse<TResult>
{
    public TResult? Result { get; init; }
    public IReadOnlyDictionary<string, int?>? Updates { get; init; }

    public EarthApiResponse(TResult results)
    {
        Result = results;
    }

    public EarthApiResponse(TResult? results, EarthUpdatesResponse? updates)
    {
        Result = results;
        Updates = updates?.Map.ToDictionary();
    }
}
