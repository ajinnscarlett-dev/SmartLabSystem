using System.Security.Cryptography;
using System.Text;
using SmartLab.Server;
using Xunit;

namespace SmartLab.Tests;

public sealed class DeviceCredentialTests
{
    private const string Token = "example-test-device-token-at-least-32-characters";
    private static string Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Token)));
    [Fact] public void ProvisionedCredentialMatchesHash() => Assert.True(WorkstationCredentialFilter.Matches(Token, Hash));
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AABBCCDDEEFF")]
    [InlineData("wrong-device-token-at-least-32-characters")]
    public void MacOrWrongSecretCannotImpersonateDevice(string? supplied) => Assert.False(WorkstationCredentialFilter.Matches(supplied, Hash));
    [Fact] public void InvalidProvisioningFailsClosed() => Assert.False(WorkstationCredentialFilter.Matches(Token, new string('Z', 64)));
}