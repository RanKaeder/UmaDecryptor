using Microsoft.Extensions.Logging;
using UmaDecryptor.Core;

namespace UmaDecryptor.Database;

/// <summary>
/// UMA数据库密钥管理器
/// </summary>
public class UmaDatabaseKeyManager
{
    private readonly ILogger<UmaDatabaseKeyManager> _logger;
    
    // UMA数据库基础密钥 (用于XOR运算生成最终密钥)
    private static readonly byte[] DATABASE_BASE_KEY = new byte[16] 
    {
        0xF1, 0x70, 0xCE, 0xA4, 0xDF, 0xCE, 0xA3, 0xE1,
        0xA5, 0xD8, 0xC7, 0x0B, 0xD1, 0x00, 0x00, 0x00
    };
    
    // UMA数据库解密密钥 - 日本服务器（32字节，需要与BaseKey进行XOR）
    private static readonly byte[] DATABASE_KEY_JAPAN = new byte[32] 
    {
        0x6D, 0x5B, 0x65, 0x33, 0x63, 0x36, 0x63, 0x25, 0x54, 0x71, 0x2D, 0x73,
        0x50, 0x53, 0x63, 0x38, 0x6D, 0x34, 0x37, 0x7B, 0x35, 0x63, 0x70, 0x23,
        0x37, 0x34, 0x53, 0x29, 0x73, 0x43, 0x36, 0x33
    };

    // UMA数据库解密密钥 - Global服务器（33字节，需要与BaseKey进行XOR）
    private static readonly byte[] DATABASE_KEY_GLOBAL = new byte[33] 
    {
        0x36, 0x23, 0x6B, 0x4C, 0x2A, 0x39,
        0x21, 0x75, 0x52, 0x26, 0x32, 0x76,
        0x25, 0x50, 0x3F, 0x35, 0x5D, 0x77,
        0x58, 0x6D, 0x40, 0x71, 0x38, 0x5E,
        0x4C, 0x31, 0x28, 0x74, 0x29, 0x59,
        0x37, 0x24, 0x53
    };

    public UmaDatabaseKeyManager(ILogger<UmaDatabaseKeyManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取数据库解密密钥（根据区域选择）
    /// </summary>
    /// <param name="region">服务器区域</param>
    public byte[] GetDatabaseDecryptionKey(Region region = Region.Japan)
    {
        byte[] baseKey = region switch
        {
            Region.Global => DATABASE_KEY_GLOBAL,
            Region.Japan => DATABASE_KEY_JAPAN,
            _ => DATABASE_KEY_JAPAN
        };

        if (baseKey.Length == 0)
        {
            _logger.LogWarning("Database decryption key is not configured for region: {Region}", region);
            throw new InvalidOperationException($"Database decryption key is not set for region: {region}");
        }

        // 生成最终密钥（与BaseKey进行XOR运算）
        byte[] finalKey = GenerateFinalKey(baseKey);

        _logger.LogDebug("Database decryption key retrieved for region {Region} (original length: {OriginalLength}, final length: {FinalLength})", 
            region, baseKey.Length, finalKey.Length);
        
        return finalKey;
    }

    /// <summary>
    /// 生成最终密钥（与DBBaseKey进行XOR运算）
    /// 这个逻辑与UmaViewer的GenFinalKey函数相同
    /// </summary>
    private byte[] GenerateFinalKey(byte[] key)
    {
        if (DATABASE_BASE_KEY.Length < 13)
        {
            throw new InvalidOperationException("Invalid Base Key length");
        }

        byte[] finalKey = new byte[key.Length];
        
        for (int i = 0; i < key.Length; i++)
        {
            finalKey[i] = (byte)(key[i] ^ DATABASE_BASE_KEY[i % 13]);
        }

        return finalKey;
    }

    /// <summary>
    /// 获取数据库解密密钥（兼容旧版本的无参数版本）
    /// </summary>
    [Obsolete("Use GetDatabaseDecryptionKey(Region region) instead")]
    public byte[] GetDatabaseDecryptionKey()
    {
        return GetDatabaseDecryptionKey(Region.Japan);
    }
}