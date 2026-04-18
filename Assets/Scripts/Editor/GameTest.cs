using UnityEngine;
using UnityEditor;
using SWO1.Core;
using SWO1.Simulation;
using SWO1.Command;

namespace SWO1.Editor
{
    public static class GameTest
    {
        [MenuItem("WW2 Commander/Run Tests")]
        public static void RunTests()
        {
            Debug.Log("=== WW2 Commander 测试开始 ===");
            
            // Test 1: BattleSimulator
            var sim = new GameObject("[BattleSimulator]").AddComponent<BattleSimulator>();
            Debug.Log($"✅ BattleSimulator 创建成功");
            Debug.Log($"   桥HP: {sim.BridgeHP}");
            
            // Test 2: GameDirector
            var director = new GameObject("[GameDirector]").AddComponent<GameDirector>();
            Debug.Log($"✅ GameDirector 创建成功");
            Debug.Log($"   当前阶段: {director.CurrentPhase}");
            
            // Test 3: EnemyWaveManager
            var waveMgr = new GameObject("[EnemyWaveManager]").AddComponent<EnemyWaveManager>();
            Debug.Log($"✅ EnemyWaveManager 创建成功");
            
            // Test 4: CommandSystem
            var cmdSys = new GameObject("[CommandSystem]").AddComponent<CommandSystem>();
            Debug.Log($"✅ CommandSystem 创建成功");
            
            // Test 5: GameEventBus
            var eventBus = new GameObject("[GameEventBus]").AddComponent<GameEventBus>();
            Debug.Log($"✅ GameEventBus 创建成功");
            
            Debug.Log("=== 所有核心模块加载成功 ===");
            Debug.Log("=== 测试通过！可以关闭此窗口，在 Unity Editor 中打开场景测试 ===");
        }
    }
}
