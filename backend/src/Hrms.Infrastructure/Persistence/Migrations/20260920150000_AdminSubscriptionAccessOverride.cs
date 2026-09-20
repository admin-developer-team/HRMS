using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations;

[DbContext(typeof(HrmsDbContext))]
[Migration("20260920150000_AdminSubscriptionAccessOverride")]
public sealed class AdminSubscriptionAccessOverride : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "AdminAccessEnabled", table: "Tenants", type: "boolean", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "AdminAccessStartsAt", table: "Tenants", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "AdminAccessEndsAt", table: "Tenants", type: "timestamp with time zone", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AdminAccessEnabled", table: "Tenants");
        migrationBuilder.DropColumn(name: "AdminAccessStartsAt", table: "Tenants");
        migrationBuilder.DropColumn(name: "AdminAccessEndsAt", table: "Tenants");
    }
}
