using CommandLine;
using Microsoft.Extensions.Logging;
using UmaDecryptor.Commands;
using UmaDecryptor.Services;

namespace UmaDecryptor;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // 设置控制台编码为UTF-8以支持中文显示
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;
        System.Console.InputEncoding = System.Text.Encoding.UTF8;
        
        // 配置日志
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        try
        {
            var parseResult = Parser.Default.ParseArguments<UmaDirOptions, DecryptDbOptions, DecryptDatOptions>(args);
            
            return await parseResult.MapResult(
                async (UmaDirOptions options) =>
                {
                    // 如果启用了详细模式，调整日志级别
                    if (options.Verbose)
                    {
                        using var verboseLoggerFactory = LoggerFactory.Create(builder =>
                        {
                            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
                        });
                        var logger = verboseLoggerFactory.CreateLogger<UmaDirService>();
                        var datLogger = verboseLoggerFactory.CreateLogger<DecryptDatService>();
                        var keyManagerLogger = verboseLoggerFactory.CreateLogger<UmaDecryptor.Database.UmaDatabaseKeyManager>();
                        var keyManager = new UmaDecryptor.Database.UmaDatabaseKeyManager(keyManagerLogger);
                        var decryptDatService = new DecryptDatService(datLogger, keyManager);
                        var umaDirService = new UmaDirService(logger, decryptDatService);
                        await umaDirService.ProcessAsync(options);
                    }
                    else
                    {
                        var logger = loggerFactory.CreateLogger<UmaDirService>();
                        var datLogger = loggerFactory.CreateLogger<DecryptDatService>();
                        var keyManagerLogger = loggerFactory.CreateLogger<UmaDecryptor.Database.UmaDatabaseKeyManager>();
                        var keyManager = new UmaDecryptor.Database.UmaDatabaseKeyManager(keyManagerLogger);
                        var decryptDatService = new DecryptDatService(datLogger, keyManager);
                        var umaDirService = new UmaDirService(logger, decryptDatService);
                        await umaDirService.ProcessAsync(options);
                    }
                    return 0;
                },
                async (DecryptDbOptions options) =>
                {
                    // 如果启用了详细模式，调整日志级别
                    if (options.Verbose)
                    {
                        // 重新创建带调试级别的logger factory
                        using var verboseLoggerFactory = LoggerFactory.Create(builder =>
                        {
                            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
                        });
                        var logger = verboseLoggerFactory.CreateLogger<DecryptDbService>();
                        var decryptDbService = new DecryptDbService(logger);
                        await decryptDbService.ProcessAsync(options);
                    }
                    else
                    {
                        var logger = loggerFactory.CreateLogger<DecryptDbService>();
                        var decryptDbService = new DecryptDbService(logger);
                        await decryptDbService.ProcessAsync(options);
                    }
                    return 0;
                },
                async (DecryptDatOptions options) =>
                {
                    // 如果启用了详细模式，调整日志级别
                    if (options.Verbose)
                    {
                        // 重新创建带调试级别的logger factory
                        using var verboseLoggerFactory = LoggerFactory.Create(builder =>
                        {
                            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
                        });
                        var logger = verboseLoggerFactory.CreateLogger<DecryptDatService>();
                        var keyManagerLogger = verboseLoggerFactory.CreateLogger<UmaDecryptor.Database.UmaDatabaseKeyManager>();
                        var keyManager = new UmaDecryptor.Database.UmaDatabaseKeyManager(keyManagerLogger);
                        var decryptDatService = new DecryptDatService(logger, keyManager);
                        return await decryptDatService.ExecuteAsync(options);
                    }
                    else
                    {
                        var logger = loggerFactory.CreateLogger<DecryptDatService>();
                        var keyManagerLogger = loggerFactory.CreateLogger<UmaDecryptor.Database.UmaDatabaseKeyManager>();
                        var keyManager = new UmaDecryptor.Database.UmaDatabaseKeyManager(keyManagerLogger);
                        var decryptDatService = new DecryptDatService(logger, keyManager);
                        return await decryptDatService.ExecuteAsync(options);
                    }
                },
                errors => Task.FromResult(1)
            );
        }
        catch (Exception ex)
        {
            var logger = loggerFactory.CreateLogger<Program>();
            logger.LogError(ex, "Application error occurred");
            return 1;
        }
    }
}