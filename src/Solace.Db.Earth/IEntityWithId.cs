namespace Solace.Db.Earth;

public interface IEntityWithId<TId>
    where TId : notnull
{
    TId Id { get; set; }
}