# India payroll operations

This release makes INR and Asia/Kolkata the defaults for new company and salary records. It adds a controlled monthly payroll calculation using employee salary profiles, approved leave, attendance, company holidays, overtime, and one-time adjustments. Existing company settings and old payroll runs are not rewritten.

## Company setup

1. In **Attendance**, define the company's working days, holidays and attendance rules. Resolve the month's pending attendance corrections and leave requests before payroll.
2. In **Payroll → Company policy**, confirm whether `Base salary` on an employee record is annual or monthly. Choose calendar, fixed-30 or working-day proration; how to handle missing attendance; unpaid leave/absence deductions; overtime; and employer/employee PF and ESI rates. Save the policy.
3. In **Payroll → Employee profiles**, review each eligible employee's basic/HRA structure, PF and ESI enrollment, tax regime, monthly TDS, professional tax, other deduction, and optional overtime rate. The system deliberately blocks calculation if any eligible employee lacks a saved profile.
4. Create a run for a complete calendar month. Add one-time bonus, reimbursement or deduction entries with unique codes per employee. Calculate and inspect each employee's item and detailed source breakdown.
5. Reconcile calculated totals against source attendance, approved leave, payroll registers, and statutory calculations. Record the review, then approve. A company may disable the separate review requirement in its policy, though manual verification remains essential.
6. Disburse wages using the company's bank process. Enter the actual payment date and bank transaction or batch reference to mark the approved run paid. Employee payslips are then released in **My services → Payslips**, where they can be printed or saved as PDF. If the company explicitly enables **Release payslips after approval**, an approved payslip is visible before payment and clearly says payment is not yet recorded.

Changes to policy, profiles or adjustments reset calculated but unapproved runs to Draft and clear their items. Approved and paid runs cannot be recalculated or amended through these endpoints. Errors or arrears after approval require a later run adjustment.

## Scope of the calculations

- Calendar-month gross salary is prorated for hire and termination dates. Unpaid approved leave, marked absences, half days and missing-attendance policy affect loss of pay. Working-day policy reads the configured attendance working days and company/selected optional holidays.
- Overtime is read from attendance records and uses a company multiplier or employee hourly-rate override.
- PF and ESI calculations use the configured rates only for enrolled employees. Employer contribution amounts are recorded in the item breakdown but not subtracted from net pay.
- Professional tax, TDS and other recurring deductions are reviewed amounts on each employee profile. Bonus and other one-time entries are adjustments on the run.
- The payslip records the calculated line items, pay period and recorded disbursement reference. The browser's print action generates a PDF locally.

## Before live statutory payroll

This is an operational payroll workflow, **not an automated compliance or banking system**. Have an Indian payroll/tax specialist validate company-specific rules and opening balances. In particular, it does not yet calculate projected annual TDS or issue Form 16, generate/file EPFO or ESIC returns, implement state-specific professional-tax schedules, assess minimum wages or gratuity, maintain effective-dated salary revisions and arrears automatically, confirm bank transfers through an integration, or import year-to-date balances. ESI coverage and PF wage-ceiling treatment must be reviewed against each employer and employee's actual eligibility/contribution period. The recorded bank reference is a manual assertion, not a bank confirmation.

Apply the new EF Core migration before opening the updated API. The application initializer applies pending migrations at startup; take a database backup and rehearse the migration on a staging copy before production rollout.
