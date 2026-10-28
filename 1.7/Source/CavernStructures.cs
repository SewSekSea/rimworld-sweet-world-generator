using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class CavernStructureDef : Def
    {
        public List<string> rows;
        public bool ruin;
    }

    internal static class CavernStructures
    {
        internal static List<CellRect> Generate(Map map, Faction faction)
        {
            bool insects = faction == Faction.OfInsects;
            string name = insects ? "UGWG_OasisRuin" : "UGWG_OasisLodge";
            CellRect bounds = map.GetComponent<MapComponent_CavernOasis>().Bounds;
            var lots = new List<CellRect>();
            foreach (string planName in new[] { name, name + "Compact" })
            {
                CavernStructureDef plan = DefDatabase<CavernStructureDef>.GetNamed(planName);
                foreach (IntVec3 origin in bounds.Cells.OrderBy(c => c.x).ThenBy(c => c.z))
                {
                    if (lots.Count >= (insects ? 1 : 2)) break;
                    var lot = new CellRect(origin.x, origin.z, plan.rows[0].Length, plan.rows.Count);
                    if (lots.Any(r => r.ExpandedBy(3).Overlaps(lot)) || !lot.ExpandedBy(1).Cells.All(c => c.InBounds(map) && c.GetTerrain(map).defName == "UGWG_OasisSoil" && c.Walkable(map) && c.GetThingList(map).All(t => t.def.passability == Traversability.Standable || t is Plant || t.def.defName == "UGWG_OasisCrystal") && c.GetEdifice(map) == null && !c.GetThingList(map).OfType<Building>().Any(b => b.def.defName != "UGWG_OasisCrystal"))) continue;
                    if (lot.ExpandedBy(6).Contains(MapGenerator.PlayerStartSpot) || lot.ExpandedBy(5).Contains(map.GetComponent<MapComponent_CavernOasis>().entrance)) continue;
                    if (lot.Cells.Any(c => c.GetThingList(map).Any(t => t is Pawn || (t.def.category == ThingCategory.Item) || t.def.defName == "SteamGeyser"))) continue;
                    foreach (IntVec3 cell in lot.Cells)
                        foreach (Thing thing in cell.GetThingList(map).ToList())
                            if (thing is Plant || thing.def.defName == "UGWG_OasisCrystal") thing.Destroy(DestroyMode.Vanish);
                    if (!TryFramework(map, faction, plan, lot)) Native(map, faction, plan, lot);
                    RoofDef ceiling = DefDatabase<RoofDef>.GetNamed("UGWG_CavernCeiling");
                    foreach (IntVec3 cell in lot.Cells) map.roofGrid.SetRoof(cell, ceiling);
                    foreach (Thing building in lot.Cells.SelectMany(c => c.GetThingList(map)).OfType<Building>().Distinct())
                    {
                        if (building.def.CanHaveFaction) building.SetFaction(faction);
                        building.TryGetComp<CompRefuelable>()?.Refuel(20f);
                    }
                    lots.Add(lot);
                }
                if (lots.Count > 0) break;
            }
            if (lots.Count == 0) Log.Warning("[Sweet World Generator] No safe dry footprint was available for cavern structures on this map.");
            return lots;
        }

        private static bool TryFramework(Map map, Faction faction, CavernStructureDef plan, CellRect lot)
        {
            Type pieceType = AccessTools.TypeByName("CustomBaseFramework.PieceDef");
            Type contextType = AccessTools.TypeByName("CustomBaseFramework.StampContext");
            Type stamperType = AccessTools.TypeByName("CustomBaseFramework.PieceStamper");
            if (pieceType == null || contextType == null || stamperType == null) return false;
            Type database = typeof(DefDatabase<>).MakeGenericType(pieceType);
            object piece = AccessTools.Method(database, "GetNamedSilentFail").Invoke(null, new object[] { plan.defName + "_CBF" });
            if (piece == null) return false;
            object context = Activator.CreateInstance(contextType);
            AccessTools.Field(contextType, "map").SetValue(context, map);
            AccessTools.Field(contextType, "origin").SetValue(context, new IntVec3(lot.minX, 0, lot.minZ));
            AccessTools.Field(contextType, "rot").SetValue(context, Rot4.North);
            AccessTools.Field(contextType, "faction").SetValue(context, faction);
            AccessTools.Field(contextType, "seed").SetValue(context, Rand.Int);
            AccessTools.Field(contextType, "respectWater").SetValue(context, true);
            AccessTools.Method(stamperType, "Stamp", new[] { pieceType, contextType }).Invoke(null, new[] { piece, context });
            Log.Message("[Sweet World Generator] Custom Base Framework stamped " + plan.defName + " inside " + lot);
            return true;
        }

        private static void Native(Map map, Faction faction, CavernStructureDef plan, CellRect lot)
        {
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamed("Concrete");
            for (int z = 0; z < plan.rows.Count; z++) for (int x = 0; x < plan.rows[z].Length; x++)
            {
                IntVec3 cell = new IntVec3(lot.minX + x, 0, lot.minZ + z);
                map.terrainGrid.SetTerrain(cell, floor);
                string name = null;
                string material = "WoodLog";
                switch (plan.rows[z][x])
                {
                    case 'W': name = "Wall"; material = "BlocksGranite"; break;
                    case 'D': name = "Door"; break;
                    case 'B': name = "Bed"; break;
                    case 'T': name = "Table2x2c"; break;
                    case 'C': name = "DiningChair"; break;
                    case 'S': name = "FueledStove"; material = "Steel"; break;
                    case 'L': name = "TorchLamp"; break;
                }
                if (name == null) continue;
                ThingDef def = DefDatabase<ThingDef>.GetNamed(name);
                Thing thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? DefDatabase<ThingDef>.GetNamed(material) : null);
                if (thing.def.CanHaveFaction) thing.SetFactionDirect(faction);
                GenSpawn.Spawn(thing, cell, map, name == "DiningChair" ? Rot4.East : Rot4.North);
            }
        }
    }
}
