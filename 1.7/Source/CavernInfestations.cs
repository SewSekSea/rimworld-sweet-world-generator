using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace UnaidedMistake.WorldGenerator
{
    [HarmonyPatch]
    internal static class CavernInfestationPatch
    {
        internal static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (System.Type type in new[] { typeof(IncidentWorker_Infestation), typeof(IncidentWorker_DeepDrillInfestation) })
                foreach (string method in new[] { "CanFireNowSub", "TryExecuteWorker" })
                    yield return AccessTools.DeclaredMethod(type, method);
        }
        internal static bool Prefix(IncidentParms __0, ref bool __result)
        {
            if (!MapComponent_CavernOasis.IsCavern(__0?.target as Map)) return true;
            __result = false;
            return false;
        }
    }
}
