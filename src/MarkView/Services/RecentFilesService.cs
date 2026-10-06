using System.IO;

namespace MarkView.Services;

/// <summary>Most-recently-used files, stored in <see cref="Models.AppSettings.RecentFiles"/> (persisted with the settings).</summary>
public sealed class RecentFilesService(ISettingsService settings) : IRecentFilesService
{
    public const int MaxItems = 10;

    /// <summary>A fresh snapshot on every call, so bindings see a new instance after each change.</summary>
    public IReadOnlyList<string> Items => [.. List];

    public event EventHandler? Changed;

    // Always read through the settings: loading them may replace the list instance.
    private List<string> List => settings.Settings.RecentFiles;

    public void Add(string path)
    {
        var list = List;
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > MaxItems)
        {
            list.RemoveRange(MaxItems, list.Count - MaxItems);
        }

        OnChanged();
    }

    public void Remove(string path)
    {
        if (List.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            OnChanged();
        }
    }

    public void Clear()
    {
        List.Clear();
        OnChanged();
    }

    public void Prune()
    {
        if (List.RemoveAll(p => !File.Exists(p)) > 0)
        {
            OnChanged();
        }
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
