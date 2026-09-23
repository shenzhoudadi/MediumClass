# WrathMods-Medium Class

> **2026-09-23 离线维护分支：尚未编译或游戏测试，不是稳定发布。**
> 面向 WotR 2.7.0x / UMM 0.33.0 / ModMenu 1.3.2 / TTT-Core 0.7.14a / BlueprintCore 2.8.7。
> 构建、迁移、已修复内容及剩余阻塞项见 [中文交接说明](maintenance/HANDOFF.zh-CN.md)。
> 已消除源码中的重复 TypeId；Hierophant/Marshal 旧档状态恢复仍待测试。普通 Build 不会部署到游戏。

> 原 Mod 内容保留原则和最新修复/未修事项见 [第二轮复查](maintenance/REVIEW-2026-09-23.zh-CN.md)。

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
ModMenu 1.3.2 - https://github.com/WittleWolfie/ModMenu
TabletopTweaks-Core 0.7.14a - https://github.com/Vek17/TabletopTweaks-Core
```

Note: Creative liberty has been taken with some of the feats as a direct 1:1 translation was not feasible.

Special thanks to Holic75, WittleWolfie, and Vek17. Referenced their work.
Created with https://github.com/WittleWolfie/WW-Blueprint-Core
