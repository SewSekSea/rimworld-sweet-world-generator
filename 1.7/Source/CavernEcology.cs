using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class MapComponent_CavernEcology : MapComponent
    {
        private bool initialized;
        private bool habitableEcology;
        private IntVec3 sanctuary;
        public MapComponent_CavernEcology(Map map) : base(map) { }
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref initialized, "oasisEcologyInitialized", false); Scribe_Values.Look(ref habitableEcology, "habitableCavernEcology", false); Scribe_Values.Look(ref sanctuary, "cavernEcologySanctuary"); }
        public static bool Habitat(Map map, IntVec3 cell)
        {
            return cell.InBounds(map) && cell.GetTerrain(map).defName == "UGWG_OasisSoil" && cell.Standable(map) && cell.GetEdifice(map) == null && !cell.GetThingList(map).Any(t => t is Building);
        }
        private void PlantAt(string name, IntVec3 cell, float growth)
        {
            if (!Habitat(map, cell) || cell.GetPlant(map) != null) return;
            var plant = (Plant)GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name)), cell, map);
            plant.Growth = growth;
        }
        private bool Undisturbed(IntVec3 cell)
        {
            return Habitat(map, cell) && map.zoneManager.ZoneAt(cell) == null && !cell.GetThingList(map).Any(t => t is Blueprint || t is Frame || t is Building);
        }
        private void SeedHabitat(string name, int target, float spacing, float growth)
        {
            var existing = map.listerThings.AllThings.Where(t => t.def.defName == name).Select(t => t.Position).ToList();
            foreach (IntVec3 cell in map.GetComponent<MapComponent_CavernOasis>().Bounds.Cells.InRandomOrder())
            {
                if (existing.Count >= target) break;
                if (!Undisturbed(cell) || (cell - sanctuary).LengthHorizontalSquared < 64 || existing.Any(c => (c - cell).LengthHorizontalSquared < spacing * spacing)) continue;
                Plant plant = cell.GetPlant(map);
                if (plant != null && plant.def.defName != "UGWG_OasisGrass") continue;
                plant?.Destroy(DestroyMode.Vanish);
                PlantAt(name, cell, growth);
                existing.Add(cell);
            }
        }
        public override void MapGenerated()
        {
            if (initialized || !MapComponent_CavernOasis.IsCavern(map)) return;
            CellRect oasis = map.GetComponent<MapComponent_CavernOasis>().Bounds;
            if (oasis.Width < 1) return;
            sanctuary = MapGenerator.PlayerStartSpot;
            var crystals = new List<IntVec3>();
            foreach (IntVec3 cell in oasis.Cells.Where(c => Habitat(map, c)).InRandomOrder())
            {
                float spacing = Rand.Range(7f, 9f);
                if ((cell - MapGenerator.PlayerStartSpot).LengthHorizontalSquared < 12 || crystals.Any(c => (c - cell).LengthHorizontalSquared < spacing * spacing)) continue;
                Plant existing = cell.GetPlant(map);
                existing?.Destroy(DestroyMode.Vanish);
                GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("UGWG_OasisCrystal")), cell, map);
                crystals.Add(cell);
            }
            double grassPhase = Rand.Range(0f, 6.28f), fungusPhase = Rand.Range(0f, 6.28f);
            foreach (IntVec3 cell in oasis.Cells)
            {
                if (!Habitat(map, cell) || crystals.Contains(cell)) continue;
                double patch = (Math.Sin(cell.x * 0.21 + cell.z * 0.08 + grassPhase) + Math.Cos(cell.z * 0.27 - cell.x * 0.11 - grassPhase)) * 0.25 + 0.5;
                double fungus = (Math.Sin(cell.x * 0.18 - cell.z * 0.13 + fungusPhase) + Math.Cos(cell.z * 0.23 + fungusPhase)) * 0.25 + 0.5;
                if (Rand.Chance((float)(0.025 + fungus * 0.13))) PlantAt("UGWG_OasisFungus", cell, Rand.Range(0.45f, 1f));
                else if (Rand.Chance((float)(0.2 + patch * 0.65))) PlantAt("UGWG_OasisGrass", cell, Rand.Range(0.4f, 1f));
            }
            var dry = oasis.Cells.Where(c => Habitat(map, c) && c.GetFirstPawn(map) == null).InRandomOrder().Take(4).ToList();
            foreach (IntVec3 cell in dry) GenSpawn.Spawn(PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Hare")), cell, map);
            int soilCount = oasis.Cells.Count(c => c.GetTerrain(map).defName == "UGWG_OasisSoil");
            SeedHabitat("UGWG_OasisTree", Math.Max(8, soilCount / 40), 4f, 0.85f);
            SeedHabitat("UGWG_OasisHerb", Math.Max(6, soilCount / 150), 3f, 0.9f);
            var herdCells = oasis.Cells.Where(c => Habitat(map, c) && c.GetFirstPawn(map) == null).InRandomOrder().Take(2).ToList();
            for (int i = 0; i < herdCells.Count; i++)
            {
                bool domestic = map.ParentFaction == Faction.OfPlayer && Find.GameInitData != null && !CavernScenario.Reclamation;
                Pawn muffalo = PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("Muffalo"), domestic ? Faction.OfPlayer : null, fixedGender: i == 0 ? Gender.Female : Gender.Male, fixedBiologicalAge: 4f));
                GenSpawn.Spawn(muffalo, herdCells[i], map);
            }
            habitableEcology = true;
            initialized = true;
        }
        public override void MapComponentTick()
        {
            if (!initialized || !MapComponent_CavernOasis.IsCavern(map) || (Find.TickManager.TicksGame + map.uniqueID) % 2000 != 0) return;
            CellRect oasis = map.GetComponent<MapComponent_CavernOasis>().Bounds;
            int limit = oasis.Cells.Count(c => c.GetTerrain(map).defName == "UGWG_OasisSoil") / 2;
            if (habitableEcology && (Find.TickManager.TicksGame + map.uniqueID) % 20000 == 0)
            {
                int trees = map.listerThings.AllThings.Count(t => t.def.defName == "UGWG_OasisTree");
                SeedHabitat("UGWG_OasisTree", Math.Min(trees + 1, Math.Max(8, limit / 20)), 4f, 0.05f);
                int herbs = map.listerThings.AllThings.Count(t => t.def.defName == "UGWG_OasisHerb");
                SeedHabitat("UGWG_OasisHerb", Math.Min(herbs + 1, Math.Max(6, limit / 75)), 3f, 0.05f);
            }
            if (map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).Count(p => oasis.Contains(p.Position) && (p.def.defName == "UGWG_OasisGrass" || p.def.defName == "UGWG_OasisFungus")) >= limit) return;
            IntVec3 cell;
            if (!oasis.Cells.Where(c => Habitat(map, c) && c.GetPlant(map) == null).TryRandomElement(out cell)) return;
            PlantAt(Rand.Chance(0.15f) ? "UGWG_OasisFungus" : "UGWG_OasisGrass", cell, 0.1f);
        }
    }
}
