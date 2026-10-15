using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.OfficialTemplates;

public class OfficialTemplateService : IOfficialTemplateService
{
    private readonly IOfficialTemplateRepository _repository;
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc"
    };

    private const long MaxFileSize = 25 * 1024 * 1024; // 25 MB

    public OfficialTemplateService(
        IOfficialTemplateRepository repository,
        AppDbContext context,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedResult<OfficialTemplateResponseDto>>> GetPagedAsync(
        PaginationQuery query,
        string? category = null,
        bool includeInactive = false)
    {
        var paged = await _repository.GetPagedAsync(query, category, includeInactive);
        var dtos = paged.Items.Select(MapToResponseDto).ToList();

        var result = PaginatedResult<OfficialTemplateResponseDto>.Create(
            dtos,
            paged.TotalCount,
            paged.PageNumber,
            paged.PageSize
        );

        return Result<PaginatedResult<OfficialTemplateResponseDto>>.Success(result);
    }

    public async Task<Result<OfficialTemplateResponseDto>> GetByIdAsync(long id)
    {
        var template = await _repository.GetByIdAsync(id, includeVersions: true);
        if (template == null)
            return Result<OfficialTemplateResponseDto>.Failure("Formato institucional no encontrado.", 404);

        return Result<OfficialTemplateResponseDto>.Success(MapToResponseDto(template));
    }

    public async Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> DownloadCurrentAsync(long id)
    {
        var template = await _repository.GetByIdAsync(id);
        if (template == null)
            return Result<(byte[], string, string)>.Failure("Formato institucional no encontrado.", 404);

        if (!File.Exists(template.CurrentFilePath))
            return Result<(byte[], string, string)>.Failure("El archivo físico no se encuentra en el servidor.", 404);

        var bytes = await File.ReadAllBytesAsync(template.CurrentFilePath);
        return Result<(byte[], string, string)>.Success((bytes, template.CurrentContentType, template.CurrentFileName));
    }

    public async Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> DownloadVersionAsync(long templateId, long versionId)
    {
        var version = await _repository.GetVersionByIdAsync(templateId, versionId);
        if (version == null)
            return Result<(byte[], string, string)>.Failure("La versión solicitada no existe.", 404);

        if (!File.Exists(version.FilePath))
            return Result<(byte[], string, string)>.Failure("El archivo físico histórico no se encuentra en el servidor.", 404);

        var bytes = await File.ReadAllBytesAsync(version.FilePath);
        return Result<(byte[], string, string)>.Success((bytes, version.ContentType, version.FileName));
    }

    public async Task<Result<List<OfficialTemplateVersionResponseDto>>> GetHistoryAsync(long templateId)
    {
        var template = await _repository.GetByIdAsync(templateId);
        if (template == null)
            return Result<List<OfficialTemplateVersionResponseDto>>.Failure("Formato institucional no encontrado.", 404);

        var versions = await _repository.GetVersionsByTemplateIdAsync(templateId);
        var uploaderIds = versions.Where(v => v.UploadedBy.HasValue).Select(v => v.UploadedBy!.Value).Distinct().ToList();

        var usersDict = await _context.Users
            .Where(u => uploaderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

        var dtos = versions.Select(v => new OfficialTemplateVersionResponseDto
        {
            Id = v.Id,
            OfficialTemplateId = v.OfficialTemplateId,
            VersionNumber = v.VersionNumber,
            FileName = v.FileName,
            FileSize = v.FileSize,
            ContentType = v.ContentType,
            ChangeNotes = v.ChangeNotes,
            UploadedAt = v.UploadedAt,
            UploadedBy = v.UploadedBy,
            UploaderName = v.UploadedBy.HasValue && usersDict.TryGetValue(v.UploadedBy.Value, out var name) ? name : "Administrador"
        }).ToList();

        return Result<List<OfficialTemplateVersionResponseDto>>.Success(dtos);
    }

    public async Task<Result<OfficialTemplateResponseDto>> CreateAsync(CreateOfficialTemplateDto dto, string uploadsRootPath)
    {
        if (dto.File == null || dto.File.Length == 0)
            return Result<OfficialTemplateResponseDto>.Failure("Debe proporcionar un archivo válido.");

        if (dto.File.Length > MaxFileSize)
            return Result<OfficialTemplateResponseDto>.Failure($"El archivo excede el tamaño máximo permitido de {MaxFileSize / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Result<OfficialTemplateResponseDto>.Failure("Solo se permiten archivos en formato PDF o Word (.docx, .doc).");

        var codeExists = await _repository.ExistsCodeAsync(dto.Code);
        if (codeExists)
            return Result<OfficialTemplateResponseDto>.Failure($"El código '{dto.Code}' ya está registrado para otro formato.");

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId > 0 ? _currentUser.UserId : (long?)null;

        var template = new OfficialTemplate
        {
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim(),
            CurrentVersion = 1,
            CurrentFileName = Path.GetFileName(dto.File.FileName),
            CurrentFileSize = dto.File.Length,
            CurrentContentType = ResolveContentType(dto.File.ContentType, ext),
            IsActive = true,
            IsVisible = true,
            DisplayOrder = 0,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repository.AddAsync(template);
        await _repository.SaveChangesAsync();

        // Guardar archivo físico en uploads/official-templates/{template.Id}/
        var targetDir = Path.Combine(uploadsRootPath, "official-templates", template.Id.ToString());
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var storedFileName = $"v1_{Guid.NewGuid():N}{ext}";
        var storedFilePath = Path.Combine(targetDir, storedFileName);

        using (var stream = new FileStream(storedFilePath, FileMode.Create))
        {
            await dto.File.CopyToAsync(stream);
        }

        template.CurrentFilePath = storedFilePath;
        await _repository.UpdateAsync(template);

        // Crear registro de versión v1
        var version = new OfficialTemplateVersion
        {
            OfficialTemplateId = template.Id,
            VersionNumber = 1,
            FileName = template.CurrentFileName,
            FilePath = storedFilePath,
            FileSize = template.CurrentFileSize,
            ContentType = template.CurrentContentType,
            ChangeNotes = "Versión inicial del formato institucional.",
            UploadedAt = now,
            UploadedBy = userId,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true,
            IsVisible = true
        };

        await _repository.AddVersionAsync(version);
        await _repository.SaveChangesAsync();

        return Result<OfficialTemplateResponseDto>.Success(MapToResponseDto(template));
    }

    public async Task<Result<OfficialTemplateResponseDto>> UpdateAsync(long id, UpdateOfficialTemplateDto dto)
    {
        var template = await _repository.GetByIdAsync(id, includeVersions: true);
        if (template == null)
            return Result<OfficialTemplateResponseDto>.Failure("Formato institucional no encontrado.", 404);

        var codeExists = await _repository.ExistsCodeAsync(dto.Code, id);
        if (codeExists)
            return Result<OfficialTemplateResponseDto>.Failure($"El código '{dto.Code}' ya está registrado para otro formato.");

        var userId = _currentUser.UserId > 0 ? _currentUser.UserId : (long?)null;

        template.Code = dto.Code.Trim().ToUpperInvariant();
        template.Name = dto.Name.Trim();
        template.Description = dto.Description?.Trim();
        template.Category = string.IsNullOrWhiteSpace(dto.Category) ? "General" : dto.Category.Trim();
        template.UpdatedBy = userId;
        template.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(template);
        await _repository.SaveChangesAsync();

        return Result<OfficialTemplateResponseDto>.Success(MapToResponseDto(template));
    }

    public async Task<Result<OfficialTemplateResponseDto>> AddNewVersionAsync(long id, NewOfficialTemplateVersionDto dto, string uploadsRootPath)
    {
        var template = await _repository.GetByIdAsync(id, includeVersions: true);
        if (template == null)
            return Result<OfficialTemplateResponseDto>.Failure("Formato institucional no encontrado.", 404);

        if (dto.File == null || dto.File.Length == 0)
            return Result<OfficialTemplateResponseDto>.Failure("Debe proporcionar un archivo válido.");

        if (dto.File.Length > MaxFileSize)
            return Result<OfficialTemplateResponseDto>.Failure($"El archivo excede el tamaño máximo permitido de {MaxFileSize / (1024 * 1024)}MB.");

        var ext = Path.GetExtension(dto.File.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Result<OfficialTemplateResponseDto>.Failure("Solo se permiten archivos en formato PDF o Word (.docx, .doc).");

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId > 0 ? _currentUser.UserId : (long?)null;
        var nextVersion = template.CurrentVersion + 1;

        var targetDir = Path.Combine(uploadsRootPath, "official-templates", template.Id.ToString());
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var storedFileName = $"v{nextVersion}_{Guid.NewGuid():N}{ext}";
        var storedFilePath = Path.Combine(targetDir, storedFileName);

        using (var stream = new FileStream(storedFilePath, FileMode.Create))
        {
            await dto.File.CopyToAsync(stream);
        }

        var originalFileName = Path.GetFileName(dto.File.FileName);
        var contentType = ResolveContentType(dto.File.ContentType, ext);

        // Actualizar plantilla con la nueva versión
        template.CurrentVersion = nextVersion;
        template.CurrentFileName = originalFileName;
        template.CurrentFilePath = storedFilePath;
        template.CurrentFileSize = dto.File.Length;
        template.CurrentContentType = contentType;
        template.UpdatedBy = userId;
        template.UpdatedAt = now;

        await _repository.UpdateAsync(template);

        // Guardar registro en historial
        var version = new OfficialTemplateVersion
        {
            OfficialTemplateId = template.Id,
            VersionNumber = nextVersion,
            FileName = originalFileName,
            FilePath = storedFilePath,
            FileSize = dto.File.Length,
            ContentType = contentType,
            ChangeNotes = string.IsNullOrWhiteSpace(dto.ChangeNotes) ? $"Actualización a versión {nextVersion}." : dto.ChangeNotes.Trim(),
            UploadedAt = now,
            UploadedBy = userId,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true,
            IsVisible = true
        };

        await _repository.AddVersionAsync(version);
        await _repository.SaveChangesAsync();

        return Result<OfficialTemplateResponseDto>.Success(MapToResponseDto(template));
    }

    public async Task<Result<bool>> SoftDeleteAsync(long id)
    {
        var template = await _repository.GetByIdAsync(id);
        if (template == null)
            return Result<bool>.Failure("Formato institucional no encontrado.", 404);

        var userId = _currentUser.UserId > 0 ? _currentUser.UserId : (long?)null;
        template.IsActive = false;
        template.DeletedAt = DateTime.UtcNow;
        template.DeletedBy = userId;

        await _repository.UpdateAsync(template);
        await _repository.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ReactivateAsync(long id)
    {
        var template = await _repository.GetByIdAsync(id);
        if (template == null)
            return Result<bool>.Failure("Formato institucional no encontrado.", 404);

        template.IsActive = true;
        template.DeletedAt = null;
        template.DeletedBy = null;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : (long?)null;

        await _repository.UpdateAsync(template);
        await _repository.SaveChangesAsync();

        return Result<bool>.Success(true);
    }

    private static string ResolveContentType(string? clientContentType, string extension)
    {
        if (!string.IsNullOrWhiteSpace(clientContentType) && clientContentType != "application/octet-stream")
            return clientContentType;

        return extension.ToLowerInvariant() switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            _ => "application/pdf"
        };
    }

    private static OfficialTemplateResponseDto MapToResponseDto(OfficialTemplate t)
    {
        return new OfficialTemplateResponseDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            Description = t.Description,
            Category = t.Category,
            CurrentVersion = t.CurrentVersion,
            CurrentFileName = t.CurrentFileName,
            CurrentFileSize = t.CurrentFileSize,
            CurrentContentType = t.CurrentContentType,
            IsActive = t.IsActive,
            IsVisible = t.IsVisible,
            DisplayOrder = t.DisplayOrder,
            CreatedBy = t.CreatedBy,
            UpdatedBy = t.UpdatedBy,
            DeletedBy = t.DeletedBy,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            DeletedAt = t.DeletedAt,
            VersionsCount = t.Versions?.Count ?? 0
        };
    }
}
