using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MarkView.Services;

/// <summary>Extracts the embedded Preview/* resources to a content-addressed folder served to WebView2.</summary>
public static class PreviewAssets
{
    private const string Prefix = "Preview/";

    private static Task<string>? _folder;

    /// <summary>Folder holding preview.html and its assets (extracted once per content version). Call from the UI thread.</summary>
    public static Task<string> GetFolderAsync() => _folder ??= Task.Run(Extract);

    private static string Extract()
    {
        var assembly = typeof(PreviewAssets).Assembly;
        var files = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return (Name: name[Prefix.Length..], Content: buffer.ToArray());
            })
            .ToList();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (name, content) in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(name));
            hash.AppendData(content);
        }

        var folder = Path.Combine(AppPaths.LocalDataDir, "preview", Convert.ToHexStringLower(hash.GetHashAndReset())[..16]);
        if (Directory.Exists(folder))
        {
            return folder;
        }

        // Extract next to the target then rename, so a crash never leaves a half-written folder behind.
        var staging = folder + "." + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        foreach (var (name, content) in files)
        {
            File.WriteAllBytes(Path.Combine(staging, name), content);
        }

        try
        {
            Directory.Move(staging, folder);
        }
        catch (IOException) when (Directory.Exists(folder))
        {
            // Another instance extracted the same version meanwhile.
            Directory.Delete(staging, true);
        }

        return folder;
    }
}
