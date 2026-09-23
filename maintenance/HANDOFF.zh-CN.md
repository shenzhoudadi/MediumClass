# MediumClass 离线维护交接

日期：2026-09-23。分支：`maintenance/wotr-2.7-offline`。
上游基线：`Telyl/MediumClass` 的 `e5fd3ba2e35350a218658abde4e093ff743175b7`（0.1.3-beta）。

**这是可继续开发的源码快照，不是已验证可玩的 Mod 安装包。没有编译、运行游戏或测试旧存档。**
`Info.json` 的 0.1.4 仅用于区分这批维护源码，尚未发布；Mod Id、程序集名、入口和蓝图 GUID 保留。为消除重复注册，Hierophant UnitPart、未使用的合并法术书组件及与游戏类型冲突的 DecisiveStrikeStandardComponent 更换了 TypeId，旧档影响见下文。

> **最新状态与原内容保留审计见 [第二轮复查](REVIEW-2026-09-23.zh-CN.md)。** 本次还修复一组与游戏 `FreeActionSpell` 的外部 TypeId 冲突，并撤回 Trickster’s Edge 的推测性公式重写。

## 1. 目标环境

| 项目 | 目标 | 来源 |
|---|---|---|
| WotR Windows/PC | 2.7.0x | [官方公告](https://store.steampowered.com/news/app/1184370/view/822580849468768447) |
| UMM | 0.33.0 | [作者主页](https://www.nexusmods.com/site/mods/21)；作者 GitHub README 指向该页 |
| ModMenu | 1.3.2 | [GitHub Release](https://github.com/WittleWolfie/ModMenu/releases/tag/v1.3.2) |
| TTT-Core | 0.7.14a | [GitHub Release](https://github.com/Vek17/TabletopTweaks-Core/releases/tag/v0.7.14)，标签仍是 v0.7.14 |
| BlueprintCore | 2.8.7 | [GitHub](https://github.com/WittleWolfie/WW-Blueprint-Core/releases/tag/v2.8.7) / [NuGet](https://www.nuget.org/packages/WW-Blueprint-Core/2.8.7) |

来源选择按官方公告/官网 > 原作者 GitHub > Nexus 作者页。所有组合兼容性均为待测试。
TTT-Core 的标签内 Info.json 是 0.7.14，master 为 0.7.14a；请使用对应 a 修订发行包并记录 DLL 哈希。
UMM 的 Requirements 使用 TTT `0.7.14` 数字最低版本，避免把 a 后缀的版本解析当作已验证行为；实际验收仍以 0.7.14a 为目标。
构建工具固定 AssemblyPublicizer 1.0.2、ILRepack.MSBuild.Task 2.0.13；保持 net472，固定 C# 9。

## 2. 本批已实施内容

| 项目 | 源码修改 | 仍需验证 |
|---|---|---|
| C01 构建 | 本机路径放进忽略提交的 props；升级 BPC；固定工具包；每次构建先核对游戏引用并 publicize；不再使用仓库旧 DLL | Windows 首次 restore、Build/Rebuild、ILRepack；构建不等于通过 |
| 构建副作用 | Build 不部署、不打包；StageMod、PackageMod、DeployMod 独立显式调用；只将本 Mod DLL、Info 和资源包放进 stage | 安装包清单、实际安装路径；bin 会有本地解析依赖，不能直接打包 bin |
| C02 / H03 初始化 | Load 安装补丁失败返回 false，只清理本 Mod 的补丁；禁用热重载；状态分为未开始/进行中/成功/失败；阶段计时日志和 UMM 界面失败提示；失败后不重试半成品 | Harmony 注册、两个钩子的实际顺序；延迟异常不能回滚已经创建的蓝图，也不会阻止引擎继续保存 |
| H02 BPC 参数 | AddPrerequisiteIsPet 使用命名参数；Spirit Focus 的 FeatureSelectionConfigurator 导入更新到 CustomConfigurators | 普通角色可选、宠物不可选；其他游戏 API 编译差异 |
| C06 Surge | d6/d8/d10 修正上界，20级以上沿用 d10 | 最大值、分布、传奇/多职业行为；每个检定的随机值策略未重做 |
| H08 部分 | 免费 Surge 退款归施法者；零成本 Legendary Marshal 不退款、不消耗免费次数；缺资源对象时不空引用 | 扣费/加惩罚/退款时序和读档后的免费次数仍待修，不能宣称彻底修复 |
| C07 部分 | Trickster's Edge 恢复上游算法和零 ranks 的 +3 补偿；仅保留自身 modifier 幂等清理 | BaseStatBonus 与真实 ranks、职业技能 +3、trained-only、升级刷新必须实测 |
| C08 / H10 | 副 Trickster 骰数改为除法；额外精准伤害从攻击武器获取物理类型，使用单次事件副本；先过滤无武器/无攻击掷骰的伤害 | 主副灵体、多段攻击、物理 DR 与精准免疫；已有 rank 的清理仍未迁移 |
| C10 | Wild Arcana 先判断16级、再13级，恢复六环分支 | 16级转换列表与实际扣费 |
| C11 | Legendary Archmage 的每日资源不再走影响力惩罚分支 | 七至九环施法不误加惩罚；旧版已产生的错误 Buff 尚未迁移 |
| H05 部分 | ApplySpirits 检查缺失主灵体状态；可选力量蓝图判空；清理不创建新 Part；Archmage/Hierophant 缺失 Part 时移除不崩溃 | 缺失状态时仅记录并退出，不能保证已授予效果全部撤销；完整所有权/重建仍待做 |
| C14 | 修复四份 JSON 中损坏的撇号，严格 UTF-8 可读，保留本地化 key | 游戏内显示 |
| 日志 | 通道前缀由 COP 改为 MediumClass | 在 Player.log / UMM 日志中检索 BEGIN/END/FAILED |

## 3. 明确保留的阻塞项

这些不是已经解决的问题，不能用“静态检查通过”覆盖。

1. **C03 TypeId 重复已在源码中消除，但旧档迁移仍待实测**。Archmage 保留旧 ID `18df8977af254951be0e49854a471953`，Hierophant 改用 `0afe477b-82f7-410e-a905-048a80fb3d93`。已挂载的 `MediumContextSpiritBonusComponent` 保留旧 ID `995fb9e0-f2f5-4dc2-a281-b7959ea95cda`；没有任何蓝图引用的 `MergeMediumSpellbookComponent` 改用 `b18c5744-bb1f-4c61-b769-02a71aa96fbb`。第二轮还确认 DecisiveStrikeStandardComponent 与旧游戏 FreeActionSpell 的 ID 重复，现改为 `b61fefa0-ffda-416f-a405-50f5eaa0094a`。Hierophant 和 Marshal/Decisive Strike 旧档都须单独验证。目前静态检查不再有重复 TypeId；这**不证明旧存档中的 Hierophant UnitPart 一定能自动恢复**。其转换列表由带唯一 TypeId 的事实组件在启用时重建这一点需通过旧档副本测试。第一次启动仍需看日志，不能仅凭修复 TypeId 断言加载卡顿已解决。
2. **H04 / H06 读档与升级/洗点**：主灵体恢复已改为通灵 Buff 来源能力中的灵体引用。完整序列化与重建回调顺序仍待验证，缺少来源上下文时只记录日志并保留已有状态。
3. **C04 / C05 / H05 状态与共享祝福**：Part 删除已改为按来源事实和灵体键；共享祝福仍没有持久化受益者及来源，独行漏删已修。下一批应按来源保存 Fact/Entity 引用，处理离队、双 Medium、独行和重复清理，不能简单删所有同蓝图效果。
4. **H07 法术书**：先核实 AddSpellbook/ForbidSpellbook 的计数、CL、法术位与读档表现。合并组件未挂载，不应直接启用；不删除永久已知法术。
5. **C09 Surprise Strike 冷却**：首次命中而非首次攻击、休息而非24小时、不同攻击者共用冷却，仍待改及实测。
6. **C12 Transfer Magic**：已修正为成功添加后才移除来源，完整无害/最高环/随机过滤仍未完成。测试阶段不要在重要角色/剧情对象上使用；后续实现应先筛选合法最高环无害法术并成功复制，再移除来源。
7. **H08 / H09 费用**：Surge 扣费顺序、Arcane Surge 对 AbilityData 费用的作用范围和恢复仍待测。
8. C13 无效设置开关、F01–F05 图标/Homebrew/Spirit Focus/草稿功能与文档完善不包含在已修复清单中。Spirit Focus 仅修导入以适配编译，仍未启用。

## 4. 在游戏电脑上接续

1. 解压源码。安装 Visual Studio 2022 或 Build Tools，包含 MSBuild、.NET 桌面构建工具、.NET Framework 4.7.2 targeting pack 和 SDK。使用 **Developer PowerShell for VS 2022**；此项目的 publicizer 是 net472 MSBuild task，本批未验证 `dotnet build` 路线。
2. 安装/核对目标依赖。备份现有 Medium 安装目录及测试存档。
3. 复制配置：`Copy-Item MediumClass.local.props.example MediumClass.local.props`，编辑实际 WrathPath；需要时分别指定 ManagedPath、UMMPath、ModMenuPath、TTTCorePath。
4. 可选离线检查：`python scripts/check_source.py --strict`。当前应通过且无重复 TypeId；若失败，保存输出并联系维护者。该检查不验证旧档迁移。
5. 构建：`./scripts/Build.ps1`。保留完整输出，先修当前游戏 API 的编译差异。没有游戏依赖时应明确报缺失引用，不会用上游旧 DLL 假装通过。
6. 编译成功后才生成测试安装文件：`./scripts/Build.ps1 -Target StageMod`。输出 `artifacts/Release/MediumClass/`。冷启动测试前停止游戏，手动替换备份后的 Mod 目录；也可以明确执行 `-Target DeployMod`，该操作会复制到配置中的游戏目录。
7. 若需要本机测试 ZIP：`./scripts/Build.ps1 -Target PackageMod`。该目标只是本机打包，不创建 GitHub Release。
8. **先冷启动，不读重要旧档**。若类型注册/蓝图阶段失败，先保存日志定位；新角色通过后才在旧档副本上验证 Hierophant 状态恢复。升级验证始终另存，避免覆盖唯一原档。

只运行 Build 会在本地 bin/obj 写构建产物，不会部署、发布或压缩。StageMod 会重建专用暂存目录，不影响游戏；只分发 stage 内的白名单文件。

## 5. 最小实机测试记录

| 用例 | 记录内容 | 状态 |
|---|---|---|
| 目标程序集编译 | 版本、引用路径、MSBuild/SDK、完整错误/警告 | 待测试 |
| 原版/最小依赖/加 Medium 冷启动 | 首个异常、TypeId 注册、各初始化阶段、主菜单 | 待测试 |
| 新角色选职业 | 普通角色可选，宠物不可选；职业入口不重复 | 待测试 |
| 六灵体逐个 | 通灵、保存、退出进程、读档、休息、重选 | 待测试 |
| Surge 边界 | 1/9/10/19/20级；免费次数、资源3/2/1；普通队友受益 | 待测试 |
| Trickster's Edge | 已恢复上游算法；核实 BaseStatBonus、零 ranks +3、负加值以及 trained-only；暂不指定改写后的预期 | 待测试 |
| Surprise Strike | 主/副 Trickster；20级副灵体6d6；斩/刺/钝武器；非武器伤害；DR和精准免疫 | 待测试 |
| Archmage | 13/16级列表；传奇七至九环只扣每日资源、不加影响力惩罚 | 待测试 |
| 共享祝福 | 独行、队友离队再加入、双 Medium、休息和读档 | 待测试 |
| 法术/生命周期 | 三轮换灵体；升级/洗点完成和取消；15/20级；多职业/神话合书 | 待测试 |
| 旧档迁移 | 0.1.3 原档副本；确认 Hierophant UnitPart/转换列表在读档后重建；另存并二次读取，不刷资源、不重复授予 | 待测试 |

提交问题时附：实际游戏与依赖版本、Medium DLL 哈希、完整 Player.log/UMM 日志、复现步骤和存档副本。没有这些证据，不把卡加载归因到某一个猜测。

## 6. 迁移与个人 GitHub

源码 ZIP 的 `source/` 可独立打开开发，不含游戏 DLL、构建产物、本机路径或凭据。
附带 `changes.patch` 是相对上游基线的 Git 补丁；不要把补丁再次应用到已经更新的 source 目录。

若希望保留上游历史：

```powershell
git clone https://github.com/Telyl/MediumClass.git MediumClass-maintenance
cd MediumClass-maintenance
git switch -c maintenance/wotr-2.7-offline e5fd3ba2e35350a218658abde4e093ff743175b7
git apply --check ../MediumClass-maintenance-2026-09-23/changes.patch
git apply --index ../MediumClass-maintenance-2026-09-23/changes.patch
git commit -m "Prepare WotR 2.7 offline maintenance baseline"
```

在 GitHub 上创建自己的 fork 后，将其添加为 personal remote，并推送维护分支：

```powershell
git remote add personal https://github.com/shenzhoudadi/MediumClass.git
git push -u personal maintenance/wotr-2.7-offline
```

上述推送地址是用户账号下**待创建的目标**，不是声称已经存在或已经上传。当前 GitHub 连接可识别账号，但没有创建仓库/fork 的操作，因此本轮采用用户授权的可迁移文件交付。未对上游提 PR，未发布版本。
