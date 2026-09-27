using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.References;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using MediumClass.Medium.NewComponents.AbilitySpecific;
using MediumClass.Utilities;
using MediumClass.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace MediumClass.Medium.Spirits.Archmage
{
    class SeanceBoon
    {
        private static readonly string FeatName = "ArchmageSeanceBoon";
        private static readonly string DisplayName = "ArchmageSeanceBoon.Name";
        private static readonly string Description = "ArchmageSeanceBoon.Description";
        private static readonly ModLogger Logger = Logging.GetLogger(nameof(SeanceBoon));

        public static void ConfigureEnabled()
        {
            Logger.Log("Generating Archmage Seance Boon");
            var buff = BuffConfigurator.New(FeatName + "Buff", Guids.ArchmageSeanceBoonBuff)
                .SetDisplayName(DisplayName)
                .SetDescription(Description)
                .SetIcon("assets/icons/spiritarchmage.png")
                .AddComponent<ArchmageSeanceDamageBonus>()
                .Configure();

            FeatureConfigurator.New(FeatName, Guids.ArchmageSeanceBoon)
                .SetDisplayName(DisplayName)
                .SetDescription(Description)
                .SetIcon("assets/icons/spiritarchmage.png")
                .AddFacts(new() { buff })
                .Configure();
        }
    }
}
