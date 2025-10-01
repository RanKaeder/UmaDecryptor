using Microsoft.Extensions.Logging;
using UmaDecryptor.Commands;
using UmaDecryptor.Core;
using UmaDecryptor.Database;

namespace UmaDecryptor.Services;

/// <summary>
/// UMA目录处理服务
/// </summary>
public class UmaDirService
{
    private readonly ILogger<UmaDirService> _logger;
    private readonly DatabaseDecryptor _dbDecryptor;
    private readonly UmaDirectoryValidator _directoryValidator;
    private readonly DecryptDatService _decryptDatService;

    public UmaDirService(ILogger<UmaDirService> logger, DecryptDatService decryptDatService)
    {
        _logger = logger;
        _decryptDatService = decryptDatService;
        
        // 为子组件创建专用的logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        
        _dbDecryptor = new DatabaseDecryptor(loggerFactory.CreateLogger<DatabaseDecryptor>());
        _directoryValidator = new UmaDirectoryValidator(loggerFactory.CreateLogger<UmaDirectoryValidator>());
    }

    /// <summary>
    /// 异步处理UMA目录
    /// </summary>
    public async Task ProcessAsync(UmaDirOptions options)
    {
        _logger.LogInformation("🚀 Starting UMA directory processing...");
        _logger.LogInformation("📂 Input path: {InputPath}", options.InputPath);

        // 验证输入目录
        if (!await _directoryValidator.ValidateAsync(options.InputPath))
        {
            _logger.LogError("❌ Invalid UMA directory structure");
            return;
        }

        // 如果只是查看信息
        if (options.InfoOnly)
        {
            await DisplayDirectoryInfoAsync(options.InputPath);
            return;
        }

        // 设置输出目录
        var outputPath = options.OutputPath ?? Path.Combine(options.InputPath, "decrypted");
        _logger.LogInformation("📁 Output path: {OutputPath}", outputPath);

        // 创建输出目录
        Directory.CreateDirectory(outputPath);

        try
        {
            // 第一步：解密 meta 数据库
            _logger.LogInformation("📋 Step 1: Decrypting meta database...");
            var metaPath = Path.Combine(options.InputPath, "meta");
            var outputMetaPath = Path.Combine(outputPath, "meta");
            await _dbDecryptor.DecryptDatabasesAsync(options.InputPath, outputPath);

            // 第二步：拷贝 master 文件夹
            _logger.LogInformation("📁 Step 2: Copying master folder...");
            var masterSourcePath = Path.Combine(options.InputPath, "master");
            var masterOutputPath = Path.Combine(outputPath, "master");
            await CopyMasterFolderAsync(masterSourcePath, masterOutputPath, !options.Overwrite);

            // 第三步：解密 dat 文件夹
            _logger.LogInformation("🔓 Step 3: Decrypting dat folder...");
            var datSourcePath = Path.Combine(options.InputPath, "dat");
            var datOutputPath = Path.Combine(outputPath, "dat");
            await DecryptDatFolderAsync(datSourcePath, datOutputPath, outputMetaPath, options);

            _logger.LogInformation("🎉 UMA directory processing completed successfully!");
            _logger.LogInformation("✅ All files have been processed and saved to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to process UMA directory");
            throw;
        }
    }

    /// <summary>
    /// 显示目录信息
    /// </summary>
    private async Task DisplayDirectoryInfoAsync(string inputPath)
    {
        _logger.LogInformation("=== UMA Directory Information ===");
        
        // 显示meta文件信息
        var metaPath = Path.Combine(inputPath, "meta");
        await DisplayMetaFileInfoAsync(metaPath);
        
        // 显示各个目录的文件统计
        var masterPath = Path.Combine(inputPath, "master");
        var datPath = Path.Combine(inputPath, "dat");

        await DisplayFolderStatsAsync("Master", masterPath);
        await DisplayFolderStatsAsync("Dat", datPath);
    }

    /// <summary>
    /// 显示meta文件信息
    /// </summary>
    private async Task DisplayMetaFileInfoAsync(string metaPath)
    {
        await Task.Run(() =>
        {
            if (!File.Exists(metaPath))
            {
                _logger.LogWarning("Meta file not found: {Path}", metaPath);
                return;
            }

            var fileInfo = new FileInfo(metaPath);
            _logger.LogInformation("Meta database file: {FileSize:N0} bytes ({SizeMB:F2} MB)", 
                fileInfo.Length, fileInfo.Length / (1024.0 * 1024.0));
            _logger.LogInformation("Meta file last modified: {LastModified:yyyy-MM-dd HH:mm:ss}", 
                fileInfo.LastWriteTime);
        });
    }

    private async Task DisplayFolderStatsAsync(string folderName, string path)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("{FolderName} folder not found: {Path}", folderName, path);
            return;
        }

        await Task.Run(() =>
        {
            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            var totalSize = files.Sum(file => new FileInfo(file).Length);
            
            _logger.LogInformation("{FolderName}: {FileCount} files, {TotalSize:N0} bytes", 
                folderName, files.Length, totalSize);
        });
    }

    /// <summary>
    /// 拷贝 master 文件夹
    /// </summary>
    private async Task CopyMasterFolderAsync(string sourcePath, string outputPath, bool skipExisting = false)
    {
        if (!Directory.Exists(sourcePath))
        {
            _logger.LogWarning("⚠️ Master folder not found: {SourcePath}", sourcePath);
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                // 递归拷贝所有文件和子目录
                var (copiedFiles, skippedFiles) = CopyDirectory(sourcePath, outputPath, true, skipExisting);
                
                if (skipExisting && skippedFiles > 0)
                {
                    _logger.LogInformation("✅ Master folder: {CopiedFiles} files copied, {SkippedFiles} files skipped", 
                        copiedFiles, skippedFiles);
                }
                else
                {
                    _logger.LogInformation("✅ Copied master folder: {FileCount} files", copiedFiles);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to copy master folder");
                throw;
            }
        });
    }

    /// <summary>
    /// 解密 dat 文件夹
    /// </summary>
    private async Task DecryptDatFolderAsync(string datSourcePath, string datOutputPath, string metaPath, UmaDirOptions options)
    {
        if (!Directory.Exists(datSourcePath))
        {
            _logger.LogWarning("⚠️ Dat folder not found: {SourcePath}", datSourcePath);
            return;
        }

        if (!File.Exists(metaPath))
        {
            _logger.LogError("❌ Meta database not found: {MetaPath}", metaPath);
            throw new FileNotFoundException("Meta database file not found", metaPath);
        }

        try
        {
            // 创建 DecryptDatOptions 来调用 DecryptDatService
            var datOptions = new DecryptDatOptions
            {
                InputPath = datSourcePath,
                OutputPath = datOutputPath,
                MetaPath = metaPath,
                DatabaseKey = options.DatabaseKey,
                MaxThreads = options.MaxThreads, // 传递线程数选项
                Verbose = options.Verbose
            };

            int result = await _decryptDatService.ExecuteAsync(datOptions);
            
            if (result != 0)
            {
                throw new InvalidOperationException($"Dat decryption failed with exit code: {result}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to decrypt dat folder");
            throw;
        }
    }

    /// <summary>
    /// 递归拷贝目录
    /// </summary>
    private (int copiedFiles, int skippedFiles) CopyDirectory(string sourceDir, string destinationDir, bool recursive, bool skipExisting = false)
    {
        // 获取源目录信息
        var dir = new DirectoryInfo(sourceDir);

        // 检查源目录是否存在
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // 缓存目录信息，避免重复查询
        DirectoryInfo[] dirs = dir.GetDirectories();

        // 创建目标目录
        Directory.CreateDirectory(destinationDir);

        int copiedFiles = 0;
        int skippedFiles = 0;

        // 拷贝所有文件到目标目录
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            
            if (skipExisting && File.Exists(targetFilePath))
            {
                skippedFiles++;
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("⏭️ Skipping existing file: {FileName}", file.Name);
                }
                continue;
            }
            
            file.CopyTo(targetFilePath, true);
            copiedFiles++;
        }

        // 如果需要递归拷贝子目录
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                var (subCopied, subSkipped) = CopyDirectory(subDir.FullName, newDestinationDir, true, skipExisting);
                copiedFiles += subCopied;
                skippedFiles += subSkipped;
            }
        }

        return (copiedFiles, skippedFiles);
    }
}