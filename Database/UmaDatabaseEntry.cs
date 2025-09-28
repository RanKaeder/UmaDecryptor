namespace UmaDecryptor.Database;

/// <summary>
/// UMA数据库条目
/// </summary>
public class UmaDatabaseEntry
{
    /// <summary>文件类型 (m列)</summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>文件名 (n列)</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>URL或路径 (h列)</summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>依赖项 (d列)</summary>
    public string Dependencies { get; set; } = string.Empty;
    
    /// <summary>校验和 (c列，如果存在)</summary>
    public string? Checksum { get; set; }
    
    /// <summary>密钥 (e列，如果存在)</summary>
    public string? Key { get; set; }

    public override string ToString()
    {
        return $"[{Type}] {Name} -> {Url}";
    }
}