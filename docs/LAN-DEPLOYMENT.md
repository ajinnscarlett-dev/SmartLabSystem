# Trusted LAN deployment

Deploy the server and client together: heartbeat and logout now require the session ID returned by PC login. Older clients must be upgraded.

## Development
Use Development environment and HTTP on a trusted local test machine without device tokens. Existing UDP discovery remains available. Never deploy this compatibility mode to an untrusted LAN.

## School LAN
1. Use a stable DNS hostname (for example smartlab.school.example) mapped to the server's current IP. Configure Kestrel HTTPS with a certificate whose subject matches that hostname and whose issuing CA is trusted by every Windows client. Supply certificate path/password through protected deployment configuration, never Git. Do not disable certificate validation.
2. Set client SMARTLAB_SERVER_URL=https://your-trusted-host:port/. A configured HTTPS endpoint never falls back to unauthenticated HTTP discovery. DNS handles server IP changes; PC MAC identity and reported dynamic workstation IP remain intact.
3. Generate a different cryptographically random 32-byte secret for each PC (Base64 encoded). Store it as SMARTLAB_DEVICE_TOKEN in the client process environment, provided by a restricted deployment launcher/account. Do not put tokens in source-controlled appsettings.json.
4. Store only SHA-256 hex digests of those tokens on the server, under SmartLab:DeviceTokenHashes:<exact PCNumber>. Environment syntax is SmartLab__DeviceTokenHashes__601-PC01; for names inconvenient in shell variables, use protected deployment configuration. Set SmartLab__RequireDeviceCredentials=true explicitly. Production defaults to requiring them.
5. Keep Jwt signing keys and database credentials in protected configuration. Disable development bootstrap. Configure SQL Server encryption and certificate trust appropriate to the school; the checked-in SQL Express TrustServerCertificate setting is a development default.
6. Restart client processes after provisioning/rotation. Rotate a compromised PC token and its server digest together. Missing/invalid tokens fail closed with HTTP 403. Logs never include tokens.
7. Limit firewall exposure to the configured HTTPS port; UDP 5048 is only needed for development discovery. Verify untrusted certificates and an HTTP downgrade fail.

Tradeoff: this is a bearer device credential, not hardware attestation. A user or administrator who can read the running client process can steal it. Use school-managed accounts and restrict local admin privileges. HTTPS protects transit; MAC matching remains an additional registration check. No custom PKI or certificate bypass is introduced.

## Database upgrade
Back up before applying migrations. The unique non-null CurrentUserId index deliberately rejects pre-existing duplicate owners. Review and resolve duplicates with MIS rather than silently deleting assignments. Offline ownership is retained until the same student recovers or an administrator explicitly releases it.

GET /api/UsageHistory?pcId=<id> exposes the existing usage session ID to authorized staff. Admin recovery is POST /api/PC/<id>/release-session with {"sessionId":123,"reason":"verified abandoned session"}. It requires the current session and records an activity entry. The PC stays Offline until presence establishes reachability.

Build/automated tests are not evidence of trusted certificate installation, physical Windows input, LAN throughput, or a five-PC pilot.
