using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TecNM.Residency.OfficialTemplates;

public class CreateOfficialTemplateDto
{
    [Required(ErrorMessage = "El código del formato es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no debe superar 50 caracteres.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del formato es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no debe superar 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(100, ErrorMessage = "La categoría no debe superar 100 caracteres.")]
    public string Category { get; set; } = "General";

    [Required(ErrorMessage = "El archivo en formato PDF o Word (DOCX/DOC) es obligatorio.")]
    public IFormFile File { get; set; } = null!;
}

public class UpdateOfficialTemplateDto
{
    [Required(ErrorMessage = "El código del formato es obligatorio.")]
    [StringLength(50, ErrorMessage = "El código no debe superar 50 caracteres.")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre del formato es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no debe superar 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(100, ErrorMessage = "La categoría no debe superar 100 caracteres.")]
    public string Category { get; set; } = "General";
}

public class NewOfficialTemplateVersionDto
{
    [Required(ErrorMessage = "El nuevo archivo en formato PDF o Word (DOCX/DOC) es obligatorio.")]
    public IFormFile File { get; set; } = null!;

    public string? ChangeNotes { get; set; }
}

public class OfficialTemplateResponseDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
    public int CurrentVersion { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public long CurrentFileSize { get; set; }
    public string CurrentContentType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int DisplayOrder { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public long? DeletedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int VersionsCount { get; set; }
}

public class OfficialTemplateVersionResponseDto
{
    public long Id { get; set; }
    public long OfficialTemplateId { get; set; }
    public int VersionNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? ChangeNotes { get; set; }
    public DateTime UploadedAt { get; set; }
    public long? UploadedBy { get; set; }
    public string? UploaderName { get; set; }
}
