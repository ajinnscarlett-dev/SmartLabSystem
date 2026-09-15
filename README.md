# SmartLabSystem

SmartLabSystem is a centralized LAN-based computer laboratory management system built with C#/.NET 8, WPF, ASP.NET Core Web API, SQL Server, EF Core, and JWT role-based authorization.

## Architecture

One Main Server PC runs the SmartLab ASP.NET Core API, SQL Server, SmartLab database, and Admin/MIS application. Laboratory PCs run the SmartLab WPF client. Core operation is designed for the school LAN and does not require Internet access after deployment.

Client IP addresses are dynamic. PC identity is based on the registered PC record and MAC/persistent machine identity; IP address and LastSeen are operational fields.

## Laboratories

- COMLAB 101
- COMLAB 601
- COMLAB 602
- COMLAB 603
- COMLAB 604
- COMLAB 605
- COMLAB 607
- COMLAB 609

## Prerequisites

- Windows 10/11 for the WPF client
- .NET 8 SDK/runtime
- SQL Server / SQL Server Express
- Visual Studio 2022 or another .NET 8 development environment
- School LAN routing/firewall access between clients and the Main Server

## Main Server configuration

`SmartLab.Server/appsettings.json` contains the SQL Server connection string, JWT issuer/audience, heartbeat settings, and the startup migration switch.

The JWT signing key is intentionally not stored in source control. Configure it through a local secret or the `SMARTLAB_JWT_KEY` environment variable. The key must be at least 32 characters long.

Example PowerShell setup on the server:

```powershell
[Environment]::SetEnvironmentVariable(
  'SMARTLAB_JWT_KEY',
  'replace-with-a-long-random-secret',
  'Machine'
)
```

Restart the SmartLab Server after changing the environment variable.

## Database

The server contains EF Core migrations. Current schema includes:

- Users
- PCs / laboratories
- Activity logs
- Announcements
- Service Desk tickets
- Teacher laboratory authorization
- Persistent PC usage history
- Maintenance history
- Hardware inventory
- Notifications
- Student Need Assistance requests

By default `SmartLab:ApplyMigrationsOnStartup` is `true`, so the server applies pending migrations before serving requests. Set it to `false` in environments where migrations are applied manually.

Manual migration commands:

```powershell
dotnet ef database update --project SmartLab.Server --startup-project SmartLab.Server
```

## Machine presence

The WPF client starts a background machine-presence service before student authentication. It resolves the Main Server from the configured `ServerUrl` or available LAN discovery, then announces:

- PC number
- MAC address
- current IPv4 address

The server only accepts anonymous presence when the PC number and MAC match an already registered workstation. An unregistered client cannot create a workstation or reassign another PC.

The four operational PC states are:

- Available
- Occupied
- Offline
- Maintenance

Maintenance has priority over automatic heartbeat/offline state changes.

## Production networking

Give the Main Server a stable IP, DHCP reservation, or local DNS hostname. Client PCs do not need static IP addresses.

UDP discovery is intended as a local convenience. Production routed/VLAN deployments should prefer a configured Main Server address or hostname because broadcast traffic normally does not cross routers.

Ensure the school network permits client-to-server TCP access to the SmartLab API port configured by the server application and UDP discovery only where needed.

## Student workflow

1. SmartLab Client starts.
2. Machine presence begins before login.
3. Registered workstation is shown as Available.
4. Student authenticates.
5. PC becomes Occupied and a persistent usage session is created.
6. Heartbeat continues.
7. Announcements and notifications are available.
8. Need Assistance can create a persistent request and notify authorized Teacher/MIS users.
9. Student logs out.
10. Usage session is closed and the PC returns to Available.

## Teacher workflow

Teacher access is restricted server-side to authorized laboratories. The existing teacher dashboard uses real PC/API data for monitoring, screen viewing, commands, Service Desk, and teacher screen sharing.

## Admin/MIS workflow

Admin/MIS manages laboratories, PCs, users, teacher authorization, commands, announcements, Service Desk, maintenance, hardware inventory, usage history, reporting, analytics, and system health.

## Hardware inventory

The Student WPF session client performs a best-effort hardware audit using Windows management APIs and updates the inventory record for the currently assigned workstation. The collected fields are intentionally limited to operational hardware information such as CPU, RAM, storage, GPU, OS version, MAC, and IP.

## Reports and analytics

Administrative CSV exports are available through the `ReportsController` for usage, maintenance, inventory, Service Desk, and activity logs.

Administrative analytics are available through `AnalyticsController`, including most-used PCs/labs, peak usage hour, average session duration, utilization, offline events, maintenance frequency, and Service Desk volume.

Archive support provides a retention plan and historical usage CSV export. The archive endpoint does not blindly delete operational history.

## Security notes

- JWT validation checks issuer, audience, signing key, and lifetime.
- Administrative and teacher controller boundaries are enforced server-side.
- Teacher laboratory access is checked on relevant APIs.
- Student-owned operations are restricted to the authenticated student/session.
- Anonymous presence is bound to a pre-registered workstation MAC.
- Historical records use restricted foreign keys to avoid accidental cascade deletion.
- JWT signing keys must not be committed to Git.

## Physical deployment checklist

### Server PC

1. Install SQL Server.
2. Create/verify the SmartLabDB database account/Windows access.
3. Configure `DefaultConnection` for the server.
4. Configure `SMARTLAB_JWT_KEY` as a machine environment variable.
5. Choose a stable server IP/DNS name.
6. Open the required API TCP port in Windows Firewall.
7. Start SmartLab Server.

### Laboratory clients

1. Install the WPF SmartLab Client.
2. Set `ServerUrl` to the stable Main Server address for production.
3. Give each workstation its intended SmartLab PC number and registered MAC identity.
4. Configure SmartLab Client to start with Windows where appropriate.
5. Validate presence before student login.

## LAN smoke testing

Use `scripts/SmartLab-LanSmokeTest.ps1` from a Windows machine on the school LAN. Start with one client, then expand to two, five, ten, one full laboratory, and multiple laboratories.

At every stage verify:

- server discovery/configuration
- pre-login presence
- Available/Occupied/Offline/Maintenance
- student login/logout
- usage history
- dynamic IP change
- reconnect after short outage
- server restart recovery
- client restart recovery
- Teacher authorization
- command execution/result handling
- screen monitoring
- Need Assistance notifications

## Environment-specific validation

The source tree can be inspected and modified here, but this environment does not provide a Windows desktop, your SQL Server instance, or the physical school LAN. Therefore the following remain mandatory on the target environment:

- physical WPF execution
- SQL Server integration against the real SmartLabDB
- Windows hardware/WMI collection
- LAN discovery and firewall validation
- cross-subnet routing validation
- actual PC command execution
- real screen-monitoring and teacher-sharing validation
- multi-PC/load testing up to the target fleet size
