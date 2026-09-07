using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkItemMultiAssignees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemAssignees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_WorkItemAssignees", x => x.Id);
                });

            migrationBuilder.Sql("""
                WITH source AS (
                    SELECT "Id" AS "WorkItemId", "TenantId", "AssigneeEmployeeId" AS "EmployeeId",
                           md5("Id"::text || "AssigneeEmployeeId"::text) AS hash
                    FROM "WorkItems"
                    WHERE "AssigneeEmployeeId" IS NOT NULL AND "IsDeleted" = false
                )
                INSERT INTO "WorkItemAssignees" ("Id", "WorkItemId", "EmployeeId", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "IsDeleted", "DeletedAt", "Version", "TenantId")
                SELECT (substr(hash, 1, 8) || '-' || substr(hash, 9, 4) || '-' || substr(hash, 13, 4) || '-' || substr(hash, 17, 4) || '-' || substr(hash, 21, 12))::uuid,
                       "WorkItemId", "EmployeeId", now(), NULL, NULL, NULL, false, NULL, 0, "TenantId"
                FROM source;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAssignees_TenantId_EmployeeId_WorkItemId",
                table: "WorkItemAssignees",
                columns: new[] { "TenantId", "EmployeeId", "WorkItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAssignees_TenantId_WorkItemId_EmployeeId",
                table: "WorkItemAssignees",
                columns: new[] { "TenantId", "WorkItemId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemAssignees");
        }
    }
}
