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
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
        private Task? _worker;
        private bool _disposed;

        public void Start()
        {
            if (_worker != null) return;
            _worker = RunAsync(_cancellationTokenSource.Token);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!AuthSession.IsAuthenticated)
                        await SendPresenceAsync(cancellationToken);
                }
                catch { }

                try { await Task.Delay(_interval, cancellationToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task SendPresenceAsync(CancellationToken cancellationToken)
        {
            bool serverFound = await SmartLabServerConfig.ResolveServerAsync();
            if (!serverFound) return;

            _httpClient.BaseAddress = new Uri(SmartLabServerConfig.BaseUrl);

            MachineNetworkIdentity? identity = GetMachineNetworkIdentity();
            string pcNumber = PCConfig.PCNumber;

            if (string.IsNullOrWhiteSpace(pcNumber) || identity == null)
                return;

            var request = new MachinePresenceRequest
            {
                PCNumber = pcNumber,
                MACAddress = identity.MacAddress,
                IPAddress = identity.IPAddress
            };

            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/PC/presence",
                request,
                cancellationToken);
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
            catch { }

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
            _httpClient.Dispose();
            _cancellationTokenSource.Dispose();
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

        private sealed class MachinePresenceRequest
        {
            public string PCNumber { get; init; } = string.Empty;
            public string MACAddress { get; init; } = string.Empty;
            public string? IPAddress { get; init; }
        }
    }
}
