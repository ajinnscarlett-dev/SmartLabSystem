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
- Per-user first-login password-change state

By default `SmartLab:ApplyMigrationsOnStartup` is `true`, so the server applies pending migrations before serving requests. Set it to `false` in environments where migrations are applied manually.

Manual migration commands:

```powershell
dotnet ef database update --project SmartLab.Server --startup-project SmartLab.Server
```

## Machine presence and PC registration

The WPF client starts a background machine-presence service before student authentication. It resolves the Main Server from the configured `ServerUrl` or available LAN discovery, then announces:

- PC number
- MAC address
- current IPv4 address

The server only accepts anonymous presence when the PC number and MAC match an already registered workstation. An unregistered client cannot create a workstation or reassign another PC.

Initial PC registration/provisioning is an Admin/MIS operation. Student login verifies the current workstation MAC against that pre-registered PC identity before changing the PC to Occupied.

IP address is dynamic operational metadata and may change without changing workstation identity.

The four operational PC states are:

- Available
- Occupied
- Offline
- Maintenance

Maintenance has priority over automatic heartbeat/offline state changes.

## Authentication and password policy

Passwords are stored as ASP.NET Identity password hashes, not plaintext values.

Newly provisioned accounts and administrator-reset accounts are marked as requiring a password change. Login returns that state, the WPF client opens the Change Password dialog, and the server blocks operational APIs until the authenticated user completes the password change.

The authenticated change-password endpoint verifies the current password, hashes the new password, clears the requirement flag, and writes an activity log entry. Closing or cancelling the change-password dialog clears the client session.

## Remote control

Remote control uses the existing SmartLab command and screen-monitoring pipeline:

`Admin/Teacher viewer -> normalized mouse coordinates -> server command queue -> shared JSON contract -> Student WPF client -> Windows virtual desktop mapping -> SetCursorPos/SendInput`

Mouse X/Y values are normalized to `0..1`. The viewer accounts for `Stretch.Uniform` letterboxing before creating the normalized coordinates. The student client maps those coordinates against Windows virtual-desktop metrics (`SM_XVIRTUALSCREEN`, `SM_YVIRTUALSCREEN`, `SM_CXVIRTUALSCREEN`, `SM_CYVIRTUALSCREEN`), which supports negative desktop origins and multi-monitor layouts.

Only LEFT and RIGHT mouse clicks are accepted by the server. Keyboard commands use validated Windows virtual-key codes. Remote input is accepted only while an active, authorized remote-control session exists for the requesting Admin/Teacher.

The viewer refreshes live screen traffic while the remote session is open. The server also maintains a short remote-control session lease and removes the session after inactivity or when the PC is Offline, so an unexpected viewer/client failure cannot leave remote control active indefinitely.

The repository contains deterministic JSON contract tests for the remote mouse and keyboard payloads, plus controller-level tests for workstation identity and PC ownership rules. Actual Windows cursor movement, SendInput behavior, multi-monitor hardware behavior, and end-to-end remote control still require physical Windows validation.

## Production networking

Give the Main Server a stable IP, DHCP reservation, or local DNS hostname. Client PCs do not need static IP addresses.

UDP discovery is intended as a local convenience. Production routed/VLAN deployments should prefer a configured Main Server address or hostname because broadcast traffic normally does not cross routers.

Ensure the school network permits client-to-server TCP access to the SmartLab API port configured by the server application and UDP discovery only where needed.

## Student workflow

1. SmartLab Client starts.
2. Machine presence begins before login.
3. Registered workstation is observed as Available when enabled and not occupied/under maintenance.
4. Student authenticates.
5. If required, the student must complete the password-change flow before continuing.
6. Student PC login verifies the registered workstation MAC.
7. PC becomes Occupied and a persistent usage session is created.
8. Heartbeat continues.
9. Announcements and notifications are available.
10. Need Assistance can create a persistent request and notify authorized Teacher/MIS users.
11. Student logs out.
12. Usage session is closed and the PC returns to Available.

## Teacher workflow

Teacher access is restricted server-side to authorized laboratories. The existing teacher dashboard uses real PC/API data for monitoring, screen viewing, commands, Service Desk, Need Assistance, and teacher screen sharing.

## Admin/MIS workflow

Admin/MIS manages laboratories, PCs, users, teacher authorization, commands, announcements, Service Desk, maintenance, hardware inventory, usage history, reporting, analytics, archive, activity logs, and system health.

PC registration/configuration, MAC identity changes, enable/disable, maintenance, teacher laboratory authorization, user management, and other administrative mutations are enforced server-side with Admin authorization.

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
- Student login cannot auto-create an unknown workstation.
- Deactivated accounts are rejected by both persisted account state and authentication role checks.
- First-login password changes are enforced server-side; client-side UX is not the security boundary.
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
8. Run the latest EF migration before first production use if startup migration is disabled.

### Laboratory clients

1. Install the WPF SmartLab Client.
2. Set `ServerUrl` to the stable Main Server address for production.
3. Give each workstation its intended SmartLab PC number and register its MAC identity through Admin/MIS.
4. Configure SmartLab Client to start with Windows where appropriate.
5. Validate anonymous presence before student login.
6. Validate student login, heartbeat, screen sharing, and commands on one PC before expanding the deployment.

## LAN smoke testing

Use `scripts/SmartLab-LanSmokeTest.ps1` from a Windows machine on the school LAN. Start with one client, then expand to two, five, ten, one full laboratory, and multiple laboratories.

At every stage verify:

- server discovery/configuration
- pre-login presence
- registered MAC validation
- Available/Occupied/Offline/Maintenance
- first-login password-change enforcement
- student login/logout
- usage history
- dynamic IP change
- reconnect after short outage
- server restart recovery
- client restart recovery
- Teacher authorization
- command execution/result handling
- screen monitoring
- remote-control lease cleanup after an unexpected viewer close
- Need Assistance notifications

## Automated verification

The repository CI workflow uses Windows Server, restores the solution, builds Release, runs EF model validation with `dotnet ef migrations has-pending-model-changes`, and runs the full test suite. Do not bypass or disable these steps.

## Environment-specific validation

The source tree can be inspected and modified in a development environment, but final capstone deployment still requires validation against the target Windows hardware, SQL Server instance, and school LAN.

Mandatory target-environment checks include:

- physical WPF execution
- SQL Server integration against the real SmartLabDB
- Windows hardware/WMI collection
- LAN discovery and firewall validation
- cross-subnet routing validation
- actual PC command execution
- real screen-monitoring and teacher-sharing validation
- actual remote-control cursor/keyboard behavior
- multi-monitor coordinate behavior on the target hardware
- multi-PC/load testing up to the target fleet size
