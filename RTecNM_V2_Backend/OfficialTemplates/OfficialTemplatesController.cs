using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TecNM.Residency.Common;

namespace TecNM.Residency.OfficialTemplates;

[ApiController]
[Authorize]
[Route("api/v1/official-templates")]
public class OfficialTemplatesController : ControllerBase
{
    private readonly IOfficialTemplateService _service;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public OfficialTemplatesController(
        IOfficialTemplateService service,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _service = service;
        _environment = environment;
        _configuration = configuration;
    }

    private string UploadsRootPath =>
        Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _configuration["Uploads:Path"] ?? "uploads"));

    private bool IsAdmin => User.IsInRole("admin");

    /// <summary>
    /// Listar formatos oficiales paginados con filtros (Acceso universal para todos los roles).
    /// Si el usuario no es admin, solo puede ver formatos activos.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationQuery query,
        [FromQuery] string? category = null,
        [FromQuery] bool includeInactive = false)
    {
        // Solo administradores pueden ver registros inactivos
        var allowInactive = IsAdmin && includeInactive;
        var result = await _service.GetPagedAsync(query, category, allowInactive);
        return Ok(result.Data);
    }

    /// <summary>
    /// Obtener detalle de un formato institucional por ID.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        if (!result.Data!.IsActive && !IsAdmin)
            return NotFound(new { message = "El formato no se encuentra disponible." });

        return Ok(result.Data);
    }

    /// <summary>
    /// Descargar archivo vigente del formato institucional (Acceso universal para todos los roles).
    /// </summary>
    [HttpGet("{id:long}/download")]
    public async Task<IActionResult> DownloadCurrent(long id)
    {
        var result = await _service.DownloadCurrentAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        var (bytes, contentType, fileName) = result.Data;
        return File(bytes, contentType, fileName);
    }

    /// <summary>
    /// Obtener historial completo de versiones de un formato (Exclusivo Administrador).
    /// </summary>
    [HttpGet("{id:long}/history")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetHistory(long id)
    {
        var result = await _service.GetHistoryAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Descargar una versión histórica específica de un formato (Exclusivo Administrador).
    /// </summary>
    [HttpGet("{id:long}/versions/{versionId:long}/download")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DownloadVersion(long id, long versionId)
    {
        var result = await _service.DownloadVersionAsync(id, versionId);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        var (bytes, contentType, fileName) = result.Data;
        return File(bytes, contentType, fileName);
    }

    /// <summary>
    /// Crear y subir nuevo formato institucional (Exclusivo Administrador).
    /// Formatos permitidos: PDF, DOCX, DOC.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CreateOfficialTemplateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _service.CreateAsync(dto, UploadsRootPath);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    /// <summary>
    /// Actualizar metadatos de un formato institucional (Exclusivo Administrador).
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateOfficialTemplateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _service.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Subir nueva versión de archivo para un formato existente (Exclusivo Administrador).
    /// </summary>
    [HttpPost("{id:long}/new-version")]
    [Authorize(Roles = "admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddNewVersion(long id, [FromForm] NewOfficialTemplateVersionDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _service.AddNewVersionAsync(id, dto, UploadsRootPath);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    /// <summary>
    /// Eliminación lógica de un formato institucional (Exclusivo Administrador).
    /// </summary>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SoftDelete(long id)
    {
        var result = await _service.SoftDeleteAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return NoContent();
    }

    /// <summary>
    /// Reactivación de un formato institucional previamente desactivado (Exclusivo Administrador).
    /// </summary>
    [HttpPatch("{id:long}/activate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Reactivate(long id)
    {
        var result = await _service.ReactivateAsync(id);
        if (!result.IsSuccess)
            return StatusCode(result.StatusCode ?? 400, new { message = result.ErrorMessage });

        return NoContent();
    }
}
