# MediumClass 独立复查 Prompt

将下面“给 Agent 的任务”全文复制给新 Agent，并给它维护分支访问权，或上传 `MediumClass-maintenance-2026-09-23.zip`。不要让它只检查原作者仓库的 master。

## 当前增量

另须阅读 [最新自查记录](maintenance/SELF-REVIEW-2026-09-23.zh-CN.md)：当前 ModMenu 目标为 CasDragon/ModMenu 2.0.14；审查新增的角色状态、重复启用及暂存改动。

用户提供了针对 `73b78ab` 的独立报告，维护分支随后追加修正。必须先读 [独立报告复核与后续修正](maintenance/INDEPENDENT-REVIEW-FOLLOWUP.zh-CN.md)，再审查最新 HEAD；不要沿用报告中“UMM 0.33.0 不存在”的错误结论，也不要把 Source 身份失效从假设写成已证实。原提交映射只覆盖最初三批维护。

## 交接定位

- 原仓库：`https://github.com/Telyl/MediumClass`
- 上游基线：`e5fd3ba2e35350a218658abde4e093ff743175b7`（0.1.3-beta）。
- 本地维护分支：`maintenance/wotr-2.7-offline`。
- 本地源码提交：`6dcc14c582869513ef1f8c0d4c4027625d0d8535`；GitHub 对应代码提交：`141b2ef65120dadd941a1128f42e2f37869eb56a`。
- 三批维护提交依次为 `2f2d64b`、`5799f1d`、`6dcc14c`。
- 备用 ZIP SHA256：`d6350345946d00d484bf5568d3f384b01ed5888bbe9d54730fae8ed022c3d310`。
- 审查入口：[个人维护分支](https://github.com/shenzhoudadi/MediumClass/tree/maintenance/wotr-2.7-offline)。三批 GitHub 提交与本地提交的源码树已逐一核对一致，映射见 `maintenance/github-import.json`；`73b78ab` 为文档整理与当前树移除旧游戏 DLL/hash；更晚的机制实现修正见本提示词开头。请在报告中记录你实际读取的最新 HEAD。

---

## 给 Agent 的任务

请用中文独立复查 Pathfinder: Wrath of the Righteous 的 Medium 职业 Mod 维护版本。此次只审查和写报告，不修改代码、不提交 PR、不发布版本。可以在临时目录进行静态分析、依赖核对和具备真实依赖时的构建验证。

### 首要约束

这是原 Mod 的兼容维护，必须保留原职业与六灵体设计、成长表、法术列表、Homebrew、能力费用和持续时间。不要按你理解的桌游规则重写能力或自行调整平衡。若原说明、实现与引擎行为冲突，列出证据和最小修复方案，等待用户决定。已有维护文档仅供线索，不能作为“修复正确”的证明。

### 先确定审查对象

1. 报告你实际读取的仓库、分支、HEAD、工作区状态。如果只有 ZIP，读取 `handoff.json`，核对 SHA256SUMS，并说明缺少 Git 历史。
2. 阅读根目录 README；`maintenance/HANDOFF.zh-CN.md`、`maintenance/REVIEW-2026-09-23.zh-CN.md`、`maintenance/VALIDATION.zh-CN.md`、`maintenance/identifiers-baseline.json`；再读根目录的 `AGENT-HANDOFF.zh-CN.md` 和 `TEST-GUIDE.zh-CN.md`。
3. 对比原始基线与维护版本，逐项检查维护改动有没有引入新错误。不要只搜索 TODO 或复述已有清单。必要时分别审查三批提交，尤其注意最后一批撤回和状态清理修改。

### 依赖与构建

自行重新核对最新稳定版本，来源优先级：官方公告/官网 > 原作者 GitHub Release/源码 > Nexus 作者页面。记录 URL、查询日期、准确版本及适用平台，区分最新版本与项目当前目标。先前目标为 WotR PC 2.7.0x、UMM 0.33.0、ModMenu 1.3.2、TTT-Core 0.7.14a、BlueprintCore 2.8.7，不要未经核验就沿用“最新”的说法。

检查 net472/C#9、NuGet、AssemblyPublicizer、ILRepack、引用解析及 MSBuild Target 顺序；核实实际版本 API 签名、Harmony 钩子是否存在。检查 Build 是否只构建，StageMod/PackageMod/DeployMod 是否显式执行，打包是否只包含本 Mod 的白名单文件。不能依赖仓库旧游戏 DLL 证明最新版兼容；不要上传游戏 DLL、凭据、本机路径或测试存档。

当前版本只做过静态检查及 96 份 C# tree-sitter 语法解析，未进行 MSBuild 编译和游戏测试。请独立运行可用检查并保存实际输出：

```text
python -m pip install -r scripts/requirements-checks.txt
python scripts/check_source.py --strict --syntax
```

有真实目标程序集时，可重复传入 `--typeid-assembly <DLL路径>`。语法解析通过不等于类型检查通过，编译通过不等于游戏可运行。没有游戏环境就明确标注无法验证，不能编造测试结果。

### 必查范围

- 游戏卡加载：Main 的初始化阶段、重入/失败状态、Harmony 安装/卸载、蓝图注册顺序和依赖准备时机。区分可确认缺陷与加载卡顿根因猜测。
- TypeId/GUID：检查内部与游戏/依赖之间的冲突；区分 Blueprint GUID、序列化 TypeId 和运行时 Fact 身份。现有三处更换：Hierophant UnitPart、MergeMediumSpellbookComponent、DecisiveStrikeStandardComponent；评估旧档迁移，不要简单给冲突旧 ID 加别名。
- OnPostLoad：从通用通灵 Buff 来源能力恢复主灵体的链条是否可靠；空 Context、回调顺序、二次读档和旧档是否会丢状态。
- 灵体切换、休息、升级、洗点完成/取消：来源事实与灵体键移除是否正确，清理能否重复执行，退出是否意外创建 Part，状态丢失时是否漏清效果。
- 法术书：临时与永久已知法术、CL、法术位、AddSpellbook/ForbidSpellbook 计数、神话合书、Archmage/Hierophant 转换列表去重与多来源字段。
- 共享灵体祝福：实际授予 Fact 的所有权，独行、离队、双 Medium、同蓝图来自其他来源；检查仍按蓝图/显示名删除导致误删与漏删的问题。
- Spirit Surge：骰子上界、退款对象、零成本、免费次数、影响力惩罚触发顺序与保存恢复。
- Trickster’s Edge：最后一批已恢复原公式 `Medium等级 - 原技能BaseValue` 与原零 ranks `+3` 补偿；核实实际 ranks/ClassSkill/Modifier 语义，不能再无证据删除补偿或改公式。检查新增幂等清理是否改变结果。
- Surprise Strike：主副灵体骰数、物理伤害类型、DR/精准免疫、多段攻击、首次攻击与首次命中差异、休息与24小时差异、多个攻击者共用冷却。
- Transfer Magic：成功复制后移除的顺序、异常/失败行为、Buff provenance、harmless、最高环/同环随机、personal、永久/瞬时与区域效果限制。之前只修了先删后复制问题，过滤并未完成。
- Arcane Surge/AddResourcelessSpell：ConditionalWeakTable 按 Runtime 隔离是否正确，原费用恢复、同一 AbilityData 多来源覆盖、读档与结束事件时机。
- modifier 清理、Marshal 防御 Buff 来源、MediumTranceOfThree 的 ranks 等运行时字段是否在蓝图组件间共享或不能恢复。
- README 未完成项、无效设置和未挂载组件：明确区分功能缺失/设计选择与真正 bug。不要直接启用未挂载的合书组件或 Spirit Focus。

### 原内容保留审计

对比 `Medium/MediumProgression.cs`、`Medium/MediumSpellbook.cs`、`Medium/MediumProficiencies.cs`、`Utilities/Guids.cs`、六灵体能力定义和本地化。此前报告称上述四个文件与上游逐字节相同、236 个 Blueprint GUID 不变、本地化只修四份 JSON 损坏撇号；请独立验证。逐项区分“修正实现错误导致行为变化”与“擅自更改职业设计”，有证据不足的改动单列。

### 报告格式

请输出并保存一份 `INDEPENDENT-REVIEW.zh-CN.md`，内容包括：

1. 实际审查范围、源码版本、依赖来源、运行过的命令、结果与未验证范围。
2. 按严重性排序的问题表：编号、已确认/高风险待实测/功能缺失或设计选择、文件及行号/函数、证据与因果链、原有缺陷还是维护引入、最小修复建议、验证方法、旧存档风险。
3. 对三批维护修改逐项给出“合理/有缺陷/证据不足”的结论，尤其复查新增状态恢复、清理和费用缓存逻辑。
4. 原内容保留审计结果，明确哪些行为变化有依据，哪些应撤回或由用户决定。
5. 分阶段 TODO：构建与加载 → 读档/切换/清理 → 机制数值 → 文档/打包；区分可以离线修复与必须游戏验证的事项。
6. 给测试者的最小用例矩阵：最小依赖冷启动、新角色、六灵体循环、保存完全退出再读、休息、升级、洗点、双 Medium、离队、Hierophant/Marshal 旧档副本。
7. 给普通用户的简短结论：是否适合开始测试、最先可能遇到什么、还需要哪些版本/日志/存档。

完成审查后停止，等待用户确认下一轮修复。不要因为前一位 Agent 写“已修复”就跳过验证，也不要因为没有游戏就只给泛泛建议。
