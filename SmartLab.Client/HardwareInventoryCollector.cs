using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SmartLab.Client
{
    public sealed record HardwareInventorySnapshot(
        string? Cpu,
        string? Ram,
        string? Storage,
        string? Gpu,
        string? OperatingSystem,
        string? MacAddress,
        string? IpAddress);

    public static class HardwareInventoryCollector
    {
        public static HardwareInventorySnapshot Collect()
        {
            return new HardwareInventorySnapshot(
                GetCpu(),
                GetRam(),
                GetStorage(),
                GetGpu(),
                Environment.OSVersion.VersionString,
                GetMacAddress(),
                GetLocalIpv4Address());
        }

        private static string? GetCpu()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_Processor");

                return searcher.Get()
                    .Cast<ManagementObject>()
                    .Select(m => m["Name"]?.ToString())
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                    ?.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static string? GetRam()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");

                var value = searcher.Get()
                    .Cast<ManagementObject>()
                    .Select(m => m["TotalPhysicalMemory"])
                    .FirstOrDefault();

                if (value == null)
                    return null;

                if (!ulong.TryParse(value.ToString(), out ulong bytes))
                    return null;

                return $"{Math.Round(bytes / 1073741824d, 1)} GB";
            }
            catch
            {
                return null;
            }
        }

        private static string GetStorage()
        {
            try
            {
                long total = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .Sum(d => d.TotalSize);

                return $"{Math.Round(total / 1_000_000_000d, 1)} GB";
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string? GetGpu()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController");

                return searcher.Get()
                    .Cast<ManagementObject>()
                    .Select(m => m["Name"]?.ToString())
                    .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                    ?.Trim();
            }
            catch
            {
                return null;
            }
        }

        private static string? GetMacAddress()
        {
            try
            {
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
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

        private static string? GetLocalIpv4Address()
        {
            try
            {
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;

                        string ip = address.Address.ToString();
                        if (!ip.StartsWith("169.254.", StringComparison.Ordinal))
                            return ip;
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
