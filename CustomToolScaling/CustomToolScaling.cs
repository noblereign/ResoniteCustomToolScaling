using System.Reflection;
using System.Reflection.Emit;
using FrooxEngine;
using Renderite.Shared;
using HarmonyLib;
using ResoniteModLoader;

namespace CustomToolScaling;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class CustomToolScaling : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.1.0"; //Changing the version here updates it in all locations needed
	public override string Name => "CustomToolScaling";
	public override string Author => "Noble";
	public override string Version => VERSION_CONSTANT;
	public override string Link => "https://github.com/noblereign/ResoniteCustomToolScaling/";

	public static ModConfiguration? Config;

	[AutoRegisterConfigKey]
	public static ModConfigurationKey<bool> Enabled = new("Enabled", "Enables the mod, pretty self-explanatory.", () => true);

	public override void OnEngineInit() {
		Config = GetConfiguration()!;
		Config!.Save(true);

		Harmony harmony = new("dog.glacier.CustomToolScaling");
		harmony.PatchAll(Assembly.GetExecutingAssembly());
	}

	[HarmonyPatch(typeof(InteractionHandler), "AlignTool")]
	class AlignTool_DynamicScale_Transpiler {
		public static float DetermineTargetScale(InteractionHandler handler, ITool tool, float vanillaScale) {
			float finalScale = vanillaScale;

			if (Config!.GetValue(Enabled)) {
				Slot handSlot = handler.LocalUser.GetBodyNodeSlot(handler.Side == Chirality.Right ? BodyNode.RightHand : BodyNode.LeftHand);

				DynamicVariableSpace varSpace = handSlot.FindSpace("Avatar");
				if (varSpace != null) {
					varSpace.TryReadValue<bool>("Noble.ToolScale.DirectScaling", out bool setDirectly);
					float customScaleNumber = 0f;

					bool valueFound = varSpace.TryReadValue<float>("Noble.ToolScale", out float avatarRequestedScale);
					if (valueFound && avatarRequestedScale > 0f) {
						customScaleNumber = avatarRequestedScale;
					}
					bool valueFoundChirality = varSpace.TryReadValue<float>($"Noble.ToolScale.{(handler.Side == Chirality.Right ? "Right" : "Left")}", out float chiralityToolScale);
					if (valueFoundChirality && chiralityToolScale > 0f) {
						customScaleNumber = chiralityToolScale;
					}

					if (customScaleNumber > 0f) {
						if (setDirectly) {
							finalScale = customScaleNumber * handler.LocalUserRoot.GlobalScale;
						} else {
							finalScale = vanillaScale * customScaleNumber;
						}
					}
				}
			}

			return finalScale;
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			var codes = new List<CodeInstruction>(instructions);

			MethodInfo customScaleMethod = AccessTools.Method(
				typeof(AlignTool_DynamicScale_Transpiler),
				nameof(DetermineTargetScale)
			);

			for (int i = 2; i < codes.Count; i++) {
				// Matches:
				// IL_0325: ldloc.s 15 (targetScale / vanillaScale)
				// IL_0327: ldloc.s 20 (currentScale)
				// IL_0329: div
				if (codes[i].opcode == OpCodes.Div && codes[i - 2].IsLdloc()) {
					codes.InsertRange(i - 2, new[] {
						new CodeInstruction(OpCodes.Ldarg_0), // InteractionHandler (this)
						new CodeInstruction(OpCodes.Ldarg_1)  // ITool (tool)
					});
					// codes[i] is now the original ldloc.s 15 (vanillaScale), so insert the call right after it
					codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, customScaleMethod));
					break;
				}
			}

			return codes;
		}
	}
}
