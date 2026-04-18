# SWO-138: 核心模块代码重构完成报告

## 概述
将传统 RTS 架构重构为指挥所模拟系统，实现 5 个核心模块的重写。

## 文件结构
```
Assets/Scripts/
├── Core/                          # 核心系统
│   ├── GameDirector.cs            # 游戏总控 - 战役时间线、阶段管理
│   └── GameEventBus.cs            # 全局事件总线 - 模块间松耦合通信
├── CommandPost/                   # 指挥所模块
│   ├── CameraController.cs        # 【模块1】第一人称固定座位摄像机
│   ├── InputSystem.cs             # 【模块2】物理交互输入系统
│   ├── InteractableObject.cs      # 可交互对象基类
│   └── ChessPiece.cs              # 沙盘兵棋标记
├── Command/                       # 指挥模块
│   └── CommandSystem.cs           # 【模块3】无线电指挥系统
├── Intelligence/                  # 情报模块
│   └── InformationSystem.cs       # 【模块4】信息系统（核心创新）
└── UI/                            # UI 模块
    └── UISystem.cs                # 【模块5】指挥所内景 UI 系统
```

## 5 个核心模块重构详情

### 1. CameraController（摄像机）— 替代旧 RTS 自由摄像机
**旧系统**: 自由平移、缩放、上帝视角
**新系统**: 固定座位第一人称视角
- 玩家只能转头（Yaw ±120°）和低头抬头（Pitch ±45°）
- 呼吸晃动增加沉浸感
- 注视点系统：可平滑聚焦沙盘/电台/地图/窗户
- 鼠标锁定/解锁切换

### 2. InputSystem（输入）— 替代旧鼠标框选/右键移动
**旧系统**: 左键选择单位，右键移动，框选多单位
**新系统**: 物理交互状态机
- Free模式：射线检测悬停物件，高亮显示
- Grabbing模式：抓取沙盘棋子，拖动放置，右键旋转
- RadioFocused模式：数字键切频率，鼠标点通话键
- DocumentReading模式：阅读情报文件
- 通过事件总线通知，不直接调用其他系统

### 3. CommandSystem（指挥）— 替代旧直接单位操控
**旧系统**: 选择单位 → 右键目标 = 即时执行
**新系统**: 无线电间接指令，异步生命周期
- 指令流程：Draft → Sending → InTransit → Delivered → Acknowledged → Executing → Completed
- 延迟模拟：简单指令30-60s，复杂指令60-180s
- 丢失概率：Easy 5% / Normal 15% / Hard 30%
- 误解系统：低士气导致指令被错误理解
- "收到" ≠ "理解" ≠ "执行"

### 4. InformationSystem（信息）— 替代旧完整战场可见
**旧系统**: 实时看到所有单位血量/位置
**新系统**: 不完整/延迟/错误/矛盾的情报
- 延迟：30s-300s，紧急汇报较短
- 准确度衰减：士气影响、交战压力、指挥官性格
- 信息遗漏：低准确度时不汇报兵力/位置/弹药
- 信息错误：位置偏移、兵力偏差、敌军误判
- 干扰：文本损坏、静电标记
- 新鲜度衰减：情报随时间变色（绿→黄→橙→灰）
- 矛盾检测：不同来源的情报互相矛盾

### 5. UISystem（UI）— 替代旧传统 HUD
**旧系统**: 屏幕叠加血条、小地图、选择面板
**新系统**: 指挥所内景 UI
- RadioTextPanel：打字机效果的无线电文本
- OrderCardPanel：指令卡物理交互
- StatusNotePanel：桌面状态便签
- SandTableOverlay：参谋沙盘标注（有延迟和误差）
- CommandStatusPanel：指令状态追踪

## 架构特点
1. **事件驱动**：所有模块通过 GameEventBus 通信，零直接引用
2. **状态机**：InputSystem 使用交互模式状态机
3. **异步设计**：指令和汇报都使用协程模拟真实延迟
4. **数据驱动**：难度参数、准确度参数均可在 Inspector 调整
5. **单一职责**：每个模块只负责自己的领域

## 已删除的旧代码
- `GameManager.cs` (WW2_Commander) → 替代为 GameDirector
- `UnitController.cs` (WW2_Commander) → 替代为 ChessPiece + 战场模拟
- `InteractionInput.cs` → 合并到 InputSystem
- `CommandPostUI.cs` → 替代为 UISystem
- `RadioCommandSystem.cs` → 合并到 CommandSystem

## 待开发模块
- 战场模拟引擎（SimulationManager）— 计算战斗/移动/士气
- 前线 AI（FrontlineAI / EnemyAI）— 部队自动行为
- 参谋 AI（StaffAI）— 自动更新沙盘
- 音频系统（FMOD）— 无线电干扰音效
- 关卡设计（Omaha Beach）— 地形/敌军部署

## 总代码量
- 9 个 C# 文件
- 约 3,922 行代码
- 4 个命名空间：SWO1.Core / SWO1.CommandPost / SWO1.Command / SWO1.Intelligence / SWO1.UI

---
**Issue**: SWO-138
**状态**: ✅ 核心模块重构完成
**下一步**: 战场模拟引擎 + 前线 AI 开发
