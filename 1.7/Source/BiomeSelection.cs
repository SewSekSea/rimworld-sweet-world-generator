using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    internal static class BiomeSelection
    {
        private static WorldPreset generationPreset;
        private static List<BiomeDef> generationBiomes;
        private static readonly float[] Temperatures = { -60f, -40f, -25f, -15f, -5f, 0f, 5f, 15f, 25f, 35f, 45f, 60f };
        private static readonly float[] Rainfalls = { 0f, 100f, 300f, 599f, 600f, 1000f, 1600f, 2400f, 4000f };

        internal static float Weight(WorldPreset preset, BiomeDef biome)
        {
            int index = preset.biomeNames.IndexOf(biome.defName);
            return index >= 0 && index < preset.biomeWeights.Count ? preset.biomeWeights[index] : BiomeCatalog.DefaultWeight(biome);
        }

        internal static bool Eligible(BiomeDef biome)
        {
            PlanetLayerDef surface = PlanetLayerDefOf.Surface;
            return BiomeCatalog.Editable(biome) && biome.canBuildBase && biome.canExitMap
                && (biome.layerWhitelist.NullOrEmpty() || biome.layerWhitelist.Contains(surface))
                && (biome.layerBlacklist.NullOrEmpty() || !biome.layerBlacklist.Contains(surface))
                && (!surface.onlyAllowWhitelistedBiomes || !biome.layerWhitelist.NullOrEmpty() && biome.layerWhitelist.Contains(surface));
        }

        internal static string Validate(WorldPreset preset)
        {
            if (preset == null || !preset.selectedBiomesOnly) return null;
            if (preset.selectedBiomeNames == null || preset.selectedBiomeNames.Count == 0) return "Select at least one land biome before applying this strict world recipe.";
            foreach (string name in preset.selectedBiomeNames.Distinct())
            {
                BiomeDef biome = DefDatabase<BiomeDef>.GetNamedSilentFail(name);
                if (biome == null) return "Selected biome '" + name + "' is missing. Enable its required mod or change the selection.";
                if (!Eligible(biome))
                    return "'" + biome.LabelCap + "' is not an eligible settleable surface land biome. Oceans and original special maps cannot be selected here.";
                float weight = Weight(preset, biome);
                if (Single.IsNaN(weight) || Single.IsInfinity(weight) || weight <= 0f) return "Selected biome '" + biome.LabelCap + "' needs a positive frequency weight.";
            }
            return null;
        }

        internal static void Begin()
        {
            End();
            WorldPreset current = BiomeScorePatch.CurrentPreset;
            string error = Validate(current);
            if (error != null) throw new InvalidOperationException(error);
            if (current == null || !current.selectedBiomesOnly) return;
            generationPreset = current.Clone();
            generationBiomes = generationPreset.selectedBiomeNames.Distinct().Select(n => DefDatabase<BiomeDef>.GetNamed(n)).ToList();
        }

        internal static void End() { generationPreset = null; generationBiomes = null; }

        private static BiomeDef Best(Tile tile, PlanetTile index, PlanetLayer layer, List<BiomeDef> choices)
        {
            BiomeDef best = null;
            float highest = 0f;
            foreach (BiomeDef biome in choices)
            {
                if (!biome.Worker.CanPlaceOnLayer(biome, layer)) continue;
                Rand.PushState(index.tileId ^ GenText.StableStringHash(biome.defName));
                float score;
                try { score = biome.Worker.GetScore(biome, tile, index); }
                finally { Rand.PopState(); }
                if (!Single.IsNaN(score) && !Single.IsInfinity(score) && score > highest) { best = biome; highest = score; }
            }
            return best;
        }

        internal static BiomeDef Choose(Tile tile, PlanetTile index, PlanetLayer layer)
        {
            WorldPreset preset = generationPreset ?? BiomeScorePatch.CurrentPreset;
            if (preset == null || !preset.selectedBiomesOnly || layer.Def != PlanetLayerDefOf.Surface || tile.WaterCovered) return null;
            List<BiomeDef> choices = generationBiomes ?? preset.selectedBiomeNames.Distinct().Select(n => DefDatabase<BiomeDef>.GetNamed(n)).ToList();
            BiomeDef chosen = Best(tile, index, layer, choices);
            if (chosen != null) return chosen;
            float originalTemperature = tile.temperature, originalRainfall = tile.rainfall;
            float bestTemperature = originalTemperature, bestRainfall = originalRainfall, distance = Single.PositiveInfinity;
            if (preset.adaptBiomeClimate)
            {
                try
                {
                    foreach (float temperature in Temperatures.Concat(new[] { originalTemperature }))
                    foreach (float rainfall in Rainfalls.Concat(new[] { originalRainfall }))
                    {
                        float delta = Mathf.Abs(temperature - originalTemperature) / 20f + Mathf.Abs(rainfall - originalRainfall) / 1000f;
                        if (delta >= distance) continue;
                        tile.temperature = temperature; tile.rainfall = rainfall;
                        BiomeDef candidate = Best(tile, index, layer, choices);
                        if (candidate == null) continue;
                        chosen = candidate; distance = delta; bestTemperature = temperature; bestRainfall = rainfall;
                    }
                }
                finally { tile.temperature = originalTemperature; tile.rainfall = originalRainfall; }
                if (chosen != null) { tile.temperature = bestTemperature; tile.rainfall = bestRainfall; return chosen; }
            }
            throw new InvalidOperationException("Strict biome selection cannot place any selected biome on land tile " + index.tileId + ". " + (preset.adaptBiomeClimate ? "Climate adaptation could not satisfy its biome workers. " : "Climate adaptation is off. ") + "Choose a broader biome set or change the recipe. No unselected land biome was substituted.");
        }
    }

    [HarmonyPatch(typeof(RimWorld.Planet.WorldGenerator), "GenerateWorld")]
    internal static class WorldRecipeGenerationPatch
    {
        internal static void Prefix() { BiomeSelection.Begin(); }
        internal static Exception Finalizer(Exception __exception) { BiomeSelection.End(); return __exception; }
    }

    [HarmonyPatch(typeof(WorldGenStep_Terrain), "BiomeFrom")]
    internal static class StrictLandBiomePatch
    {
        internal static bool Prefix(Tile __0, PlanetTile __1, PlanetLayer __2, ref BiomeDef __result)
        {
            BiomeDef selected = BiomeSelection.Choose(__0, __1, __2);
            if (selected == null) return true;
            __result = selected;
            return false;
        }
    }

    internal static class WorldRecipes
    {
        private static WorldPreset Make(string name, string seed, params string[] biomes)
        {
            return new WorldPreset { builtInRecipe = true, name = name, seed = seed, selectedBiomesOnly = true, adaptBiomeClimate = true, hydrophobia = true,
                selectedBiomeNames = biomes.ToList(), biomeNames = biomes.ToList(), biomeWeights = biomes.Select(b => 1f).ToList() };
        }

        internal static IEnumerable<WorldPreset> Available()
        {
            yield return Make("Temperate World", "Sweet-Temperate", "TemperateForest");
            yield return Make("Desert World", "Sweet-Desert", "Desert");
            if (DefDatabase<BiomeDef>.GetNamedSilentFail("UGWG_CavernOasis") != null) yield return Make("Cavern World", "Sweet-Caverns", "UGWG_CavernOasis");
            string[] alpha = BiomeCatalog.Natural("", "").Where(b => BiomeSelection.Eligible(b) && String.Equals(b.modContentPack?.PackageIdPlayerFacing, "sarg.alphabiomes", StringComparison.OrdinalIgnoreCase)).Select(b => b.defName).ToArray();
            if (alpha.Length > 0) yield return Make("Alpha Biomes World", "Sweet-Alpha", alpha);
            if (DefDatabase<BiomeDef>.GetNamedSilentFail("UGWG_SurfaceLabyrinth") != null && DefDatabase<BiomeDef>.GetNamedSilentFail("UGWG_SurfaceMetalHell") != null)
            {
                yield return Make("Labyrinth Planet", "Sweet-Labyrinth", "UGWG_SurfaceLabyrinth");
                yield return Make("Metal Desolation", "Sweet-Metal", "UGWG_SurfaceMetalHell");
                yield return Make("Mixed Anomaly Surface", "Sweet-Anomaly-Mix", "UGWG_SurfaceLabyrinth", "UGWG_SurfaceMetalHell");
            }
        }
    }

    internal sealed class Dialog_BiomeSelection : Window
    {
        private readonly PresetEditor editor;
        private Vector2 scroll;
        private string query = "";
        private string provider = "";
        private WorldPreset Draft { get { return editor.Draft; } }
        public override Vector2 InitialSize { get { return new Vector2(Mathf.Min(1000f, UI.screenWidth - 40f), Mathf.Min(820f, UI.screenHeight - 40f)); } }
        internal Dialog_BiomeSelection(PresetEditor editor) { this.editor = editor; doCloseX = true; closeOnAccept = false; closeOnCancel = true; absorbInputAroundWindow = true; }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width - 32f, 34f), "Selected biomes only");
            Text.Font = GameFont.Small;
            Widgets.CheckboxLabeled(new Rect(0f, 42f, rect.width, 30f), "Strict mode: only checked land biomes may generate", ref Draft.selectedBiomesOnly);
            Widgets.CheckboxLabeled(new Rect(0f, 78f, rect.width, 30f), "Adapt land temperature and rainfall when needed for selected biomes", ref Draft.adaptBiomeClimate);
            Widgets.Label(new Rect(0f, 116f, rect.width, 48f), "Oceans and lakes keep native generation. Adaptation changes land climate, not mountains or water. If no selected biome can generate, the recipe fails clearly instead of substituting another biome.");
            query = Widgets.TextField(new Rect(0f, 174f, rect.width * 0.56f, 30f), query);
            TooltipHandler.TipRegion(new Rect(0f, 174f, rect.width * 0.56f, 30f), "Search by biome name, definition name or provider.");
            if (Widgets.ButtonText(new Rect(rect.width * 0.58f, 174f, rect.width * 0.42f, 30f), String.IsNullOrEmpty(provider) ? "All providers" : provider))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("All providers", delegate { provider = ""; scroll = Vector2.zero; }) };
                options.AddRange(BiomeCatalog.Providers().Select(p => new FloatMenuOption(p, delegate { provider = p; scroll = Vector2.zero; })));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            var biomes = BiomeCatalog.Natural(query, provider).Where(BiomeSelection.Eligible).ToList();
            Widgets.Label(new Rect(0f, 214f, rect.width - 180f, 30f), Draft.selectedBiomeNames.Count + " selected; " + biomes.Count + " shown. Frequencies are edited in Biomes.");
            if (Widgets.ButtonText(new Rect(rect.width - 170f, 214f, 170f, 30f), "Clear selection")) Draft.selectedBiomeNames.Clear();
            Rect outer = new Rect(0f, 252f, rect.width, Mathf.Max(40f, rect.height - 350f));
            Widgets.BeginScrollView(outer, ref scroll, new Rect(0f, 0f, outer.width - 20f, Mathf.Max(outer.height, biomes.Count * 52f + 8f)));
            float y = 4f;
            foreach (BiomeDef biome in biomes)
            {
                bool selected = Draft.selectedBiomeNames.Contains(biome.defName), before = selected;
                Rect row = new Rect(0f, y, outer.width - 24f, 30f);
                Widgets.CheckboxLabeled(row, biome.LabelCap, ref selected);
                TooltipHandler.TipRegion(row, biome.description);
                if (selected != before)
                {
                    Draft.selectedBiomeNames.RemoveAll(n => n == biome.defName);
                    if (selected)
                    {
                        Draft.selectedBiomeNames.Add(biome.defName);
                        if (BiomeSelection.Weight(Draft, biome) <= 0f)
                        {
                            int index = Draft.biomeNames.IndexOf(biome.defName);
                            if (index < 0) { Draft.biomeNames.Add(biome.defName); Draft.biomeWeights.Add(1f); }
                            else { while (Draft.biomeWeights.Count <= index) Draft.biomeWeights.Add(1f); Draft.biomeWeights[index] = 1f; }
                        }
                    }
                }
                Text.Font = GameFont.Tiny; Widgets.Label(new Rect(0f, y + 28f, row.width, 22f), BiomeCatalog.Provider(biome)); Text.Font = GameFont.Small;
                y += 52f;
            }
            Widgets.EndScrollView();
            Widgets.Label(new Rect(0f, rect.height - 88f, rect.width, 44f), BiomeSelection.Validate(Draft) ?? "Draft selection ready. Return to world controls to Apply, or Save preset to keep this named recipe.");
            if (Widgets.ButtonText(new Rect(rect.width - 180f, rect.height - 40f, 180f, 38f), "Done")) Close();
        }
    }
}
