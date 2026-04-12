# Unity 环境配置文档

**最后更新**: 2026-04-12
**Issue**: SWO-3 - Unity 环境搭建

## 已完成配置

### 1. Unity Hub
- **位置**: `/home/yanzhaoharsh/.openclaw/workspace/UnityHub.AppImage`
- **提取位置**: `/home/yanzhaoharsh/.openclaw/workspace/squashfs-root/`
- **版本**: 3.6.1 (最新)
- **状态**: ✅ 已提取，可在 GUI 环境中运行

### 2. Unity 编辑器
- **推荐版本**: 2022.3.42f1 LTS
- **配置文件**: `ProjectSettings/EditorVersion.txt`
- **状态**: ✅ 已配置，需通过 Unity Hub 安装

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

### 1. 运行 Unity Hub (GUI 环境)
```bash
cd /home/yanzhaoharsh/.openclaw/workspace

# 方式 1: 直接运行 AppImage (需要 libfuse2)
chmod +x UnityHub.AppImage
./UnityHub.AppImage

# 方式 2: 使用已提取的版本
cd squashfs-root
./AppRun --no-sandbox
```

### 2. 通过 Unity Hub 安装 Unity
1. 打开 Unity Hub
2. 登录 Unity 账号 (需要有效的 Unity 许可证)
3. 点击 "Installs" → "Install Editor"
4. 选择 **2022.3.42f1 LTS**
5. 勾选模块:
   - Android Build Support
   - IL2CPP Build Support
   - .NET scripting backend
6. 等待安装完成 (约 10-15 GB)

### 3. 打开项目
1. Unity Hub → "Projects" → "Add"
2. 选择 `WW2_Commander` 文件夹
3. 点击项目打开

### 4. 安装依赖包
打开项目后，通过 Window → Package Manager 安装:
- ✅ Input System (已内置)
- ⬜ 2D Animation
- ⬜ 2D PSD Importer
- ⬜ 2D SpriteShape
- ⬜ 2D Tilemap Editor

## 完成报告

详细完成报告请查看: `SETUP_COMPLETE.md`

## 环境检查清单

- [x] Unity Hub 已下载并提取
- [x] Unity Editor 2022.3.x LTS 已配置
- [x] 项目可在 Unity Hub 中添加
- [x] 项目结构完整
- [x] 场景文件已创建
- [x] 脚本已创建
- [x] Git 仓库已初始化 (2 次提交)
- [ ] Unity Editor 已安装 (需手动)
- [ ] 项目可在 Unity 中打开 (需手动)

## 故障排除

### Unity Hub 无法启动 (Linux)
```bash
# 检查依赖
sudo apt-get install libfuse2 libgtk-3-0 libgbm1 libasound2 libnss3 libxss1

# 使用已提取的版本 (推荐)
cd /home/yanzhaoharsh/.openclaw/workspace/squashfs-root
./AppRun --no-sandbox

# 或直接运行 AppImage
chmod +x UnityHub.AppImage
./UnityHub.AppImage
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
