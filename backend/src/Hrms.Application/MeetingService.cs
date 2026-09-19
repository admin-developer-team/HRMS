using Hrms.Domain;
using Hrms.Domain.Common;

namespace Hrms.Application;

public sealed record MeetingPerson(Guid EmployeeId, string Name, string WorkEmail);
public sealed record MeetingDto(Guid Id, string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string? Location, string? MeetingUrl, Guid OrganizerUserId, string OrganizerName,
    IReadOnlyList<MeetingPerson> Attendees, DateTimeOffset? CancelledAt, bool CanEdit, long Version);
public sealed record SaveMeetingRequest(string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    string? Location, string? MeetingUrl, IReadOnlyList<Guid> AttendeeEmployeeIds, long Version = 0);

public sealed class MeetingService(
    IRepository<Meeting> meetings, IRepository<MeetingAttendee> attendees, IRepository<Employee> employees,
    IRepository<UserAccount> users, IRepository<Tenant> tenants, ICurrentTenant tenant, ICurrentUser user,
    IUnitOfWork unitOfWork, INotificationService notifications, IEmailQueue emailQueue) : ServiceBase(tenant)
{
    private Guid ActorId => user.UserId ?? throw new UnauthorizedAccessException("Sign in to manage meetings.");
    private bool CanSeeAll => user.HasPermission(Permissions.DashboardAdmin) || user.HasPermission(Permissions.IdentityManage);

    public async Task<IReadOnlyList<MeetingPerson>> CandidatesAsync(string? search, CancellationToken ct)
    {
        var term = search?.Trim().ToLowerInvariant();
        var people = await employees.ListAsync(x => x.UserId != null &&
            x.Status != EmploymentStatus.Inactive && x.Status != EmploymentStatus.Suspended &&
            x.Status != EmploymentStatus.Terminated && x.Status != EmploymentStatus.Resigned &&
            (string.IsNullOrEmpty(term) || x.FirstName.ToLower().Contains(term) || x.LastName.ToLower().Contains(term) || x.WorkEmail.ToLower().Contains(term)),
            q => q.OrderBy(x => x.FirstName).ThenBy(x => x.LastName), take: 50, cancellationToken: ct);
        var userIds = people.Select(x => x.UserId!.Value).ToArray();
        var activeIds = (await users.ListAsync(x => userIds.Contains(x.Id) && x.IsActive, cancellationToken: ct)).Select(x => x.Id).ToHashSet();
        return people.Where(x => activeIds.Contains(x.UserId!.Value)).Select(Person).ToArray();
    }

    public async Task<IReadOnlyList<MeetingDto>> ListAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (to <= from || to - from > TimeSpan.FromDays(45)) throw new DomainException("Select a calendar range of up to 45 days.");
        var rows = await meetings.ListAsync(x => x.StartsAt < to && x.EndsAt > from,
            q => q.OrderBy(x => x.StartsAt), cancellationToken: ct);
        if (rows.Count == 0) return [];
        var ids = rows.Select(x => x.Id).ToArray();
        var links = await attendees.ListAsync(x => ids.Contains(x.MeetingId), cancellationToken: ct);
        var visible = CanSeeAll ? rows : rows.Where(x => x.OrganizerUserId == ActorId ||
            (user.EmployeeId.HasValue && links.Any(a => a.MeetingId == x.Id && a.EmployeeId == user.EmployeeId.Value))).ToArray();
        return await MapAsync(visible, links, ct);
    }

    public async Task<MeetingDto> CreateAsync(SaveMeetingRequest r, CancellationToken ct)
    {
        Validate(r, true);
        var invited = await ValidateAttendees(r.AttendeeEmployeeIds, ct);
        var row = new Meeting { TenantId = TenantId, OrganizerUserId = ActorId, Title = r.Title.Trim(),
            Description = Clean(r.Description), StartsAt = r.StartsAt.ToUniversalTime(), EndsAt = r.EndsAt.ToUniversalTime(),
            Location = Clean(r.Location), MeetingUrl = Clean(r.MeetingUrl) };
        await meetings.AddAsync(row, ct);
        foreach (var person in invited)
            await attendees.AddAsync(new MeetingAttendee { TenantId = TenantId, MeetingId = row.Id, EmployeeId = person.Id }, ct);
        await Notify(invited.Select(x => x.Id), row, "invitation", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapAsync([row], await attendees.ListAsync(x => x.MeetingId == row.Id, cancellationToken: ct), ct))[0];
    }

    public async Task<MeetingDto> UpdateAsync(Guid id, SaveMeetingRequest r, CancellationToken ct)
    {
        var row = await meetings.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Meeting not found.");
        EnsureEditor(row);
        if (row.CancelledAt.HasValue) throw new DomainException("A cancelled meeting cannot be edited.");
        CheckVersion(row, r.Version);
        Validate(r, false);
        var invited = await ValidateAttendees(r.AttendeeEmployeeIds, ct);
        var current = await attendees.ListAsync(x => x.MeetingId == id, cancellationToken: ct);
        var newIds = invited.Select(x => x.Id).ToHashSet();
        var oldIds = current.Select(x => x.EmployeeId).ToHashSet();
        foreach (var link in current.Where(x => !newIds.Contains(x.EmployeeId))) attendees.Remove(link);
        foreach (var person in invited.Where(x => !oldIds.Contains(x.Id)))
            await attendees.AddAsync(new MeetingAttendee { TenantId = TenantId, MeetingId = id, EmployeeId = person.Id }, ct);
        row.Title = r.Title.Trim(); row.Description = Clean(r.Description); row.StartsAt = r.StartsAt.ToUniversalTime();
        row.EndsAt = r.EndsAt.ToUniversalTime(); row.Location = Clean(r.Location); row.MeetingUrl = Clean(r.MeetingUrl);
        await Notify(newIds, row, "updated", ct);
        var removedIds = oldIds.Except(newIds).ToHashSet();
        if (removedIds.Count > 0)
        {
            var organizerEmployeeIds = (await employees.ListAsync(x => x.UserId == row.OrganizerUserId, cancellationToken: ct))
                .Select(x => x.Id);
            removedIds.ExceptWith(organizerEmployeeIds);
            await notifications.QueueForEmployeesAsync(removedIds.Select(x => (Guid?)x), $"Removed from meeting: {row.Title}",
                "You are no longer invited to this meeting.", "meeting", "/calendar", ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        return (await MapAsync([row], await attendees.ListAsync(x => x.MeetingId == id, cancellationToken: ct), ct))[0];
    }

    public async Task CancelAsync(Guid id, long version, CancellationToken ct)
    {
        var row = await meetings.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Meeting not found.");
        EnsureEditor(row);
        CheckVersion(row, version);
        if (row.CancelledAt.HasValue) return;
        row.CancelledAt = DateTimeOffset.UtcNow;
        var links = await attendees.ListAsync(x => x.MeetingId == id, cancellationToken: ct);
        await Notify(links.Select(x => x.EmployeeId), row, "cancelled", ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<MeetingDto>> MapAsync(IEnumerable<Meeting> rows, IReadOnlyList<MeetingAttendee> links, CancellationToken ct)
    {
        var meetings = rows.ToArray();
        var employeeIds = links.Select(x => x.EmployeeId).Distinct().ToArray();
        var userIds = meetings.Select(x => x.OrganizerUserId).Distinct().ToArray();
        var people = (await employees.ListAsync(x => employeeIds.Contains(x.Id), cancellationToken: ct)).ToDictionary(x => x.Id);
        var organizerNames = (await users.ListAsync(x => userIds.Contains(x.Id), cancellationToken: ct)).ToDictionary(x => x.Id, x => x.DisplayName);
        return meetings.Select(x => new MeetingDto(x.Id, x.Title, x.Description, x.StartsAt, x.EndsAt, x.Location,
            x.MeetingUrl, x.OrganizerUserId, organizerNames.GetValueOrDefault(x.OrganizerUserId, "Organizer"),
            links.Where(a => a.MeetingId == x.Id && people.ContainsKey(a.EmployeeId)).Select(a => Person(people[a.EmployeeId])).ToArray(),
            x.CancelledAt, CanSeeAll || x.OrganizerUserId == ActorId, x.Version)).ToArray();
    }

    private async Task<IReadOnlyList<Employee>> ValidateAttendees(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids is null) throw new DomainException("Select valid meeting attendees.");
        if (ids.Count > 100 || ids.Distinct().Count() != ids.Count) throw new DomainException("Choose up to 100 distinct attendees.");
        if (ids.Count == 0) return [];
        var people = await employees.ListAsync(x => ids.Contains(x.Id) && x.UserId != null &&
            x.Status != EmploymentStatus.Inactive && x.Status != EmploymentStatus.Suspended &&
            x.Status != EmploymentStatus.Terminated && x.Status != EmploymentStatus.Resigned, cancellationToken: ct);
        var userIds = people.Select(x => x.UserId!.Value).ToArray();
        var activeUsers = (await users.ListAsync(x => userIds.Contains(x.Id) && x.IsActive, cancellationToken: ct)).Select(x => x.Id).ToHashSet();
        if (people.Count != ids.Count || people.Any(x => !activeUsers.Contains(x.UserId!.Value)))
            throw new DomainException("Select active employees with active accounts in this company.");
        return people;
    }

    private async Task Notify(IEnumerable<Guid> employeeIds, Meeting row, string action, CancellationToken ct)
    {
        var company = await tenants.GetByIdAsync(TenantId, ct);
        var zone = AttendanceCalendar.Zone(company?.TimeZone);
        var localStart = TimeZoneInfo.ConvertTime(row.StartsAt, zone);
        var localEnd = TimeZoneInfo.ConvertTime(row.EndsAt, zone);
        var where = row.MeetingUrl ?? row.Location;
        var message = action == "cancelled" ? $"The meeting scheduled for {localStart:ddd, d MMM yyyy h:mm tt} ({zone.Id}) has been cancelled."
            : $"{row.Title} is {(action == "updated" ? "now scheduled" : "scheduled")} for {localStart:ddd, d MMM yyyy h:mm tt}–{localEnd:h:mm tt} ({zone.Id})." +
              (string.IsNullOrEmpty(where) ? "" : $" Join or meet at: {where}");
        var title = $"Meeting {action}: {row.Title}";
        var organizer = await users.GetByIdAsync(row.OrganizerUserId, ct);
        var organizerEmployeeIds = (await employees.ListAsync(x => x.UserId == row.OrganizerUserId, cancellationToken: ct))
            .Select(x => x.Id).ToHashSet();
        await notifications.QueueForEmployeesAsync(employeeIds.Where(x => !organizerEmployeeIds.Contains(x)).Select(x => (Guid?)x),
            title, message, "meeting", "/calendar", ct);
        if (organizer is { IsActive: true })
            await emailQueue.QueueAsync(organizer.Email, organizer.DisplayName, EmailTemplateKeys.ForNotification("meeting"),
                new Dictionary<string, string?> { ["title"] = title, ["message"] = message, ["link"] = "/calendar" }, ct);
    }

    private void EnsureEditor(Meeting row)
    {
        if (row.OrganizerUserId != ActorId && !CanSeeAll) throw new UnauthorizedAccessException("Only the organizer or an administrator can change this meeting.");
    }

    private static void Validate(SaveMeetingRequest r, bool creating)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length is < 3 or > 150) throw new DomainException("Meeting title must be 3 to 150 characters.");
        if (r.Description?.Length > 4000 || r.Location?.Length > 300 || r.MeetingUrl?.Length > 1000)
            throw new DomainException("Meeting details are too long.");
        if (r.EndsAt <= r.StartsAt || r.EndsAt - r.StartsAt > TimeSpan.FromHours(24))
            throw new DomainException("Meeting end must follow start and be within 24 hours.");
        if (creating && r.StartsAt < DateTimeOffset.UtcNow.AddMinutes(-1)) throw new DomainException("Schedule a meeting in the future.");
        if (!string.IsNullOrWhiteSpace(r.MeetingUrl) && (!Uri.TryCreate(r.MeetingUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainException("Use a secure https meeting link.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static MeetingPerson Person(Employee x) => new(x.Id, x.FullName, x.WorkEmail);
}
