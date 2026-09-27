using HarmonyLib;
using Kingmaker.Blueprints.JsonSystem;
using MediumClass.Utils;
using System;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 绝境巡行者的构建入口。
    ///
    /// 第一阶段 <see cref="ConfigureBlueprints"/> 由 Main 的蓝图初始化管线调用（紧随通灵者本体之后），
    /// 只创建蓝图、不发布变体；第二阶段在 BlueprintsCache.Init 之后、且排在 TTT Base 之后执行，
    /// 负责依赖命运血脉的发布工作。
    /// </summary>
    internal static class ProwlerBuild
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(ProwlerBuild));
        private static bool configured;

        internal static void ConfigureBlueprints()
        {
            if (configured) { return; }
            configured = true;
            Logger.Log("BEGIN Prowler blueprints");
            ProwlerSpiritScaling.Configure();
            ProwlerArchetypeBuilder.Configure();
            Logger.Log("END Prowler blueprints");
        }

        [HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
        internal static class ProwlerLateInit
        {
            private static bool started;

            [HarmonyPostfix, HarmonyPriority(Priority.Last), HarmonyAfter("TabletopTweaks-Base")]
            private static void Postfix()
            {
                if (started) { return; }
                started = true;
                try
                {
                    ProwlerArchetypeBuilder.Publish();
                }
                catch (Exception e)
                {
                    Logger.LogException("Failed to publish the Prowler archetype.", e);
                }
            }
        }
    }
}
