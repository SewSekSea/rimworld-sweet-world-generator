using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class ScenPart_CavernStart : ScenPart
    {
        public bool reclamation;
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref reclamation, "reclamation", false); }
        public override void PreConfigure() { CavernScenario.Configure(); }
        public override string Summary(Scenario scen) { return reclamation ? "Reclaim an insect-held fertile lake oasis from the surrounding caverns." : "Begin with three supplied pioneers in a fertile underground lake oasis."; }
    }

    internal static class CavernScenario
    {
        internal static ScenPart_CavernStart Part { get { return Find.Scenario?.AllParts.OfType<ScenPart_CavernStart>().FirstOrDefault(); } }
        internal static bool Reclamation { get { return Part?.reclamation == true; } }
        internal static void Configure()
        {
            if (!ModsConfig.AnomalyActive) return;
            WorldPreset preset = BiomeScorePatch.CurrentPreset?.Clone() ?? new WorldPreset();
            preset.name = "Cavern World"; preset.selectedBiomesOnly = true; preset.adaptBiomeClimate = true; preset.hydrophobia = true;
            preset.selectedBiomeNames.Clear(); preset.selectedBiomeNames.Add("UGWG_CavernOasis");
            preset.biomeNames.Clear(); preset.biomeNames.Add("UGWG_CavernOasis");
            preset.biomeWeights.Clear(); preset.biomeWeights.Add(1f);
            WorldPresetRegistry.Apply(preset, -1);
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldGenerator), "GenerateWorld")]
    internal static class CavernScenarioWorldPatch
    {
        [HarmonyPriority(Priority.First)]
        internal static void Prefix() { if (CavernScenario.Part != null) CavernScenario.Configure(); }
    }
}
