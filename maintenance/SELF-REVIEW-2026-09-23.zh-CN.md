# MediumClass 再次自查与最小修正

日期：2026-09-23。起点为个人 Fork 的 `maintenance/wotr-2.7-offline`，GitHub 提交 `aea63b6a2c4d3254be615a3c7abc363fa7c73a61`（本地 `a57b43c569f400be106e73764b18385bbcd4de8d`，两者源码树相同）。

起点的实际源码 tree：`5c184508cfca0ded9be227b317dbc0e17fea7cbd`。本次检查近期差异、蓝图组件状态、通灵/清理、设置 API 和构建任务；没有执行游戏、C# 编译或安装部署。下面的“修正”指已更改源码，不代表实机验收。

## 1. 必须纠正的依赖结论

此前仅查看 WittleWolfie/ModMenu，把 1.3.2 当成最新，遗漏了维护移交。这是此前维护与复查的遗漏。

- [原作者仓库](https://github.com/WittleWolfie/ModMenu) 已归档，README 指向 CasDragon 的维护仓库。
- 直接查询现维护者 GitHub Releases API，最新非草稿、非预发布版本为 [2.0.14](https://github.com/CasDragon/ModMenu/releases/tag/2.0.14)，发布于 **2026-09-01 13:32:20 UTC**。
- 附件为 `ModMenu-2.0.14.zip`；API 声明 SHA-256 为 `0ddc34495d820777d53f8a1a4f99a6e892dc7f4e1272ebb3abb2bbbef1e2e363`。本轮没有下载附件计算哈希。
- 查阅标签 `2.0.14` 的 `ModMenu/ModMenu.cs` 与 `ModMenu/Settings/SettingsBuilder.cs`：本项目使用的 `SettingsBuilder.New`、`AddDefaultButton`、`AddSubHeader`、`AddToggle`、`Toggle.New`、`WithLongDescription`、`AddSettings`、`GetSettingValue<T>` 仍存在匹配的源码签名。这不等于二进制或游戏兼容性通过。
- `Info.json:7`、README 与当前交接/测试手册改为 **ModMenu 2.0.14**。这是选定的测试基线；此门槛会要求测试者升级旧 ModMenu，不表示 1.3.2 已被证明必然不能运行。
- 其余目标保持 WotR PC 2.7.0x、UMM 0.33.0、TTT-Core 0.7.14a、BlueprintCore 2.8.7。本次没有重做其余全部版本查询。UMM 版本的直接页面核验见 [前次复核](INDEPENDENT-REVIEW-FOLLOWUP.zh-CN.md)，不采纳独立报告的 0.32.5 降级建议。

## 2. 本次发现与处理

行号对应本次修改后的文件；后续编辑请优先按类/方法定位。

| 问题与状态 | 文件位置 | 原因与本次修正 | 验证方法 | 旧档影响 |
|---|---|---|---|---|
| ApplySpirits 缓存角色 Part：缓存存在已确认；多角色触发后果待实测 | `Medium/NewComponents/ApplySpirits.cs:38–70` | 蓝图组件字段缓存 `UnitPartMedium`，退出时 `??=` 只补空值，无法识别其他角色或被替换的旧 Part。改成从当前 Runtime Owner 取 Part 的只读属性，保持现有缺状态守卫与授予/撤销算法 | 双 Medium 顺序通灵不同灵体，分别休息、洗点；加入保存/退出/读取。检查自己的属性和能力，不只看日志 | 不新增存档字段；不迁移已丢失状态，也没有修好全部所有权清理 |
| Decisive Strike 直接依赖 Context/MainTarget：已确认；读档空引用触发待实测 | `Medium/NewComponents/AbilitySpecific/DecisiveStrikeStandardComponent.cs:27–44`；授予定义在 `Medium/Spirits/Marshal/DecisiveStrike.cs` | Buff 明确施加于受益队友，却从施法上下文取队友能力。改从 Buff Owner 取；重启用先按本 Fact 清旧登记，防重复。保留原法术筛选、免费动作及施法后撤销规则 | Marshal 对队友施放；队友施法/到期后核对动作；存读、重复启用、双 Marshal 分别验证 | 本次未改 TypeId，之前该组件的 TypeId 迁移风险仍存在；没有修复已有旧登记 |
| Trickster 通灵祝福重复启用未先清自身：已确认；实际重复回调待实测 | `Medium/NewComponents/AbilitySpecific/AddTricksterSeance.cs:31–45` | `OnTurnOn` 每次直接 AddModifier。加入空 stat 防护，先按当前 Runtime 移除自己的 modifier，再按原算法添加 | 零 ranks、有 ranks、反复通灵/读档；原单次 `1`、零 BaseValue 时额外 `+3` 保持不变 | 不新增字段，不删除其他 Runtime 的修正；不承诺清除此前已孤立的修正 |
| 暂存白名单不能约束合并工具的旁产物：配置缺口已确认，实际产物待构建 | `MediumClass.csproj:187–215` | ILRepack 直接输出到 stage，Deploy 又使用通配符。标签 v2.0.13 的任务源码在 Windows 默认开启 DebugInfo。改为输出到 `obj/mod-merge/<Configuration>`，Stage 与 Deploy 都显式复制 DLL、Info.json、mediumclass_assets 三项 | Windows `StageMod` 完整日志；stage 恰好三文件，DLL 含 BPC 类型，Package ZIP 对照 stage | 不影响存档；普通 Build 不部署。不会自动删除已经安装目录中的历史多余文件，测试安装按手册先备份旧目录 |

ILRepack 一手证据：[固定版本任务源码](https://github.com/peters/ILRepack.MSBuild.Task/blob/v2.0.13/src/ILRepack.MSBuild.Task/ILRepack.cs)。其 `InputAssemblies` 是 `ITaskItem[]`，内部对输入执行 `Distinct()`；没有把分号参数或重复列主程序集直接认定为构建错误。

## 3. 未解决的问题与分类

### 已确认的剩余实现缺口

- `ApplySpirits.Revert` 仍按英文显示名子串清理部分 Buff，其他撤销路径按蓝图删除且无实际授予 Fact 记录；`MediumContextSharedSeanceComponent.OnTurnOff` 仍缺少持久化受益者/授予来源。多来源误删与离队漏清风险不能靠扩大删除范围修复。
- `ContextActionTransferMagic.RunAction` 没有完整实现原说明的合法法术筛选；`ImpromptuSurpriseStrike`/`SurpriseStrike.cs` 的首次命中、按目标共享冷却、休息移除与原说明存在差异。这些是原有实现缺口，本轮未重做规则。
- `UnitPartArchmage.cs:335–340` 与 `UnitPartHierophant.cs:248–253` 的 `GetConversionSpells` 对没有填充的数组元素调用 `Select`。仓库内未找到调用方，因此不能拿它解释当前启动卡住；若未来使用该入口，先明确它应从哪个转换列表返回数据，再修复，不能简单返回空列表后宣称机制正确。

### 高风险，需要目标程序集与游戏验证

- `MediumSpiritSurgeComponent` 在组件字段缓存 Stats/Concentration/CharacterLevel/MarshalBonus，关闭时依赖 Stats，专注回调也读取缓存。字段是否跨 Runtime 混用、读档是否重建应优先查证。不要直接把这些字段改为一份静态缓存；需要按 Runtime 分离并明确存读生命周期。本轮未调整骰面、退款或专注算法。
- `MediumTranceOfThreeComponent:26–41` 的 ranks、`MediumInfluencePenaltyComponent` 的 fightDefensively 仍不是明确持久化的运行时记录。Trance 组件实际挂载在蓝图上，不能将它归入未启用功能。
- `AddResourcelessSpell` 的费用原值缓存不进存档；同一 AbilityData 被重叠修改的恢复顺序未解决。必须测非零原费用、中途存读和双角色，不可直接统一写回 0。
- `UnitPartMedium.OnPostLoad` 与 Buff 启用顺序、Source 身份恢复、读档后的转换列表与法术书清理仍待验证。保留 I04 严格来源条件，没有采用 null 通配删除。
- 前次通灵动作排序已检查保持 ApplySpirit → Influence；跨阈值扣费发生在动作前后、免费 Surge 退款与惩罚协调仍须观察，不能从源码动作顺序推出整个资源流程正确。
- `Main` 初始化、Harmony 安装、Publicize 和目标 API 尚无实际构建/冷启动证据。无法排除加载卡住；日志 BEGIN/END/FAILED 用于定位，不是成功运行证明。

### 未启用功能或需用户决定的设计

合书组件、Spirit Focus 的启用、图标及原 README 中 Homebrew 未完成项保持原状。Trickster’s Edge 的原公式与 +3 保留；本次没有借“维护”重做职业成长、法术列表、费用、持续时间或数值强度。

## 4. 验证范围

本次执行 `python scripts/check_source.py --strict --syntax` 与从上游到工作树的 `git diff --check e5fd3ba2e35350a218658abde4e093ff743175b7`，结果见 [验证记录](VALIDATION.zh-CN.md)。

对比上游：`MediumProgression.cs`、`MediumSpellbook.cs`、`MediumProficiencies.cs`、`Utilities/Guids.cs` 字节保持一致。本次没有修改前次通灵排序、OnPostLoad 修正或 Trickster’s Edge 公式。

没有 .NET/MSBuild/目标游戏环境；未做编译、ILRepack、冷启动、读档。源码静态检查通过不能标为“已兼容”或“可直接安装”。本次只同步个人维护分支，不提交上游 PR、不发布版本。

## 5. 下一步

1. 测试机按手册安装当前依赖，使用维护分支最新源码构建并保存完整输出。
2. 检查 stage 三文件及 BPC 合并；最小依赖冷启动，失败时收集完整日志。
3. 新角色六灵体 → 双 Medium/Marshal → 存读、休息、升级、洗点 → Hierophant/Marshal 旧档副本。
4. 优先核验共享组件的运行时字段与授予所有权，再决定存档兼容的结构性修复；测试前不要覆盖重要旧档。
