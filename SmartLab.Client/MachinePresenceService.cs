using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
            if (_worker != null)
            {
                return;
            }

            _worker = RunAsync(_cancellationTokenSource.Token);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Once authenticated, the existing student session heartbeat
                    // becomes the authoritative heartbeat. This avoids duplicate
                    // heartbeat loops after login.
                    if (!AuthSession.IsAuthenticated)
                    {
                        await SendPresenceAsync(cancellationToken);
                    }
                }
                catch
                {
                    // Presence must never prevent the WPF client from starting.
                }

                try
                {
                    await Task.Delay(_interval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task SendPresenceAsync(CancellationToken cancellationToken)
        {
            bool serverFound = await SmartLabServerConfig.ResolveServerAsync();

            if (!serverFound)
            {
                return;
            }

            _httpClient.BaseAddress = new Uri(SmartLabServerConfig.BaseUrl);

            string macAddress = GetMacAddress() ?? string.Empty;
            string? ipAddress = GetLocalIPv4Address();
            string pcNumber = PCConfig.PCNumber;

            if (string.IsNullOrWhiteSpace(pcNumber) ||
                string.IsNullOrWhiteSpace(macAddress))
            {
                return;
            }

            var request = new MachinePresenceRequest
            {
                PCNumber = pcNumber,
                MACAddress = macAddress,
                IPAddress = ipAddress
            };

            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "api/PC/presence",
                request,
                cancellationToken);

            // 403/409 intentionally remain non-fatal: they indicate that this
            // physical machine is not the registered owner of the PC number.
        }

        private static string? GetMacAddress()
        {
            try
            {
                foreach (NetworkInterface networkInterface in
                         NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    byte[] bytes = networkInterface.GetPhysicalAddress().GetAddressBytes();

                    if (bytes.Length == 6)
                    {
                        return string.Join("-", bytes.Select(b => b.ToString("X2")));
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string? GetLocalIPv4Address()
        {
            try
            {
                foreach (NetworkInterface networkInterface in
                         NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (UnicastIPAddressInformation address in
                             networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        string ip = address.Address.ToString();

                        if (!ip.StartsWith("169.254.", StringComparison.Ordinal))
                        {
                            return ip;
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cancellationTokenSource.Cancel();
            _httpClient.Dispose();
            _cancellationTokenSource.Dispose();
        }

        private sealed class MachinePresenceRequest
        {
            public string PCNumber { get; init; } = string.Empty;
            public string MACAddress { get; init; } = string.Empty;
            public string? IPAddress { get; init; }
        }
    }
}
