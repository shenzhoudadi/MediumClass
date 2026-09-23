# MediumClass 小白实机测试手册

适用对象：帮忙在 Windows 游戏电脑上做第一轮测试的人。  
测试日期：2026-09-23  
维护源码：`maintenance/wotr-2.7-offline`，GitHub 代码提交 `141b2ef65120dadd941a1128f42e2f37869eb56a`（与本地 `6dcc14c` 源码树相同；后续为文档整理，映射见 `maintenance/github-import.json`）

## 先看这几句话

- 这份维护内容**还没有编译，也没有在游戏里运行过**。手里的压缩包是源码，不是可直接安装的 Mod。
- 构建需要 Windows、已安装的《Pathfinder: Wrath of the Righteous》、Visual Studio 2022 Build Tools，以及几个 Mod 的 DLL。只熟悉玩游戏、不方便安装开发工具的话，请先把这份手册交给项目维护者，由其另找能构建的人；不要把压缩包直接放进游戏。
- 这是测试版。源码中的两组内部 TypeId 重复及一组与游戏 FreeActionSpell 的冲突已修复，但 Hierophant 和 Marshal/Decisive Strike 的 ID 变化可能影响旧存档恢复；游戏是否能正常注册和读取旧档仍未验证。测试前备份 Mod 和存档；不要拿唯一存档试。
- 不要在测试时更新或覆盖自己唯一的游戏存档。测试过程创建的新存档也请另存为测试档。

## 需要准备什么

1. Windows 版游戏。先记下游戏版本号；Steam 可以在游戏属性/更新页面查看。请记录完整数字。
2. Unity Mod Manager（UMM）和本 Mod 所需依赖。当前测试目标如下，具体来源和版本记录在源码包的 `maintenance/HANDOFF.zh-CN.md`：
   - UMM 0.33.0
   - ModMenu 1.3.2
   - TabletopTweaks-Core 0.7.14a
   - BlueprintCore 2.8.7（会随本 Mod 构建嵌入，不需要把它当成单独游戏 Mod 安装）
3. Visual Studio 2022 或 Build Tools，安装时勾选 **.NET 桌面构建工具 / MSBuild**，并安装 **.NET Framework 4.7.2 targeting pack**。
4. Git 不必需；Python 也不必需。第一次构建需要联网下载构建依赖包。
5. 从维护者处取得：
   - 维护分支的 GitHub 下载 ZIP，或备用 `MediumClass-maintenance-2026-09-23.zip`（两者都是源码包）
   - 本手册

如果游戏和依赖已经装好，不要为了测试随意升级其他 Mod。先记录现有版本；尽量在可回滚的环境测试。

## 第一步：备份和记下游戏位置

1. 完全退出游戏。
2. 在文件资源管理器中找到游戏安装目录。Steam 用户可以在 Steam 库里右键游戏，选择“管理”→“浏览本地文件”。打开后应能看到 `Wrath_Data` 文件夹；这个目录就是后面要填写的游戏路径。
3. 记下这个路径，例如：

   `D:\SteamLibrary\steamapps\common\Pathfinder Wrath of the Righteous`

   这是示例，请换成你电脑上实际显示的路径。
4. 在游戏目录的 `Mods` 文件夹里，找到现有的 `MediumClass` 文件夹（如果有），复制到别处备份。**不要把备份留在 `Mods` 文件夹内**，否则 UMM 可能同时读到新旧两份。
5. 复制一份要测试的存档到其他位置。测试期间只使用副本。

## 第二步：取得并解压源码

1. 打开 [个人仓库维护分支](https://github.com/shenzhoudadi/MediumClass/tree/maintenance/wotr-2.7-offline)。确认页面左上角显示 `maintenance/wotr-2.7-offline`，不要下载默认的 master。
2. 点击绿色 **Code** → **Download ZIP**，解压到例如 `C:\ModTest\MediumClass`。也可使用维护者提供的备用 ZIP。
3. 找到含 `MediumClass.csproj`、`MediumClass.local.props.example`、`scripts` 和 `maintenance` 的文件夹，下文称为“源码根目录”。GitHub ZIP 的项目文件就在解压后的项目目录内；备用 ZIP 则在 `source` 子目录内。
4. 后续命令均在源码根目录运行，不要把源码解压到游戏目录或 `Program Files`。

## 第三步：告诉构建工具游戏装在哪里

1. 在 Windows 开始菜单找到并打开“Developer PowerShell for VS 2022”。输入 `cd` 加上 源码根目录路径，例如：

   ```powershell
   cd "C:\ModTest\MediumClass"
   ```

2. 复制配置示例，输入：

   ```powershell
   Copy-Item .\MediumClass.local.props.example .\MediumClass.local.props
   notepad .\MediumClass.local.props
   ```

3. 记事本会打开配置文件。把 `<WrathPath>...</WrathPath>` 中间的示例路径，替换成第一步记下的**游戏主目录**。例如：

   ```xml
   <WrathPath>D:\SteamLibrary\steamapps\common\Pathfinder Wrath of the Righteous</WrathPath>
   ```

   不要把 `Wrath_Data` 写进路径末尾；不要删除两边的 XML 标签。保存并关闭记事本。

4. 如果游戏装在非默认位置，或依赖 Mod 装在非默认 `Mods` 目录，先不要猜路径。请把目录截图发给维护者，让其确认是否需要填写额外路径。

## 第四步：构建并生成测试文件

1. 在 **Developer PowerShell for VS 2022** 中，确认当前路径是源码根目录。普通 PowerShell 可能找不到 MSBuild。
2. 输入：

   ```powershell
   .\scripts\Build.ps1 -Target StageMod
   ```

3. 第一次运行会还原构建依赖，可能需要一些时间。窗口里出现红色错误、`Build failed`、缺 DLL 或要求配置路径时，先停下来，复制整个窗口内容；不要去网上随便下载 DLL 放进项目。
4. 如果成功，测试文件会出现在：

   源码根目录下的 `artifacts\Release\MediumClass`

   里面应有 `MediumClass.dll`、`Info.json` 和 `mediumclass_assets`（它是单个资源文件，不是文件夹）。这才是要放进游戏的测试文件。
5. 如果输出目录不存在或没有 `MediumClass.dll`，说明构建没成功，不要继续安装。

## 第五步：安装到测试环境

1. 确认游戏已退出。
2. 在游戏目录打开 `Mods` 文件夹。若不存在，可以确认 UMM 是否已正确安装；不要自己猜一个不同位置。
3. 把刚才的整个 `MediumClass` 暂存文件夹复制到游戏的 `Mods` 文件夹。最终目录看起来应类似：

   `游戏目录\Mods\MediumClass\Info.json`

   `游戏目录\Mods\MediumClass\MediumClass.dll`

   `游戏目录\Mods\MediumClass\mediumclass_assets`

4. 不要复制源码的 `bin` 文件夹，也不要复制整个 `artifacts` 文件夹。
5. 打开 UMM，确认 MediumClass 出现在 Mod 列表中，并启用它。确认 UMM、ModMenu、TabletopTweaks-Core 也已启用。
6. 如果 UMM 提示缺少依赖或版本不符，先截图记录，不要通过删除依赖检查、改版本号等方式强行启动。

## 第六步：按风险从低到高测试

### A. 冷启动

1. 启动游戏，观察是否能到主菜单。
2. 如果卡在加载界面、崩溃、返回桌面，先记下发生时间和画面，等一会儿让日志写完，再退出游戏。不要连续反复启动，也不要加载旧存档来“试试看”。
3. 启动失败就停止后续测试，按后文收集日志并联系维护者。

### B. 新建测试角色

主菜单能正常打开后，再新建一个测试角色：

1. 查看 Medium 是否能正常选择；如果选项缺失、重复或点选报错，截图记录。
2. 进入游戏后检查职业和灵体相关界面是否报错。
3. 做一次测试存档，退出到桌面，再重新启动并读取这个**测试存档**。
4. 检查灵体是否还在、效果是否重复、是否出现明显错误；尝试休息和切换灵体，再保存并重新读取。
5. 如果你不熟悉高等级或战斗机制，不必自行猜测数值是否正确，把角色等级、灵体、能力名称和现象记下来即可。

### C. 旧存档

只有 A、B 都完成并且没有明显错误后，才考虑加载旧档副本。第一次不要覆盖旧档；读取后另存一个新测试档。若加载卡住或角色状态异常，立即停止，不要反复保存覆盖。

**特别提醒：** 源码层面的重复 TypeId 已消除；Hierophant UnitPart 更换了 TypeId，另外 Marshal 的 Decisive Strike 组件也更换了与游戏冲突的 TypeId。旧存档中的相关状态可能需要重新建立。此恢复行为没有实机验证。旧档测试出现问题时不要继续推进主线；保留原档和错误日志。

## 本轮新增的重点观察

- 单人独行时，休息后共享祝福能否正常撤销。
- 通灵、保存、完全退出后读取，灵体选择是否保留；切换后加值/惩罚是否残留。
- 如果已有临时测试角色，再观察 Arcane Surge 结束后费用是否恢复。不要为测试在重要存档上修改等级或加能力。
- 旧档只用副本；拥有 Hierophant 或 Marshal/Decisive Strike 状态的档案应分别测试。
- Trickster’s Edge 已恢复原 Mod 算法；不要按此前手册或交接中重写公式的预期数值判定通过。

## 出问题时怎么恢复

1. 关闭游戏。
2. 到 `游戏目录\Mods`，把本次测试的整个 `MediumClass` 文件夹移到游戏 `Mods` 文件夹之外。不要只在 `Mods` 内改名，UMM 仍可能扫描到它。
3. 如之前备份过旧版 Mod，把旧版文件夹恢复原位。
4. 再启动游戏确认能否恢复正常。不要删除或覆盖存档；先不要把疑似损坏的测试存档载入正式游戏。

## 请把这些信息发回给维护者

最好把以下内容放在一个文件夹或 ZIP 里：

- 游戏完整版本号、游戏平台（Steam / GOG 等）和游戏安装路径大致位置（可以遮掉个人用户名）。
- Windows 版本、Visual Studio/Build Tools 版本。
- UMM、ModMenu、TabletopTweaks-Core 版本；UMM Mod 列表截图也有帮助。
- 完整构建窗口输出。构建成功时也请说明成功，并记录 `MediumClass.dll` 的文件大小和修改时间。
- 具体测试步骤：做了什么、预期看到什么、实际看到什么、在哪一步出错。
- 游戏卡住或报错时的截图，以及错误发生的大致时间。
- `Player.log`：在文件资源管理器地址栏粘贴下面路径并回车，复制 `Player.log`。请在复现后尽快复制，因为下一次启动可能覆盖它：

  `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Wrath of the Righteous\Player.log`

- UMM 日志：在 UMM 界面查看/导出日志；如果找不到，拍下 UMM 错误提示和 Mod 列表即可，不要因此卡住测试。
- 若需要存档协助，先问维护者如何安全打包测试存档；不要公开分享带个人信息或唯一进度的存档。

### 复制填写这个简短报告

```text
游戏版本 / 平台：
UMM 版本：
ModMenu 版本：
TTT-Core 版本：
构建结果（成功/失败）：
测试阶段（启动/新角色/读档/休息/灵体切换/旧档副本）：
操作步骤：
预期结果：
实际结果：
错误发生时间：
附件（构建输出、日志、截图）：
```

## 给测试者的结论标准

- “能到主菜单”只代表这次冷启动通过，**不代表职业功能、读档或旧档兼容已经通过**。
- “构建成功”只代表源码在这台机器的引用环境下编译通过，**不代表游戏中能运行**。
- 只有按步骤测过并记录了对应结果，才能把相应项目标记为“实机通过”。
