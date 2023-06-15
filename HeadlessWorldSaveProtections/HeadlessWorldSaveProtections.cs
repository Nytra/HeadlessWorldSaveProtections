using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using CloudX.Shared;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

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

				Type stateMachineType = AccessTools.TypeByName("FrooxEngine.Userspace+<SaveWorldTask>d__207");
				MethodInfo moveNext = stateMachineType.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);
				MethodInfo moveNextPostfix = AccessTools.DeclaredMethod(typeof(Userspace_SaveWorldTask_MoveNext_Patch), nameof(Userspace_SaveWorldTask_MoveNext_Patch.Postfix));
				MethodInfo moveNextTranspiler = AccessTools.DeclaredMethod(typeof(Userspace_SaveWorldTask_MoveNext_Patch), nameof(Userspace_SaveWorldTask_MoveNext_Patch.Transpiler));
				harmony.Patch(moveNext, postfix: new HarmonyMethod(moveNextPostfix), transpiler: new HarmonyMethod(moveNextTranspiler));
			}
		}

		class WorldInfo
		{
			public SessionAccessLevel prevAccessLevel;
			public bool prevAwayKickEnabled;
		}

		private static bool doneSaving = false;

		private static Dictionary<World, WorldInfo> worldInfoMap = new Dictionary<World, WorldInfo>();

		[HarmonyPatch(typeof(Userspace), "SaveWorldTask")]
		class Userspace_SaveWorldTask_Patch
		{
			public static bool Prefix(World world, FrooxEngine.Record record, RecordOwnerTransferer transferer)
			{
				Msg($"World \"{world.Name}\" is saving. Setting access level to private and disabling away kick.");

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

		class Userspace_SaveWorldTask_MoveNext_Patch
		{
			public static void Postfix(World ___world)
			{
				//Msg("Postfix");
				if (doneSaving)
				{
					Msg($"World \"{___world.Name}\" finished saving. Restoring previous access level and away kick.");
					___world.AccessLevel = worldInfoMap[___world].prevAccessLevel;
					___world.AwayKickEnabled = worldInfoMap[___world].prevAwayKickEnabled;
					worldInfoMap.Remove(___world);
					doneSaving = false;
				}
			}

			public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				var codes = new List<CodeInstruction>(instructions);
				for (var i = 0; i < codes.Count; i++)
				{
					if (codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "Finished save world: ")
					{
						Msg("Found string! Inserting method call!");
						var instructionsToInsert = new List<CodeInstruction>();
						instructionsToInsert.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Userspace_SaveWorldTask_MoveNext_Patch), "FinishedSavingWorld")));
						codes.InsertRange(i, instructionsToInsert);
						break;
					}
				}
				return codes.AsEnumerable();
			}

			public static void FinishedSavingWorld()
			{
				doneSaving = true;
			}
		}
	}
}