using ProjectIntern.Web.ViewModels.Admin.WorkDay;
using ProjectIntern.Web.ViewModels.ApplicationUser;

namespace ProjectIntern.Services.Core.Interfaces;

public interface IWorkDayService
{
    public Task<InternCalendarAdminViewModel> GetWorkDaysForInternAsAdminAsync(Guid internId);
    public Task<InternCalendarViewModel> GetWorkDaysForInternAsync(Guid internId);
    public Task CreateWorkDayAsync(Guid internId, IEnumerable<DateTime> dates, TimeOnly? startTime, TimeOnly? endTime, Guid createdByUserId);
    public Task UpdateWorkDayHoursAsync(Guid internId, IEnumerable<DateTime> dates, TimeOnly? startTime, TimeOnly? endTime);
    public Task DeleteWorkDaysAsync(Guid internId, IEnumerable<DateTime> dates, Guid adminId);
    public Task CreateWorkDaysAsInternAsync(Guid internId, IEnumerable<DateTime> dates, TimeOnly? startTime, TimeOnly? endTime);
    public Task UpdateWorkDayHoursAsInternAsync(Guid internId, IEnumerable<DateTime> dates, TimeOnly? startTime, TimeOnly? endTime);
    public Task DeleteWorkDaysAsInternAsync(Guid internId, IEnumerable<DateTime> dates);
}
