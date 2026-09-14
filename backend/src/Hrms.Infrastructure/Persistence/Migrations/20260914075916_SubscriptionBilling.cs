using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingProvider",
                table: "TenantSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSubscriptionId",
                table: "TenantSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillingCheckouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ProviderSubscriptionId = table.Column<string>(type: "text", nullable: false),
                    CheckoutUrl = table.Column<string>(type: "text", nullable: false),
                    PlanCode = table.Column<string>(type: "text", nullable: false),
                    ProviderPlanId = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    EmployeeLimit = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsTest = table.Column<bool>(type: "boolean", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_BillingCheckouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BillingWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    ProviderEventId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_BillingWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_BillingProvider_ProviderSubscriptionId",
                table: "TenantSubscriptions",
                columns: new[] { "BillingProvider", "ProviderSubscriptionId" },
                unique: true,
                filter: "\"ProviderSubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BillingCheckouts_Provider_ProviderSubscriptionId",
                table: "BillingCheckouts",
                columns: new[] { "Provider", "ProviderSubscriptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingWebhookEvents_Provider_ProviderEventId",
                table: "BillingWebhookEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillingCheckouts");

            migrationBuilder.DropTable(
                name: "BillingWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_TenantSubscriptions_BillingProvider_ProviderSubscriptionId",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "BillingProvider",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "ProviderSubscriptionId",
                table: "TenantSubscriptions");
        }
    }
}
