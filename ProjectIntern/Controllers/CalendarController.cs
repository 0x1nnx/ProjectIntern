using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectIntern.GCommon;
using ProjectIntern.Services.Core.Interfaces;
using ProjectIntern.Web.ViewModels.Admin.Requests;
using ProjectIntern.Web.ViewModels.ApplicationUser;
using System.Security.Claims;

namespace ProjectIntern.Controllers;

[Authorize]
public class CalendarController : Controller
{
    private readonly IWorkDayService workDayService;
    private readonly ILogger<CalendarController> logger;

    public CalendarController(IWorkDayService workDayService, ILogger<CalendarController> logger)
    {
        this.workDayService = workDayService;
        this.logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }

        Guid internId = GetCurrentUserId();

        try
        {
            InternCalendarViewModel model = await workDayService.GetWorkDaysForInternAsync(internId);
            return View(model);
        }
        catch (ArgumentException)
        {
            TempData["ErrorMessage"] = "Your account could not be found.";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading calendar for intern {InternId}", internId);
            TempData["ErrorMessage"] = "Could not load your calendar at this time.";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddDays([FromBody] WorkDayBatchRequestModel request)
    {
        return HandleInternRequest(request, "add work days", "Work days added successfully.",
            (internId, dates, startTime, endTime) =>
                workDayService.CreateWorkDaysAsInternAsync(internId, dates, startTime, endTime));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateHours([FromBody] WorkDayBatchRequestModel request)
    {
        return HandleInternRequest(request, "update work hours", "Work hours updated successfully.",
            (internId, dates, startTime, endTime) =>
                workDayService.UpdateWorkDayHoursAsInternAsync(internId, dates, startTime, endTime));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveDays([FromBody] WorkDayBatchRequestModel request)
    {
        return HandleInternRequest(request, "remove work days", "Work days removed successfully.",
            (internId, dates, _, _) =>
                workDayService.DeleteWorkDaysAsInternAsync(internId, dates));
    }

    private async Task<IActionResult> HandleInternRequest(
        WorkDayBatchRequestModel? request,
        string actionDescription,
        string successMessage,
        Func<Guid, IEnumerable<DateTime>, TimeOnly?, TimeOnly?, Task> action)
    {
        if (User.IsInRole("Admin"))
            return Forbid();

        if (request == null || !request.Dates.Any())
            return BadRequest(new { message = "No dates provided." });

        // Interns can only manage their own calendar; any InternId in the request body is ignored.
        Guid internId = GetCurrentUserId();

        try
        {
            List<DateTime> dates = request.Dates
                .Select(d => DateTime.SpecifyKind(DateTime.Parse(d), DateTimeKind.Utc))
                .ToList();

            TimeOnly? startTime = WorkingHoursParser.Parse(request.StartTime);
            TimeOnly? endTime = WorkingHoursParser.Parse(request.EndTime);

            await action(internId, dates, startTime, endTime);
            return Ok(new { message = successMessage });
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "Invalid date format." });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Business rule violation for intern {InternId} trying to {Action}", internId, actionDescription);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error for intern {InternId} trying to {Action}", internId, actionDescription);
            return StatusCode(500, new { message = $"An error occurred while trying to {actionDescription}." });
        }
    }

    private Guid GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out Guid id))
            throw new InvalidOperationException("Could not resolve current user ID.");

        return id;
    }
}