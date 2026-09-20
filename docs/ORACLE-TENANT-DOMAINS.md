# Company workspace domains on Oracle

The application uses one deployment and one database. The public host selects the
company: `acme.hrms.ssym.co.in` resolves tenant slug `acme`, while
`hrms.ssym.co.in` is reserved for the platform administrator. A signed-in
user's tenant ID must match the host tenant on every API and SignalR request.

## DNS and HTTPS

1. Keep the existing `hrms.ssym.co.in` record pointed at the Oracle VM.
2. At the authoritative DNS provider (currently GoDaddy nameservers), add an `A` record named `*.hrms` pointing
   to the same public IPv4 address as the existing `hrms` record. The wildcard covers newly created company
   slugs without adding a record for every company.
3. Issue a certificate containing **both** `hrms.ssym.co.in` and
   `*.hrms.ssym.co.in`. Let's Encrypt requires DNS-01 validation for
   the wildcard. Configure automatic renewal using a narrowly scoped DNS API
   credential; a manually renewed certificate is not suitable for production.
4. Extend the existing HTTPS Nginx server block with
   `server_name hrms.ssym.co.in *.hrms.ssym.co.in;` and
   point `ssl_certificate` and `ssl_certificate_key` at the new certificate.
   Keep the existing Angular `try_files` rule and API/SignalR proxy rules.
   In each proxied API/SignalR location, set `proxy_set_header Host $host;` so
   ASP.NET receives the company hostname. Keep the API port private to the VM.
5. Run `sudo nginx -t` and reload Nginx, then verify both the platform host and
   one existing company slug over HTTPS before announcing the URLs.

The API's `Tenancy:BaseDomain` setting defaults to
`hrms.ssym.co.in` in `appsettings.json` and can be overridden with
`Tenancy__BaseDomain` for another environment. The Angular production build
uses the same domain in `environment.production.ts`. Keep these values aligned.
For local development, visit `<slug>.localhost:4200` after restarting Angular
with the checked-in dev-server settings. It binds to `127.0.0.1` so Windows can
reach company subdomains; the Angular proxy preserves the host when forwarding
to the API. Plain `localhost:4200` is the platform workspace. See the
[frontend local-workspace guide](../frontend/README.md#local-company-workspaces)
for commands and a workspace verification request.

## Email and migration

Before enabling platform email on the Oracle Ubuntu service, configure a persistent
ASP.NET Core Data Protection key ring. Set `DataProtection__KeysPath=/var/lib/hrms/keys`
in the API service environment. Create that directory outside the release directory,
give only the API service account read/write access, and include it in backups. Keep
the same directory across API restarts and deployments. If the service already has
an SMTP password saved, preserve its existing Data Protection key files when moving
to this directory; otherwise re-enter and save the SMTP key after the move. Multiple
API instances must use the same key ring. Container deployments must mount it on a
persistent volume.

After deployment, use **Test SMTP directly** and then **Test background delivery**
in platform email settings. The second test follows the same outbox and worker as
meeting, leave, work, and account emails and shows whether SMTP accepted it or why
it is retrying. Keep the API service running and monitor its email delivery warnings.
SMTP acceptance does not guarantee when a recipient's mailbox provider displays the
message.

The platform email configuration should keep its public application base URL
set to `https://hrms.ssym.co.in`. The worker now derives each
company's URL from its slug. New account invitations and notification links
therefore open on the correct company host. Existing secure links sent to the
platform host are redirected to their company host before redemption. Older
`/login?tenant=<slug>` links are redirected as well.

Do not enable the DNS wildcard before the certificate and Nginx host routing
are ready. The code can be deployed first, but new company email URLs will
only work after these infrastructure steps are complete.
