using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartLab.Client
{
    public static class SmartLabServerConfig
    {
        private const int ServerPort = 5047;
        private const int DiscoveryPort = 5048;
        private const int ConnectTimeoutMilliseconds = 300;
        private const int DiscoveryTimeoutMilliseconds = 1000;

        private const string DiscoveryRequest =
            "SMARTLAB_DISCOVERY_V1";

        private const string DiscoveryResponse =
            "SMARTLAB_SERVER_V1|5047";

        public static string BaseUrl { get; private set; }

        static SmartLabServerConfig()
        {
            // IMPORTANT:
            // Do not perform network discovery during startup.
            BaseUrl = LoadConfiguredServerUrl();
        }

        public static async Task<bool> ResolveServerAsync()
        {
            if (IsServerReachable(BaseUrl))
            {
                return true;
            }

            string? discovered =
                await DiscoverServerOnLocalNetworkAsync();

            if (string.IsNullOrWhiteSpace(
                discovered))
            {
                return false;
            }

            BaseUrl = discovered;
            return true;
        }

        private static string LoadConfiguredServerUrl()
        {
            string serverUrl =
                "http://localhost:5047/";

            try
            {
                string configPath =
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "appsettings.json");

                if (File.Exists(configPath))
                {
                    string json =
                        File.ReadAllText(configPath);

                    using JsonDocument document =
                        JsonDocument.Parse(json);

                    if (document.RootElement.TryGetProperty(
                        "ServerUrl",
                        out JsonElement element))
                    {
                        string? configured =
                            element.GetString();

                        if (!string.IsNullOrWhiteSpace(
                            configured))
                        {
                            serverUrl =
                                configured;
                        }
                    }
                }
            }
            catch
            {
                // Keep localhost fallback.
            }

            return EnsureTrailingSlash(
                serverUrl);
        }

        private static bool IsServerReachable(
            string baseUrl)
        {
            try
            {
                Uri uri =
                    new Uri(
                        EnsureTrailingSlash(
                            baseUrl));

                return CanConnectTcp(
                    uri.Host,
                    uri.Port > 0
                        ? uri.Port
                        : ServerPort);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string?>
            DiscoverServerOnLocalNetworkAsync()
        {
            using UdpClient udpClient =
                new UdpClient(
                    AddressFamily.InterNetwork);

            udpClient.EnableBroadcast = true;

            byte[] requestBytes =
                Encoding.UTF8.GetBytes(
                    DiscoveryRequest);

            foreach (
                IPEndPoint endpoint
                in GetBroadcastEndpoints())
            {
                try
                {
                    await udpClient.SendAsync(
                        requestBytes,
                        requestBytes.Length,
                        endpoint);
                }
                catch
                {
                    // Try next endpoint.
                }
            }

            using CancellationTokenSource timeout =
                new CancellationTokenSource(
                    DiscoveryTimeoutMilliseconds);

            while (!timeout.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult received =
                        await udpClient.ReceiveAsync(
                            timeout.Token);

                    string response =
                        Encoding.UTF8.GetString(
                            received.Buffer);

                    if (string.Equals(
                        response,
                        DiscoveryResponse,
                        StringComparison.Ordinal))
                    {
                        return
                            $"http://{received.RemoteEndPoint.Address}:{ServerPort}/";
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    break;
                }
            }

            return null;
        }

        private static
            System.Collections.Generic.List<IPEndPoint>
            GetBroadcastEndpoints()
        {
            var endpoints =
                new System.Collections.Generic.List<IPEndPoint>();

            var seen =
                new System.Collections.Generic.HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            IPEndPoint universal =
                new IPEndPoint(
                    IPAddress.Broadcast,
                    DiscoveryPort);

            seen.Add(
                universal.Address.ToString());

            endpoints.Add(
                universal);

            foreach (
                NetworkInterface networkInterface
                in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus !=
                    OperationalStatus.Up)
                {
                    continue;
                }

                if (networkInterface.NetworkInterfaceType ==
                    NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                try
                {
                    IPInterfaceProperties properties =
                        networkInterface.GetIPProperties();

                    foreach (
                        UnicastIPAddressInformation address
                        in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily !=
                            AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        IPAddress? mask =
                            address.IPv4Mask;

                        if (mask == null)
                        {
                            continue;
                        }

                        byte[] ipBytes =
                            address.Address.GetAddressBytes();

                        byte[] maskBytes =
                            mask.GetAddressBytes();

                        byte[] broadcastBytes =
                            new byte[4];

                        for (int i = 0;
                             i < 4;
                             i++)
                        {
                            broadcastBytes[i] =
                                (byte)(
                                    ipBytes[i] |
                                    (byte)~maskBytes[i]);
                        }

                        IPAddress broadcast =
                            new IPAddress(
                                broadcastBytes);

                        string broadcastText =
                            broadcast.ToString();

                        if (seen.Add(
                            broadcastText))
                        {
                            endpoints.Add(
                                new IPEndPoint(
                                    broadcast,
                                    DiscoveryPort));
                        }
                    }
                }
                catch
                {
                    // Ignore unsupported interfaces.
                }
            }

            return endpoints;
        }

        private static bool CanConnectTcp(
            string host,
            int port)
        {
            try
            {
                using TcpClient client =
                    new TcpClient();

                Task connectTask =
                    client.ConnectAsync(
                        host,
                        port);

                if (!connectTask.Wait(
                    ConnectTimeoutMilliseconds))
                {
                    return false;
                }

                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static string EnsureTrailingSlash(
            string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return
                    $"http://localhost:{ServerPort}/";
            }

            return url.EndsWith(
                "/",
                StringComparison.Ordinal)
                ? url
                : url + "/";
        }
    }
}
