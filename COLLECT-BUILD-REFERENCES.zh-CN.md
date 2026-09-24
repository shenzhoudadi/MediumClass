# 不用安装开发工具：收集构建所需文件

2026-09-24。目标是由维护者构建好安装包，再让测试者通过 UMM 安装。

目前还没有编译好的维护版。此前约 257 KB 的 ZIP 是源码，不能安装到 UMM。测试日志已确认 UMM 找不到 `MediumClass.dll`，尚未执行本 Mod 的代码。

## 测试者只需做这几步

1. 关闭游戏。将维护者提供的 **MediumClass-Collect-References.zip** 解压到下载目录或桌面的新文件夹中。不要放进 UMM，也不要只在压缩包预览里运行。
2. 双击 **Collect-BuildReferences.cmd**。工具会尝试识别常见 Steam 游戏目录；如弹出窗口，选择包含 `Wrath_Data` 的游戏主目录。窗口会打印它选择的目录，请核对。
3. 等待完成。同一文件夹里会生成 **MediumClass-build-references-日期时间-编号.zip**，把这个新 ZIP 发给维护者。报错时发窗口截图。
4. 收到维护者编译并检查后的安装包，再用 UMM 安装。目前这个收集工具和它生成的 ZIP 都不是安装包。

不需要 Visual Studio、Python、管理员权限或手动填写构建配置。依赖 ModMenu、TabletopTweaks-Core 和 UMM 需要已安装。

## 工具实际做什么

- 只复制游戏 `Wrath_Data/Managed` 第一层的 DLL、UMM 的 DLL，以及 ModMenu、TabletopTweaks-Core 目录第一层的 DLL 和各自 Info.json。
- 生成相对文件清单、文件版本和 SHA-256，便于维护者核对构建引用。
- 只写入临时目录和工具旁的新 ZIP；退出时清理本次临时目录。不会改变游戏、Mod 或存档，也不会联网或自动上传。
- 收集的是程序集，不包含存档、账号资料、游戏资源包或日志。参考 ZIP 可能比之前源码包大很多，属正常情况。
- CMD 只对本次 PowerShell 进程指定脚本执行策略，不修改机器的持久执行策略。如果系统组织策略阻止运行，停止并发截图，不要修改组织策略。

只把生成的参考文件交给维护者用于构建，不要提交到 GitHub 或放进最终安装包。

## 给维护者

源码仓库中入口位于 `scripts/Collect-BuildReferences.cmd`，旁边必须保留同名 PS1。可显式运行 `Collect-BuildReferences.ps1 -GamePath <游戏主目录>` 以选择自定义路径。

解压后的布局可用作本机引用根目录：`Wrath_Data/Managed`、`Wrath_Data/Managed/UnityModManager`、`Mods/ModMenu`、`Mods/TabletopTweaks-Core`。不要将收到的游戏 DLL 作为版本库文件。

收集到引用后仍需安装构建工具、还原 NuGet、解决编译错误、运行合并与打包；本工具不保证当前源码编译通过。最终交付包必须包含编译后的 MediumClass.dll、Info.json、mediumclass_assets，并确认 DLL 已合并 BlueprintCore。游戏兼容性仍由安装后的实机测试验证。
