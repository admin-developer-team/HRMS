# Architecture

## Tenant boundary

The current implementation uses one database and one schema. Every company-owned entity implements `ITenantEntity`. `HrmsDbContext` installs a global filter requiring both a current tenant and a matching `TenantId`, plus `IsDeleted = false`. With no tenant context, tenant rows are invisible. `SaveChangesAsync` assigns missing tenant IDs, rejects ownership changes, applies audit/version fields and converts deletes to soft deletes.

Tenant identity comes from the signed JWT. `X-Tenant-ID` is only a fallback for refresh flow and a mismatch with the JWT is rejected. Platform entities (`Tenant`) are not tenant filtered; their API is protected by the platform-administrator claim.

For customers requiring physical isolation, introduce an `ITenantConnectionResolver` in Infrastructure and create a DbContext per tenant database. Application and Domain do not need to change.

## Request flow

```text
HTTP -> exception/correlation/rate-limit middleware
     -> JWT authentication -> tenant resolution -> authorization policy
     -> controller -> application service -> repository -> HrmsDbContext -> PostgreSQL
```

Controllers translate HTTP only. Services own validation and state transitions. Repositories own persistence querying. `IUnitOfWork` commits a workflow atomically.

## Security decisions

- Passwords use per-password random salt and PBKDF2-SHA512 with 210,000 iterations.
- Refresh tokens are random 512-bit values; only SHA-256 hashes are stored. Rotation revokes the old token.
- Five failed logins cause a 15-minute lock.
- Access tokens default to 15 minutes and include tenant, roles and permissions.
- Administrative policies accept either the explicit permission or the tenant administrator wildcard.
- Sensitive payroll/bank/tax data should be field-encrypted with a managed KMS before production; masked/encrypted storage fields are already separated in the model.

## Extension seams

- Payroll: replace the default base-pay calculator with country/version-specific rule services.
- Files: issue object-storage upload URLs and persist only `StorageKey` through the document endpoints.
- Notifications: publish transactional outbox records to email/SMS/push workers.
- Enterprise identity: map OIDC/SAML/SCIM identities to `UserAccount` and tenant roles.
- Reporting: use read replicas/materialized views rather than bypassing tenant filters in request code.

