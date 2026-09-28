using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartLab.Client
{
    public sealed class MachinePresenceService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Func<CancellationToken, Task<Uri?>> _resolveServer;
        private readonly Func<MachinePresenceRequest?> _getIdentity;
        private readonly Func<bool> _isAuthenticated;
        private readonly bool _ownsClient;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
        private Task? _worker;
        private bool _disposed;

        public string ConnectionState { get; private set; } = "CONNECTING";

        public MachinePresenceService() : this(
            new HttpClient { Timeout = TimeSpan.FromSeconds(8) },
            async token =>
            {
                token.ThrowIfCancellationRequested();
                return await SmartLabServerConfig.ResolveServerAsync()
                    ? new Uri(SmartLabServerConfig.BaseUrl) : null;
            },
            () =>
            {
                var identity = GetMachineNetworkIdentity();
                return identity == null ? null : new MachinePresenceRequest
                {
                    PCNumber = PCConfig.PCNumber,
                    MACAddress = identity.MacAddress,
                    IPAddress = identity.IPAddress
                };
            },
            () => AuthSession.IsAuthenticated,
            true) { }

        internal MachinePresenceService(HttpClient client,
            Func<CancellationToken, Task<Uri?>> resolveServer,
            Func<MachinePresenceRequest?> getIdentity,
            Func<bool> isAuthenticated, bool ownsClient = false)
        {
            _httpClient = client;
            _resolveServer = resolveServer;
            _getIdentity = getIdentity;
            _isAuthenticated = isAuthenticated;
            _ownsClient = ownsClient;
        }

        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_worker != null) return;
            _worker = RunAsync(_cancellationTokenSource.Token);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!_isAuthenticated())
                        await SendPresenceAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    ConnectionState = "RECONNECTING";
                    ClientLog.Write("Error", "PresenceUnexpectedFailure", exception: ex);
                }

                try { await Task.Delay(_interval, cancellationToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        internal async Task SendPresenceAsync(CancellationToken cancellationToken)
        {
            try
            {
                Uri? server = await _resolveServer(cancellationToken);
                if (server == null)
                {
                    SetFailure("SERVER OFFLINE", "PresenceServerUnreachable");
                    return;
                }
                MachinePresenceRequest? request = _getIdentity();
                if (request == null || string.IsNullOrWhiteSpace(request.PCNumber))
                {
                    SetFailure("INVALID WORKSTATION IDENTITY", "PresenceIdentityUnavailable");
                    return;
                }

                // Reuse the connection pool. Absolute URIs allow rediscovery without
                // mutating BaseAddress after the first request.
                using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                    new Uri(server, "api/PC/presence"), request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    string state = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Conflict
                        ? "WORKSTATION REGISTRATION MISMATCH" : "SERVER ERROR";
                    SetFailure(state, "PresenceHttpFailure", (int)response.StatusCode);
                    return;
                }
                if (ConnectionState != "CONNECTED")
                    ClientLog.Write("Information", "PresenceConnected", new { previousState = ConnectionState });
                ConnectionState = "CONNECTED";
                ClientLog.Write("Debug", "PresenceHeartbeatSucceeded");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                SetFailure("RECONNECTING", "PresenceTimeout");
            }
            catch (HttpRequestException ex)
            {
                SetFailure("SERVER OFFLINE", "PresenceNetworkFailure");
                ClientLog.Write("Warning", "PresenceTransportError", exception: ex);
            }
        }

        private void SetFailure(string state, string eventName, int? statusCode = null)
        {
            ConnectionState = state;
            ClientLog.Write("Warning", eventName, new { state, statusCode });
        }

        public static string? GetMacAddress()
        {
            return GetMachineNetworkIdentity()?.MacAddress;
        }

        private static MachineNetworkIdentity? GetMachineNetworkIdentity()
        {
            try
            {
                IEnumerable<NetworkInterface> candidates = NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(IsUsableInterface)
                    .OrderByDescending(IsPreferredLanInterface)
                    .ThenByDescending(IsLikelyLanInterface);

                foreach (NetworkInterface networkInterface in candidates)
                {
                    byte[] bytes = networkInterface.GetPhysicalAddress().GetAddressBytes();
                    if (bytes.Length != 6)
                        continue;

                    string? ipAddress = GetUsableIPv4Address(networkInterface);
                    if (ipAddress == null)
                        continue;

                    return new MachineNetworkIdentity(
                        string.Join("-", bytes.Select(b => b.ToString("X2"))),
                        ipAddress);
                }
            }
            catch (Exception ex) { ClientLog.Write("Warning", "NetworkIdentityUnavailable", exception: ex); }

            return null;
        }

        private static bool IsUsableInterface(NetworkInterface networkInterface)
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                return false;

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                return false;

            try
            {
                return GetUsableIPv4Address(networkInterface) != null &&
                       networkInterface.GetPhysicalAddress().GetAddressBytes().Length == 6;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPreferredLanInterface(NetworkInterface networkInterface)
        {
            return networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                   networkInterface.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet ||
                   networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
        }

        private static bool IsLikelyLanInterface(NetworkInterface networkInterface)
        {
            string description = (networkInterface.Description ?? string.Empty).ToLowerInvariant();
            string name = (networkInterface.Name ?? string.Empty).ToLowerInvariant();

            string[] virtualMarkers =
            {
                "virtual",
                "vpn",
                "tunnel",
                "loopback",
                "hyper-v",
                "vmware",
                "virtualbox",
                "zerotier",
                "tailscale",
                "wireguard",
                "hamachi"
            };

            return !virtualMarkers.Any(marker =>
                description.Contains(marker, StringComparison.Ordinal) ||
                name.Contains(marker, StringComparison.Ordinal));
        }

        private static string? GetUsableIPv4Address(NetworkInterface networkInterface)
        {
            try
            {
                foreach (UnicastIPAddressInformation address in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    IPAddress ipAddress = address.Address;
                    if (IPAddress.IsLoopback(ipAddress) || ipAddress.Equals(IPAddress.Any))
                        continue;

                    string ip = ipAddress.ToString();
                    if (ip.StartsWith("169.254.", StringComparison.Ordinal))
                        continue;

                    return ip;
                }
            }
            catch { }

            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cancellationTokenSource.Cancel();
            _ = (_worker ?? Task.CompletedTask).ContinueWith(_ =>
            {
                if (_ownsClient) _httpClient.Dispose();
                _cancellationTokenSource.Dispose();
            }, TaskScheduler.Default);
        }

        private sealed class MachineNetworkIdentity
        {
            public MachineNetworkIdentity(string macAddress, string ipAddress)
            {
                MacAddress = macAddress;
                IPAddress = ipAddress;
            }

            public string MacAddress { get; }
            public string IPAddress { get; }
        }

        internal sealed class MachinePresenceRequest
        {
            public string PCNumber { get; init; } = string.Empty;
            public string MACAddress { get; init; } = string.Empty;
            public string? IPAddress { get; init; }
        }
    }
}
