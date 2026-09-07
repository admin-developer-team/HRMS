using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendancePolicySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EarlyDepartureGraceMinutes",
                table: "AttendanceRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LateGraceMinutes",
                table: "AttendanceRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredMinutes",
                table: "AttendanceRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ScheduledEndAt",
                table: "AttendanceRecords",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ScheduledStartAt",
                table: "AttendanceRecords",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireLocationCapture",
                table: "AttendancePolicies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // Freeze the policy that governed existing attendance sessions so later
            // policy changes do not rewrite historical compliance reports.
            migrationBuilder.Sql("""
                UPDATE "AttendanceRecords" AS ar
                SET "ScheduledStartAt" = ap."OfficeStartsAt",
                    "ScheduledEndAt" = ap."OfficeEndsAt",
                    "RequiredMinutes" = ap."RequiredMinutesPerDay",
                    "LateGraceMinutes" = ap."LateGraceMinutes",
                    "EarlyDepartureGraceMinutes" = ap."EarlyDepartureGraceMinutes"
                FROM "AttendancePolicies" AS ap
                WHERE ar."TenantId" = ap."TenantId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarlyDepartureGraceMinutes",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "LateGraceMinutes",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "RequiredMinutes",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ScheduledEndAt",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ScheduledStartAt",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "RequireLocationCapture",
                table: "AttendancePolicies");
        }
    }
}
