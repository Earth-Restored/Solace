namespace Solace.AdminPanel.Models.Db;

internal sealed class DbBuildplatePreview
{
    public int Id { get; set; }

    public Guid? PlayerId { get; set; }

    public required Guid BuildplateId { get; set; }

    public required byte[] PreviewData { get; set; }
}