using TecNM.Residency.Common;

namespace TecNM.Residency.OfficialTemplates;

public interface IOfficialTemplateService
{
    Task<Result<PaginatedResult<OfficialTemplateResponseDto>>> GetPagedAsync(
        PaginationQuery query,
        string? category = null,
        bool includeInactive = false);

    Task<Result<OfficialTemplateResponseDto>> GetByIdAsync(long id);

    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> DownloadCurrentAsync(long id);

    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> DownloadVersionAsync(long templateId, long versionId);

    Task<Result<List<OfficialTemplateVersionResponseDto>>> GetHistoryAsync(long templateId);

    Task<Result<OfficialTemplateResponseDto>> CreateAsync(CreateOfficialTemplateDto dto, string uploadsRootPath);

    Task<Result<OfficialTemplateResponseDto>> UpdateAsync(long id, UpdateOfficialTemplateDto dto);

    Task<Result<OfficialTemplateResponseDto>> AddNewVersionAsync(long id, NewOfficialTemplateVersionDto dto, string uploadsRootPath);

    Task<Result<bool>> SoftDeleteAsync(long id);

    Task<Result<bool>> ReactivateAsync(long id);
}
