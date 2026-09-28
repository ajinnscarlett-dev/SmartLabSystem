using System;
using System.IO;
using System.Text.Json;

namespace SmartLab.Client;

// Local diagnostic events never contain credentials, bearer tokens or screen data.
internal static class ClientLog
{
    private static readonly object Gate = new();

    public static void Write(string level, string eventName, object? detail = null, Exception? exception = null)
    {
        try
        {
            lock (Gate)
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartLab");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "ClientEvents.jsonl");
                if (File.Exists(path) && new FileInfo(path).Length > 5 * 1024 * 1024)
                    File.Move(path, path + ".1", true);
                File.AppendAllText(path, JsonSerializer.Serialize(new
                {
                    timestampUtc = DateTime.UtcNow,
                    level,
                    eventName,
                    detail,
                    errorType = exception?.GetType().Name
                }) + Environment.NewLine);
            }
        }
        catch (IOException) { /* Logging must remain best effort if the disk is unavailable. */ }
        catch (UnauthorizedAccessException) { /* A restricted profile must not crash networking. */ }
    }
}
