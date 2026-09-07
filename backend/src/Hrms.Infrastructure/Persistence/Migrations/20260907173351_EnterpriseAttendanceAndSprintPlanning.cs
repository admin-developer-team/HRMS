using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseAttendanceAndSprintPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRoles_TenantId_UserId_RoleId",
                table: "UserRoles");

            migrationBuilder.AddColumn<Guid>(
                name: "SprintId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttendanceCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalRecordVersion = table.Column<long>(type: "bigint", nullable: true),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedClockIn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequestedClockOut = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", maxLength: 40, nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewComment = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceCorrections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkSprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Goal = table.Column<string>(type: "text", nullable: true),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", maxLength: 40, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSprints", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_TenantId_ProjectId_SprintId_Status",
                table: "WorkItems",
                columns: new[] { "TenantId", "ProjectId", "SprintId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "TenantId", "UserId", "RoleId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TenantId_EmployeeId",
                table: "AttendanceRecords",
                columns: new[] { "TenantId", "EmployeeId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ClockedOutAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceCorrections_TenantId_EmployeeId_WorkDate_Status",
                table: "AttendanceCorrections",
                columns: new[] { "TenantId", "EmployeeId", "WorkDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSprints_TenantId_ProjectId",
                table: "WorkSprints",
                columns: new[] { "TenantId", "ProjectId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSprints_TenantId_ProjectId_Status",
                table: "WorkSprints",
                columns: new[] { "TenantId", "ProjectId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceCorrections");

            migrationBuilder.DropTable(
                name: "WorkSprints");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_TenantId_ProjectId_SprintId_Status",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_TenantId_UserId_RoleId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_TenantId_EmployeeId",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "SprintId",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_TenantId_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "TenantId", "UserId", "RoleId" },
                unique: true);
        }
    }
}
