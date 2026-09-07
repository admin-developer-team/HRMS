using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleDailyAttendanceSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_TenantId_EmployeeId_WorkDate",
                table: "AttendanceRecords");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TenantId_EmployeeId_WorkDate",
                table: "AttendanceRecords",
                columns: new[] { "TenantId", "EmployeeId", "WorkDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_TenantId_EmployeeId_WorkDate",
                table: "AttendanceRecords");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TenantId_EmployeeId_WorkDate",
                table: "AttendanceRecords",
                columns: new[] { "TenantId", "EmployeeId", "WorkDate" },
                unique: true);
        }
    }
}
