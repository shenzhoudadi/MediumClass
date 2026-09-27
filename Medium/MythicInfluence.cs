using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using MediumClass.Utilities;

namespace MediumClass.Medium
{
    internal static class MythicInfluence
    {
        internal const string FeatureGuid = "ae859d6d-ffb3-4ba4-93c7-f6458f02db1e";
        internal static void Configure()
        {
            // Let BlueprintCore register the MythicAbility group once in every
            // matching selection. Do not append the same feature a second time.
            FeatureConfigurator.New("MythicExpandedInfluence", FeatureGuid)
                .SetDisplayName("MythicExpandedInfluence.Name")
                .SetDescription("MythicExpandedInfluence.Description")
                .SetIcon("assets/icons/spiritsurge.png")
                .AddToGroups(FeatureGroup.MythicAbility)
                .AddPrerequisiteFeature(Guids.MediumChannelSpirit, group: Prerequisite.GroupType.Any)
                .AddPrerequisiteFeature(Guids.ProwlerSpirit, group: Prerequisite.GroupType.Any)
                .Configure();
        }
    }
}
