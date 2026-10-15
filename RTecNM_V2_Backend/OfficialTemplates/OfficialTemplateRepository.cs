using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.OfficialTemplates;

public class OfficialTemplateRepository : IOfficialTemplateRepository
{
    private readonly AppDbContext _context;

    public OfficialTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<OfficialTemplate?> GetByIdAsync(long id, bool includeVersions = false)
    {
        IQueryable<OfficialTemplate> query = _context.OfficialTemplates;
        if (includeVersions)
        {
            query = query.Include(t => t.Versions);
        }
        return await query.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<OfficialTemplate?> GetByCodeAsync(string code)
    {
        return await _context.OfficialTemplates
            .FirstOrDefaultAsync(t => t.Code.ToLower() == code.Trim().ToLower());
    }

    public async Task<bool> ExistsCodeAsync(string code, long? excludeId = null)
    {
        var normalized = code.Trim().ToLower();
        return await _context.OfficialTemplates
            .AnyAsync(t => t.Code.ToLower() == normalized && (!excludeId.HasValue || t.Id != excludeId.Value));
    }

    public async Task<PaginatedResult<OfficialTemplate>> GetPagedAsync(
        PaginationQuery query,
        string? category = null,
        bool includeInactive = false)
    {
        IQueryable<OfficialTemplate> q = _context.OfficialTemplates.Include(t => t.Versions);

        if (!includeInactive)
        {
            q = q.Where(t => t.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var cat = category.Trim().ToLower();
            q = q.Where(t => t.Category.ToLower() == cat);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(t => t.Name.ToLower().Contains(term)
                          || t.Code.ToLower().Contains(term)
                          || (t.Description != null && t.Description.ToLower().Contains(term))
                          || t.CurrentFileName.ToLower().Contains(term));
        }

        q = q.ApplySort(
            query.SortBy,
            query.SortDir,
            new[] { "Code", "Name", "Category", "CurrentVersion", "CurrentFileName", "CurrentFileSize", "CreatedAt", "UpdatedAt", "DisplayOrder" },
            "DisplayOrder",
            defaultDescending: false);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<OfficialTemplateVersion>> GetVersionsByTemplateIdAsync(long templateId)
    {
        return await _context.OfficialTemplateVersions
            .Where(v => v.OfficialTemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();
    }

    public async Task<OfficialTemplateVersion?> GetVersionByIdAsync(long templateId, long versionId)
    {
        return await _context.OfficialTemplateVersions
            .FirstOrDefaultAsync(v => v.OfficialTemplateId == templateId && v.Id == versionId);
    }

    public async Task AddAsync(OfficialTemplate template)
    {
        await _context.OfficialTemplates.AddAsync(template);
    }

    public async Task UpdateAsync(OfficialTemplate template)
    {
        _context.OfficialTemplates.Update(template);
        await Task.CompletedTask;
    }

    public async Task AddVersionAsync(OfficialTemplateVersion version)
    {
        await _context.OfficialTemplateVersions.AddAsync(version);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
