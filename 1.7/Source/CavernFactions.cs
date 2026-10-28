using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace UnaidedMistake.WorldGenerator
{
    [HarmonyPatch(typeof(SettlementDefeatUtility), "IsDefeated")]
    internal static class CavernInsectDefeatPatch
    {
        internal static bool Prefix(Map map, Faction faction, ref bool __result)
        {
            if (!MapComponent_CavernOasis.IsCavern(map) || faction != Faction.OfInsects) return true;
            __result = !map.mapPawns.SpawnedPawnsInFaction(faction).Any(p => GenHostility.IsActiveThreatToPlayer(p, false));
            return false;
        }
    }

    [HarmonyPatch(typeof(SettlementDefeatUtility), "HasAnyOtherBase")]
    internal static class CavernInsectContinuityPatch
    {
        internal static void Postfix(Settlement __0, ref bool __result)
        {
            if (__0.Faction == Faction.OfInsects && MapComponent_CavernOasis.IsCavern(__0.Map)) __result = true;
        }
    }

    public sealed class MapComponent_CavernOccupation : MapComponent
    {
        private bool initialized;
        public MapComponent_CavernOccupation(Map map) : base(map) { }
        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref initialized, "oasisOccupationInitialized", false); }
        public override void MapGenerated()
        {
            if (initialized || !MapComponent_CavernOasis.IsCavern(map)) return;
            bool expedition = map.ParentFaction == Faction.OfPlayer && Find.GameInitData != null && CavernScenario.Reclamation;
            Faction faction = expedition ? Faction.OfInsects : map.ParentFaction;
            if (faction != null && faction != Faction.OfPlayer)
            {
                CavernStructures.Generate(map, faction);
                CellRect bounds = map.GetComponent<MapComponent_CavernOasis>().Bounds;
                IntVec3 preferred = new IntVec3(bounds.minX + 7, 0, bounds.CenterCell.z);
                IntVec3 center = bounds.Cells.Where(c => MapComponent_CavernEcology.Habitat(map, c)).OrderBy(c => (c - preferred).LengthHorizontalSquared).ThenBy(c => c.z).ThenBy(c => c.x).First();
                List<Pawn> defenders;
                if (faction == Faction.OfInsects)
                    defenders = Enumerable.Range(0, 3).Select(i => PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Megascarab"), faction)).ToList();
                else
                {
                    var parms = new PawnGroupMakerParms { faction = faction, tile = map.Tile, groupKind = PawnGroupKindDefOf.Settlement, points = 350f, inhabitants = true };
                    PawnGroupMaker maker;
                    defenders = PawnGroupMakerUtility.TryGetRandomPawnGroupMaker(parms, out maker, false) ? PawnGroupMakerUtility.GeneratePawns(parms).ToList() : new List<Pawn>();
                    if (defenders.Count == 0 && faction.def.basicMemberKind != null)
                        defenders = Enumerable.Range(0, 3).Select(i => PawnGenerator.GeneratePawn(faction.def.basicMemberKind, faction)).ToList();
                }
                foreach (Pawn pawn in defenders)
                {
                    IntVec3 cell = bounds.Cells.Where(c => MapComponent_CavernEcology.Habitat(map, c) && c.GetFirstPawn(map) == null).OrderBy(c => (c - center).LengthHorizontalSquared).First();
                    GenSpawn.Spawn(pawn, cell, map);
                }
                if (defenders.Count > 0) LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(center, 4f, 12f, false, false), map, defenders);
            }
            if (expedition)
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                    if (pawn.equipment.Primary == null)
                        pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle")));
            initialized = true;
        }
    }

    public sealed class GameComponent_CavernSites : GameComponent
    {
        private bool initialized;
        public GameComponent_CavernSites(Game game) { }
        public override void ExposeData() { Scribe_Values.Look(ref initialized, "cavernWorldSitesInitialized", false); }
        public override void StartedNewGame()
        {
            Map map = Find.CurrentMap;
            if (initialized || !MapComponent_CavernOasis.IsCavern(map) || Faction.OfInsects == null) return;
            PlanetLayer layer = map.Tile.Layer;
            var tiles = Enumerable.Range(0, layer.TilesCount).Where(i => layer[i].PrimaryBiome.GetModExtension<CavernBiome>() != null)
                .Select(i => new PlanetTile(i, layer)).InRandomOrder().ToList();
            int number = 0;
            foreach (PlanetTile tile in tiles)
            {
                if (number >= 2) break;
                if (Find.WorldObjects.AnyWorldObjectAt(tile) || !TileFinder.IsValidTileForNewSettlement(tile) || !Find.WorldReachability.CanReach(map.Tile, tile)) continue;
                var settlement = (Settlement)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("Settlement"));
                settlement.Tile = tile; settlement.SetFaction(Faction.OfInsects); settlement.Name = "Insect-held cavern oasis " + (++number);
                Find.WorldObjects.Add(settlement);
            }
            if (number < 2) Messages.Message("Cavern exploration: " + number + " reachable insect-held oasis sites were placed. Remaining sites are unsuitable or unreachable in this world.", MessageTypeDefOf.NeutralEvent, false);
            initialized = true;
        }
    }
}
