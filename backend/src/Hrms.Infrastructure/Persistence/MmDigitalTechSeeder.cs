using Hrms.Application;
using Hrms.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure.Persistence;

public sealed record MmDigitalTechSeedResult(string TenantSlug, string Password, IReadOnlyList<string> Accounts, bool Created);

public sealed class MmDigitalTechSeeder(
    HrmsDbContext db, ICurrentTenant currentTenant, IPasswordHasher passwordHasher)
{
    public const string TenantSlug = "mm-digital-tech";
    public const string DemoPassword = "Demo@12345";
    private static readonly Guid TenantId = Guid.Parse("6d6d6469-6769-7461-6c74-656368000001");

    public async Task<MmDigitalTechSeedResult> SeedAsync(CancellationToken ct = default)
    {
        var emails = new[]
        {
            "admin@mmdigitaltechmkt.com",
            "hr@mmdigitaltechmkt.com",
            "payroll@mmdigitaltechmkt.com",
            "marketing.manager@mmdigitaltechmkt.com",
            "tech.manager@mmdigitaltechmkt.com",
            "seo@mmdigitaltechmkt.com",
            "social.media@mmdigitaltechmkt.com",
            "content@mmdigitaltechmkt.com",
            "designer@mmdigitaltechmkt.com",
            "developer@mmdigitaltechmkt.com"
        };

        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Slug == TenantSlug, ct))
            return new MmDigitalTechSeedResult(TenantSlug, DemoPassword, emails, false);

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tenant = new Tenant
        {
            Id = TenantId,
            Name = "MM Digital Tech",
            LegalName = "MM Digital Tech Marketing",
            Slug = TenantSlug,
            TaxIdentifier = "TEST-GSTIN-MM-DIGITAL-TECH",
            DefaultCurrency = "INR",
            TimeZone = "Asia/Kolkata",
            Locale = "en-IN",
            Status = TenantStatus.Active,
            SettingsJson = """{"website":"https://mmdigitaltechmkt.com","phone":"+91 7011195988","email":"marketing@mmdigitaltechmkt.com","address":"399 GF, Sector-1, Vaishali, Ghaziabad","financialYearStartMonth":4,"workWeek":["Monday","Tuesday","Wednesday","Thursday","Friday"]}"""
        };
        db.Tenants.Add(tenant);
        currentTenant.Set(TenantId, TenantSlug);
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = TenantId, PlanCode = "enterprise-demo", EmployeeLimit = 100,
            StartsAt = now, EndsAt = now.AddYears(1), IsActive = true
        });

        var roles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);
        var tenantAdmin = new Role
        {
            TenantId = TenantId, Name = "Tenant Administrator", NormalizedName = "TENANT_ADMIN",
            PermissionsCsv = Permissions.All, IsSystem = true
        };
        roles[tenantAdmin.NormalizedName] = tenantAdmin;
        db.Roles.Add(tenantAdmin);
        foreach (var definition in Permissions.TenantSystemRoles)
        {
            var role = new Role
            {
                TenantId = TenantId, Name = definition.Name, NormalizedName = definition.NormalizedName,
                PermissionsCsv = string.Join(',', definition.Permissions), IsSystem = true
            };
            roles[role.NormalizedName] = role;
            db.Roles.Add(role);
        }

        UserAccount AddUser(string name, string email)
        {
            var user = new UserAccount
            {
                TenantId = TenantId, DisplayName = name, Email = email,
                PasswordHash = passwordHasher.Hash(DemoPassword), IsActive = true
            };
            db.Users.Add(user);
            return user;
        }

        void Grant(UserAccount user, params string[] roleNames)
        {
            foreach (var roleName in roleNames)
                db.UserRoles.Add(new UserRole { TenantId = TenantId, UserId = user.Id, RoleId = roles[roleName].Id });
        }

        var adminUser = AddUser("Mohit Sharma", emails[0]);
        var hrUser = AddUser("Ananya Verma", emails[1]);
        var payrollUser = AddUser("Rohit Gupta", emails[2]);
        var marketingManagerUser = AddUser("Neha Singh", emails[3]);
        var techManagerUser = AddUser("Arjun Mehta", emails[4]);
        var seoUser = AddUser("Priya Kapoor", emails[5]);
        var socialUser = AddUser("Sakshi Jain", emails[6]);
        var contentUser = AddUser("Aman Khan", emails[7]);
        var designerUser = AddUser("Riya Patel", emails[8]);
        var developerUser = AddUser("Vikram Das", emails[9]);
        Grant(adminUser, "TENANT_ADMIN");
        Grant(hrUser, "HR_ADMINISTRATOR", "EMPLOYEE_SELF_SERVICE");
        Grant(payrollUser, "PAYROLL_ADMINISTRATOR", "EMPLOYEE_SELF_SERVICE");
        Grant(marketingManagerUser, "PEOPLE_MANAGER", "WORK_COORDINATOR", "EMPLOYEE_SELF_SERVICE");
        Grant(techManagerUser, "PEOPLE_MANAGER", "WORK_COORDINATOR", "EMPLOYEE_SELF_SERVICE");
        foreach (var user in new[] { seoUser, socialUser, contentUser, designerUser, developerUser })
            Grant(user, "EMPLOYEE_SELF_SERVICE", "WORK_CONTRIBUTOR");

        var office = new Location
        {
            TenantId = TenantId, Name = "Ghaziabad Head Office", Code = "GZB-HQ",
            Address = "399 GF, Sector-1, Vaishali", City = "Ghaziabad", CountryCode = "IN"
        };
        var remote = new Location { TenantId = TenantId, Name = "India Remote", Code = "IN-REMOTE", Address = "Remote workplace", CountryCode = "IN" };
        db.Locations.AddRange(office, remote);

        var leadership = new Department { TenantId = TenantId, Name = "Leadership", Code = "LEAD" };
        var marketing = new Department { TenantId = TenantId, Name = "Digital Marketing", Code = "MKT" };
        var creative = new Department { TenantId = TenantId, Name = "Creative & Content", Code = "CREATIVE" };
        var technology = new Department { TenantId = TenantId, Name = "Web Technology", Code = "TECH" };
        var operations = new Department { TenantId = TenantId, Name = "People & Finance", Code = "OPS" };
        db.Departments.AddRange(leadership, marketing, creative, technology, operations);

        Designation Des(string name, string code, int level, string description)
        {
            var d = new Designation { TenantId = TenantId, Name = name, Code = code, Level = level, Description = description };
            db.Designations.Add(d);
            return d;
        }
        var directorDes = Des("Managing Director", "MD", 8, "Company strategy and administration");
        var hrDes = Des("HR Executive", "HR-EXEC", 4, "People operations and employee lifecycle");
        var payrollDes = Des("Finance & Payroll Executive", "FIN-EXEC", 4, "Payroll and financial operations");
        var marketingLeadDes = Des("Digital Marketing Manager", "MKT-MGR", 6, "Campaign strategy and team leadership");
        var techLeadDes = Des("Technology Manager", "TECH-MGR", 6, "Web delivery and technical leadership");
        var seoDes = Des("SEO Specialist", "SEO-SP", 3, "Search optimization and analytics");
        var socialDes = Des("Social Media Executive", "SOCIAL", 3, "Social campaigns and community management");
        var contentDes = Des("Content Writer", "CONTENT", 3, "Marketing and website content");
        var designerDes = Des("Graphic Designer", "DESIGN", 3, "Campaign creative and brand assets");
        var developerDes = Des("Web Developer", "WEB-DEV", 4, "Websites, landing pages and integrations");

        Employee AddEmployee(string number, UserAccount user, string first, string last, Department department,
            Designation designation, Location location, decimal salary, string phone, Guid? managerId = null)
        {
            var employee = new Employee
            {
                TenantId = TenantId, EmployeeNumber = number, UserId = user.Id, FirstName = first, LastName = last,
                WorkEmail = user.Email, Phone = phone, HireDate = today.AddYears(-2), Status = EmploymentStatus.Active,
                EmploymentType = EmploymentType.Permanent, DepartmentId = department.Id, DesignationId = designation.Id,
                LocationId = location.Id, ManagerId = managerId, BaseSalary = salary, SalaryCurrency = "INR",
                BankAccountMasked = "XXXXXX" + number[^4..], AddressJson = """{"city":"Ghaziabad","state":"Uttar Pradesh","country":"India"}"""
            };
            db.Employees.Add(employee);
            return employee;
        }

        var admin = AddEmployee("MM1001", adminUser, "Mohit", "Sharma", leadership, directorDes, office, 1800000, "+91 7011195988");
        var hr = AddEmployee("MM1002", hrUser, "Ananya", "Verma", operations, hrDes, office, 600000, "+91 9000010002", admin.Id);
        var payroll = AddEmployee("MM1003", payrollUser, "Rohit", "Gupta", operations, payrollDes, office, 650000, "+91 9000010003", admin.Id);
        var marketingManager = AddEmployee("MM1004", marketingManagerUser, "Neha", "Singh", marketing, marketingLeadDes, office, 1100000, "+91 9000010004", admin.Id);
        var techManager = AddEmployee("MM1005", techManagerUser, "Arjun", "Mehta", technology, techLeadDes, office, 1200000, "+91 9000010005", admin.Id);
        var seo = AddEmployee("MM1006", seoUser, "Priya", "Kapoor", marketing, seoDes, office, 550000, "+91 9000010006", marketingManager.Id);
        var social = AddEmployee("MM1007", socialUser, "Sakshi", "Jain", marketing, socialDes, office, 500000, "+91 9000010007", marketingManager.Id);
        var content = AddEmployee("MM1008", contentUser, "Aman", "Khan", creative, contentDes, remote, 520000, "+91 9000010008", marketingManager.Id);
        var designer = AddEmployee("MM1009", designerUser, "Riya", "Patel", creative, designerDes, office, 580000, "+91 9000010009", marketingManager.Id);
        var developer = AddEmployee("MM1010", developerUser, "Vikram", "Das", technology, developerDes, remote, 850000, "+91 9000010010", techManager.Id);
        leadership.HeadEmployeeId = admin.Id;
        operations.HeadEmployeeId = hr.Id;
        marketing.HeadEmployeeId = marketingManager.Id;
        creative.HeadEmployeeId = designer.Id;
        technology.HeadEmployeeId = techManager.Id;

        var employees = new[] { admin, hr, payroll, marketingManager, techManager, seo, social, content, designer, developer };
        db.EmployeeEmergencyContacts.AddRange(
            new EmployeeEmergencyContact { TenantId = TenantId, EmployeeId = seo.Id, Name = "Raj Kapoor", Relationship = "Parent", Phone = "+91 9888801006", IsPrimary = true },
            new EmployeeEmergencyContact { TenantId = TenantId, EmployeeId = developer.Id, Name = "Anita Das", Relationship = "Spouse", Phone = "+91 9888801010", IsPrimary = true });

        db.Shifts.AddRange(
            new Shift { TenantId = TenantId, Name = "General shift", StartsAt = new TimeOnly(9, 30), EndsAt = new TimeOnly(18, 0), GraceMinutes = 15 },
            new Shift { TenantId = TenantId, Name = "Flexible remote shift", StartsAt = new TimeOnly(10, 0), EndsAt = new TimeOnly(18, 30), GraceMinutes = 15 });
        db.AttendancePolicies.Add(new AttendancePolicy
        {
            TenantId = TenantId, OfficeStartsAt = new TimeOnly(9, 30), OfficeEndsAt = new TimeOnly(18, 0),
            RequiredMinutesPerDay = 510, LateGraceMinutes = 15, EarlyDepartureGraceMinutes = 10,
            WorkingDaysCsv = "Monday,Tuesday,Wednesday,Thursday,Friday", RequireLocationCapture = false
        });
        db.Holidays.AddRange(
            new Holiday { TenantId = TenantId, Name = "Independence Day", Date = new DateOnly(today.Year, 8, 15), LocationId = office.Id },
            new Holiday { TenantId = TenantId, Name = "Diwali", Date = new DateOnly(today.Year, 11, 8), LocationId = office.Id });

        var annual = new LeaveType { TenantId = TenantId, Name = "Annual Leave", Code = "ANNUAL", AnnualAllowance = 18, IsPaid = true, MaxConsecutiveDays = 10 };
        var sick = new LeaveType { TenantId = TenantId, Name = "Sick Leave", Code = "SICK", AnnualAllowance = 10, IsPaid = true, RequiresDocument = true, MaxConsecutiveDays = 5 };
        var casual = new LeaveType { TenantId = TenantId, Name = "Casual Leave", Code = "CASUAL", AnnualAllowance = 6, IsPaid = true, MaxConsecutiveDays = 2 };
        db.LeaveTypes.AddRange(annual, sick, casual);
        foreach (var employee in employees)
            db.LeaveBalances.AddRange(
                new LeaveBalance { TenantId = TenantId, EmployeeId = employee.Id, LeaveTypeId = annual.Id, Year = today.Year, Entitled = 18, Used = 1 },
                new LeaveBalance { TenantId = TenantId, EmployeeId = employee.Id, LeaveTypeId = sick.Id, Year = today.Year, Entitled = 10 },
                new LeaveBalance { TenantId = TenantId, EmployeeId = employee.Id, LeaveTypeId = casual.Id, Year = today.Year, Entitled = 6 });
        db.LeaveRequests.Add(new LeaveRequest { TenantId = TenantId, EmployeeId = seo.Id, LeaveTypeId = casual.Id, StartsOn = today.AddDays(7), EndsOn = today.AddDays(7), Days = 1, Reason = "Personal appointment", Status = LeaveRequestStatus.Pending });
        var yesterday = today.AddDays(-1);
        foreach (var employee in employees.Skip(1))
        {
            var start = new DateTimeOffset(yesterday.ToDateTime(new TimeOnly(9, 28)), TimeSpan.FromHours(5.5)).ToUniversalTime();
            db.AttendanceRecords.Add(new AttendanceRecord { TenantId = TenantId, EmployeeId = employee.Id, WorkDate = yesterday, ClockedInAt = start, ClockedOutAt = start.AddHours(8.5), WorkHours = 8.5m, Status = employee.LocationId == remote.Id ? AttendanceStatus.Remote : AttendanceStatus.Present, Source = "Demo seed" });
        }
        db.TimesheetEntries.AddRange(
            new TimesheetEntry { TenantId = TenantId, EmployeeId = seo.Id, WorkDate = yesterday, ProjectCode = "MMD-WEB", Description = "SEO audit and keyword research", Hours = 8, Status = WorkflowStatus.Pending },
            new TimesheetEntry { TenantId = TenantId, EmployeeId = developer.Id, WorkDate = yesterday, ProjectCode = "MMD-WEB", Description = "Landing page optimization", Hours = 8, Status = WorkflowStatus.Approved });

        var periodStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var payrollRun = new PayrollRun { TenantId = TenantId, Name = periodStart.ToString("MMMM yyyy") + " payroll", PeriodStart = periodStart, PeriodEnd = periodEnd, PaymentDate = periodEnd.AddDays(3), Status = PayrollRunStatus.Approved, Currency = "INR" };
        db.PayrollRuns.Add(payrollRun);
        foreach (var employee in employees)
        {
            var basic = decimal.Round(employee.BaseSalary / 12m, 2);
            var allowances = decimal.Round(basic * .15m, 2);
            var deductions = decimal.Round(basic * .05m, 2);
            var taxes = decimal.Round(basic * .08m, 2);
            var gross = basic + allowances;
            var net = gross - deductions - taxes;
            payrollRun.GrossTotal += gross; payrollRun.DeductionTotal += deductions + taxes; payrollRun.NetTotal += net;
            db.PayrollItems.Add(new PayrollItem { TenantId = TenantId, PayrollRunId = payrollRun.Id, EmployeeId = employee.Id, BasicPay = basic, Allowances = allowances, Deductions = deductions, Taxes = taxes, GrossPay = gross, NetPay = net, BreakdownJson = """{"demoOnly":true,"allowanceRate":0.15}""" });
        }

        var cycle = new PerformanceCycle { TenantId = TenantId, Name = today.Year + " Annual Review", StartsOn = new DateOnly(today.Year, 4, 1), EndsOn = new DateOnly(today.Year, 12, 31), IsActive = true };
        db.PerformanceCycles.Add(cycle);
        db.PerformanceReviews.AddRange(
            new PerformanceReview { TenantId = TenantId, CycleId = cycle.Id, EmployeeId = seo.Id, ReviewerId = marketingManager.Id, Status = ReviewStatus.ManagerReview, SelfRating = 4.1m, GoalsJson = """[{"goal":"Improve organic traffic","progress":70}]""" },
            new PerformanceReview { TenantId = TenantId, CycleId = cycle.Id, EmployeeId = developer.Id, ReviewerId = techManager.Id, Status = ReviewStatus.SelfReview, SelfRating = 4.0m, GoalsJson = """[{"goal":"Improve website conversion","progress":65}]""" });

        var laptop = new Asset { TenantId = TenantId, AssetTag = "MMD-LAP-001", Name = "Dell Latitude Laptop", Category = "Laptop", SerialNumber = "DEMO-MMD-001", PurchasedOn = today.AddMonths(-8), PurchaseCost = 78000, Status = AssetStatus.Assigned };
        var camera = new Asset { TenantId = TenantId, AssetTag = "MMD-CAM-001", Name = "Sony Content Camera", Category = "Camera", SerialNumber = "DEMO-MMD-CAM", PurchasedOn = today.AddMonths(-5), PurchaseCost = 92000, Status = AssetStatus.Assigned };
        db.Assets.AddRange(laptop, camera);
        db.AssetAssignments.AddRange(
            new AssetAssignment { TenantId = TenantId, AssetId = laptop.Id, EmployeeId = developer.Id, AssignedAt = now.AddMonths(-6), Notes = "Primary development device" },
            new AssetAssignment { TenantId = TenantId, AssetId = camera.Id, EmployeeId = social.Id, AssignedAt = now.AddMonths(-4), Notes = "Campaign content production" });
        db.ExpenseClaims.AddRange(
            new ExpenseClaim { TenantId = TenantId, EmployeeId = social.Id, ClaimNumber = $"MMD-EXP-{today.Year}-001", Category = "Advertising", ExpenseDate = today.AddDays(-5), Amount = 3500, Currency = "INR", Description = "Campaign creative promotion", Status = ExpenseStatus.Submitted },
            new ExpenseClaim { TenantId = TenantId, EmployeeId = developer.Id, ClaimNumber = $"MMD-EXP-{today.Year}-002", Category = "Software", ExpenseDate = today.AddDays(-8), Amount = 2400, Currency = "INR", Description = "Development tooling", Status = ExpenseStatus.Approved, ReviewedBy = techManagerUser.Id });

        var course = new TrainingCourse { TenantId = TenantId, Title = "Digital Advertising & Data Privacy", Provider = "MM Digital Tech Academy", Description = "Campaign governance, privacy and responsible data handling", DurationHours = 3, IsMandatory = true, ExpiresOn = today.AddMonths(10) };
        db.TrainingCourses.Add(course);
        foreach (var employee in employees.Skip(1))
            db.TrainingEnrollments.Add(new TrainingEnrollment { TenantId = TenantId, CourseId = course.Id, EmployeeId = employee.Id, Status = employee == seo ? EnrollmentStatus.Completed : EnrollmentStatus.Enrolled, EnrolledAt = now.AddDays(-10), CompletedAt = employee == seo ? now.AddDays(-2) : null, Score = employee == seo ? 94 : null });
        db.Announcements.AddRange(
            new Announcement { TenantId = TenantId, Title = "Welcome to the MM Digital Tech workspace", Body = "Use this workspace for attendance, leave, projects and employee services.", PublishedAt = now, ExpiresAt = now.AddDays(30), Audience = "all" },
            new Announcement { TenantId = TenantId, Title = "Weekly campaign review", Body = "Campaign review is scheduled every Friday at 4:00 PM IST.", PublishedAt = now, ExpiresAt = now.AddDays(60), Audience = "all" });

        var job = new JobOpening { TenantId = TenantId, Title = "Performance Marketing Executive", Code = "MKT-PME-01", DepartmentId = marketing.Id, HiringManagerId = marketingManager.Id, Openings = 2, Description = "Plan and optimize paid digital campaigns.", Status = JobStatus.Open, ClosesOn = today.AddDays(30) };
        var candidate = new Candidate { TenantId = TenantId, FirstName = "Kavya", LastName = "Rao", Email = "kavya.rao@example.test", Phone = "+91 9888822110", Source = "Company website" };
        db.JobOpenings.Add(job); db.Candidates.Add(candidate);
        db.JobApplications.Add(new JobApplication { TenantId = TenantId, JobOpeningId = job.Id, CandidateId = candidate.Id, Stage = CandidateStage.Interview, Rating = 4.2m, Notes = "First interview scheduled", AppliedAt = now.AddDays(-4) });

        var project = new WorkProject { TenantId = TenantId, Key = "MMD-WEB", Name = "Website growth and lead generation", Description = "SEO, content, social campaigns and conversion-focused website improvements.", LeadEmployeeId = marketingManager.Id, NextItemNumber = 4, IsActive = true };
        db.WorkProjects.Add(project);
        foreach (var employee in new[] { marketingManager, techManager, seo, social, content, designer, developer })
            db.WorkProjectMembers.Add(new WorkProjectMember { TenantId = TenantId, ProjectId = project.Id, EmployeeId = employee.Id, CanCreateItems = true, CanAssignItems = employee == marketingManager || employee == techManager, CanTransitionItems = true, CanLogWork = true, CanViewAllWorklogs = employee == marketingManager || employee == techManager });
        var items = new[]
        {
            new WorkItem { TenantId = TenantId, ProjectId = project.Id, Number = 1, Key = "MMD-WEB-1", Type = WorkItemType.Epic, Summary = "Increase qualified lead generation", Description = "Coordinate SEO, paid media, content and landing-page improvements.", Status = WorkItemStatus.InProgress, Priority = WorkItemPriority.High, ReporterEmployeeId = marketingManager.Id, AssigneeEmployeeId = marketingManager.Id, DueDate = today.AddDays(30), OriginalEstimateMinutes = 2400, RemainingEstimateMinutes = 1500, StoryPoints = 13, LabelsCsv = "growth,leads" },
            new WorkItem { TenantId = TenantId, ProjectId = project.Id, Number = 2, Key = "MMD-WEB-2", Type = WorkItemType.Task, Summary = "Complete technical SEO audit", Status = WorkItemStatus.InProgress, Priority = WorkItemPriority.High, ReporterEmployeeId = marketingManager.Id, AssigneeEmployeeId = seo.Id, DueDate = today.AddDays(7), OriginalEstimateMinutes = 480, RemainingEstimateMinutes = 240, StoryPoints = 5, LabelsCsv = "seo,audit" },
            new WorkItem { TenantId = TenantId, ProjectId = project.Id, Number = 3, Key = "MMD-WEB-3", Type = WorkItemType.Story, Summary = "Build conversion landing page", Status = WorkItemStatus.ToDo, Priority = WorkItemPriority.Critical, ReporterEmployeeId = techManager.Id, AssigneeEmployeeId = developer.Id, DueDate = today.AddDays(12), OriginalEstimateMinutes = 960, RemainingEstimateMinutes = 960, StoryPoints = 8, LabelsCsv = "website,conversion" }
        };
        db.WorkItems.AddRange(items);

        await db.SaveChangesAsync(ct);
        currentTenant.Clear();
        return new MmDigitalTechSeedResult(TenantSlug, DemoPassword, emails, true);
    }
}
