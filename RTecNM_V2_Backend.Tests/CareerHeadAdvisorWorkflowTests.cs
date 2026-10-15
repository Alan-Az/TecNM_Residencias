using Microsoft.EntityFrameworkCore;
using Moq;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Notifications;
using TecNM.Residency.Evaluations;
using TecNM.Residency.Notifications;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;
using Xunit;

namespace RTecNM_V2_Backend.Tests;

public class CareerHeadAdvisorWorkflowTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task EnsureCareerHeadProfile_AutomaticallySynchronizes_AdvisorRecord()
    {
        using var db = CreateInMemoryDbContext();

        var user = new User
        {
            Email = "jefe.sistemas@tecnm.mx",
            FirstName = "Jefe",
            LastName = "Sistemas",
            Role = UserRole.CareerHead,
            CareerId = 1,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var roleRepo = new RoleRepository(db);
        await roleRepo.EnsureCareerHeadProfileAsync(
            user.Id,
            user.Email,
            "Ing. Jefe Carrera Sistemas",
            "Ing.",
            1,
            "1234567890",
            null,
            null
        );

        var advisor = await db.Advisors.FirstOrDefaultAsync(a => a.UserId == user.Id);
        Assert.NotNull(advisor);
        Assert.Equal("Ing. Jefe Carrera Sistemas", advisor.FullName);
        Assert.Equal(AdvisorType.Internal, advisor.AdvisorType);
        Assert.True(advisor.IsActive);
    }

    [Fact]
    public async Task CleanupProfilesForUser_PreservesAdvisorRecord_ForCareerHead()
    {
        using var db = CreateInMemoryDbContext();

        var user = new User
        {
            Email = "jefe.industrial@tecnm.mx",
            FirstName = "Jefa",
            LastName = "Industrial",
            Role = UserRole.CareerHead,
            CareerId = 2,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var advisor = new Advisor
        {
            UserId = user.Id,
            FullName = "Mtra. Jefa Industrial",
            AdvisorType = AdvisorType.Internal,
            IsActive = true
        };
        db.Advisors.Add(advisor);
        await db.SaveChangesAsync();

        var roleRepo = new RoleRepository(db);
        await roleRepo.CleanupProfilesForUserAsync(user.Id, UserRole.CareerHead);

        var existingAdvisor = await db.Advisors.FirstOrDefaultAsync(a => a.UserId == user.Id);
        Assert.NotNull(existingAdvisor);
        Assert.True(existingAdvisor.IsActive);
    }

    [Fact]
    public async Task StudentRepository_AllowsCareerHeadToRetrieveAdvisedStudent_FromAnotherCareer()
    {
        using var db = CreateInMemoryDbContext();

        var chUser = new User
        {
            Email = "jefe.electronica@tecnm.mx",
            Role = UserRole.CareerHead,
            CareerId = 1,
            IsActive = true
        };
        db.Users.Add(chUser);
        await db.SaveChangesAsync();

        var chAdvisor = new Advisor
        {
            UserId = chUser.Id,
            FullName = "Jefe Asesor",
            AdvisorType = AdvisorType.Internal,
            IsActive = true
        };
        db.Advisors.Add(chAdvisor);
        await db.SaveChangesAsync();

        var studentUser = new User
        {
            Email = "alumno.mecanica@tecnm.mx",
            Role = UserRole.Student,
            IsActive = true
        };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        // Estudiante de carrera 2 (Mecánica), pero asesorado por Jefe de carrera 1
        var student = new Student
        {
            UserId = studentUser.Id,
            ControlNumber = "2026MEC001",
            FirstName = "Carlos",
            LastName = "Mecánico",
            CareerId = 2,
            AdvisorId = chAdvisor.Id,
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.UserId).Returns(chUser.Id);
        mockCurrentUser.Setup(u => u.Role).Returns(UserRole.CareerHead);
        mockCurrentUser.Setup(u => u.CareerId).Returns(1); // Carrera 1

        var studentRepo = new StudentRepository(db, mockCurrentUser.Object);

        var retrievedStudent = await studentRepo.GetByIdAsync(student.Id);
        Assert.NotNull(retrievedStudent);
        Assert.Equal("2026MEC001", retrievedStudent.ControlNumber);
    }

    [Fact]
    public async Task CareerHead_CanGradeEvaluation_ForAdvisedStudentOfAnotherCareer()
    {
        using var db = CreateInMemoryDbContext();

        var chUser = new User
        {
            Email = "jefe.quimica@tecnm.mx",
            Role = UserRole.CareerHead,
            CareerId = 1,
            IsActive = true
        };
        db.Users.Add(chUser);
        await db.SaveChangesAsync();

        var chAdvisor = new Advisor
        {
            UserId = chUser.Id,
            FullName = "Dra. Jefa Asesora",
            AdvisorType = AdvisorType.Internal,
            IsActive = true
        };
        db.Advisors.Add(chAdvisor);
        await db.SaveChangesAsync();

        var studentUser = new User
        {
            Email = "alumno.sistemas@tecnm.mx",
            Role = UserRole.Student,
            IsActive = true
        };
        db.Users.Add(studentUser);
        await db.SaveChangesAsync();

        var student = new Student
        {
            UserId = studentUser.Id,
            ControlNumber = "2026SIS005",
            FirstName = "Laura",
            LastName = "Sistemas",
            CareerId = 2, // Otra carrera
            AdvisorId = chAdvisor.Id,
            IsActive = true
        };
        db.Students.Add(student);
        await db.SaveChangesAsync();

        var project = new Project
        {
            Title = "Proyecto Interdisciplinario",
            StudentId = student.Id,
            Student = student,
            AdvisorId = chAdvisor.Id,
            Status = ProjectStatus.InProgress,
            IsActive = true
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(u => u.UserId).Returns(chUser.Id);
        mockCurrentUser.Setup(u => u.Role).Returns(UserRole.CareerHead);
        mockCurrentUser.Setup(u => u.IsInRole(UserRole.CareerHead)).Returns(true);
        mockCurrentUser.Setup(u => u.IsInRole(UserRole.Advisor)).Returns(false);
        mockCurrentUser.Setup(u => u.CareerId).Returns(1);

        var mockEvalRepo = new Mock<IEvaluationRepository>();
        mockEvalRepo.Setup(r => r.SaveEvaluationAsync(It.IsAny<Evaluation>()))
            .ReturnsAsync((Evaluation e) => e);

        var mockAdvisorRepo = new Mock<IAdvisorRepository>();
        mockAdvisorRepo.Setup(r => r.GetByUserIdAsync(chUser.Id)).ReturnsAsync(chAdvisor);

        var mockProjectRepo = new Mock<IProjectRepository>();
        mockProjectRepo.Setup(r => r.GetByIdAsync(project.Id)).ReturnsAsync(project);
        mockProjectRepo.Setup(r => r.UpdateAsync(It.IsAny<Project>())).Returns(Task.CompletedTask);

        var mockNotification = new Mock<INotificationService>();
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
            chAdvisor.Id,
            "partial_1",
            98,
            "Excelente desempeño en residencia"
        );

        var result = await evalService.GradeEvaluationAsync(dto);

        Assert.True(result.IsSuccess);
        Assert.Equal(98, result.Data!.Score);
    }
}
