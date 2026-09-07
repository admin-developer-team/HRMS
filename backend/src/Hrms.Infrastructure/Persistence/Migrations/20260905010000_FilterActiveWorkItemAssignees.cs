using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HrmsDbContext))]
[Migration("20260905010000_FilterActiveWorkItemAssignees")]
public sealed class FilterActiveWorkItemAssignees : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_WorkItemAssignees_TenantId_WorkItemId_EmployeeId",
            table: "WorkItemAssignees");

        migrationBuilder.CreateIndex(
            name: "IX_WorkItemAssignees_TenantId_WorkItemId_EmployeeId",
            table: "WorkItemAssignees",
            columns: new[] { "TenantId", "WorkItemId", "EmployeeId" },
            unique: true,
            filter: "\"IsDeleted\" = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_WorkItemAssignees_TenantId_WorkItemId_EmployeeId",
            table: "WorkItemAssignees");

        migrationBuilder.CreateIndex(
            name: "IX_WorkItemAssignees_TenantId_WorkItemId_EmployeeId",
            table: "WorkItemAssignees",
            columns: new[] { "TenantId", "WorkItemId", "EmployeeId" },
            unique: true);
    }
}
