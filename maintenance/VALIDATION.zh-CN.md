# 离线验证记录

验证日期：2026-09-23。范围仅为源码、构建配置和迁移文件，不含编译与游戏运行。

| 检查 | 结果 | 能证明的范围 |
|---|---|---|
| `python scripts/check_source.py --strict` | 0 errors，0 known TypeId collisions | TypeId唯一性、GUID/本地化键、JSON/XML、路径/包版本/显式打包约束；不代表旧档兼容 |
| `python scripts/check_source.py --syntax` | 已安装固定依赖并重跑通过，96 份 C# 语法无错误 | 仅为语法解析，不代表 C# 类型检查或游戏 ABI 兼容 |
| UTF-8/JSON | 9份通过 | 修复4份文本损坏；不代表游戏内显示已验收 |
| 标识核对 | 静态检查通过；Blueprint GUID 保持不变；Mod Id/程序集名/入口不变 | 三个重复 TypeId 已替换（含游戏 FreeActionSpell 外部冲突）；旧档恢复需要实机验证 |
| 严格静态门禁 | `python scripts/check_source.py --strict` 通过，0 errors，0 known TypeId collisions | 确认源码无重复 TypeId；不代表旧档迁移或游戏加载通过 |
| `git diff --check e5fd3ba2e35350a218658abde4e093ff743175b7` | 本轮移除手册行末双空格后通过 | 上游至当前工作树没有 diff 空白错误；此前 `73b78ab` 的手册两行会报错 |
| BPC 2.7.5→2.8.7 方法签名检查 | 修正 AddPrerequisiteIsPet 参数及 FeatureSelectionConfigurator 导入；检查其余本项目涉及的签名变更调用 | 对可唯一映射的新旧参数列表，未发现剩余位置实参漂移或已移除命名参数；重载解析、默认行为和引擎 API 仍须编译及实测 |
| publicizer 输出核对 | 对照原作者 PublicizeTask.cs | 确認产生 `_public.dll` 和 `_public.hash`，保留缓存输出并复制为引用名；缺输出时清旧 hash |
| ILRepack 参数核对 | 对照原作者 ILRepack.cs | 2.0.13 task 未暴露 SearchDirectories 参数，依赖从工作目录解析；本地复制引用供解析，stage只输出白名单 |

环境中没有 dotnet、MSBuild、Mono/C#编译器或目标游戏程序集。因此 **restore、Build/Rebuild、ILRepack、PowerShell构建脚本执行、游戏冷启动、读档、职业机制均未验证**。

为了检查旧字段的名称，曾只读查看上游附带旧 Assembly-CSharp.dll 的元数据；这不是目标版本 ABI 检查。交付源码包不包含该游戏 DLL。

## 复现静态检查

```text
python -m pip install -r scripts/requirements-checks.txt
python scripts/check_source.py --syntax
python scripts/check_source.py --strict
```

此次 TypeId 修复后，两个检查都应通过且不再报告重复 ID。若 `--strict` 仍失败，先修检查器或基线差异，不要忽略错误。旧档兼容仍必须在测试副本中单独验证。

## 下一位执行者的任务

先用目标游戏程序集编译并保存完整错误输出；再处理类型注册和第一次冷启动失败；然后执行 HANDOFF 的测试矩阵。不要直接把本源码快照标成“已兼容2.7.0x”，也不要先覆盖重要旧档来验证。

## 第二轮额外验证

见 [第二轮复查](REVIEW-2026-09-23.zh-CN.md)。新增 `--typeid-assembly` 对程序集的 TypeIdAttribute 做精确检查；在上游附带旧 DLL 上复现 FreeActionSpell 冲突后，修复后通过。目标游戏及 TTT 等依赖尚未提供，未对其做此项验证。Trickster’s Edge 数值公式已恢复原始算法；成长表、法术书、Homebrew 未重做。

## 独立报告后的验证范围

本轮重跑 `python scripts/check_source.py --strict --syntax` 与上述上游范围的 diff 检查。修改与未采纳项见 [复核记录](INDEPENDENT-REVIEW-FOLLOWUP.zh-CN.md)。这两处生命周期改动仍未编译或游戏测试。
