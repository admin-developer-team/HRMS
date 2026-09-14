using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndiaPayroll2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollItems_TenantId_PayrollRunId_EmployeeId",
                table: "PayrollItems");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "PayrollRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                table: "PayrollRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CalculatedAt",
                table: "PayrollRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "PayrollRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "PayrollRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicySnapshotJson",
                table: "PayrollRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatutoryReviewedAt",
                table: "PayrollRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StatutoryReviewedBy",
                table: "PayrollRuns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeePayrollProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    BasicPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HraPercentOfBasic = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PfEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EsiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MonthlyTds = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProfessionalTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherMonthlyDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OvertimeHourlyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxRegime = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_EmployeePayrollProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsEarning = table.Column<bool>(type: "boolean", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_PayrollAdjustments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalaryBasis = table.Column<string>(type: "text", nullable: false),
                    PayableDaysBasis = table.Column<string>(type: "text", nullable: false),
                    MissingAttendance = table.Column<string>(type: "text", nullable: false),
                    DeductUnpaidLeave = table.Column<bool>(type: "boolean", nullable: false),
                    DeductAbsences = table.Column<bool>(type: "boolean", nullable: false),
                    DeductHalfDays = table.Column<bool>(type: "boolean", nullable: false),
                    StandardDailyHours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OvertimeMultiplier = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PfEmployeeRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PfEmployerRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PfWageCeiling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EsiEmployeeRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EsiEmployerRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    EsiGrossCeiling = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequireStatutoryReview = table.Column<bool>(type: "boolean", nullable: false),
                    ReleasePayslipsOnApproval = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_PayrollPolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_TenantId_PayrollRunId_EmployeeId",
                table: "PayrollItems",
                columns: new[] { "TenantId", "PayrollRunId", "EmployeeId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePayrollProfiles_TenantId_EmployeeId",
                table: "EmployeePayrollProfiles",
                columns: new[] { "TenantId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollAdjustments_TenantId_PayrollRunId_EmployeeId_Code",
                table: "PayrollAdjustments",
                columns: new[] { "TenantId", "PayrollRunId", "EmployeeId", "Code" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPolicies_TenantId",
                table: "PayrollPolicies",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeePayrollProfiles");

            migrationBuilder.DropTable(
                name: "PayrollAdjustments");

            migrationBuilder.DropTable(
                name: "PayrollPolicies");

            migrationBuilder.DropIndex(
                name: "IX_PayrollItems_TenantId_PayrollRunId_EmployeeId",
                table: "PayrollItems");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "CalculatedAt",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PolicySnapshotJson",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "StatutoryReviewedAt",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "StatutoryReviewedBy",
                table: "PayrollRuns");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_TenantId_PayrollRunId_EmployeeId",
                table: "PayrollItems",
                columns: new[] { "TenantId", "PayrollRunId", "EmployeeId" },
                unique: true);
        }
    }
}
