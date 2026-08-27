namespace Solace.Db.Migrator.Old.Earth;

public interface IVersionedEntity
{
    int Version { get; set; }
}