# Production checklist

- Put connection strings, JWT keys and bootstrap credentials in a secrets manager; rotate the bootstrap password after first login.
- Terminate TLS at a trusted proxy/load balancer and configure forwarded headers and allowed hosts.
- Disable automatic migrations for multi-instance production rollout; run reviewed migrations as a deployment step.
- Use a restricted PostgreSQL application role, encrypted connections, point-in-time recovery, tested restores and cross-region backups.
- Add PostgreSQL row-level security as defense in depth if your threat model requires protection from application query mistakes.
- Configure the frontend CORS allowlist exactly; never use wildcard origins with credentials.
- Add KMS-backed encryption for tax, national ID, bank, health and other regulated fields.
- Integrate malware scanning, content limits, retention and legal-hold policies for uploaded documents.
- Select and certify payroll/tax rules per country, legal entity and effective date. Reconcile totals before enabling payment.
- Add MFA, enterprise SSO/SCIM, password reset/email verification and privileged-session controls.
- Export structured logs, metrics and traces; alert on login abuse, cross-tenant rejections, payroll changes and outbox failures.
- Add background workers for the outbox, scheduled accruals, expiry reminders and notification delivery.
- Run load, penetration, dependency, container and migration rollback tests in CI.
- Complete privacy/DPA, data residency, retention, subject-access/deletion and employee-consent requirements for each sales region.
