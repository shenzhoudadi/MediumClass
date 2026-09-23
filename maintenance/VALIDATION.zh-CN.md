# 离线验证记录

验证日期：2026-09-23。范围仅为源码、构建配置和迁移文件，不含编译与游戏运行。

| 检查 | 结果 | 能证明的范围 |
|---|---|---|
| `python scripts/check_source.py --syntax` | 0 errors，2 known blockers | GUID/TypeId/本地化键保持；JSON/XML可解析；路径/包版本/显式打包约束；C#语法解析 |
| C# tree-sitter 解析 | 96份源码无解析错误 | 不代表 C# 类型检查或游戏 ABI 兼容 |
| UTF-8/JSON | 9份通过 | 修复4份文本损坏；不代表游戏内显示已验收 |
| 原有标识对照 | 236个 Guids.cs GUID值不变；TypeId 值不变；Mod Id/程序集名/入口不变 | 避免本批直接改动旧档引用身份；不保证旧逻辑和旧档能正常恢复 |
| 严格发布门禁 | `--strict` 返回1，按预期拒绝 | 两组继承自上游的 TypeId 冲突确实仍阻止“静态发布通过” |
| `git diff --check` | 通过 | 补丁没有空白错误 |
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

最后一个命令当前应失败。不能删除其警告名单或修改基线来宣称已解决冲突；先完成 HANDOFF 中的迁移设计，并将理由与实际游戏/旧档证据一并记录。

## 下一位执行者的任务

先用目标游戏程序集编译并保存完整错误输出；再处理类型注册和第一次冷启动失败；然后执行 HANDOFF 的测试矩阵。不要直接把本源码快照标成“已兼容2.7.0x”，也不要先覆盖重要旧档来验证。
