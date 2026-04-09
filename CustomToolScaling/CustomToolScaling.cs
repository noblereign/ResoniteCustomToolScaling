using System.Reflection;
using System.Reflection.Emit;
using FrooxEngine;
using Elements.Core;
using Renderite.Shared;
using HarmonyLib;
using ResoniteModLoader;

namespace CustomToolScaling;
//More info on creating mods can be found https://github.com/resonite-modding-group/ResoniteModLoader/wiki/Creating-Mods
public class CustomToolScaling : ResoniteMod {
	internal const string VERSION_CONSTANT = "1.0.0"; //Changing the version here updates it in all locations needed
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
		harmony.PatchAll();
	}

	public static float EvaluateVanillaScale(InteractionHandler handler, ITool tool) { // kinda gotta redo almost the entire thing sadly
		HandPoser handPoser = handler.Slot.ActiveUserRoot.GetRegisteredComponent((HandPoser p) => p.Enabled && p.Slot.IsActive && p.Side.Value == (Chirality)handler.Side && p.PoseSource.Target != null);
		Slot handRoot = ((handPoser == null) ? handler.Slot.ActiveUserRoot.GetHandSlot(handler.Side) : handPoser.CurrentHandRoot);
		Slot slot = tool.Slot;
		List<GripPoseReference> references = Pool.BorrowList<GripPoseReference>();
		InteractionHandler.GetGripPoseReferences(tool, slot, references, (GripPoseReference g) => g.HandSide.Value == (Chirality)handler.Side);
		if (references.Count == 0) {
			return 1f; // this might differ from vanilla behavior? not sure what this is actually meant to return...
		}
		Slot hand = handler.Slot.ActiveUserRoot.GetHandSlot(handler.Side);
		Slot holder = tool.Slot.Parent;
		Slot space = holder.Parent;
		float minDist = float.MaxValue;
		GripPoseReference minReference = null!;
		for (int i = 0; i < references.Count; i++) {
			GripPoseReference r = references[i];
			float dist = MathX.Distance(r.Slot.GlobalPosition, hand.GlobalPosition);
			if (dist < minDist) {
				minDist = dist;
				minReference = r;
			}
		}
		Pool.Return(ref references);
		GripPoseReference reference = minReference!;
		float3 referencePos;
		floatQ referenceRot;
		float referenceScale;
		float targetScale = handRoot.LocalScaleToGlobal(1f); // fallback
		floatQ targetRot;
		float3 targetPos;
		if (handPoser != null) {
			float3 referenceForward = reference.LocalForwardDirection;
			float3 referenceUp = MathX.Cross(in referenceForward, reference.LocalRightDirection);
			referencePos = slot.SpacePointToLocal(reference.LocalFingerStartPosition, reference.Slot);
			referenceRot = slot.SpaceRotationToLocal(floatQ.LookRotation(in referenceForward, in referenceUp), reference.Slot);
			referenceScale = reference.Slot.LocalScaleToGlobal(reference.LocalScale);
			float3 fingerStart = GripPoseReference.ComputeFingerStart(handPoser);
			float3 localFingerStart = handRoot.GlobalPointToLocal(in fingerStart);
			float localScale = localFingerStart.Magnitude;
			float3 localForward = localFingerStart.Normalized;
			float3 localUp = MathX.Cross(in localForward, handPoser.HandRight.Value);
			targetPos = fingerStart;
			targetRot = handRoot.LocalRotationToGlobal(floatQ.LookRotation(in localForward, in localUp));
			targetScale = handRoot.LocalScaleToGlobal(localScale);
		} else {
			referencePos = slot.SpacePointToLocal(reference.Slot.LocalPosition, reference.Slot.Parent);
			referenceRot = slot.SpaceRotationToLocal(reference.Slot.LocalRotation, reference.Slot.Parent);
			referenceScale = reference.Slot.LocalScaleToGlobal(1f);
			targetPos = handRoot.GlobalPosition;
			targetRot = handRoot.GlobalRotation;
			targetScale = handRoot.LocalScaleToGlobal(1f);
		}
		return targetScale;
	}

	[HarmonyPatch]
	class AlignTool_DynamicScale_Transpiler {
		public static float DetermineTargetScale(InteractionHandler handler, ITool tool) {
			float vanillaScale = EvaluateVanillaScale(handler, tool);
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
							finalScale = customScaleNumber;
						} else {
							finalScale = vanillaScale * customScaleNumber;
						}
					}
				}
			}

			return finalScale;
		}

		static MethodBase TargetMethod() {
			// Find the hidden async state machine
			var stateMachineType = typeof(InteractionHandler)
				.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(t => t.Name.Contains("<AlignTool>d__"));

			if (stateMachineType == null) return null!;

			return AccessTools.Method(stateMachineType, "MoveNext");
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
			var codes = new List<CodeInstruction>(instructions);

			MethodInfo customScaleMethod = AccessTools.Method(
				typeof(AlignTool_DynamicScale_Transpiler),
				nameof(DetermineTargetScale)
			);

			var stateMachineType = typeof(InteractionHandler)
				.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(t => t.Name.Contains("<AlignTool>d__"));

			FieldInfo toolField = AccessTools.Field(stateMachineType, "tool");

			// Grab the hidden field holding the InteractionHandler instance!
			FieldInfo instanceField = AccessTools.Field(stateMachineType, "<>4__this");

			for (int i = 0; i < codes.Count; i++) {
				if (codes[i].opcode == OpCodes.Div) {
					// We are replacing the instruction at [i-2] that originally pushed the float.

					// 1. Load the state machine instance onto the stack
					codes[i - 2].opcode = OpCodes.Ldarg_0;
					codes[i - 2].operand = null;

					// 2. Prepare our new instructions to fetch the fields and call the method
					var loadInstance = new CodeInstruction(OpCodes.Ldfld, instanceField); // Gets InteractionHandler
					var loadStateMachineAgain = new CodeInstruction(OpCodes.Ldarg_0);     // Needs state machine again
					var loadTool = new CodeInstruction(OpCodes.Ldfld, toolField);         // Gets ITool
					var callCustomMath = new CodeInstruction(OpCodes.Call, customScaleMethod); // Calls our function

					// 3. Shove them all into the execution line right after [i-2]
					codes.InsertRange(i - 1, new[] {
					loadInstance,
					loadStateMachineAgain,
					loadTool,
					callCustomMath
				});

					break; // Surgery complete
				}
			}

			return codes.AsEnumerable();
		}
	}
}
