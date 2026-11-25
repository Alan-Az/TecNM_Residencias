namespace TecNM.Residency.Documents;

public class MissingDocumentItemDto
{
    public string DocumentType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Prerequisite { get; set; }
    public string? StatusNote { get; set; }
}

public class UploadedDocumentSummaryDto
{
    public long Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class MissingDocumentsChecklistDto
{
    public long ProjectId { get; set; }
    public long StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentControlNumber { get; set; } = string.Empty;
    public long CareerId { get; set; }
    public string CareerName { get; set; } = string.Empty;
    public long? AdvisorId { get; set; }
    public string? AdvisorName { get; set; }
    public bool IsAccreditation { get; set; }
    public string ProjectType { get; set; } = string.Empty;
    public int TotalRequired { get; set; }
    public int TotalUploaded { get; set; }
    public int TotalMissing { get; set; }
    public bool IsComplete { get; set; }
    public List<MissingDocumentItemDto> MissingDocuments { get; set; } = new();
    public List<UploadedDocumentSummaryDto> UploadedDocuments { get; set; } = new();
}
