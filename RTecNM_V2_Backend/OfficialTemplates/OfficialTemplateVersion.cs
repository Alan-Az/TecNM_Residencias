using TecNM.Residency.Common;

namespace TecNM.Residency.OfficialTemplates;

public class OfficialTemplateVersion : BaseEntity
{
    public long OfficialTemplateId { get; set; }
    public int VersionNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public string? ChangeNotes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public long? UploadedBy { get; set; }

    public OfficialTemplate? OfficialTemplate { get; set; }
}
