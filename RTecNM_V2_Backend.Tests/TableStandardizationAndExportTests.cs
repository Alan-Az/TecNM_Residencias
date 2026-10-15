using Microsoft.EntityFrameworkCore;
using Moq;
using TecNM.Residency.Admin;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Companies;
using TecNM.Residency.Evaluations;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;
using Xunit;

namespace RTecNM_V2_Backend.Tests;

public class TableStandardizationAndExportTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CompanyService_ExportPdfAsync_GeneratesValidPdfBytes_AndRespectsFilters()
    {
        using var db = CreateInMemoryDbContext();

        db.Companies.AddRange(
            new Company
            {
                Name = "Alpha Technologies",
                LegalName = "Alpha Tech SA de CV",
                Rfc = "ATE123456789",
                Sector = "PRIVADO",
                ContactName = "Juan Perez",
                ContactEmail = "juan@alpha.com",
                ContactPhone = "8661234567",
                IsActive = true
            },
            new Company
            {
                Name = "Beta Industries",
                LegalName = "Beta Ind SA de CV",
                Rfc = "BIN987654321",
                Sector = "PUBLICO",
                ContactName = "Maria Lopez",
                ContactEmail = "maria@beta.com",
                ContactPhone = "8667654321",
                IsActive = false
            }
        );
        await db.SaveChangesAsync();

        var repo = new CompanyRepository(db);
        var service = new CompanyService(repo);

        // 1. Exportar solo activas
        var resultActive = await service.ExportPdfAsync(search: null, sector: null, sortBy: "Name", sortDir: "asc", includeInactive: false);
        Assert.True(resultActive.IsSuccess);
        Assert.NotNull(resultActive.Data);
        Assert.NotEmpty(resultActive.Data!);

        // 2. Exportar con filtro de búsqueda por término
        var resultSearch = await service.ExportPdfAsync(search: "Alpha", sector: null, sortBy: "Name", sortDir: "asc", includeInactive: true);
        Assert.True(resultSearch.IsSuccess);
        Assert.NotNull(resultSearch.Data);
        Assert.NotEmpty(resultSearch.Data!);
    }

    [Fact]
    public async Task ReportGeneratorService_GetReleasableProjectsAsync_FiltersBySearch_AndSortsCorrectly()
    {
        using var db = CreateInMemoryDbContext();

        var user1 = new User { Email = "carlos@tecnm.mx", FirstName = "Carlos", LastName = "Hernandez", Role = UserRole.Student, IsActive = true };
        var user2 = new User { Email = "ana@tecnm.mx", FirstName = "Ana", LastName = "Zavala", Role = UserRole.Student, IsActive = true };
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        var student1 = new Student
        {
            UserId = user1.Id,
            User = user1,
            FirstName = "Carlos",
            LastName = "Hernandez",
            ControlNumber = "20051111",
            Curp = "HERC200511HCL12345",
            CareerId = 1,
            IsActive = true
        };
        var student2 = new Student
        {
            UserId = user2.Id,
            User = user2,
            FirstName = "Ana",
            LastName = "Zavala",
            ControlNumber = "20052222",
            Curp = "ZAVA200522MCL54321",
            CareerId = 1,
            IsActive = true
        };
        db.Students.AddRange(student1, student2);
        await db.SaveChangesAsync();

        var project1 = new Project
        {
            Title = "Sistema de Control Escolar",
            StudentId = student1.Id,
            Student = student1,
            IsActive = true,
            Status = ProjectStatus.InProgress
        };
        var project2 = new Project
        {
            Title = "Automatización de Inventarios",
            StudentId = student2.Id,
            Student = student2,
            IsActive = true,
            Status = ProjectStatus.InProgress
        };
        db.Projects.AddRange(project1, project2);
        await db.SaveChangesAsync();

        // Evaluaciones para promediar: 3 periodos (partial_1, partial_2, final)
        // Project 1 promedio: (80 + 85 + 90) / 3 = 85
        db.Evaluations.AddRange(
            new Evaluation { ProjectId = project1.Id, EvaluationPeriod = "partial_1", Score = 80, IsActive = true },
            new Evaluation { ProjectId = project1.Id, EvaluationPeriod = "partial_2", Score = 85, IsActive = true },
            new Evaluation { ProjectId = project1.Id, EvaluationPeriod = "final", Score = 90, IsActive = true }
        );

        // Project 2 promedio: (95 + 95 + 95) / 3 = 95
        db.Evaluations.AddRange(
            new Evaluation { ProjectId = project2.Id, EvaluationPeriod = "partial_1", Score = 95, IsActive = true },
            new Evaluation { ProjectId = project2.Id, EvaluationPeriod = "partial_2", Score = 95, IsActive = true },
            new Evaluation { ProjectId = project2.Id, EvaluationPeriod = "final", Score = 95, IsActive = true }
        );
        await db.SaveChangesAsync();

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.Role).Returns(UserRole.Admin);

        var reportService = new ReportGeneratorService(db, mockUser.Object);

        // Prueba de búsqueda por título
        var querySearch = new PaginationQuery
        {
            PageNumber = 1,
            PageSize = 10,
            Search = "Inventarios"
        };
        var searchResult = await reportService.GetReleasableProjectsAsync(querySearch);
        Assert.True(searchResult.IsSuccess);
        var searchItems = searchResult.Data!.Items.ToList();
        Assert.Single(searchItems);
        Assert.Equal("Automatización de Inventarios", searchItems[0].Title);

        // Prueba de ordenamiento descendente por promedio (AverageScore desc)
        var querySort = new PaginationQuery
        {
            PageNumber = 1,
            PageSize = 10,
            SortBy = "AverageScore",
            SortDir = "desc"
        };
        var sortResult = await reportService.GetReleasableProjectsAsync(querySort);
        Assert.True(sortResult.IsSuccess);
        var sortItems = sortResult.Data!.Items.ToList();
        Assert.Equal(2, sortItems.Count);
        Assert.Equal("Automatización de Inventarios", sortItems[0].Title);
        Assert.Equal("Sistema de Control Escolar", sortItems[1].Title);

        // Prueba de exportación a PDF
        var exportResult = await reportService.ExportReleasableProjectsPdfAsync(search: null, sortBy: "AverageScore", sortDir: "desc");
        Assert.True(exportResult.IsSuccess);
        Assert.NotNull(exportResult.Data);
        Assert.NotEmpty(exportResult.Data!);
    }
}
