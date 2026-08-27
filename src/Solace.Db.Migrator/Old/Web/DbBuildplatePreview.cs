namespace Solace.Db.Migrator.Old.Web;

public class DbBuildplatePreview
{
    public int Id { get; set; }

    public Guid? PlayerId { get; set; }

    public required Guid BuildplateId { get; set; }

    public required byte[] PreviewData { get; set; }
}