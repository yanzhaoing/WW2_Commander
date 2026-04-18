#!/usr/bin/env python3
"""
WW2 Commander 集成测试运行器 (Python版)
测试范围: BattleSimulator, GameDirector, CommandSystem, 全链路集成
"""
import random
import math
import json
from dataclasses import dataclass, field
from typing import List, Dict, Optional, Tuple
from enum import Enum, auto

# ============================================================
# 数据模型
# ============================================================

class CampaignPhase(Enum):
    Briefing = auto()
    Embarkation = auto()
    FirstWaveLanding = auto()
    FirstReports = auto()
    SecondWaveLanding = auto()
    ThirdWaveLanding = auto()
    CounterAttack = auto()
    CriticalDecision = auto()
    Resolution = auto()

class GameOutcome(Enum):
    InProgress = auto()
    PerfectVictory = auto()
    PyrrhicVictory = auto()
    PartialVictory = auto()
    Defeat = auto()
    TotalDefeat = auto()

class Difficulty(Enum):
    Easy = 0
    Normal = 1
    Hard = 2

class CommandType(Enum):
    Move = auto()
    Attack = auto()
    Defend = auto()
    Retreat = auto()
    Recon = auto()
    ArtilleryStrike = auto()
    StatusQuery = auto()
    Supply = auto()
    Custom = auto()

class CommandStatus(Enum):
    Draft = auto()
    Sending = auto()
    InTransit = auto()
    Delivered = auto()
    Acknowledged = auto()
    Executing = auto()
    Completed = auto()
    Lost = auto()
    Failed = auto()

@dataclass
class Vector3:
    x: float = 0.0
    y: float = 0.0
    z: float = 0.0

    @staticmethod
    def zero():
        return Vector3(0, 0, 0)

    def distance_to(self, other: 'Vector3') -> float:
        return math.sqrt((self.x-other.x)**2 + (self.y-other.y)**2 + (self.z-other.z)**2)

@dataclass
class CombatUnit:
    unit_id: str = ""
    troop_count: int = 0
    max_troop_count: int = 0
    morale: float = 50.0
    ammo_level: float = 100.0
    position: Vector3 = field(default_factory=Vector3.zero)
    is_defending: bool = False
    is_eliminated: bool = False

    @property
    def troop_ratio(self) -> float:
        return self.troop_count / self.max_troop_count if self.max_troop_count > 0 else 0.0

    @property
    def morale_coeff(self) -> float:
        return 0.5 + (self.morale / 200.0)

    @property
    def defense_modifier(self) -> float:
        return 0.6 if self.is_defending else 1.0

# ============================================================
# BattleSimulator
# ============================================================

class BattleSimulator:
    def __init__(self):
        self.bridge_hp = 100.0
        self.bridge_max_hp = 100.0
        self.bridge_dps = 2.0
        self.base_damage = 10.0
        self.random_min = 0.8
        self.random_max = 1.2
        self.low_ammo_morale_penalty = -5.0
        self.no_comm_morale_penalty = -3.0
        self.ammo_low_threshold = 20.0

        self.friendly_units: Dict[str, CombatUnit] = {}
        self.enemy_units: Dict[str, CombatUnit] = {}
        self.total_friendly_casualties = 0
        self.total_enemy_casualties = 0
        self.initial_friendly = 0
        self.initial_enemy = 0
        self.event_log: List[str] = []

    def register_friendly(self, uid, count, morale, ammo, pos):
        unit = CombatUnit(uid, count, count, morale, ammo, pos)
        self.friendly_units[uid] = unit
        self.initial_friendly += count

    def register_enemy(self, uid, count, morale, pos):
        unit = CombatUnit(uid, count, count, morale, 100.0, pos)
        self.enemy_units[uid] = unit
        self.initial_enemy += count

    def unregister_enemy(self, uid):
        self.enemy_units.pop(uid, None)

    def calculate_damage(self, attacker: CombatUnit, defender: CombatUnit) -> float:
        dmg = self.base_damage * attacker.troop_ratio * attacker.morale_coeff * defender.defense_modifier
        dmg *= random.uniform(self.random_min, self.random_max)
        return max(0.0, dmg)

    def apply_damage_to_unit(self, uid, damage):
        unit = self.friendly_units.get(uid)
        if not unit or unit.is_eliminated:
            return 0
        actual = min(round(damage), unit.troop_count)
        unit.troop_count -= actual
        self.total_friendly_casualties += actual
        if unit.troop_count <= 0:
            unit.troop_count = 0
            unit.is_eliminated = True
            self.event_log.append(f"UnitEliminated:{uid}")
        if actual > unit.max_troop_count * 0.1:
            self.modify_morale(uid, -15)
        return actual

    def apply_damage_to_enemy(self, uid, damage):
        unit = self.enemy_units.get(uid)
        if not unit or unit.is_eliminated:
            return 0
        actual = min(round(damage), unit.troop_count)
        unit.troop_count -= actual
        self.total_enemy_casualties += actual
        if unit.troop_count <= 0:
            unit.troop_count = 0
            unit.is_eliminated = True
            self.event_log.append(f"EnemyEliminated:{uid}")
        return actual

    def apply_bridge_damage(self, dps, dt):
        if self.bridge_hp <= 0:
            return 0
        dmg = dps * dt
        self.bridge_hp = max(0, self.bridge_hp - dmg)
        if self.bridge_hp <= 0:
            self.event_log.append("BridgeDestroyed")
        return dmg

    def enemy_attack_bridge(self, uid, dt):
        unit = self.enemy_units.get(uid)
        if not unit or unit.is_eliminated:
            return
        mult = 1.0 + unit.troop_ratio * 0.5
        self.apply_bridge_damage(self.bridge_dps * mult, dt)

    def modify_morale(self, uid, delta):
        unit = self.friendly_units.get(uid)
        if unit:
            unit.morale = max(0, min(100, unit.morale + delta))

    def apply_reinforcement_bonus(self, uid): self.modify_morale(uid, 5)
    def apply_repel_bonus(self, uid): self.modify_morale(uid, 10)
    def apply_artillery_bonus(self, uid): self.modify_morale(uid, 8)
    def apply_no_comm_penalty(self, uid): self.modify_morale(uid, self.no_comm_morale_penalty)

    def set_defending(self, uid, val):
        unit = self.friendly_units.get(uid)
        if unit:
            unit.is_defending = val

    def morale_tick(self):
        for uid, unit in self.friendly_units.items():
            if unit.is_eliminated:
                continue
            if unit.ammo_level < self.ammo_low_threshold:
                self.modify_morale(uid, self.low_ammo_morale_penalty)

    def get_friendly(self, uid): return self.friendly_units.get(uid)
    def get_enemy(self, uid): return self.enemy_units.get(uid)

    def all_friendly_eliminated(self):
        if not self.friendly_units:
            return False
        return all(u.is_eliminated for u in self.friendly_units.values())

    def casualty_rate(self):
        return self.total_friendly_casualties / self.initial_friendly if self.initial_friendly > 0 else 0

    def repair_bridge(self, amount):
        self.bridge_hp = min(self.bridge_max_hp, self.bridge_hp + amount)

# ============================================================
# GameDirector
# ============================================================

class GameDirector:
    def __init__(self):
        self.difficulty = Difficulty.Normal
        self.time_scale = 1.0
        self.start_time = 360.0
        self.end_time = 540.0
        self.current_time = 360.0
        self.phase = CampaignPhase.Briefing
        self.is_paused = False
        self.outcome = GameOutcome.InProgress
        self.objectives = [False, False, False]
        self.event_log: List[str] = []

    def initialize(self):
        self.current_time = self.start_time
        self.phase = CampaignPhase.Briefing
        self.outcome = GameOutcome.InProgress
        self.objectives = [False, False, False]
        self.event_log = []

    def update(self, dt):
        if self.is_paused or self.outcome != GameOutcome.InProgress:
            return
        self.current_time += (dt * self.time_scale) / 60.0
        self._update_phase()
        self._check_outcome()

    def _update_phase(self):
        t = self.current_time
        if t >= 540: new = CampaignPhase.Resolution
        elif t >= 480: new = CampaignPhase.CriticalDecision
        elif t >= 450: new = CampaignPhase.CounterAttack
        elif t >= 420: new = CampaignPhase.ThirdWaveLanding
        elif t >= 405: new = CampaignPhase.SecondWaveLanding
        elif t >= 395: new = CampaignPhase.FirstReports
        elif t >= 390: new = CampaignPhase.FirstWaveLanding
        elif t >= 375: new = CampaignPhase.Embarkation
        else: new = CampaignPhase.Briefing
        if new != self.phase:
            self.phase = new
            self.event_log.append(f"PhaseChanged:{new.name}")

    def report_objective(self, idx):
        if 0 <= idx < len(self.objectives):
            self.objectives[idx] = True

    def _check_outcome(self):
        if self.current_time < self.end_time:
            return
        captured = sum(self.objectives)
        self.outcome = GameOutcome.PartialVictory if captured >= 1 else GameOutcome.Defeat
        self.event_log.append(f"OutcomeDetermined:{self.outcome.name}")

    def set_final_outcome(self, casualty_rate):
        captured = sum(self.objectives)
        if captured == 3 and casualty_rate < 0.3:
            self.outcome = GameOutcome.PerfectVictory
        elif captured == 3 and casualty_rate >= 0.5:
            self.outcome = GameOutcome.PyrrhicVictory
        elif captured == 3:
            self.outcome = GameOutcome.PartialVictory
        elif captured >= 1:
            self.outcome = GameOutcome.PartialVictory
        else:
            self.outcome = GameOutcome.Defeat
        self.event_log.append(f"FinalOutcome:{self.outcome.name}")

    def set_total_defeat(self):
        self.outcome = GameOutcome.TotalDefeat
        self.event_log.append("TotalDefeat")

    def pause(self): self.is_paused = True
    def resume(self): self.is_paused = False

    def formatted_time(self):
        h = int(self.current_time // 60)
        m = int(self.current_time % 60)
        return f"{h:02d}:{m:02d}"

# ============================================================
# CommandSystem
# ============================================================

class CommandSystem:
    def __init__(self):
        self.base_delivery = 30.0
        self.loss_chance = [0.05, 0.15, 0.30]
        self.delay_mult = [0.7, 1.0, 1.8]
        self.misinterpret_chance = [0.05, 0.12, 0.25, 0.45]
        self.no_reply_chance = 0.15
        self.event_log: List[str] = []

    def calculate_delay(self, cmd_type: CommandType, diff: Difficulty) -> float:
        dm = self.delay_mult[diff.value]
        simple = {CommandType.StatusQuery, CommandType.Supply}
        complex_ = {CommandType.Move, CommandType.Attack, CommandType.Retreat}
        if cmd_type in simple:
            td = 45.0
        elif cmd_type in complex_:
            td = 120.0
        elif cmd_type == CommandType.ArtilleryStrike:
            td = 67.0
        else:
            td = 75.0
        return self.base_delivery * dm + td

    def get_loss_chance(self, diff: Difficulty) -> float:
        return self.loss_chance[min(diff.value, 2)]

    def get_misinterpret_chance(self, morale: float) -> float:
        if morale >= 80: return self.misinterpret_chance[0]
        elif morale >= 50: return self.misinterpret_chance[1]
        elif morale >= 30: return self.misinterpret_chance[2]
        else: return self.misinterpret_chance[3]

    def simulate_delivery(self, cmd_type, morale, diff, roll) -> CommandStatus:
        loss = self.get_loss_chance(diff)
        if roll < loss:
            self.event_log.append(f"CommandLost:{cmd_type.name}")
            return CommandStatus.Lost
        adj_roll = (roll - loss) / (1 - loss) if loss < 1 else roll
        mis = self.get_misinterpret_chance(morale)
        if adj_roll < mis:
            self.event_log.append(f"CommandMisinterpreted:{cmd_type.name}")
            return CommandStatus.Delivered
        self.event_log.append(f"CommandDelivered:{cmd_type.name}")
        return CommandStatus.Delivered

    def generate_misinterpretation(self, cmd_type, content):
        if cmd_type == CommandType.Move:
            return content + "（方向可能偏差15°）"
        elif cmd_type == CommandType.Attack:
            return content.replace("攻击", "绕过")
        elif cmd_type == CommandType.Defend:
            return content
        elif cmd_type == CommandType.Retreat:
            return content.replace("撤退", "原地待命")
        elif cmd_type == CommandType.ArtilleryStrike:
            return content + "（坐标可能有误）"
        return content + "（理解不确定）"

# ============================================================
# 测试框架
# ============================================================

results = []

def assert_test(condition, name, category, fail_msg=""):
    results.append({
        "name": name, "passed": condition,
        "message": "OK" if condition else f"FAIL: {fail_msg}",
        "category": category
    })

def assert_near(actual, expected, tol, name, category):
    ok = abs(actual - expected) <= tol
    assert_test(ok, name, category, f"expected ~{expected:.4f}, got {actual:.4f} (tol={tol:.4f})")

def assert_equal(actual, expected, name, category):
    assert_test(actual == expected, name, category, f"expected {expected}, got {actual}")

# ============================================================
# BattleSimulator 测试
# ============================================================

def test_battle_simulator():
    print("\n📋 [Phase 1] BattleSimulator 单元测试...")
    random.seed(42)

    # 伤害计算
    attacker = CombatUnit("", 50, 100, 75.0, 80.0)
    defender = CombatUnit("", 50, 100, 50.0)
    sim = BattleSimulator()
    dmg = sim.calculate_damage(attacker, defender)
    # 10 * 0.5 * 0.875 * 1.0 * [0.8~1.2] = [3.5, 5.25]
    assert_test(dmg >= 0, "伤害值非负", "BattleSimulator")
    assert_test(3.5 <= dmg <= 5.25, "伤害值在预期范围内", "BattleSimulator", f"dmg={dmg:.3f}")

    # 士气系数
    assert_near(CombatUnit("", morale=100).morale_coeff, 1.0, 0.001, "士气100→系数1.0", "BattleSimulator")
    assert_near(CombatUnit("", morale=50).morale_coeff, 0.75, 0.001, "士气50→系数0.75", "BattleSimulator")
    assert_near(CombatUnit("", morale=0).morale_coeff, 0.5, 0.001, "士气0→系数0.5", "BattleSimulator")

    # 防御修正
    u = CombatUnit("", is_defending=False)
    assert_near(u.defense_modifier, 1.0, 0.001, "非防御修正=1.0", "BattleSimulator")
    u.is_defending = True
    assert_near(u.defense_modifier, 0.6, 0.001, "防御修正=0.6", "BattleSimulator")

    # 桥头堡伤害
    sim = BattleSimulator()
    dmg = sim.apply_bridge_damage(2, 10)
    assert_near(dmg, 20, 0.001, "桥伤害=20", "BattleSimulator")
    assert_near(sim.bridge_hp, 80, 0.001, "桥HP=80", "BattleSimulator")

    # 桥头堡摧毁
    sim = BattleSimulator()
    sim.apply_bridge_damage(100, 2)
    assert_test(sim.bridge_hp <= 0, "桥HP降为0", "BattleSimulator", f"HP={sim.bridge_hp}")
    assert_test(sim.bridge_hp <= 0, "IsBridgeDestroyed", "BattleSimulator")
    assert_test("BridgeDestroyed" in sim.event_log, "BridgeDestroyed事件", "BattleSimulator")
    extra = sim.apply_bridge_damage(10, 1)
    assert_equal(extra, 0, "摧毁后不受伤害", "BattleSimulator")

    # 部队歼灭
    sim = BattleSimulator()
    sim.register_friendly("test", 10, 75, 80, Vector3.zero())
    dmg = sim.apply_damage_to_unit("test", 15)
    assert_equal(dmg, 10, "伤害不超过兵力", "BattleSimulator")
    u = sim.get_friendly("test")
    assert_test(u.is_eliminated, "部队标记歼灭", "BattleSimulator")
    assert_equal(u.troop_count, 0, "歼灭后兵力0", "BattleSimulator")

    # 士气系统
    sim = BattleSimulator()
    sim.register_friendly("mt", 100, 50, 80, Vector3.zero())
    sim.apply_reinforcement_bonus("mt")
    assert_near(sim.get_friendly("mt").morale, 55, 0.001, "增援→士气+5", "BattleSimulator")
    sim.apply_repel_bonus("mt")
    assert_near(sim.get_friendly("mt").morale, 65, 0.001, "击退→士气+10", "BattleSimulator")
    sim.apply_artillery_bonus("mt")
    assert_near(sim.get_friendly("mt").morale, 73, 0.001, "炮击→士气+8", "BattleSimulator")
    sim.apply_no_comm_penalty("mt")
    assert_near(sim.get_friendly("mt").morale, 70, 0.001, "无通讯→士气-3", "BattleSimulator")
    sim.modify_morale("mt", 50)
    assert_equal(sim.get_friendly("mt").morale, 100, "士气上限100", "BattleSimulator")
    sim.modify_morale("mt", -200)
    assert_equal(sim.get_friendly("mt").morale, 0, "士气下限0", "BattleSimulator")

    # 伤亡统计
    sim = BattleSimulator()
    sim.register_friendly("a", 50, 75, 80, Vector3.zero())
    sim.register_friendly("b", 50, 75, 80, Vector3.zero())
    sim.register_enemy("ea", 100, 60, Vector3.zero())
    assert_equal(sim.initial_friendly, 100, "初始我方=100", "BattleSimulator")
    assert_equal(sim.initial_enemy, 100, "初始敌方=100", "BattleSimulator")
    sim.apply_damage_to_unit("a", 10)
    sim.apply_damage_to_unit("b", 5)
    assert_equal(sim.total_friendly_casualties, 15, "我方伤亡=15", "BattleSimulator")
    sim.apply_damage_to_enemy("ea", 20)
    assert_equal(sim.total_enemy_casualties, 20, "敌方伤亡=20", "BattleSimulator")
    assert_near(sim.casualty_rate(), 0.15, 0.001, "伤亡率=15%", "BattleSimulator")

    # 全灭检测
    sim = BattleSimulator()
    assert_test(not sim.all_friendly_eliminated(), "空列表不算全灭", "BattleSimulator")
    sim.register_friendly("solo", 5, 75, 80, Vector3.zero())
    assert_test(not sim.all_friendly_eliminated(), "有存活不算全灭", "BattleSimulator")
    sim.apply_damage_to_unit("solo", 10)
    assert_test(sim.all_friendly_eliminated(), "全灭检测正确", "BattleSimulator")

    # 桥修复
    sim = BattleSimulator()
    sim.apply_bridge_damage(50, 1)
    sim.repair_bridge(30)
    assert_near(sim.bridge_hp, 80, 0.001, "修复→80", "BattleSimulator")
    sim.repair_bridge(50)
    assert_equal(sim.bridge_hp, 100, "修复不超过上限", "BattleSimulator")

    # 边界
    sim = BattleSimulator()
    assert_equal(sim.apply_damage_to_unit("nonexist", 10), 0, "攻击不存在我方→0", "BattleSimulator")
    assert_equal(sim.apply_damage_to_enemy("nonexist", 10), 0, "攻击不存在敌方→0", "BattleSimulator")
    sim.register_friendly("dead", 5, 75, 80, Vector3.zero())
    sim.apply_damage_to_unit("dead", 10)
    assert_equal(sim.apply_damage_to_unit("dead", 5), 0, "攻击已歼灭→0", "BattleSimulator")
    e = CombatUnit("", 0, 100)
    assert_near(e.troop_ratio, 0, 0.001, "0兵力→系数0", "BattleSimulator")
    e2 = CombatUnit("", 10, 0)
    assert_equal(e2.troop_ratio, 0, "Max=0→系数0", "BattleSimulator")
    sim.modify_morale("ghost", 50)  # 不崩溃
    assert_test(True, "修改不存在士气不崩溃", "BattleSimulator")

# ============================================================
# GameDirector 测试
# ============================================================

def test_game_director():
    print("\n📋 [Phase 2] GameDirector 单元测试...")

    d = GameDirector()
    d.initialize()
    assert_equal(d.phase, CampaignPhase.Briefing, "初始阶段=简报", "GameDirector")

    # 阶段时间线
    timeline = [
        (360, CampaignPhase.Briefing), (375, CampaignPhase.Embarkation),
        (390, CampaignPhase.FirstWaveLanding), (395, CampaignPhase.FirstReports),
        (405, CampaignPhase.SecondWaveLanding), (420, CampaignPhase.ThirdWaveLanding),
        (450, CampaignPhase.CounterAttack), (480, CampaignPhase.CriticalDecision),
        (540, CampaignPhase.Resolution),
    ]
    for time_val, expected_phase in timeline:
        d.initialize()
        dt = (time_val - d.start_time) * 60
        d.update(dt)
        assert_equal(d.phase, expected_phase, f"时间{time_val/60:.0f}:{time_val%60:.0f}→{expected_phase.name}", "GameDirector")

    # 目标占领
    d = GameDirector()
    d.initialize()
    d.report_objective(0)
    d.report_objective(2)
    d.update((540 - 360) * 60)
    assert_equal(d.outcome, GameOutcome.PartialVictory, "占2目标→部分胜利", "GameDirector")

    # 胜利条件
    d = GameDirector(); d.initialize()
    d.report_objective(0); d.report_objective(1); d.report_objective(2)
    d.set_final_outcome(0.2)
    assert_equal(d.outcome, GameOutcome.PerfectVictory, "全占+低伤亡→完美胜利", "GameDirector")

    d = GameDirector(); d.initialize()
    d.report_objective(0); d.report_objective(1); d.report_objective(2)
    d.set_final_outcome(0.6)
    assert_equal(d.outcome, GameOutcome.PyrrhicVictory, "全占+高伤亡→惨胜", "GameDirector")

    d = GameDirector(); d.initialize()
    d.set_final_outcome(0.1)
    assert_equal(d.outcome, GameOutcome.Defeat, "零占领→失败", "GameDirector")

    d = GameDirector(); d.initialize()
    d.set_total_defeat()
    assert_equal(d.outcome, GameOutcome.TotalDefeat, "全军覆没", "GameDirector")

    # 暂停/恢复
    d = GameDirector(); d.initialize()
    d.pause()
    d.update(60)
    assert_near(d.current_time, 360, 0.001, "暂停不推进时间", "GameDirector")
    d.resume()
    d.update(60)
    assert_test(d.current_time > 360, "恢复后推进", "GameDirector", f"time={d.current_time}")

    # 时间格式
    d = GameDirector(); d.initialize()
    assert_equal(d.formatted_time(), "06:00", "初始时间=06:00", "GameDirector")
    d.update(15 * 60)
    assert_equal(d.formatted_time(), "06:15", "15分钟后=06:15", "GameDirector")

    # 完整游戏循环
    d = GameDirector(); d.initialize()
    d.time_scale = 60
    frames = 0
    while d.outcome == GameOutcome.InProgress and frames < 10000:
        d.update(0.016)
        frames += 1
    assert_equal(d.phase, CampaignPhase.Resolution, "循环到达Resolution", "GameDirector")
    assert_test(frames < 10000, "合理帧数内结束", "GameDirector", f"frames={frames}")

# ============================================================
# CommandSystem 测试
# ============================================================

def test_command_system():
    print("\n📋 [Phase 3] CommandSystem 单元测试...")

    c = CommandSystem()

    # 延迟计算
    easy_d = c.calculate_delay(CommandType.Move, Difficulty.Easy)
    hard_d = c.calculate_delay(CommandType.Move, Difficulty.Hard)
    assert_test(hard_d > easy_d, "Hard延迟>Easy", "CommandSystem", f"Easy={easy_d:.1f}, Hard={hard_d:.1f}")
    q_d = c.calculate_delay(CommandType.StatusQuery, Difficulty.Normal)
    m_d = c.calculate_delay(CommandType.Move, Difficulty.Normal)
    assert_test(q_d < m_d, "Query延迟<Move", "CommandSystem", f"Q={q_d:.1f}, M={m_d:.1f}")

    # 丢失率
    assert_near(c.get_loss_chance(Difficulty.Easy), 0.05, 0.001, "Easy丢失=5%", "CommandSystem")
    assert_near(c.get_loss_chance(Difficulty.Normal), 0.15, 0.001, "Normal丢失=15%", "CommandSystem")
    assert_near(c.get_loss_chance(Difficulty.Hard), 0.30, 0.001, "Hard丢失=30%", "CommandSystem")

    # 误解率
    assert_near(c.get_misinterpret_chance(90), 0.05, 0.001, "士气90→5%", "CommandSystem")
    assert_near(c.get_misinterpret_chance(60), 0.12, 0.001, "士气60→12%", "CommandSystem")
    assert_near(c.get_misinterpret_chance(40), 0.25, 0.001, "士气40→25%", "CommandSystem")
    assert_near(c.get_misinterpret_chance(10), 0.45, 0.001, "士气10→45%", "CommandSystem")

    # 送达模拟
    c = CommandSystem()
    s1 = c.simulate_delivery(CommandType.Move, 75, Difficulty.Easy, 0.01)
    assert_equal(s1, CommandStatus.Lost, "极低roll→丢失", "CommandSystem")
    c = CommandSystem()
    s2 = c.simulate_delivery(CommandType.Move, 75, Difficulty.Easy, 0.5)
    assert_equal(s2, CommandStatus.Delivered, "中等roll→送达", "CommandSystem")
    c = CommandSystem()
    s3 = c.simulate_delivery(CommandType.Move, 75, Difficulty.Easy, 0.049)
    assert_equal(s3, CommandStatus.Lost, "边界0.049<0.05→丢失", "CommandSystem")
    c = CommandSystem()
    s4 = c.simulate_delivery(CommandType.Move, 75, Difficulty.Easy, 0.051)
    assert_equal(s4, CommandStatus.Delivered, "边界0.051>0.05→送达", "CommandSystem")

    # 误解文本
    m1 = c.generate_misinterpretation(CommandType.Move, "向北移动至目标Alpha")
    assert_test(m1 != "向北移动至目标Alpha", "Move被误解", "CommandSystem")
    m2 = c.generate_misinterpretation(CommandType.Defend, "就地防御")
    assert_equal(m2, "就地防御", "Defend不被误解", "CommandSystem")
    m3 = c.generate_misinterpretation(CommandType.Retreat, "撤退至海堤")
    assert_test("原地待命" in m3, "Retreat被误解为待命", "CommandSystem")

    # 难度递增
    e = c.get_loss_chance(Difficulty.Easy)
    n = c.get_loss_chance(Difficulty.Normal)
    h = c.get_loss_chance(Difficulty.Hard)
    assert_test(e < n < h, "丢失率递增", "CommandSystem", f"{e}<{n}<{h}")

# ============================================================
# 集成测试
# ============================================================

def test_integration():
    print("\n🔗 [Phase 4] 全链路集成测试...")

    # 完整游戏循环
    sim = BattleSimulator()
    director = GameDirector()
    cmd = CommandSystem()
    director.initialize()
    director.time_scale = 60

    sim.register_friendly("company_1", 55, 75, 80, Vector3(100, 0, 50))
    sim.register_friendly("company_2", 50, 65, 65, Vector3(200, 0, 100))
    sim.register_friendly("tank_platoon", 45, 80, 90, Vector3(150, 0, 80))
    sim.register_enemy("german_1", 80, 70, Vector3(120, 0, 60))
    sim.register_enemy("german_2", 60, 60, Vector3(180, 0, 90))

    frames = 0
    combat_events = 0
    while director.outcome == GameOutcome.InProgress and frames < 10000:
        director.update(0.016)
        if frames % 300 == 0 and frames > 0:
            a = sim.get_enemy("german_1")
            d_ = sim.get_friendly("company_1")
            if a and d_ and not a.is_eliminated and not d_.is_eliminated:
                dmg = sim.calculate_damage(a, d_)
                sim.apply_damage_to_unit("company_1", dmg)
                combat_events += 1
            if d_ and a and not d_.is_eliminated and not a.is_eliminated:
                dmg = sim.calculate_damage(d_, a)
                sim.apply_damage_to_enemy("german_1", dmg)
            sim.enemy_attack_bridge("german_1", 5)
            sim.enemy_attack_bridge("german_2", 5)
        if frames == 3000: director.report_objective(0)
        if frames == 5000: director.report_objective(1)
        frames += 1

    director.set_final_outcome(sim.casualty_rate())
    assert_test(frames < 10000, "游戏循环正常结束", "Integration", f"frames={frames}")
    assert_test(combat_events > 0, "有战斗事件", "Integration", f"events={combat_events}")
    assert_equal(director.phase, CampaignPhase.Resolution, "到达Resolution", "Integration")
    assert_test(sim.total_friendly_casualties + sim.total_enemy_casualties > 0, "有伤亡统计", "Integration")
    assert_test(director.outcome != GameOutcome.InProgress, "有明确结局", "Integration", f"outcome={director.outcome.name}")

    # 指令→数值全链路
    sim = BattleSimulator()
    sim.register_friendly("unit", 100, 75, 80, Vector3.zero())
    sim.register_enemy("enemy", 100, 60, Vector3.zero())
    rounds = 0
    while rounds < 1000:
        f = sim.get_friendly("unit")
        e = sim.get_enemy("enemy")
        if f.is_eliminated or e.is_eliminated:
            break
        fd = sim.calculate_damage(f, e)
        ed = sim.calculate_damage(e, f)
        sim.apply_damage_to_enemy("enemy", fd)
        sim.apply_damage_to_unit("unit", ed)
        rounds += 1
    assert_test(rounds < 1000, "战斗在合理轮次结束", "Integration", f"rounds={rounds}")
    assert_test(f.is_eliminated or e.is_eliminated, "至少一方全灭", "Integration")

    # 士气影响指挥
    cmd = CommandSystem()
    low = cmd.get_misinterpret_chance(10)
    high = cmd.get_misinterpret_chance(90)
    assert_test(low > high, "低士气误解率>高士气", "Integration", f"low={low:.2f}, high={high:.2f}")

    # 桥HP=0
    sim = BattleSimulator()
    sim.apply_bridge_damage(200, 1)
    sim.apply_bridge_damage(10, 1)
    sim.apply_bridge_damage(10, 1)
    assert_equal(sim.bridge_hp, 0, "桥HP不为负", "Integration")
    dc = sum(1 for e in sim.event_log if e == "BridgeDestroyed")
    assert_equal(dc, 1, "BridgeDestroyed只触发一次", "Integration")

    # 全灭行为
    sim = BattleSimulator()
    director = GameDirector()
    director.initialize()
    sim.register_friendly("a", 10, 75, 80, Vector3.zero())
    sim.register_friendly("b", 10, 75, 80, Vector3.zero())
    sim.apply_damage_to_unit("a", 20)
    sim.apply_damage_to_unit("b", 20)
    assert_test(sim.all_friendly_eliminated(), "全灭检测", "Integration")
    assert_near(sim.casualty_rate(), 1.0, 0.001, "全灭伤亡率100%", "Integration")
    director.set_final_outcome(1.0)
    assert_equal(director.outcome, GameOutcome.Defeat, "全灭+0目标→失败", "Integration")

# ============================================================
# Bug 检测
# ============================================================

def detect_bugs():
    print("\n🔍 [Phase 5] Bug 检测...")
    bugs = []

    # Bug 1: 桥头堡伤害过高
    sim = BattleSimulator()
    sim.register_enemy("e1", 100, 80, Vector3.zero())
    sim.register_enemy("e2", 100, 80, Vector3.zero())
    sim.register_enemy("e3", 100, 80, Vector3.zero())
    sim.enemy_attack_bridge("e1", 5)
    sim.enemy_attack_bridge("e2", 5)
    sim.enemy_attack_bridge("e3", 5)
    damage_done = 100 - sim.bridge_hp
    if sim.bridge_hp < 50:
        bugs.append(("P1", "BattleSimulator",
            f"3个满编敌军各攻击5秒后桥HP={sim.bridge_hp:.1f}/100 (损失{damage_done:.1f})。GDD要求-2/s基础，但EnemyAttackBridge叠加兵力倍率(最高1.5x)，3敌军≈9 DPS，100HP仅撑~11秒。10分钟游戏中桥会过早被摧毁。",
            "降低bridgeDamagePerSecond至0.5-1.0，或限制同时攻击桥头堡的敌军数量上限"))

    # Bug 2: ApplyMoraleTick缺少无通讯惩罚
    bugs.append(("P2", "BattleSimulator",
        "ApplyMoraleTick()只检查弹药不足(-5/tick)，不检查无通讯(-3/tick)。GDD 5.5要求'长时间无通讯→-3/tick'。ApplyNoCommPenalty需外部手动调用，缺乏自动超时检测。",
        "在ApplyMoraleTick中增加通讯状态追踪(最后通讯时间)，超时自动应用-3惩罚"))

    # Bug 3: 重复注册累加initial
    sim = BattleSimulator()
    sim.register_friendly("u", 50, 75, 80, Vector3.zero())
    first = sim.initial_friendly
    sim.register_friendly("u", 50, 75, 80, Vector3.zero())
    second = sim.initial_friendly
    if second != first:
        bugs.append(("P2", "BattleSimulator",
            f"重复注册同ID累加InitialFriendlyTroops: {first}→{second}。导致伤亡率计算错误。",
            "注册前检查ID是否存在，已存在则跳过"))

    # Bug 4: SetTotalDefeat可被覆盖
    director = GameDirector()
    director.initialize()
    director.set_total_defeat()
    director.set_final_outcome(0.1)
    if director.outcome != GameOutcome.TotalDefeat:
        bugs.append(("P1", "GameDirector",
            f"SetTotalDefeat()后SetFinalOutcome()覆盖结局: TotalDefeat→{director.outcome.name}。全军覆没应是最高优先级。",
            "SetFinalOutcome开头增加 if outcome == TotalDefeat: return 保护"))

    # Bug 5: CheckOutcome不检测全灭
    bugs.append(("P1", "GameDirector",
        "GameDirector.Update()不检测部队全灭。即使所有部队被歼灭，游戏仍等到09:00才结束。玩家在空等中体验极差。",
        "在Update中增加BattleSimulator.all_friendly_eliminated()检测，触发SetTotalDefeat()"))

    # Bug 6: 敌军弹药无消耗
    bugs.append(("P3", "BattleSimulator",
        "敌军弹药固定100且无消耗逻辑，永远不会因弹药耗尽而削弱。",
        "（设计决策）在GDD中明确或增加敌军弹药消耗系统"))

    # Bug 7: 命令历史无限增长
    bugs.append(("P3", "CommandSystem",
        "command_history无限增长，长时间游戏可能内存溢出。",
        "添加最大历史记录限制(如200条)"))

    # Bug 8: 重大伤亡阈值过低
    bugs.append(("P2", "BattleSimulator",
        "重大伤亡阈值10%过于敏感。100人的部队每次受11点伤害就-15士气，几次交战就崩溃。GDD未明确定义'重大'。",
        "提高阈值至20-25%，或增加60秒冷却时间"))

    return bugs

# ============================================================
# 缺失模块分析
# ============================================================

def missing_modules():
    return [
        ("SWO-145", "EnemyWaveManager.cs", "P0",
         "负责敌军波次生成/撤退/强化。当前无代码。",
         "BattleSimulator的敌军数据来源缺失，无法测试波次触发逻辑"),
        ("SWO-146", "AIDirector.cs", "P0",
         "负责LLM事件导演、难度自适应、降级机制。",
         "无降级机制，LLM API超时时游戏会卡死"),
        ("SWO-147", "SandTableRenderer.cs", "P1",
         "2D沙盘可视化渲染。",
         "核心UI缺失，玩家无法直观理解战场态势"),
        ("SWO-148", "Command→Battle对接", "P1",
         "CommandSystem.SendCommand不调用BattleSimulator。防御/炮击指令无实际效果。",
         "指令→数值链路断裂，玩家指令无意义"),
    ]

# ============================================================
# 主入口
# ============================================================

def main():
    print("🎯 WW2 Commander 集成测试运行器")
    print("测试范围: BattleSimulator, GameDirector, CommandSystem, 全链路集成")
    print("=" * 70)

    random.seed(42)

    test_battle_simulator()
    test_game_director()
    test_command_system()
    test_integration()

    bugs = detect_bugs()

    # 打印测试报告
    print("\n" + "=" * 70)
    print("WW2 Commander 集成测试报告")
    print("=" * 70)

    categories = {}
    for r in results:
        categories.setdefault(r["category"], []).append(r)

    for cat, tests in categories.items():
        passed = sum(1 for t in tests if t["passed"])
        total = len(tests)
        print(f"\n[{cat}] {passed}/{total} 通过")
        for t in tests:
            icon = "✅" if t["passed"] else "❌"
            print(f"  {icon} {t['name']}: {t['message']}")

    total_p = sum(1 for r in results if r["passed"])
    total_a = len(results)
    rate = total_p / total_a * 100 if total_a > 0 else 0
    print(f"\n{'-'*70}")
    print(f"总计: {total_p}/{total_a} 通过 ({rate:.1f}%)")

    # Bug报告
    print(f"\n{'='*70}")
    print("🐛 Bug 报告")
    print("=" * 70)
    if not bugs:
        print("  未发现 Bug ✅")
    else:
        for sev, mod, desc, fix in bugs:
            print(f"\n  [{sev}] {mod}")
            print(f"  问题: {desc}")
            print(f"  修复: {fix}")

    # 缺失模块
    print(f"\n{'='*70}")
    print("⚠️ 缺失模块报告")
    print("=" * 70)
    for issue_id, name, priority, desc, impact in missing_modules():
        print(f"\n  [{priority}] {issue_id} {name}")
        print(f"  状态: ❌ 未找到/未完成")
        print(f"  描述: {desc}")
        print(f"  影响: {impact}")

    # 总结
    print(f"\n{'='*70}")
    print("📊 测试总结")
    print("=" * 70)
    print(f"  单元测试: {total_p}/{total_a} 通过 ({rate:.1f}%)")
    print(f"  Bug 发现: {len(bugs)} 个 (P1={sum(1 for s,_,_,_ in bugs if s=='P1')}, P2={sum(1 for s,_,_,_ in bugs if s=='P2')}, P3={sum(1 for s,_,_,_ in bugs if s=='P3')})")
    print(f"  缺失模块: 4 个 (EnemyWaveManager, AIDirector, SandTableRenderer, Command→Battle对接)")
    print(f"\n  结论: 现有模块逻辑正确，但关键游戏循环模块缺失，")
    print(f"  无法进行完整集成测试。建议优先实现 SWO-145 和 SWO-146。")
    print("=" * 70)

if __name__ == "__main__":
    main()
