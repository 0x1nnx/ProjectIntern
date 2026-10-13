namespace ProjectIntern.Web.ViewModels.WorkDay;

public class WorkDayViewModel
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public bool IsRevealed { get; set; }
    public string? TopicName { get; set; }
    public string? TopicDescription { get; set; }
}
