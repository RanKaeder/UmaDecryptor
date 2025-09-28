using Microsoft.Extensions.Logging;
using System.Data.SQLite;
using Microsoft.Data.Sqlite;

namespace UmaDecryptor.Database;

/// <summary>
/// 数据库解密器 - 核心数据库解密功能
/// </summary>
public class DatabaseDecryptor
{
    private readonly ILogger<DatabaseDecryptor> _logger;
    private readonly UmaDatabaseKeyManager _keyManager;
    private readonly DatabaseFileProcessor _fileProcessor;

    public DatabaseDecryptor(ILogger<DatabaseDecryptor> logger)
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
    /// 解密所有数据库文件
    /// </summary>
    public async Task DecryptDatabasesAsync(string inputPath, string outputPath)
    {
        _logger.LogInformation("Starting database decryption process...");

        // 获取数据库解密密钥
        var decryptionKey = _keyManager.GetDatabaseDecryptionKey();
        _logger.LogDebug("Database decryption key obtained");

        // 扫描需要解密的数据库文件
        var databaseFiles = await ScanDatabaseFilesAsync(inputPath);
        _logger.LogInformation("Found {Count} database files to decrypt", databaseFiles.Count);

        // 处理meta文件解密 (直接输出到根目录)
        var metaFiles = databaseFiles.Where(f => f.DatabaseType == DatabaseType.Meta).ToList();
        if (metaFiles.Any())
        {
            _logger.LogInformation("Processing meta database files...");
            var metaDecryptionTasks = metaFiles.Select(async dbFile =>
            {
                await DecryptSingleDatabaseAsync(dbFile, outputPath, decryptionKey);
            });
            await Task.WhenAll(metaDecryptionTasks);
        }

        // 处理master文件夹 - 原封不动拷贝
        await CopyMasterDirectoryAsync(inputPath, outputPath);

        // 处理其他数据库文件（如果有的话）
        var otherFiles = databaseFiles.Where(f => f.DatabaseType != DatabaseType.Meta).ToList();
        if (otherFiles.Any())
        {
            _logger.LogInformation("Processing other database files...");
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            var decryptionTasks = otherFiles.Select(async dbFile =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await DecryptSingleDatabaseAsync(dbFile, outputPath, decryptionKey);
                }
                finally
                {
                    semaphore.Release();
                }
            });
            await Task.WhenAll(decryptionTasks);
        }
        
        _logger.LogInformation("Database processing completed successfully!");
    }

    /// <summary>
    /// 扫描输入目录中的数据库文件
    /// </summary>
    private async Task<List<DatabaseFileInfo>> ScanDatabaseFilesAsync(string inputPath)
    {
        return await Task.Run(() =>
        {
            var databaseFiles = new List<DatabaseFileInfo>();
            
            // 检查meta文件（直接在根目录下的加密数据库文件）
            var metaFile = Path.Combine(inputPath, "meta");
            if (File.Exists(metaFile))
            {
                _logger.LogInformation("Found meta database file: {FileSize:N0} bytes", new FileInfo(metaFile).Length);
                databaseFiles.Add(new DatabaseFileInfo
                {
                    FilePath = metaFile,
                    RelativePath = "meta",
                    FileSize = new FileInfo(metaFile).Length,
                    IsEncrypted = true, // meta文件总是加密的
                    DatabaseType = DatabaseType.Meta
                });
            }

            // 扫描其他可能的数据库目录
            var searchDirectories = new[]
            {
                Path.Combine(inputPath, "master"),
                Path.Combine(inputPath, "dat")
            };

            foreach (var dir in searchDirectories.Where(Directory.Exists))
            {
                var dirName = Path.GetFileName(dir);
                // 查找数据库文件
                var patterns = new[] { "*.db", "*.sqlite", "*.dat" };
                
                foreach (var pattern in patterns)
                {
                    var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        if (IsDatabaseFile(file))
                        {
                            var dbType = dirName.ToLower() switch
                            {
                                "master" => DatabaseType.Master,
                                "dat" => DatabaseType.Data,
                                _ => DatabaseType.Unknown
                            };

                            databaseFiles.Add(new DatabaseFileInfo
                            {
                                FilePath = file,
                                RelativePath = Path.GetRelativePath(inputPath, file),
                                FileSize = new FileInfo(file).Length,
                                IsEncrypted = CheckIfEncrypted(file),
                                DatabaseType = dbType
                            });
                        }
                    }
                }
            }

            return databaseFiles;
        });
    }

    /// <summary>
    /// 解密单个数据库文件
    /// </summary>
    private async Task DecryptSingleDatabaseAsync(DatabaseFileInfo dbInfo, string outputPath, byte[] key)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));
        }

        _logger.LogInformation("Decrypting {DatabaseType} database: {RelativePath} ({FileSize:N0} bytes)", 
            dbInfo.DatabaseType, dbInfo.RelativePath, dbInfo.FileSize);

        try
        {
            if (!dbInfo.IsEncrypted)
            {
                await CopyUnencryptedDatabaseAsync(dbInfo, outputPath);
                return;
            }

            // 根据数据库类型确定输出文件名和路径
            string outputFilePath;
            if (dbInfo.DatabaseType == DatabaseType.Meta)
            {
                // meta文件直接输出到根目录，不加后缀
                outputFilePath = Path.Combine(outputPath, "meta");
            }
            else
            {
                // 其他文件保持原有逻辑，放在databases文件夹
                var dbOutputPath = Path.Combine(outputPath, "databases");
                Directory.CreateDirectory(dbOutputPath);
                outputFilePath = Path.Combine(dbOutputPath, Path.GetFileName(dbInfo.FilePath));
            }
            
            // 对于 meta 文件，使用新的完整读取所有表的逻辑
            if (dbInfo.DatabaseType == DatabaseType.Meta)
            {
                await DecryptMetaWithAllTablesAsync(dbInfo.FilePath, outputFilePath, key);
            }
            else
            {
                // 执行解密操作（其他文件使用原逻辑）
                await _fileProcessor.DecryptDatabaseFileAsync(dbInfo.FilePath, outputFilePath, key);
            }
            
            // 验证解密结果
            if (await _fileProcessor.ValidateDecryptedFileAsync(outputFilePath))
            {
                _logger.LogInformation("Successfully decrypted {DatabaseType}: {RelativePath} -> {OutputFile}", 
                    dbInfo.DatabaseType, dbInfo.RelativePath, Path.GetFileName(outputFilePath));
            }
            else
            {
                _logger.LogWarning("Decryption completed but validation failed for: {RelativePath}", 
                    dbInfo.RelativePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt {DatabaseType} database: {RelativePath}", 
                dbInfo.DatabaseType, dbInfo.RelativePath);
            throw;
        }
    }

    /// <summary>
    /// 复制未加密的数据库文件
    /// </summary>
    private async Task CopyUnencryptedDatabaseAsync(DatabaseFileInfo dbInfo, string outputPath)
    {
        var outputFilePath = Path.Combine(outputPath, Path.GetFileName(dbInfo.FilePath));
        await Task.Run(() => File.Copy(dbInfo.FilePath, outputFilePath, overwrite: true));
    }

    /// <summary>
    /// 拷贝master文件夹到输出目录
    /// </summary>
    private async Task CopyMasterDirectoryAsync(string inputPath, string outputPath)
    {
        var masterInputPath = Path.Combine(inputPath, "master");
        var masterOutputPath = Path.Combine(outputPath, "master");

        if (!Directory.Exists(masterInputPath))
        {
            _logger.LogWarning("Master directory not found: {MasterPath}", masterInputPath);
            return;
        }

        _logger.LogInformation("Copying master directory...");
        _logger.LogInformation("Source: {SourcePath}", masterInputPath);
        _logger.LogInformation("Target: {TargetPath}", masterOutputPath);

        await Task.Run(() =>
        {
            try
            {
                // 创建目标目录
                Directory.CreateDirectory(masterOutputPath);

                // 获取源目录的所有文件和子目录
                var sourceInfo = new DirectoryInfo(masterInputPath);
                CopyDirectoryRecursively(sourceInfo, masterOutputPath);

                // 统计拷贝结果
                var copiedFiles = Directory.GetFiles(masterOutputPath, "*", SearchOption.AllDirectories);
                var totalSize = copiedFiles.Sum(file => new FileInfo(file).Length);

                _logger.LogInformation("✅ Master directory copied successfully!");
                _logger.LogInformation("📊 Copied {FileCount} files, total size: {TotalSize:N0} bytes ({SizeMB:F2} MB)", 
                    copiedFiles.Length, totalSize, totalSize / (1024.0 * 1024.0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy master directory");
                throw;
            }
        });
    }

    /// <summary>
    /// 递归复制目录及其内容
    /// </summary>
    private void CopyDirectoryRecursively(DirectoryInfo sourceDir, string targetDirPath)
    {
        // 创建目标目录
        Directory.CreateDirectory(targetDirPath);

        // 复制所有文件
        foreach (var file in sourceDir.GetFiles())
        {
            var targetFilePath = Path.Combine(targetDirPath, file.Name);
            
            try
            {
                file.CopyTo(targetFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy file: {FileName}", file.Name);
                // 继续处理其他文件，不中断整个过程
            }
        }

        // 递归复制所有子目录
        foreach (var subDir in sourceDir.GetDirectories())
        {
            var targetSubDirPath = Path.Combine(targetDirPath, subDir.Name);
            
            try
            {
                CopyDirectoryRecursively(subDir, targetSubDirPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy subdirectory: {SubDirName}", subDir.Name);
                // 继续处理其他目录，不中断整个过程
            }
        }
    }

    /// <summary>
    /// 检查文件是否为数据库文件
    /// </summary>
    private bool IsDatabaseFile(string filePath)
    {
        // 简单的数据库文件识别逻辑
        // 可以通过文件扩展名或文件头判断
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        
        // 常见的数据库文件扩展名或特殊文件名
        return extension == ".db" || extension == ".sqlite" || extension == ".dat" ||
               fileName.Contains("master") || fileName.Contains("meta");
    }

    /// <summary>
    /// 检查数据库文件是否加密
    /// </summary>
    private bool CheckIfEncrypted(string filePath)
    {
        // 对于UMA游戏，我们假设大部分文件都需要特殊处理
        // meta文件肯定是加密的，其他文件可能需要检测
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        
        if (fileName == "meta")
        {
            return true; // meta文件总是加密的
        }
        
        // 其他文件的加密检测逻辑可以后续完善
        // 目前返回false，表示直接复制
        return false;
    }

    /// <summary>
    /// 解密 meta 数据库并读取所有表（新逻辑）
    /// </summary>
    private async Task DecryptMetaWithAllTablesAsync(string inputFilePath, string outputFilePath, byte[] key)
    {
        _logger.LogInformation("Processing meta database with complete table reading...");

        await Task.Run(() =>
        {
            IntPtr db = IntPtr.Zero;

            try
            {
                // 打开加密数据库
                db = Sqlite3MC.Open(inputFilePath);

                // 设置cipher配置
                int cfgRc = Sqlite3MC.MC_Config(db, "cipher", 3);

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
                    throw new InvalidOperationException($"Database validation failed: {validateErr}");
                }

                _logger.LogInformation("✅ Successfully opened encrypted meta database");

                // 读取所有表的数据
                var allTablesData = ReadAllTablesFromDatabase(db);
                _logger.LogInformation("📊 Read data from {TableCount} tables", allTablesData.Count);

                // 创建解密后的数据库
                CreateDecryptedDatabase(outputFilePath, allTablesData);

                _logger.LogInformation("🎉 Successfully created decrypted meta database with all tables");
            }
            finally
            {
                if (db != IntPtr.Zero)
                {
                    Sqlite3MC.Close(db);
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

        _logger.LogInformation("Creating decrypted database with {TableCount} tables: {OutputPath}", allTablesData.Count, outputPath);

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

        try 
        {
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

                    _logger.LogDebug("Created table '{TableName}' with {ColumnCount} columns: {Columns}", 
                        tableName, columns.Count, string.Join(", ", columns));

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
    }
}

/// <summary>
/// 数据库文件信息
/// </summary>
public class DatabaseFileInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsEncrypted { get; set; }
    public DatabaseType DatabaseType { get; set; } = DatabaseType.Unknown;
}

/// <summary>
/// 数据库类型枚举
/// </summary>
public enum DatabaseType
{
    Unknown,
    Meta,      // 主meta数据库文件
    Master,    // master目录下的数据库文件
    Data       // dat目录下的数据文件
}