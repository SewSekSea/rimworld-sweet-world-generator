using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class CavernBiome : DefModExtension { }

    public sealed class BiomeWorker_CavernOasis : BiomeWorker
    {
        public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
        {
            if (!ModsConfig.AnomalyActive || tile == null || tile.WaterCovered) return 0f;
            WorldPreset preset = BiomeScorePatch.CurrentPreset;
            int index = preset == null ? -1 : preset.biomeNames.IndexOf(biome.defName);
            if (index < 0 || index >= preset.biomeWeights.Count) return 0f;
            float weight = preset.biomeWeights[index];
            return Single.IsNaN(weight) || Single.IsInfinity(weight) || weight <= 0f ? 0f : 100f;
        }
    }

    [HarmonyPatch(typeof(Tile), "get_AllowRoofedEdgeWalkIn")]
    internal static class CavernEdgeEntryPatch
    {
        internal static void Postfix(Tile __instance, ref bool __result)
        {
            if (__instance.PrimaryBiome?.GetModExtension<CavernBiome>() != null) __result = true;
        }
    }

    [HarmonyPatch]
    internal static class CavernGatherSpotPatch
    {
        internal static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(RCellFinder), "TryFindRandomSpotJustOutsideColony", new[] { typeof(IntVec3), typeof(Map), typeof(Pawn), typeof(IntVec3).MakeByRefType(), typeof(Predicate<IntVec3>) });
        }
        internal static void Postfix(IntVec3 __0, Map __1, Pawn __2, ref IntVec3 __3, Predicate<IntVec3> __4, ref bool __result)
        {
            if (__result || !MapComponent_CavernOasis.IsCavern(__1)) return;
            CellRect bounds = __1.GetComponent<MapComponent_CavernOasis>().Bounds;
            IntVec3 preferred = new IntVec3(bounds.maxX - 3, 0, bounds.CenterCell.z);
            foreach (IntVec3 cell in bounds.Cells.OrderBy(c => (c - preferred).LengthHorizontalSquared))
            {
                if (!cell.Standable(__1) || cell.GetTerrain(__1).IsWater || cell.GetEdifice(__1) != null || (__4 != null && !__4(cell))) continue;
                TraverseParms travel = __2 == null ? (TraverseParms)TraverseMode.PassDoors : TraverseParms.For(__2, Danger.Deadly);
                if (__0.IsValid && !__1.reachability.CanReach(__0, cell, PathEndMode.OnCell, travel)) continue;
                __3 = cell; __result = true; return;
            }
        }
    }

    [HarmonyPatch(typeof(TradeShip), "get_CanTradeNow")]
    internal static class CavernOrbitalTradePatch
    {
        internal static void Postfix(TradeShip __instance, ref bool __result)
        {
            if (MapComponent_CavernOasis.IsCavern(__instance.Map)) __result = false;
        }
    }

    [HarmonyPatch(typeof(TradeShip), "TryOpenComms")]
    internal static class CavernOrbitalCommsPatch
    {
        internal static bool Prefix(TradeShip __instance)
        {
            if (!MapComponent_CavernOasis.IsCavern(__instance.Map)) return true;
            Messages.Message("Orbital deliveries cannot reach this deep cavern. Trade with visiting ground caravans instead.", MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }

    [HarmonyPatch(typeof(ScenPart_ScatterThings), "GenerateIntoMap")]
    internal static class CavernSkyScatterPatch
    {
        internal static bool Prefix(Map map, bool ___allowRoofed)
        {
            return !MapComponent_CavernOasis.IsCavern(map) || ___allowRoofed;
        }
    }

    [HarmonyPatch(typeof(ScenPart_PlayerPawnsArriveMethod), "DoDropPods")]
    internal static class CavernStandingArrivalPatch
    {
        internal static bool Prefix(Map __0, List<Thing> __1)
        {
            if (!MapComponent_CavernOasis.IsCavern(__0) || Find.GameInitData == null) return true;
            var payload = Find.GameInitData.startingAndOptionalPawns.Cast<Thing>().Concat(__1).Distinct().ToList();
            foreach (Thing thing in payload)
            {
                if (thing.Spawned) continue;
                IntVec3 cell = HydrophobiaUtility.RequireDryCell(__0, MapGenerator.PlayerStartSpot);
                if (thing.def.CanHaveFaction) thing.SetFactionDirect(Faction.OfPlayer);
                if (thing is Pawn) GenSpawn.Spawn(thing, cell, __0);
                else if (!GenPlace.TryPlaceThing(thing, cell, __0, ThingPlaceMode.Near, null, c => !c.GetTerrain(__0).IsWater))
                    throw new InvalidOperationException("Could not place cavern starting supply " + thing.def.defName + " on dry ground.");
            }
            return false;
        }
    }

    [HarmonyPatch]
    internal static class CavernMapGeneratorPatch
    {
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            return typeof(MapParent).Assembly.GetTypes().Where(t => typeof(MapParent).IsAssignableFrom(t))
                .Select(t => t.GetMethod("get_MapGeneratorDef", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(m => m != null && !m.IsAbstract).Distinct().Cast<MethodBase>();
        }

        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(MapParent __instance, ref MapGeneratorDef __result)
        {
            if (!ModsConfig.AnomalyActive || __instance == null || !__instance.Tile.Valid || __result?.pocketMapProperties != null) return;
            bool frameworkSettlement = __instance is Settlement && __result?.genSteps?.Any(s => s.genStep.GetType().Namespace == "CustomBaseFramework") == true;
            if (__result == null || (!frameworkSettlement && __result.defName != "Base_Player" && __result.defName != "Base_Faction" && __result.defName != "Encounter")) return;
            PlanetTile tile = __instance.Tile;
            if (tile.Layer.Def != PlanetLayerDefOf.Surface || tile.tileId < 0 || tile.tileId >= tile.Layer.TilesCount) return;
            if (tile.Layer[tile.tileId].PrimaryBiome?.GetModExtension<CavernBiome>() == null) return;
            MapGeneratorDef generator = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("UGWG_Cavern");
            if (generator != null) __result = generator;
        }
    }
}
