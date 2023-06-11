using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using CloudX.Shared;
using System.Reflection;
using System;

namespace HeadlessWorldSaveProtections
{
    public class HeadlessWorldSaveProtections : NeosMod
    {
        public override string Name => "HeadlessWorldSaveProtections";
        public override string Author => "Nytra";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/Nytra/HeadlessWorldSaveProtections";
        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("owo.Nytra.HeadlessWorldSaveProtections");

            Type neosHeadless = AccessTools.TypeByName("NeosHeadless.Program");
            if (neosHeadless != null)
            {
                harmony.PatchAll();

                MethodInfo logOriginal = AccessTools.DeclaredMethod(typeof(UniLog), "Log", new Type[] { typeof(string), typeof(bool) });
                MethodInfo logPostfix = AccessTools.DeclaredMethod(typeof(UniLogLogPatch), nameof(UniLogLogPatch.Postfix));
                harmony.Patch(logOriginal, postfix: new HarmonyMethod(logPostfix));
            }
        }

        private static World currentlySavingWorld = null;
        private static SessionAccessLevel prevSessionAccessLevel;
        private static bool prevAwayKickEnabled;

        [HarmonyPatch(typeof(Userspace), "SaveWorldTask")]
        class Userspace_SaveWorldTask_Patch
        {
            public static bool Prefix(World world, FrooxEngine.Record record, RecordOwnerTransferer transferer)
            {
                Msg("World is saving. Setting session to private and disabling away kick.");

                currentlySavingWorld = world;
                prevSessionAccessLevel = world.AccessLevel;
                prevAwayKickEnabled = world.AwayKickEnabled;

                world.AccessLevel = SessionAccessLevel.Private;
                world.AwayKickEnabled = false;

                return true;
            }
        }

        class UniLog_Log_Patch
        {
            public static void Postfix(string message, bool stackTrace)
            {
                if (currentlySavingWorld != null && message.StartsWith("Finished save world: "))
                {
                    Msg("World finished saving. Restoring previous world config.");
                    currentlySavingWorld.AccessLevel = prevSessionAccessLevel;
                    currentlySavingWorld.AwayKickEnabled = prevAwayKickEnabled;
                    currentlySavingWorld = null;
                }
            }
        }
    }
}