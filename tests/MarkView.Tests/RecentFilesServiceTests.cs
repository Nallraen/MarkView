using System.IO;
using MarkView.Models;
using MarkView.Services;

namespace MarkView.Tests;

public sealed class RecentFilesServiceTests
{
    private readonly FakeSettingsService _settings = new();
    private readonly RecentFilesService _service;

    public RecentFilesServiceTests() => _service = new RecentFilesService(_settings);

    [Fact]
    public void Add_PutsMostRecentFirst_AndPersistsInSettings()
    {
        _service.Add(@"C:\a.md");
        _service.Add(@"C:\b.md");

        Assert.Equal([@"C:\b.md", @"C:\a.md"], _service.Items);
        Assert.Equal([@"C:\b.md", @"C:\a.md"], _settings.Settings.RecentFiles);
    }

    [Fact]
    public void Add_KeepsTenItems()
    {
        for (var i = 0; i < 12; i++)
        {
            _service.Add($@"C:\{i}.md");
        }

        Assert.Equal(RecentFilesService.MaxItems, _service.Items.Count);
        Assert.Equal(@"C:\11.md", _service.Items[0]);
        Assert.Equal(@"C:\2.md", _service.Items[^1]);
    }

    [Fact]
    public void Add_ExistingPath_MovesItToTopIgnoringCase()
    {
        _service.Add(@"C:\Docs\a.md");
        _service.Add(@"C:\b.md");

        _service.Add(@"c:\docs\A.MD");

        Assert.Equal([@"c:\docs\A.MD", @"C:\b.md"], _service.Items);
    }

    [Fact]
    public void Remove_IgnoresCase()
    {
        _service.Add(@"C:\a.md");

        _service.Remove(@"C:\A.md");

        Assert.Empty(_service.Items);
    }

    [Fact]
    public void Prune_RemovesMissingFiles()
    {
        var existing = Path.GetTempFileName();
        try
        {
            _service.Add(existing);
            _service.Add(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.md"));

            _service.Prune();

            Assert.Equal([existing], _service.Items);
        }
        finally
        {
            File.Delete(existing);
        }
    }

    [Fact]
    public void Changes_RaiseChanged_AndItemsIsANewInstance()
    {
        var raised = 0;
        _service.Changed += (_, _) => raised++;
        var before = _service.Items;

        _service.Add(@"C:\a.md");
        _service.Remove(@"C:\a.md");
        _service.Clear();

        Assert.Equal(3, raised);
        Assert.NotSame(before, _service.Items);
    }

    [Fact]
    public void Items_ReadsTheCurrentSettingsList()
    {
        _settings.Settings.RecentFiles = [@"C:\loaded.md"];

        Assert.Equal([@"C:\loaded.md"], _service.Items);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; } = new();

        public void Load()
        {
        }

        public void Save()
        {
        }
    }
}
