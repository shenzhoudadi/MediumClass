# WrathMods-Medium Class

## 先看这里：需要的版本和更新地址

更新日期：2026-09-24。下表是**本维护包的目标和要求**，尚未完成编译和实机验证。

### 玩游戏的电脑

| 文件 / Mod | 本包要求的版本 | 需要做什么 | 官方下载 / 更新地址 |
| --- | --- | --- | --- |
| Pathfinder: Wrath of the Righteous | **PC 2.7.0x** | 在购买游戏的平台更新；Steam 用户在库中更新游戏 | [Steam 游戏页](https://store.steampowered.com/app/1184370/) |
| Unity Mod Manager（UMM） | **0.33.0** | 游戏内实际加载的 UMM 需达到此版本；如果仍是 0.32.4，更新到 0.33.0 | [Nexus 作者文件页](https://www.nexusmods.com/site/mods/21?tab=files) · [作者提供的 Dropbox 下载](https://www.dropbox.com/s/wz8x8e4onjdfdbm/UnityModManager.zip?dl=1) |
| ModMenu | **2.0.14** | 2.0.13 需要更新；用 UMM 安装 ModMenu 的 ZIP | [作者发布页](https://github.com/CasDragon/ModMenu/releases/tag/2.0.14) · [直接下载 ModMenu-2.0.14.zip](https://github.com/CasDragon/ModMenu/releases/download/2.0.14/ModMenu-2.0.14.zip) |
| TabletopTweaks-Core | **0.7.14a** | 已是这个版本就保留；不是则用 UMM 更新 | [作者发布页（标签 v0.7.14，发行版 0.7.14a）](https://github.com/Vek17/TabletopTweaks-Core/releases/tag/v0.7.14) · [直接下载 ZIP](https://github.com/Vek17/TabletopTweaks-Core/releases/download/v0.7.14/TabletopTweaks-Core.zip) |
| BlueprintCore / WW-Blueprint-Core | **2.8.7** | **玩家不用下载或安装**；构建时自动获取并合并进 MediumClass.dll | [NuGet 版本页（供维护者核对）](https://www.nuget.org/packages/WW-Blueprint-Core/2.8.7) |
| Medium Class | **0.1.4 维护测试版** | 等 DLL 构建成功后，使用生成的 `MediumClass-UMM-install-untested.zip` 安装 | 本分支源码 ZIP 和私人构建包都不能直接导入 UMM |

**UMM 只下载新版安装器还不够：** 打开安装器，选择 WotR 和游戏根目录（包含 `Wrath_Data` 的那一级），执行安装或更新，再进游戏按 `Ctrl+F10` 核对版本。这里的 0.33.0 是当前 `Info.json` 声明的要求；不代表已经证明 0.32.4 必然无法运行。Dropbox 是[作者 README 提供的镜像](https://github.com/newman55/unity-mod-manager)，镜像里的具体版本未独立核验，下载后请核对版本。

### 只帮忙创建 DLL 的电脑

这台电脑需要可联网的 **64 位 Windows**；无需安装游戏或 UMM。已收到私人构建包的帮忙者，使用包内已有参考 DLL，**无需重新收集或上传**。

| 构建工具 | 版本 / 安装时勾选什么 | 官方下载地址 |
| --- | --- | --- |
| Visual Studio / Build Tools | **2022（MSBuild 17.x）**；已有则不必重装 | [Build Tools 2022 安装程序](https://aka.ms/vs/17/release/vs_BuildTools.exe) |
| .NET SDK | 在上面的安装器中勾选 **“.NET 桌面生成工具”**（完整 VS 中为“.NET 桌面开发”），保留推荐的 .NET SDK、MSBuild 组件 | 使用上面的 Build Tools 2022 安装器一起安装 |
| .NET Framework 目标包 | **4.7.2 targeting pack**；在“单个组件”中勾选，或安装右侧 **Developer Pack**。仅装 Runtime 不够编译 | [微软 4.7.2 下载页，选 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472) |

私人构建包：解压后双击 `01-双击尝试构建.cmd`，结束后回传新生成的 `回传给维护者.zip`。脚本自动获取 ModMenu 2.0.14 和 BlueprintCore 2.8.7。仅为玩游戏，不需要安装上述构建工具；也不需要为本次构建更新 Spellbook Merge、Isekai 等其他 Mod。

## 安装与维护说明

**普通玩家：先打开 [00-先看这里.txt](00-先看这里.txt) 或 [中文安装与测试说明](01-安装与测试说明.md)。** 电脑上可双击 TXT 用记事本阅读。

当前没有编译好的维护版安装包。普通玩家先按上表准备前置，等待构建产物；不用自己配置开发环境。已提供参考文件的本轮测试者不必重复收集。源码 ZIP 不可直接导入 UMM。

> **2026-09-23 离线维护分支：尚未编译或游戏测试，不是稳定发布。**
> 面向 WotR 2.7.0x / UMM 0.33.0 / ModMenu 2.0.14 / TTT-Core 0.7.14a / BlueprintCore 2.8.7。
> 构建、迁移、已修复内容及剩余阻塞项见 [中文交接说明](maintenance/HANDOFF.zh-CN.md)。
> 已消除源码中的重复 TypeId；Hierophant/Marshal 旧档状态恢复仍待测试。普通 Build 不会部署到游戏。

> 原 Mod 内容保留原则和最新修复/未修事项见 [第二轮复查](maintenance/REVIEW-2026-09-23.zh-CN.md)。

> 2026-09-23 后续：通灵动作顺序与读档保留状态已追加修正；UMM 0.33.0 经作者页面重查仍保留。详见 [独立报告复核与后续修正](maintenance/INDEPENDENT-REVIEW-FOLLOWUP.zh-CN.md)。

> 再次自查：ModMenu 已移交 CasDragon，目标更正为 2.0.14；新增角色状态、重复启用和打包修正。详见 [最新自查记录](maintenance/SELF-REVIEW-2026-09-23.zh-CN.md)。这些修改仍未编译、未实机验证。

> **2026-09-24 测试反馈：** 当前源码包没有 MediumClass.dll，直接导入 UMM 会报缺文件。普通测试者可使用 [双击收集工具](COLLECT-BUILD-REFERENCES.zh-CN.md) 提供构建引用，由维护者编译安装包。

> **玩家说明更新：** 新增中文 TXT 手册、双击入口和反馈模板；本轮未改职业代码。交付问题与剩余风险见 [2026-09-24 复查记录](maintenance/REVIEW-2026-09-24.zh-CN.md)。

## 交接与复查入口

请使用 `maintenance/wotr-2.7-offline` 分支；默认 master 保留上游状态。

- [给其他 Agent 的独立复查 Prompt](REVIEW-PROMPT.zh-CN.md)
- [新 Agent 交接说明](AGENT-HANDOFF.zh-CN.md)
- [普通玩家安装与测试说明](01-安装与测试说明.md)
- [维护者编译与交付](90-维护者编译说明.md)
- [本地/GitHub 提交对应关系](maintenance/github-import.json)

维护分支当前树不含旧游戏 DLL/hash。由维护者使用目标游戏引用构建安装包，普通测试者提供参考文件后等待成品包。

## Maintenance build

Copy `MediumClass.local.props.example` to `MediumClass.local.props`, set `WrathPath`, and run
`./scripts/Build.ps1` in Developer PowerShell for Visual Studio 2022 with the .NET Framework 4.7.2
targeting pack installed. `StageMod`, `PackageMod`, and `DeployMod` are explicit opt-in targets.
Do not package the entire `bin` directory; it contains local reference assemblies.

Offline invariants: `python scripts/check_source.py`. Optional C# syntax parsing:
`python -m pip install -r scripts/requirements-checks.txt`, then `python scripts/check_source.py --syntax`.
`--strict` checks that TypeIds are unique. Passing it does not prove old-save migration or game compatibility;
test save copies in game. No offline check substitutes for compiling against the installed game.

The original feature notes below describe implementation intent, not verified compatibility.

Additional Occult Class (Medium) for Pathfinder: Wrath of the Righteous
https://www.d20pfsrd.com/alternative-rule-systems/occult-adventures/occult-classes/medium/

## Still Needs Implementing
* Some Icons
* Legendary Champion (Homebrew) until I can get a Martial Flexibility feat.

## Spirit Implementation
If not mentioned, it is basically a 1:1 translation from Tabletop.
* Archmage
	* COMPLETE
* Champion
	* Lesser Spirit Power((Homebrew) - Adds all exotic weapons instead of allowing a choice.
	* Legendary Champion ((Homebrew) - Adds weapon and armor training feats.
* Guardian
	* Legendary Guardian (Homebrew) - Adds Paladin Smite
	* Greater Spirit Power(Homebrew) - Adds Azata ability of Charisma to HP on character death.
* Hierophant
	* COMPLETE
* Marshal
	* COMPLETE
* Trickster
	* COMPLETE

Requires the following mods to function:
```
Unity Mod Manager 0.33.0 - https://www.nexusmods.com/site/mods/21
ModMenu 2.0.14 - https://github.com/CasDragon/ModMenu/releases/tag/2.0.14
TabletopTweaks-Core 0.7.14a - https://github.com/Vek17/TabletopTweaks-Core
```

Note: Creative liberty has been taken with some of the feats as a direct 1:1 translation was not feasible.

Special thanks to Holic75, WittleWolfie, and Vek17. Referenced their work.
Created with https://github.com/WittleWolfie/WW-Blueprint-Core
