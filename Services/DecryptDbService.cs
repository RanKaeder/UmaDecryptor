using Microsoft.Extensions.Logging;
using UmaDecryptor.Commands;
using UmaDecryptor.Database;
using Microsoft.Data.Sqlite;

namespace UmaDecryptor.Services;

/// <summary>
/// 单个数据库文件解密服务
/// </summary>
public class DecryptDbService
{
    private readonly ILogger<DecryptDbService> _logger;
    private readonly UmaDatabaseKeyManager _keyManager;
    private readonly DatabaseFileProcessor _fileProcessor;

    public DecryptDbService(ILogger<DecryptDbService> logger)
    {
        _logger = logger;
        
        // 为子组件创建专用的logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        
        _keyManager = new UmaDatabaseKeyManager(loggerFactory.CreateLogger<UmaDatabaseKeyManager>());
        _fileProcessor = new DatabaseFileProcessor(loggerFactory.CreateLogger<DatabaseFileProcessor>());
    }

    /// <summary>
    /// 解密单个数据库文件
    /// </summary>
    public async Task ProcessAsync(DecryptDbOptions options)
    {
        _logger.LogInformation("Starting database decryption...");
        _logger.LogInformation("Input: {InputPath}", options.InputPath);
        _logger.LogInformation("Output: {OutputPath}", options.OutputPath);

        try
        {
            // 验证输入文件
            if (!File.Exists(options.InputPath))
            {
                _logger.LogError("Input file does not exist: {InputPath}", options.InputPath);
                return;
            }

            var fileInfo = new FileInfo(options.InputPath);
            _logger.LogInformation("Input file size: {FileSize:N0} bytes ({SizeMB:F2} MB)", 
                fileInfo.Length, fileInfo.Length / (1024.0 * 1024.0));

            // 获取解密密钥
            byte[] decryptionKey;
            if (!string.IsNullOrEmpty(options.CustomKey))
            {
                _logger.LogInformation("Using custom decryption key");
                decryptionKey = ParseHexKey(options.CustomKey);
            }
            else
            {
                _logger.LogInformation("Using default decryption key");
                decryptionKey = _keyManager.GetDatabaseDecryptionKey();
            }

            // 创建输出目录
            var outputDir = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 执行解密
            _logger.LogInformation("Starting decryption process...");
            await DecryptSingleDatabaseAsync(options.InputPath, options.OutputPath, decryptionKey, options.CipherIndex);

            // 验证结果
            if (File.Exists(options.OutputPath))
            {
                var outputFileInfo = new FileInfo(options.OutputPath);
                _logger.LogInformation("Decryption completed successfully!");
                _logger.LogInformation("Output file size: {FileSize:N0} bytes ({SizeMB:F2} MB)", 
                    outputFileInfo.Length, outputFileInfo.Length / (1024.0 * 1024.0));

                // 验证解密结果
                var isValid = await _fileProcessor.ValidateDecryptedFileAsync(options.OutputPath);
                if (isValid)
                {
                    _logger.LogInformation("✅ Decrypted database validation passed");
                }
                else
                {
                    _logger.LogWarning("⚠️ Decrypted database validation failed - file may be corrupted");
                }
            }
            else
            {
                _logger.LogError("❌ Decryption failed - output file was not created");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database decryption failed");
            throw;
        }
    }

    /// <summary>
    /// 解密单个数据库文件的核心逻辑
    /// </summary>
    private async Task DecryptSingleDatabaseAsync(string inputPath, string outputPath, byte[] key, int cipherIndex)
    {
        await Task.Run(() =>
        {
            IntPtr db = IntPtr.Zero;

            try
            {
                // 打开加密数据库
                db = Sqlite3MC.Open(inputPath);

                // 设置cipher index
                int cfgRc = Sqlite3MC.MC_Config(db, "cipher", cipherIndex);

                // 设置解密密钥
                int rcKey = Sqlite3MC.Key_SetBytes(db, key);
                if (rcKey != Sqlite3MC.SQLITE_OK)
                {
                    string em = Sqlite3MC.GetErrMsg(db);
                    throw new InvalidOperationException($"sqlite3_key returned rc={rcKey}, errmsg={em}");
                }

                // 验证数据库可读性
                if (!Sqlite3MC.ValidateReadable(db, out string? validateErr))
                {
                    throw new InvalidOperationException($"Database validation failed after key setup: {validateErr}");
                }

                _logger.LogInformation("✅ Successfully opened and validated encrypted database");

                // 读取数据库内容 (所有表)
                var allTablesData = ReadAllTablesFromDatabase(db);
                _logger.LogInformation("📊 Read data from {TableCount} tables", allTablesData.Count);

                // 创建解密后的数据库（输出路径直接使用，不修改）
                CreateDecryptedDatabase(outputPath, allTablesData);

                _logger.LogInformation("✅ Successfully created decrypted database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database decryption process");
                throw;
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    try
                    {
                        Sqlite3MC.Close(db);
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
    /// 从数据库中读取所有表的数据
    /// </summary>
    private Dictionary<string, List<Dictionary<string, object>>> ReadAllTablesFromDatabase(IntPtr db)
    {
        var allTablesData = new Dictionary<string, List<Dictionary<string, object>>>();

        try
        {
            // 首先获取所有表名
            var tableNames = new List<string>();
            const string getTablesQuery = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            
            Sqlite3MC.ForEachRow(getTablesQuery, db, (stmt) =>
            {
                try
                {
                    string? tableName = Sqlite3MC.ColumnText(stmt, 0);
                    if (!string.IsNullOrEmpty(tableName))
                    {
                        tableNames.Add(tableName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading table name");
                }
            });

            _logger.LogInformation("🔍 Found {TableCount} tables: {Tables}", 
                tableNames.Count, string.Join(", ", tableNames));

            // 读取每个表的数据
            foreach (var tableName in tableNames)
            {
                var tableData = new List<Dictionary<string, object>>();
                
                try
                {
                    var query = $"SELECT * FROM [{tableName}]";

                    Sqlite3MC.ForEachRow(query, db, (stmt) =>
                    {
                        try
                        {
                            var entry = new Dictionary<string, object>();
                            
                            // 获取列数
                            int columnCount = Sqlite3MC.ColumnCount(stmt);
                            
                            for (int i = 0; i < columnCount; i++)
                            {
                                string columnName = Sqlite3MC.ColumnName(stmt, i) ?? $"column_{i}";
                                string? value = Sqlite3MC.ColumnText(stmt, i);
                                entry[columnName] = value ?? string.Empty;
                            }
                            
                            tableData.Add(entry);
                        }
                        catch (Exception exRow)
                        {
                            _logger.LogError(exRow, "Error reading row from table {TableName}", tableName);
                        }
                    });

                    allTablesData[tableName] = tableData;
                    _logger.LogInformation("📋 Table '{TableName}': {RowCount} rows", tableName, tableData.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to read table {TableName}", tableName);
                    // 继续处理其他表，不要让一个表的错误影响整体
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to read table information from database");
            throw;
        }

        return allTablesData;
    }

    /// <summary>
    /// 创建解密后的SQLite数据库（包含所有表）
    /// </summary>
    private void CreateDecryptedDatabase(string outputPath, Dictionary<string, List<Dictionary<string, object>>> allTablesData)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));
        }

        // 确保输出目录存在
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // 删除现有文件
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        // 创建SQLite连接字符串
        var absolutePath = Path.GetFullPath(outputPath);
        
        // 使用Microsoft.Data.Sqlite创建输出数据库
        var connectionString = $"Data Source={absolutePath}";

        // 设置工作目录为可执行文件所在目录
        var originalWorkingDir = Environment.CurrentDirectory;
        try
        {
            var exeDir = System.AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(exeDir))
            {
                Environment.CurrentDirectory = exeDir;
            }

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            try
            {
                using var transaction = connection.BeginTransaction();

            // 为每个表创建表结构并插入数据
            foreach (var tableData in allTablesData)
            {
                string tableName = tableData.Key;
                var rows = tableData.Value;
                
                if (rows.Count == 0)
                {
                    _logger.LogWarning("⚠️ Table '{TableName}' is empty, skipping", tableName);
                    continue;
                }

                try
                {
                    // 从第一行数据推断列结构
                    var firstRow = rows[0];
                    var columns = firstRow.Keys.ToList();

                    // 创建表结构
                    var columnDefs = columns.Select(col => $"[{col}] TEXT").ToList();
                    var createTableSql = $"CREATE TABLE [{tableName}] ({string.Join(", ", columnDefs)});";

                    using var createCommand = new SqliteCommand(createTableSql, connection, transaction);
                    createCommand.ExecuteNonQuery();

                    // 批量插入数据
                    var paramNames = columns.Select(col => $"@{col}").ToList();
                    var insertSql = $"INSERT INTO [{tableName}] ([{string.Join("], [", columns)}]) VALUES ({string.Join(", ", paramNames)});";
                    
                    using var insertCommand = new SqliteCommand(insertSql, connection, transaction);

                    // 添加参数
                    foreach (var col in columns)
                    {
                        insertCommand.Parameters.Add($"@{col}", SqliteType.Text);
                    }

                    int insertedCount = 0;
                    foreach (var row in rows)
                    {
                        // 设置参数值
                        foreach (var col in columns)
                        {
                            var value = row.ContainsKey(col) ? row[col] : DBNull.Value;
                            if (value == DBNull.Value)
                            {
                                insertCommand.Parameters[$"@{col}"].Value = DBNull.Value;
                            }
                            else
                            {
                                insertCommand.Parameters[$"@{col}"].Value = value?.ToString() ?? string.Empty;
                            }
                        }

                        insertCommand.ExecuteNonQuery();
                        insertedCount++;
                    }

                    _logger.LogInformation("✅ Table '{TableName}': inserted {InsertedCount} rows", tableName, insertedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to create table '{TableName}'", tableName);
                    // 继续处理其他表
                }
            }

            transaction.Commit();
            _logger.LogInformation("🎉 Successfully created decrypted database with {TableCount} tables", allTablesData.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating decrypted database");
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SQLite connection");
            throw;
        }
        finally
        {
            Environment.CurrentDirectory = originalWorkingDir;
        }
    }

    /// <summary>
    /// 解析十六进制密钥字符串
    /// </summary>
    private byte[] ParseHexKey(string hexKey)
    {
        try
        {
            // 移除可能的前缀和空格
            hexKey = hexKey.Replace("0x", "").Replace(" ", "").Replace("-", "");

            if (hexKey.Length % 2 != 0)
            {
                throw new ArgumentException("Hex key length must be even");
            }

            byte[] keyBytes = new byte[hexKey.Length / 2];
            for (int i = 0; i < keyBytes.Length; i++)
            {
                keyBytes[i] = Convert.ToByte(hexKey.Substring(i * 2, 2), 16);
            }

            return keyBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse hex key: {HexKey}", hexKey);
            throw new ArgumentException($"Invalid hex key format: {hexKey}", ex);
        }
    }
}