using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    internal static class CavernResources
    {
        internal static void Generate(Map map, CavernPlan plan)
        {
            Lava(map, plan);
            var walls = map.AllCells.Where(c => !plan.open.Contains(c) && c.GetEdifice(map)?.def.building?.isNaturalRock == true && !plan.oasis.ExpandedBy(3).Contains(c) && GenAdj.CardinalDirections.Any(d => plan.open.Contains(c + d))).InRandomOrder().ToList();
            var used = new List<IntVec3>();
            foreach (string resource in new[] { "MineableSteel", "MineableSteel", "MineableComponentsIndustrial", "MineableSteel", "MineableComponentsIndustrial", "MineableSilver", "MineablePlasteel", "MineableUranium", "MineableGold", "MineableJade" })
            {
                IntVec3 seed = walls.FirstOrDefault(c => used.All(u => (c - u).LengthHorizontalSquared >= 64), IntVec3.Invalid);
                if (!seed.IsValid) continue;
                ThingDef def = DefDatabase<ThingDef>.GetNamed(resource);
                var deposit = new HashSet<IntVec3> { seed };
                var queue = new Queue<IntVec3>();
                queue.Enqueue(seed);
                int target = resource == "MineableSteel" ? 20 : resource == "MineableComponentsIndustrial" ? 8 : 6;
                while (queue.Count > 0 && deposit.Count < target)
                {
                    IntVec3 cell = queue.Dequeue();
                    foreach (IntVec3 direction in GenAdj.CardinalDirections.InRandomOrder())
                    {
                        IntVec3 next = cell + direction;
                        if (!next.InBounds(map) || plan.open.Contains(next) || plan.oasis.ExpandedBy(3).Contains(next) || next.GetEdifice(map)?.def.building?.isNaturalRock != true || !deposit.Add(next)) continue;
                        queue.Enqueue(next);
                        if (deposit.Count >= target) break;
                    }
                }
                foreach (IntVec3 cell in deposit) GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map, WipeMode.Vanish);
                used.Add(seed);
            }
            IntVec3 geyser;
            if (plan.chamber.Where(c => !plan.water.Contains(c) && (c - plan.start).LengthHorizontalSquared > 100 && (c - plan.entrance).LengthHorizontalSquared > 100 && new CellRect(c.x - 2, c.z - 2, 5, 5).Cells.All(n => plan.chamber.Contains(n) && !plan.water.Contains(n))).TryRandomElement(out geyser))
                GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SteamGeyser")), geyser, map);
        }

        private static void Lava(Map map, CavernPlan plan)
        {
            TerrainDef lava = ModsConfig.OdysseyActive ? DefDatabase<TerrainDef>.GetNamed("LavaDeep") : DefDatabase<TerrainDef>.GetNamed("UGWG_CavernMagma");
            foreach (List<IntVec3> cells in CavernSurvivalPlan.LavaPools(plan, map.Size, Rand.Int))
            {
                IntVec3 center = cells[cells.Count / 2];
                foreach (IntVec3 cell in CavernSurvivalPlan.LavaLedge(center))
                    if (!plan.open.Contains(cell)) cell.GetEdifice(map)?.Destroy(DestroyMode.Vanish);
                foreach (IntVec3 cell in cells)
                {
                    cell.GetEdifice(map)?.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(cell, lava);
                }
            }
        }
    }

    public static class CavernSurvivalPlan
    {
        public static List<IntVec3> LavaLedge(IntVec3 center)
        {
            var cells = new List<IntVec3>();
            for (int x = -11; x <= 11; x++) for (int z = -11; z <= 11; z++)
                if (x * x + z * z <= 121) cells.Add(center + new IntVec3(x, 0, z));
            return cells;
        }
        public static List<List<IntVec3>> LavaPools(CavernPlan plan, IntVec3 size, int seed)
        {
            var random = new Random(seed);
            var offsets = new List<IntVec3>();
            for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
                if (x * x + z * z <= 5) offsets.Add(new IntVec3(x, 0, z));
            var pools = new List<IntVec3>();
            var result = new List<List<IntVec3>>();
            var candidates = new List<IntVec3>();
            for (int x = 12; x < size.x - 12; x++) for (int z = 12; z < size.z - 12; z++)
            {
                var center = new IntVec3(x, 0, z);
                if (!plan.oasis.ExpandedBy(12).Contains(center) && (center - plan.outsideStart).LengthHorizontalSquared >= 144 && !plan.open.Contains(center)) candidates.Add(center);
            }
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                IntVec3 swap = candidates[i]; candidates[i] = candidates[j]; candidates[j] = swap;
            }
            foreach (IntVec3 center in candidates)
            {
                if (pools.Count >= Math.Max(1, size.x / 100)) break;
                if (pools.Any(c => (c - center).LengthHorizontalSquared < 676)) continue;
                var cells = offsets.Select(o => center + o).ToList();
                if (!cells.All(c => c.x >= 5 && c.z >= 5 && c.x < size.x - 5 && c.z < size.z - 5 && !plan.open.Contains(c))) continue;
                if (GenRadial.RadialCellsAround(center, 9.5f, true).Any(n => plan.open.Contains(n))) continue;
                var ledge = LavaLedge(center);
                if (!ledge.Any(c => plan.open.Contains(c)) || ledge.Any(c => c.x < 1 || c.z < 1 || c.x >= size.x - 1 || c.z >= size.z - 1 || plan.oasis.ExpandedBy(3).Contains(c) || c.InHorDistOf(plan.start, 6f) || c.InHorDistOf(plan.outsideStart, 6f))) continue;
                if (!SideOpening(ledge, plan.open, center)) continue;
                pools.Add(center);
                result.Add(cells);
            }
            return result;
        }
        private static bool SideOpening(List<IntVec3> ledge, HashSet<IntVec3> routes, IntVec3 center)
        {
            var mouth = new HashSet<IntVec3>();
            foreach (IntVec3 cell in ledge)
            {
                if (routes.Contains(cell)) mouth.Add(cell);
                foreach (IntVec3 direction in GenAdj.CardinalDirections) if (routes.Contains(cell + direction)) mouth.Add(cell + direction);
            }
            if (mouth.Count == 0) return false;
            var seen = new HashSet<IntVec3>(); var queue = new Queue<IntVec3>(); queue.Enqueue(mouth.First());
            while (queue.Count > 0)
            {
                IntVec3 cell = queue.Dequeue();
                if (!seen.Add(cell)) continue;
                foreach (IntVec3 direction in GenAdj.CardinalDirections) if (mouth.Contains(cell + direction) && !seen.Contains(cell + direction)) queue.Enqueue(cell + direction);
            }
            if (seen.Count != mouth.Count) return false;
            double x = mouth.Average(c => c.x - center.x), z = mouth.Average(c => c.z - center.z), length = Math.Sqrt(x * x + z * z);
            return length > 0 && mouth.All(c => ((c.x - center.x) * x + (c.z - center.z) * z) / length >= 8);
        }
    }
}
