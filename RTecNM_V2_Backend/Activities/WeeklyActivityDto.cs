namespace TecNM.Residency.Activities;

public record WeeklyProgressDto(
    long Id,
    int WeekNumber,
    string Status,
    string? Notes
);

public record WeeklyActivityDto(
    long Id,
    long ProjectId,
    int ActivityNumber,
    string Title,
    List<WeeklyProgressDto> Progresses,
    int? StartWeek = 1,
    int? EstimatedWeeks = 1,
    int? EstimatedDays = 7
);

public record SaveWeeklyProgressDto(
    long ActivityId,
    int WeekNumber,
    string Status,
    string? Notes
);

public record CreateActivityDto(
    long ProjectId,
    int ActivityNumber,
    string Title,
    int? StartWeek = 1,
    int? EstimatedWeeks = 1,
    int? EstimatedDays = 7
);

public record UpdateActivityDto(
    string Title,
    int? StartWeek = 1,
    int? EstimatedWeeks = 1,
    int? EstimatedDays = 7
);
