using Microsoft.EntityFrameworkCore;
using Moq;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Notifications;
using TecNM.Residency.Companies;
using TecNM.Residency.Documents;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;
using Xunit;

namespace RTecNM_V2_Backend.Tests;

public class AdvisorMissingDocumentsChecklistTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private DocumentService CreateService(AppDbContext db, Mock<ICurrentUserService> currentUserMock)
    {
        var docRepoMock = new Mock<IDocumentRepository>();
        var projRepoMock = new Mock<IProjectRepository>();
        var studentRepoMock = new Mock<IStudentRepository>();
        var emailQueueMock = new Mock<IEmailQueue>();
        var emailTemplateServiceMock = new Mock<IEmailTemplateService>();

        projRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>()))
            .ReturnsAsync((long id) => db.Projects.Find(id));

        studentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>()))
            .ReturnsAsync((long id) => db.Students.Find(id));

        return new DocumentService(
            docRepoMock.Object,
            projRepoMock.Object,
            studentRepoMock.Object,
            currentUserMock.Object,
            emailQueueMock.Object,
            emailTemplateServiceMock.Object,
            db
        );
    }

    [Fact]
    public async Task GetMissingDocumentsChecklist_AdvisorViewingOwnAdvisee_ReturnsSuccessWithMissingList()
    {
        using var db = CreateInMemoryDbContext();

        var advisorUser = new User { Role = UserRole.Advisor, Email = "advisor1@tecnm.mx", IsActive = true };
        db.Users.Add(advisorUser);
        await db.SaveChangesAsync();

        var advisor = new Advisor { UserId = advisorUser.Id, FullName = "Ing. Asesor Uno", AdvisorType = AdvisorType.Internal, IsActive = true };
        db.Advisors.Add(advisor);
        await db.SaveChangesAsync();

        var studentUser = new User { Role = UserRole.Student, Email = "student1@tecnm.mx", IsActive = true };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ControlNumber = "2026001", FirstName = "Juan", LastName = "Pérez", AdvisorId = advisor.Id, CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var company = new Company { Name = "Empresa Test 1", IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var project = new Project
        {
            StudentId = student.Id,
            AdvisorId = advisor.Id,
            CompanyId = company.Id,
            Title = "Sistema de Control",
            Status = ProjectStatus.InProgress,
            IsActive = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(advisorUser.Id);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Advisor);

        var service = CreateService(db, currentUserMock);

        var result = await service.GetMissingDocumentsChecklistAsync(project.Id);

        Assert.True(result.IsSuccess, $"Error was: {result.ErrorMessage} (status: {result.StatusCode})");
        Assert.NotNull(result.Data);
        Assert.Equal(6, result.Data.TotalRequired);
        Assert.Equal(6, result.Data.TotalMissing);
        Assert.False(result.Data.IsComplete);
        Assert.Contains(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.CartaAceptacion);
        Assert.Contains(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.Formato29);
        Assert.Contains(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.Formato29V2);
        Assert.Contains(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.Formato30);
        Assert.Contains(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.CartaTerminacion);
        Assert.Contains(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.ProyectoFinal);
    }

    [Fact]
    public async Task GetMissingDocumentsChecklist_AdvisorViewingAnotherAdvisorAdvisee_ReturnsForbidden()
    {
        using var db = CreateInMemoryDbContext();

        var advisorUser1 = new User { Role = UserRole.Advisor, Email = "advisor1@tecnm.mx", IsActive = true };
        var advisorUser2 = new User { Role = UserRole.Advisor, Email = "advisor2@tecnm.mx", IsActive = true };
        db.Users.AddRange(advisorUser1, advisorUser2);
        await db.SaveChangesAsync();

        var advisor1 = new Advisor { UserId = advisorUser1.Id, FullName = "Ing. Asesor Uno", AdvisorType = AdvisorType.Internal, IsActive = true };
        var advisor2 = new Advisor { UserId = advisorUser2.Id, FullName = "Ing. Asesor Dos", AdvisorType = AdvisorType.Internal, IsActive = true };
        db.Advisors.AddRange(advisor1, advisor2);
        await db.SaveChangesAsync();

        var student = new Student { ControlNumber = "2026002", FirstName = "María", LastName = "López", AdvisorId = advisor2.Id, CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var company = new Company { Name = "Empresa Test 2", IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var project = new Project
        {
            StudentId = student.Id,
            AdvisorId = advisor2.Id, // Asignado a Asesor Dos
            CompanyId = company.Id,
            Title = "Proyecto Redes",
            Status = ProjectStatus.InProgress,
            IsActive = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // Current user es Asesor Uno (advisorUser1)
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(advisorUser1.Id);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Advisor);

        var service = CreateService(db, currentUserMock);

        var result = await service.GetMissingDocumentsChecklistAsync(project.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("No autorizado", result.ErrorMessage);
    }

    [Fact]
    public async Task GetMissingDocumentsChecklist_CareerHeadViewingOwnAdvisee_ReturnsSuccess()
    {
        using var db = CreateInMemoryDbContext();

        // Jefe de carrera que también tiene perfil de asesor
        var chUser = new User { Role = UserRole.CareerHead, Email = "jefe.sistemas@tecnm.mx", CareerId = 1, IsActive = true };
        db.Users.Add(chUser);
        await db.SaveChangesAsync();

        var chAdvisor = new Advisor { UserId = chUser.Id, FullName = "Mtro. Jefe y Asesor", AdvisorType = AdvisorType.Internal, IsActive = true };
        db.Advisors.Add(chAdvisor);
        await db.SaveChangesAsync();

        var student = new Student { ControlNumber = "2026003", FirstName = "Pedro", LastName = "Soto", AdvisorId = chAdvisor.Id, CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var company = new Company { Name = "Empresa Test 3", IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var project = new Project
        {
            StudentId = student.Id,
            AdvisorId = chAdvisor.Id, // Asignado directamente al Jefe de Carrera como asesor
            CompanyId = company.Id,
            Title = "Proyecto Web",
            Status = ProjectStatus.InProgress,
            IsActive = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(chUser.Id);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.CareerHead);
        currentUserMock.Setup(u => u.CareerId).Returns(1);

        var service = CreateService(db, currentUserMock);

        var result = await service.GetMissingDocumentsChecklistAsync(project.Id);

        Assert.True(result.IsSuccess, $"Error was: {result.ErrorMessage} (status: {result.StatusCode})");
        Assert.NotNull(result.Data);
        Assert.Equal(project.Id, result.Data.ProjectId);
        Assert.Equal("Pedro Soto", result.Data.StudentName);
    }

    [Fact]
    public async Task GetMissingDocumentsChecklist_CareerHeadViewingAnotherAdvisorAdvisee_ReturnsForbidden()
    {
        using var db = CreateInMemoryDbContext();

        // Jefe de carrera
        var chUser = new User { Role = UserRole.CareerHead, Email = "jefe.sistemas@tecnm.mx", CareerId = 1, IsActive = true };
        var otherUser = new User { Role = UserRole.Advisor, Email = "otro.docente@tecnm.mx", CareerId = 1, IsActive = true };
        db.Users.AddRange(chUser, otherUser);
        await db.SaveChangesAsync();

        var chAdvisor = new Advisor { UserId = chUser.Id, FullName = "Mtro. Jefe y Asesor", AdvisorType = AdvisorType.Internal, IsActive = true };
        var otherAdvisor = new Advisor { UserId = otherUser.Id, FullName = "Ing. Otro Docente", AdvisorType = AdvisorType.Internal, IsActive = true };
        db.Advisors.AddRange(chAdvisor, otherAdvisor);
        await db.SaveChangesAsync();

        // Estudiante de la carrera de sistemas, pero asesorado por 'otherAdvisor'
        var student = new Student { ControlNumber = "2026004", FirstName = "Lucía", LastName = "Méndez", AdvisorId = otherAdvisor.Id, CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var company = new Company { Name = "Empresa Test 4", IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var project = new Project
        {
            StudentId = student.Id,
            AdvisorId = otherAdvisor.Id,
            CompanyId = company.Id,
            Title = "Proyecto Base de Datos",
            Status = ProjectStatus.InProgress,
            IsActive = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(chUser.Id);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.CareerHead);
        currentUserMock.Setup(u => u.CareerId).Returns(1);

        var service = CreateService(db, currentUserMock);

        var result = await service.GetMissingDocumentsChecklistAsync(project.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("Como Jefe de Carrera, esta vista de documentos faltantes de asesor solo está disponible para tus propios alumnos asesorados", result.ErrorMessage);
    }

    [Fact]
    public async Task GetMissingDocumentsChecklist_AccreditationProject_RequiresOnlyThreeSpecializedDocuments()
    {
        using var db = CreateInMemoryDbContext();

        var advisorUser = new User { Role = UserRole.Advisor, Email = "advisor@tecnm.mx", IsActive = true };
        db.Users.Add(advisorUser);
        await db.SaveChangesAsync();

        var advisor = new Advisor { UserId = advisorUser.Id, FullName = "Ing. Asesor", AdvisorType = AdvisorType.Internal, IsActive = true };
        db.Advisors.Add(advisor);
        await db.SaveChangesAsync();

        var student = new Student { ControlNumber = "2026005", FirstName = "Diana", LastName = "Ramos", AdvisorId = advisor.Id, CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var company = new Company { Name = "Empresa Test 5", IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        // Proyecto por acreditación InnovaTec
        var project = new Project
        {
            StudentId = student.Id,
            AdvisorId = advisor.Id,
            CompanyId = company.Id,
            Title = "Acreditación InnovaTec Nacional",
            ProjectType = "acreditacion_innovatec",
            Status = ProjectStatus.InProgress,
            IsActive = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(advisorUser.Id);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Advisor);

        var service = CreateService(db, currentUserMock);

        var result = await service.GetMissingDocumentsChecklistAsync(project.Id);

        Assert.True(result.IsSuccess, $"Error was: {result.ErrorMessage} (status: {result.StatusCode})");
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsAccreditation);
        Assert.Equal(3, result.Data.TotalRequired);
        Assert.Contains(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.ConstanciaAcreditacion);
        Assert.Contains(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.Dictamen);
        Assert.Contains(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.Libranza);
    }

    [Fact]
    public async Task GetMissingDocumentsChecklist_WhenDocumentIsRejected_CountsAsMissingWithRejectionReason()
    {
        using var db = CreateInMemoryDbContext();

        var advisorUser = new User { Role = UserRole.Advisor, Email = "advisor@tecnm.mx", IsActive = true };
        db.Users.Add(advisorUser);
        await db.SaveChangesAsync();

        var advisor = new Advisor { UserId = advisorUser.Id, FullName = "Ing. Asesor", AdvisorType = AdvisorType.Internal, IsActive = true };
        db.Advisors.Add(advisor);
        await db.SaveChangesAsync();

        var student = new Student { ControlNumber = "2026006", FirstName = "Sofía", LastName = "García", AdvisorId = advisor.Id, CareerId = 1, IsActive = true };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var company = new Company { Name = "Empresa Test 6", IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var project = new Project
        {
            StudentId = student.Id,
            AdvisorId = advisor.Id,
            CompanyId = company.Id,
            Title = "Proyecto Móvil",
            Status = ProjectStatus.InProgress,
            IsActive = true
        };
        db.Projects.Add(project);

        // Subió Carta de Aceptación aprobada
        db.Documents.Add(new Document
        {
            ProjectId = project.Id,
            DocumentType = DocumentType.CartaAceptacion,
            FileName = "Carta_Aceptacion.pdf",
            Status = DocumentStatus.Approved,
            IsActive = true
        });

        // Subió Formato 29, pero fue RECHAZADO por observaciones
        db.Documents.Add(new Document
        {
            ProjectId = project.Id,
            DocumentType = DocumentType.Formato29,
            FileName = "Formato_29.pdf",
            Status = DocumentStatus.Rejected,
            RejectionReason = "Firma del asesor externo ilegible",
            IsActive = true
        });

        await db.SaveChangesAsync();

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(advisorUser.Id);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Advisor);

        var service = CreateService(db, currentUserMock);

        var result = await service.GetMissingDocumentsChecklistAsync(project.Id);

        Assert.True(result.IsSuccess, $"Error was: {result.ErrorMessage} (status: {result.StatusCode})");
        Assert.NotNull(result.Data);

        // Carta de aceptación ya no debe estar en faltantes
        Assert.DoesNotContain(result.Data.MissingDocuments, m => m.DocumentType == DocumentType.CartaAceptacion);

        // Formato 29 debe estar en faltantes porque fue rechazado
        var f29Missing = result.Data.MissingDocuments.FirstOrDefault(m => m.DocumentType == DocumentType.Formato29);
        Assert.NotNull(f29Missing);
        Assert.Contains("Documento devuelto/rechazado: Firma del asesor externo ilegible", f29Missing.StatusNote);
    }
}
