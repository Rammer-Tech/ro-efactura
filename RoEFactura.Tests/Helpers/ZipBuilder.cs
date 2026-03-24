using System.IO.Compression;
using System.Text;

namespace RoEFactura.Tests.Helpers;

/// <summary>Builds in-memory ZIP archives for UblProcessingService tests.</summary>
public static class ZipBuilder
{
    public static byte[] WithEntries(params (string fileName, string content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (fileName, content) in entries)
            {
                var entry = archive.CreateEntry(fileName);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        ms.Position = 0;
        return ms.ToArray();
    }

    /// <summary>Loads XML fixture content from embedded resources.</summary>
    public static string LoadFixture(string relativePath)
    {
        var assembly = typeof(ZipBuilder).Assembly;
        var resourceName = $"RoEFactura.Tests.Fixtures.{relativePath.Replace('/', '.').Replace('\\', '.')}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
