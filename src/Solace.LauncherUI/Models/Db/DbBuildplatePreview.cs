using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Solace.LauncherUI.Models.Db;

public class DbBuildplatePreview
{
    public int Id { get; set; }

    public Guid? PlayerId { get; set; }

    public required Guid BuildplateId { get; set; }

    public required byte[] PreviewData { get; set; }
}