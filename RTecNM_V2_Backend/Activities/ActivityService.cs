using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Activities;

public class ActivityService : IActivityService
{
    private readonly IActivityRepository _repository;
    private readonly IProjectRepository _projectRepository;

    public ActivityService(IActivityRepository repository, IProjectRepository projectRepository)
    {
        _repository = repository;
        _projectRepository = projectRepository;
    }

    public async Task<Result<List<WeeklyActivityDto>>> GetScheduleByProjectIdAsync(long projectId)
    {
        var activities = await _repository.GetByProjectIdAsync(projectId);
        
        var dtos = activities.Select(a => MapToDto(a)).ToList();
        return Result<List<WeeklyActivityDto>>.Success(dtos);
    }

    public async Task<Result<WeeklyActivityDto>> CreateActivityAsync(CreateActivityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<WeeklyActivityDto>.Failure("La descripción de la actividad es obligatoria.");

        int startWeek = Math.Max(1, Math.Min(26, dto.StartWeek ?? 1));
        int estimatedWeeks = Math.Max(1, Math.Min(26, dto.EstimatedWeeks ?? (dto.EstimatedDays.HasValue ? (int)Math.Ceiling(dto.EstimatedDays.Value / 7.0) : 1)));
        int estimatedDays = dto.EstimatedDays ?? (estimatedWeeks * 7);
        int endWeek = Math.Min(26, startWeek + estimatedWeeks - 1);

        var activity = new WeeklyActivity
        {
            ProjectId = dto.ProjectId,
            ActivityNumber = dto.ActivityNumber,
            Title = dto.Title.Trim(),
            StartWeek = startWeek,
            EstimatedWeeks = estimatedWeeks,
            EstimatedDays = estimatedDays,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Generar al menos 16 semanas (o hasta endWeek si es mayor)
        int totalWeeks = Math.Max(16, endWeek);
        for (int w = 1; w <= totalWeeks; w++)
        {
            bool inRange = (w >= startWeek && w <= endWeek);
            activity.Progresses.Add(new WeeklyProgress
            {
                WeekNumber = w,
                Status = inRange ? "in_progress" : "pending",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        var created = await _repository.CreateActivityAsync(activity);
        return Result<WeeklyActivityDto>.Success(MapToDto(created));
    }

    public async Task<Result<WeeklyActivityDto>> UpdateActivityAsync(long activityId, UpdateActivityDto dto)
    {
        var activity = await _repository.GetByIdAsync(activityId);
        if (activity == null)
            return Result<WeeklyActivityDto>.Failure("Actividad no encontrada.", 404);

        if (!string.IsNullOrWhiteSpace(dto.Title))
            activity.Title = dto.Title.Trim();

        int startWeek = Math.Max(1, Math.Min(26, dto.StartWeek ?? activity.StartWeek ?? 1));
        int estimatedWeeks = Math.Max(1, Math.Min(26, dto.EstimatedWeeks ?? (dto.EstimatedDays.HasValue ? (int)Math.Ceiling(dto.EstimatedDays.Value / 7.0) : activity.EstimatedWeeks ?? 1)));
        int estimatedDays = dto.EstimatedDays ?? (estimatedWeeks * 7);
        int endWeek = Math.Min(26, startWeek + estimatedWeeks - 1);

        activity.StartWeek = startWeek;
        activity.EstimatedWeeks = estimatedWeeks;
        activity.EstimatedDays = estimatedDays;
        activity.UpdatedAt = DateTime.UtcNow;

        // Revalidar y ajustar progreso preservando semanas ya completadas o con notas previas
        var existingByWeek = activity.Progresses.ToDictionary(p => p.WeekNumber);
        int maxWeek = Math.Max(16, Math.Max(activity.Progresses.Any() ? activity.Progresses.Max(p => p.WeekNumber) : 16, endWeek));

        for (int w = 1; w <= maxWeek; w++)
        {
            bool inNewRange = (w >= startWeek && w <= endWeek);

            if (existingByWeek.TryGetValue(w, out var existing))
            {
                // NUNCA sobreescribir semanas ya completadas ni semanas con notas existentes del alumno
                if (existing.Status == "completed" || existing.Status == "completado" || !string.IsNullOrWhiteSpace(existing.Notes))
                {
                    continue;
                }

                if (inNewRange && existing.Status == "pending")
                {
                    existing.Status = "in_progress";
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else if (!inNewRange && existing.Status == "in_progress")
                {
                    existing.Status = "pending";
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                activity.Progresses.Add(new WeeklyProgress
                {
                    ActivityId = activity.Id,
                    WeekNumber = w,
                    Status = inNewRange ? "in_progress" : "pending",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _repository.UpdateActivityAsync(activity);
        return Result<WeeklyActivityDto>.Success(MapToDto(activity));
    }

    public async Task<Result<bool>> SaveWeeklyProgressAsync(SaveWeeklyProgressDto dto)
    {
        if (dto.WeekNumber < 1 || dto.WeekNumber > 26)
            return Result<bool>.Failure("El número de semana debe estar entre 1 y 26.");

        var activity = await _repository.GetByIdAsync(dto.ActivityId);
        if (activity == null)
            return Result<bool>.Failure("Actividad no encontrada.", 404);

        var validStatuses = new[] { "pending", "in_progress", "completed", "pendiente", "en_proceso", "completado" };
        if (!validStatuses.Contains(dto.Status.ToLowerInvariant()))
            return Result<bool>.Failure($"Estado '{dto.Status}' no válido.");

        var progress = new WeeklyProgress
        {
            ActivityId = dto.ActivityId,
            WeekNumber = dto.WeekNumber,
            Status = dto.Status.ToLowerInvariant(),
            Notes = dto.Notes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.SaveProgressAsync(progress);
        return Result<bool>.Success(true);
    }

    private static WeeklyActivityDto MapToDto(WeeklyActivity a)
    {
        var progresses = a.Progresses
            .Where(p => p.IsActive)
            .OrderBy(p => p.WeekNumber)
            .Select(p => new WeeklyProgressDto(
                p.Id,
                p.WeekNumber,
                p.Status,
                p.Notes
            ))
            .ToList();

        // Ensure at least 16 weeks exist in response (minimum TecNM requirement)
        int minWeeks = 16;
        int maxWeekInProgress = progresses.Any() ? progresses.Max(p => p.WeekNumber) : minWeeks;
        int totalWeeksToEnsure = Math.Max(minWeeks, Math.Min(26, maxWeekInProgress));

        if (progresses.Count < totalWeeksToEnsure)
        {
            var existingWeeks = progresses.Select(p => p.WeekNumber).ToHashSet();
            for (int w = 1; w <= totalWeeksToEnsure; w++)
            {
                if (!existingWeeks.Contains(w))
                {
                    progresses.Add(new WeeklyProgressDto(0, w, "pending", null));
                }
            }
            progresses = progresses.OrderBy(p => p.WeekNumber).ToList();
        }

        var activeProgresses = progresses.Where(p => p.Status != "pending" && p.Status != "pendiente").ToList();
        int fallbackStart = activeProgresses.Any() ? activeProgresses.Min(p => p.WeekNumber) : 1;
        int fallbackEstimatedWeeks = activeProgresses.Any() ? (activeProgresses.Max(p => p.WeekNumber) - fallbackStart + 1) : 1;

        int startWeek = a.StartWeek ?? fallbackStart;
        int estimatedWeeks = a.EstimatedWeeks ?? fallbackEstimatedWeeks;
        int estimatedDays = a.EstimatedDays ?? (estimatedWeeks * 7);

        return new WeeklyActivityDto(
            a.Id,
            a.ProjectId,
            a.ActivityNumber,
            a.Title,
            progresses,
            startWeek,
            estimatedWeeks,
            estimatedDays
        );
    }
}
