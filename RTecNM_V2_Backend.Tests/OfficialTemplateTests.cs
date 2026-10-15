using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using TecNM.Residency.Common;
using TecNM.Residency.OfficialTemplates;
using Xunit;

namespace RTecNM_V2_Backend.Tests;

public class OfficialTemplateTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private IFormFile CreateMockFormFile(string fileName, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        var file = new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
        return file;
    }

    [Fact]
    public async Task CreateAsync_WhenExtensionIsInvalid_ReturnsFailure()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new OfficialTemplateRepository(db);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(1);

        var service = new OfficialTemplateService(repo, db, currentUserServiceMock.Object);

        var file = CreateMockFormFile("script.exe", "application/octet-stream", Encoding.UTF8.GetBytes("fake content"));
        var dto = new CreateOfficialTemplateDto
        {
            Code = "TEST-01",
            Name = "Formato Test",
            Category = "General",
            File = file
        };

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var result = await service.CreateAsync(dto, tempDir);
            Assert.False(result.IsSuccess);
            Assert.Contains("Solo se permiten archivos en formato PDF o Word", result.ErrorMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CreateAsync_And_AddNewVersion_IncrementsVersionCorrectly()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new OfficialTemplateRepository(db);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(1);

        var service = new OfficialTemplateService(repo, db, currentUserServiceMock.Object);

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Crear formato versión 1
            var fileV1 = CreateMockFormFile("formato_solicitud.pdf", "application/pdf", Encoding.UTF8.GetBytes("version 1 pdf"));
            var createDto = new CreateOfficialTemplateDto
            {
                Code = "ITLP-RES-01",
                Name = "Solicitud de Residencia",
                Category = "Inicial",
                File = fileV1
            };

            var createResult = await service.CreateAsync(createDto, tempDir);
            Assert.True(createResult.IsSuccess);
            Assert.Equal(1, createResult.Data!.CurrentVersion);
            Assert.Equal("ITLP-RES-01", createResult.Data.Code);

            // 2. Subir nueva versión 2 en Word (.docx)
            var fileV2 = CreateMockFormFile("formato_solicitud_v2.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", Encoding.UTF8.GetBytes("version 2 word"));
            var newVerDto = new NewOfficialTemplateVersionDto
            {
                File = fileV2,
                ChangeNotes = "Actualización a versión Word editable."
            };

            var updateVerResult = await service.AddNewVersionAsync(createResult.Data.Id, newVerDto, tempDir);
            Assert.True(updateVerResult.IsSuccess);
            Assert.Equal(2, updateVerResult.Data!.CurrentVersion);
            Assert.Equal("formato_solicitud_v2.docx", updateVerResult.Data.CurrentFileName);

            // 3. Verificar historial
            var historyResult = await service.GetHistoryAsync(createResult.Data.Id);
            Assert.True(historyResult.IsSuccess);
            Assert.Equal(2, historyResult.Data!.Count);
            Assert.Equal(2, historyResult.Data[0].VersionNumber);
            Assert.Equal(1, historyResult.Data[1].VersionNumber);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SoftDelete_And_Reactivate_CompliesWithIsActiveProtocol()
    {
        using var db = CreateInMemoryDbContext();
        var repo = new OfficialTemplateRepository(db);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        currentUserServiceMock.Setup(u => u.UserId).Returns(1);

        var service = new OfficialTemplateService(repo, db, currentUserServiceMock.Object);

        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var file = CreateMockFormFile("documento.pdf", "application/pdf", Encoding.UTF8.GetBytes("pdf content"));
            var createDto = new CreateOfficialTemplateDto
            {
                Code = "ITLP-RES-02",
                Name = "Reporte Preliminar",
                Category = "Seguimiento",
                File = file
            };

            var created = await service.CreateAsync(createDto, tempDir);
            Assert.True(created.IsSuccess);
            Assert.True(created.Data!.IsActive);

            // Soft delete
            var deleteResult = await service.SoftDeleteAsync(created.Data.Id);
            Assert.True(deleteResult.IsSuccess);

            var deleted = await repo.GetByIdAsync(created.Data.Id);
            Assert.NotNull(deleted);
            Assert.False(deleted!.IsActive);
            Assert.NotNull(deleted.DeletedAt);

            // Reactivate
            var reactivateResult = await service.ReactivateAsync(created.Data.Id);
            Assert.True(reactivateResult.IsSuccess);

            var reactivated = await repo.GetByIdAsync(created.Data.Id);
            Assert.NotNull(reactivated);
            Assert.True(reactivated!.IsActive);
            Assert.Null(reactivated.DeletedAt);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
