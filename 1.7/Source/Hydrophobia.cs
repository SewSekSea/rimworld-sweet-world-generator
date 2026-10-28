using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class GameComponent_Hydrophobia : GameComponent
    {
        private bool enabled;
        private int mapId = -1;
        private List<string> pendingPawnIds = new List<string>();

        public GameComponent_Hydrophobia(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "hydrophobiaInitialArrival", false);
            Scribe_Values.Look(ref mapId, "hydrophobiaInitialMap", -1);
            Scribe_Collections.Look(ref pendingPawnIds, "hydrophobiaPendingPawns", LookMode.Value);
            if (pendingPawnIds == null) pendingPawnIds = new List<string>();
        }

        public void Prepare(Map map, IEnumerable<Pawn> pawns, bool protect)
        {
            if (!protect || map == null || pawns == null) return;
            if (mapId != map.uniqueID) pendingPawnIds.Clear();
            mapId = map.uniqueID;
            enabled = true;
            foreach (Pawn pawn in pawns)
                if (pawn != null && !pawn.Spawned && !pendingPawnIds.Contains(pawn.ThingID))
                    pendingPawnIds.Add(pawn.ThingID);
        }

        internal bool Pending(Pawn pawn, Map map)
        {
            return enabled && map != null && map.uniqueID == mapId && pawn != null && pendingPawnIds.Contains(pawn.ThingID);
        }

        internal void Arrived(Pawn pawn, Map map)
        {
            if (Pending(pawn, map) && pawn.Spawned && pawn.Map == map && !pawn.Position.GetTerrain(map).IsWater)
                pendingPawnIds.Remove(pawn.ThingID);
            if (pendingPawnIds.Count == 0) enabled = false;
        }
    }

    internal static class HydrophobiaUtility
    {
        internal static bool Enabled { get { return BiomeScorePatch.CurrentPreset?.hydrophobia == true; } }
        internal static bool StartingTileAllowed(Tile tile) { return !Enabled || (tile != null && !tile.WaterCovered); }
        internal static bool InitialMap(Map map)
        {
            return Enabled && map != null && Find.GameInitData != null && map.Tile == Find.GameInitData.startingTile;
        }

        internal static bool TryFindDryCell(Map map, IntVec3 preferred, out IntVec3 result, bool forPod = false)
        {
            result = IntVec3.Invalid;
            if (map == null) return false;
            if (Suitable(map, preferred, forPod)) { result = preferred; return true; }
            int best = Int32.MaxValue;
            foreach (IntVec3 cell in map.AllCells)
            {
                int distance = (cell - preferred).LengthHorizontalSquared;
                if (distance >= best || !Suitable(map, cell, forPod)) continue;
                result = cell;
                best = distance;
            }
            return result.IsValid;
        }

        private static bool Suitable(Map map, IntVec3 cell, bool forPod)
        {
            if (!cell.InBounds(map)) return false;
            TerrainDef terrain = cell.GetTerrain(map);
            return terrain != null && !terrain.IsWater && cell.Standable(map) && cell.GetFirstPawn(map) == null &&
                (!forPod || DropCellFinder.IsGoodDropSpot(cell, map, true, false, false));
        }

        internal static IntVec3 RequireDryCell(Map map, IntVec3 preferred, bool forPod = false)
        {
            IntVec3 result;
            if (TryFindDryCell(map, preferred, out result, forPod)) return result;
            throw new InvalidOperationException("Hydrophobia could not find safe dry ground for the initial colony arrival. Choose another starting site or disable Hydrophobia. No terrain was changed.");
        }

        internal static GameComponent_Hydrophobia State { get { return Current.Game?.GetComponent<GameComponent_Hydrophobia>(); } }
    }

    [HarmonyPatch(typeof(Page_SelectStartingSite), "CanDoNext")]
    internal static class HydrophobiaStartingTilePatch
    {
        internal static void Postfix(ref bool __result)
        {
            if (!__result || !HydrophobiaUtility.Enabled) return;
            PlanetTile tile = Find.WorldInterface.SelectedTile;
            if (tile.Valid && !HydrophobiaUtility.StartingTileAllowed(tile.Tile))
            {
                Messages.Message("Hydrophobia requires a non-water starting tile. Coastal and swamp land tiles are allowed.", MessageTypeDefOf.RejectInput, false);
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(GenStep_FindPlayerStartSpot), "Generate")]
    internal static class HydrophobiaStartSpotPatch
    {
        internal static void Postfix(Map map)
        {
            if (HydrophobiaUtility.InitialMap(map))
                MapGenerator.PlayerStartSpot = HydrophobiaUtility.RequireDryCell(map, MapGenerator.PlayerStartSpot);
        }
    }

    [HarmonyPatch(typeof(ScenPart_PlayerPawnsArriveMethod), "GenerateIntoMap")]
    internal static class HydrophobiaInitialMapPatch
    {
        internal static void Prefix(Map map)
        {
            if (!HydrophobiaUtility.InitialMap(map)) return;
            if (!HydrophobiaUtility.StartingTileAllowed(map.Tile.Tile))
                throw new InvalidOperationException("Hydrophobia requires a non-water starting tile. Choose a land tile before starting the colony.");
            MapGenerator.PlayerStartSpot = HydrophobiaUtility.RequireDryCell(map, MapGenerator.PlayerStartSpot);
        }
    }

    [HarmonyPatch]
    internal static class HydrophobiaInitialPayloadPatch
    {
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ScenPart_PlayerPawnsArriveMethod), "DoDropPods");
            yield return AccessTools.Method(typeof(ScenPart_PlayerPawnsArriveMethod), "DoGravship");
        }

        internal static void Prefix(Map __0, List<Thing> __1)
        {
            if (!HydrophobiaUtility.InitialMap(__0)) return;
            HydrophobiaUtility.State?.Prepare(__0, Find.GameInitData.startingAndOptionalPawns.Concat(__1.OfType<Pawn>()), true);
        }
    }

    [HarmonyPatch(typeof(DropPodUtility), "MakeDropPodAt")]
    internal static class HydrophobiaDropPodPatch
    {
        internal static void Prefix(ref IntVec3 c, Map map, ActiveTransporterInfo info)
        {
            GameComponent_Hydrophobia state = HydrophobiaUtility.State;
            if (state == null || info == null || !info.innerContainer.OfType<Pawn>().Any(p => state.Pending(p, map))) return;
            c = HydrophobiaUtility.RequireDryCell(map, c, true);
        }
    }

    [HarmonyPatch(typeof(GenSpawn), "Spawn", new Type[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]
    internal static class HydrophobiaPawnSpawnPatch
    {
        internal static void Prefix(Thing newThing, ref IntVec3 loc, Map map, bool respawningAfterLoad)
        {
            Pawn pawn = newThing as Pawn;
            if (pawn == null || respawningAfterLoad || HydrophobiaUtility.State?.Pending(pawn, map) != true) return;
            loc = HydrophobiaUtility.RequireDryCell(map, loc);
        }

        internal static void Postfix(Thing newThing, Map map, bool respawningAfterLoad)
        {
            Pawn pawn = newThing as Pawn;
            if (pawn != null && !respawningAfterLoad) HydrophobiaUtility.State?.Arrived(pawn, map);
        }
    }
}
