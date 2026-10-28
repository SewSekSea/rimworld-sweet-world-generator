using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class MapComponent_CavernOasis : MapComponent
    {
        private int x, z, width, height;
        public IntVec3 entrance = IntVec3.Invalid;
        public CellRect Bounds { get { return new CellRect(x, z, width, height); } }
        public MapComponent_CavernOasis(Map map) : base(map) { }
        public static bool IsCavern(Map map) { return map?.Biome?.GetModExtension<CavernBiome>() != null; }
        public void Set(CellRect bounds, IntVec3 passage) { x = bounds.minX; z = bounds.minZ; width = bounds.Width; height = bounds.Height; entrance = passage; }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref x, "oasisX", 0); Scribe_Values.Look(ref z, "oasisZ", 0);
            Scribe_Values.Look(ref width, "oasisWidth", 0); Scribe_Values.Look(ref height, "oasisHeight", 0);
            Scribe_Values.Look(ref entrance, "oasisEntrance", IntVec3.Invalid);
        }
    }

    public sealed class CavernPlan
    {
        public CellRect oasis;
        public IntVec3 entrance;
        public IntVec3 start;
        public IntVec3 outsideStart;
        public HashSet<IntVec3> open = new HashSet<IntVec3>();
        public HashSet<IntVec3> chamber = new HashSet<IntVec3>();
        public HashSet<IntVec3> water = new HashSet<IntVec3>();
        public HashSet<IntVec3> deepWater = new HashSet<IntVec3>();
    }

    public static class CavernLayout
    {
        public static CavernPlan BuildPlan(IntVec3 size, int seed)
        {
            if (size.x < 100 || size.z < 100) throw new ArgumentOutOfRangeException("size", "Cavern-oasis maps require at least 100 cells on each side.");
            var random = new System.Random(seed);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                CavernPlan plan = Candidate(size, random);
                var dry = new HashSet<IntVec3>(plan.chamber.Except(plan.water));
                var travel = new HashSet<IntVec3>(plan.open.Except(plan.water));
                var mouth = new HashSet<IntVec3>(plan.chamber.SelectMany(c => GenAdj.CardinalDirections.Select(d => c + d)).Where(c => !plan.chamber.Contains(c) && plan.open.Contains(c)));
                if (dry.Count < 300 || plan.water.Count < 100 || !Connected(dry) || !Connected(plan.water) || !Connected(travel) || !Connected(mouth)) continue;
                if (!travel.Any(c => c.x == 0) || !travel.Any(c => c.z == 0) || !travel.Any(c => c.x == size.x - 1) || !travel.Any(c => c.z == size.z - 1)) continue;
                if (!RockReachesEdge(plan, size)) continue;
                IntVec3 lake = new IntVec3((int)plan.water.Average(c => c.x), 0, (int)plan.water.Average(c => c.z));
                IntVec3 desired = plan.oasis.CenterCell + (plan.oasis.CenterCell - lake) * 2;
                var arrivals = dry.Where(c => Clearing(dry, c)).OrderBy(c => (c - desired).LengthHorizontalSquared).ThenBy(c => c.z).ThenBy(c => c.x).ToList();
                if (arrivals.Count == 0 || !travel.Contains(plan.outsideStart) || plan.oasis.Contains(plan.outsideStart)) continue;
                plan.start = arrivals[0];
                return plan;
            }
            throw new InvalidOperationException("Unable to generate a connected single-passage cavern with sufficient dry farmland for this size and seed.");
        }

        private static bool Clearing(HashSet<IntVec3> dry, IntVec3 center)
        {
            for (int dx = -2; dx <= 2; dx++) for (int dz = -2; dz <= 2; dz++)
                if (!dry.Contains(center + new IntVec3(dx, 0, dz))) return false;
            return true;
        }

        private static bool Connected(HashSet<IntVec3> cells)
        {
            if (cells.Count == 0) return false;
            var visited = new HashSet<IntVec3>();
            var queue = new Queue<IntVec3>(); queue.Enqueue(cells.First()); visited.Add(queue.Peek());
            while (queue.Count > 0)
            {
                IntVec3 cell = queue.Dequeue();
                foreach (IntVec3 d in GenAdj.CardinalDirections)
                {
                    IntVec3 next = cell + d;
                    if (cells.Contains(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited.Count == cells.Count;
        }

        private static double Outline(double angle, double phase, double second)
        {
            return 1.0 + 0.12 * Math.Sin(3 * angle + phase) + 0.075 * Math.Sin(5 * angle + second) + 0.035 * Math.Cos(7 * angle - phase);
        }

        private static bool Inside(IntVec3 c, IntVec3 size) { return c.x >= 0 && c.z >= 0 && c.x < size.x && c.z < size.z; }

        private static bool RockReachesEdge(CavernPlan plan, IntVec3 size)
        {
            IntVec3 start = plan.chamber.SelectMany(c => GenAdj.CardinalDirections.Select(d => c + d)).First(c => !plan.open.Contains(c));
            var seen = new HashSet<IntVec3> { start }; var pending = new Queue<IntVec3>(); pending.Enqueue(start);
            while (pending.Count > 0)
            {
                IntVec3 c = pending.Dequeue();
                if (c.x == 0 || c.z == 0 || c.x == size.x - 1 || c.z == size.z - 1) return true;
                foreach (IntVec3 d in GenAdj.CardinalDirections)
                {
                    IntVec3 next = c + d;
                    if (Inside(next, size) && !plan.open.Contains(next) && seen.Add(next)) pending.Enqueue(next);
                }
            }
            return false;
        }

        private static void Line(IntVec3 from, IntVec3 to, Action<IntVec3, int> stroke, double phase)
        {
            int length = Math.Max(1, Math.Max(Math.Abs(to.x - from.x), Math.Abs(to.z - from.z)) * 2);
            for (int i = 0; i <= length; i++)
            {
                double t = (double)i / length;
                var c = new IntVec3((int)Math.Round(from.x + (to.x - from.x) * t), 0, (int)Math.Round(from.z + (to.z - from.z) * t));
                stroke(c, 2 + (Math.Sin(c.x * 0.18 + c.z * 0.11 + phase) > 0.35 ? 1 : 0));
            }
        }

        private static List<int> Route(int from, int to, IntVec3[] points, bool[] legal, int columns, double phase)
        {
            var costs = Enumerable.Repeat(Double.PositiveInfinity, points.Length).ToArray();
            var previous = Enumerable.Repeat(-1, points.Length).ToArray();
            var queue = new PriorityQueue<int, double>();
            costs[from] = 0; queue.Enqueue(from, 0);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == to)
                {
                    var path = new List<int>();
                    for (int node = to; node >= 0; node = previous[node]) path.Add(node);
                    path.Reverse(); return path;
                }
                int x = current % columns, z = current / columns;
                for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0 || x + dx < 0 || x + dx >= columns || z + dz < 0 || z + dz >= points.Length / columns) continue;
                    int next = current + dx + dz * columns;
                    if (!legal[next] || dx != 0 && dz != 0 && (!legal[current + dx] || !legal[current + dz * columns])) continue;
                    IntVec3 position = points[next];
                    double terrainCost = 2.1 + Math.Sin(position.x * 0.13 + phase) * 0.7 + Math.Cos(position.z * 0.11 - phase) * 0.7;
                    double cost = costs[current] + Math.Sqrt((position - points[current]).LengthHorizontalSquared) * terrainCost;
                    if (cost >= costs[next]) continue;
                    costs[next] = cost; previous[next] = current;
                    queue.Enqueue(next, cost + Math.Sqrt((position - points[to]).LengthHorizontalSquared) * 0.65);
                }
            }
            return null;
        }

        private static void Network(CavernPlan plan, IntVec3 size, HashSet<IntVec3> reserved, System.Random random, Action<IntVec3, int> stroke, double phase)
        {
            int columns = 1 + (size.x + 3) / 5, rows = 1 + (size.z + 3) / 5;
            var points = new IntVec3[columns * rows]; var legal = new bool[points.Length];
            for (int z = 0; z < rows; z++) for (int x = 0; x < columns; x++)
            {
                int node = z * columns + x; points[node] = new IntVec3(Math.Min(x * 5, size.x - 1), 0, Math.Min(z * 5, size.z - 1));
                bool safe = true;
                for (int dx = -3; dx <= 3 && safe; dx++) for (int dz = -3; dz <= 3; dz++)
                    if (reserved.Contains(points[node] + new IntVec3(dx, 0, dz))) { safe = false; break; }
                legal[node] = safe;
            }
            var available = Enumerable.Range(0, points.Length).Where(i => legal[i]).ToList();
            int root = available.OrderBy(i => (points[i] - plan.outsideStart).LengthHorizontalSquared).First();
            var nodes = new List<int> { root };
            foreach (IntVec3 target in new[] { new IntVec3(0, 0, random.Next(12, size.z - 12)), new IntVec3(size.x - 1, 0, random.Next(12, size.z - 12)), new IntVec3(random.Next(12, size.x - 12), 0, 0), new IntVec3(random.Next(12, size.x - 12), 0, size.z - 1) })
                nodes.Add(available.OrderBy(i => (points[i] - target).LengthHorizontalSquared).First());
            for (int i = 0; i < 10; i++)
            {
                int choice = available[random.Next(available.Count)];
                if (nodes.All(n => (points[n] - points[choice]).LengthHorizontalSquared > 100)) nodes.Add(choice);
            }
            Line(plan.outsideStart, points[root], stroke, phase);
            var connected = new HashSet<int> { nodes[0] };
            while (connected.Count < nodes.Distinct().Count())
            {
                int from = -1, to = -1, distance = Int32.MaxValue;
                foreach (int a in connected) foreach (int b in nodes)
                    if (!connected.Contains(b) && (points[a] - points[b]).LengthHorizontalSquared < distance)
                    { from = a; to = b; distance = (points[a] - points[b]).LengthHorizontalSquared; }
                List<int> path = Route(from, to, points, legal, columns, phase);
                if (path == null) return;
                for (int i = 1; i < path.Count; i++) Line(points[path[i - 1]], points[path[i]], stroke, phase);
                connected.Add(to);
            }
            foreach (int node in nodes.Skip(5).Take(4))
            {
                IntVec3 position = points[node];
                int radiusX = random.Next(5, 10), radiusZ = random.Next(4, 9);
                for (int dx = -radiusX - 1; dx <= radiusX + 1; dx++) for (int dz = -radiusZ - 1; dz <= radiusZ + 1; dz++)
                {
                    double x = (double)dx / radiusX, z = (double)dz / radiusZ;
                    if (x * x + z * z < 1 + 0.2 * Math.Sin(Math.Atan2(z, x) * 3 + phase)) stroke(position + new IntVec3(dx, 0, dz), 1);
                }
            }
        }

        private static CavernPlan Candidate(IntVec3 size, System.Random random)
        {
            var plan = new CavernPlan();
            double rx = Math.Min(53, Math.Max(18, size.x / 6.0)), rz = Math.Min(48, Math.Max(18, size.z / 6.0));
            IntVec3 center = new IntVec3(size.x / 2 + random.Next(-4, 5), 0, size.z / 2 + random.Next(-4, 5));
            double phase = random.NextDouble() * Math.PI * 2, second = random.NextDouble() * Math.PI * 2;
            for (int x = center.x - (int)(rx * 1.3); x <= center.x + rx * 1.3; x++)
                for (int z = center.z - (int)(rz * 1.3); z <= center.z + rz * 1.3; z++)
                {
                    double dx = (x - center.x) / rx, dz = (z - center.z) / rz;
                    double boundary = Outline(Math.Atan2(dz, dx), phase, second);
                    if (dx * dx + dz * dz <= boundary * boundary) plan.chamber.Add(new IntVec3(x, 0, z));
                }
            int minX = plan.chamber.Min(c => c.x), minZ = plan.chamber.Min(c => c.z);
            plan.oasis = new CellRect(minX, minZ, plan.chamber.Max(c => c.x) - minX + 1, plan.chamber.Max(c => c.z) - minZ + 1);
            plan.open.UnionWith(plan.chamber);
            IntVec3 lake = center + new IntVec3((random.Next(2) == 0 ? -1 : 1) * Math.Max(3, (int)rx / 5), 0, (random.Next(2) == 0 ? -1 : 1) * Math.Max(2, (int)rz / 6));
            double lakeX = Math.Max(7.2, rx / 2.9), lakeZ = Math.Max(7.2, rz / 3.1);
            double lakePhase = random.NextDouble() * Math.PI * 2;
            foreach (IntVec3 c in plan.chamber)
            {
                double dx = (c.x - lake.x) / lakeX, dz = (c.z - lake.z) / lakeZ;
                double boundary = 1 + 0.15 * Math.Sin(3 * Math.Atan2(dz, dx) + lakePhase) + 0.07 * Math.Cos(5 * Math.Atan2(dz, dx) - second);
                double distance = (dx * dx + dz * dz) / (boundary * boundary);
                if (distance <= 1) plan.water.Add(c);
                if (distance <= 0.43) plan.deepWater.Add(c);
            }
            var reserved = new HashSet<IntVec3>();
            foreach (IntVec3 c in plan.chamber)
                for (int dx = -5; dx <= 5; dx++) for (int dz = -5; dz <= 5; dz++)
                    if (dx * dx + dz * dz <= 25) reserved.Add(c + new IntVec3(dx, 0, dz));
            Action<IntVec3, int> stroke = (position, radius) =>
            {
                for (int dx = -radius; dx <= radius; dx++) for (int dz = -radius; dz <= radius; dz++)
                {
                    IntVec3 c = position + new IntVec3(dx, 0, dz);
                    if (dx * dx + dz * dz <= radius * radius && Inside(c, size) && !reserved.Contains(c)) plan.open.Add(c);
                }
            };
            IntVec3 ringCenter = plan.oasis.CenterCell;
            int entranceSide = random.Next(4);
            IntVec3 direction = entranceSide == 0 ? new IntVec3(1, 0, 0) : entranceSide == 1 ? new IntVec3(0, 0, 1) : entranceSide == 2 ? new IntVec3(-1, 0, 0) : new IntVec3(0, 0, -1);
            IntVec3 perpendicular = new IntVec3(-direction.z, 0, direction.x);
            Func<IntVec3, int> axis = c => (c.x - ringCenter.x) * direction.x + (c.z - ringCenter.z) * direction.z;
            Func<IntVec3, int> across = c => (c.x - ringCenter.x) * perpendicular.x + (c.z - ringCenter.z) * perpendicular.z;
            int edge = plan.chamber.Where(c => across(c) >= -1 && across(c) <= 2).Max(c => axis(c));
            int outside = (entranceSide % 2 == 0 ? plan.oasis.Width : plan.oasis.Height) / 2 + 15;
            for (int along = edge - 5; along <= outside + 4; along++)
                for (int cross = -1; cross <= 2; cross++)
                {
                    IntVec3 c = ringCenter + direction * along + perpendicular * cross;
                    if (Inside(c, size)) plan.open.Add(c);
                }
            plan.entrance = ringCenter + direction * (edge + 1);
            plan.outsideStart = ringCenter + direction * outside;
            Network(plan, size, reserved, random, stroke, phase);
            return plan;
        }
    }

    public sealed class GenStep_CavernLayout : GenStep
    {
        public override int SeedPart { get { return 913271211; } }
        public override void Generate(Map map, GenStepParams parms)
        {
            if (!MapComponent_CavernOasis.IsCavern(map)) return;
            CavernPlan plan = CavernLayout.BuildPlan(map.Size, Rand.Int);
            map.GetComponent<MapComponent_CavernOasis>().Set(plan.oasis, plan.entrance);
            TerrainDef flesh = DefDatabase<TerrainDef>.GetNamed("Flesh");
            TerrainDef soil = DefDatabase<TerrainDef>.GetNamed("UGWG_OasisSoil");
            TerrainDef shallow = DefDatabase<TerrainDef>.GetNamed("WaterShallow");
            TerrainDef deep = DefDatabase<TerrainDef>.GetNamed("WaterDeep");
            var rocks = Find.World.NaturalRockTypesIn(map.Tile).ToList();
            if (rocks.Count == 0) rocks.Add(DefDatabase<ThingDef>.GetNamed("Granite"));
            foreach (IntVec3 cell in map.AllCells)
            {
                TerrainDef terrain = flesh;
                if (plan.chamber.Contains(cell)) terrain = plan.deepWater.Contains(cell) ? deep : plan.water.Contains(cell) ? shallow : soil;
                map.terrainGrid.SetTerrain(cell, terrain);
                if (!plan.open.Contains(cell))
                {
                    int index = Math.Min(rocks.Count - 1, (int)(Mathf.PerlinNoise(cell.x * 0.037f, cell.z * 0.037f) * rocks.Count));
                    GenSpawn.Spawn(ThingMaker.MakeThing(rocks[index]), cell, map, WipeMode.Vanish);
                }
            }
            CavernResources.Generate(map, plan);
            MapGenerator.PlayerStartSpot = plan.start;
            if (Find.GameInitData != null && CavernScenario.Reclamation)
                MapGenerator.PlayerStartSpot = plan.outsideStart;
            MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects").Add(plan.oasis.ExpandedBy(3));
            MapGenerator.rootsToUnfog.Add(MapGenerator.PlayerStartSpot);
        }
    }
}
