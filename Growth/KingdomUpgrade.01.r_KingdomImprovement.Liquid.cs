using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		internal static bool CarryLiquidDurable(GameObject SourceObject, GameObject TargetObject,
			r_KingdomImprovement Receipt, out int Moved)
		{
			Moved = 0;
			if (Receipt == null || !Receipt.HandoverFlagsValid()
				|| !ExactHandoverControlTypes(Receipt))
				return FailHandover(Receipt, "Handover boolean flags are corrupt.");
			if (Receipt.HandoverQuarantined) return false;
			if (!ExactHandoverObjects(SourceObject, TargetObject, Receipt))
				return FailHandover(Receipt, "Handover endpoints do not match their exact IDs.");
			GameObject receiptOwner = Receipt.ParentObject;
			if (receiptOwner == null || receiptOwner.HasStringProperty(
				HandoverPrefix + "LiquidPhase") || Receipt.HandoverPhase < 0
				|| Receipt.HandoverPhase > 3 || (Receipt.HandoverPhase > 0
					&& !receiptOwner.HasIntProperty(HandoverPrefix + "LiquidPhase")))
				return FailHandover(Receipt, "Liquid handover phase is corrupt.");
			LiquidVolume source = SourceObject.GetPart<LiquidVolume>();
			LiquidVolume target = TargetObject.GetPart<LiquidVolume>();
			if (Receipt.HandoverPhase == 0 && (source == null || source.Volume <= 0))
			{
				if (HasLiquidIntentEvidence(receiptOwner)) return FailHandover(Receipt,
					"A zero-liquid handover carries an uncommitted liquid receipt prefix.");
				Receipt.HandoverPhase = 3;
				return true;
			}
			if (Receipt.HandoverPhase == 3 && Receipt.HandoverSourceVolumeBefore == 0)
			{
				if (HasLiquidIntentEvidence(receiptOwner) || source != null && source.Volume > 0)
					return FailHandover(Receipt,
						"A settled zero-liquid handover gained foreign liquid evidence.");
				return true;
			}
			if (source == null || target == null)
				return FailHandover(Receipt, "Liquid source has no exact successor vessel.");
			if (Receipt.HandoverPhase == 0)
				if (!TryPublishLiquidIntent(source, target, Receipt)) return false;
			if (!ReconcileLiquidPhase(SourceObject, TargetObject, Receipt, source, target)) return false;
			if (!ExactLiquidReceiptShape(Receipt)) return FailHandover(Receipt,
				"Liquid handover receipt is corrupt or unbounded.");
			if (Receipt.HandoverPhase == 3)
			{
				Moved = Receipt.HandoverSourceVolumeBefore;
				return true;
			}
			if (Receipt.HandoverPhase == 1)
			{
				string callbackFailure = null;
				try { KingdomLiquids.Drain(source, Receipt.HandoverSourceVolumeBefore); }
				catch (Exception exception) { callbackFailure = exception.Message; }
				ObserveHandoverMutation(SourceObject, TargetObject, SourceObject.CurrentCell, null);
				if (!ReproveManifestAfterCallback(SourceObject, TargetObject,
					SourceObject.CurrentCell, Receipt)) return false;
				if (!ReconcileLiquidPhase(SourceObject, TargetObject, Receipt, source, target))
					return false;
				if (Receipt.HandoverPhase == 1) return RetryLiquid(Receipt,
					"Liquid drain had no exact effect" + (callbackFailure == null ? "." : ": "
						+ callbackFailure));
			}
			return ResumeDrainedLiquid(SourceObject, TargetObject, Receipt, source, target,
				out Moved);
		}

		private static bool ResumeDrainedLiquid(GameObject SourceObject, GameObject TargetObject,
			r_KingdomImprovement Receipt, LiquidVolume Source, LiquidVolume Target, out int Moved)
		{
			Moved = 0;
			if (Receipt.HandoverPhase == 3)
			{
				Moved = Receipt.HandoverSourceVolumeBefore;
				return true;
			}
			if (Receipt.HandoverPhase != 2 || Source == null || Target == null)
				return FailHandover(Receipt, "Drained liquid receipt is ambiguous before fill.");
			LiquidVolume frozen;
			if (!TryFrozenLiquid(Receipt.HandoverSourceComposition,
				Receipt.HandoverSourceVolumeBefore, out frozen))
				return FailHandover(Receipt, "Frozen liquid composition cannot be reconstructed.");
			string callbackFailure = null;
			try { Target.MixWith(frozen, PouredFrom: SourceObject); }
			catch (Exception exception) { callbackFailure = exception.Message; }
			ObserveHandoverMutation(SourceObject, TargetObject, SourceObject.CurrentCell, null);
			if (!ReproveManifestAfterCallback(SourceObject, TargetObject,
				SourceObject.CurrentCell, Receipt)) return false;
			if (!ReconcileLiquidPhase(SourceObject, TargetObject, Receipt, Source, Target)) return false;
			if (Receipt.HandoverPhase == 3)
			{
				Moved = Receipt.HandoverSourceVolumeBefore;
				return true;
			}
			return RetryLiquid(Receipt, "Liquid fill had no exact effect"
				+ (callbackFailure == null ? "." : ": " + callbackFailure));
		}

		private static bool TryPublishLiquidIntent(LiquidVolume Source, LiquidVolume Target,
			r_KingdomImprovement Receipt)
		{
			if (!KingdomUpgradeContentRules.LiquidEndpointSafe(Source.MaxVolume, false,
					LiquidEndpointHasContextRisk(Source))
				|| !KingdomUpgradeContentRules.LiquidEndpointSafe(Target.MaxVolume, false,
					LiquidEndpointHasContextRisk(Target)))
				return FailHandover(Receipt,
					"Open or context-sensitive liquid cannot be transferred safely during improvement.");
			int space = Target.MaxVolume < 0 ? int.MaxValue : Target.MaxVolume - Target.Volume;
			string sourceText = EncodeLiquid(Source);
			string targetText = EncodeLiquid(Target);
			string expected;
			if (Source.Volume <= 0 || Target.Volume < 0 || space < Source.Volume
				|| (long)Target.Volume + Source.Volume > int.MaxValue || sourceText == null
				|| targetText == null || !TryPreviewLiquidAfter(Source, Target, out expected))
				return FailHandover(Receipt, "Successor liquid capacity changed before handover.");
			GameObject owner = Receipt.ParentObject;
			string digest = LiquidIntentDigest(Receipt.HandoverSourceId,
				Receipt.HandoverTargetId, Receipt.HandoverConstructionReceipt,
				Source.Volume.ToString(CultureInfo.InvariantCulture),
				Target.Volume.ToString(CultureInfo.InvariantCulture),
				Target.MaxVolume.ToString(CultureInfo.InvariantCulture), sourceText, targetText,
				expected);
			if (!ExactOrAbsentInt(owner, "SourceVolumeBefore", Source.Volume)
				|| !ExactOrAbsentInt(owner, "SourceVolumeAfter", 0)
				|| !ExactOrAbsentInt(owner, "TargetVolumeBefore", Target.Volume)
				|| !ExactOrAbsentInt(owner, "TargetVolumeAfter", -1)
				|| !ExactOrAbsentInt(owner, "TargetCapacity", Target.MaxVolume)
				|| !ExactOrAbsentText(owner, HandoverPrefix + "SourceComposition", sourceText)
				|| !ExactOrAbsentText(owner, HandoverPrefix + "TargetCompositionBefore", targetText)
				|| owner.HasStringProperty(HandoverPrefix + "TargetCompositionAfter")
				|| owner.HasIntProperty(HandoverPrefix + "TargetCompositionAfter")
				|| !ExactOrAbsentText(owner, HandoverPrefix + "TargetCompositionExpected", expected)
				|| !ExactOrAbsentText(owner, HandoverPrefix + "LiquidIntentDigest", digest))
				return FailHandover(Receipt, "Liquid intent prefix carries a third value.");
			try
			{
				Receipt.HandoverSourceVolumeBefore = Source.Volume;
				Receipt.HandoverSourceVolumeAfter = 0;
				Receipt.HandoverTargetVolumeBefore = Target.Volume;
				Receipt.HandoverTargetVolumeAfter = -1;
				Receipt.HandoverTargetCapacity = Target.MaxVolume;
				Receipt.HandoverSourceComposition = sourceText;
				Receipt.HandoverTargetCompositionBefore = targetText;
				Receipt.HandoverText("TargetCompositionExpected", expected);
				Receipt.HandoverText("LiquidIntentDigest", digest);
				Receipt.HandoverPhase = 1;
			}
			catch (Exception exception)
			{
				return RetryLiquid(Receipt, "Liquid intent publication remains retryable: "
					+ exception.Message);
			}
			return true;
		}

		private static bool ReconcileLiquidPhase(GameObject SourceObject, GameObject TargetObject,
			r_KingdomImprovement Receipt, LiquidVolume Source, LiquidVolume Target)
		{
			string expectedText = Receipt.HandoverText("TargetCompositionExpected");
			long expectedLong = (long)Receipt.HandoverTargetVolumeBefore
				+ Receipt.HandoverSourceVolumeBefore;
			string digest = LiquidIntentDigest(Receipt.HandoverSourceId,
				Receipt.HandoverTargetId, Receipt.HandoverConstructionReceipt,
				Receipt.HandoverSourceVolumeBefore.ToString(CultureInfo.InvariantCulture),
				Receipt.HandoverTargetVolumeBefore.ToString(CultureInfo.InvariantCulture),
				Receipt.HandoverTargetCapacity.ToString(CultureInfo.InvariantCulture),
				Receipt.HandoverSourceComposition, Receipt.HandoverTargetCompositionBefore,
				expectedText);
			if (!ExactLiquidReceiptTypes(Receipt) || expectedLong <= 0
				|| expectedLong > int.MaxValue
				|| string.IsNullOrEmpty(expectedText)
				|| Receipt.HandoverText("LiquidIntentDigest") != digest
				|| Receipt.ParentObject.HasIntProperty(HandoverPrefix + "LiquidIntentDigest")
				|| !TryFrozenLiquid(expectedText, (int)expectedLong, out _)
				|| !ExactHandoverObjects(SourceObject, TargetObject, Receipt)
				|| Target == null || Target.MaxVolume != Receipt.HandoverTargetCapacity)
				return FailHandover(Receipt, "Liquid receipt expected aftermath is malformed.");
			int expectedVolume = (int)expectedLong;
			bool before = ExactLiquidEndpoint(SourceObject, Source,
				Receipt.HandoverSourceVolumeBefore, Receipt.HandoverSourceComposition)
				&& ExactLiquidEndpoint(TargetObject, Target, Receipt.HandoverTargetVolumeBefore,
					Receipt.HandoverTargetCompositionBefore);
			bool empty = ExactLiquidEndpoint(SourceObject, Source, 0, EncodeEmptyLiquid());
			bool targetBefore = ExactLiquidEndpoint(TargetObject, Target,
				Receipt.HandoverTargetVolumeBefore, Receipt.HandoverTargetCompositionBefore);
			bool settled = empty && ExactLiquidEndpoint(TargetObject, Target, expectedVolume,
				expectedText);
			bool afterText = Receipt.ParentObject.HasStringProperty(
				HandoverPrefix + "TargetCompositionAfter");
			if (!settled && (Receipt.HandoverTargetVolumeAfter != -1 || afterText))
				return FailHandover(Receipt,
					"Liquid aftermath prefix appeared before its exact physical settlement.");
			if (Receipt.HandoverPhase == 1 && empty && targetBefore) Receipt.HandoverPhase = 2;
			if (Receipt.HandoverPhase == 2 && settled)
			{
				GameObject owner = Receipt.ParentObject;
				if (!ExactOrAbsentInt(owner, "TargetVolumeAfter", expectedVolume)
					|| !ExactOrAbsentText(owner, HandoverPrefix + "TargetCompositionAfter",
						expectedText)) return FailHandover(Receipt,
						"Liquid aftermath publication carries a third value.");
				Receipt.HandoverTargetVolumeAfter = expectedVolume;
				Receipt.HandoverTargetCompositionAfter = expectedText;
				Receipt.HandoverPhase = 3;
			}
			if ((Receipt.HandoverPhase == 1 && before) || (Receipt.HandoverPhase == 2
				&& empty && targetBefore) || (Receipt.HandoverPhase == 3 && settled)) return true;
			return FailHandover(Receipt, "Liquid handover physical state is foreign or partial.");
		}

		private static bool TryPreviewLiquidAfter(LiquidVolume Source, LiquidVolume Target,
			out string Expected)
		{
			Expected = null;
			try
			{
				LiquidVolume incoming = new LiquidVolume { Volume = Source.Volume,
					MaxVolume = Source.MaxVolume,
					ComponentLiquids = new Dictionary<string, int>(Source.ComponentLiquids) };
				LiquidVolume preview = new LiquidVolume { Volume = Target.Volume,
					MaxVolume = Target.MaxVolume,
					ComponentLiquids = new Dictionary<string, int>(Target.ComponentLiquids) };
				if (!preview.MixWith(incoming) || preview.Volume != Target.Volume + Source.Volume)
					return false;
				Expected = EncodeLiquid(preview);
				return Expected != null;
			}
			catch { return false; }
		}

		private static bool ExactOrAbsentInt(GameObject Owner, string Name, int Expected)
		{
			string property = HandoverPrefix + Name;
			return Owner != null && !Owner.HasStringProperty(property)
				&& (!Owner.HasIntProperty(property) || Owner.GetIntProperty(property) == Expected);
		}

		private static bool ExactLiquidReceiptTypes(r_KingdomImprovement Receipt)
		{
			GameObject owner = Receipt?.ParentObject;
			if (owner == null) return false;
			string[] integers = { "LiquidPhase", "SourceVolumeBefore", "SourceVolumeAfter",
				"TargetVolumeBefore", "TargetVolumeAfter", "TargetCapacity" };
			string[] texts = { "SourceComposition", "TargetCompositionBefore",
				"TargetCompositionExpected", "LiquidIntentDigest" };
			for (int i = 0; i < integers.Length; i++)
			{
				string property = HandoverPrefix + integers[i];
				if (!owner.HasIntProperty(property) || owner.HasStringProperty(property)) return false;
			}
			for (int i = 0; i < texts.Length; i++)
			{
				string property = HandoverPrefix + texts[i];
				if (!owner.HasStringProperty(property) || owner.HasIntProperty(property)) return false;
			}
			string after = HandoverPrefix + "TargetCompositionAfter";
			if (owner.HasIntProperty(after)) return false;
			if (Receipt.HandoverPhase >= 3) return owner.HasStringProperty(after)
				&& owner.GetStringProperty(after) == Receipt.HandoverText("TargetCompositionExpected");
			return !owner.HasStringProperty(after)
				|| owner.GetStringProperty(after) == Receipt.HandoverText(
					"TargetCompositionExpected");
		}

	}
}
