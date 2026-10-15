using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TecNM.Residency.Advisors;
using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public static class CareerHeadAdvisorRemediation
{
    public static async Task RemediateCareerHeadAdvisorsAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            var careerHeads = await context.CareerHeads
                .Include(ch => ch.User)
                .Where(ch => ch.IsActive)
                .ToListAsync();

            if (!careerHeads.Any())
                return;

            int syncedCount = 0;
            foreach (var ch in careerHeads)
            {
                var advisor = await context.Advisors.FirstOrDefaultAsync(a => a.UserId == ch.UserId);
                if (advisor == null)
                {
                    var newAdvisor = new Advisor
                    {
                        UserId = ch.UserId,
                        FullName = ch.FullName,
                        Title = ch.Title ?? "Jefe de Carrera",
                        DepartmentId = ch.CareerId > 0 ? ch.CareerId : 1,
                        Phone = ch.Phone,
                        AdvisorType = AdvisorType.Internal,
                        IsActive = true,
                        IsVisible = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.Advisors.Add(newAdvisor);
                    await context.SaveChangesAsync();

                    var deptLink = await context.AdvisorDepartments
                        .FirstOrDefaultAsync(ad => ad.AdvisorId == newAdvisor.Id && ad.DepartmentId == newAdvisor.DepartmentId);
                    if (deptLink == null)
                    {
                        context.AdvisorDepartments.Add(new AdvisorDepartment
                        {
                            AdvisorId = newAdvisor.Id,
                            DepartmentId = newAdvisor.DepartmentId,
                            IsActive = true,
                            IsVisible = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                        await context.SaveChangesAsync();
                    }

                    syncedCount++;
                    logger.LogInformation("[CareerHeadAdvisorRemediation] Creado perfil dual de Asesor para Jefe de Carrera: {FullName} (UserId: {UserId})", ch.FullName, ch.UserId);
                }
                else if (!advisor.IsActive)
                {
                    advisor.IsActive = true;
                    advisor.UpdatedAt = DateTime.UtcNow;
                    context.Advisors.Update(advisor);
                    await context.SaveChangesAsync();
                    syncedCount++;
                    logger.LogInformation("[CareerHeadAdvisorRemediation] Reactivado perfil dual de Asesor para Jefe de Carrera: {FullName} (UserId: {UserId})", ch.FullName, ch.UserId);
                }
            }

            if (syncedCount > 0)
            {
                logger.LogInformation("[CareerHeadAdvisorRemediation] Sincronización completada. Total perfiles duales creados/reactivados: {Count}", syncedCount);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CareerHeadAdvisorRemediation] Error al sincronizar perfiles duales de asesor para Jefes de Carrera.");
        }
    }
}
