using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class WorldPreset : IExposable
    {
        internal bool builtInRecipe;
        public string name = "Preset";
        public string seed = "";
        public float coverage = 0.30f;
        public int subdivisions = 10;
        public float pollution = 0.05f;
        public float riverDensity = 1f;
        public float mountainDensity = 1f;
        public float infestationDensity = 1f;
        public int rainfall = (int)OverallRainfall.Normal;
        public int temperature = (int)OverallTemperature.Normal;
        public int population = (int)OverallPopulation.Normal;
        public int landmarks = (int)LandmarkDensity.Normal;
        public bool factionsInitialized;
        public bool hydrophobia;
        public bool selectedBiomesOnly;
        public bool adaptBiomeClimate = true;
        public List<string> selectedBiomeNames = new List<string>();
        public List<string> factionNames = new List<string>();
        public List<string> biomeNames = new List<string>();
        public List<float> biomeWeights = new List<float>();

        public WorldPreset Clone()
        {
            return new WorldPreset
            {
                builtInRecipe = builtInRecipe,
                name = name,
                seed = seed,
                coverage = coverage,
                subdivisions = subdivisions,
                pollution = pollution,
                riverDensity = riverDensity,
                mountainDensity = mountainDensity,
                infestationDensity = infestationDensity,
                rainfall = rainfall,
                temperature = temperature,
                population = population,
                landmarks = landmarks,
                factionsInitialized = factionsInitialized,
                hydrophobia = hydrophobia,
                selectedBiomesOnly = selectedBiomesOnly,
                adaptBiomeClimate = adaptBiomeClimate,
                selectedBiomeNames = new List<string>(selectedBiomeNames ?? new List<string>()),
                factionNames = new List<string>(factionNames ?? new List<string>()),
                biomeNames = new List<string>(biomeNames ?? new List<string>()),
                biomeWeights = new List<float>(biomeWeights ?? new List<float>())
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref name, "name", "Preset");
            Scribe_Values.Look(ref seed, "seed", "");
            Scribe_Values.Look(ref coverage, "coverage", 0.30f);
            Scribe_Values.Look(ref subdivisions, "subdivisions", 10);
            Scribe_Values.Look(ref pollution, "pollution", 0.05f);
            Scribe_Values.Look(ref riverDensity, "riverDensity", 1f);
            Scribe_Values.Look(ref mountainDensity, "mountainDensity", 1f);
            Scribe_Values.Look(ref infestationDensity, "infestationDensity", 1f);
            Scribe_Values.Look(ref rainfall, "rainfall", (int)OverallRainfall.Normal);
            Scribe_Values.Look(ref temperature, "temperature", (int)OverallTemperature.Normal);
            Scribe_Values.Look(ref population, "population", (int)OverallPopulation.Normal);
            Scribe_Values.Look(ref landmarks, "landmarks", (int)LandmarkDensity.Normal);
            Scribe_Values.Look(ref factionsInitialized, "factionsInitialized", false);
            Scribe_Values.Look(ref hydrophobia, "hydrophobia", false);
            Scribe_Values.Look(ref selectedBiomesOnly, "selectedBiomesOnly", false);
            Scribe_Values.Look(ref adaptBiomeClimate, "adaptBiomeClimate", true);
            Scribe_Collections.Look(ref selectedBiomeNames, "selectedBiomeNames", LookMode.Value);
            Scribe_Collections.Look(ref factionNames, "factionNames", LookMode.Value);
            Scribe_Collections.Look(ref biomeNames, "biomeNames", LookMode.Value);
            Scribe_Collections.Look(ref biomeWeights, "biomeWeights", LookMode.Value);
            if (factionNames == null) factionNames = new List<string>();
            if (biomeNames == null) biomeNames = new List<string>();
            if (biomeWeights == null) biomeWeights = new List<float>();
            if (selectedBiomeNames == null) selectedBiomeNames = new List<string>();
        }
    }

    public static class WorldPresetRegistry
    {
        private static readonly List<WorldPreset> installedPresets = new List<WorldPreset>();
        private static WorldPreset activeInstalledPreset;
        public static WorldPreset AppliedPreset { get; private set; }
        public static int AppliedPersonalSlot { get; private set; } = -1;

        public static IEnumerable<WorldPreset> InstalledPresets
        {
            get { return installedPresets; }
        }

        public static WorldPreset ActiveInstalledPreset
        {
            get { return activeInstalledPreset; }
        }

        public static void Register(WorldPreset preset)
        {
            if (preset == null || String.IsNullOrWhiteSpace(preset.name)) return;
            for (int index = installedPresets.Count - 1; index >= 0; index--)
            {
                if (String.Equals(installedPresets[index].name, preset.name, StringComparison.OrdinalIgnoreCase))
                {
                    installedPresets.RemoveAt(index);
                }
            }
            installedPresets.Add(preset.Clone());
            Log.Message("[Sweet World Generator] Registered installed preset: " + preset.name);
        }

        public static void Activate(WorldPreset preset)
        {
            activeInstalledPreset = preset == null ? null : preset.Clone();
            Apply(preset, -1);
        }

        public static void Apply(WorldPreset preset, int personalSlot)
        {
            AppliedPreset = preset == null ? null : preset.Clone();
            AppliedPersonalSlot = personalSlot;
        }
    }

    public sealed class UnaidedWorldSettings : ModSettings
    {
        public List<WorldPreset> presets = new List<WorldPreset>();
        public int selectedPreset;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref presets, "presets", LookMode.Deep);
            Scribe_Values.Look(ref selectedPreset, "selectedPreset", 0);
            EnsurePresets();
        }

        public void EnsurePresets()
        {
            if (presets == null) presets = new List<WorldPreset>();
            while (presets.Count < 3)
            {
                WorldPreset preset = new WorldPreset();
                preset.name = "World preset " + (presets.Count + 1);
                presets.Add(preset);
            }
            for (int index = 0; index < presets.Count; index++)
                if (presets[index] == null) presets[index] = new WorldPreset { name = "World preset " + (index + 1) };
            if (selectedPreset < 0 || selectedPreset >= presets.Count) selectedPreset = 0;
        }
    }

    public sealed class UnaidedWorldGeneratorMod : Mod
    {
        internal static UnaidedWorldSettings Settings;

        public UnaidedWorldGeneratorMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<UnaidedWorldSettings>();
            Settings.EnsurePresets();
            WorldPresetRegistry.Apply(Settings.presets[Settings.selectedPreset], Settings.selectedPreset);
            Harmony harmony = new Harmony("unaidedmistake.worldgenerator");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            PatchBiomeWorkers(harmony);
            Log.Message("[Sweet World Generator] Loaded. Vanilla world generation remains in control; no preview renderer or background generation is used.");
        }

        private static void PatchBiomeWorkers(Harmony harmony)
        {
            HashSet<MethodBase> patched = new HashSet<MethodBase>();
            Type baseType = typeof(BiomeWorker);
            foreach (Type type in GenTypes.AllTypes)
            {
                if (type.IsAbstract || !baseType.IsAssignableFrom(type)) continue;
                MethodInfo method = type.GetMethod("GetScore", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new Type[] { typeof(BiomeDef), typeof(Tile), typeof(PlanetTile) }, null);
                // Reflection through a derived worker can return an inherited MethodInfo
                // whose ReflectedType is not its implementation owner. Harmony requires
                // the declared implementation, including one on an abstract base class.
                if (method != null && method.DeclaringType != null)
                    method = method.DeclaringType.GetMethod(method.Name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                        null, new Type[] { typeof(BiomeDef), typeof(Tile), typeof(PlanetTile) }, null);
                if (method != null && (method.IsAbstract || method.ContainsGenericParameters)) continue;
                if (method == null || patched.Contains(method)) continue;
                harmony.Patch(method, postfix: new HarmonyMethod(typeof(BiomeScorePatch), "Postfix"));
                patched.Add(method);
            }
            Log.Message("[Sweet World Generator] Biome compatibility hooks: " + patched.Count);
        }

        public override string SettingsCategory()
        {
            return "Sweet World Generator";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Widgets.Label(inRect, "World controls are available from the vanilla Create World page. This mod never creates a live preview and does not edit existing worlds.");
        }
    }

    internal static class PageFields
    {
        internal static readonly FieldInfo Coverage = AccessTools.Field(typeof(Page_CreateWorldParams), "planetCoverage");
        internal static readonly FieldInfo Pollution = AccessTools.Field(typeof(Page_CreateWorldParams), "pollution");
        internal static readonly FieldInfo Rainfall = AccessTools.Field(typeof(Page_CreateWorldParams), "rainfall");
        internal static readonly FieldInfo Temperature = AccessTools.Field(typeof(Page_CreateWorldParams), "temperature");
        internal static readonly FieldInfo Population = AccessTools.Field(typeof(Page_CreateWorldParams), "population");
        internal static readonly FieldInfo Landmarks = AccessTools.Field(typeof(Page_CreateWorldParams), "landmarkDensity");
        internal static readonly FieldInfo Factions = AccessTools.Field(typeof(Page_CreateWorldParams), "factions");
        internal static readonly FieldInfo InitialFactions = AccessTools.Field(typeof(Page_CreateWorldParams), "initialFactions");
        internal static readonly FieldInfo Seed = AccessTools.Field(typeof(Page_CreateWorldParams), "seedString");
    }

    [HarmonyPatch(typeof(Page), "get_InitialSize")]
    internal static class CreateWorldPageWidthPatch
    {
        internal static void Postfix(Page __instance, ref Vector2 __result)
        {
            if (__instance is Page_CreateWorldParams)
                __result.x = Mathf.Min(__result.x + 80f, Mathf.Max(320f, UI.screenWidth - 40f));
        }
    }

    [HarmonyPatch(typeof(WorldFactionsUIUtility), "DoRow")]
    internal static class CreateWorldFactionRowWidthPatch
    {
        internal static void Prefix(ref Rect rect)
        {
            if (Find.WindowStack.IsOpen<Page_CreateWorldParams>())
                rect.width = Mathf.Max(0f, rect.width - 18f);
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
    internal static class CreateWorldPagePatch
    {
        internal static void Postfix(Page_CreateWorldParams __instance, Rect rect)
        {
            Rect button = new Rect(rect.width - 235f, 4f, 225f, 34f);
            if (Widgets.ButtonText(button, "Sweet world controls"))
            {
                Find.WindowStack.Add(new Dialog_UnaidedWorldControls(__instance));
            }
        }
    }

    internal static class BiomeScorePatch
    {
        public static void Postfix(BiomeDef biome, ref float __result)
        {
            if (biome == null || __result <= 0f) return;
            WorldPreset preset = CurrentPreset;
            if (preset == null) return;
            int index = preset.biomeNames.IndexOf(biome.defName);
            if (index >= 0 && index < preset.biomeWeights.Count)
            {
                __result *= Mathf.Clamp(preset.biomeWeights[index], 0f, 3f);
            }
        }

        internal static WorldPreset CurrentPreset
        {
            get
            {
                UnaidedWorldSettings settings = UnaidedWorldGeneratorMod.Settings;
                if (WorldPresetRegistry.AppliedPreset != null) return WorldPresetRegistry.AppliedPreset;
                if (WorldPresetRegistry.ActiveInstalledPreset != null) return WorldPresetRegistry.ActiveInstalledPreset;
                if (settings == null) return null;
                settings.EnsurePresets();
                return settings.presets[settings.selectedPreset];
            }
        }
    }

    internal static class WorldControlUtility
    {
        internal static bool HasInsectHiveFaction(WorldPreset preset)
        {
            if (preset == null || preset.factionNames == null) return false;
            for (int index = 0; index < preset.factionNames.Count; index++)
            {
                FactionDef faction = DefDatabase<FactionDef>.GetNamedSilentFail(preset.factionNames[index]);
                if (faction == null) continue;
                string identity = (faction.defName + " " + faction.label).ToLowerInvariant();
                if (identity.Contains("insect") && (identity.Contains("hive") || identity.Contains("gene") || identity.Contains("xeno")))
                {
                    return true;
                }
            }
            return false;
        }

        internal static float RiverDensity
        {
            get
            {
                WorldPreset preset = BiomeScorePatch.CurrentPreset;
                return preset == null ? 1f : Mathf.Clamp(preset.riverDensity, 0f, 2f);
            }
        }

        internal static float MountainDensity
        {
            get
            {
                WorldPreset preset = BiomeScorePatch.CurrentPreset;
                return preset == null ? 1f : Mathf.Clamp(preset.mountainDensity, 0f, 2f);
            }
        }

        internal static float InfestationDensity
        {
            get
            {
                WorldPreset preset = BiomeScorePatch.CurrentPreset;
                if (!HasInsectHiveFaction(preset)) return 0f;
                return Mathf.Clamp(preset.infestationDensity, 0f, 2f);
            }
        }
    }

    [HarmonyPatch(typeof(ScenPart_PlanetLayer), "get_Settings")]
    internal static class PlanetSubdivisionPatch
    {
        internal static void Postfix(ScenPart_PlanetLayer __instance, PlanetLayerSettings __result)
        {
            WorldPreset preset = BiomeScorePatch.CurrentPreset;
            if (__instance == null || __result == null || preset == null || __instance.layer != PlanetLayerDefOf.Surface) return;
            __result.subdivisions = Mathf.Clamp(preset.subdivisions, 1, 11);
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Rivers), "CalculateEvaporationConstant")]
    internal static class RiverDensityPatch
    {
        internal static void Postfix(ref float __result)
        {
            float density = WorldControlUtility.RiverDensity;
            __result = density <= 0.001f ? __result * 1000f : __result / density;
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Rivers), "GenerateRivers")]
    internal static class NoRiversPatch
    {
        internal static void Postfix(PlanetLayer layer)
        {
            if (WorldControlUtility.RiverDensity > 0.001f || layer == null) return;
            for (int index = 0; index < layer.TilesCount; index++)
            {
                SurfaceTile tile = layer[index] as SurfaceTile;
                if (tile != null && tile.Rivers != null) tile.Rivers.Clear();
            }
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Terrain), "GenerateTileFor")]
    internal static class MountainDensityPatch
    {
        internal static void Postfix(PlanetTile tile, ref Tile __result)
        {
            if (__result == null || __result.WaterCovered) return;
            float density = WorldControlUtility.MountainDensity;
            float roll = Rand.ValueSeeded(tile.tileId ^ 194783205);
            if (density < 1f)
            {
                if ((__result.hilliness == Hilliness.Mountainous || __result.hilliness == Hilliness.Impassable) && roll > density)
                {
                    __result.hilliness = Hilliness.LargeHills;
                }
            }
            else if (density > 1f)
            {
                float promotion = density - 1f;
                if (__result.hilliness == Hilliness.LargeHills && roll < promotion)
                {
                    __result.hilliness = Hilliness.Mountainous;
                }
                else if (__result.hilliness == Hilliness.Mountainous && roll < promotion * 0.35f)
                {
                    __result.hilliness = Hilliness.Impassable;
                }
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker), "get_BaseChanceThisGame")]
    internal static class InfestationDensityPatch
    {
        internal static void Postfix(IncidentWorker __instance, ref float __result)
        {
            if (__instance is IncidentWorker_Infestation || __instance is IncidentWorker_DeepDrillInfestation)
            {
                __result *= WorldControlUtility.InfestationDensity;
            }
        }
    }

}
