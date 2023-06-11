using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using BaseX;
using CloudX.Shared;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

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
				MethodInfo logPostfix = AccessTools.DeclaredMethod(typeof(UniLog_Log_Patch), nameof(UniLog_Log_Patch.Postfix));
				harmony.Patch(logOriginal, postfix: new HarmonyMethod(logPostfix));
			}
		}

		class WorldInfo
		{
			public SessionAccessLevel prevAccessLevel;
			public bool prevAwayKickEnabled;
		}

		private static Dictionary<World, WorldInfo> worldInfoMap = new Dictionary<World, WorldInfo>();

		[HarmonyPatch(typeof(Userspace), "SaveWorldTask")]
		class Userspace_SaveWorldTask_Patch
		{
			public static bool Prefix(World world, FrooxEngine.Record record, RecordOwnerTransferer transferer)
			{
				Msg("World is saving. Setting session to private and disabling away kick.");

				if (!worldInfoMap.ContainsKey(world))
				{
					worldInfoMap.Add(world, new WorldInfo());
				}

				worldInfoMap[world].prevAccessLevel = world.AccessLevel;
				worldInfoMap[world].prevAwayKickEnabled = world.AwayKickEnabled;

				world.AccessLevel = SessionAccessLevel.Private;
				world.AwayKickEnabled = false;

				return true;
			}
		}

		class UniLog_Log_Patch
		{
			public static void Postfix(string message, bool stackTrace)
			{
				if (message.StartsWith("World Saved! Name: ") && message.Contains(". RecordId: ") && message.Contains(". Local: ") && message.Contains(", Global: "))
				{
					foreach(World world in worldInfoMap.Keys.ToList())
					{
						FrooxEngine.Record r = world.CorrespondingRecord;

						if (message.Contains($"World Saved! Name: {r.Name}. RecordId: {r.OwnerId}:{r.RecordId}"))
						{
							Msg($"World \"{world.Name}\" finished saving. Restoring session config.");
							world.AccessLevel = worldInfoMap[world].prevAccessLevel;
							world.AwayKickEnabled = worldInfoMap[world].prevAwayKickEnabled;
							worldInfoMap.Remove(world);
						}
					}
				}
			}
		}
	}
}