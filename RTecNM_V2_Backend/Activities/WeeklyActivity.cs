using TecNM.Residency.Common;

namespace TecNM.Residency.Activities;

public class WeeklyActivity : BaseEntity
{
    public long ProjectId { get; set; }
    public int ActivityNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? StartWeek { get; set; }
    public int? EstimatedWeeks { get; set; }
    public int? EstimatedDays { get; set; }

    public ICollection<WeeklyProgress> Progresses { get; set; } = new List<WeeklyProgress>();
}
