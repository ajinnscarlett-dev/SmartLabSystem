using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartLab.Client
{
    public static class SmartLabServerConfig
    {
        private const int ServerPort = 5047;
        private const int ConnectTimeoutMilliseconds = 250;

        public static string BaseUrl { get; }

        static SmartLabServerConfig()
        {
            string configuredUrl =
                LoadConfiguredServerUrl();

            // ==========================================================
            // FIRST: TRY THE CONFIGURED SERVER
            // ==========================================================

            if (IsServerReachable(configuredUrl))
            {
                BaseUrl =
                    EnsureTrailingSlash(
                        configuredUrl);

                return;
            }

            // ==========================================================
            // SECOND: DISCOVER SERVER ON THE LOCAL /24 NETWORK
            // ==========================================================

            string? discoveredUrl =
                DiscoverServerOnLocalNetwork();

            BaseUrl =
                !string.IsNullOrWhiteSpace(discoveredUrl)
                    ? discoveredUrl
                    : EnsureTrailingSlash(
                        configuredUrl);
        }

        // ==========================================================
        // LOAD CONFIGURED SERVER URL
        // ==========================================================

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

                if (!File.Exists(configPath))
                {
                    return serverUrl;
                }

                string json =
                    File.ReadAllText(
                        configPath);

                using JsonDocument document =
                    JsonDocument.Parse(json);

                if (document.RootElement.TryGetProperty(
                    "ServerUrl",
                    out JsonElement serverUrlElement))
                {
                    string? configuredUrl =
                        serverUrlElement.GetString();

                    if (!string.IsNullOrWhiteSpace(
                        configuredUrl))
                    {
                        serverUrl =
                            configuredUrl;
                    }
                }
            }
            catch
            {
                // Keep localhost default.
            }

            return EnsureTrailingSlash(
                serverUrl);
        }

        // ==========================================================
        // CHECK ONE SERVER ADDRESS
        // ==========================================================

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

        // ==========================================================
        // AUTOMATIC LAN SERVER DISCOVERY
        //
        // IMPORTANT:
        // This method is intentionally synchronous.
        // It avoids blocking the WPF UI thread on async continuations
        // during SmartLabServerConfig static initialization.
        // ==========================================================

        private static string? DiscoverServerOnLocalNetwork()
        {
            string? localIp =
                GetActiveLocalIPv4();

            if (string.IsNullOrWhiteSpace(localIp))
            {
                return null;
            }

            string[] parts =
                localIp.Split('.');

            if (parts.Length != 4)
            {
                return null;
            }

            if (!int.TryParse(
                parts[3],
                out int localLastOctet))
            {
                return null;
            }

            string networkPrefix =
                $"{parts[0]}.{parts[1]}.{parts[2]}.";

            ConcurrentBag<string> discovered =
                new ConcurrentBag<string>();

            ParallelOptions options =
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 64
                };

            Parallel.For(
                1,
                255,
                options,
                (host, state) =>
                {
                    if (host == localLastOctet ||
                        !discovered.IsEmpty)
                    {
                        return;
                    }

                    string candidateIp =
                        networkPrefix + host;

                    if (CanConnectTcp(
                        candidateIp,
                        ServerPort))
                    {
                        discovered.Add(
                            candidateIp);

                        state.Stop();
                    }
                });

            foreach (string ip in discovered)
            {
                return
                    $"http://{ip}:{ServerPort}/";
            }

            return null;
        }

        // ==========================================================
        // SYNCHRONOUS TCP CONNECT TEST
        // ==========================================================

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

        // ==========================================================
        // GET ACTIVE LOCAL IPV4
        // ==========================================================

        private static string? GetActiveLocalIPv4()
        {
            try
            {
                foreach (
                    NetworkInterface networkInterface
                    in NetworkInterface
                        .GetAllNetworkInterfaces())
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

                    IPInterfaceProperties properties =
                        networkInterface.GetIPProperties();

                    foreach (
                        UnicastIPAddressInformation address
                        in properties.UnicastAddresses)
                    {
                        if (address.Address.AddressFamily ==
                            AddressFamily.InterNetwork)
                        {
                            string ip =
                                address.Address.ToString();

                            if (!ip.StartsWith(
                                "169.254.",
                                StringComparison.Ordinal))
                            {
                                return ip;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Best effort.
            }

            return null;
        }

        // ==========================================================
        // NORMALIZE URL
        // ==========================================================

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
