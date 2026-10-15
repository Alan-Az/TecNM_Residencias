using TecNM.Residency.Common;

namespace TecNM.Residency.OfficialTemplates;

public interface IOfficialTemplateRepository
{
    Task<OfficialTemplate?> GetByIdAsync(long id, bool includeVersions = false);
    Task<OfficialTemplate?> GetByCodeAsync(string code);
    Task<bool> ExistsCodeAsync(string code, long? excludeId = null);
    Task<PaginatedResult<OfficialTemplate>> GetPagedAsync(PaginationQuery query, string? category = null, bool includeInactive = false);
    Task<List<OfficialTemplateVersion>> GetVersionsByTemplateIdAsync(long templateId);
    Task<OfficialTemplateVersion?> GetVersionByIdAsync(long templateId, long versionId);
    Task AddAsync(OfficialTemplate template);
    Task UpdateAsync(OfficialTemplate template);
    Task AddVersionAsync(OfficialTemplateVersion version);
    Task SaveChangesAsync();
}
