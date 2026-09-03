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
            using UdpClient udpClient =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        DiscoveryPort));

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
