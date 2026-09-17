using System.Text.Json;
using SmartLab.Shared;
using Xunit;

namespace SmartLab.Tests;

public sealed class RemoteInputSerializationContractTests
{
    [Theory]
    [InlineData("{\"x\":0.5,\"y\":0.5,\"button\":\"LEFT\"}", 0.5, 0.5, "LEFT")]
    [InlineData("{\"x\":0,\"y\":0,\"button\":\"LEFT\"}", 0.0, 0.0, "LEFT")]
    [InlineData("{\"x\":1,\"y\":1,\"button\":\"RIGHT\"}", 1.0, 1.0, "RIGHT")]
    public void RemoteMouseServerJsonDeserializesThroughSharedContract(
        string json,
        double expectedX,
        double expectedY,
        string expectedButton)
    {
        RemoteMouseCommandPayload? payload =
            JsonSerializer.Deserialize<RemoteMouseCommandPayload>(json);

        Assert.NotNull(payload);
        Assert.Equal(expectedX, payload.X);
        Assert.Equal(expectedY, payload.Y);
        Assert.Equal(expectedButton, payload.Button);
    }

    [Fact]
    public void RemoteMouseSharedContractSerializesToServerWireFormat()
    {
        RemoteMouseCommandPayload payload =
            new RemoteMouseCommandPayload
            {
                X = 0.5,
                Y = 0.5,
                Button = "LEFT"
            };

        string json =
            JsonSerializer.Serialize(payload);

        Assert.Equal(
            "{\"x\":0.5,\"y\":0.5,\"button\":\"LEFT\"}",
            json);
    }

    [Fact]
    public void RemoteKeyServerJsonDeserializesThroughSharedContract()
    {
        const string json = "{\"keyCode\":65}";

        RemoteKeyCommandPayload? payload =
            JsonSerializer.Deserialize<RemoteKeyCommandPayload>(json);

        Assert.NotNull(payload);
        Assert.Equal(65, payload.KeyCode);
    }

    [Fact]
    public void RemoteKeySharedContractSerializesToServerWireFormat()
    {
        RemoteKeyCommandPayload payload =
            new RemoteKeyCommandPayload
            {
                KeyCode = 65
            };

        string json =
            JsonSerializer.Serialize(payload);

        Assert.Equal(
            "{\"keyCode\":65}",
            json);
    }
}
