# 通灵者英灵施法：离线回归检查

此项目直接链接生产文件 `Medium/SpiritSpellcastingMath.cs`，使用独立预期值检查两本英灵源表的 0—20 级进度、低级开环、魅力奖励、属性门槛、额外次数及六环上限。

从源码根目录运行：

```powershell
dotnet run --project tests/SpiritSpellcasting/SpiritSpellcasting.Tests.csproj -- 'D:/steam/steamapps/common/Pathfinder Second Adventure/Wrath_Data/Managed/Assembly-CSharp.dll' 'bin/Release/net472/MediumClass.dll'
```

需要 .NET 9 SDK。两个参数均可省略；这样只执行纯逻辑与源表检查。第一个参数为游戏程序集，使用 Mono.Cecil 只读核对原生补丁目标的签名，以及两个界面入口绕过每日次数函数的事实。第二个参数为已编译的 Mod 程序集，检查每日次数补丁是否接入经过验证的计算函数、进度是否直接使用职业等级、界面补丁是否指向正确方法，以及读档和升级的同步路径是否避免调用休息恢复次数。

这些是静态接线检查：会跟踪 Mod 自己的方法调用，不执行 Harmony 补丁，也不模拟原生引擎副作用，不加载或启动游戏。

通过本检查不代表完成 Unity 内的降灵、施法扣次、休息、升级或存档往返测试。
