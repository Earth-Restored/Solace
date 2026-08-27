namespace Solace.Db.Migrator.Old.Earth;

public interface IEntityWithId<TId>
    where TId : notnull
{
    TId Id { get; set; }
}