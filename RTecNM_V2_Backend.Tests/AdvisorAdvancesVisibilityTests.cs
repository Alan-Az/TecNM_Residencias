using Microsoft.EntityFrameworkCore;
using Moq;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Companies;
using TecNM.Residency.Documents;
using TecNM.Residency.Evaluations;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;
using Xunit;

namespace RTecNM_V2_Backend.Tests;

public class AdvisorAdvancesVisibilityTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Simulation_WhenProjectAdvisorIdIsNull_ButStudentHasAdvisor_AdvisorIsForbiddenFromSeeingAdvances()
    {
        // ARRANGE: Configuración de la BD y entidades
        using var db = CreateInMemoryDbContext();

        // 1. Crear usuario y perfil de Asesor
        var advisorUser = new User
        {
            Email = "profesor.asesor@monclova.tecnm.mx",
            PasswordHash = "hash",
            Role = UserRole.Advisor,
            IsActive = true
        };
        db.Users.Add(advisorUser);
        await db.SaveChangesAsync();

        var advisor = new Advisor
        {
            UserId = advisorUser.Id,
            FullName = "Ing. Roberto Asesor",
            DepartmentId = 1,
            IsActive = true
        };
        db.Advisors.Add(advisor);
        await db.SaveChangesAsync();

        // 2. Crear usuario y perfil de Estudiante asignado al asesor
        var studentUser = new User
        {
            Email = "alumno.residente@monclova.tecnm.mx",
            PasswordHash = "hash",
            Role = UserRole.Student,
            IsActive = true
        };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student
        {
            UserId = studentUser.Id,
            User = studentUser,
            ControlNumber = "20680999",
            FirstName = "Carlos",
            LastName = "López",
            CareerId = 1,
            AdvisorId = advisor.Id, // Vinculado al ID real del asesor
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        // 3. Crear empresa receptora
        var company = new Company
        {
            Name = "Empresa de Prueba SA de CV",
            IsActive = true
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        // 4. Crear Anteproyecto del alumno donde AdvisorId quedó NULL en la tabla projects
        var project = new Project
        {
            Title = "Implementación ERP para Monclova",
            StudentId = student.Id,
            Student = student,
            CompanyId = company.Id,
            Company = company,
            AdvisorId = null, // <<-- DESINCRONIZADO: projects.advisor_id es NULL
            Status = ProjectStatus.InProgress,
            IsActive = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        // 5. El estudiante sube su avance (avance_1)
        var avanceDoc = new Document
        {
            Id = 50,
            ProjectId = project.Id,
            DocumentType = DocumentType.Avance1,
            FileName = "Primer_Avance_Residencia_Carlos.pdf",
            FilePath = "documents/100_avance_1.pdf",
            Status = DocumentStatus.Uploaded,
            IsActive = true
        };
        db.Documents.Add(avanceDoc);
        await db.SaveChangesAsync();

        // 6. Mock del contexto de usuario como Asesor autenticado
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(advisorUser.Id);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Advisor);
        currentUserMock.Setup(u => u.IsInRole(UserRole.Advisor)).Returns(true);
        currentUserMock.Setup(u => u.IsInRole(UserRole.Student)).Returns(false);
        currentUserMock.Setup(u => u.IsInRole(UserRole.Admin)).Returns(false);
        currentUserMock.Setup(u => u.IsInRole(UserRole.CareerHead)).Returns(false);

        // Repositorios reales sobre InMemory
        var projectRepo = new ProjectRepository(db, currentUserMock.Object);
        var studentRepo = new StudentRepository(db, currentUserMock.Object);
        var advisorRepo = new AdvisorRepository(db, currentUserMock.Object);
        var companyRepo = new CompanyRepository(db);
        var docRepo = new DocumentRepository(db);
        var evalRepo = new EvaluationRepository(db, currentUserMock.Object);

        var projectService = new ProjectService(
            projectRepo,
            currentUserMock.Object,
            studentRepo,
            advisorRepo,
            companyRepo,
            docRepo,
            evalRepo);

        // DIAGNÓSTICO:
        var advisorProfile = await advisorRepo.GetByUserIdAsync(advisorUser.Id);
        Assert.NotNull(advisorProfile);
        Assert.Equal(advisor.Id, advisorProfile.Id);

        var dbProject = await db.Projects.Include(p => p.Student).FirstOrDefaultAsync(p => p.Id == project.Id);
        Assert.NotNull(dbProject);
        Assert.NotNull(dbProject.Student);
        Assert.Equal(advisor.Id, dbProject.Student.AdvisorId);

        // ACT: El asesor intenta acceder al anteproyecto o consultar sus documentos/avances
        // Esto invoca internamente CanAccessProjectAsync(projectId) en:
        // - GET /api/v1/documents/project/{projectId} (Expediente y avances de residencia)
        // - GET /api/v1/projects/{projectId}/activities (Cronograma de actividades y avances semanales)
        // - GET /api/v1/evaluations/project/{projectId} (Evaluaciones de avance)
        var accessResult = await projectService.CanAccessProjectAsync(project.Id);

        // ASSERT: VERIFICACIÓN DEL FIX
        // CanAccessProjectAsync devuelve TRUE porque evalúa project.Student.AdvisorId == advisor.Id
        // y auto-sincroniza project.AdvisorId = advisor.Id
        Assert.True(accessResult.IsSuccess);
        
        var refreshedProject = await db.Projects.FindAsync(project.Id);
        Assert.Equal(advisor.Id, refreshedProject!.AdvisorId);
    }

    [Fact]
    public async Task Simulation_WhenProjectAdvisorIdIsSynchronized_AdvisorCanAccessProjectAndSeeAdvances()
    {
        // ARRANGE: Caso donde projects.advisor_id sí está sincronizado con el asesor
        using var db = CreateInMemoryDbContext();

        var advisorUser = new User
        {
            Email = "profesor.asesor@monclova.tecnm.mx",
            Role = UserRole.Advisor,
            IsActive = true
        };
        db.Users.Add(advisorUser);
        await db.SaveChangesAsync();

        var advisor = new Advisor
        {
            UserId = advisorUser.Id,
            FullName = "Ing. Roberto Asesor",
            DepartmentId = 1,
            IsActive = true
        };
        db.Advisors.Add(advisor);
        await db.SaveChangesAsync();

        var studentUser = new User
        {
            Email = "alumno.residente@monclova.tecnm.mx",
            Role = UserRole.Student,
            IsActive = true
        };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student
        {
            UserId = studentUser.Id,
            ControlNumber = "20680999",
            FirstName = "Carlos",
            LastName = "López",
            CareerId = 1,
            AdvisorId = advisor.Id,
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var company = new Company { Name = "Empresa SA", IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        // Proyecto con AdvisorId correctamente asignado
        var project = new Project
        {
            Title = "Proyecto Sincronizado",
            StudentId = student.Id,
            CompanyId = company.Id,
            AdvisorId = advisor.Id, // <<-- Sincronizado correctamente
            Status = ProjectStatus.InProgress,
            IsActive = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var avanceDoc = new Document
        {
            Id = 51,
            ProjectId = project.Id,
            DocumentType = DocumentType.Avance1,
            FileName = "Primer_Avance_Residencia_Carlos.pdf",
            FilePath = "documents/101_avance_1.pdf",
            Status = DocumentStatus.Uploaded,
            IsActive = true
        };
        db.Documents.Add(avanceDoc);
        await db.SaveChangesAsync();

        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(advisorUser.Id);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Advisor);
        currentUserMock.Setup(u => u.IsInRole(UserRole.Advisor)).Returns(true);

        var projectRepo = new ProjectRepository(db, currentUserMock.Object);
        var studentRepo = new StudentRepository(db, currentUserMock.Object);
        var advisorRepo = new AdvisorRepository(db, currentUserMock.Object);
        var companyRepo = new CompanyRepository(db);
        var docRepo = new DocumentRepository(db);
        var evalRepo = new EvaluationRepository(db, currentUserMock.Object);

        var projectService = new ProjectService(
            projectRepo,
            currentUserMock.Object,
            studentRepo,
            advisorRepo,
            companyRepo,
            docRepo,
            evalRepo);

        // ACT: Asesor intenta acceder al proyecto
        var accessResult = await projectService.CanAccessProjectAsync(project.Id);
        var docsResult = await docRepo.GetPagedByProjectIdAsync(project.Id, new PaginationQuery { PageNumber = 1, PageSize = 10 });

        // ASSERT: Tiene acceso exitoso y los avances son visibles
        Assert.True(accessResult.IsSuccess);
        Assert.True(accessResult.Data);
        Assert.Single(docsResult.Items);
        Assert.Equal(DocumentType.Avance1, docsResult.Items.First().DocumentType);
    }
}
