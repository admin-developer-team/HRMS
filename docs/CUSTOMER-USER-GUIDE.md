# PeopleFlow HRMS Customer User Guide

Version: September 2026

This guide explains how company administrators, HR teams, payroll teams, managers, and employees use PeopleFlow. Screen visibility depends on assigned roles, so a user may see only part of the navigation described here.

## 1. Demo environment

Open `http://localhost:4200/login` for local use.

Use these credentials only for the Northstar demonstration company:

| Field | Value |
|---|---|
| Company / tenant slug | `northstar-demo` |
| Temporary demo password | `Demo@12345` |

| Login email | Role coverage | Best use in a demonstration |
|---|---|---|
| `admin@northstar.demo` | Tenant Administrator | Full tenant setup, all screens, users, roles, audit, and project access |
| `priya.hr@northstar.demo` | HR Administrator + Employee Self-Service | Employee lifecycle, leave, attendance administration, talent, and own services |
| `arjun.payroll@northstar.demo` | Payroll Administrator + Employee Self-Service | Payroll controls and own services |
| `neha.manager@northstar.demo` | People Manager + Work Coordinator | Direct-report approvals, ticket assignment, team worklogs, and reports |
| `rohan.dev@northstar.demo` | Employee Self-Service + Work Contributor | Employee workflows, assigned tickets, comments, transitions, and own worklogs |
| `meera.qa@northstar.demo` | Employee Self-Service + Work Contributor | Employee workflows, QA tickets, comments, transitions, and own worklogs |

The password is shared for demonstration convenience. Change or remove every demo account before using the tenant for real company data.

## 2. Signing in and out

1. Open the sign-in page.
2. Enter the company slug. A company slug identifies the tenant and keeps its records separate from every other company.
3. Enter the account email and password.
4. Select whether the browser should remember the session.
5. Select **Sign in**.
6. To sign out, open the account menu in the top-right corner and select **Sign out**.

Five consecutive invalid password attempts temporarily lock the account for 15 minutes. Passwords must contain at least 8 characters.

## 3. Application layout

- The left navigation contains only modules permitted for the signed-in user.
- Select the menu icon to collapse or expand desktop navigation. On a phone it opens an overlay.
- Global search opens the employee directory for authorized HR users.
- The app-grid button contains common quick actions; the contrast button changes the theme.
- Tables scroll horizontally on narrow screens. Drawers become full-screen on mobile.
- Status pills show lifecycle state. Row menus contain commands valid for that record.
- Success and error banners confirm an operation. Do not resubmit while Save is disabled.

## 4. Roles and access

PeopleFlow checks permissions in both the interface and API. Hiding a menu is not the security boundary.

| System role | Intended use |
|---|---|
| Tenant Administrator | Full control of one customer company |
| HR Administrator | People and HR operations, identity, audit, and Work administration |
| Payroll Administrator | Payroll, required employee lookup, dashboard, and audit |
| People Manager | Own services plus direct-report views and approvals |
| Employee Self-Service | Own profile, attendance, requests, learning, assets, documents, payslips, and password |
| Work Contributor | Read permitted projects, create/edit tickets, transition, comment, and log own time |
| Work Coordinator | Contributor abilities plus assignment and permitted team worklogs |

Work access has two layers:

1. **Access & roles** grants Work Contributor, Work Coordinator, or a custom Work role.
2. **Work > Projects & access** grants a specific project and controls create, assign, transition, log-time, and view-all-worklog capabilities.

A user needs both layers. After changing roles, the user must sign out and in again so a new access token contains the permissions.

## 5. Recommended company setup order

1. Create the customer company from the platform workspace.
2. Sign in as its Tenant Administrator.
3. Create locations, departments, and designations.
4. Create employees and assign location, department, designation, and manager.
5. Create employee login accounts and assign least-privilege roles.
6. Configure leave types, shifts, holidays, and announcements.
7. Register assets, courses, review cycles, jobs, and other operational data.
8. Create Work projects, add members, and assign Work roles.
9. Configure branding and density in **Appearance**.
10. Test one administrator, manager, and employee account before rollout.

## 6. Platform workspace

Platform administrators use **Customer companies** to provision and search tenants. Company creation requires a unique name and slug, administrator identity and password, currency, time zone, and employee limit. Provisioning creates the subscription, Tenant Administrator, system roles, and default annual and sick leave types.

Use a company row's **Manage company** action to convert a trial to an active customer, change the subscription plan and employee limit, extend dates, or suspend/cancel access. **Active** is the permanent/live customer state. Suspending or cancelling a company revokes its active sessions; an inactive or expired subscription also blocks sign-in and token refresh. The employee limit cannot be reduced below the current licensed workforce.

Platform access is separate from customer-company administration.

## 7. Admin dashboard

The dashboard summarizes active employees, pending leave, open positions, available assets, current payroll total, and employee counts by status. Use it for awareness, then open the source module before making a decision.

## 8. Employees

### Add an employee

1. Open **Employees** and select **Add employee**.
2. Enter employee ID, name, work email, phone, hire date, employment type, and status.
3. Assign department, designation, location, and manager.
4. Enter base salary and currency, then save.

Employee ID and work email must be unique in the company. Use stable IDs because they appear in exports.

### Manage employee records

- The row menu opens profile, edit, account creation, and delete actions.
- **Export CSV** exports the current result.
- Delete is a soft delete; audit history remains.
- Use Active, Probation, Notice Period, Suspended, Terminated, and Resigned consistently.

### Create login access

1. Open an employee row menu and select **Create login account**.
2. Enter a temporary password of at least 8 characters.
3. Assign roles and save.
4. Share slug, work email, and temporary password securely.
5. Ask the employee to change the password from **My services**.

Creating an employee does not automatically create credentials.

Administrators can edit employee names, employee IDs, work email, hire date, employment type, organization assignment, manager, compensation, and lifecycle status. Setting an employee to **Inactive**, **Suspended**, **Terminated**, or **Resigned** disables the linked login and revokes active sessions; changing the employee back to **Active** re-enables it. The application prevents an administrator from deactivating their own employee record.

## 9. Organization

- **Departments:** create unique codes, parent relationships, and department heads.
- **Designations:** create titles with unique codes, levels, and descriptions.
- **Locations:** create offices or remote locations with code, address, city, and country code.

Create lookup data before employees so employee forms can reference it.

## 10. Leave

Administrators create leave types, annual allowance, paid/unpaid rules, document requirements, and maximum consecutive days. They search requests, review statuses, approve or reject with comments, and inspect yearly balances.

Employees use **My services > Leave** to choose a type, dates, day count, and reason. **Leave balances** shows entitled, used, pending, and available amounts.

Managers use **My team > Leave approvals** and can access only direct reports. Approval updates request and balance together.

## 11. Attendance

Employees clock in and out from **My workspace**. Location permission is requested only when the employee presses the action.

Attendance can record date, exact time, coordinates, browser accuracy, resolved address, public IP, user agent, work hours, overtime, source, status, and notes. Employees see their history; managers see direct reports; attendance administrators search the tenant register.

Location is sensitive personal data. Customers must define a lawful basis, privacy notice, access policy, and retention period before production use.

## 12. Workforce operations

### Shifts and holidays

Create shifts with start/end times, grace minutes, and night-shift setting. Create holidays by date, optional location, and optional-holiday setting.

### Timesheets

Employees add a work date, optional project code, description, and hours. Managers review direct-report timesheets. Workforce administrators search and review the wider register.

Timesheets and Work worklogs are different:

- A timesheet is a daily HR approval record.
- A Work worklog records time against a ticket and powers the ticket-by-date report.

### Documents and announcements

Administrators add document metadata and storage references, then verify or reject documents. Employees see their own list. Announcements use a title, message, audience, publish time, and expiry.

## 13. Work management

Work is a native PeopleFlow delivery module inspired by common issue-tracking workflows. It does not use Atlassian branding.

### Create and secure a project

1. Assign users **Work Contributor** or **Work Coordinator** in **Access & roles**.
2. Open **Work**, select **New project**, and enter key, name, lead, and description.
3. Open **Projects & access** and add employees.
4. Configure:
   - **Create:** create and edit tickets.
   - **Assign:** assign and unassign.
   - **Transition:** change status.
   - **Log time:** create worklogs.
   - **View all logs:** see other employees' project worklogs.
5. Select **Save access**.

Project keys are 2-10 letters/numbers, start with a letter, and generate stable keys such as `PFLOW-4`.

### Ticket fields

Tickets support Epic, Story, Task, Bug, and Subtask types; summary and description; parent; priority; reporter; assignee; due date; original and remaining estimate; story points; labels; resolution; resolved timestamp; and concurrent-edit version.

### Board and list

- **Board** groups active tickets into Backlog, To do, In progress, In review, and Done.
- Select a card to open its details.
- **All work** offers key/summary search plus status and priority filters.
- Cancelled tickets remain in list and audit history.

### Workflow

| Current | Allowed next |
|---|---|
| Backlog | To do, Cancelled |
| To do | Backlog, In progress, Cancelled |
| In progress | To do, In review, Done, Cancelled |
| In review | In progress, Done, Cancelled |
| Done | In progress |
| Cancelled | Backlog |

Done/Cancelled records resolution and resolved time and sets remaining estimate to zero. Reopening clears resolution.

### Ticket details and activity

Open a ticket to edit fields, transition, assign a project member, read time summary, add comments, delete own comments, add/delete own worklogs, and review history. Backend authorization also supports comment/worklog editing. Administrators can manage all entries; standard users are limited to their own.

### Log time

1. Open a ticket.
2. Choose work date.
3. Enter hours and a meaningful description.
4. Select **Log time**.

A worklog must be 1 minute to 24 hours. Work cannot be logged more than one day ahead. Logging reduces an existing remaining estimate unless an explicit remainder is supplied.

### Time matrix

1. Open **Work > Time report**.
2. Choose **7 days**, **This month**, **This year**, or custom From/To.
3. Optionally select project and employee.
4. Select **Generate**.

Tickets are rows, dates are columns, cells show daily time, and the last column shows ticket totals. Sticky context remains visible while scrolling. Reports cover up to 367 days. Visibility follows each project's **View all logs** setting; contributors always see their own permitted logs.

Use work data as factual context, not an automatic performance score. Consider complexity, quality, collaboration, leave, and role expectations.

## 14. Payroll

1. Create a run with period, payment date, and currency.
2. Calculate to generate employee items.
3. Review basic pay, allowances, overtime, deductions, taxes, gross, and net.
4. Progress through Draft, Processing, Approved, Paid, or Cancelled.
5. Employees view eligible payslips in **My services**.

Country-specific taxes, statutory filings, pensions, bank rails, and legal compliance require customer-specific implementation and certification.

## 15. Expenses

Employees create a draft with category, date, amount, currency, description, and receipt reference, then submit it. Managers review direct reports; authorized administrators review tenant claims. States include Draft, Submitted, Approved, Rejected, and Reimbursed.

## 16. Recruitment

Create job openings with department, hiring manager, openings, description, and closing date. Create candidates and resume references, add applications, and move them through Applied, Screening, Interview, Offer, Hired, Rejected, or Withdrawn while recording ratings and notes.

## 17. Performance

Create review cycles and employee/reviewer assignments. Record goals, self rating, manager rating, and feedback. Progress through Draft, Self Review, Manager Review, Calibration, and Completed. Employees update self-review fields; managers handle direct reports; HR oversees cycles.

## 18. Assets

Register asset tag, name, category, serial number, purchase date, and cost. Assign to an employee with custody notes. **Return asset** closes custody and restores availability. Employees see their assigned assets under **My services**.

## 19. Learning and development

Create courses with provider, description, duration, mandatory flag, and expiry. Enroll employees and record In Progress or Completed state and score. Employees review assigned learning and mark supported training complete.

## 20. Access and roles

Create a user with display name, email, temporary password, optional employee link, and roles. Use **Change roles** later.

Custom permissions cover dashboard, employees, organization, leave, attendance, workforce, payroll, recruitment, performance, assets, expenses, learning, identity, audit, self-service, direct reports, and every Work capability.

In **Access & roles > Users**, authorized administrators can change roles, reset temporary passwords, and activate or deactivate login accounts. Users without employee records, including the platform administrator, can change their own password from **Settings**. The system prevents self-deactivation and revokes existing sessions after password or access changes.

Avoid `*` except for a small number of accountable tenant administrators. Review access regularly and remove it promptly when responsibilities change.

## 21. Audit log

Audit records include timestamp, actor user ID, action, entity type/ID, IP when available, correlation ID, and backend before/after values. Filter by entity type during investigations. Correlation IDs connect UI failures to API logs.

## 22. Employee self-service

**My workspace** shows profile, today's attendance, request counts, leave availability, training due, and announcements.

**My services** contains leave, balances, timesheets, expenses, learning, performance, assets, documents, payslips, announcements, and password change. Employees access only their linked records.

## 23. Manager workspace

**My team** contains direct-report directory, leave approvals, attendance, timesheet approvals, expense approvals, and performance reviews. Changing an employee's manager changes manager-scope access.

## 24. Appearance

Use **Appearance** for primary/accent/sidebar colors, light/dark scheme, comfortable/compact density, and corner radius. Check contrast and readability before rollout.

## 25. Operating practices

- Use unique, stable codes.
- Keep manager relationships current before approvals.
- Never share administrator accounts.
- Assign least privilege.
- Review payroll before approval/payment.
- Reconcile leave and attendance periodically.
- Do not use ticket hours as the sole performance measure.
- Apply company and legal retention requirements.
- Remove demo accounts and records before production use.

## 26. Troubleshooting

### A menu is missing

Check **Access & roles**. For Work, also check **Projects & access**. Sign in again after role changes.

### A record changed error appears

Another user saved first. Close, reload, review current values, and retry.

### An employee cannot sign in

Confirm slug, work email, active user, and that credentials were provisioned. Five failures cause a 15-minute lockout.

### Location attendance fails

Allow browser/device location, use HTTPS outside localhost, and retry on a stable connection.

### A Work project is invisible

Confirm a Work role, project membership, active project, and a fresh sign-in.

### Another employee's worklogs are hidden

The user needs `work.view-all-logs` plus **View all logs** on that project membership.

### A time report is wide

Horizontal scrolling is intentional because dates are columns. Use shorter ranges for detailed daily review.

### An API error needs investigation

Record message, time, user, record, and correlation ID. Search audit/server logs without exposing passwords or tokens.

## 27. Demonstration walkthrough

1. Sign in as `admin@northstar.demo` and show dashboard, employees, organization, roles, and audit.
2. Open **Work**, show `PFLOW`, open a ticket, and review comments, logs, and history.
3. Show **Projects & access**, then generate a **7 days** time matrix.
4. Sign in as `neha.manager@northstar.demo` and show direct reports, approvals, assignment, and team logs.
5. Sign in as `rohan.dev@northstar.demo` and show attendance, leave, expenses, learning, payslip, ticket comment/transition, and logging time.
6. Return as administrator and show resulting audit entries.

## 28. Local operator appendix

Run from the repository root:

```powershell
npm run backend
npm run frontend
```

URLs:

- Web: `http://localhost:4200`
- API: `http://localhost:5207`
- Swagger: `http://localhost:5207/swagger`
- Health: `http://localhost:5207/health`

Build and test:

```powershell
npm run build
npm run test
```

Create the idempotent demo tenant manually:

```powershell
dotnet run --project backend/src/Hrms.Api/Hrms.Api.csproj -c Release -- --seed-demo
```

The command migrates first, creates `northstar-demo` only when absent, prints a summary, and exits. Store database credentials, JWT keys, and bootstrap passwords in environment variables or .NET user-secrets, never in this guide.
