using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    public sealed class Plant_CavernHair : Plant
    {
        public override void TickLong()
        {
            if (Spawned && !MapComponent_CavernHair.Habitat(Map, Position)) { Destroy(DestroyMode.Vanish); return; }
            base.TickLong();
        }
        public override void Print(SectionLayer layer)
        {
            Rand.PushState(Position.GetHashCode());
            try
            {
                int tufts = Mathf.Clamp(Mathf.CeilToInt(Growth * 9f), 1, 9);
                float height = def.graphicData.drawSize.x * def.plant.visualSizeRange.LerpThroughRange(Growth);
                var colors = new Color32[4];
                PlantUtility.SetWindExposureColors(colors, this);
                for (int i = 0; i < tufts; i++)
                {
                    Vector3 center = Position.ToVector3() + new Vector3((i % 3 + 0.5f) / 3f, def.Altitude, (i / 3 + 0.5f) / 3f) + Gen.RandomHorizontalVector(0.07f);
                    Material material = Graphic is Graphic_Random variants ? variants.SubGraphicAtIndex(Rand.Range(0, variants.SubGraphicsCount)).MatSingle : Graphic.MatSingleFor(this);
                    bool flip = Rand.Bool;
                    Graphic.TryGetTextureAtlasReplacementInfo(material, def.category.ToAtlasGroup(), flip, false, out material, out var uv, out var vertexColor);
                    Printer_Plane.PrintPlane(layer, center, new Vector2(height * 0.3f, height), material, 0f, flip, uv, colors, 0.1f, this.HashOffset() % 1024);
                }
            }
            finally { Rand.PopState(); }
        }
    }

    public sealed class MapComponent_CavernHair : MapComponent
    {
        private bool enabled;
        private Dictionary<IntVec3, int> pending = new Dictionary<IntVec3, int>();
        public bool Enabled { get { return enabled; } }
        public MapComponent_CavernHair(Map map) : base(map) { }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enabled, "cavernHairEnabled", false);
            Scribe_Collections.Look(ref pending, "cavernHairSeedTicks", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pending == null) pending = new Dictionary<IntVec3, int>();
        }
        public static bool Habitat(Map map, IntVec3 cell)
        {
            return RootSite(map, cell) && cell.Walkable(map) && !cell.GetThingList(map).Any(t => !(t is Plant) && t.def.BlocksPlanting());
        }
        private static bool RootSite(Map map, IntVec3 cell)
        {
            return MapComponent_CavernOasis.IsCavern(map) && cell.InBounds(map) && cell.GetTerrain(map).defName == "Flesh" && cell.GetEdifice(map) == null && cell.GetZone(map) == null && !cell.GetThingList(map).Any(t => t is Building || t is Blueprint || t is Frame) && !GenRadial.RadialCellsAround(cell, 3f, true).Any(c => c.InBounds(map) && c.GetTerrain(map).burnDamage > 0f);
        }
        private bool Seed(IntVec3 cell, float growth)
        {
            if (!Habitat(map, cell) || cell.GetPlant(map) != null || cell.GetFirstPawn(map) != null) return false;
            float temperature = cell.GetTemperature(map);
            if (temperature <= 0f || temperature >= 58f) return false;
            var plant = (Plant)GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("UGWG_CavernHair")), cell, map);
            plant.Growth = growth;
            return true;
        }
        public override void MapGenerated()
        {
            if (enabled || !MapComponent_CavernOasis.IsCavern(map)) return;
            enabled = true;
            foreach (IntVec3 cell in map.AllCells)
                if (Habitat(map, cell) && Rand.Chance(0.18f)) Seed(cell, Rand.Range(0.55f, 0.9f));
        }
        public void NotifyMined(IntVec3 cell)
        {
            if (enabled && cell.InBounds(map) && cell.GetTerrain(map).defName == "Flesh") pending[cell] = Find.TickManager.TicksGame + 6000;
        }
        public override void MapComponentTick()
        {
            if (!enabled || !MapComponent_CavernOasis.IsCavern(map) || (Find.TickManager.TicksGame + map.uniqueID) % 2000 != 0) return;
            foreach (IntVec3 cell in pending.Where(p => p.Value <= Find.TickManager.TicksGame).OrderBy(p => p.Value).ThenBy(p => p.Key.z).ThenBy(p => p.Key.x).Select(p => p.Key).Take(16).ToList())
            {
                if (!RootSite(map, cell) || cell.GetPlant(map) != null || Seed(cell, 0.05f)) pending.Remove(cell);
                else pending[cell] = Find.TickManager.TicksGame + 2000;
            }
            var habitat = map.AllCells.Where(c => Habitat(map, c)).ToList();
            int count = map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("UGWG_CavernHair")).Count;
            IntVec3 next;
            if (count < habitat.Count / 5 && habitat.Where(c => c.GetPlant(map) == null).TryRandomElement(out next)) Seed(next, 0.05f);
        }
    }

    [HarmonyPatch(typeof(Mineable), nameof(Mineable.DestroyMined))]
    internal static class CavernHairMiningPatch
    {
        internal struct MiningState
        {
            internal Map map;
            internal IntVec3 cell;
            internal bool changeTerrain, flesh;
        }
        internal static void Prefix(Mineable __instance, out MiningState __state)
        {
            Map map = __instance.Map;
            __state = new MiningState { map = map, cell = __instance.Position, changeTerrain = __instance.canChangeTerrainOnDestroyed, flesh = MapComponent_CavernOasis.IsCavern(map) && map.GetComponent<MapComponent_CavernHair>().Enabled && __instance.Position.GetTerrain(map).defName == "Flesh" };
            if (__state.flesh) __instance.canChangeTerrainOnDestroyed = false;
        }
        internal static void Postfix(Mineable __instance, MiningState __state)
        {
            __instance.canChangeTerrainOnDestroyed = __state.changeTerrain;
            if (__instance.Destroyed && __state.flesh) __state.map.GetComponent<MapComponent_CavernHair>().NotifyMined(__state.cell);
        }
    }
}
