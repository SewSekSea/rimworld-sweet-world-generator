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
        public List<string> factionNames = new List<string>();
        public List<string> biomeNames = new List<string>();
        public List<float> biomeWeights = new List<float>();

        public WorldPreset Clone()
        {
            return new WorldPreset
            {
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
            Scribe_Collections.Look(ref factionNames, "factionNames", LookMode.Value);
            Scribe_Collections.Look(ref biomeNames, "biomeNames", LookMode.Value);
            Scribe_Collections.Look(ref biomeWeights, "biomeWeights", LookMode.Value);
            if (factionNames == null) factionNames = new List<string>();
            if (biomeNames == null) biomeNames = new List<string>();
            if (biomeWeights == null) biomeWeights = new List<float>();
        }
    }

    public static class WorldPresetRegistry
    {
        private static readonly List<WorldPreset> installedPresets = new List<WorldPreset>();
        private static WorldPreset activeInstalledPreset;

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
            Log.Message("[Unaided World Generator] Registered installed preset: " + preset.name);
        }

        public static void Activate(WorldPreset preset)
        {
            activeInstalledPreset = preset == null ? null : preset.Clone();
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
            Harmony harmony = new Harmony("unaidedmistake.worldgenerator");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            PatchBiomeWorkers(harmony);
            Log.Message("[Unaided World Generator] Loaded. Vanilla world generation remains in control; no preview renderer or background generation is used.");
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
            Log.Message("[Unaided World Generator] Biome compatibility hooks: " + patched.Count);
        }

        public override string SettingsCategory()
        {
            return "Unaided World Generator";
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

    [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
    internal static class CreateWorldPagePatch
    {
        internal static void Postfix(Page_CreateWorldParams __instance, Rect rect)
        {
            Rect button = new Rect(rect.width - 235f, 4f, 225f, 34f);
            if (Widgets.ButtonText(button, "Unaided world controls"))
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

    public sealed class Dialog_UnaidedWorldControls : Window
    {
        private readonly Page_CreateWorldParams page;
        private Vector2 scroll;
        private float viewHeight = 900f;
        private string presetNameBuffer;
        private WorldPreset installedPreset;

        public override Vector2 InitialSize { get { return new Vector2(760f, 760f); } }

        public Dialog_UnaidedWorldControls(Page_CreateWorldParams page)
        {
            this.page = page;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
            UnaidedWorldGeneratorMod.Settings.EnsurePresets();
            if (WorldPresetRegistry.ActiveInstalledPreset != null) installedPreset = WorldPresetRegistry.ActiveInstalledPreset.Clone();
            EnsureFactionSnapshot(Active);
            presetNameBuffer = Active.name;
        }

        private WorldPreset Active
        {
            get { return installedPreset ?? UnaidedWorldGeneratorMod.Settings.presets[UnaidedWorldGeneratorMod.Settings.selectedPreset]; }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "Unaided World Generator");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 34f, inRect.width, 42f), "These controls feed the normal RimWorld generator. There is no preview thread, custom renderer, or existing-world editor.");

            DrawPresetBar(new Rect(0f, 80f, inRect.width, 68f));

            Rect outRect = new Rect(0f, 154f, inRect.width, inRect.height - 204f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 18f, viewHeight);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float y = 0f;
            DrawGenerationControls(viewRect.width, ref y);
            y += 12f;
            DrawFactionControls(viewRect.width, ref y);
            y += 12f;
            DrawBiomeControls(viewRect.width, ref y);
            viewHeight = Mathf.Max(outRect.height, y + 20f);
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(inRect.width - 330f, inRect.height - 42f, 160f, 38f), "Apply and save"))
            {
                Active.name = String.IsNullOrWhiteSpace(presetNameBuffer) ? "World preset" : presetNameBuffer.Trim();
                ApplyToPage(Active);
                WorldPresetRegistry.Activate(installedPreset);
                if (installedPreset == null) UnaidedWorldGeneratorMod.Settings.Write();
                Messages.Message("Unaided world settings applied to the vanilla generator.", MessageTypeDefOf.TaskCompletion, false);
            }
            if (Widgets.ButtonText(new Rect(inRect.width - 160f, inRect.height - 42f, 160f, 38f), "Close")) Close();
        }

        private void DrawPresetBar(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            float x = rect.x + 8f;
            Widgets.Label(new Rect(x, rect.y + 7f, 90f, 28f), "Preset:");
            x += 72f;
            for (int i = 0; i < UnaidedWorldGeneratorMod.Settings.presets.Count; i++)
            {
                int slot = i;
                string label = (i + 1) + (slot == UnaidedWorldGeneratorMod.Settings.selectedPreset ? " selected" : "");
                if (Widgets.ButtonText(new Rect(x, rect.y + 5f, 105f, 30f), label))
                {
                    if (installedPreset == null) SavePageInto(Active);
                    installedPreset = null;
                    WorldPresetRegistry.Activate(null);
                    UnaidedWorldGeneratorMod.Settings.selectedPreset = slot;
                    EnsureFactionSnapshot(Active);
                    presetNameBuffer = Active.name;
                    ApplyToPage(Active);
                }
                x += 112f;
            }
            List<WorldPreset> installed = new List<WorldPreset>(WorldPresetRegistry.InstalledPresets);
            string installedLabel = installedPreset == null ? "Installed presets" : installedPreset.name;
            if (Widgets.ButtonText(new Rect(x, rect.y + 5f, rect.xMax - x - 8f, 30f), installedLabel))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int index = 0; index < installed.Count; index++)
                {
                    WorldPreset choice = installed[index];
                    options.Add(new FloatMenuOption(choice.name, delegate
                    {
                        installedPreset = choice.Clone();
                        WorldPresetRegistry.Activate(installedPreset);
                        EnsureFactionSnapshot(installedPreset);
                        presetNameBuffer = installedPreset.name;
                        ApplyToPage(installedPreset);
                    }));
                }
                if (options.Count == 0) options.Add(new FloatMenuOption("No installed presets", null));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 39f, 100f, 24f), "Name:");
            presetNameBuffer = Widgets.TextField(new Rect(rect.x + 82f, rect.y + 38f, rect.width - 92f, 25f), presetNameBuffer ?? "");
        }

        private void DrawGenerationControls(float width, ref float y)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), "Planet controls");
            Text.Font = GameFont.Small;
            y += 34f;
            Active.coverage = DrawSlider(width, ref y, "Planet coverage", Active.coverage, 0.05f, 1f, Mathf.RoundToInt(Active.coverage * 100f) + "%");
            Widgets.Label(new Rect(0f, y, width * 0.40f, 28f), "World seed");
            Active.seed = Widgets.TextField(new Rect(width * 0.41f, y, width * 0.59f, 25f), Active.seed ?? "");
            y += 31f;
            Active.subdivisions = Mathf.RoundToInt(DrawSlider(width, ref y, "World detail", Active.subdivisions, 1f, 11f, Active.subdivisions + " / 11"));
            Active.pollution = DrawSlider(width, ref y, "Pollution coverage", Active.pollution, 0f, 1f, Mathf.RoundToInt(Active.pollution * 100f) + "%");
            Active.riverDensity = DrawSlider(width, ref y, "River density", Active.riverDensity, 0f, 2f, Active.riverDensity.ToString("0.00") + "x");
            Active.mountainDensity = DrawSlider(width, ref y, "Mountain density", Active.mountainDensity, 0f, 2f, Active.mountainDensity.ToString("0.00") + "x");
            Active.rainfall = DrawEnum<OverallRainfall>(width, ref y, "Rainfall", Active.rainfall);
            Active.temperature = DrawEnum<OverallTemperature>(width, ref y, "Temperature", Active.temperature);
            Active.population = DrawEnum<OverallPopulation>(width, ref y, "Population", Active.population);
            bool insectHivePresent = WorldControlUtility.HasInsectHiveFaction(Active);
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            if (!insectHivePresent)
            {
                GUI.color = new Color(0.48f, 0.48f, 0.48f, 1f);
                GUI.enabled = false;
            }
            Active.infestationDensity = DrawSlider(width, ref y, "Insect infestations", Active.infestationDensity, 0f, 2f,
                insectHivePresent ? Active.infestationDensity.ToString("0.00") + "x" : "Hive faction removed");
            GUI.enabled = oldEnabled;
            GUI.color = oldColor;
            Active.landmarks = DrawEnum<LandmarkDensity>(width, ref y, "Landmark density", Active.landmarks);
            Widgets.Label(new Rect(0f, y, width, 44f), "Coverage controls how much of the planet is generated. World detail controls the underlying tile subdivision from 1 to 11; 10 is RimWorld's normal value and 11 is substantially heavier.");
            y += 48f;
        }

        private void DrawFactionControls(float width, ref float y)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), "Faction setup - simple counts");
            Text.Font = GameFont.Small;
            y += 32f;
            Widgets.Label(new Rect(0f, y, width, 40f), "Only factions supplied by currently active mods are shown. A removed race or faction mod cannot remain in this list.");
            y += 42f;

            if (Widgets.ButtonText(new Rect(0f, y, 180f, 28f), "Restore vanilla list"))
            {
                CopyFactionList(InitialFactionList(), Active);
            }
            if (Widgets.ButtonText(new Rect(188f, y, 150f, 28f), "One of each"))
            {
                List<FactionDef> candidates = FactionCandidates();
                Active.factionNames.Clear();
                for (int index = 0; index < candidates.Count; index++) Active.factionNames.Add(candidates[index].defName);
                Active.factionsInitialized = true;
            }
            y += 34f;

            List<FactionDef> defs = FactionCandidates();
            for (int index = 0; index < defs.Count; index++)
            {
                FactionDef faction = defs[index];
                int count = FactionCount(Active, faction.defName);
                Widgets.Label(new Rect(0f, y, width - 150f, 28f), faction.LabelCap);
                if (Widgets.ButtonText(new Rect(width - 144f, y, 38f, 26f), "-")) SetFactionCount(Active, faction.defName, Mathf.Max(0, count - 1));
                Widgets.Label(new Rect(width - 101f, y, 52f, 26f), count.ToString());
                if (Widgets.ButtonText(new Rect(width - 45f, y, 38f, 26f), "+")) SetFactionCount(Active, faction.defName, Mathf.Min(12, count + 1));
                y += 29f;
            }
        }

        private void DrawBiomeControls(float width, ref float y)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 30f), "Biome frequency");
            Text.Font = GameFont.Small;
            y += 32f;
            Widgets.Label(new Rect(0f, y, width, 42f), "0 disables natural placement; 1 is the mod author's normal weight; values above 1 make the biome more common. Water and special non-natural biomes are omitted.");
            y += 47f;
            List<BiomeDef> biomes = DefDatabase<BiomeDef>.AllDefsListForReading.FindAll(delegate(BiomeDef b) { return b.generatesNaturally && b.implemented; });
            biomes.Sort(delegate(BiomeDef a, BiomeDef b) { return String.Compare(a.LabelCap.ToString(), b.LabelCap.ToString(), StringComparison.CurrentCultureIgnoreCase); });
            foreach (BiomeDef biome in biomes)
            {
                float value = GetBiomeWeight(Active, biome.defName);
                value = DrawSlider(width, ref y, biome.LabelCap.ToString(), value, 0f, 3f, value.ToString("0.00") + "x");
                SetBiomeWeight(Active, biome.defName, value);
            }
        }

        private static float DrawSlider(float width, ref float y, string label, float value, float min, float max, string valueLabel)
        {
            Widgets.Label(new Rect(0f, y, width * 0.40f, 28f), label);
            value = Widgets.HorizontalSlider(new Rect(width * 0.41f, y + 3f, width * 0.43f, 24f), value, min, max, true);
            Widgets.Label(new Rect(width * 0.86f, y, width * 0.14f, 28f), valueLabel);
            y += 31f;
            return value;
        }

        private static int DrawEnum<T>(float width, ref float y, string label, int raw) where T : struct
        {
            Array values = Enum.GetValues(typeof(T));
            int index = Array.IndexOf(values, (T)Enum.ToObject(typeof(T), raw));
            if (index < 0) index = 0;
            Widgets.Label(new Rect(0f, y, width * 0.52f, 28f), label);
            if (Widgets.ButtonText(new Rect(width * 0.53f, y, width * 0.47f, 28f), values.GetValue(index).ToString()))
            {
                index = (index + 1) % values.Length;
            }
            y += 32f;
            return Convert.ToInt32(values.GetValue(index));
        }

        private static float GetBiomeWeight(WorldPreset preset, string defName)
        {
            int index = preset.biomeNames.IndexOf(defName);
            return index >= 0 && index < preset.biomeWeights.Count ? preset.biomeWeights[index] : 1f;
        }

        private static void SetBiomeWeight(WorldPreset preset, string defName, float value)
        {
            int index = preset.biomeNames.IndexOf(defName);
            if (index < 0)
            {
                preset.biomeNames.Add(defName);
                preset.biomeWeights.Add(value);
            }
            else
            {
                while (preset.biomeWeights.Count <= index) preset.biomeWeights.Add(1f);
                preset.biomeWeights[index] = value;
            }
        }

        private void EnsureFactionSnapshot(WorldPreset preset)
        {
            if (preset == null || preset.factionsInitialized) return;
            CopyFactionList((List<FactionDef>)PageFields.Factions.GetValue(page), preset);
        }

        private List<FactionDef> InitialFactionList()
        {
            List<FactionDef> initial = (List<FactionDef>)PageFields.InitialFactions.GetValue(page);
            return initial ?? new List<FactionDef>();
        }

        private List<FactionDef> FactionCandidates()
        {
            List<FactionDef> result = new List<FactionDef>();
            HashSet<string> names = new HashSet<string>();
            AddFactionCandidates(result, names, InitialFactionList());
            AddFactionCandidates(result, names, (List<FactionDef>)PageFields.Factions.GetValue(page));
            for (int index = 0; index < Active.factionNames.Count; index++)
            {
                FactionDef faction = DefDatabase<FactionDef>.GetNamedSilentFail(Active.factionNames[index]);
                if (faction != null && names.Add(faction.defName)) result.Add(faction);
            }
            result.Sort(delegate(FactionDef a, FactionDef b)
            {
                return String.Compare(a.LabelCap.ToString(), b.LabelCap.ToString(), StringComparison.CurrentCultureIgnoreCase);
            });
            return result;
        }

        private static void AddFactionCandidates(List<FactionDef> result, HashSet<string> names, List<FactionDef> source)
        {
            if (source == null) return;
            for (int index = 0; index < source.Count; index++)
            {
                FactionDef faction = source[index];
                if (faction != null && names.Add(faction.defName)) result.Add(faction);
            }
        }

        private static void CopyFactionList(List<FactionDef> source, WorldPreset preset)
        {
            preset.factionNames.Clear();
            if (source != null)
            {
                for (int index = 0; index < source.Count; index++)
                {
                    if (source[index] != null) preset.factionNames.Add(source[index].defName);
                }
            }
            preset.factionsInitialized = true;
        }

        private static int FactionCount(WorldPreset preset, string defName)
        {
            int count = 0;
            for (int index = 0; index < preset.factionNames.Count; index++)
            {
                if (preset.factionNames[index] == defName) count++;
            }
            return count;
        }

        private static void SetFactionCount(WorldPreset preset, string defName, int count)
        {
            for (int index = preset.factionNames.Count - 1; index >= 0; index--)
            {
                if (preset.factionNames[index] == defName) preset.factionNames.RemoveAt(index);
            }
            for (int index = 0; index < count; index++) preset.factionNames.Add(defName);
            preset.factionsInitialized = true;
        }

        private void SavePageInto(WorldPreset preset)
        {
            if (PageFields.Seed != null) preset.seed = (string)PageFields.Seed.GetValue(page) ?? "";
            preset.coverage = (float)PageFields.Coverage.GetValue(page);
            preset.pollution = (float)PageFields.Pollution.GetValue(page);
            preset.rainfall = (int)(OverallRainfall)PageFields.Rainfall.GetValue(page);
            preset.temperature = (int)(OverallTemperature)PageFields.Temperature.GetValue(page);
            preset.population = (int)(OverallPopulation)PageFields.Population.GetValue(page);
            preset.landmarks = (int)(LandmarkDensity)PageFields.Landmarks.GetValue(page);
            CopyFactionList((List<FactionDef>)PageFields.Factions.GetValue(page), preset);
        }

        private static void ApplyToPage(Page_CreateWorldParams page, WorldPreset preset)
        {
            if (PageFields.Seed != null && !String.IsNullOrWhiteSpace(preset.seed)) PageFields.Seed.SetValue(page, preset.seed.Trim());
            PageFields.Coverage.SetValue(page, Mathf.Clamp(preset.coverage, 0.05f, 1f));
            PageFields.Pollution.SetValue(page, Mathf.Clamp01(preset.pollution));
            PageFields.Rainfall.SetValue(page, (OverallRainfall)preset.rainfall);
            PageFields.Temperature.SetValue(page, (OverallTemperature)preset.temperature);
            PageFields.Population.SetValue(page, (OverallPopulation)preset.population);
            PageFields.Landmarks.SetValue(page, (LandmarkDensity)preset.landmarks);
            List<FactionDef> factions = (List<FactionDef>)PageFields.Factions.GetValue(page);
            if (factions != null && preset.factionsInitialized)
            {
                factions.Clear();
                for (int index = 0; index < preset.factionNames.Count; index++)
                {
                    FactionDef faction = DefDatabase<FactionDef>.GetNamedSilentFail(preset.factionNames[index]);
                    if (faction != null) factions.Add(faction);
                }
            }
        }

        private void ApplyToPage(WorldPreset preset)
        {
            ApplyToPage(page, preset);
        }
    }
}
