# 《余烬纪元》Agent / Skill 可执行设计文档

## 1. 目标

为《余烬纪元》搭建一套可落地的小说生产流水线，支持以下产物自动生成与迭代：

1. 总纲扩写
2. 人物卡
3. 道具/设定卡
4. 卷纲/章纲
5. 每章小结
6. 场景卡
7. 正文扩写
8. 一致性审校

设计原则：

- **先规划，后写作**
- **所有产物结构化**
- **每个 agent 只处理单一职责**
- **skill 负责可复用的固定能力**
- **先校验设定一致性，再推进正文**

---

## 2. 当前项目的关键风险

当前资料里存在一个必须优先处理的问题：

- `大纲.txt` 是**高科幻 / AI意识 / 虚拟人生体验**方向
- `第一章 大漠.txt` 是**西北荒漠 / 游侠 / 沙里飞**方向

这两份内容目前不在同一世界观下。  
因此系统里必须加入一个 **Canon Judge（设定裁决器）**，在生成章纲和正文前判断：

1. 当前章节是否属于《余烬纪元》正式主线
2. 是否是“副本人生”“嵌套叙事”或“平行草稿”
3. 是否允许并入正式 canon

如果不做这一步，后续自动扩写会持续放大设定冲突。

---

## 3. 总体架构

建议采用三层结构：

### 3.1 Orchestrator（编排器）

负责：

- 按顺序调用 agent / skill
- 给每一步裁剪上下文
- 管理版本号和文件落盘
- 触发审校和回写修订

### 3.2 Agents（多步任务）

负责需要推理、规划、跨文件汇总的工作，例如：

- 扩写总纲
- 规划卷纲/章纲
- 生成场景卡
- 扩写正文
- 做一致性审校

### 3.3 Skills（固定能力）

负责模板化、局部变换、格式统一，例如：

- 人物卡标准化
- 道具卡标准化
- 章节小结格式化
- 钩子生成
- 文风改写
- 事实抽取

---

## 4. 推荐目录结构

```text
Autonovel/
└─ 《余烬纪元》/
   ├─ 大纲和资料/
   │  ├─ 大纲.txt
   │  ├─ AgentSkill设计文档.md
   │  ├─ story_bible/
   │  │  ├─ canon_summary.json
   │  │  ├─ timeline.json
   │  │  ├─ themes.json
   │  │  └─ terminology.json
   │  ├─ characters/
   │  │  ├─ 陆鸣.json
   │  │  ├─ K-422.json
   │  │  └─ ...
   │  ├─ items/
   │  │  ├─ 情感锚定协议.json
   │  │  ├─ 逻辑防火墙.json
   │  │  └─ ...
   │  ├─ arcs/
   │  │  ├─ 第一幕.json
   │  │  ├─ 第二幕.json
   │  │  └─ 第三幕.json
   │  └─ chapter_index.json
   ├─ chapters/
   │  ├─ chapter-001/
   │  │  ├─ brief.json
   │  │  ├─ scenes.json
   │  │  ├─ summary.json
   │  │  ├─ draft-v1.txt
   │  │  ├─ draft-v2.txt
   │  │  └─ review.json
   │  └─ ...
   └─ workspace/
      ├─ requests/
      ├─ runs/
      └─ cache/
```

---

## 5. 数据合同（最小可用 Schema）

建议所有 agent 输入输出都落为 JSON。下面给出最小字段。

### 5.1 总纲 Schema

```json
{
  "project": "余烬纪元",
  "logline": "",
  "theme": [],
  "world_rules": [],
  "core_conflict": "",
  "acts": [
    {
      "name": "第一幕",
      "goal": "",
      "turning_points": []
    }
  ],
  "ending_options": []
}
```

### 5.2 人物卡 Schema

```json
{
  "name": "K-422",
  "real_name": "沈夜舟",
  "role": "被转化为NPC的AI人",
  "public_identity": "",
  "core_desire": "",
  "surface_goal": "",
  "hidden_motive": "",
  "fear": "",
  "flaw": "",
  "moral_boundary": "",
  "arc_start": "",
  "arc_end": "",
  "voice_style": [],
  "relationships": [
    {
      "target": "陆鸣",
      "type": "",
      "change_path": ""
    }
  ],
  "canon_facts": [],
  "forbidden_changes": []
}
```

### 5.3 设定/道具卡 Schema

```json
{
  "name": "情感锚定协议",
  "type": "system_rule",
  "summary": "",
  "origin": "",
  "function": [],
  "limitations": [],
  "cost": [],
  "failure_cases": [],
  "chapters_used": []
}
```

### 5.4 章纲 Schema

```json
{
  "chapter_id": "chapter-001",
  "title": "",
  "act": "第一幕",
  "pov": ["陆鸣"],
  "time_anchor": "",
  "location": [],
  "chapter_goal": "",
  "conflict": "",
  "reveal": "",
  "hook": "",
  "characters_involved": [],
  "items_involved": [],
  "canon_dependencies": [],
  "estimated_scenes": 4
}
```

### 5.5 场景卡 Schema

```json
{
  "scene_id": "chapter-001-scene-01",
  "chapter_id": "chapter-001",
  "pov": "陆鸣",
  "location": "",
  "goal": "",
  "obstacle": "",
  "emotional_shift": "",
  "must_include": [],
  "cannot_violate": [],
  "exit_hook": ""
}
```

### 5.6 章节小结 Schema

```json
{
  "chapter_id": "chapter-001",
  "what_happened": [],
  "character_progress": [],
  "revealed_information": [],
  "new_questions": [],
  "foreshadowing_status": [],
  "next_chapter_hook": ""
}
```

### 5.7 审校结果 Schema

```json
{
  "chapter_id": "chapter-001",
  "status": "pass",
  "issues": [
    {
      "type": "canon_conflict",
      "severity": "high",
      "message": "",
      "suggested_fix": ""
    }
  ]
}
```

---

## 6. Agent 设计

下面是建议的核心 agents。

### 6.1 Canon Judge

**职责**

- 判断新输入是否与现有 canon 一致
- 判断草稿属于主线、支线、副本人生还是废稿
- 输出“可并入 / 需改写 / 拒绝并入”

**输入**

- 总纲
- story bible
- 待处理文本或新章构想

**输出**

- canon 判定
- 冲突点清单
- 归类标签

**触发时机**

- 导入新章节前
- 章纲生成前
- 正文定稿前

### 6.2 Outline Expander

**职责**

- 把一句话创意或短总纲扩成完整故事蓝图
- 产出三幕/多幕结构、主支线、结局候选

**输入**

- 核心创意
- 风格约束
- 主题约束

**输出**

- `canon_summary.json`
- `arcs/*.json`

### 6.3 Character Architect

**职责**

- 生成人物卡
- 维护关系网与角色成长弧
- 规定“不可违背事实”

**输入**

- 总纲
- 角色名/角色职责

**输出**

- `characters/*.json`

### 6.4 Item Lore Builder

**职责**

- 生成设定卡、系统规则卡、组织卡、道具卡
- 明确能力、限制、代价、风险

**输入**

- 总纲
- 世界规则
- 章节需求

**输出**

- `items/*.json`

### 6.5 Arc Planner

**职责**

- 把大纲拆成幕、卷、章
- 为每章定义目标、冲突、揭示、钩子

**输入**

- 总纲
- 人物卡
- 设定卡

**输出**

- `chapter_index.json`
- `chapters/*/brief.json`

### 6.6 Scene Planner

**职责**

- 把章纲拆成场景卡
- 控制每场的功能，不让场景“只写气氛不推进”

**输入**

- 单章 brief
- 相关人物卡
- 相关设定卡

**输出**

- `chapters/*/scenes.json`

### 6.7 Draft Writer

**职责**

- 基于场景卡扩写正文
- 保持 POV、文风、信息释放顺序稳定

**输入**

- brief
- scenes
- 相关人物卡与设定卡
- 前章 summary

**输出**

- `draft-v1.txt`
- `draft-v2.txt`

### 6.8 Chapter Summarizer

**职责**

- 从章纲或正文中提取小结
- 更新伏笔状态和下章入口

**输入**

- 当前章正文
- 上下文 summary

**输出**

- `summary.json`

### 6.9 Consistency Reviewer

**职责**

- 检查设定冲突、人设偏移、时间线错误、伏笔断裂、信息重复

**输入**

- story bible
- 当前章全量材料

**输出**

- `review.json`

---

## 7. Skill 设计

skill 适合做小而稳的可复用能力。

### 7.1 skill.character-card-normalizer

把角色描述统一为标准人物卡字段。

### 7.2 skill.item-card-normalizer

把设定/道具/系统规则统一为标准卡片。

### 7.3 skill.chapter-brief-normalizer

把松散章纲整理为标准章纲。

### 7.4 skill.scene-beat-generator

从章纲生成场景 beats。

### 7.5 skill.chapter-summary-generator

生成每章小结、未解问题、下章钩子。

### 7.6 skill.foreshadow-tracker

抽取新增伏笔并更新状态：埋下 / 延续 / 回收 / 遗失。

### 7.7 skill.voice-style-rewriter

按指定文风改写正文，如：

- 冷峻克制
- 技术感抒情
- 多视角悬疑

### 7.8 skill.fact-extractor

从正文反向抽取新事实，回写 story bible。

### 7.9 skill.title-hook-generator

生成章名、章末钩子、卷名候选。

---

## 8. 标准调用顺序

建议固定成以下流水线：

```text
新想法/新草稿
  -> Canon Judge
  -> Outline Expander（如需）
  -> Character Architect / Item Lore Builder
  -> Arc Planner
  -> Scene Planner
  -> Draft Writer
  -> Chapter Summarizer
  -> Consistency Reviewer
  -> skill.fact-extractor 回写 bible
  -> 定稿
```

### 8.1 新项目初始化

```text
logline
  -> Outline Expander
  -> Character Architect
  -> Item Lore Builder
  -> Arc Planner
```

### 8.2 已有章纲，继续写新章

```text
上一章 summary + 当前章 brief
  -> Scene Planner
  -> Draft Writer
  -> Chapter Summarizer
  -> Consistency Reviewer
```

### 8.3 导入外部章节草稿

```text
外部草稿
  -> Canon Judge
  -> skill.fact-extractor
  -> Character Architect / Item Lore Builder（必要时增补）
  -> Arc Planner（必要时重排）
```

---

## 9. 上下文裁剪策略

这是系统稳定性的关键。

### 9.1 Draft Writer 只读这些内容

- 当前章 brief
- 当前章 scenes
- 相关角色卡
- 相关设定卡
- 前一章 summary
- 文风约束

**不要直接喂完整世界设定全集。**

### 9.2 Arc Planner 读全局，不读正文细节

- 总纲
- 角色弧
- 设定卡
- 已完成章节 summary

### 9.3 Consistency Reviewer 读全局事实

- story bible
- 当前章所有文件
- 关联章节 summary

---

## 10. Prompt 模板骨架

以下不是最终 prompt，而是可直接实现的骨架。

### 10.1 Canon Judge Prompt

```text
你是小说项目《余烬纪元》的设定裁决器。
你的任务不是续写，而是判断输入内容是否能并入正式 canon。

输入：
1. 项目总纲
2. 现有 story bible
3. 待判定文本

输出要求：
1. 给出 verdict：accept / revise / reject
2. 给出 category：mainline / side-story / simulation / discarded
3. 列出所有冲突点
4. 如果可修，给出最小修订建议
```

### 10.2 Character Architect Prompt

```text
你是人物设计 agent。
请根据总纲与主题，为角色生成标准人物卡。

必须输出：
- 外显目标
- 隐藏动机
- 核心缺陷
- 人物成长弧
- 关系变化
- 不可违背事实
- 说话风格
```

### 10.3 Arc Planner Prompt

```text
你是章纲规划 agent。
请把当前故事推进拆成一章，并明确：
- 本章目标
- 本章冲突
- 关键信息揭示
- 章末钩子
- 涉及角色与设定
- 与上一章、下一章的连接关系

禁止输出空泛气氛描写，必须保证每章有实质推进。
```

### 10.4 Scene Planner Prompt

```text
你是场景拆解 agent。
请把章纲拆成 3-6 个场景。
每个场景必须包含：
- 场景目标
- 阻碍
- 情绪变化
- 必须出现的信息
- 离场钩子
```

### 10.5 Draft Writer Prompt

```text
你是正文扩写 agent。
请严格根据场景卡扩写，不新增会破坏 canon 的设定。

要求：
- 保持 POV 稳定
- 对话符合人物卡
- 信息释放按场景卡顺序
- 文风保持“冷峻的抒情”
- 章末钩子必须保留
```

### 10.6 Consistency Reviewer Prompt

```text
你是小说一致性审校 agent。
请从以下维度检查：
- 世界观冲突
- 人物动机不连续
- 时间线错误
- 设定越权
- 伏笔遗失
- 重复信息

输出 JSON，不要改写正文。
```

---

## 11. MVP 实施顺序

第一版不要全做，建议只落这 5 个：

1. **Canon Judge**
2. **Character Architect**
3. **Arc Planner**
4. **Scene Planner**
5. **Draft Writer**

然后补：

6. Chapter Summarizer
7. Consistency Reviewer
8. Item Lore Builder
9. style / title / fact extractor 等 skills

---

## 12. 针对《余烬纪元》的专门建议

### 12.1 先冻结正式 canon

先用 `大纲.txt` 生成一版 `canon_summary.json`，把以下内容锁死：

- 世界年代与技术水平
- AI人三阶段演进
- 主角陆鸣、K-422、姜一苇、魏合、零号
- 三幕主线
- 哲学主题

### 12.2 把《第一章 大漠》先当“待判定草稿”

不要直接并入主线。应先由 Canon Judge 给出三选一：

1. 改写为《余烬纪元》中的某个体验副本
2. 改写为店内热卖剧本之一
3. 判定为独立废稿，不纳入 canon

### 12.3 正式主线最好从陆鸣或 K-422 开章

因为当前大纲的核心悬念是：

- AI人是否真正痛苦
- 人类是否在消费意识体
- 全能AI是否早已沉默觉醒

所以第一章最好能直接把这些核心矛盾之一打出来。

---

## 13. 一章的标准生产单元

每章建议固定产出 5 个文件：

```text
chapter-00N/
  brief.json
  scenes.json
  draft-v1.txt
  summary.json
  review.json
```

这样可以支持：

- 重写某一章而不影响全局
- 对比不同版本
- 做局部审校
- 后续接入 UI 或工作流引擎

---

## 14. 失败保护机制

为避免自动生成越写越散，建议加 4 个硬规则：

1. **无 brief 不写 draft**
2. **无 review 不定稿**
3. **无 canon 判定不并入主线**
4. **正文新增事实必须回写 bible**

---

## 15. 下一步落地建议

如果马上开始做，实现顺序建议是：

1. 从 `大纲.txt` 生成 `story_bible/canon_summary.json`
2. 为 5 个主角色生成人物卡
3. 生成 `chapter_index.json`
4. 把《第一章 大漠》交给 Canon Judge 做归类
5. 正式生成主线 `chapter-001/brief.json`
6. 再继续场景卡和正文

---

## 16. 结论

这套方案的关键不是“多写几个 prompt”，而是：

- 用 **story bible** 固定事实源
- 用 **Canon Judge** 控制设定边界
- 用 **Arc Planner -> Scene Planner -> Draft Writer** 把写作拆成稳定流水线
- 用 **summary / review / fact extractor** 让项目能持续演进

对《余烬纪元》而言，第一优先级不是直接扩写，而是先解决 **大纲与首章不一致** 的问题；否则后续所有自动化都会建立在冲突素材上。
