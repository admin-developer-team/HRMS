using Hrms.Application;
using Hrms.Domain;

namespace Hrms.Tests;

public sealed class WorkManagementTests
{
    [Theory]
    [InlineData(WorkItemStatus.Backlog, WorkItemStatus.ToDo, true)]
    [InlineData(WorkItemStatus.ToDo, WorkItemStatus.InProgress, true)]
    [InlineData(WorkItemStatus.InProgress, WorkItemStatus.InReview, true)]
    [InlineData(WorkItemStatus.InReview, WorkItemStatus.Done, true)]
    [InlineData(WorkItemStatus.Done, WorkItemStatus.InProgress, true)]
    [InlineData(WorkItemStatus.Backlog, WorkItemStatus.Done, false)]
    [InlineData(WorkItemStatus.Done, WorkItemStatus.Backlog, false)]
    public void Workflow_allows_only_configured_transitions(WorkItemStatus from, WorkItemStatus to, bool expected)
    {
        Assert.Equal(expected, WorkWorkflow.CanTransition(from, to));
    }

    [Fact]
    public void Work_roles_are_explicit_and_do_not_expand_employee_self_service()
    {
        var employee = Permissions.TenantSystemRoles.Single(x => x.NormalizedName == "EMPLOYEE_SELF_SERVICE");
        var contributor = Permissions.TenantSystemRoles.Single(x => x.NormalizedName == "WORK_CONTRIBUTOR");
        var coordinator = Permissions.TenantSystemRoles.Single(x => x.NormalizedName == "WORK_COORDINATOR");

        Assert.DoesNotContain(Permissions.WorkRead, employee.Permissions);
        Assert.Contains(Permissions.WorkLog, contributor.Permissions);
        Assert.DoesNotContain(Permissions.WorkAssign, contributor.Permissions);
        Assert.Contains(Permissions.WorkAssign, coordinator.Permissions);
        Assert.Contains(Permissions.WorkViewAllLogs, coordinator.Permissions);
    }
}
