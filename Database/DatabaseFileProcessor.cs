using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;

namespace UmaDecryptor.Database;

/// <summary>
/// 数据库文件处理器 - 负责使用sqlite3mc解密UMA数据库
/// </summary>
public class DatabaseFileProcessor
{
    private readonly ILogger<DatabaseFileProcessor> _logger;

    public DatabaseFileProcessor(ILogger<DatabaseFileProcessor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解密数据库文件 - 使用sqlite3mc读取加密数据库并生成解密版本
    /// </summary>
    public async Task DecryptDatabaseFileAsync(string inputFilePath, string outputFilePath, byte[] key)
    {
        _logger.LogInformation("Decrypting database: {InputFile} -> {OutputFile}", inputFilePath, outputFilePath);

        // 检查是否为meta文件
        var isMetaFile = Path.GetFileName(inputFilePath).Equals("meta", StringComparison.OrdinalIgnoreCase);
        
        if (isMetaFile)
        {
            await DecryptMetaFileAsync(inputFilePath, outputFilePath, key);
        }
        else
        {
            // 对于其他类型的数据库文件，可以扩展处理逻辑
            await DecryptGenericDatabaseAsync(inputFilePath, outputFilePath, key);
        }
    }

    /// <summary>
    /// 解密Meta文件 - 使用sqlite3mc读取加密数据库
    /// </summary>
    private async Task DecryptMetaFileAsync(string inputFilePath, string outputFilePath, byte[] key)
    {
        _logger.LogInformation("Processing meta database file with sqlite3mc...");
        
        await Task.Run(() =>
        {
            IntPtr db = IntPtr.Zero;
            
            try
            {
                // 打开加密的数据库
                _logger.LogDebug("Opening encrypted database: {InputFile}", inputFilePath);
                db = Sqlite3MC.Open(inputFilePath);

                // 设置cipher index (根据您的代码，使用cipher index 3)
                int cfgRc = Sqlite3MC.MC_Config(db, "cipher", 3);
                _logger.LogDebug("sqlite3mc_config(cipher, 3) returned: {ReturnCode}", cfgRc);

                // 设置解密密钥
                int rcKey = Sqlite3MC.Key_SetBytes(db, key);
                if (rcKey != Sqlite3MC.SQLITE_OK)
                {
                    string em = Sqlite3MC.GetErrMsg(db);
                    throw new InvalidOperationException($"sqlite3_key returned rc={rcKey}, errmsg={em}");
                }

                // 验证数据库是否可读
                if (!Sqlite3MC.ValidateReadable(db, out string? validateErr))
                {
                    throw new InvalidOperationException($"Database validation failed after key setup: {validateErr}");
                }

                _logger.LogInformation("Successfully opened and validated encrypted database");

                // 读取数据并创建解密的数据库
                var entries = ReadMetaEntriesFromDatabase(db);
                _logger.LogInformation("Read {EntryCount} entries from encrypted database", entries.Count);

                // 创建解密后的数据库
                CreateDecryptedDatabase(outputFilePath, entries);
                
                _logger.LogInformation("Successfully created decrypted database: {OutputFile}", outputFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt meta database");
                throw;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try 
                    { 
                        Sqlite3MC.Close(db);
                        _logger.LogDebug("Closed encrypted database connection");
                    }
                    catch (Exception e) 
                    { 
                        _logger.LogError(e, "Error closing database connection");
                    }
                }
            }
        });
    }

    /// <summary>
    /// 从加密数据库中读取Meta条目
    /// </summary>
    private Dictionary<string, UmaDatabaseEntry> ReadMetaEntriesFromDatabase(IntPtr db)
    {
        var entries = new Dictionary<string, UmaDatabaseEntry>(StringComparer.Ordinal);
        
        // 查询所有列，更简洁和灵活
        string sql = "SELECT * FROM a";
        
        _logger.LogDebug("Executing query: {Sql}", sql);
        
        try
        {
            Sqlite3MC.ForEachRow(sql, db, (stmt) =>
            {
                try
                {
                    // 读取列数据 (按标准顺序：m,n,h,c,d,e)
                    string? m = Sqlite3MC.ColumnText(stmt, 0); // type
                    string? n = Sqlite3MC.ColumnText(stmt, 1); // name
                    string? h = Sqlite3MC.ColumnText(stmt, 2); // url
                    string? c = Sqlite3MC.ColumnText(stmt, 3); // checksum
                    string? d = Sqlite3MC.ColumnText(stmt, 4); // dependencies
                    string? e = Sqlite3MC.ColumnText(stmt, 5); // key

                    // 验证必要字段
                    if (string.IsNullOrEmpty(m))
                    {
                        _logger.LogWarning("Skipping row: empty type string (m)");
                        return;
                    }

                    if (string.IsNullOrEmpty(n))
                    {
                        _logger.LogWarning("Skipping row: empty name string (n)");
                        return;
                    }

                    // 创建条目 (包含所有列)
                    var entry = new UmaDatabaseEntry
                    {
                        Type = m,
                        Name = n,
                        Url = h ?? string.Empty,
                        Checksum = c,
                        Dependencies = d ?? string.Empty,
                        Key = e
                    };

                    // 添加到字典（去重）
                    if (!entries.ContainsKey(entry.Name))
                    {
                        entries.Add(entry.Name, entry);
                    }
                }
                catch (Exception exRow)
                {
                    _logger.LogError(exRow, "Error reading row from database");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing database query");
            throw;
        }

        return entries;
    }

    /// <summary>
    /// 创建解密后的SQLite数据库
    /// </summary>
    private void CreateDecryptedDatabase(string outputPath, Dictionary<string, UmaDatabaseEntry> entries)
    {
        _logger.LogDebug("Creating decrypted database: {OutputPath}", outputPath);
        
        // 确保输出目录存在
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // 删除现有文件
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        // 创建SQLite连接字符串
        var connectionString = $"Data Source={outputPath}";
        
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        
        try
        {
            // 创建表结构 (完整的6列)
            var createTableSql = @"
                CREATE TABLE a (
                    m TEXT,  -- type
                    n TEXT,  -- name  
                    h TEXT,  -- url
                    c TEXT,  -- checksum
                    d TEXT,  -- dependencies
                    e TEXT   -- key
                );
                
                CREATE INDEX IF NOT EXISTS idx_name ON a(n);
                CREATE INDEX IF NOT EXISTS idx_type ON a(m);
                CREATE INDEX IF NOT EXISTS idx_checksum ON a(c);
            ";
            
            using var createCommand = new SqliteCommand(createTableSql, connection);
            createCommand.ExecuteNonQuery();
            
            _logger.LogDebug("Created table structure with all 6 columns in decrypted database");

            // 插入数据
            using var transaction = connection.BeginTransaction();
            
            var insertSql = "INSERT INTO a (m, n, h, c, d, e) VALUES (@m, @n, @h, @c, @d, @e)";
            using var insertCommand = new SqliteCommand(insertSql, connection);
            
            insertCommand.Parameters.Add("@m", SqliteType.Text);
            insertCommand.Parameters.Add("@n", SqliteType.Text);
            insertCommand.Parameters.Add("@h", SqliteType.Text);
            insertCommand.Parameters.Add("@c", SqliteType.Text);
            insertCommand.Parameters.Add("@d", SqliteType.Text);
            insertCommand.Parameters.Add("@e", SqliteType.Text);

            int insertedCount = 0;
            foreach (var entry in entries.Values)
            {
                insertCommand.Parameters["@m"].Value = entry.Type;
                insertCommand.Parameters["@n"].Value = entry.Name;
                insertCommand.Parameters["@h"].Value = entry.Url;
                insertCommand.Parameters["@c"].Value = entry.Checksum ?? string.Empty;
                insertCommand.Parameters["@d"].Value = entry.Dependencies;
                insertCommand.Parameters["@e"].Value = entry.Key ?? string.Empty;

                insertCommand.ExecuteNonQuery();
                insertedCount++;
            }

            transaction.Commit();
            _logger.LogInformation("Inserted {InsertedCount} entries into decrypted database", insertedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating decrypted database");
            throw;
        }
    }

    /// <summary>
    /// 解密通用数据库文件 (扩展用)
    /// </summary>
    private async Task DecryptGenericDatabaseAsync(string inputFilePath, string outputFilePath, byte[] key)
    {
        _logger.LogInformation("Processing generic database file: {InputFile}", inputFilePath);
        
        // TODO: 根据需要实现其他类型数据库的解密逻辑
        await Task.Run(() =>
        {
            // 暂时直接复制文件
            File.Copy(inputFilePath, outputFilePath, overwrite: true);
            _logger.LogInformation("Copied database file: {OutputFile}", outputFilePath);
        });
    }

    /// <summary>
    /// 验证解密结果
    /// </summary>
    public async Task<bool> ValidateDecryptedFileAsync(string filePath)
    {
        try
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError("Decrypted file does not exist: {FilePath}", filePath);
                    return false;
                }

                // 检查文件大小
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    _logger.LogError("Decrypted file is empty: {FilePath}", filePath);
                    return false;
                }

                // 尝试连接SQLite数据库来验证
                return ValidateSQLiteDatabase(filePath);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating decrypted file: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// 验证SQLite数据库连接
    /// </summary>
    private bool ValidateSQLiteDatabase(string dbPath)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();
            
            // 尝试获取数据库中的表数量来验证数据库完整性
            using var command = new SqliteCommand(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';", connection);
            var tableCount = command.ExecuteScalar();
            
            connection.Close();
            
            _logger.LogInformation("SQLite database validation successful - found {TableCount} tables", tableCount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQLite database validation failed");
            return false;
        }
    }
}