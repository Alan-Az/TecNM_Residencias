using Microsoft.EntityFrameworkCore;
using Moq;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Notifications;
using TecNM.Residency.Companies;
using TecNM.Residency.Documents;
using TecNM.Residency.Evaluations;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;
using Xunit;

namespace RTecNM_V2_Backend.Tests;

public class DynamicAdvancesWorkflowTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Theory]
    [InlineData("avance_1", true, 1)]
    [InlineData("avance_2", true, 2)]
    [InlineData("avance_3", true, 3)]
    [InlineData("avance_15", true, 15)]
    [InlineData("AVANCE_4", true, 4)]
    [InlineData("proyecto_final", false, 0)]
    [InlineData("formato_29", false, 0)]
    [InlineData("avance_", false, 0)]
    [InlineData("avance_abc", false, 0)]
    public void DocumentType_DynamicAdvances_AreIdentifiedCorrectly(string docType, bool expectedIsAdvance, int expectedNumber)
    {
        bool isAdvance = DocumentType.IsAdvanceType(docType);
        int? number = DocumentType.GetAdvanceNumber(docType);

        Assert.Equal(expectedIsAdvance, isAdvance);
        Assert.Equal(expectedNumber == 0 ? null : expectedNumber, number);
        Assert.True(DocumentType.IsValid(docType) || !expectedIsAdvance);
    }

    [Fact]
    public async Task CheckFormatUploadOrder_BlocksSequentialAdvance_WhenPreviousAdvanceIsMissing()
    {
        using var db = CreateInMemoryDbContext();

        var studentUser = new User { Email = "s1@tecnm.mx", PasswordHash = "h", Role = UserRole.Student, IsActive = true };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ControlNumber = "111", FirstName = "A", LastName = "B", CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var project = new Project { Title = "P1", StudentId = student.Id, Status = ProjectStatus.Approved, IsActive = true };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Carta aceptacion aprobada
        db.Documents.Add(new Document
        {
            ProjectId = project.Id,
            DocumentType = DocumentType.CartaAceptacion,
            Status = DocumentStatus.Approved,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var docService = CreateDocumentService(db);

        // Intentar subir avance_2 sin avance_1 debe lanzar excepción
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.CheckFormatUploadOrderAsync(project.Id, "avance_2"));

        Assert.Contains("Avance #1", ex.Message);
    }

    [Fact]
    public async Task CheckFormatUploadOrder_BlocksAdvance2OrGreater_WhenNoAdvisorySessionExists()
    {
        using var db = CreateInMemoryDbContext();

        var studentUser = new User { Email = "s2@tecnm.mx", PasswordHash = "h", Role = UserRole.Student, IsActive = true };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ControlNumber = "112", FirstName = "A", LastName = "B", CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var project = new Project { Title = "P2", StudentId = student.Id, Status = ProjectStatus.Approved, IsActive = true };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Carta aceptacion y avance_1 existentes
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.CartaAceptacion, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = "avance_1", Status = DocumentStatus.Approved, IsActive = true });
        await db.SaveChangesAsync();

        var docService = CreateDocumentService(db);

        // Sin asesorías registradas: debe requerir sesión de asesoría
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.CheckFormatUploadOrderAsync(project.Id, "avance_2"));

        Assert.Contains("asesoría", ex.Message);
    }

    [Fact]
    public async Task CheckFormatUploadOrder_AllowsSequentialAdvances_WhenPrerequisitesMet()
    {
        using var db = CreateInMemoryDbContext();

        var studentUser = new User { Email = "s3@tecnm.mx", PasswordHash = "h", Role = UserRole.Student, IsActive = true };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ControlNumber = "113", FirstName = "A", LastName = "B", CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var project = new Project { Title = "P3", StudentId = student.Id, Status = ProjectStatus.Approved, IsActive = true };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Prerrequisitos
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.CartaAceptacion, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = "avance_1", Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = "avance_2", Status = DocumentStatus.Approved, IsActive = true });

        // Sesión de asesoría registrada
        db.AdvisorySessions.Add(new AdvisorySession
        {
            ProjectId = project.Id,
            AdvisorId = 1,
            SessionDate = DateTime.UtcNow.AddDays(-2),
            TopicsCovered = "Revisión de avance",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var docService = CreateDocumentService(db);

        // Subir avance_3 debe permitirse sin excepciones
        var exception = await Record.ExceptionAsync(() =>
            docService.CheckFormatUploadOrderAsync(project.Id, "avance_3"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CheckFormatUploadOrder_RequiresAtLeast2Advances_BeforeAllowingProyectoFinal()
    {
        using var db = CreateInMemoryDbContext();

        var studentUser = new User { Email = "s4@tecnm.mx", PasswordHash = "h", Role = UserRole.Student, IsActive = true };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ControlNumber = "114", FirstName = "A", LastName = "B", CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var project = new Project { Title = "P4", StudentId = student.Id, Status = ProjectStatus.Approved, IsActive = true };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Prerrequisitos generales aprobados
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.CartaAceptacion, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.Formato29, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.Formato29V2, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.Formato30, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.CartaTerminacion, Status = DocumentStatus.Approved, IsActive = true });

        // Solo 1 avance cargado
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = "avance_1", Status = DocumentStatus.Approved, IsActive = true });
        await db.SaveChangesAsync();

        var docService = CreateDocumentService(db);

        // CASO 1: Con solo 1 avance debe BLOQUEAR proyecto_final
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.CheckFormatUploadOrderAsync(project.Id, DocumentType.ProyectoFinal));
        Assert.Contains("2 avances", ex1.Message);

        // CASO 2: Agregar segundo avance pero RECHAZADO: sigue bloqueando
        var rejectedAdvance2 = new Document { ProjectId = project.Id, DocumentType = "avance_2", Status = DocumentStatus.Rejected, IsActive = true };
        db.Documents.Add(rejectedAdvance2);
        await db.SaveChangesAsync();

        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.CheckFormatUploadOrderAsync(project.Id, DocumentType.ProyectoFinal));
        Assert.Contains("2 avances", ex2.Message);

        // CASO 3: Con 2 avances aprobados y 2 asesorías: debe PERMITIR proyecto_final
        rejectedAdvance2.Status = DocumentStatus.Approved;
        db.AdvisorySessions.Add(new AdvisorySession { ProjectId = project.Id, AdvisorId = 1, SessionDate = DateTime.UtcNow.AddDays(-5), TopicsCovered = "Sesion 1", IsActive = true });
        db.AdvisorySessions.Add(new AdvisorySession { ProjectId = project.Id, AdvisorId = 1, SessionDate = DateTime.UtcNow.AddDays(-1), TopicsCovered = "Sesion 2", IsActive = true });
        await db.SaveChangesAsync();

        var exception3 = await Record.ExceptionAsync(() =>
            docService.CheckFormatUploadOrderAsync(project.Id, DocumentType.ProyectoFinal));
        Assert.Null(exception3);
    }

    [Fact]
    public async Task CheckFormatUploadOrder_AccreditationProject_BypassesAdvancesAndAdvisoryChecks()
    {
        using var db = CreateInMemoryDbContext();

        var studentUser = new User { Email = "s5@tecnm.mx", PasswordHash = "h", Role = UserRole.Student, IsActive = true };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ControlNumber = "115", FirstName = "Innova", LastName = "Tec", CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // Proyecto de acreditación nacional (InnovaTecNM)
        var project = new Project
        {
            Title = "Proyecto InnovaTec",
            StudentId = student.Id,
            ProjectType = "acreditacion_innovatec",
            Status = ProjectStatus.Approved,
            IsActive = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var docService = CreateDocumentService(db);

        // Proyecto InnovaTec debe poder cargar Proyecto Final sin requerir 2 avances ordinarios ni asesorías de empresa
        var exception = await Record.ExceptionAsync(() =>
            docService.CheckFormatUploadOrderAsync(project.Id, DocumentType.ProyectoFinal));

        Assert.Null(exception);
    }

    [Fact]
    public async Task CheckFormatUploadOrder_DuplicateAdvance1_DoesNotCountAsTwoAdvances()
    {
        using var db = CreateInMemoryDbContext();

        var studentUser = new User { Email = "s6@tecnm.mx", PasswordHash = "h", Role = UserRole.Student, IsActive = true };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ControlNumber = "116", FirstName = "A", LastName = "B", CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var project = new Project { Title = "P6", StudentId = student.Id, Status = ProjectStatus.Approved, IsActive = true };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.CartaAceptacion, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.Formato29, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.Formato29V2, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.Formato30, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.CartaTerminacion, Status = DocumentStatus.Approved, IsActive = true });

        // Alumno sube 'avance_1' dos veces (ejemplo: versión corregida y versión original)
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = "avance_1", Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = "avance_1", Status = DocumentStatus.Approved, IsActive = true });
        await db.SaveChangesAsync();

        var docService = CreateDocumentService(db);

        // Aunque hay 2 registros de 'avance_1', representan solo 1 avance distinto -> debe BLOQUEAR proyecto_final
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.CheckFormatUploadOrderAsync(project.Id, DocumentType.ProyectoFinal));

        Assert.Contains("2 avances", ex.Message);
    }

    [Fact]
    public async Task CheckFormatUploadOrder_SoftDeletedAdvance_DoesNotCountTowardMinimum()
    {
        using var db = CreateInMemoryDbContext();

        var studentUser = new User { Email = "s7@tecnm.mx", PasswordHash = "h", Role = UserRole.Student, IsActive = true };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ControlNumber = "117", FirstName = "A", LastName = "B", CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var project = new Project { Title = "P7", StudentId = student.Id, Status = ProjectStatus.Approved, IsActive = true };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.CartaAceptacion, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.Formato29, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.Formato29V2, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.Formato30, Status = DocumentStatus.Approved, IsActive = true });
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = DocumentType.CartaTerminacion, Status = DocumentStatus.Approved, IsActive = true });

        // avance_1 activo
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = "avance_1", Status = DocumentStatus.Approved, IsActive = true });
        // avance_2 ELIMINADO lógicamente (IsActive = false)
        db.Documents.Add(new Document { ProjectId = project.Id, DocumentType = "avance_2", Status = DocumentStatus.Approved, IsActive = false, DeletedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var docService = CreateDocumentService(db);

        // Como avance_2 está inactivo, solo hay 1 avance válido -> debe BLOQUEAR proyecto_final
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docService.CheckFormatUploadOrderAsync(project.Id, DocumentType.ProyectoFinal));

        Assert.Contains("2 avances", ex.Message);
    }

    [Fact]
    public async Task GradeEvaluation_WhenProjectAdvisorIdIsNull_AutoHealsFromStudentAndSucceeds()
    {
        using var db = CreateInMemoryDbContext();

        var advisorUser = new User { Email = "advisor@tecnm.mx", Role = UserRole.Advisor, IsActive = true };
        db.Users.Add(advisorUser);
        await db.SaveChangesAsync();

        var advisor = new Advisor { UserId = advisorUser.Id, FullName = "Dr. Asesor", IsActive = true };
        db.Advisors.Add(advisor);
        await db.SaveChangesAsync();

        var studentUser = new User { Email = "s8@tecnm.mx", Role = UserRole.Student, IsActive = true };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ControlNumber = "118", FirstName = "A", LastName = "B", CareerId = 1, AdvisorId = advisor.Id, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // Project con advisor_id NULL en tabla projects
        var project = new Project { Title = "P8", StudentId = student.Id, Student = student, AdvisorId = null, Status = ProjectStatus.InProgress, IsActive = true };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var mockEvalRepo = new Mock<IEvaluationRepository>();
        mockEvalRepo.Setup(r => r.SaveEvaluationAsync(It.IsAny<Evaluation>()))
            .ReturnsAsync((Evaluation e) => e);

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.UserId).Returns(advisorUser.Id);
        mockCurrentUser.Setup(u => u.IsInRole(UserRole.Advisor)).Returns(true);
        mockCurrentUser.Setup(u => u.IsInRole(It.IsNotIn(UserRole.Advisor))).Returns(false);

        var mockAdvisorRepo = new Mock<IAdvisorRepository>();
        mockAdvisorRepo.Setup(r => r.GetByUserIdAsync(advisorUser.Id)).ReturnsAsync(advisor);

        var mockProjectRepo = new Mock<IProjectRepository>();
        mockProjectRepo.Setup(r => r.GetByIdAsync(project.Id)).ReturnsAsync(project);
        mockProjectRepo.Setup(r => r.UpdateAsync(It.IsAny<Project>())).Returns(Task.CompletedTask);

        var mockNotification = new Mock<TecNM.Residency.Notifications.INotificationService>();
        var mockEmailQueue = new Mock<IEmailQueue>();
        var mockEmailTemplate = new Mock<IEmailTemplateService>();

        var evalService = new EvaluationService(
            mockEvalRepo.Object,
            mockCurrentUser.Object,
            mockAdvisorRepo.Object,
            mockProjectRepo.Object,
            mockNotification.Object,
            mockEmailQueue.Object,
            mockEmailTemplate.Object,
            db);

        var dto = new GradeEvaluationDto(
            project.Id,
            advisor.Id,
            "partial_1",
            95,
            "Excelente avance 1"
        );

        var result = await evalService.GradeEvaluationAsync(dto);

        Assert.True(result.IsSuccess);
        Assert.Equal(95, result.Data!.Score);
        // Verificar auto-healing del campo
        Assert.Equal(advisor.Id, project.AdvisorId);
    }

    private DocumentService CreateDocumentService(AppDbContext db)
    {
        var mockDocRepo = new Mock<IDocumentRepository>();
        var mockProjectRepo = new Mock<IProjectRepository>();
        var mockStudentRepo = new Mock<IStudentRepository>();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        var mockEmailQueue = new Mock<IEmailQueue>();
        var mockEmailTemplate = new Mock<IEmailTemplateService>();

        mockProjectRepo.Setup(r => r.GetByIdAsync(It.IsAny<long>()))
            .ReturnsAsync((long id) => db.Projects.Find(id));

        mockStudentRepo.Setup(r => r.GetByIdAsync(It.IsAny<long>()))
            .ReturnsAsync((long id) => db.Students.Find(id));

        return new DocumentService(
            mockDocRepo.Object,
            mockProjectRepo.Object,
            mockStudentRepo.Object,
            mockCurrentUser.Object,
            mockEmailQueue.Object,
            mockEmailTemplate.Object,
            db
        );
    }
}
