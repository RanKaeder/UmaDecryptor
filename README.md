# UmaDe- **📋 完整数据库解密**: 解密 meta 数据库，保留所有表结构和数据（包括赛马数据等）
- **🔓 资源文件解密**: 解密游戏资源文件（AssetBundle等），支持任意目录结构
- **📁 目录批处理**: 一键处理整个游戏数据目录
- **⚡ 智能增量更新**: 默认跳过已存在文件，游戏更新后只处理新增内容
- **🔄 灵活更新模式**: 使用 `--overwrite` 选项轻松切换增量/全量模式
- **🚀 多线程并行**: 高效处理大量文件，充分利用CPU资源
- **🛠️ 灵活配置**: 支持自定义密钥和详细日志模式r

UMA赛马娘游戏数据解密工具 - 一个功能完整的.NET 8控制台应用程序，支持解密UMA游戏的数据库文件和资源文件。

## 🎯 功能特性

### 核心能力
- **📋 完整数据库解密**: 解密 meta 数据库，保留所有表结构和数据（包括赛马数据等）
- **🔓 资源文件解密**: 解密游戏资源文件（AssetBundle等），支持任意目录结构
- **📁 目录批处理**: 一键处理整个游戏数据目录
- **⚡ 增量更新**: 智能跳过已存在文件，游戏更新后只处理新增内容
- **� 多线程并行**: 高效处理大量文件，充分利用CPU资源
- **�🛠️ 灵活配置**: 支持自定义密钥和详细日志模式

### 三大命令

#### 1. `uma-dir` - 一站式目录处理 🚀
处理完整的UMA游戏目录，自动解密所有类型的数据文件。

```bash
# 处理整个游戏目录（增量模式，默认跳过已存在文件）
UmaDecryptor.exe uma-dir -i "C:\Users\User\AppData\LocalLow\Cygames\umamusume" -o "C:\UMA_Decrypted"

# 全量模式（覆盖所有已存在文件）
UmaDecryptor.exe uma-dir -i "C:\Games\UMA" -o "C:\Games\UMA_Decrypted" --overwrite

# 使用自定义数据库密钥和详细日志
UmaDecryptor.exe uma-dir -i "C:\Games\UMA" -o "C:\Games\UMA_Decrypted" -k "AABBCCDD..." -v

# 只查看目录信息，不进行解密
UmaDecryptor.exe uma-dir -i "C:\Games\UMA" --info

# 多线程并行处理（指定线程数）
UmaDecryptor.exe uma-dir -i "C:\Games\UMA" -o "C:\Games\UMA_Decrypted" -t 8
```

**自动处理流程:**
1. 📋 解密 meta 数据库（读取所有表，不只是'a'表）
2. 📁 拷贝 master 文件夹（原封不动保持完整性，支持增量）
3. 🔓 解密 dat 文件夹（所有资源文件，保持目录结构，智能跳过已存在）

#### 2. `decrypt-db` - 数据库解密 📊
解密单个数据库文件，输出完整的SQLite数据库。

```bash
# 基本解密（输出无后缀）
UmaDecryptor.exe decrypt-db -i meta -o meta_decrypted

# 使用自定义密钥
UmaDecryptor.exe decrypt-db -i meta -o meta_decrypted -k "9C2BAB97BCF8C0C4..."

# 详细日志模式
UmaDecryptor.exe decrypt-db -i meta -o meta_decrypted -v
```

**新特性:**
- ✅ **完整数据库**: 读取所有表，包括全部文件加密数据
- ✅ **表结构保持**: 完整重建所有表结构和索引
- ✅ **标准输出**: 生成标准SQLite文件，可用任何SQLite工具打开

#### 3. `decrypt-dat` - 资源文件解密 🗂️
解密资源文件夹，支持任意目录结构，不限于标准的dat文件夹格式。

```bash
# 解密标准dat文件夹（增量模式，默认跳过已存在文件）
UmaDecryptor.exe decrypt-dat -i "C:\Game\dat" -o "C:\Game\dat_decrypted" -m "meta_decrypted"

# 全量模式（覆盖所有已存在文件）
UmaDecryptor.exe decrypt-dat -i "C:\Game\dat" -o "C:\Game\dat_decrypted" -m "meta_decrypted" --overwrite

# 解密任意结构的资源文件夹
UmaDecryptor.exe decrypt-dat -i "C:\CustomAssets" -o "C:\Output" -m "meta.db" -v

# 多线程并行处理
UmaDecryptor.exe decrypt-dat -i "C:\Game\dat" -o "C:\Game\dat_decrypted" -m "meta.db" -t 16 --overwrite
```

**灵活特性:**
- 🗂️ **任意目录结构**: 不限于 `dat/XX/FILENAME` 格式
- 📝 **智能匹配**: 根据文件名与数据库记录自动匹配密钥
- 📂 **结构保持**: 完整保留原始目录层次结构
- 🔍 **递归处理**: 自动扫描所有子目录
- ⚡ **增量更新**: 默认跳过已存在文件，提升处理效率

## 🆕 v1.1.0+ 新功能

### ⚡ 智能更新模式
- **默认增量**: 自动检测并跳过已存在的文件，提升处理效率
- **简化选项**: 使用直观的 `--overwrite` 选项控制更新模式
- **灵活切换**: 随时在增量和全量模式间切换
- **高效更新**: 游戏版本更新后只需处理新增文件

### 📊 增强的进度报告
```
🚀 Starting dat files decryption
⏭️ Skip existing files: enabled (incremental mode)
📊 Progress: 1200/5000 files processed (✅800 ⏭️350 ❌50)
🎉 Decryption completed!
📊 Final stats: 5000 processed, ✅4500 success, ⏭️450 skipped, ❌50 errors
```

### 🎯 使用模式对比
```bash
# 增量模式（默认）- 跳过已存在文件
UmaDecryptor.exe decrypt-dat -i input -o output -m meta

# 全量模式 - 覆盖所有文件
UmaDecryptor.exe decrypt-dat -i input -o output -m meta --overwrite
```

### 🚀 性能优化
- **多线程并行**: 默认使用CPU核心数，可自定义线程数
- **内存优化**: 流式处理大文件，减少内存占用
- **网络友好**: 适合处理网络存储上的大型游戏目录

## 📦 快速开始

### 环境要求
- Windows x64 系统
- .NET 8 Runtime（或SDK用于开发）
- sqlite3mc_x64.dll（项目自带）

### 安装运行
```bash
# 克隆项目
git clone <repository-url>
cd UmaDecryptor

# 构建项目
dotnet build

# 查看帮助
dotnet run -- --help

# 处理游戏目录
dotnet run -- uma-dir -i "C:\Users\User\AppData\LocalLow\Cygames\umamusume" -o "C:\UMA_Decrypted" -v
```

## 📁 输出结构示例

```
输出目录/
├── meta                    # 解密后的完整数据库（包含所有表）
├── master/                 # 原封不动拷贝
│   └── (所有原始文件)
└── dat/                    # 解密后的资源文件
    ├── 2A/
    │   └── 2A2A5FYOKMKLWC6IDBUTYVFBZ7GAD73K  # 解密后保持原文件名
    ├── 3B/
    │   └── 3B3B6GZPLNMLXD7JECVUZWGCA8HBE84L
    └── ...                 # 保持完整目录结构
```

## 🔧 技术详情

### 解密算法
- **数据库**: 使用 sqlite3mc 处理 SQLCipher 加密的数据库
- **资源文件**: 
  - 前256字节保持原样
  - 从256字节开始使用密钥循环异或解密
  - 支持负数密钥（小端序处理）

### 项目架构
```
UmaDecryptor/
├── Commands/              # CLI命令定义
├── Core/                  # 核心验证组件
├── Crypto/                # 加密解密算法
├── Database/              # 数据库处理
├── Services/              # 业务逻辑服务
├── Program.cs             # 程序入口
├── sqlite3mc_x64.dll      # SQLite加密扩展
└── UmaDecryptor.csproj      # 项目配置
```

## 🚀 发布部署

### 独立可执行文件
```bash
# 发布为单文件应用
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# 输出位置
cd bin\Release\net8.0\win-x64\publish\
UmaDecryptor.exe --help
```

### 依赖项
- **CommandLineParser**: CLI参数解析
- **System.Data.SQLite**: SQLite数据库操作  
- **Microsoft.Extensions.Logging**: 结构化日志
- **sqlite3mc_x64.dll**: SQLite加密扩展（自动包含）

## 📋 使用示例

### 典型工作流程
```bash
# 1. 处理完整游戏目录（推荐）- 增量模式
UmaDecryptor.exe uma-dir -i "C:\Users\User\AppData\LocalLow\Cygames\umamusume" -o "C:\UMA_Full_Decrypted" -v

# 1a. 首次完整处理或强制全量更新
UmaDecryptor.exe uma-dir -i "C:\Users\User\AppData\LocalLow\Cygames\umamusume" -o "C:\UMA_Full_Decrypted" --overwrite -v

# 2. 或者分步处理：

# 2a. 先解密数据库
UmaDecryptor.exe decrypt-db -i "meta" -o "meta_readable" -v

# 2b. 再解密资源文件（增量模式）
UmaDecryptor.exe decrypt-dat -i "dat_folder" -o "dat_decrypted" -m "meta_readable" -v

# 2c. 强制重新解密所有资源文件
UmaDecryptor.exe decrypt-dat -i "dat_folder" -o "dat_decrypted" -m "meta_readable" --overwrite -v
```

### 高级用法
```bash
# 使用自定义密钥（增量模式）
UmaDecryptor.exe uma-dir -i "game_dir" -o "output" -k "9C2BAB97BCF8C0C4F1A9EA7881A213F6C9EBF9D8D4C6A8E43CE5A259BDE7E9FD"

# 使用自定义密钥（全量模式）
UmaDecryptor.exe uma-dir -i "game_dir" -o "output" -k "9C2BAB97BCF8C0C4F1A9EA7881A213F6C9EBF9D8D4C6A8E43CE5A259BDE7E9FD" --overwrite

# 处理非标准目录结构（增量模式）
UmaDecryptor.exe decrypt-dat -i "extracted_assets" -o "decrypted_bundles" -m "meta_db"

# 处理非标准目录结构（全量模式）
UmaDecryptor.exe decrypt-dat -i "extracted_assets" -o "decrypted_bundles" -m "meta_db" --overwrite

# 查看目录信息
UmaDecryptor.exe uma-dir -i "game_dir" --info

# 高性能并行处理（全量模式）
UmaDecryptor.exe decrypt-dat -i "large_assets" -o "output" -m "meta_db" -t 32 --overwrite
```

### 💡 使用建议
```bash
# 游戏首次解密 - 建议使用全量模式
UmaDecryptor.exe uma-dir -i "game_dir" -o "output" --overwrite

# 游戏更新后 - 使用默认增量模式
UmaDecryptor.exe uma-dir -i "game_dir" -o "output"

# 遇到问题时 - 强制重新处理
UmaDecryptor.exe uma-dir -i "game_dir" -o "output" --overwrite -v
```

## ⚠️ 重要说明

- **用途**: 本工具仅用于学习和研究目的
- **合规**: 请遵守相关游戏的服务条款和当地法律法规
- **数据**: 确保备份原始数据文件
- **兼容**: 目前仅支持Windows x64平台

## 📄 许可证

MIT License - 详见 [LICENSE](LICENSE) 文件

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

### 开发环境设置
```bash
# 克隆仓库
git clone https://github.com/RanKaeder/UmaDecryptor.git
cd UmaDecryptor

# 恢复依赖
dotnet restore

# 构建项目
dotnet build

# 运行测试
dotnet run -- --help
```

### 项目架构
```
UmaDecryptor/
├── Commands/              # CLI命令定义
│   ├── UmaDirOptions.cs      # uma-dir 命令选项
│   ├── DecryptDbOptions.cs   # decrypt-db 命令选项
│   └── DecryptDatOptions.cs  # decrypt-dat 命令选项
├── Core/                  # 核心验证组件
│   └── UmaDirectoryValidator.cs
├── Crypto/                # 加密解密算法
│   └── AssetBundleDecryptor.cs
├── Database/              # 数据库处理
│   ├── DatabaseDecryptor.cs
│   ├── UmaDatabaseKeyManager.cs
│   └── Sqlite3MC.cs
├── Services/              # 业务逻辑服务
│   ├── UmaDirService.cs      # uma-dir 服务
│   ├── DecryptDbService.cs   # decrypt-db 服务
│   └── DecryptDatService.cs  # decrypt-dat 服务
├── Program.cs             # 程序入口
├── sqlite3mc_x64.dll      # SQLite加密扩展
└── UmaDecryptor.csproj    # 项目配置
```

## ⚠️ 重要说明

- **用途**: 本工具仅用于学习和研究目的
- **合规**: 请遵守相关游戏的服务条款和当地法律法规  
- **数据**: 确保备份原始数据文件
- **兼容**: 目前仅支持Windows x64平台
- **责任**: 使用本工具所产生的任何后果由用户自行承担

## 🔗 相关链接

- [GitHub Repository](https://github.com/RanKaeder/UmaDecryptor)
- [Issues](https://github.com/RanKaeder/UmaDecryptor/issues)
- [Releases](https://github.com/RanKaeder/UmaDecryptor/releases)