using System;
using System.Globalization;
using System.Reflection;
using XRL.Liquids;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		internal static bool TryPublishLiquidCustody(GameObject Source, GameObject Target,
			r_KingdomImprovement Receipt)
		{
			if (!ExactHandoverObjects(Source, Target, Receipt) || Receipt.HandoverPhase != 3
				|| !ExactLiquidReceiptShape(Receipt))
				return FailHandover(Receipt, "Settled liquid custody cannot be published exactly.");
			LiquidVolume source = Source.GetPart<LiquidVolume>();
			LiquidVolume target = Target.GetPart<LiquidVolume>();
			if (source != null && (source.Volume != 0 || EncodeLiquid(source) != EncodeEmptyLiquid()))
				return FailHandover(Receipt, "Liquid source is not exactly empty after handover.");
			int moved = Receipt.HandoverSourceVolumeBefore;
			int hasVessel = target == null ? 0 : 1;
			int volume = target == null ? 0 : target.Volume;
			int capacity = target == null ? 0 : target.MaxVolume;
			string composition = target == null ? EncodeEmptyLiquid() : EncodeLiquid(target);
			if (composition == null || moved < 0 || moved > 0 && (target == null
					|| volume != Receipt.HandoverTargetVolumeAfter
					|| capacity != Receipt.HandoverTargetCapacity
					|| composition != Receipt.HandoverTargetCompositionAfter))
				return FailHandover(Receipt, "Liquid aftermath no longer matches its exact receipt.");
			string digest = LiquidCustodyDigest(Target.IDIfAssigned,
				Receipt.HandoverConstructionReceipt, moved, hasVessel, volume, capacity, composition);
			string schemaKey = ManifestKey("LiquidSchema");
			if (Target.HasStringProperty(schemaKey))
				return FailHandover(Receipt, "Liquid custody schema has the wrong type.");
			if (Target.HasIntProperty(schemaKey))
			{
				string failure = null;
				int proved = 0;
				return Target.GetIntProperty(schemaKey) == 1
					&& VerifyLiquidCustody(Target, Receipt.HandoverConstructionReceipt,
						out proved, out failure) && proved == moved
					|| FailHandover(Receipt, failure ?? "Liquid custody schema is invalid.");
			}
			if (!ExactManifestTextOrAbsent(Target, "LiquidTargetId", Target.IDIfAssigned)
				|| !ExactManifestTextOrAbsent(Target, "LiquidConstructionReceipt",
					Receipt.HandoverConstructionReceipt)
				|| !ExactManifestIntOrAbsent(Target, "LiquidMoved", moved)
				|| !ExactManifestIntOrAbsent(Target, "LiquidHasVessel", hasVessel)
				|| !ExactManifestIntOrAbsent(Target, "LiquidVolume", volume)
				|| !ExactManifestIntOrAbsent(Target, "LiquidCapacity", capacity)
				|| !ExactManifestTextOrAbsent(Target, "LiquidComposition", composition)
				|| !ExactManifestTextOrAbsent(Target, "LiquidDigest", digest))
				return FailHandover(Receipt, "Liquid custody prefix carries a third value.");
			try
			{
				Target.SetStringProperty(ManifestKey("LiquidTargetId"), Target.IDIfAssigned);
				Target.SetStringProperty(ManifestKey("LiquidConstructionReceipt"),
					Receipt.HandoverConstructionReceipt);
				Target.SetIntProperty(ManifestKey("LiquidMoved"), moved);
				Target.SetIntProperty(ManifestKey("LiquidHasVessel"), hasVessel);
				Target.SetIntProperty(ManifestKey("LiquidVolume"), volume);
				Target.SetIntProperty(ManifestKey("LiquidCapacity"), capacity);
				Target.SetStringProperty(ManifestKey("LiquidComposition"), composition);
				Target.SetStringProperty(ManifestKey("LiquidDigest"), digest);
				Target.SetIntProperty(schemaKey, 1);
			}
			catch (Exception exception)
			{
				Receipt.HandoverFailure = "Liquid custody publication remains retryable: "
					+ exception.Message;
				return false;
			}
			string verifyFailure;
			int verified;
			return VerifyLiquidCustody(Target, Receipt.HandoverConstructionReceipt,
				out verified, out verifyFailure) && verified == moved
				|| FailHandover(Receipt, verifyFailure ?? "Liquid custody did not settle.");
		}

		private static bool VerifyLiquidCustody(GameObject Target, string ConstructionReceipt,
			out int Moved, out string Failure)
		{
			Moved = 0;
			Failure = null;
			if (!RequiredManifestInt(Target, "LiquidSchema", out int schema) || schema != 1
				|| !RequiredManifestText(Target, "LiquidTargetId", out string targetId)
				|| !RequiredManifestText(Target, "LiquidConstructionReceipt", out string receipt)
				|| !RequiredManifestInt(Target, "LiquidMoved", out int moved)
				|| !RequiredManifestInt(Target, "LiquidHasVessel", out int hasVessel)
				|| !RequiredManifestInt(Target, "LiquidVolume", out int volume)
				|| !RequiredManifestInt(Target, "LiquidCapacity", out int capacity)
				|| !RequiredManifestText(Target, "LiquidComposition", out string composition)
				|| !RequiredManifestText(Target, "LiquidDigest", out string digest)
				|| targetId != Target?.IDIfAssigned || receipt != ConstructionReceipt || moved < 0
				|| hasVessel < 0 || hasVessel > 1 || volume < 0
				|| composition == null || composition.Length > MaxHandoverText
				|| digest != LiquidCustodyDigest(targetId, receipt, moved, hasVessel,
					volume, capacity, composition))
				return ManifestFailure(out Failure, "Liquid custody receipt is malformed.");
			LiquidVolume target = Target.GetPart<LiquidVolume>();
			if (hasVessel == 0)
			{
				if (target != null || volume != 0 || capacity != 0
					|| composition != EncodeEmptyLiquid())
					return ManifestFailure(out Failure, "A vessel appeared after liquid settlement.");
			}
			else if (target == null || target.ParentObject != Target || target.Volume != volume
				|| target.MaxVolume != capacity || EncodeLiquid(target) != composition
				|| volume == 0 && composition != EncodeEmptyLiquid()
				|| volume > 0 && !TryFrozenLiquid(composition, volume, out _))
				return ManifestFailure(out Failure, "Final successor liquid composition changed.");
			Moved = moved;
			return true;
		}

		private static string LiquidCustodyDigest(string TargetId, string Receipt, int Moved,
			int HasVessel, int Volume, int Capacity, string Composition)
		{
			return LiquidIntentDigest(TargetId, Receipt,
				Moved.ToString(CultureInfo.InvariantCulture),
				HasVessel.ToString(CultureInfo.InvariantCulture),
				Volume.ToString(CultureInfo.InvariantCulture),
				Capacity.ToString(CultureInfo.InvariantCulture), Composition);
		}

		internal static bool LiquidEndpointHasContextRisk(LiquidVolume Volume)
		{
			if (Volume?.ComponentLiquids == null) return true;
			GameObject owner = Volume.ParentObject;
			if (owner != null && (owner.HasRegisteredEvent("LiquidMixing")
				|| owner.HasRegisteredEvent("LiquidMixed")
				|| owner.WantEvent(LiquidMixedEvent.ID, MinEvent.CascadeLevel))) return true;
			foreach (string id in Volume.ComponentLiquids.Keys)
			{
				if (string.Equals(id, "neutronflux", StringComparison.OrdinalIgnoreCase))
					return true;
				BaseLiquid liquid;
				try { liquid = LiquidVolume.GetLiquid(id); }
				catch { return true; }
				if (liquid == null || OverridesLiquidHook(liquid, "MixingWith")
					|| OverridesLiquidHook(liquid, "MixedWith")
					|| OverridesLiquidHook(liquid, "FillingContainer")) return true;
			}
			return false;
		}

		private static bool OverridesLiquidHook(BaseLiquid Liquid, string Name)
		{
			try
			{
				MethodInfo method = Liquid.GetType().GetMethod(Name);
				return method == null || method.DeclaringType != typeof(BaseLiquid);
			}
			catch { return true; }
		}
	}
}
