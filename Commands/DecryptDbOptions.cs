using CommandLine;

namespace UmaDecryptor.Commands;

[Verb("decrypt-db", HelpText = "解密单个 UMA 数据库文件")]
public class DecryptDbOptions
{
    [Option('i', "input", Required = true, HelpText = "输入加密数据库文件路径")]
    public string InputPath { get; set; } = string.Empty;

    [Option('o', "output", Required = true, HelpText = "输出解密数据库文件路径")]
    public string OutputPath { get; set; } = string.Empty;

    [Option('k', "key", HelpText = "自定义解密密钥 (十六进制格式，可选 - 如未提供则使用默认密钥)")]
    public string? CustomKey { get; set; }

    [Option('c', "cipher", Default = 3, HelpText = "加密索引 (默认: 3)")]
    public int CipherIndex { get; set; } = 3;

    [Option('v', "verbose", HelpText = "启用详细日志")]
    public bool Verbose { get; set; } = false;
}