using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeSelfServiceAndAttendanceLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ClockInAccuracyMeters",
                table: "AttendanceRecords",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClockInAddress",
                table: "AttendanceRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClockInIpAddress",
                table: "AttendanceRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClockInLatitude",
                table: "AttendanceRecords",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClockInLongitude",
                table: "AttendanceRecords",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClockInUserAgent",
                table: "AttendanceRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClockOutAccuracyMeters",
                table: "AttendanceRecords",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClockOutAddress",
                table: "AttendanceRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClockOutIpAddress",
                table: "AttendanceRecords",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClockOutLatitude",
                table: "AttendanceRecords",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClockOutLongitude",
                table: "AttendanceRecords",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClockOutUserAgent",
                table: "AttendanceRecords",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClockInAccuracyMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockInAddress",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockInIpAddress",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockInLatitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockInLongitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockInUserAgent",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockOutAccuracyMeters",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockOutAddress",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockOutIpAddress",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockOutLatitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockOutLongitude",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ClockOutUserAgent",
                table: "AttendanceRecords");
        }
    }
}
