namespace ProjectIntern.Web.ViewModels.Admin.Requests;

public class WorkDayBatchRequestModel
{
    public Guid InternId { get; set; }
    public List<string> Dates { get; set; } = new();
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}
