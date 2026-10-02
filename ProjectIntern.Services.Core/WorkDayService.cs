using ProjectIntern.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectIntern.Services.Core.Interfaces;
using ProjectIntern.Services.Core.Repository.Interfaces;
using ProjectIntern.Web.ViewModels.Admin.WorkDay;
using ProjectIntern.Web.ViewModels.ApplicationUser;
using ProjectIntern.Web.ViewModels.WorkDay;

namespace ProjectIntern.Services.Core;

public class WorkDayService : IWorkDayService
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly IGenericRepository<WorkDayAssignment> workDayAssignmentRepository;
    private readonly ILogger<WorkDayService> logger;

    public WorkDayService(UserManager<ApplicationUser> userManager,
        IGenericRepository<WorkDayAssignment> workDayAssignmentRepository,
        ILogger<WorkDayService> logger)
    {
        this.userManager = userManager;
        this.workDayAssignmentRepository = workDayAssignmentRepository;
        this.logger = logger;
    }

    public async Task CreateWorkDayAsync(Guid internId, IEnumerable<DateTime> dates, Guid adminId)
    {
        ApplicationUser applicationUser = await userManager
            .Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == internId)
            .Include(u => u.InternshipSpeciality)
                .ThenInclude(s => s!.Topics.Where(t => !t.IsDeleted))
            .Include(u => u.LastAssignedTopic)
            .FirstOrDefaultAsync()
            ?? throw new ArgumentException($"Intern with ID {internId} not found.");

        if (applicationUser.InternshipSpecialityId == null)
            throw new InvalidOperationException("Intern has no speciality assigned.");

        List<DateTime> sortedDates = dates
            .Select(d => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (applicationUser.InternshipStartDate.HasValue)
        {
            DateTime start = applicationUser.InternshipStartDate.Value.Date;
            if (sortedDates.Any(d => d < start))
                throw new InvalidOperationException("One or more dates are before the intern's start date.");
        }

        if (applicationUser.InternshipEndDate.HasValue)
        {
            DateTime end = applicationUser.InternshipEndDate.Value.Date;
            if (sortedDates.Any(d => d > end))
                throw new InvalidOperationException("One or more dates are after the intern's end date.");
        }

        List<Topic> topics = applicationUser.InternshipSpeciality!.Topics
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Order)
            .ToList();

        if (!topics.Any())
            throw new InvalidOperationException("No active topics in this speciality.");

        int lastOrder = applicationUser.LastAssignedTopic?.Order ?? -1;
        bool hasCompleted = applicationUser.LastAssignedTopicId != null
            && !topics.Any(t => t.Order > lastOrder);

        if (hasCompleted)
            throw new InvalidOperationException("Intern has completed the curriculum. No new days can be added.");

        List<WorkDayAssignment> existingWorkDays = await workDayAssignmentRepository
            .GetQueryable(asNoTracking: false, ignoreQueryFilters: true)
            .Where(w => w.InternId == internId
                        && sortedDates.Contains(w.Date))
            .ToListAsync();

        List<DateTime> alreadyActiveDates = existingWorkDays
            .Where(w => !w.IsDeleted)
            .Select(w => DateTime.SpecifyKind(w.Date, DateTimeKind.Utc))
            .ToList();

        if (alreadyActiveDates.Any())
            throw new InvalidOperationException(
                $"The following dates already have work days assigned: {string.Join(", ", alreadyActiveDates.Select(d => d.ToString("yyyy-MM-dd")))}");

        DateTime todayUtc = DateTime.UtcNow.Date;
        int neededTopicsCount = 0;

        foreach (DateTime date in sortedDates)
        {
            WorkDayAssignment? existing = existingWorkDays.FirstOrDefault(w => w.Date == date);
            if (existing == null || date > todayUtc)
            {
                neededTopicsCount++;
            }
        }

        int remainingTopics = topics.Count(t => t.Order > lastOrder);
        if (neededTopicsCount > remainingTopics)
        {
            throw new InvalidOperationException(
                $"Not enough topics remaining. {remainingTopics} topics left but {neededTopicsCount} adjustments/new days requested.");
        }

        foreach (DateTime date in sortedDates)
        {
            WorkDayAssignment? existing = existingWorkDays.FirstOrDefault(w => w.Date == date);
            Topic? nextTopic = topics.FirstOrDefault(t => t.Order > lastOrder);

            if (existing != null)
            {
                existing.IsDeleted = false;
                existing.CreatedByUserId = adminId;
                existing.CreatedAt = DateTime.UtcNow;

                if (date > todayUtc && nextTopic != null)
                {
                    existing.TopicId = nextTopic.Id;
                    lastOrder = nextTopic.Order;
                    applicationUser.LastAssignedTopicId = nextTopic.Id;
                }

                continue;
            }

            WorkDayAssignment newWorkDay = new WorkDayAssignment
            {
                Id = Guid.NewGuid(),
                Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                InternId = internId,
                TopicId = nextTopic!.Id,
                CreatedByUserId = adminId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
            };

            await workDayAssignmentRepository.AddAsync(newWorkDay);

            lastOrder = nextTopic.Order;
            applicationUser.LastAssignedTopicId = nextTopic.Id;
        }

        await userManager.UpdateAsync(applicationUser);

        try
        {
            await workDayAssignmentRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new DbUpdateException("An error occurred while saving work days.", ex);
        }
    }

    public async Task DeleteWorkDaysAsync(Guid internId, IEnumerable<DateTime> dates, Guid adminId)
    {
        ApplicationUser? applicationUser = await CheckUserExistsWithSpecialityIncluded(internId);

        List<DateTime> normalizedDates = dates
            .Select(d => DateTime.SpecifyKind(d.Date, DateTimeKind.Utc))
            .Distinct()
            .ToList();

        var workDayAssignmentsToDelete = await workDayAssignmentRepository
            .GetQueryable(asNoTracking: false)
            .Where(w => w.InternId == internId && normalizedDates.Contains(w.Date))
            .ToListAsync();

        List<DateTime> pastDates = workDayAssignmentsToDelete
            .Where(w => w.Date < DateTime.UtcNow.Date)
            .Select(w => DateTime.SpecifyKind(w.Date, DateTimeKind.Utc))
            .ToList();

        if (pastDates.Any())
        {
            logger.LogWarning("Admin {AdminId} deleted {Count} past work days for intern {InternId}",
                adminId, pastDates.Count, internId);
        }

        foreach (var workDayAssignment in workDayAssignmentsToDelete)
        {
            workDayAssignment.IsDeleted = true;
        }

        try
        {
            await workDayAssignmentRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new DbUpdateException("An error occurred while deleting work days.", ex);
        }

        WorkDayAssignment? lastRemainingDay = await workDayAssignmentRepository
            .GetQueryable(asNoTracking: true)
            .Where(w => w.InternId == internId)
            .OrderByDescending(w => w.Date)
            .FirstOrDefaultAsync();

        applicationUser.LastAssignedTopicId = lastRemainingDay?.TopicId;

        await userManager.UpdateAsync(applicationUser);
    }

    public async Task<InternCalendarAdminViewModel> GetWorkDaysForInternAsAdminAsync(Guid internId)
    {
        ApplicationUser? applicationUser = await CheckUserExistsWithSpecialityIncluded(internId);

        IOrderedQueryable<WorkDayAssignment> query = workDayAssignmentRepository
            .GetQueryable()
            .Where(w => w.InternId == internId)
            .Include(w => w.Topic)
            .OrderBy(w => w.Date);

        InternCalendarAdminViewModel result = new InternCalendarAdminViewModel
        {
            InternId = internId,
            InternName = applicationUser.Name,
            SpecialityName = applicationUser.InternshipSpeciality?.Name ?? "N/A",
            InternshipStartDate = applicationUser.InternshipStartDate,
            InternshipEndDate = applicationUser.InternshipEndDate,
            WorkDays = await query.Select(w => new WorkDayAdminViewModel
            {
                Id = w.Id,
                Date = w.Date,
                TopicName = w.Topic.Name,
                TopicDescription = w.Topic.Description
            }).ToListAsync()
        };

        return result;
    }

    public async Task<InternCalendarViewModel> GetWorkDaysForInternAsync(Guid internId)
    {
        ApplicationUser? applicationUser = await CheckUserExistsWithSpecialityIncluded(internId);

        DateTime today = DateTime.UtcNow.Date;

        IReadOnlyList<WorkDayViewModel> workDays = await workDayAssignmentRepository
            .GetQueryable()
            .Where(w => w.InternId == internId)
            .Include(w => w.Topic)
            .OrderBy(w => w.Date)
            .Select(w => new WorkDayViewModel
            {
                Id = w.Id,
                Date = w.Date,
                IsRevealed = w.Date <= today,
                TopicName = w.Date <= today ? w.Topic.Name : null,
                TopicDescription = w.Date <= today ? w.Topic.Description : null,
            })
            .ToListAsync();

        int completedTopicsCount = workDays.Count(w => w.IsRevealed);
        bool hasCompleted = applicationUser.LastAssignedTopicId != null
    && !applicationUser.InternshipSpeciality!.Topics
        .Any(t => !t.IsDeleted && t.Order > (applicationUser.LastAssignedTopic?.Order ?? -1));

        return new InternCalendarViewModel
        {
            InternId = internId,
            InternName = applicationUser.Name,
            SpecialityName = applicationUser.InternshipSpeciality?.Name ?? "N/A",
            InternshipStartDate = applicationUser.InternshipStartDate,
            InternshipEndDate = applicationUser.InternshipEndDate,
            CompletedTopicsCount = completedTopicsCount,
            WorkDays = workDays
        };
    }

    private async Task<ApplicationUser> CheckUserExistsWithSpecialityIncluded(Guid internId)
    {
        var applicationUser = await userManager
             .Users
             .IgnoreQueryFilters()
             .Where(u => u.Id == internId)
             .Include(u => u.InternshipSpeciality)
                .ThenInclude(s => s!.Topics.Where(t => !t.IsDeleted))
             .Include(u => u.LastAssignedTopic)
             .FirstOrDefaultAsync();

        if (applicationUser == null)
        {
            throw new ArgumentException($"Intern with ID {internId} not found.");
        }

        return applicationUser;
    }
}
