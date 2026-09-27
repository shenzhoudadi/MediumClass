# 共鸣累计与神话能力回归检查

此测试直接链接发布版本的 `MediumInfluenceRules`、`InfluenceMath`、`SpiritSurgeMath` 与持久化共鸣部件。
游戏对象是明确的 API 模型；不是启动 Unity 或实际游戏的验证。

测试会先读取指定游戏 DLL，以 Mono.Cecil 验证每个生产补丁的实际目标、参数类型/参数名、重载与字段注入，再在 .NET Framework 4.7.2 测试程序中用指定版本的真实 Harmony 安装全部生产共鸣补丁。

```powershell
dotnet build tests/InfluenceScenarios/InfluenceScenarios.csproj -t:Rebuild -p:HarmonyReferencePath="绝对路径/0Harmony.dll"
& tests/InfluenceScenarios/bin/Debug/net472/InfluenceScenarios.exe "绝对路径/Assembly-CSharp.dll"
```

切换 Harmony 版本时必须 Rebuild，避免旧输出目录保留上一次的运行时 DLL。
本次分别使用游戏 Harmony 2.0.4.0 与构建依赖 Harmony 2.3.6.0，均通过 667 项检查。

覆盖内容：初始 0 点；额外降灵的 1/2/3 点递增代价；达到上限后的可用性；免费奔涌不增加共鸣；累计/上限显示（含奥术奥秘等转化法术快捷栏，零共鸣时保留可施放状态）；神话等级向下取整；神话与坚毅灵魂上限加成叠加；不因提高上限而重置共鸣；星界灯塔资源别名共用计数且授予资源不会清空；慰灵减少共鸣、1 点下限与 3 点惩罚阈值；休息清零；旧档剩余资源转换且只转换一次；英灵目录延迟恢复；多个角色与其他职业资源互不干扰；穷举 2d8 的 64 种骰子组合，确认两次独立 d8、三角分布与传奇统帅/Prowler 的固定骰子优先级。

.NET 9 上旧 Harmony 自身的动态方法生成器不兼容（LocalBuilder / ReflectionHelper 错误），因此该套件使用与模组目标一致的 .NET Framework 主机，不修改游戏的 Harmony 或生产环境设置。

