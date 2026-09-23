# MediumClass 新 Agent 交接（第二轮复查后）

日期：2026-09-23。先读取本文，再读仓库中的 `maintenance/REVIEW-2026-09-23.zh-CN.md`、`maintenance/HANDOFF.zh-CN.md`、`maintenance/VALIDATION.zh-CN.md`。

## 当前定位

- 上游：`https://github.com/Telyl/MediumClass`
- 上游基线：`e5fd3ba2e35350a218658abde4e093ff743175b7`
- 分支：`maintenance/wotr-2.7-offline`
- 原本地维护提交：`6dcc14c582869513ef1f8c0d4c4027625d0d8535`；对应 GitHub 代码提交：`141b2ef65120dadd941a1128f42e2f37869eb56a`。
- GitHub API 重建提交时改变了提交号，三批源码树逐一验证相同；完整映射见 `maintenance/github-import.json`。其后提交仅整理文档与移除当前树中的旧游戏 DLL/hash。
- 此前提交：`2f2d64b`（第一批维护）、`5799f1d`（两组内部 TypeId 修复）。
- 仓库：`https://github.com/shenzhoudadi/MediumClass`；请切换维护分支，默认 master 是原作者代码。
- 最新交付：`MediumClass-maintenance-2026-09-23.zip`；同名文件已更新，核对 `handoff.json` 中的提交。
- 当前维护源码及本文随维护分支交付；未发上游 PR 或版本。GitHub 下载包和备用 ZIP 均是源码，不是已编译安装包。

## 用户最新约束（优先遵守）

用户明确要求：**保留原 Mod 的职业内容和设计，不擅自重做玩法或调整强度。**

- 可修兼容、空引用、清理、登记和有原始说明支持的实现错误。
- 对规则解释、能力范围、费用、持续时间、Homebrew 及数值重设计有疑问时，先列证据和候选，不擅自选自己的规则版本。
- 首批 Trickster’s Edge 的新公式及删除 +3 补偿已撤回，保留上游数值算法，仅保留自身 modifier 防重复和空值保护。别按旧交接文档重新引入那个公式。
- 用户目前不能用游戏电脑，准备交给别人先测试。本轮交给新 Agent 的任务是独立复查、写报告后停止，不直接改代码；以 `REVIEW-PROMPT.zh-CN.md` 为准。
- 用户已创建 `shenzhoudadi/MediumClass` Fork 并授权上传独立维护分支。没有授权发布正式版本或给上游提 PR。
- 依赖信息来源优先级：官方公告/官网 > 作者 GitHub > Nexus 作者页。

## 目标环境

WotR PC 2.7.0x、UMM 0.33.0、ModMenu 1.3.2、TTT-Core 0.7.14a、BlueprintCore 2.8.7。版本信息沿用此前核对结果，本轮没有重做最新版本检索；出处和 TTT 版本标签差异见 HANDOFF。组合兼容性均待测试。

构建：net472、C# 9；从真实安装目录引用游戏/依赖程序集，BlueprintCore 内嵌；普通 Build 不部署。配置 `MediumClass.local.props` 后用 VS Developer PowerShell 运行 `scripts/Build.ps1`；StageMod 输出只有本 Mod DLL、Info.json、单个 `mediumclass_assets` 文件。

## 已修内容

第一批：移除构建机硬编码路径；固定依赖；独立构建/暂存/打包/部署；增加初始化阶段日志；BPC API 参数/命名空间适配；按原说明修骰面上界、副 Trickster 除法、Wild Arcana 六环分支、传奇 Archmage 资源分支；修复文本编码。

本次追加：

1. 删除灵体按来源事实和灵体键执行，不再清空所有灵体。
2. 多个 OnTurnOff 使用 Get 而非 Ensure，不在清理时重建 Part。
3. OnPostLoad 从通灵 Buff 蓝图及来源能力的 ContextActionApplySpirit 恢复主灵体，不按英文显示名猜。
4. 灵体加值/影响力惩罚的 stat modifier 按当前 Runtime 清理，不依赖已丢失的状态字段；数值未重写。
5. Archmage/Hierophant 转换列表登记幂等；尚有其他来源时不删整个 Part。
6. Transfer Magic 先检查并成功复制，再删来源；**完整合法法术过滤未实现**。
7. Arcane Surge 记录所有被修改 AbilityData 的原费用，按 Runtime 分隔并在退出时恢复；**读档与重叠效果仍待验证**。
8. Shared Seance 独行时也清理本人；**来源所有权和离队角色清理仍未解决**。
9. 主灵体尚未设置时的影响力检查不再直接索引字典。
10. 撤回 Trickster’s Edge 依据不足的数值改写，修正新手手册资源文件/回滚步骤。

## TypeId 修复与旧档风险

| 类型 | 当前 ID | 说明 |
|---|---|---|
| UnitPartArchmage | `18df8977af254951be0e49854a471953` | 保留旧值 |
| UnitPartHierophant | `0afe477b-82f7-410e-a905-048a80fb3d93` | 从与 Archmage 重复的 ID 分离 |
| MediumContextSpiritBonusComponent | `995fb9e0-f2f5-4dc2-a281-b7959ea95cda` | 保留已挂载组件的旧值 |
| MergeMediumSpellbookComponent | `b18c5744-bb1f-4c61-b769-02a71aa96fbb` | 未发现挂载或调用；从上一个 ID 分离 |
| DecisiveStrikeStandardComponent | `b61fefa0-ffda-416f-a405-50f5eaa0094a` | 旧值 `dc0b7d8176400bd46af14e7ddbf790a3` 与旧游戏 FreeActionSpell 的 TypeIdAttribute 精确冲突 |

没有完成旧存档迁移。特别检查 Hierophant 和 Marshal/Decisive Strike 活跃状态的旧档副本。不能将属于游戏原生类型的旧 ID 注册成 Mod 别名。原始证据和 DLL 哈希保存在 identifiers-baseline.json；旧游戏 DLL 不随源码包分发。

## 验证已到哪一步

- `python scripts/check_source.py --strict --syntax` 通过；96 份 C# tree-sitter 语法解析通过。
- `--typeid-assembly lib/Assembly-CSharp.dll` 在修改前检出 FreeActionSpell 冲突，修改后通过。检查的是仓库旧 DLL，不是最新游戏或其他依赖。
- Python 检查器语法、JSON/XML、GUID 和 Mod 身份检查、diff 空白检查通过。
- 原上游职业成长、法术书、熟练项、Blueprint GUID 文件完全相同；本地化差异只有损坏撇号恢复。
- 当前环境没有目标游戏和 .NET 构建工具，未执行 C# 编译、MSBuild、ILRepack、游戏或存档测试。
- ZIP 的补丁重建和文件校验状态以 `handoff.json` 为准。

## 仍然明确存在的事项

详细代码位置、原因、建议、验证和旧档影响见 REVIEW。优先级：

1. 目标环境编译、外部 TypeId 注册和首次冷启动。
2. 读档事件顺序、UnitPart/运行时字段重建、升级和洗点协调。
3. Shared Seance 实际受益者/来源持久化；避免按蓝图误删别人效果。
4. ApplySpirits 清理依赖状态与显示名；已授予 Fact/资源所有权。
5. Transfer Magic 的合法法术过滤、Surprise Strike 的首次攻击/24小时/攻击者归属冷却。
6. Arcane Surge 共享 AbilityData、存档与重叠效果，法术书计数和残留；不直接删除永久已知法术。
7. Trickster’s Edge 原公式的 ranks/+3/负值问题：先验证，不自行改成另一套规则。

## 测试机执行顺序

备份 → 记录真实游戏/依赖版本和路径 → 安装 VS/MSBuild/net472 targeting pack → 配置 props → 构建并保留完整输出 → StageMod → 最小依赖冷启动 → 新角色 → 六灵体保存/退出/读取/休息 → 旧档副本 → 特殊数值和多来源场景。

新手按仓库根目录 `TEST-GUIDE.zh-CN.md` 操作。任何失败提供完整日志、版本、步骤和必要存档副本。回滚时将测试 Mod 目录整个移到 Mods 之外，不能仅改名。
