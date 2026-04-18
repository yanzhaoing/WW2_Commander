# WW2 Commander - 全面开工任务清单

**目标：** 最短时间内让游戏可玩
**开始时间：** 2026-04-16 17:38

## 任务列表

### T1: 网格地图渲染 (高优先级)
- 修改 SetupScene2D，调用 MapGenerator 生成地图
- 用现有瓦片素材渲染 16×12 网格
- 显示坐标标注 (A1-P12)
- 输出: SetupScene2D.cs 修改

### T2: 按钮事件绑定 (高优先级)
- CommandMenu 按钮 → GameController2D.ExecuteCommand()
- 部队选择按钮 → 选中当前单位
- 频率按钮 → 切换频率
- 发送按钮 → 执行当前组合指令
- 输出: CommandMenu.cs 或新建 CommandMenuBinder.cs

### T3: GameController2D 集成 (高优先级)
- SetupScene2D 创建 GameController2D + 所有依赖系统
- 连接 MapGenerator, RadioSystem, EnemySpawner, SandTable2D
- 确保 Start() 后自动初始化
- 输出: SetupScene2D.cs 修改

### T4: 单位标记显示 (中优先级)
- 用现有单位图标在地图上显示我方/敌方单位
- 友军: blue marker, 敌军: red marker
- 输出: SandTable2D 或 GameController2D 修改

### T5: RadioSystem → RadioPanel 连接 (中优先级)
- RadioSystem 生成的汇报 → 显示在 RadioPanel UI
- 输出: 事件桥接代码

### T6: 全面审查 (最后)
- 编译检查
- 运行时检查
- 功能完整性检查
