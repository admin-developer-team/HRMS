using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HrmsDbContext))]
[Migration("20260905011000_FilterActiveWorkProjectMembers")]
public sealed class FilterActiveWorkProjectMembers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_WorkProjectMembers_TenantId_ProjectId_EmployeeId",
            table: "WorkProjectMembers");

        migrationBuilder.CreateIndex(
            name: "IX_WorkProjectMembers_TenantId_ProjectId_EmployeeId",
            table: "WorkProjectMembers",
            columns: new[] { "TenantId", "ProjectId", "EmployeeId" },
            unique: true,
            filter: "\"IsDeleted\" = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_WorkProjectMembers_TenantId_ProjectId_EmployeeId",
            table: "WorkProjectMembers");

        migrationBuilder.CreateIndex(
            name: "IX_WorkProjectMembers_TenantId_ProjectId_EmployeeId",
            table: "WorkProjectMembers",
            columns: new[] { "TenantId", "ProjectId", "EmployeeId" },
            unique: true);
    }
}
