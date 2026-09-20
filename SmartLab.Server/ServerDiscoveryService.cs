using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SmartLab.Server
{
    public sealed class ServerDiscoveryService :
        BackgroundService
    {
        private const int DiscoveryPort = 5048;

        private const string DiscoveryRequest =
            "SMARTLAB_DISCOVERY_V1";

        private const string DiscoveryResponse =
            "SMARTLAB_SERVER_V1|5047";

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            UdpClient udpClient;

            try
            {
                udpClient = new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        DiscoveryPort));
            }
            catch (SocketException ex)
            {
                // Discovery is optional to HTTP/API availability. A stale
                // process or another service may already own UDP 5048; do not
                // let that prevent the SmartLab HTTP server from staying up.
                Console.WriteLine(
                    $"SmartLab discovery could not bind UDP {DiscoveryPort}: {ex.Message}");
                return;
            }

            using (udpClient)

            byte[] responseBytes =
                Encoding.UTF8.GetBytes(
                    DiscoveryResponse);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult received =
                        await udpClient.ReceiveAsync(
                            stoppingToken);

                    string request =
                        Encoding.UTF8.GetString(
                            received.Buffer);

                    if (!string.Equals(
                        request,
                        DiscoveryRequest,
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    await udpClient.SendAsync(
                        responseBytes,
                        responseBytes.Length,
                        received.RemoteEndPoint);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
        }
    }
}
