using CommandLine;

namespace UmaDecryptor.Commands;

/// <summary>
/// decrypt-dat 命令选项
/// </summary>
[Verb("decrypt-dat", HelpText = "解密资源文件夹中的文件 (支持任意目录结构)")]
public class DecryptDatOptions
{
    [Option('i', "input", Required = true, HelpText = "输入路径 (包含需要解密文件的文件夹，支持任意目录结构)")]
    public string InputPath { get; set; } = string.Empty;

    [Option('o', "output", Required = true, HelpText = "输出路径 (解密后文件夹路径，保持原目录结构)")]
    public string OutputPath { get; set; } = string.Empty;

    [Option('m', "meta", Required = true, HelpText = "meta数据库文件路径 (用于获取文件名与解密密钥的映射关系)")]
    public string MetaPath { get; set; } = string.Empty;

    [Option('k', "key", Required = false, HelpText = "数据库解密密钥 (十六进制字符串，如: AABBCCDD...)")]
    public string? DatabaseKey { get; set; }

    [Option('t', "threads", Required = false, HelpText = "并行处理线程数 (默认: CPU核心数)")]
    public int? MaxThreads { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "显示详细日志")]
    public bool Verbose { get; set; }
}