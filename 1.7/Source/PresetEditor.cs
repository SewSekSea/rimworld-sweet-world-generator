using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    internal sealed class PresetEditor
    {
        private readonly UnaidedWorldSettings settings;
        private readonly List<WorldPreset> drafts;
        private readonly List<WorldPreset> saved;
        private readonly List<string> initialFactions;
        private readonly Dictionary<string, WorldPreset> installedDrafts = new Dictionary<string, WorldPreset>(StringComparer.OrdinalIgnoreCase);
        private WorldPreset installed;
        public int Slot { get; private set; }
        public bool IsInstalled { get { return installed != null; } }
        public WorldPreset Draft { get { return installed ?? drafts[Slot]; } }
        public bool Dirty { get { return IsInstalled || !Same(Draft, saved[Slot]); } }
        public IEnumerable<WorldPreset> PersonalDrafts { get { return drafts; } }

        public PresetEditor(UnaidedWorldSettings settings, IEnumerable<FactionDef> factions)
        {
            this.settings = settings;
            settings.EnsurePresets();
            initialFactions = factions.Where(f => f != null).Select(f => f.defName).ToList();
            drafts = settings.presets.Select(p => p.Clone()).ToList();
            foreach (WorldPreset draft in drafts) InitializeFactions(draft);
            saved = drafts.Select(p => p.Clone()).ToList();
            Slot = settings.selectedPreset;
            int appliedSlot = WorldPresetRegistry.AppliedPersonalSlot;
            if (WorldPresetRegistry.ActiveInstalledPreset != null)
                SelectInstalled(WorldPresetRegistry.AppliedPreset ?? WorldPresetRegistry.ActiveInstalledPreset);
            else if (WorldPresetRegistry.AppliedPreset != null && appliedSlot >= 0 && appliedSlot < drafts.Count)
            {
                Slot = appliedSlot;
                drafts[Slot] = WorldPresetRegistry.AppliedPreset.Clone();
                InitializeFactions(drafts[Slot]);
            }
        }

        private void InitializeFactions(WorldPreset preset)
        {
            if (preset.factionsInitialized) return;
            preset.factionNames = new List<string>(initialFactions);
            preset.factionsInitialized = true;
        }

        public void SelectPersonal(int slot)
        {
            if (slot < 0 || slot >= drafts.Count) throw new ArgumentOutOfRangeException("slot");
            Slot = slot;
            installed = null;
        }

        public void SelectInstalled(WorldPreset preset)
        {
            if (preset == null) return;
            WorldPreset draft;
            string identity = (preset.builtInRecipe ? "recipe:" : "installed:") + preset.name;
            if (!installedDrafts.TryGetValue(identity, out draft))
            {
                draft = preset.Clone();
                installedDrafts.Add(identity, draft);
            }
            installed = draft;
            InitializeFactions(installed);
        }

        public void RestoreSaved()
        {
            if (IsInstalled) return;
            drafts[Slot] = settings.presets[Slot].Clone();
            InitializeFactions(drafts[Slot]);
            saved[Slot] = drafts[Slot].Clone();
        }

        public void CopyToPersonal(int slot)
        {
            WorldPreset copy = Draft.Clone();
            copy.builtInRecipe = false;
            copy.name = (String.IsNullOrWhiteSpace(copy.name) ? "World preset" : copy.name.Trim()) + " copy";
            SelectPersonal(slot);
            drafts[slot] = copy;
        }

        public void Save(Action persist)
        {
            if (IsInstalled) throw new InvalidOperationException("Copy an installed preset into a personal slot first.");
            WorldPreset previous = settings.presets[Slot];
            int previousSlot = settings.selectedPreset;
            Draft.name = String.IsNullOrWhiteSpace(Draft.name) ? "World preset " + (Slot + 1) : Draft.name.Trim();
            settings.presets[Slot] = Draft.Clone();
            settings.selectedPreset = Slot;
            try { persist(); }
            catch
            {
                settings.presets[Slot] = previous;
                settings.selectedPreset = previousSlot;
                throw;
            }
            saved[Slot] = Draft.Clone();
        }

        public void ResetPlanet()
        {
            WorldPreset normal = new WorldPreset();
            Draft.seed = normal.seed;
            Draft.coverage = normal.coverage;
            Draft.subdivisions = normal.subdivisions;
            Draft.pollution = normal.pollution;
            Draft.riverDensity = normal.riverDensity;
            Draft.mountainDensity = normal.mountainDensity;
            Draft.infestationDensity = normal.infestationDensity;
            Draft.rainfall = normal.rainfall;
            Draft.temperature = normal.temperature;
            Draft.population = normal.population;
            Draft.landmarks = normal.landmarks;
            Draft.hydrophobia = false;
        }

        public void ResetFactions()
        {
            Draft.factionNames = new List<string>(initialFactions);
            Draft.factionsInitialized = true;
        }

        public void ResetBiomes() { Draft.biomeNames.Clear(); Draft.biomeWeights.Clear(); Draft.selectedBiomeNames.Clear(); Draft.selectedBiomesOnly = false; Draft.adaptBiomeClimate = true; }
        public void ResetAll() { ResetPlanet(); ResetFactions(); ResetBiomes(); }

        private static bool Same(WorldPreset a, WorldPreset b)
        {
            return a.name == b.name && a.seed == b.seed && a.coverage == b.coverage && a.subdivisions == b.subdivisions
                && a.pollution == b.pollution && a.riverDensity == b.riverDensity && a.mountainDensity == b.mountainDensity
                && a.infestationDensity == b.infestationDensity && a.rainfall == b.rainfall && a.temperature == b.temperature
                && a.population == b.population && a.landmarks == b.landmarks && a.hydrophobia == b.hydrophobia
                && a.selectedBiomesOnly == b.selectedBiomesOnly && a.adaptBiomeClimate == b.adaptBiomeClimate && a.selectedBiomeNames.SequenceEqual(b.selectedBiomeNames)
                && a.factionsInitialized == b.factionsInitialized && a.factionNames.SequenceEqual(b.factionNames)
                && a.biomeNames.SequenceEqual(b.biomeNames) && a.biomeWeights.SequenceEqual(b.biomeWeights);
        }
    }

    internal static class BiomeCatalog
    {
        internal static string Provider(Def def) { return def.GetModExtension<CavernBiome>() != null ? "Cavern oases" : def.GetModExtension<SurfaceAnomalyBiome>() != null ? "Anomaly surface variants" : def.modContentPack == null ? "Unknown provider" : def.modContentPack.Name; }
        internal static float DefaultWeight(BiomeDef biome) { return biome.GetModExtension<SurfaceAnomalyBiome>() != null || biome.GetModExtension<CavernBiome>() != null ? 0f : 1f; }
        internal static bool Matches(Def def, string query, string provider)
        {
            if (!String.IsNullOrEmpty(provider) && !String.Equals(Provider(def), provider, StringComparison.OrdinalIgnoreCase)) return false;
            if (String.IsNullOrWhiteSpace(query)) return true;
            string search = query.Trim();
            return (def.LabelCap.ToString() + " " + def.defName + " " + Provider(def)).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        internal static bool Editable(BiomeDef biome) { return biome != null && biome.implemented && biome.generatesNaturally && !biome.isWaterBiome; }
        internal static List<BiomeDef> Natural(string query, string provider)
        {
            return DefDatabase<BiomeDef>.AllDefsListForReading.Where(b => Editable(b) && Matches(b, query, provider)).OrderBy(b => b.LabelCap.ToString()).ToList();
        }
        internal static List<BiomeDef> Special(string query, string provider)
        {
            return DefDatabase<BiomeDef>.AllDefsListForReading.Where(b => b.implemented && !b.isWaterBiome && !b.generatesNaturally && Matches(b, query, provider)).OrderBy(b => b.LabelCap.ToString()).ToList();
        }
        internal static List<string> Providers()
        {
            return DefDatabase<BiomeDef>.AllDefsListForReading.Where(b => b.implemented && !b.isWaterBiome).Select(Provider).Distinct().OrderBy(p => p).ToList();
        }
    }
}
