using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class Dialog_UnaidedWorldControls : Window
    {
        private readonly Page_CreateWorldParams page;
        private readonly PresetEditor editor;
        private readonly Dictionary<string, string> numericBuffers = new Dictionary<string, string>();
        private Vector2 scroll;
        private float viewHeight = 850f;
        private int tab;
        private string factionSearch = "";
        private string biomeSearch = "";
        private string provider = "";
        private bool showSpecial;
        private string status = "Apply changes this world setup. Save preset keeps a personal preset for later.";
        private WorldPreset Active { get { return editor.Draft; } }
        internal bool CanRenamePreset { get { return !editor.IsInstalled; } }

        public override Vector2 InitialSize
        {
            get { return new Vector2(Mathf.Min(1060f, Mathf.Max(360f, UI.screenWidth - 40f)), Mathf.Min(840f, Mathf.Max(380f, UI.screenHeight - 40f))); }
        }

        public Dialog_UnaidedWorldControls(Page_CreateWorldParams page)
        {
            this.page = page;
            editor = new PresetEditor(UnaidedWorldGeneratorMod.Settings, InitialFactionList());
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width - 30f, 32f), "Sweet World Generator");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 36f, inRect.width, 26f), "Plan a new world with vanilla generation. Existing worlds are not edited.");
            DrawPresetBar(new Rect(0f, 68f, inRect.width, 88f));
            string[] tabs = { "Planet", "Factions", "Biomes" };
            float tabWidth = (inRect.width - 16f) / 3f;
            for (int index = 0; index < tabs.Length; index++)
            {
                Rect button = new Rect(index * (tabWidth + 8f), 164f, tabWidth, 34f);
                if (index == tab && index != 1) Widgets.DrawHighlightSelected(button);
                if (Widgets.ButtonText(button, tabs[index])) { if (index == 1) OpenFactions(); else SelectTab(index); }
            }
            Rect outer = new Rect(0f, 208f, inRect.width, Mathf.Max(40f, inRect.height - 302f));
            Rect view = new Rect(0f, 0f, outer.width - 20f, viewHeight);
            Widgets.BeginScrollView(outer, ref scroll, view);
            float y = 4f;
            if (tab == 0) DrawPlanet(view.width, ref y);
            else DrawBiomes(view.width, ref y);
            viewHeight = Mathf.Max(outer.height, y + 16f);
            Widgets.EndScrollView();
            string dirty = editor.Dirty ? "Unsaved draft. " : "Saved preset. ";
            Widgets.Label(new Rect(0f, inRect.height - 84f, inRect.width, 38f), dirty + status);
            float buttonWidth = Mathf.Min(180f, (inRect.width - 16f) / 3f);
            float start = inRect.width - buttonWidth * 3f - 16f;
            if (Widgets.ButtonText(new Rect(start, inRect.height - 40f, buttonWidth, 38f), "Apply to world")) RequestApply();
            if (Widgets.ButtonText(new Rect(start + buttonWidth + 8f, inRect.height - 40f, buttonWidth, 38f), editor.IsInstalled ? "Save personal copy..." : "Save preset")) SaveDraft();
            if (Widgets.ButtonText(new Rect(start + 2f * (buttonWidth + 8f), inRect.height - 40f, buttonWidth, 38f), "Cancel / close")) Close();
        }

        private void DrawPresetBar(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            float width = rect.width;
            string selected = (editor.IsInstalled ? Active.builtInRecipe ? "Recipe: " : "Installed: " : "Personal: ") + Active.name + (editor.Dirty ? " *" : "");
            Rect personalButton = new Rect(8f, rect.y + 8f, width * 0.58f - 12f, 30f);
            if (Widgets.ButtonText(personalButton, selected.Truncate(personalButton.width - 16f)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                int index = 0;
                foreach (WorldPreset preset in editor.PersonalDrafts)
                {
                    int slot = index++;
                    options.Add(new FloatMenuOption((slot + 1) + ": " + preset.name, delegate { SwitchPersonalPreset(slot); }));
                }
                if (!editor.IsInstalled)
                    options.Add(new FloatMenuOption("Reload saved version of this slot...", delegate
                    {
                        Confirm("Replace this slot's draft with its saved version? This does not change the applied world setup.", delegate { editor.RestoreSaved(); ChangedSelection(); });
                    }));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            TooltipHandler.TipRegion(personalButton, "Switch between personal drafts. Switching does not save or apply them; drafts are retained until this window closes.");
            List<WorldPreset> installed = WorldRecipes.Available().Concat(WorldPresetRegistry.InstalledPresets).ToList();
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && installed.Count > 0;
            if (Widgets.ButtonText(new Rect(width * 0.58f, rect.y + 8f, width * 0.42f - 8f, 30f), "World recipes / installed..."))
                Find.WindowStack.Add(new FloatMenu(installed.Select(p => new FloatMenuOption((p.builtInRecipe ? "Recipe: " : "Installed: ") + p.name, delegate { editor.SelectInstalled(p); ChangedSelection(); })).ToList()));
            GUI.enabled = enabled;
            Widgets.Label(new Rect(8f, rect.y + 48f, 55f, 30f), "Name");
            GUI.enabled = enabled && CanRenamePreset;
            Active.name = Widgets.TextField(new Rect(66f, rect.y + 46f, width - 326f, 30f), Active.name ?? "");
            GUI.enabled = enabled;
            if (!CanRenamePreset) TooltipHandler.TipRegion(new Rect(66f, rect.y + 46f, width - 326f, 30f), "Copy an installed preset into a personal slot before renaming it. Its source identity stays unchanged.");
            if (Widgets.ButtonText(new Rect(width - 250f, rect.y + 46f, 116f, 30f), "Duplicate...")) CopyMenu(false);
            if (Widgets.ButtonText(new Rect(width - 126f, rect.y + 46f, 118f, 30f), "Reset preset"))
                Confirm("Reset this draft's controls to their normal values? Saved presets are not changed until Save preset.", delegate { editor.ResetAll(); numericBuffers.Clear(); status = "Draft reset; not applied or saved."; });
        }

        private void ChangedSelection() { numericBuffers.Clear(); scroll = Vector2.zero; status = "Draft selected; no settings were applied or saved."; }
        private void SwitchPersonalPreset(int slot) { editor.SelectPersonal(slot); ChangedSelection(); }
        private void SelectTab(int selected) { if (tab != selected) { tab = selected; scroll = Vector2.zero; viewHeight = 850f; } }
        private void OpenFactions()
        {
            if (!Find.WindowStack.IsOpen<Dialog_WorldFactions>()) Find.WindowStack.Add(new Dialog_WorldFactions(this));
        }
        private void OpenBiomeSelection()
        {
            if (!Find.WindowStack.IsOpen<Dialog_BiomeSelection>()) Find.WindowStack.Add(new Dialog_BiomeSelection(editor));
        }
        private static void Confirm(string text, Action action) { Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, action)); }

        private void CopyMenu(bool save)
        {
            List<FloatMenuOption> choices = new List<FloatMenuOption>();
            int index = 0;
            foreach (WorldPreset preset in editor.PersonalDrafts)
            {
                int slot = index++;
                if (!editor.IsInstalled && slot == editor.Slot) continue;
                string label = "Slot " + (slot + 1) + ": " + preset.name;
                choices.Add(new FloatMenuOption(label, delegate
                {
                    Confirm("Replace the draft in " + label + " with a copy?" + (save ? " This will also overwrite that saved personal preset." : " It will not be saved until you choose Save preset."), delegate
                    {
                        editor.CopyToPersonal(slot);
                        ChangedSelection();
                        if (save) SaveDraft();
                        else status = "Personal copy created; rename it and Save preset to keep it.";
                    });
                }));
            }
            Find.WindowStack.Add(new FloatMenu(choices));
        }

        private void SaveDraft()
        {
            if (editor.IsInstalled) { CopyMenu(true); return; }
            Normalize(Active);
            string error = BiomeSelection.Validate(Active);
            if (error != null) { Messages.Message(error, MessageTypeDefOf.RejectInput, false); return; }
            if (Active.selectedBiomesOnly && String.IsNullOrWhiteSpace(Active.seed)) Active.seed = (string)PageFields.Seed.GetValue(page);
            editor.Save(delegate { UnaidedWorldGeneratorMod.Settings.Write(); });
            status = "Personal preset saved. Use Apply to world to change this world setup.";
        }

        private void RequestApply()
        {
            if (Active.subdivisions >= 11 || Active.coverage >= 0.75f)
                Confirm("This setup uses high world detail or broad coverage and may take substantially more time and memory to generate. Apply it?", ApplyDraft);
            else ApplyDraft();
        }

        private void ApplyDraft()
        {
            Normalize(Active);
            string error = BiomeSelection.Validate(Active);
            if (error != null) { Messages.Message(error, MessageTypeDefOf.RejectInput, false); return; }
            ApplyToPage(page, Active);
            if (editor.IsInstalled) WorldPresetRegistry.Activate(Active);
            else { WorldPresetRegistry.Activate(null); WorldPresetRegistry.Apply(Active, editor.Slot); }
            status = "Draft applied to this world setup. It is not saved unless you choose Save preset.";
            Messages.Message("World controls applied. Use the normal Generate button when ready.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static void Normalize(WorldPreset preset)
        {
            preset.coverage = Finite(preset.coverage, 0.05f, 1f, 0.3f);
            preset.pollution = Finite(preset.pollution, 0f, 1f, 0.05f);
            preset.riverDensity = Finite(preset.riverDensity, 0f, 2f, 1f);
            preset.mountainDensity = Finite(preset.mountainDensity, 0f, 2f, 1f);
            preset.infestationDensity = Finite(preset.infestationDensity, 0f, 2f, 1f);
            preset.subdivisions = Mathf.Clamp(preset.subdivisions, 1, 11);
            for (int index = 0; index < preset.biomeWeights.Count; index++) preset.biomeWeights[index] = Finite(preset.biomeWeights[index], 0f, 3f, 1f);
        }

        private static float Finite(float value, float min, float max, float normal) { return Single.IsNaN(value) || Single.IsInfinity(value) ? normal : Mathf.Clamp(value, min, max); }

        private void Heading(float width, ref float y, string text, Action reset)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width - 180f, 32f), text);
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(width - 170f, y, 170f, 30f), "Reset this section"))
                Confirm("Reset this section of the draft? Saved and applied values stay unchanged.", delegate { reset(); numericBuffers.Clear(); });
            y += 42f;
        }

        private void DrawPlanet(float width, ref float y)
        {
            Heading(width, ref y, "Planet and starting conditions", editor.ResetPlanet);
            Widgets.Label(new Rect(0f, y, width * 0.3f, 30f), "World seed");
            Active.seed = Widgets.TextField(new Rect(width * 0.32f, y, width * 0.68f, 30f), Active.seed ?? "");
            TooltipHandler.TipRegion(new Rect(0f, y, width, 30f), "A saved seed is applied when you choose Apply to world. An empty seed keeps the vanilla page's current seed.");
            y += 42f;
            Rect hydro = new Rect(0f, y, width, 30f);
            Widgets.CheckboxLabeled(hydro, "Hydrophobia: dry starting tile and pawn arrival", ref Active.hydrophobia);
            TooltipHandler.TipRegion(hydro, "Reject water-covered starting world tiles and keep initial colonists and their arrival pods on walkable dry ground. Coastal and swamp maps remain allowed. This does not change terrain or later arrivals.");
            y += 42f;
            Active.coverage = FloatRow(width, ref y, "coverage", "Planet coverage", Active.coverage * 100f, 5f, 100f, 30f, "%") / 100f;
            Active.subdivisions = Mathf.RoundToInt(FloatRow(width, ref y, "detail", "World detail (10 = normal)", Active.subdivisions, 1f, 11f, 10f, "", true));
            if (Active.subdivisions >= 11 || Active.coverage >= 0.75f)
            {
                Color old = GUI.color; GUI.color = Color.yellow;
                Widgets.Label(new Rect(0f, y, width, 42f), "Heavy generation: detail 11 and broad coverage can require substantially more time and memory.");
                GUI.color = old; y += 46f;
            }
            Active.riverDensity = FloatRow(width, ref y, "rivers", "River density", Active.riverDensity, 0f, 2f, 1f, "x");
            Active.mountainDensity = FloatRow(width, ref y, "mountains", "Mountain density", Active.mountainDensity, 0f, 2f, 1f, "x");
            Active.rainfall = EnumRow<OverallRainfall>(width, ref y, "Rainfall", Active.rainfall);
            Active.temperature = EnumRow<OverallTemperature>(width, ref y, "Temperature", Active.temperature);
            Active.population = EnumRow<OverallPopulation>(width, ref y, "Population", Active.population);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && ModsConfig.BiotechActive;
            Active.pollution = FloatRow(width, ref y, "pollution", ModsConfig.BiotechActive ? "Pollution coverage" : "Pollution (requires Biotech)", Active.pollution * 100f, 0f, 100f, 5f, "%") / 100f;
            GUI.enabled = enabled && ModsConfig.OdysseyActive;
            Active.landmarks = EnumRow<LandmarkDensity>(width, ref y, ModsConfig.OdysseyActive ? "Landmark density" : "Landmarks (requires Odyssey)", Active.landmarks);
            GUI.enabled = enabled && WorldControlUtility.HasInsectHiveFaction(Active);
            Active.infestationDensity = FloatRow(width, ref y, "insects", GUI.enabled ? "Insect incident frequency" : "Insect incidents (hive removed)", Active.infestationDensity, 0f, 2f, 1f, "x");
            GUI.enabled = enabled;
            Widgets.Label(new Rect(0f, y, width, 48f), "Coverage changes the generated portion of the planet. Detail changes tile subdivision. Insect frequency affects ongoing infestation incidents, not the planet's shape.");
            y += 52f;
        }

        private float FloatRow(float width, ref float y, string key, string label, float value, float min, float max, float normal, string suffix, bool integer = false)
        {
            value = Finite(value, min, max, normal);
            Rect labelRect = new Rect(0f, y + 3f, width * 0.3f, 30f);
            Widgets.Label(labelRect, label.Truncate(labelRect.width));
            TooltipHandler.TipRegion(labelRect, label + ". Normal: " + normal.ToString("0.##") + suffix + ". Range: " + min + " to " + max + ". Type a value or use the slider.");
            float previous = value;
            value = Widgets.HorizontalSlider(new Rect(width * 0.32f, y + 5f, width * 0.43f, 28f), value, min, max, true, roundTo: integer ? 1f : -1f);
            if (integer) value = Mathf.Round(value);
            string buffer;
            if (!numericBuffers.TryGetValue(key, out buffer) || value != previous) buffer = value.ToString("0.##");
            Widgets.TextFieldNumeric(new Rect(width * 0.77f, y, width * 0.13f, 30f), ref value, ref buffer, min, max);
            numericBuffers[key] = buffer;
            Widgets.Label(new Rect(width * 0.905f, y + 3f, width * 0.035f, 30f), suffix);
            if (Widgets.ButtonText(new Rect(width * 0.945f, y, width * 0.055f, 30f), "↺")) { value = normal; numericBuffers.Remove(key); }
            y += 42f;
            value = Finite(value, min, max, normal);
            return integer ? Mathf.Round(value) : value;
        }

        private static int EnumRow<T>(float width, ref float y, string label, int raw) where T : struct
        {
            Array values = Enum.GetValues(typeof(T));
            int selected = Array.IndexOf(values, (T)Enum.ToObject(typeof(T), raw));
            if (selected < 0) selected = 0;
            Widgets.Label(new Rect(0f, y + 3f, width * 0.45f, 30f), label);
            if (Widgets.ButtonText(new Rect(width * 0.48f, y, width * 0.52f, 30f), values.GetValue(selected).ToString()))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (object value in values)
                {
                    int choice = Convert.ToInt32(value);
                    options.Add(new FloatMenuOption(value.ToString(), delegate { SetEnum<T>(label, choice); }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += 42f;
            return Convert.ToInt32(values.GetValue(selected));
        }

        private static void SetEnum<T>(string label, int choice) where T : struct
        {
            Dialog_UnaidedWorldControls window = Find.WindowStack.WindowOfType<Dialog_UnaidedWorldControls>();
            if (window == null) return;
            if (typeof(T) == typeof(OverallRainfall)) window.Active.rainfall = choice;
            else if (typeof(T) == typeof(OverallTemperature)) window.Active.temperature = choice;
            else if (typeof(T) == typeof(OverallPopulation)) window.Active.population = choice;
            else if (typeof(T) == typeof(LandmarkDensity)) window.Active.landmarks = choice;
        }

        internal void DrawFactions(float width, ref float y)
        {
            Heading(width, ref y, "Faction instances", editor.ResetFactions);
            Widgets.Label(new Rect(0f, y, width, 36f), "Counts are separate factions, not settlements or population. Only definitions from loaded mods are available.");
            y += 44f;
            Widgets.Label(new Rect(0f, y + 3f, 65f, 30f), "Search");
            factionSearch = Widgets.TextField(new Rect(70f, y, width - 70f, 30f), factionSearch);
            y += 42f;
            List<FactionDef> candidates = FactionCandidates();
            List<FactionDef> visible = candidates.Where(f => BiomeCatalog.Matches(f, factionSearch, "")).ToList();
            Widgets.Label(new Rect(0f, y, width - 180f, 30f), visible.Count + " of " + candidates.Count + " faction types");
            if (Widgets.ButtonText(new Rect(width - 170f, y, 170f, 30f), "One of each"))
                Confirm("Set one instance of every available faction type in this draft? This does not apply or save the change.", delegate { Active.factionNames = candidates.Select(f => f.defName).ToList(); Active.factionsInitialized = true; });
            y += 42f;
            foreach (FactionDef faction in visible)
            {
                Color old = GUI.color;
                GUI.color = faction.DefaultColor;
                Widgets.DrawTextureFitted(new Rect(0f, y, 28f, 28f), faction.FactionIcon, 1f);
                GUI.color = old;
                Rect label = new Rect(38f, y, width - 210f, 26f);
                Widgets.Label(label, faction.LabelCap.ToString().Truncate(label.width));
                TooltipHandler.TipRegion(label, faction.LabelCap + "\n" + BiomeCatalog.Provider(faction) + "\n" + faction.description);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(38f, y + 25f, width - 210f, 22f), BiomeCatalog.Provider(faction));
                Text.Font = GameFont.Small;
                int count = Active.factionNames.Count(name => name == faction.defName);
                if (Widgets.ButtonText(new Rect(width - 152f, y, 40f, 32f), "−")) SetFactionCount(faction.defName, Math.Max(0, count - 1));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(width - 108f, y, 60f, 32f), count.ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonText(new Rect(width - 40f, y, 40f, 32f), "+")) SetFactionCount(faction.defName, Math.Min(12, count + 1));
                y += 54f;
            }
            if (visible.Count == 0) { Widgets.Label(new Rect(0f, y, width, 40f), "No factions match this search."); y += 44f; }
        }

        private void SetFactionCount(string name, int count)
        {
            Active.factionNames.RemoveAll(value => value == name);
            for (int index = 0; index < count; index++) Active.factionNames.Add(name);
            Active.factionsInitialized = true;
        }

        private List<FactionDef> FactionCandidates()
        {
            return InitialFactionList().Concat(FactionGenerator.ConfigurableFactions).Concat(Active.factionNames.Select(n => DefDatabase<FactionDef>.GetNamedSilentFail(n)))
                .Where(f => f != null).GroupBy(f => f.defName).Select(g => g.First()).OrderBy(f => f.LabelCap.ToString()).ToList();
        }

        private void DrawBiomes(float width, ref float y)
        {
            Heading(width, ref y, "Biome frequency", editor.ResetBiomes);
            if (Widgets.ButtonText(new Rect(0f, y, width, 34f), "Selected biomes only... (" + (Active.selectedBiomesOnly ? Active.selectedBiomeNames.Count + " selected" : "strict mode off") + ")")) OpenBiomeSelection();
            y += 42f;
            Widgets.Label(new Rect(0f, y, width, 44f), "Weights affect eligible natural land biomes: 0 disables their natural score, 1 is normal, 3 is more common. Water and special maps are not world-frequency controls.");
            y += 52f;
            Widgets.Label(new Rect(0f, y, width, 44f), "Anomaly surface variants are custom, settleable adaptations. They default to 0 (off); original Anomaly special maps remain separate.");
            y += 52f;
            Widgets.Label(new Rect(0f, y + 3f, 65f, 30f), "Search");
            biomeSearch = Widgets.TextField(new Rect(70f, y, width * 0.57f - 70f, 30f), biomeSearch);
            if (Widgets.ButtonText(new Rect(width * 0.59f, y, width * 0.41f, 30f), String.IsNullOrEmpty(provider) ? "All providers" : provider))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption> { new FloatMenuOption("All providers", delegate { provider = ""; scroll = Vector2.zero; }) };
                options.AddRange(BiomeCatalog.Providers().Select(p => new FloatMenuOption(p, delegate { provider = p; scroll = Vector2.zero; })));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += 42f;
            List<BiomeDef> biomes = BiomeCatalog.Natural(biomeSearch, provider);
            bool specialOnly = !String.IsNullOrEmpty(provider) && biomes.Count == 0;
            if (specialOnly) Widgets.Label(new Rect(0f, y, width, 30f), "Provider details: special maps are listed as information only.");
            else Widgets.CheckboxLabeled(new Rect(0f, y, width, 30f), "Show special-map biomes as information", ref showSpecial);
            y += 40f;
            Widgets.Label(new Rect(0f, y, width, 30f), biomes.Count + " editable land biomes shown. Alpha Biomes and other loaded providers are discovered automatically.");
            y += 38f;
            foreach (BiomeDef biome in biomes)
            {
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0f, y, width, 20f), BiomeCatalog.Provider(biome));
                Text.Font = GameFont.Small;
                y += 22f;
                int index = Active.biomeNames.IndexOf(biome.defName);
                float normal = BiomeCatalog.DefaultWeight(biome);
                float before = index >= 0 && index < Active.biomeWeights.Count ? Active.biomeWeights[index] : normal;
                float after = FloatRow(width, ref y, "biome:" + biome.defName, biome.LabelCap.ToString(), before, 0f, 3f, normal, "x");
                if (after != before)
                {
                    if (index < 0) { Active.biomeNames.Add(biome.defName); Active.biomeWeights.Add(after); }
                    else { while (Active.biomeWeights.Count <= index) Active.biomeWeights.Add(1f); Active.biomeWeights[index] = after; }
                }
            }
            if (biomes.Count == 0) { Widgets.Label(new Rect(0f, y, width, 42f), "No natural land biomes match. Special-map providers may have no editable world-frequency biomes."); y += 50f; }
            if (showSpecial || specialOnly)
            {
                foreach (BiomeDef biome in BiomeCatalog.Special(biomeSearch, provider))
                {
                    Widgets.Label(new Rect(0f, y, width, 26f), biome.LabelCap + " - " + BiomeCatalog.Provider(biome));
                    Widgets.Label(new Rect(0f, y + 27f, width, 30f), "Special map: controlled by its scenario, quest or layer, not surface-world frequency.");
                    y += 64f;
                }
            }
        }

        private List<FactionDef> InitialFactionList() { return (List<FactionDef>)PageFields.InitialFactions.GetValue(page) ?? new List<FactionDef>(); }

        private static void ApplyToPage(Page_CreateWorldParams page, WorldPreset preset)
        {
            if (!String.IsNullOrWhiteSpace(preset.seed)) PageFields.Seed.SetValue(page, preset.seed.Trim());
            PageFields.Coverage.SetValue(page, preset.coverage);
            PageFields.Pollution.SetValue(page, preset.pollution);
            PageFields.Rainfall.SetValue(page, (OverallRainfall)preset.rainfall);
            PageFields.Temperature.SetValue(page, (OverallTemperature)preset.temperature);
            PageFields.Population.SetValue(page, (OverallPopulation)preset.population);
            PageFields.Landmarks.SetValue(page, (LandmarkDensity)preset.landmarks);
            List<FactionDef> factions = (List<FactionDef>)PageFields.Factions.GetValue(page);
            if (factions == null || !preset.factionsInitialized) return;
            factions.Clear();
            factions.AddRange(preset.factionNames.Select(n => DefDatabase<FactionDef>.GetNamedSilentFail(n)).Where(f => f != null));
        }
    }

    internal sealed class Dialog_WorldFactions : Window
    {
        private readonly Dialog_UnaidedWorldControls owner;
        private Vector2 scroll;
        private float viewHeight = 1000f;

        public override Vector2 InitialSize
        {
            get { return new Vector2(Mathf.Min(1000f, Mathf.Max(360f, UI.screenWidth - 40f)), Mathf.Min(820f, Mathf.Max(380f, UI.screenHeight - 40f))); }
        }

        internal Dialog_WorldFactions(Dialog_UnaidedWorldControls owner)
        {
            this.owner = owner;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width - 32f, 34f), "World factions");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, inRect.width, 44f), "Use - and + beside a faction to remove or add instances. Changes stay in the current world-controls draft.");
            Rect outer = new Rect(0f, 92f, inRect.width, Mathf.Max(40f, inRect.height - 186f));
            Rect view = new Rect(0f, 0f, outer.width - 20f, viewHeight);
            Widgets.BeginScrollView(outer, ref scroll, view);
            float y = 4f;
            owner.DrawFactions(view.width, ref y);
            viewHeight = Mathf.Max(outer.height, y + 16f);
            Widgets.EndScrollView();
            Widgets.Label(new Rect(0f, inRect.height - 84f, inRect.width, 38f), "Return to world controls to Apply to world or Save preset. Closing this faction window keeps your draft edits.");
            if (Widgets.ButtonText(new Rect(inRect.width - 180f, inRect.height - 40f, 180f, 38f), "Done")) Close();
        }
    }
}
