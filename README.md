# WW2_Commander

二战题材即时战略游戏 (WW2-themed RTS Game)

## 项目信息

- **Unity 版本**: 2022.3.42f1 LTS (推荐)
- **模板**: 2D Core
- **分辨率**: 1920x1080 (窗口模式)
- **输入系统**: New Input System
- **脚本运行时**: .NET Standard 2.1

## 环境要求

### 必需
- Unity Hub 3.x (最新版本)
- Unity Editor 2022.3.x LTS
- Git (版本控制)

### 可选模块
- Android Build Support
- IL2CPP Build Support
- .NET scripting backend

## 安装步骤

### 1. 安装 Unity Hub

**Linux:**
```bash
# 下载 Unity Hub AppImage
wget https://public-cdn.cloud.unity3d.com/hub/prod/UnityHub.AppImage

# 赋予执行权限
chmod +x UnityHub.AppImage

# 运行
./UnityHub.AppImage
```

**Windows:**
- 访问 https://unity.com/download
- 下载 Unity Hub 安装程序
- 运行安装程序

**macOS:**
- 访问 https://unity.com/download
- 下载 Unity Hub DMG
- 拖拽到应用程序文件夹

### 2. 安装 Unity 编辑器

1. 打开 Unity Hub
2. 点击 "Installs" → "Install Editor"
3. 选择 **2022.3.x LTS** 版本
4. 选择模块:
   - ✅ Android Build Support (可选)
   - ✅ IL2CPP Build Support
   - ✅ .NET scripting backend
5. 点击安装

### 3. 激活许可证

1. 打开 Unity Hub
2. 点击 "Preferences" → "Licenses"
3. 添加许可证 (Personal/Professional/Student)

### 4. 打开项目

1. Unity Hub → "Projects" → "Add"
2. 选择 `WW2_Commander` 文件夹
3. 点击项目打开

## 项目结构

```
WW2_Commander/
├── Assets/              # 资源文件夹
│   ├── Scenes/         # 场景文件
│   ├── Scripts/        # C# 脚本
│   ├── Art/            # 美术资源
│   ├── Audio/          # 音频资源
│   └── Prefabs/        # 预制体
├── ProjectSettings/    # 项目设置
├── Packages/           # Unity 包
└── README.md           # 项目说明
```

## 文件夹说明

| 文件夹 | 用途 |
|--------|------|
| `Scripts/` | 游戏逻辑代码 |
| `Art/` | 图片、精灵、动画 |
| `Audio/` | 音效、背景音乐 |
| `Scenes/` | 游戏场景 |
| `Prefabs/` | 可复用对象模板 |
| `Data/` | 配置数据、JSON |

## 核心脚本

- `GameManager.cs` - 游戏状态管理
- `UnitController.cs` - 单位行为控制

## 开发规范

### 命名约定
- 类：PascalCase (如 `UnitController`)
- 方法：PascalCase (如 `MoveTo`)
- 变量：camelCase (如 `moveSpeed`)
- 私有字段：_camelCase 或 [SerializeField] private

### 代码风格
- 使用 XML 文档注释
- 保持方法单一职责
- 使用 Unity 事件系统解耦

## Git 配置

项目已配置 Git 版本控制。首次克隆后：

```bash
cd WW2_Commander
git init
git add .
git commit -m "Initial commit: WW2_Commander project structure"
```

### .gitignore 建议
```
# Unity
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/

# OS
.DS_Store
Thumbs.db
```

## 下一步

1. [ ] 完善游戏核心机制
2. [ ] 添加单位类型 (步兵、坦克、飞机)
3. [ ] 实现资源系统
4. [ ] 设计 UI 界面
5. [ ] 添加音效和音乐

## 许可证

本项目仅供学习和个人使用。
