using CommandLine;

namespace UmaDecryptor.Commands;

[Verb("uma-dir", HelpText = "处理 UMA 游戏目录进行数据解密")]
public class UmaDirOptions
{
    [Option('i', "input", Required = true, HelpText = "UMA 游戏目录路径 (包含 meta、master、dat 文件夹)")]
    public string InputPath { get; set; } = string.Empty;

    [Option('o', "output", HelpText = "输出目录路径 (使用 --info 时不需要)")]
    public string? OutputPath { get; set; }

    [Option('k', "key", HelpText = "数据库解密密钥 (十六进制字符串，如: AABBCCDD...)")]
    public string? DatabaseKey { get; set; }

    [Option('t', "threads", HelpText = "dat 文件并行处理线程数 (默认: CPU 核心数)")]
    public int? MaxThreads { get; set; }

    [Option("info", HelpText = "仅显示目录信息")]
    public bool InfoOnly { get; set; } = false;

    [Option("overwrite", HelpText = "覆盖已存在的文件 (全量更新模式)")]
    public bool Overwrite { get; set; } = false;

    [Option('v', "verbose", HelpText = "启用详细日志")]
    public bool Verbose { get; set; } = false;
}