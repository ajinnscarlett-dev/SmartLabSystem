using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace SmartLab.Server;

internal static class MachineIdentity
{
    public static string? NormalizeMac(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = Regex.Replace(value.Trim(), "[:-]", string.Empty);
        if (!Regex.IsMatch(normalized, "^[0-9A-Fa-f]{12}$"))
        {
            return null;
        }

        return normalized.ToUpperInvariant();
    }

    public static string? NormalizeIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!IPAddress.TryParse(value.Trim(), out IPAddress? address))
        {
            return null;
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? address.MapToIPv4().ToString()
            : address.ToString();
    }
}
