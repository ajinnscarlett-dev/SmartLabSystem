namespace SmartLab.Server;

internal static class BoundedFrameReader
{
    public static async Task<byte[]?> ReadAsync(Stream source, int limit, CancellationToken token)
    {
        using var output = new MemoryStream();
        byte[] buffer = new byte[81920];
        while (true)
        {
            int count = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, limit + 1 - (int)output.Length)), token);
            if (count == 0) return output.ToArray();
            if (output.Length + count > limit) return null;
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }
}