using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class SurfaceAnomalyBiome : DefModExtension
    {
        public bool metal;
    }

    public sealed class BiomeWorker_SurfaceAnomaly : BiomeWorker
    {
        public override float GetScore(BiomeDef biome, Tile tile, PlanetTile planetTile)
        {
            SurfaceAnomalyBiome variant = biome?.GetModExtension<SurfaceAnomalyBiome>();
            if (!ModsConfig.AnomalyActive || variant == null || tile == null || tile.WaterCovered) return 0f;
            if (tile.temperature < -5f || tile.temperature > 35f || tile.rainfall < 600f || tile.rainfall > 2000f) return 0f;
            WorldPreset preset = BiomeScorePatch.CurrentPreset;
            int index = preset == null ? -1 : preset.biomeNames.IndexOf(biome.defName);
            if (index < 0 || index >= preset.biomeWeights.Count) return 0f;
            float weight = preset.biomeWeights[index];
            if (Single.IsNaN(weight) || Single.IsInfinity(weight) || weight <= 0f) return 0f;
            float noise = Rand.ValueSeeded(planetTile.tileId ^ (variant.metal ? 1880694121 : 512713019));
            return Mathf.Max(1f, BiomeWorker_TemperateForest.Score(tile)) * (0.1f + 1.25f * noise * noise * noise);
        }
    }

    public sealed class GenStep_SurfaceAnomalyRuins : GenStep
    {
        public override int SeedPart { get { return 173119882; } }

        public override void Generate(Map map, GenStepParams parms)
        {
            SurfaceAnomalyBiome variant = map.Biome.GetModExtension<SurfaceAnomalyBiome>();
            if (!ModsConfig.AnomalyActive || variant == null || map.Tile.Layer.Def != PlanetLayerDefOf.Surface) return;
            TerrainDef floor = DefDatabase<TerrainDef>.GetNamed(variant.metal ? "Voidmetal" : "UGWG_GraySurface");
            ThingDef wall = DefDatabase<ThingDef>.GetNamed(variant.metal ? "UGWG_MetalRuinWall" : "UGWG_LabyrinthRuinWall");
            List<CellRect> used = MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects");
            int offsetX = Rand.Range(0, 10), offsetZ = Rand.Range(0, 10);
            for (int x = 16 + offsetX; x + 22 < map.Size.x - 16; x += 32)
            for (int z = 16 + offsetZ; z + 22 < map.Size.z - 16; z += 32)
            {
                CellRect plot = new CellRect(x, z, 22, 22);
                if (plot.CenterCell.DistanceTo(map.Center) < 44f || used.Any(r => r.Overlaps(plot.ExpandedBy(3)))) continue;
                if (plot.Cells.Any(c => !c.InBounds(map) || c.GetTerrain(map).IsWater || !c.Standable(map) || c.GetEdifice(map) != null || c.Roofed(map))) continue;
                if (plot.ExpandedBy(2).Cells.Any(c => c.InBounds(map) && (c.GetTerrain(map).IsRoad || c.GetTerrain(map).bridge))) continue;
                foreach (IntVec3 cell in plot.Cells) map.terrainGrid.SetTerrain(cell, floor);
                foreach (IntVec3 cell in plot.Cells)
                {
                    int localX = cell.x - x, localZ = cell.z - z;
                    bool edge = localX == 0 || localZ == 0 || localX == 21 || localZ == 21;
                    bool doorway = (localX >= 9 && localX <= 12) || (localZ >= 9 && localZ <= 12);
                    bool interior = variant.metal
                        ? (localZ == 5 || localZ == 16) && localX >= 4 && localX <= 17 && (localX < 9 || localX > 12)
                        : (localX == 6 && localZ >= 4 && localZ <= 13) || (localX == 15 && localZ >= 8 && localZ <= 17);
                    if (!(edge && !doorway) && !interior) continue;
                    if (Rand.ValueSeeded(cell.x * 13579 ^ cell.z * 21413 ^ map.Tile.tileId) < 0.1f) continue;
                    Thing ruin = ThingMaker.MakeThing(wall);
                    ruin.HitPoints = Mathf.Max(1, Mathf.RoundToInt(ruin.MaxHitPoints * Rand.Range(0.35f, 0.8f)));
                    GenSpawn.Spawn(ruin, cell, map, WipeMode.Vanish);
                }
                used.Add(plot.ExpandedBy(2));
            }
        }
    }
}
