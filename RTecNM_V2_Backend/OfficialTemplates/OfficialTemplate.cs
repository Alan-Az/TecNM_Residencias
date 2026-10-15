using TecNM.Residency.Common;

namespace TecNM.Residency.OfficialTemplates;

public class OfficialTemplate : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
    public int CurrentVersion { get; set; } = 1;
    public string CurrentFileName { get; set; } = string.Empty;
    public string CurrentFilePath { get; set; } = string.Empty;
    public long CurrentFileSize { get; set; }
    public string CurrentContentType { get; set; } = "application/pdf";

    public ICollection<OfficialTemplateVersion> Versions { get; set; } = new List<OfficialTemplateVersion>();
}
