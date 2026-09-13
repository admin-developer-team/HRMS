using Hrms.Application;
using Hrms.Domain;
using Hrms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure;

/// <summary>Tenant-filtered search read model. Every category is gated by the same permission as its destination.</summary>
public sealed class GlobalSearchService(HrmsDbContext db, ICurrentUser user, IWorkManagementService work) : IGlobalSearchService
{
    public async Task<GlobalSearchResponse> SearchAsync(string query, CancellationToken ct)
    {
        var term = query.Trim();
        if (term.Length < 2) return new GlobalSearchResponse([]);
        if (term.Length > 100) term = term[..100];
        var needle = term.ToLowerInvariant();
        var hits = new List<GlobalSearchHit>();

        if (user.IsPlatformAdmin && user.HasPermission(Permissions.PlatformManage))
        {
            var companies = await db.Tenants.AsNoTracking().Where(x => x.Slug != "platform" &&
                (x.Name.ToLower().Contains(needle) || x.Slug.ToLower().Contains(needle)))
                .OrderBy(x => x.Name).Take(5).ToArrayAsync(ct);
            hits.AddRange(companies.Select(x => Hit("Company", x.Name, x.Slug, "domain", "/companies", "Tenants", x.Slug)));
        }

        if (user.HasPermission(Permissions.EmployeesRead))
        {
            var people = await db.Employees.AsNoTracking().Where(x =>
                x.EmployeeNumber.ToLower().Contains(needle) || x.FirstName.ToLower().Contains(needle) ||
                x.LastName.ToLower().Contains(needle) || (x.FirstName + " " + x.LastName).ToLower().Contains(needle) ||
                x.WorkEmail.ToLower().Contains(needle))
                .OrderBy(x => x.FirstName).Take(6).ToArrayAsync(ct);
            hits.AddRange(people.Select(x => new GlobalSearchHit("Employee", x.FullName,
                $"{x.EmployeeNumber} · {x.WorkEmail}", "person", $"/employees/{x.Id}")));

            var departments = await db.Departments.AsNoTracking().Where(x => x.Name.ToLower().Contains(needle) || x.Code.ToLower().Contains(needle)).OrderBy(x => x.Name).Take(3).ToArrayAsync(ct);
            hits.AddRange(departments.Select(x => Hit("Department", x.Name, x.Code, "account_tree", "/organization", "Departments", x.Name)));
            var designations = await db.Designations.AsNoTracking().Where(x => x.Name.ToLower().Contains(needle) || x.Code.ToLower().Contains(needle)).OrderBy(x => x.Name).Take(3).ToArrayAsync(ct);
            hits.AddRange(designations.Select(x => Hit("Designation", x.Name, x.Code, "workspace_premium", "/organization", "Designations", x.Name)));
            var locations = await db.Locations.AsNoTracking().Where(x => x.Name.ToLower().Contains(needle) || x.Code.ToLower().Contains(needle) || (x.City != null && x.City.ToLower().Contains(needle))).OrderBy(x => x.Name).Take(3).ToArrayAsync(ct);
            hits.AddRange(locations.Select(x => Hit("Location", x.Name, x.City ?? x.Code, "location_on", "/organization", "Locations", x.Name)));
        }
        else if (user.HasPermission(Permissions.TeamRead) && user.EmployeeId.HasValue)
        {
            var managerId = user.EmployeeId.Value;
            var team = await db.Employees.AsNoTracking().Where(x => x.ManagerId == managerId &&
                (x.EmployeeNumber.ToLower().Contains(needle) || x.FirstName.ToLower().Contains(needle) ||
                 x.LastName.ToLower().Contains(needle) || (x.FirstName + " " + x.LastName).ToLower().Contains(needle)))
                .OrderBy(x => x.FirstName).Take(5).ToArrayAsync(ct);
            hits.AddRange(team.Select(x => Hit("Team member", x.FullName, x.EmployeeNumber, "groups", "/my-team", "Direct reports", x.FullName)));
        }

        if (user.HasPermission(Permissions.LeaveManage))
        {
            var types = await db.LeaveTypes.AsNoTracking().Where(x => x.Name.ToLower().Contains(needle) || x.Code.ToLower().Contains(needle)).OrderBy(x => x.Name).Take(3).ToArrayAsync(ct);
            hits.AddRange(types.Select(x => Hit("Leave type", x.Name, x.Code, "event_available", "/leave", "Leave types", x.Name)));
        }

        if (user.HasPermission(Permissions.WorkforceManage))
        {
            var shifts = await db.Shifts.AsNoTracking().Where(x => x.Name.ToLower().Contains(needle)).OrderBy(x => x.Name).Take(3).ToArrayAsync(ct);
            hits.AddRange(shifts.Select(x => Hit("Shift", x.Name, $"{x.StartsAt:HH:mm}–{x.EndsAt:HH:mm}", "schedule", "/workforce", "Shifts", x.Name)));
        }

        if (user.HasPermission(Permissions.WorkRead))
        {
            // WorkManagementService applies project membership and per-project read access.
            var items = await work.SearchItemsAsync(new PagedRequest(1, 6, term), null, null, null, null, ct);
            hits.AddRange(items.Items.Select(x => new GlobalSearchHit("Ticket", $"{x.Key} · {x.Summary}",
                $"{x.ProjectKey} · {x.Status}", "confirmation_number", "/work", new Dictionary<string, string> { ["item"] = x.Id.ToString() })));
            var projects = (await work.ListProjectsAsync(ct)).Where(x => Match($"{x.Key} {x.Name} {x.Description}", needle)).Take(4);
            hits.AddRange(projects.Select(x => new GlobalSearchHit("Project", $"{x.Key} · {x.Name}",
                "Work management", "folder", "/work", new Dictionary<string, string> { ["project"] = x.Id.ToString() })));
        }

        if (user.HasPermission(Permissions.RecruitmentManage))
        {
            var jobs = await db.JobOpenings.AsNoTracking().Where(x => x.Title.ToLower().Contains(needle) || x.Code.ToLower().Contains(needle) || x.Description.ToLower().Contains(needle)).OrderByDescending(x => x.CreatedAt).Take(4).ToArrayAsync(ct);
            hits.AddRange(jobs.Select(x => Hit("Job opening", x.Title, $"{x.Code} · {x.Status}", "work", "/recruitment", "Job openings", x.Code)));
            var candidates = await db.Candidates.AsNoTracking().Where(x => x.FirstName.ToLower().Contains(needle) || x.LastName.ToLower().Contains(needle) || (x.FirstName + " " + x.LastName).ToLower().Contains(needle) || x.Email.ToLower().Contains(needle)).OrderBy(x => x.FirstName).Take(4).ToArrayAsync(ct);
            hits.AddRange(candidates.Select(x => Hit("Candidate", $"{x.FirstName} {x.LastName}".Trim(), x.Email, "person_search", "/recruitment", "Candidates", x.Email)));
        }

        if (user.HasPermission(Permissions.AssetsManage) || (user.HasPermission(Permissions.SelfService) && user.EmployeeId.HasValue))
        {
            var ownedIds = user.HasPermission(Permissions.AssetsManage) ? null :
                await db.AssetAssignments.AsNoTracking().Where(x => x.EmployeeId == user.EmployeeId && x.ReturnedAt == null).Select(x => x.AssetId).ToArrayAsync(ct);
            var assets = await db.Assets.AsNoTracking().Where(x => (ownedIds == null || ownedIds.Contains(x.Id)) &&
                (x.AssetTag.ToLower().Contains(needle) || x.Name.ToLower().Contains(needle) || x.Category.ToLower().Contains(needle) || (x.SerialNumber != null && x.SerialNumber.ToLower().Contains(needle))))
                .OrderBy(x => x.Name).Take(5).ToArrayAsync(ct);
            var own = ownedIds is not null;
            hits.AddRange(assets.Select(x => Hit("Asset", x.Name, $"{x.AssetTag} · {x.Status}", "laptop_mac", own ? "/my-services" : "/assets", own ? "My assets" : "Assets", x.AssetTag)));
        }

        if (user.HasPermission(Permissions.TrainingManage) || (user.HasPermission(Permissions.SelfService) && user.EmployeeId.HasValue))
        {
            var ownCourseIds = user.HasPermission(Permissions.TrainingManage) ? null :
                await db.TrainingEnrollments.AsNoTracking().Where(x => x.EmployeeId == user.EmployeeId).Select(x => x.CourseId).ToArrayAsync(ct);
            var courses = await db.TrainingCourses.AsNoTracking().Where(x => (ownCourseIds == null || ownCourseIds.Contains(x.Id)) &&
                (x.Title.ToLower().Contains(needle) || (x.Provider != null && x.Provider.ToLower().Contains(needle))))
                .OrderBy(x => x.Title).Take(4).ToArrayAsync(ct);
            var own = ownCourseIds is not null;
            hits.AddRange(courses.Select(x => own
                ? new GlobalSearchHit("Course", x.Title, x.Provider ?? "Training", "school", "/my-services", new Dictionary<string, string> { ["view"] = "Learning" })
                : Hit("Course", x.Title, x.Provider ?? "Training", "school", "/training", "Courses", x.Title)));
        }

        if (user.HasPermission(Permissions.ExpensesManage) || (user.HasPermission(Permissions.SelfService) && user.EmployeeId.HasValue))
        {
            var own = !user.HasPermission(Permissions.ExpensesManage);
            var claims = await db.ExpenseClaims.AsNoTracking().Where(x => (!own || x.EmployeeId == user.EmployeeId) &&
                (x.ClaimNumber.ToLower().Contains(needle) || x.Category.ToLower().Contains(needle) || x.Description.ToLower().Contains(needle)))
                .OrderByDescending(x => x.ExpenseDate).Take(5).ToArrayAsync(ct);
            hits.AddRange(claims.Select(x => Hit("Expense", x.ClaimNumber, $"{x.Category} · {x.Status}", "receipt_long", own ? "/my-services" : "/expenses", own ? "Expenses" : "Claims", x.ClaimNumber)));
        }

        if (user.HasPermission(Permissions.LeaveManage) || (user.HasPermission(Permissions.SelfService) && user.EmployeeId.HasValue))
        {
            var own = !user.HasPermission(Permissions.LeaveManage);
            var requests = await db.LeaveRequests.AsNoTracking().Where(x => (!own || x.EmployeeId == user.EmployeeId) && x.Reason.ToLower().Contains(needle))
                .OrderByDescending(x => x.StartsOn).Take(4).ToArrayAsync(ct);
            hits.AddRange(requests.Select(x => Hit("Leave", x.Reason, $"{x.StartsOn:dd MMM yyyy} · {x.Status}", "beach_access", own ? "/my-services" : "/leave", own ? "Leave" : "Requests", x.Reason)));
        }

        if (user.HasPermission(Permissions.WorkforceManage) || (user.HasPermission(Permissions.SelfService) && user.EmployeeId.HasValue))
        {
            var own = !user.HasPermission(Permissions.WorkforceManage);
            var sheets = await db.TimesheetEntries.AsNoTracking().Where(x => (!own || x.EmployeeId == user.EmployeeId) &&
                (x.Description.ToLower().Contains(needle) || (x.ProjectCode != null && x.ProjectCode.ToLower().Contains(needle))))
                .OrderByDescending(x => x.WorkDate).Take(4).ToArrayAsync(ct);
            hits.AddRange(sheets.Select(x => Hit("Timesheet", x.Description, $"{x.ProjectCode ?? "Work"} · {x.WorkDate:dd MMM yyyy}", "schedule", own ? "/my-services" : "/workforce", "Timesheets", x.ProjectCode ?? x.Description)));
        }

        if (user.HasPermission(Permissions.PayrollManage))
        {
            var runs = await db.PayrollRuns.AsNoTracking().Where(x => x.Name.ToLower().Contains(needle) || x.Currency.ToLower().Contains(needle)).OrderByDescending(x => x.PeriodStart).Take(4).ToArrayAsync(ct);
            hits.AddRange(runs.Select(x => Hit("Payroll", x.Name, $"{x.PeriodStart:MMM yyyy} · {x.Status}", "payments", "/payroll", "Runs", x.Name)));
        }

        if (user.HasPermission(Permissions.IdentityManage))
        {
            var accounts = await db.Users.AsNoTracking().Where(x => x.DisplayName.ToLower().Contains(needle) || x.Email.ToLower().Contains(needle)).OrderBy(x => x.DisplayName).Take(4).ToArrayAsync(ct);
            hits.AddRange(accounts.Select(x => Hit("User", x.DisplayName, x.Email, "admin_panel_settings", "/identity", "Users", x.Email)));
            var roles = await db.Roles.AsNoTracking().Where(x => x.Name.ToLower().Contains(needle)).OrderBy(x => x.Name).Take(3).ToArrayAsync(ct);
            hits.AddRange(roles.Select(x => Hit("Role", x.Name, "Access & roles", "verified_user", "/identity", "Roles", x.Name)));
        }

        if (user.HasPermission(Permissions.PerformanceManage))
        {
            var cycles = await db.PerformanceCycles.AsNoTracking().Where(x => x.Name.ToLower().Contains(needle)).OrderByDescending(x => x.StartsOn).Take(4).ToArrayAsync(ct);
            hits.AddRange(cycles.Select(x => Hit("Performance cycle", x.Name, $"{x.StartsOn:MMM yyyy} – {x.EndsOn:MMM yyyy}", "monitoring", "/performance", "Cycles", x.Name)));
        }

        if (user.HasPermission(Permissions.WorkforceManage) || (user.HasPermission(Permissions.SelfService) && user.EmployeeId.HasValue))
        {
            var now = DateTimeOffset.UtcNow;
            var news = await db.Announcements.AsNoTracking().Where(x => (x.ExpiresAt == null || x.ExpiresAt > now) && (x.Title.ToLower().Contains(needle) || x.Body.ToLower().Contains(needle))).OrderByDescending(x => x.PublishedAt).Take(4).ToArrayAsync(ct);
            var own = !user.HasPermission(Permissions.WorkforceManage);
            hits.AddRange(news.Select(x => Hit("Announcement", x.Title, "Company update", "campaign", own ? "/my-services" : "/workforce", "Announcements", x.Title)));
        }

        if (user.HasPermission(Permissions.WorkforceManage) || user.EmployeeId.HasValue)
        {
            var locationId = user.EmployeeId.HasValue && !user.HasPermission(Permissions.WorkforceManage)
                ? await db.Employees.Where(x => x.Id == user.EmployeeId).Select(x => x.LocationId).FirstOrDefaultAsync(ct) : null;
            var holidays = await db.Holidays.AsNoTracking().Where(x => (user.HasPermission(Permissions.WorkforceManage) || !x.LocationId.HasValue || x.LocationId == locationId) && x.Name.ToLower().Contains(needle))
                .OrderBy(x => x.Date).Take(5).ToArrayAsync(ct);
            hits.AddRange(holidays.Select(x => new GlobalSearchHit("Holiday", x.Name, $"{x.Date:dd MMM yyyy} · {(x.IsOptional ? "Optional" : "Office closed")}", "event", "/calendar", new Dictionary<string, string> { ["date"] = x.Date.ToString("yyyy-MM-dd") })));
        }

        return new GlobalSearchResponse(hits.OrderByDescending(x => Rank(x, needle)).ThenBy(x => x.Kind).Take(36).ToArray());
    }

    private static bool Match(string text, string needle) => text.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static GlobalSearchHit Hit(string kind, string title, string subtitle, string icon, string route, string view, string search) =>
        new(kind, title, subtitle, icon, route, new Dictionary<string, string> { ["view"] = view, ["search"] = search });

    private static int Rank(GlobalSearchHit hit, string needle)
    {
        var title = hit.Title.ToLowerInvariant();
        var subtitle = hit.Subtitle.ToLowerInvariant();
        if (title == needle) return 1000;
        if (title.StartsWith(needle)) return 800;
        if (title.Contains(needle)) return 650;
        if (subtitle.Contains(needle)) return 400;
        return 200;
    }
}
