using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HrmsDbContext))]
[Migration("20260920110000_RequireMandateForNewTrials")]
public sealed class RequireMandateForNewTrials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<bool>(
        name: "RequiresBillingMandate", table: "Tenants", type: "boolean", nullable: false, defaultValue: false);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "RequiresBillingMandate", table: "Tenants");
}
