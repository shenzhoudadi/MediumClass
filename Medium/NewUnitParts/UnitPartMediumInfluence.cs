using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.UnitLogic;
using Newtonsoft.Json;

namespace MediumClass.Medium.NewUnitParts
{
    // A versioned counter, independent of the former remaining-use resource.
    // Presence of Version prevents saved accumulated points being inverted again.
    [TypeId("0b7a8dbe97ac422690a6a2b6020f7e64")]
    public class UnitPartMediumInfluence : UnitPart
    {
        [JsonProperty] public int Version;
        [JsonProperty] public int Accumulated;
    }
}
