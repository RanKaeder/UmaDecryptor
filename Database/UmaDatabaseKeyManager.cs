using Microsoft.Extensions.Logging;

namespace UmaDecryptor.Database;

/// <summary>
/// UMA数据库密钥管理器
/// </summary>
public class UmaDatabaseKeyManager
{
    private readonly ILogger<UmaDatabaseKeyManager> _logger;
    
    // UMA数据库解密密钥（已知的32字节密钥）
    private static readonly byte[] DATABASE_DECRYPTION_KEY = new byte[32] 
    {
        0x9C, 0x2B, 0xAB, 0x97, 0xBC, 0xF8, 0xC0, 0xC4,
        0xF1, 0xA9, 0xEA, 0x78, 0x81, 0xA2, 0x13, 0xF6,
        0xC9, 0xEB, 0xF9, 0xD8, 0xD4, 0xC6, 0xA8, 0xE4,
        0x3C, 0xE5, 0xA2, 0x59, 0xBD, 0xE7, 0xE9, 0xFD
    };

    public UmaDatabaseKeyManager(ILogger<UmaDatabaseKeyManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 获取数据库解密密钥
    /// </summary>
    public byte[] GetDatabaseDecryptionKey()
    {
        if (DATABASE_DECRYPTION_KEY.Length == 0)
        {
            _logger.LogWarning("Database decryption key is not configured");
            throw new InvalidOperationException("Database decryption key is not set");
        }

        _logger.LogDebug("Database decryption key retrieved (length: {KeyLength})", 
            DATABASE_DECRYPTION_KEY.Length);
        
        return DATABASE_DECRYPTION_KEY;
    }
}