# Physical Windows/LAN acceptance checklist

Status: NOT VERIFIED. Automated tests do not complete any checkbox below.
Tester: ______  Date/time: ______  Branch/commit: ______
Server OS/SQL version: ______  Client build: ______  Certificate hostname: ______
Heartbeat timeout/monitor interval: ______  Lab/VLAN: ______

Record each result as PASS/FAIL/NOT RUN, with timestamp, expected state, actual state, log filename, and defect reference. Do not continue to a larger pilot with unresolved ownership, authorization, or remote STOP defects.

## Preparation
- [ ] Back up the school database. Review duplicate CurrentUserId assignments before migrating.
- [ ] Upgrade server and all clients together; confirm migration and unique owner index.
- [ ] Configure trusted HTTPS, unique workstation tokens, registered PC numbers/MACs, accounts and teacher schedules.
- [ ] Confirm a copied PC number/MAC with no token or the wrong device token gets 403.
- [ ] Confirm an untrusted certificate fails and HTTPS does not fall back to HTTP discovery.
- [ ] Prepare two students, one teacher with a current lab schedule, an unauthorized lab, and Admin/MIS.

## Level 1: one idle PC
- [ ] Start SQL Server, server, and client. Leave the login screen open for 5–10 minutes.
- [ ] In Admin, confirm the PC stays Available/Online across repeated heartbeats.
- [ ] Disconnect network/server. Wait longer than configured heartbeat timeout plus monitor interval.
- [ ] Confirm Offline; restore network/server and confirm Available recovers.
- [ ] Repeat server restart and workstation IP change; confirm PC number/MAC still identify the same PC.
- [ ] Review client ClientEvents.jsonl and server logs: meaningful unreachable/reconnect events, no silent request failures.

## Level 2: student ownership (two PCs)
- [ ] Student A logs into PC01; PC01 becomes Occupied and one open usage session exists.
- [ ] Student A attempts PC02: rejected; PC01 ownership remains unchanged.
- [ ] Student B attempts PC01: rejected.
- [ ] Repeat near-simultaneous logins (same student/two PCs, then two students/same PC); only one claim succeeds.
- [ ] A logs out; the exact PC/session releases, PC01 becomes Available, history closes.
- [ ] B logs into PC01; ownership changes correctly.
- [ ] Replay an old logout/heartbeat for A's former session: rejected, B remains owner.

## Level 3: crash and outage recovery
- [ ] Log in, then kill the client unexpectedly.
- [ ] Wait beyond heartbeat timeout: PC becomes Offline but ownership remains.
- [ ] Restart the client; the same student recovers the same session on that PC.
- [ ] Verify another student cannot claim retained ownership, even after timeout.
- [ ] Disconnect/reconnect while occupied: Occupied → Offline → Occupied.
- [ ] Restart server with an occupied PC: ownership and history survive; client reconnects.
- [ ] Admin/MIS verifies an abandoned session, obtains its sessionId from UsageHistory, and submits release-session with a reason.
- [ ] An outdated admin sessionId is rejected. The correct release is logged; fresh presence restores Available.
- [ ] Maintenance/disabled PCs remain protected through presence, heartbeat, logout and recovery.

## Level 4: teacher
- [ ] Authorized lab: PC list/details and monitoring work.
- [ ] Unauthorized lab: list/details, monitoring, screen share, hardware data, commands and command status denied.
- [ ] Exercise Need Assistance and permitted commands in the authorized lab.
- [ ] End/cancel the teacher schedule: subsequent sensitive requests are denied immediately.
- [ ] Remote keys/mouse before an active remote session are denied.
- [ ] Student accounts cannot issue shutdown/restart/block/unblock/remote-control commands.

## Level 5: Admin regression
- [ ] Login and required password change.
- [ ] PC registration, details, network identity, laboratory assignment.
- [ ] Monitoring, current previews, stale/dim previews and age tooltip.
- [ ] Maintenance start/clear and inventory.
- [ ] User creation, disabled accounts, password changes.
- [ ] Service Desk, announcements, assistance, reports and usage history.
- [ ] Navigate away/back, resize, switch labs, and close/reopen dashboard: no duplicate work or stale image from a previous owner.

## Level 6: remote control
For each position record actual viewer/target coordinates, expected result, actual result and PASS/FAIL.

| Position | Viewer x,y | Target x,y | Expected | Actual | Result |
|---|---|---|---|---|---|
| Top-left | | | | | |
| Top-center | | | | | |
| Top-right | | | | | |
| Center | | | | | |
| Bottom-left | | | | | |
| Bottom-center | | | | | |
| Bottom-right | | | | | |

- [ ] Repeat after viewer resize and with a different aspect ratio.
- [ ] Keyboard, left click and right click.
- [ ] ESC, STOP, closing viewer and disconnect: input stops safely.
- [ ] Reconnect and explicitly re-establish the remote session.
- [ ] Old command/remote authorization cannot carry into a new student session.
- [ ] Multiple monitors, if available; record monitor layout and Windows scaling.

## Five-PC pilot (only after earlier levels pass)
Use PC01–PC05 and five distinct students.
- [ ] Simultaneous heartbeat and login.
- [ ] Admin and teacher monitoring, screen sharing and assistance.
- [ ] Announcements and permitted commands.
- [ ] Concurrent logout, client restart, network outage/reconnect and server restart.
- [ ] Scrolling previews favors visible PCs; a slow PC does not stall other previews.
- [ ] Closing the dashboard stops its local thumbnail work; capture stops when no viewer renews the monitoring lease (up to 30 seconds).
- [ ] Collect measurements below at idle, occupied, monitoring and recovery load.
- [ ] Proceed to one entire laboratory only after recording and reviewing these results.

| Scenario/time | PC count | Server/client CPU | RAM | Network bytes/sec | Preview delay | Heartbeat gaps | Failed requests | Result |
|---|---|---|---|---|---|---|---|---|
| Idle | 5 | | | | | | | |
| Occupied | 5 | | | | | | | |
| Monitoring | 5 | | | | | | | |
| Reconnect | 5 | | | | | | | |

No performance numbers are prefilled. Measure with Windows tools and application logs. Record sample duration and whether each metric is per-PC or aggregate.

## Evidence and sign-off
Physical Windows: NOT VERIFIED
Physical school SQL Server deployment: NOT VERIFIED
LAN: NOT VERIFIED
Remote control: NOT VERIFIED
Five-PC pilot: NOT VERIFIED
Tester/signature/date: ______
Outstanding defects and retest evidence: ______
