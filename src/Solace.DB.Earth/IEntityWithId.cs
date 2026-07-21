namespace Solace.DB.Earth;

public interface IEntityWithId<TId>
    where TId : notnull
{
    TId Id { get; set; }
}