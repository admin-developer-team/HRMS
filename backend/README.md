# Multi-tenant HRMS backend

A .NET 10 / ASP.NET Core backend for a multi-company HRMS SaaS product. It uses clean project boundaries, controller-service-repository flow, EF Core, PostgreSQL, JWT authentication, tenant query filters, audit history, optimistic concurrency, and soft deletion.

## What is implemented

- SaaS platform: tenant provisioning, trials/subscriptions, employee limits, platform administrator, tenant settings fields.
- Identity: login, refresh-token rotation/revocation, lockout, PBKDF2-SHA512 password hashing, tenant users, roles, permissions, JWT policies.
- Core HR: employees, reporting managers, departments, designations, locations, emergency-contact/document domain models.
- Workforce: shifts, holidays, GPS/IP-aware attendance clock-in/out, work-hour/overtime calculation, timesheets and approvals.
- Leave: configurable leave types, yearly balances, overlap checks, requests, approval/rejection and balance accounting.
- Payroll: period runs, employee payroll items, calculation, approval/payment state machine and totals.
- Talent: openings, candidates, applications/stages, performance cycles/reviews, training courses/enrollments.
- Operations: assets and assignments/returns, expense claims and approval, announcements, employee document verification.
- Employee self-service: profile dashboard, attendance, leave/balances, timesheets, expenses, learning, performance, assets, documents, announcements, payslips and password changes.
- Manager self-service: direct-report directory plus scoped leave, timesheet, expense and performance review workflows.
- Platform concerns: mandatory tenant filters, tenant-scoped unique indexes, pagination, audit logs, outbox table, correlation IDs, RFC 7807 errors, rate limiting, CORS, health endpoint and OpenAPI.
- Work management: projects, project-member capabilities, generated ticket keys, standard workflows, comments, estimates, worklogs, activity history, and date-matrix reporting.

Country-specific payroll tax, statutory filing, bank-payment rails, e-signatures, object-storage upload, email/SMS delivery and SSO/SCIM are integration points, not safe universal defaults. They must be implemented and certified for each target market before claiming legal or payroll compliance.

## Solution layout

```text
src/
  Hrms.Domain/          Entities, enums, domain primitives
  Hrms.Application/     DTOs, ports, business services and workflows
  Hrms.Infrastructure/  EF Core, PostgreSQL, repositories, identity, migrations
  Hrms.Api/             Controllers, middleware and composition root
tests/
  Hrms.Tests/           Isolation, soft-delete and security tests
```

Dependencies point inward: API -> Infrastructure/Application -> Domain. Business services depend on `IRepository<T>` and `IUnitOfWork`; only Infrastructure knows EF Core or PostgreSQL.

## Run locally

Requirements: .NET SDK 10.0.301+ and PostgreSQL 18 (PostgreSQL 15+ is also suitable).

```powershell
dotnet tool restore
dotnet restore Hrms.slnx
dotnet build Hrms.slnx --no-restore
dotnet test tests/Hrms.Tests/Hrms.Tests.csproj --no-build
dotnet run --project src/Hrms.Api/Hrms.Api.csproj
```

The development connection string expects PostgreSQL at `localhost:5432`, database/user/password `hrms` or `postgres` as configured in `src/Hrms.Api/appsettings.json`. Prefer user secrets or environment variables instead of editing committed settings:

```powershell
$env:ConnectionStrings__Hrms='Host=localhost;Port=5432;Database=hrms;Username=hrms;Password=your-password'
$env:Jwt__SigningKey='your-64-character-random-signing-key'
$env:Bootstrap__PlatformAdminPassword='your-secure-bootstrap-password'
dotnet run --project src/Hrms.Api/Hrms.Api.csproj
```

Swagger UI is available in Development at `/swagger` and opens automatically through the launch profile. The raw OpenAPI documents are available at `/swagger/v1/swagger.json` and `/openapi/v1.json`; health is `/health`. Use the Swagger **Authorize** button with the JWT access token returned by the login endpoint.

## Docker

Copy `.env.example` to `.env`, replace every secret, then run:

```bash
docker compose up --build
```

The API is exposed at `http://localhost:8080`. Production startup refuses an insecure bootstrap password. The initial migration runs automatically when `Database:AutoMigrate` is enabled.

## First login and tenant creation

Log in to the special `platform` tenant with the configured bootstrap administrator:

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "tenantSlug": "platform",
  "email": "owner@example.com",
  "password": "your bootstrap password"
}
```

Use the returned bearer token to call `POST /api/v1/platform/tenants`. Tenant provisioning atomically creates the subscription, tenant administrator role/user, and default annual/sick leave types. Log in afterward using the new tenant slug. Authenticated tokens contain `tenant_id`; an optional `X-Tenant-ID` must match it. Refresh calls are anonymous but require `X-Tenant-ID`.

## Employee accounts, roles and attendance

Creating an employee record does not silently create credentials. An authorized administrator should provision the linked account from the employee UI or through `POST /api/v1/identity/employees/{employeeId}/account`. The login email must match the employee work email.

## Email notifications

Only the platform superadmin configures email from **Settings → Platform email notifications**. That one provider-neutral SMTP connection and shared template catalog serve every company, so providers can be changed without code changes. The public application URL is taken from the deployed frontend origin when settings are saved; links include the target company slug and return to the intended HRMS screen after sign-in. For Brevo, enter the SMTP relay host, port `587`, the SMTP login as the username, and an SMTP key as the password; use a verified sender address and keep TLS enabled.

SMTP passwords are encrypted with ASP.NET Core Data Protection and are never returned by the API or written to audit payloads. Temporary account passwords exist in the retry outbox only until successful delivery and are then erased. In containers, persist the configured key ring (`DataProtection__KeysPath`; the included Compose file mounts `/var/lib/hrms/keys`) or saved SMTP credentials cannot be decrypted after the key ring is replaced.

Email templates are stored per tenant in the database and can be edited from Settings. Workflow notifications are written to `EmailOutboxItems` in the same transaction as the HRMS change, then delivered by a background worker with exponential retry. Account creation, password/security changes, leave, attendance corrections, timesheets, documents, announcements, payroll, recruitment/candidate updates, performance, assets, expenses, training, and work management are covered. The administrator can send a test message before enabling delivery.

Use the system roles as follows:

| Role | Intended user |
|---|---|
| Employee Self-Service | Individual contributors, including developers |
| People Manager | Employees who manage direct reports; includes self-service |
| HR Administrator | HR staff who administer people and HR processes |
| Payroll Administrator | Payroll staff; does not imply general HR administration |

The application enforces permissions in API policies as well as in the Angular navigation. Employee self-service endpoints require a linked employee ID and scope all records to that employee. Manager endpoints verify that the target employee is a direct report; a tenant ID alone is not sufficient.

Employee clock-in and clock-out calls require browser-provided latitude, longitude and accuracy. The server also records event time, public IP and user agent. These are sensitive personal data: production customers need an explicit privacy notice, jurisdiction-appropriate consent or lawful basis, a retention policy and limited role assignments.

## Main API groups

| Route | Capability |
|---|---|
| `/api/v1/platform/tenants` | Company provisioning and platform tenant search |
| `/api/v1/auth` | Login, refresh rotation and revocation |
| `/api/v1/identity` | Tenant users, roles and permissions |
| `/api/v1/me` | Employee self-service and direct-report manager operations |
| `/api/v1/employees`, `/organization` | Employee directory and organization structure |
| `/api/v1/leave`, `/attendance`, `/workforce` | Leave, attendance, shifts, holidays, timesheets, documents, announcements |
| `/api/v1/payroll` | Payroll calculation and controlled run states |
| `/api/v1/recruitment`, `/performance`, `/training` | Talent lifecycle |
| `/api/v1/assets`, `/expenses` | Employee operations |
| `/api/v1/dashboard`, `/audit-logs` | Tenant metrics and audit trail |

All list endpoints are tenant filtered at the DbContext level. The default page-size cap is 200. Update commands carry `version`; stale updates return a conflict/business-rule response instead of silently overwriting another user's changes.

## Database switching

Domain and Application contain no database provider references. To switch providers:

1. Replace `Npgsql.EntityFrameworkCore.PostgreSQL` in Infrastructure with the new EF Core provider.
2. Replace `UseNpgsql` in `DependencyInjection.cs` and `HrmsDbContextFactory.cs`.
3. Generate a provider-specific migration set; do not reuse PostgreSQL SQL blindly.
4. Run the tenant-isolation and workflow tests against the target engine.

Date-only/time-only and JSON payloads are modeled through EF-supported CLR types and strings to keep the model portable. Index length, collation and identifier casing still require provider testing.

See [architecture](docs/ARCHITECTURE.md) and the [production checklist](docs/PRODUCTION-CHECKLIST.md) before deploying for real customers.
