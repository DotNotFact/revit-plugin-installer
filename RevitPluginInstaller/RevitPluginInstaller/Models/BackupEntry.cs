namespace RevitPluginInstaller.Models;

public class BackupEntry
{
    public string Path { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<string> Items { get; init; } = [];
    public long SizeBytes { get; init; }
}
