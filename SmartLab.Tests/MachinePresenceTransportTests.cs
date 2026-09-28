using System.Net;
using SmartLab.Client;
using Xunit;

namespace SmartLab.Tests;

public sealed class MachinePresenceTransportTests
{
    [Fact]
    public async Task RepeatedPresenceUsesOneClientAndFollowsRediscovery()
    {
        var urls = new List<string>();
        using var client = new HttpClient(new Handler((request, _) =>
        {
            urls.Add(request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        string host = "server-one";
        using var service = Create(client, () => new Uri($"http://{host}/"));
        await service.SendPresenceAsync(default);
        host = "server-two";
        await service.SendPresenceAsync(default);
        await service.SendPresenceAsync(default);
        Assert.Equal(new[] { "server-one", "server-two", "server-two" }, urls);
        Assert.Equal("CONNECTED", service.ConnectionState);
    }

    [Fact]
    public async Task UnreachableServerThenHttpFailureThenRecoveryIsReported()
    {
        Uri? server = null;
        HttpStatusCode status = HttpStatusCode.ServiceUnavailable;
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
        using var service = Create(client, () => server);
        await service.SendPresenceAsync(default);
        Assert.Equal("SERVER OFFLINE", service.ConnectionState);
        server = new Uri("http://server/");
        await service.SendPresenceAsync(default);
        Assert.Equal("SERVER ERROR", service.ConnectionState);
        status = HttpStatusCode.OK;
        await service.SendPresenceAsync(default);
        Assert.Equal("CONNECTED", service.ConnectionState);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task IdentityFailuresAreNotReportedAsConnected(HttpStatusCode status)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
        using var service = Create(client, () => new Uri("http://server/"));
        await service.SendPresenceAsync(default);
        Assert.Equal("WORKSTATION REGISTRATION MISMATCH", service.ConnectionState);
    }

    [Fact]
    public async Task TransportFailureRecoversAndCancellationStopsWork()
    {
        bool fail = true;
        using var client = new HttpClient(new Handler((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (fail) throw new HttpRequestException("offline");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        using var service = Create(client, () => new Uri("http://server/"));
        await service.SendPresenceAsync(default);
        Assert.Equal("SERVER OFFLINE", service.ConnectionState);
        fail = false;
        await service.SendPresenceAsync(default);
        Assert.Equal("CONNECTED", service.ConnectionState);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SendPresenceAsync(cancellation.Token));
    }

    private static MachinePresenceService Create(HttpClient client, Func<Uri?> server) => new(
        client, _ => Task.FromResult(server()),
        () => new MachinePresenceService.MachinePresenceRequest { PCNumber = "601-PC01", MACAddress = "AABBCCDDEEFF" },
        () => false);

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
