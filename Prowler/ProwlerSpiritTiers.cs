using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Utils;
using Kingmaker.Blueprints.Classes;
using MediumClass.Utilities;
using MediumClass.Utils;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Prowler
{
    /// <summary>
    /// 中等（8 级）／高等（16 级）／至高（20 级）英灵之力。
    ///
    /// 三档都只是把通灵者的 SpiritPower 再升一档：ApplySpirits 读取 SpiritPower 的档位，
    /// 依次解锁次级／中等／高等／至高英灵之力；因此至高英灵之力就落在 20 级。
    /// 通灵者本体仍是 1/6/11/17，本变体为 1/8/16/20。
    /// </summary>
    internal static class ProwlerSpiritTiers
    {
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(ProwlerSpiritTiers));

        internal static void Configure(out BlueprintFeature intermediate, out BlueprintFeature greater, out BlueprintFeature supreme)
        {
            Logger.Log("Generating Prowler spirit power tiers.");
            intermediate = Tier("ProwlerIntermediate", Guids.ProwlerIntermediate);
            greater = Tier("ProwlerGreater", Guids.ProwlerGreater);
            supreme = Tier("ProwlerSupreme", Guids.ProwlerSupreme);
        }

        private static BlueprintFeature Tier(string name, string guid)
        {
            return FeatureConfigurator.New(name, guid)
                .SetDisplayName(name + ".Name")
                .SetDescription(name + ".Description")
                .SetIsClassFeature(true)
                .SetReapplyOnLevelUp(false)
                .AddFacts(new() { BlueprintTool.Get<BlueprintFeature>(Guids.SpiritPower) })
                .Configure();
        }
    }
}
