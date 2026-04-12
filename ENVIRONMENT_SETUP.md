# Unity 环境配置文档

## 已完成配置

### 1. Unity Hub
- **位置**: `/home/yanzhaoharsh/.openclaw/workspace/UnityHub.AppImage`
- **版本**: 3.6.1 (最新)
- **状态**: ✅ 已下载，需要手动运行安装

### 2. Unity 编辑器
- **推荐版本**: 2022.3.42f1 LTS
- **状态**: ⏳ 需通过 Unity Hub 安装

### 3. 项目结构
- **项目名称**: WW2_Commander
- **位置**: `/home/yanzhaoharsh/.openclaw/workspace/WW2_Commander`
- **模板**: 2D Core
- **状态**: ✅ 已创建

## 项目文件夹结构

```
WW2_Commander/
├── Assets/
│   ├── Scenes/           # 场景文件
│   │   └── MainScene.unity
│   ├── Scripts/          # C# 脚本
│   │   ├── GameManager.cs
│   │   └── UnitController.cs
│   ├── Art/              # 美术资源 (待添加)
│   ├── Audio/            # 音频资源 (待添加)
│   └── Prefabs/          # 预制体 (待添加)
├── ProjectSettings/      # 项目设置
│   ├── ProjectSettings.asset
│   ├── EditorVersion.txt
│   ├── InputManager.asset
│   ├── TagManager.asset
│   ├── TimeManager.asset
│   ├── Physics2DSettings.asset
│   ├── DynamicsManager.asset
│   ├── GraphicsSettings.asset
│   ├── QualitySettings.asset
│   ├── NavMeshAreas.asset
│   ├── PackageManagerSettings.asset
│   ├── VersionControlSettings.asset
│   ├── ClusterInputManager.asset
│   └── EditorBuildSettings.asset
├── .gitignore
├── README.md
└── ENVIRONMENT_SETUP.md
```

## 项目设置详情

### 显示设置
- **默认分辨率**: 1920x1080
- **窗口模式**: 窗口化
- **颜色空间**: Linear

### 输入系统
- **输入系统**: New Input System (已配置)
- **控制方案**: 
  - WASD / 方向键：移动
  - 鼠标左键：选择/攻击
  - 空格：特殊能力

### 脚本设置
- **脚本运行时**: .NET Standard 2.1
- **脚本后端**: IL2CPP
- **API 兼容性**: .NET Standard 2.1

### 物理设置
- **物理引擎**: 2D Physics
- **重力**: (0, -9.81)
- **碰撞层**: Units, Buildings, Terrain, Projectiles, Effects

## 下一步操作

### 1. 安装 Unity Hub (手动)
```bash
cd /home/yanzhaoharsh/.openclaw/workspace
chmod +x UnityHub.AppImage
./UnityHub.AppImage
```

### 2. 通过 Unity Hub 安装 Unity
1. 打开 Unity Hub
2. 点击 "Installs" → "Install Editor"
3. 选择 **2022.3.42f1 LTS**
4. 勾选模块:
   - Android Build Support
   - IL2CPP Build Support
   - .NET scripting backend
5. 等待安装完成

### 3. 打开项目
1. Unity Hub → "Projects" → "Add"
2. 选择 `WW2_Commander` 文件夹
3. 点击项目打开

### 4. 安装依赖包
打开项目后，通过 Package Manager 安装:
- ✅ Input System (已内置)
- [ ] 2D Animation
- [ ] 2D PSD Importer
- [ ] 2D SpriteShape
- [ ] 2D Tilemap Editor

## 环境检查清单

- [ ] Unity Hub 已安装并可运行
- [ ] Unity Editor 2022.3.x LTS 已安装
- [ ] 项目可在 Unity Hub 中看到
- [ ] 项目可正常打开无错误
- [ ] 场景可加载
- [ ] 脚本可编译无错误
- [ ] Git 仓库已初始化

## 故障排除

### Unity Hub 无法启动 (Linux)
```bash
# 检查依赖
sudo apt-get install libfuse2 libgtk-3-0 libgbm1 libasound2 libnss3 libxss1

# 使用 AppImage 提取模式
./UnityHub.AppImage --appimage-extract
./squashfs-root/AppRun --no-sandbox
```

### 项目打开缓慢
- 删除 `Library/` 文件夹后重新打开
- 检查磁盘空间

### 脚本编译错误
- 检查 .NET 版本设置
- 确保 Unity 版本匹配 (2022.3.x)

## 联系支持

如遇到问题，请参考:
- Unity 官方文档: https://docs.unity3d.com/
- Unity 论坛: https://forum.unity.com/
