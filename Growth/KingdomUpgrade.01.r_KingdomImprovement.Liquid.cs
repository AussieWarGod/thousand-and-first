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
			if (Receipt == null || !Receipt.HandoverFlagsValid())
				return FailHandover(Receipt, "Handover boolean flags are corrupt.");
			if (Receipt.HandoverQuarantined) return false;
			if (!ExactHandoverObjects(SourceObject, TargetObject, Receipt))
				return FailHandover(Receipt, "Handover endpoints do not match their exact IDs.");
			if (Receipt.HandoverPhase < 0 || Receipt.HandoverPhase > 3)
				return FailHandover(Receipt, "Liquid handover phase is corrupt.");
			if (!ExactLiquidReceiptShape(Receipt))
				return FailHandover(Receipt, "Liquid handover receipt is corrupt or unbounded.");
			LiquidVolume source = SourceObject.GetPart<LiquidVolume>();
			LiquidVolume target = TargetObject.GetPart<LiquidVolume>();
			if (source == null || source.Volume <= 0)
			{
				if (Receipt.HandoverPhase >= 1 && Receipt.HandoverSourceVolumeBefore > 0)
					return ResumeDrainedLiquid(SourceObject, TargetObject, Receipt, source, target,
						out Moved);
				Receipt.HandoverPhase = 3;
				return true;
			}
			if (target == null)
				return FailHandover(Receipt, "Liquid source has no exact successor vessel.");
			if (Receipt.HandoverPhase == 0)
			{
				int space = target.MaxVolume < 0 ? int.MaxValue : target.MaxVolume - target.Volume;
				string sourceComposition = EncodeLiquid(source);
				string targetComposition = EncodeLiquid(target);
				if (source.Volume <= 0 || target.Volume < 0 || space < source.Volume
					|| (long)target.Volume + source.Volume > int.MaxValue
					|| sourceComposition == null || targetComposition == null
					|| !TryFrozenLiquid(sourceComposition, source.Volume, out _)
					|| (target.Volume > 0
						&& !TryFrozenLiquid(targetComposition, target.Volume, out _)))
					return FailHandover(Receipt, "Successor liquid capacity changed before handover.");
				Receipt.HandoverSourceId = SourceObject.ID;
				Receipt.HandoverTargetId = TargetObject.ID;
				Receipt.HandoverSourceVolumeBefore = source.Volume;
				Receipt.HandoverSourceVolumeAfter = 0;
				Receipt.HandoverTargetVolumeBefore = target.Volume;
				Receipt.HandoverTargetVolumeAfter = -1;
				Receipt.HandoverTargetCapacity = target.MaxVolume;
				Receipt.HandoverSourceComposition = sourceComposition;
				Receipt.HandoverTargetCompositionBefore = targetComposition;
				Receipt.HandoverTargetCompositionAfter = null;
				Receipt.HandoverPhase = 1;
			}
			if (Receipt.HandoverPhase == 3)
			{
				if (!ExactLiquidEndpoint(SourceObject, source, Receipt.HandoverSourceVolumeAfter,
					EncodeEmptyLiquid()) || !ExactLiquidEndpoint(TargetObject, target,
					Receipt.HandoverTargetVolumeAfter, Receipt.HandoverTargetCompositionAfter)
					|| target.MaxVolume != Receipt.HandoverTargetCapacity)
					return FailHandover(Receipt, "Settled liquid receipt no longer matches both vessels.");
				Moved = Receipt.HandoverSourceVolumeBefore;
				return true;
			}
			if (Receipt.HandoverPhase != 1
				|| !ExactLiquidEndpoint(SourceObject, source, Receipt.HandoverSourceVolumeBefore,
					Receipt.HandoverSourceComposition)
				|| !ExactLiquidEndpoint(TargetObject, target, Receipt.HandoverTargetVolumeBefore,
					Receipt.HandoverTargetCompositionBefore)
				|| target.MaxVolume != Receipt.HandoverTargetCapacity)
				return FailHandover(Receipt, "Pending liquid receipt is ambiguous before drain.");

			int drained = KingdomLiquids.Drain(source, Receipt.HandoverSourceVolumeBefore);
			ObserveHandoverMutation(SourceObject, TargetObject, SourceObject.CurrentCell, null);
			if (drained != Receipt.HandoverSourceVolumeBefore
				|| !ExactLiquidEndpoint(SourceObject, source, 0, EncodeEmptyLiquid())
				|| !ExactHandoverObjects(SourceObject, TargetObject, Receipt)
				|| !ReferenceEquals(target, TargetObject.GetPart<LiquidVolume>()))
				return FailHandover(Receipt, "Liquid drain did not leave the exact frozen aftermath.");
			Receipt.HandoverPhase = 2;
			return ResumeDrainedLiquid(SourceObject, TargetObject, Receipt, source, target,
				out Moved);
		}

		private static bool ResumeDrainedLiquid(GameObject SourceObject, GameObject TargetObject,
			r_KingdomImprovement Receipt, LiquidVolume Source, LiquidVolume Target, out int Moved)
		{
			Moved = 0;
			if (Receipt.HandoverPhase == 3)
			{
				if (ExactLiquidEndpoint(SourceObject, Source, Receipt.HandoverSourceVolumeAfter,
						EncodeEmptyLiquid()) && ExactLiquidEndpoint(TargetObject, Target,
						Receipt.HandoverTargetVolumeAfter, Receipt.HandoverTargetCompositionAfter)
					&& Target.MaxVolume == Receipt.HandoverTargetCapacity
					&& ExactHandoverObjects(SourceObject, TargetObject, Receipt))
				{
					Moved = Receipt.HandoverSourceVolumeBefore;
					return true;
				}
				return FailHandover(Receipt, "Completed liquid aftermath changed before recovery.");
			}
			if (Receipt.HandoverPhase != 2 || Source == null || Target == null
				|| !ExactLiquidEndpoint(SourceObject, Source, 0, EncodeEmptyLiquid())
				|| !ExactLiquidEndpoint(TargetObject, Target, Receipt.HandoverTargetVolumeBefore,
					Receipt.HandoverTargetCompositionBefore)
				|| Target.MaxVolume != Receipt.HandoverTargetCapacity
				|| !ExactHandoverObjects(SourceObject, TargetObject, Receipt))
				return FailHandover(Receipt, "Drained liquid receipt is ambiguous before fill.");
			LiquidVolume frozen;
			if (!TryFrozenLiquid(Receipt.HandoverSourceComposition,
				Receipt.HandoverSourceVolumeBefore, out frozen))
				return FailHandover(Receipt, "Frozen liquid composition cannot be reconstructed.");
			bool accepted = false;
			try { accepted = Target.MixWith(frozen, PouredFrom: SourceObject); }
			catch (System.Exception ex)
			{
				ObserveHandoverMutation(SourceObject, TargetObject, SourceObject.CurrentCell, null);
				return CompensateLiquid(SourceObject, TargetObject, Receipt, Source, Target,
					frozen, "Liquid fill threw: " + ex.Message);
			}
			ObserveHandoverMutation(SourceObject, TargetObject, SourceObject.CurrentCell, null);
			if (!ExactHandoverObjects(SourceObject, TargetObject, Receipt)
				|| !ReferenceEquals(Source, SourceObject.GetPart<LiquidVolume>())
				|| !ReferenceEquals(Target, TargetObject.GetPart<LiquidVolume>()))
				return FailHandover(Receipt, "A liquid endpoint changed during fill callback.");
			int expected = Receipt.HandoverTargetVolumeBefore
				+ Receipt.HandoverSourceVolumeBefore;
			if (accepted && Target.Volume == expected
				&& ExactLiquidEndpoint(SourceObject, Source, 0, EncodeEmptyLiquid()))
			{
				string after = EncodeLiquid(Target);
				if (after == null || !ExactLiquidEndpoint(TargetObject, Target, expected, after))
					return FailHandover(Receipt,
						"Liquid fill produced an invalid or unbounded after-composition.");
				Receipt.HandoverTargetVolumeAfter = Target.Volume;
				Receipt.HandoverTargetCompositionAfter = after;
				Receipt.HandoverPhase = 3;
				Moved = Receipt.HandoverSourceVolumeBefore;
				return true;
			}
			return CompensateLiquid(SourceObject, TargetObject, Receipt, Source, Target,
				frozen, "Liquid fill was vetoed or partial.");
		}

		private static bool CompensateLiquid(GameObject SourceObject, GameObject TargetObject,
			r_KingdomImprovement Receipt, LiquidVolume Source, LiquidVolume Target,
			LiquidVolume Frozen, string Failure)
		{
			// Exact compensation is possible only when target still equals its frozen before-image.
			if (!ExactLiquidEndpoint(TargetObject, Target, Receipt.HandoverTargetVolumeBefore,
				Receipt.HandoverTargetCompositionBefore))
				return FailHandover(Receipt, Failure + " Target changed, so compensation is unsafe.");
			try { Source.MixWith(Frozen, PouredFrom: TargetObject); }
			catch (System.Exception ex)
			{
				ObserveHandoverMutation(SourceObject, TargetObject, SourceObject.CurrentCell, null);
				return FailHandover(Receipt, Failure + " Compensation threw: " + ex.Message);
			}
			ObserveHandoverMutation(SourceObject, TargetObject, SourceObject.CurrentCell, null);
			if (!ExactLiquidEndpoint(SourceObject, Source, Receipt.HandoverSourceVolumeBefore,
					Receipt.HandoverSourceComposition)
				|| !ExactLiquidEndpoint(TargetObject, Target, Receipt.HandoverTargetVolumeBefore,
					Receipt.HandoverTargetCompositionBefore)
				|| Target.MaxVolume != Receipt.HandoverTargetCapacity
				|| !ExactHandoverObjects(SourceObject, TargetObject, Receipt))
				return FailHandover(Receipt, Failure + " Exact compensation could not be proved.");
			Receipt.HandoverPhase = 0;
			Receipt.HandoverFailure = Failure;
			return false;
		}

	}
}
