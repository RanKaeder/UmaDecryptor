# UmaDecryptor

UMA赛马娘游戏数据解密工具 - 一个功能完整的.NET 8控制台应用程序，支持解密UMA游戏的数据库文件和资源文件。

## 🎯 功能特性

### 核心能力
- **📋 完整数据库解密**: 解密 meta 数据库，保留所有表结构和数据（包括赛马数据等）
- **🔓 资源文件解密**: 解密游戏资源文件（AssetBundle等），支持任意目录结构
- **📁 目录批处理**: 一键处理整个游戏数据目录
- **🛠️ 灵活配置**: 支持自定义密钥和详细日志模式

### 三大命令

#### 1. `uma-dir` - 一站式目录处理 🚀
处理完整的UMA游戏目录，自动解密所有类型的数据文件。

```bash
# 处理整个游戏目录
UmaDecryptor.exe uma-dir -i "C:\Users\User\AppData\LocalLow\Cygames\umamusume" -o "C:\UMA_Decrypted"

# 使用自定义数据库密钥
UmaDecryptor.exe uma-dir -i "C:\Games\UMA" -o "C:\Games\UMA_Decrypted" -k "AABBCCDD..." -v

# 只查看目录信息，不进行解密
UmaDecryptor.exe uma-dir -i "C:\Games\UMA" --info
```

**自动处理流程:**
1. 📋 解密 meta 数据库（读取所有表，不只是'a'表）
2. 📁 拷贝 master 文件夹（原封不动保持完整性）
3. 🔓 解密 dat 文件夹（所有资源文件，保持目录结构）

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
# 解密标准dat文件夹
UmaDecryptor.exe decrypt-dat -i "C:\Game\dat" -o "C:\Game\dat_decrypted" -m "meta_decrypted"

# 解密任意结构的资源文件夹
UmaDecryptor.exe decrypt-dat -i "C:\CustomAssets" -o "C:\Output" -m "meta.db" -v
```

**灵活特性:**
- 🗂️ **任意目录结构**: 不限于 `dat/XX/FILENAME` 格式
- 📝 **智能匹配**: 根据文件名与数据库记录自动匹配密钥
- 📂 **结构保持**: 完整保留原始目录层次结构
- 🔍 **递归处理**: 自动扫描所有子目录

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
# 1. 处理完整游戏目录（推荐）
UmaDecryptor.exe uma-dir -i "C:\Users\User\AppData\LocalLow\Cygames\umamusume" -o "C:\UMA_Full_Decrypted" -v

# 2. 或者分步处理：

# 2a. 先解密数据库
UmaDecryptor.exe decrypt-db -i "meta" -o "meta_readable" -v

# 2b. 再解密资源文件
UmaDecryptor.exe decrypt-dat -i "dat_folder" -o "dat_decrypted" -m "meta_readable" -v
```

### 高级用法
```bash
# 使用自定义密钥
UmaDecryptor.exe uma-dir -i "game_dir" -o "output" -k "9C2BAB97BCF8C0C4F1A9EA7881A213F6C9EBF9D8D4C6A8E43CE5A259BDE7E9FD"

# 处理非标准目录结构
UmaDecryptor.exe decrypt-dat -i "extracted_assets" -o "decrypted_bundles" -m "meta_db"

# 查看目录信息
UmaDecryptor.exe uma-dir -i "game_dir" --info
```

## ⚠️ 重要说明

- **用途**: 本工具仅用于学习和研究目的
- **合规**: 请遵守相关游戏的服务条款和当地法律法规
- **数据**: 确保备份原始数据文件
- **兼容**: 目前仅支持Windows x64平台

## 📄 许可证

[添加适当的开源许可证]