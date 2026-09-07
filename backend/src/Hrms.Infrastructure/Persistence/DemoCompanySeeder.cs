using Hrms.Application;
using Hrms.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hrms.Infrastructure.Persistence;

public sealed record DemoCompanySeedResult(string TenantSlug, string Password, IReadOnlyList<string> Accounts, bool Created);

public sealed class DemoCompanySeeder(
    HrmsDbContext db, ICurrentTenant currentTenant, IPasswordHasher passwordHasher)
{
    public const string TenantSlug = "northstar-demo";
    public const string DemoPassword = "Demo@12345";
    private static readonly Guid TenantId = Guid.Parse("8f3a7de1-2c51-4bfa-9e67-101010101010");

    public async Task<DemoCompanySeedResult> SeedAsync(CancellationToken ct = default)
    {
        var accountEmails = new[]
        {
            "admin@northstar.demo",
            "priya.hr@northstar.demo",
            "arjun.payroll@northstar.demo",
            "neha.manager@northstar.demo",
            "rohan.dev@northstar.demo",
            "meera.qa@northstar.demo"
        };
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Slug == TenantSlug, ct))
            return new DemoCompanySeedResult(TenantSlug, DemoPassword, accountEmails, false);

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tenant = new Tenant
        {
            Id = TenantId, Name = "Northstar Digital Solutions", Slug = TenantSlug,
            LegalName = "Northstar Digital Solutions Private Limited", TaxIdentifier = "DEMO-GSTIN-29ABCDE1234F1Z5",
            DefaultCurrency = "INR", TimeZone = "Asia/Kolkata", Locale = "en-IN",
            Status = TenantStatus.Active, SettingsJson = """{"financialYearStartMonth":4,"workWeek":["Monday","Tuesday","Wednesday","Thursday","Friday"]}"""
        };
        db.Tenants.Add(tenant);
        currentTenant.Set(TenantId, TenantSlug);
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = TenantId, PlanCode = "enterprise-demo", EmployeeLimit = 250,
            StartsAt = now.AddMonths(-3), EndsAt = now.AddMonths(9), IsActive = true
        });

        var roleMap = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);
        var tenantAdmin = new Role
        {
            TenantId = TenantId, Name = "Tenant Administrator", NormalizedName = "TENANT_ADMIN",
            PermissionsCsv = Permissions.All, IsSystem = true
        };
        roleMap[tenantAdmin.NormalizedName] = tenantAdmin;
        db.Roles.Add(tenantAdmin);
        foreach (var definition in Permissions.TenantSystemRoles)
        {
            var role = new Role
            {
                TenantId = TenantId, Name = definition.Name, NormalizedName = definition.NormalizedName,
                PermissionsCsv = string.Join(',', definition.Permissions), IsSystem = true
            };
            roleMap[role.NormalizedName] = role;
            db.Roles.Add(role);
        }

        UserAccount User(string name, string email)
        {
            var user = new UserAccount
            {
                TenantId = TenantId, DisplayName = name, Email = email,
                PasswordHash = passwordHasher.Hash(DemoPassword), IsActive = true
            };
            db.Users.Add(user);
            return user;
        }
        void Grant(UserAccount account, params string[] roles)
        {
            foreach (var roleName in roles)
                db.UserRoles.Add(new UserRole
                {
                    TenantId = TenantId, UserId = account.Id, RoleId = roleMap[roleName].Id
                });
        }

        var adminUser = User("Aditi Sharma", accountEmails[0]);
        var hrUser = User("Priya Kapoor", accountEmails[1]);
        var payrollUser = User("Arjun Mehta", accountEmails[2]);
        var managerUser = User("Neha Singh", accountEmails[3]);
        var developerUser = User("Rohan Das", accountEmails[4]);
        var qaUser = User("Meera Iyer", accountEmails[5]);
        Grant(adminUser, "TENANT_ADMIN");
        Grant(hrUser, "HR_ADMINISTRATOR", "EMPLOYEE_SELF_SERVICE");
        Grant(payrollUser, "PAYROLL_ADMINISTRATOR", "EMPLOYEE_SELF_SERVICE");
        Grant(managerUser, "PEOPLE_MANAGER", "WORK_COORDINATOR");
        Grant(developerUser, "EMPLOYEE_SELF_SERVICE", "WORK_CONTRIBUTOR");
        Grant(qaUser, "EMPLOYEE_SELF_SERVICE", "WORK_CONTRIBUTOR");

        var bengaluru = new Location
        {
            TenantId = TenantId, Name = "Bengaluru Head Office", Code = "BLR-HQ",
            Address = "100 Demo Tech Park, Outer Ring Road", City = "Bengaluru", CountryCode = "IN"
        };
        var remote = new Location
        {
            TenantId = TenantId, Name = "India Remote", Code = "IN-REMOTE",
            Address = "Remote workplace", CountryCode = "IN"
        };
        db.Locations.AddRange(bengaluru, remote);

        var executive = new Department { TenantId = TenantId, Name = "Executive", Code = "EXEC" };
        var people = new Department { TenantId = TenantId, Name = "People Operations", Code = "PEOPLE" };
        var engineering = new Department { TenantId = TenantId, Name = "Engineering", Code = "ENG" };
        var finance = new Department { TenantId = TenantId, Name = "Finance", Code = "FIN" };
        db.Departments.AddRange(executive, people, engineering, finance);

        var adminDesignation = new Designation { TenantId = TenantId, Name = "Operations Director", Code = "OPS-DIR", Level = 7, Description = "Tenant administration and operations leadership" };
        var hrDesignation = new Designation { TenantId = TenantId, Name = "HR Business Partner", Code = "HRBP", Level = 5, Description = "People operations and employee lifecycle" };
        var payrollDesignation = new Designation { TenantId = TenantId, Name = "Payroll Specialist", Code = "PAY-SP", Level = 4, Description = "Payroll operations and controls" };
        var managerDesignation = new Designation { TenantId = TenantId, Name = "Engineering Manager", Code = "ENG-MGR", Level = 6, Description = "Engineering delivery and people management" };
        var developerDesignation = new Designation { TenantId = TenantId, Name = "Software Engineer", Code = "SWE", Level = 4, Description = "Product engineering" };
        var qaDesignation = new Designation { TenantId = TenantId, Name = "Quality Engineer", Code = "QA", Level = 4, Description = "Product quality and release validation" };
        db.Designations.AddRange(adminDesignation, hrDesignation, payrollDesignation, managerDesignation, developerDesignation, qaDesignation);

        Employee Employee(string number, UserAccount account, string first, string last, Guid departmentId, Guid designationId, Guid locationId, decimal salary, Guid? managerId = null)
        {
            var employee = new Employee
            {
                TenantId = TenantId, EmployeeNumber = number, UserId = account.Id, FirstName = first, LastName = last,
                WorkEmail = account.Email, Phone = "+91 90000 " + number[^4..], HireDate = today.AddYears(-2),
                Status = EmploymentStatus.Active, EmploymentType = EmploymentType.Permanent,
                DepartmentId = departmentId, DesignationId = designationId, LocationId = locationId,
                ManagerId = managerId, BaseSalary = salary, SalaryCurrency = "INR",
                BankAccountMasked = "XXXXXX" + number[^4..], AddressJson = """{"city":"Bengaluru","country":"India"}"""
            };
            db.Employees.Add(employee);
            return employee;
        }

        var admin = Employee("NS1001", adminUser, "Aditi", "Sharma", executive.Id, adminDesignation.Id, bengaluru.Id, 2400000);
        var hr = Employee("NS1002", hrUser, "Priya", "Kapoor", people.Id, hrDesignation.Id, bengaluru.Id, 1500000, admin.Id);
        var payroll = Employee("NS1003", payrollUser, "Arjun", "Mehta", finance.Id, payrollDesignation.Id, bengaluru.Id, 1200000, admin.Id);
        var manager = Employee("NS1004", managerUser, "Neha", "Singh", engineering.Id, managerDesignation.Id, bengaluru.Id, 2200000, admin.Id);
        var developer = Employee("NS1005", developerUser, "Rohan", "Das", engineering.Id, developerDesignation.Id, remote.Id, 1400000, manager.Id);
        var qa = Employee("NS1006", qaUser, "Meera", "Iyer", engineering.Id, qaDesignation.Id, bengaluru.Id, 1300000, manager.Id);
        executive.HeadEmployeeId = admin.Id;
        people.HeadEmployeeId = hr.Id;
        finance.HeadEmployeeId = payroll.Id;
        engineering.HeadEmployeeId = manager.Id;

        db.EmployeeEmergencyContacts.AddRange(
            new EmployeeEmergencyContact { TenantId = TenantId, EmployeeId = developer.Id, Name = "Ananya Das", Relationship = "Spouse", Phone = "+91 98888 11005", IsPrimary = true },
            new EmployeeEmergencyContact { TenantId = TenantId, EmployeeId = qa.Id, Name = "Kiran Iyer", Relationship = "Sibling", Phone = "+91 98888 11006", IsPrimary = true });
        db.EmployeeDocuments.AddRange(
            new EmployeeDocument { TenantId = TenantId, EmployeeId = developer.Id, DocumentType = "Employment agreement", FileName = "rohan-employment-agreement.pdf", StorageKey = "demo/ns1005/employment-agreement", ContentType = "application/pdf", Status = DocumentStatus.Verified },
            new EmployeeDocument { TenantId = TenantId, EmployeeId = qa.Id, DocumentType = "Identity proof", FileName = "meera-identity-proof.pdf", StorageKey = "demo/ns1006/identity-proof", ContentType = "application/pdf", Status = DocumentStatus.Pending });

        var generalShift = new Shift
        {
            TenantId = TenantId, Name = "General shift", StartsAt = new TimeOnly(9, 30),
            EndsAt = new TimeOnly(18, 0), GraceMinutes = 15
        };
        db.Shifts.Add(generalShift);
        db.Holidays.AddRange(
            new Holiday { TenantId = TenantId, Name = "Independence Day", Date = new DateOnly(today.Year, 8, 15), LocationId = bengaluru.Id },
            new Holiday { TenantId = TenantId, Name = "Regional optional holiday", Date = new DateOnly(today.Year, 11, 1), LocationId = bengaluru.Id, IsOptional = true });

        var annualLeave = new LeaveType { TenantId = TenantId, Name = "Annual Leave", Code = "ANNUAL", AnnualAllowance = 20, IsPaid = true, MaxConsecutiveDays = 10 };
        var sickLeave = new LeaveType { TenantId = TenantId, Name = "Sick Leave", Code = "SICK", AnnualAllowance = 10, IsPaid = true, RequiresDocument = true, MaxConsecutiveDays = 5 };
        var casualLeave = new LeaveType { TenantId = TenantId, Name = "Casual Leave", Code = "CASUAL", AnnualAllowance = 6, IsPaid = true, MaxConsecutiveDays = 2 };
        db.LeaveTypes.AddRange(annualLeave, sickLeave, casualLeave);
        foreach (var employee in new[] { admin, hr, payroll, manager, developer, qa })
        {
            db.LeaveBalances.AddRange(
                new LeaveBalance { TenantId = TenantId, EmployeeId = employee.Id, LeaveTypeId = annualLeave.Id, Year = today.Year, Entitled = 20, Used = employee == developer ? 3 : 1, Pending = 0 },
                new LeaveBalance { TenantId = TenantId, EmployeeId = employee.Id, LeaveTypeId = sickLeave.Id, Year = today.Year, Entitled = 10, Used = 1, Pending = 0 },
                new LeaveBalance { TenantId = TenantId, EmployeeId = employee.Id, LeaveTypeId = casualLeave.Id, Year = today.Year, Entitled = 6, Used = 0, Pending = employee == qa ? 1 : 0 });
        }
        db.LeaveRequests.AddRange(
            new LeaveRequest { TenantId = TenantId, EmployeeId = qa.Id, LeaveTypeId = casualLeave.Id, StartsOn = today.AddDays(7), EndsOn = today.AddDays(7), Days = 1, Reason = "Personal appointment", Status = LeaveRequestStatus.Pending },
            new LeaveRequest { TenantId = TenantId, EmployeeId = developer.Id, LeaveTypeId = annualLeave.Id, StartsOn = today.AddDays(-20), EndsOn = today.AddDays(-18), Days = 3, Reason = "Family travel", Status = LeaveRequestStatus.Approved, ReviewedBy = managerUser.Id, ReviewedAt = now.AddDays(-22), ReviewComment = "Approved" });

        db.AttendancePolicies.Add(new AttendancePolicy
        {
            TenantId = TenantId,
            OfficeStartsAt = new TimeOnly(9, 30),
            OfficeEndsAt = new TimeOnly(18, 30),
            RequiredMinutesPerDay = 540,
            LateGraceMinutes = 10,
            EarlyDepartureGraceMinutes = 5,
            WorkingDaysCsv = "Monday,Tuesday,Wednesday,Thursday,Friday"
        });

        var clockIn = new DateTimeOffset(today.AddDays(-1).ToDateTime(new TimeOnly(9, 26)), TimeSpan.FromHours(5.5)).ToUniversalTime();
        db.AttendanceRecords.AddRange(
            new AttendanceRecord { TenantId = TenantId, EmployeeId = developer.Id, WorkDate = today.AddDays(-1), ClockedInAt = clockIn, ClockedOutAt = clockIn.AddHours(8.6), Status = AttendanceStatus.Remote, WorkHours = 8.6m, OvertimeHours = 0.1m, Source = "Web", Notes = "Remote development day", ClockInLatitude = 12.971599m, ClockInLongitude = 77.594566m, ClockOutLatitude = 12.971599m, ClockOutLongitude = 77.594566m },
            new AttendanceRecord { TenantId = TenantId, EmployeeId = qa.Id, WorkDate = today.AddDays(-1), ClockedInAt = clockIn.AddMinutes(4), ClockedOutAt = clockIn.AddHours(8.4), Status = AttendanceStatus.Present, WorkHours = 8.33m, Source = "Web", ClockInLatitude = 12.935200m, ClockInLongitude = 77.624500m, ClockOutLatitude = 12.935200m, ClockOutLongitude = 77.624500m });
        db.TimesheetEntries.AddRange(
            new TimesheetEntry { TenantId = TenantId, EmployeeId = developer.Id, WorkDate = today.AddDays(-1), ProjectCode = "PFLOW", Description = "Employee portal API integration", Hours = 7.5m, Status = WorkflowStatus.Approved },
            new TimesheetEntry { TenantId = TenantId, EmployeeId = qa.Id, WorkDate = today.AddDays(-1), ProjectCode = "PFLOW", Description = "Regression test execution", Hours = 8, Status = WorkflowStatus.Pending });

        var periodStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var payrollRun = new PayrollRun
        {
            TenantId = TenantId, Name = periodStart.ToString("MMMM yyyy") + " payroll",
            PeriodStart = periodStart, PeriodEnd = periodEnd, PaymentDate = periodEnd.AddDays(3),
            Status = PayrollRunStatus.Approved, Currency = "INR",
            GrossTotal = 770833.33m, DeductionTotal = 92500m, NetTotal = 678333.33m
        };
        db.PayrollRuns.Add(payrollRun);
        void Payroll(Employee employee, decimal basic, decimal allowances, decimal deductions, decimal taxes)
        {
            var gross = basic + allowances;
            db.PayrollItems.Add(new PayrollItem
            {
                TenantId = TenantId, PayrollRunId = payrollRun.Id, EmployeeId = employee.Id,
                BasicPay = basic, Allowances = allowances, Deductions = deductions, Taxes = taxes,
                GrossPay = gross, NetPay = gross - deductions - taxes,
                BreakdownJson = """{"housingAllowance":0.2,"transportAllowance":0.05,"demoOnly":true}"""
            });
        }
        Payroll(admin, 200000, 30000, 2400, 33000);
        Payroll(hr, 125000, 18000, 1800, 18000);
        Payroll(payroll, 100000, 15000, 1800, 13000);
        Payroll(manager, 183333.33m, 27500, 2400, 30000);
        Payroll(developer, 116666.67m, 17500, 1800, 15000);
        Payroll(qa, 108333.33m, 16250, 1800, 13500);

        var job = new JobOpening
        {
            TenantId = TenantId, Title = "Senior Frontend Engineer", Code = "ENG-FE-102",
            DepartmentId = engineering.Id, HiringManagerId = manager.Id, Openings = 2,
            Description = "Build accessible Angular product experiences and shared design-system components.",
            Status = JobStatus.Open, ClosesOn = today.AddDays(30)
        };
        var candidate = new Candidate
        {
            TenantId = TenantId, FirstName = "Kavya", LastName = "Rao",
            Email = "kavya.rao@example.test", Phone = "+91 98888 22110",
            Source = "Employee referral", ResumeStorageKey = "demo/candidates/kavya-rao.pdf"
        };
        db.JobOpenings.Add(job);
        db.Candidates.Add(candidate);
        db.JobApplications.Add(new JobApplication
        {
            TenantId = TenantId, JobOpeningId = job.Id, CandidateId = candidate.Id,
            Stage = CandidateStage.Interview, Rating = 4.3m,
            Notes = "Technical interview scheduled", AppliedAt = now.AddDays(-8)
        });

        var cycle = new PerformanceCycle
        {
            TenantId = TenantId, Name = today.Year + " Mid-year review",
            StartsOn = new DateOnly(today.Year, 7, 1), EndsOn = new DateOnly(today.Year, 9, 30), IsActive = true
        };
        db.PerformanceCycles.Add(cycle);
        db.PerformanceReviews.AddRange(
            new PerformanceReview { TenantId = TenantId, CycleId = cycle.Id, EmployeeId = developer.Id, ReviewerId = manager.Id, Status = ReviewStatus.ManagerReview, SelfRating = 4.2m, GoalsJson = """[{"goal":"Ship employee portal","progress":72},{"goal":"Improve API reliability","progress":60}]""", Feedback = "Strong delivery; improve estimation consistency." },
            new PerformanceReview { TenantId = TenantId, CycleId = cycle.Id, EmployeeId = qa.Id, ReviewerId = manager.Id, Status = ReviewStatus.SelfReview, SelfRating = 4.0m, GoalsJson = """[{"goal":"Automate regression suite","progress":65}]""" });

        var laptop = new Asset { TenantId = TenantId, AssetTag = "NS-LAP-1005", Name = "MacBook Pro 14", Category = "Laptop", SerialNumber = "DEMO-MBP-1005", PurchasedOn = today.AddYears(-1), PurchaseCost = 185000, Status = AssetStatus.Assigned };
        var monitor = new Asset { TenantId = TenantId, AssetTag = "NS-MON-1006", Name = "Dell 27-inch Monitor", Category = "Monitor", SerialNumber = "DEMO-MON-1006", PurchasedOn = today.AddMonths(-8), PurchaseCost = 28000, Status = AssetStatus.Assigned };
        var spare = new Asset { TenantId = TenantId, AssetTag = "NS-LAP-1007", Name = "ThinkPad T14", Category = "Laptop", SerialNumber = "DEMO-T14-1007", PurchasedOn = today.AddMonths(-3), PurchaseCost = 110000, Status = AssetStatus.Available };
        db.Assets.AddRange(laptop, monitor, spare);
        db.AssetAssignments.AddRange(
            new AssetAssignment { TenantId = TenantId, AssetId = laptop.Id, EmployeeId = developer.Id, AssignedAt = now.AddMonths(-10), Notes = "Primary development device" },
            new AssetAssignment { TenantId = TenantId, AssetId = monitor.Id, EmployeeId = qa.Id, AssignedAt = now.AddMonths(-6), Notes = "QA workstation" });
        db.ExpenseClaims.AddRange(
            new ExpenseClaim { TenantId = TenantId, EmployeeId = developer.Id, ClaimNumber = "EXP-" + today.Year + "-0041", Category = "Internet", ExpenseDate = today.AddDays(-5), Amount = 1499, Currency = "INR", Description = "Monthly remote-work internet reimbursement", ReceiptStorageKey = "demo/expenses/0041.pdf", Status = ExpenseStatus.Submitted },
            new ExpenseClaim { TenantId = TenantId, EmployeeId = qa.Id, ClaimNumber = "EXP-" + today.Year + "-0042", Category = "Travel", ExpenseDate = today.AddDays(-12), Amount = 860, Currency = "INR", Description = "Client-site travel", ReceiptStorageKey = "demo/expenses/0042.pdf", Status = ExpenseStatus.Approved, ReviewedBy = managerUser.Id });

        var securityCourse = new TrainingCourse
        {
            TenantId = TenantId, Title = "Information Security Essentials", Provider = "Northstar Learning",
            Description = "Annual security awareness, phishing and data-handling training.",
            DurationHours = 2, IsMandatory = true, ExpiresOn = today.AddMonths(10)
        };
        var angularCourse = new TrainingCourse
        {
            TenantId = TenantId, Title = "Advanced Angular Architecture", Provider = "Engineering Academy",
            Description = "Signals, performance and accessible component architecture.",
            DurationHours = 8, IsMandatory = false
        };
        db.TrainingCourses.AddRange(securityCourse, angularCourse);
        db.TrainingEnrollments.AddRange(
            new TrainingEnrollment { TenantId = TenantId, CourseId = securityCourse.Id, EmployeeId = developer.Id, Status = EnrollmentStatus.Completed, EnrolledAt = now.AddMonths(-2), CompletedAt = now.AddMonths(-1), Score = 92 },
            new TrainingEnrollment { TenantId = TenantId, CourseId = securityCourse.Id, EmployeeId = qa.Id, Status = EnrollmentStatus.InProgress, EnrolledAt = now.AddDays(-10) },
            new TrainingEnrollment { TenantId = TenantId, CourseId = angularCourse.Id, EmployeeId = developer.Id, Status = EnrollmentStatus.InProgress, EnrolledAt = now.AddDays(-15) });
        db.Announcements.AddRange(
            new Announcement { TenantId = TenantId, Title = "Quarterly town hall", Body = "Join the company town hall on Friday at 4:00 PM IST.", PublishedAt = now.AddDays(-2), ExpiresAt = now.AddDays(14), Audience = "all" },
            new Announcement { TenantId = TenantId, Title = "Benefits enrollment window", Body = "Review dependent details before the enrollment window closes.", PublishedAt = now.AddDays(-1), ExpiresAt = now.AddDays(20), Audience = "all" });

        var workProject = new WorkProject
        {
            TenantId = TenantId, Key = "PFLOW", Name = "PeopleFlow product delivery",
            Description = "Customer-facing HRMS product delivery, release quality and operational readiness.",
            LeadEmployeeId = manager.Id, NextItemNumber = 7, IsActive = true
        };
        db.WorkProjects.Add(workProject);
        db.WorkProjectMembers.AddRange(
            new WorkProjectMember { TenantId = TenantId, ProjectId = workProject.Id, EmployeeId = manager.Id, CanCreateItems = true, CanAssignItems = true, CanTransitionItems = true, CanLogWork = true, CanViewAllWorklogs = true },
            new WorkProjectMember { TenantId = TenantId, ProjectId = workProject.Id, EmployeeId = developer.Id, CanCreateItems = true, CanAssignItems = false, CanTransitionItems = true, CanLogWork = true, CanViewAllWorklogs = false },
            new WorkProjectMember { TenantId = TenantId, ProjectId = workProject.Id, EmployeeId = qa.Id, CanCreateItems = true, CanAssignItems = false, CanTransitionItems = true, CanLogWork = true, CanViewAllWorklogs = false },
            new WorkProjectMember { TenantId = TenantId, ProjectId = workProject.Id, EmployeeId = hr.Id, CanCreateItems = true, CanAssignItems = true, CanTransitionItems = true, CanLogWork = true, CanViewAllWorklogs = true });

        var epic = new WorkItem
        {
            TenantId = TenantId, ProjectId = workProject.Id, Number = 1, Key = "PFLOW-1",
            Type = WorkItemType.Epic, Summary = "Employee self-service release",
            Description = "Deliver a production-ready employee experience across attendance, leave and personal services.",
            Status = WorkItemStatus.InProgress, Priority = WorkItemPriority.High,
            ReporterEmployeeId = manager.Id, AssigneeEmployeeId = manager.Id, DueDate = today.AddDays(25),
            OriginalEstimateMinutes = 4800, RemainingEstimateMinutes = 2460, StoryPoints = 21, LabelsCsv = "release,employee-experience"
        };
        var apiItem = new WorkItem
        {
            TenantId = TenantId, ProjectId = workProject.Id, Number = 2, Key = "PFLOW-2", ParentId = epic.Id,
            Type = WorkItemType.Story, Summary = "Complete employee portal API integration",
            Description = "Connect attendance, leave balances and employee profile workflows to production API endpoints.",
            Status = WorkItemStatus.InProgress, Priority = WorkItemPriority.High,
            ReporterEmployeeId = manager.Id, AssigneeEmployeeId = developer.Id, DueDate = today.AddDays(5),
            OriginalEstimateMinutes = 1440, RemainingEstimateMinutes = 420, StoryPoints = 8, LabelsCsv = "api,self-service"
        };
        var responsiveItem = new WorkItem
        {
            TenantId = TenantId, ProjectId = workProject.Id, Number = 3, Key = "PFLOW-3", ParentId = epic.Id,
            Type = WorkItemType.Task, Summary = "Validate mobile layouts across HR workflows",
            Description = "Run responsive regression checks at 360px, 768px and desktop widths and record defects.",
            Status = WorkItemStatus.InReview, Priority = WorkItemPriority.Medium,
            ReporterEmployeeId = manager.Id, AssigneeEmployeeId = qa.Id, DueDate = today.AddDays(2),
            OriginalEstimateMinutes = 720, RemainingEstimateMinutes = 180, StoryPoints = 5, LabelsCsv = "mobile,qa"
        };
        var payrollBug = new WorkItem
        {
            TenantId = TenantId, ProjectId = workProject.Id, Number = 4, Key = "PFLOW-4",
            Type = WorkItemType.Bug, Summary = "Payroll totals round incorrectly for partial months",
            Description = "Net total differs by one rupee when a prorated salary contains repeating decimals.",
            Status = WorkItemStatus.ToDo, Priority = WorkItemPriority.Critical,
            ReporterEmployeeId = payroll.Id, AssigneeEmployeeId = developer.Id, DueDate = today.AddDays(1),
            OriginalEstimateMinutes = 360, RemainingEstimateMinutes = 360, StoryPoints = 3, LabelsCsv = "payroll,calculation"
        };
        var auditItem = new WorkItem
        {
            TenantId = TenantId, ProjectId = workProject.Id, Number = 5, Key = "PFLOW-5",
            Type = WorkItemType.Task, Summary = "Review role permissions before customer demo",
            Description = "Confirm employee, manager, HR, payroll and work-management permission boundaries.",
            Status = WorkItemStatus.Done, Priority = WorkItemPriority.High,
            ReporterEmployeeId = hr.Id, AssigneeEmployeeId = hr.Id, DueDate = today.AddDays(-2),
            OriginalEstimateMinutes = 240, RemainingEstimateMinutes = 0, StoryPoints = 2, LabelsCsv = "security,demo",
            Resolution = WorkItemResolution.Done, ResolvedAt = now.AddDays(-2)
        };
        var backlogItem = new WorkItem
        {
            TenantId = TenantId, ProjectId = workProject.Id, Number = 6, Key = "PFLOW-6",
            Type = WorkItemType.Story, Summary = "Export the worklog matrix to CSV",
            Description = "Allow coordinators to export the selected ticket-by-date report range.",
            Status = WorkItemStatus.Backlog, Priority = WorkItemPriority.Low,
            ReporterEmployeeId = manager.Id, DueDate = today.AddDays(35),
            OriginalEstimateMinutes = 480, RemainingEstimateMinutes = 480, StoryPoints = 3, LabelsCsv = "reporting"
        };
        db.WorkItems.AddRange(epic, apiItem, responsiveItem, payrollBug, auditItem, backlogItem);
        db.WorkItemComments.AddRange(
            new WorkItemComment { TenantId = TenantId, WorkItemId = apiItem.Id, AuthorEmployeeId = manager.Id, Body = "Please keep the empty and error states consistent with the employee directory." },
            new WorkItemComment { TenantId = TenantId, WorkItemId = apiItem.Id, AuthorEmployeeId = developer.Id, Body = "Attendance and leave are connected. I am finishing the profile update path today." },
            new WorkItemComment { TenantId = TenantId, WorkItemId = responsiveItem.Id, AuthorEmployeeId = qa.Id, Body = "Desktop and tablet are clean. Two narrow-screen issues are documented for review." },
            new WorkItemComment { TenantId = TenantId, WorkItemId = payrollBug.Id, AuthorEmployeeId = payroll.Id, Body = "Use the previous month demo payroll as the reproduction data." });
        db.WorkLogs.AddRange(
            new WorkLog { TenantId = TenantId, WorkItemId = apiItem.Id, EmployeeId = developer.Id, WorkDate = today.AddDays(-4), Minutes = 300, Description = "Attendance API and state handling" },
            new WorkLog { TenantId = TenantId, WorkItemId = apiItem.Id, EmployeeId = developer.Id, WorkDate = today.AddDays(-3), Minutes = 360, Description = "Leave balances and request integration" },
            new WorkLog { TenantId = TenantId, WorkItemId = apiItem.Id, EmployeeId = developer.Id, WorkDate = today.AddDays(-2), Minutes = 240, Description = "Profile integration and tests" },
            new WorkLog { TenantId = TenantId, WorkItemId = responsiveItem.Id, EmployeeId = qa.Id, WorkDate = today.AddDays(-3), Minutes = 240, Description = "Desktop and tablet regression" },
            new WorkLog { TenantId = TenantId, WorkItemId = responsiveItem.Id, EmployeeId = qa.Id, WorkDate = today.AddDays(-2), Minutes = 300, Description = "Mobile browser validation" },
            new WorkLog { TenantId = TenantId, WorkItemId = auditItem.Id, EmployeeId = hr.Id, WorkDate = today.AddDays(-2), Minutes = 240, Description = "Permission matrix review" },
            new WorkLog { TenantId = TenantId, WorkItemId = epic.Id, EmployeeId = manager.Id, WorkDate = today.AddDays(-4), Minutes = 180, Description = "Release planning and dependency review" });
        db.WorkItemHistories.AddRange(
            new WorkItemHistory { TenantId = TenantId, WorkItemId = epic.Id, ActorEmployeeId = manager.Id, EventType = "created", AfterValue = epic.Key },
            new WorkItemHistory { TenantId = TenantId, WorkItemId = apiItem.Id, ActorEmployeeId = manager.Id, EventType = "transitioned", FieldName = "status", BeforeValue = "ToDo", AfterValue = "InProgress" },
            new WorkItemHistory { TenantId = TenantId, WorkItemId = responsiveItem.Id, ActorEmployeeId = qa.Id, EventType = "transitioned", FieldName = "status", BeforeValue = "InProgress", AfterValue = "InReview" },
            new WorkItemHistory { TenantId = TenantId, WorkItemId = auditItem.Id, ActorEmployeeId = hr.Id, EventType = "transitioned", FieldName = "status", BeforeValue = "InReview", AfterValue = "Done" });

        await db.SaveChangesAsync(ct);
        currentTenant.Clear();
        return new DemoCompanySeedResult(TenantSlug, DemoPassword, accountEmails, true);
    }
}
