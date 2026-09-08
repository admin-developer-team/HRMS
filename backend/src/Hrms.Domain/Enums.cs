namespace Hrms.Domain;

public enum TenantStatus { Trial, Active, Suspended, Cancelled }
// Keep existing numeric values stable because PostgreSQL stores this enum as an integer.
public enum EmploymentStatus { Active, Probation, NoticePeriod, Suspended, Terminated, Resigned, Inactive }
public enum EmploymentType { Permanent, Contract, Intern, Consultant, PartTime }
public enum LeaveRequestStatus { Pending, Approved, Rejected, Cancelled }
public enum AttendanceStatus { Present, Absent, HalfDay, OnLeave, Holiday, Remote }
public enum PayrollRunStatus { Draft, Processing, Approved, Paid, Cancelled }
public enum JobStatus { Draft, Open, OnHold, Closed, Cancelled }
public enum CandidateStage { Applied, Screening, Interview, Offer, Hired, Rejected, Withdrawn }
public enum ReviewStatus { Draft, SelfReview, ManagerReview, Calibration, Completed }
public enum AssetStatus { Available, Assigned, Maintenance, Retired, Lost }
public enum ExpenseStatus { Draft, Submitted, Approved, Rejected, Reimbursed }
public enum EnrollmentStatus { Enrolled, InProgress, Completed, Cancelled }
public enum DocumentStatus { Pending, Verified, Rejected, Expired }
public enum DocumentOwnerType { Tenant, User, Employee, WorkItem, LeaveRequest, ExpenseClaim, Candidate }
public enum WorkflowStatus { Pending, Approved, Rejected, Cancelled }
public enum WorkItemType { Epic, Story, Task, Bug, Subtask }
public enum WorkItemStatus { Backlog, ToDo, InProgress, InReview, Done, Cancelled }
public enum WorkItemPriority { Lowest, Low, Medium, High, Highest, Critical }
public enum WorkItemResolution { Done, Fixed, Duplicate, CannotReproduce, WontDo, Cancelled }

