using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !TAF_TESTS
using XRL;
using XRL.World;
using XRL.World.Parts;
#endif

namespace ThousandAndFirst
{
	internal static partial class KingdomInheritEngine
	{
		private static bool TryPrepare(KingdomSealRecord Legacy, KingdomSealReceipt Receipt,
			string TargetZoneId, IKingdomInheritEngineHost Host, out Prepared Prepared,
			out KingdomInheritApplyResult Failure)
		{
			Prepared = null;
			Failure = null;
			if (Legacy == null || Receipt == null || Host == null || TargetZoneId == null)
			{
				Failure = Failed(KingdomInheritApplyFault.NullInput,
					"the inherited record, reservation, and zone are all required", "");
				return false;
			}

			KingdomSealRecord canonical;
			KingdomSealFault sealFault;
			string detail;
			try
			{
				if (!KingdomSealRecord.TryReadBody(KingdomSealRecord.CurrentSchema, Legacy.WriteBody(),
					out canonical, out sealFault, out detail))
				{
					Failure = Failed(KingdomInheritApplyFault.LegacyNotPromoted,
						Nonempty(detail, "the inherited record is malformed"), "");
					return false;
				}
			}
			catch (Exception ex)
			{
				Failure = Failed(KingdomInheritApplyFault.LegacyNotPromoted,
					"the inherited record is malformed: " + ex.Message, "");
				return false;
			}
			if (canonical.Status != KingdomSealStatus.Promoted || !canonical.IsResolved)
			{
				Failure = Failed(KingdomInheritApplyFault.LegacyNotPromoted,
					"only an exact promoted and resolved legacy may be reconstructed", "");
				return false;
			}
			long expectedSeed = KingdomSealRules.InterregnumSeed(new KingdomSealLineage(
				canonical.LineageId, canonical.LegacyId, canonical.OriginGameId,
				canonical.Generation, canonical.Revision));
			int expectedRoll = KingdomRules.InterregnumRoll(expectedSeed);
			KingdomRules.InheritedState expectedState = KingdomRules.ResolveInheritedState(
				canonical.Vigour, expectedRoll, canonical.Population);
			if (canonical.InterregnumRoll != expectedRoll
				|| canonical.InheritedState != (int)expectedState)
			{
				Failure = Failed(KingdomInheritApplyFault.LegacyNotPromoted,
					"the promoted legacy's fixed interregnum result does not match its immutable facts", "");
				return false;
			}
			if (Receipt.State != KingdomSealReceiptState.Reserved)
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptNotReserved,
					"the legacy receipt is not reserved", "");
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Receipt.LineageId))
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt's lineage id is malformed", "");
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Receipt.LegacyId))
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt's legacy id is malformed: '" + (Receipt.LegacyId ?? "<null>") + "'", "");
				return false;
			}
			if (!KingdomSealReceipt.ValidId(Receipt.TargetGameId))
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt's target game id is malformed", "");
				return false;
			}
			if (Receipt.WrittenTick < 0L)
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt's written tick is malformed", "");
				return false;
			}
			if (Receipt.LineageId != canonical.LineageId || Receipt.LegacyId != canonical.LegacyId)
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the reserved receipt does not name this exact promoted legacy", "");
				return false;
			}
			if (Host.TargetGameId != Receipt.TargetGameId)
			{
				Failure = Failed(KingdomInheritApplyFault.TargetGameMismatch,
					"the reserved receipt names a different target game", "");
				return false;
			}
			if (TargetZoneId.Length == 0 || TargetZoneId.Length > KingdomSealRecord.MaxIdChars
				|| !KingdomSealRules.IsToken(TargetZoneId) || Host.ZoneId != TargetZoneId)
			{
				Failure = Failed(KingdomInheritApplyFault.TargetZoneMismatch,
					"this zone is not the exact selected new-world target", "");
				return false;
			}

			int reconstruction = ReconstructionVersionFor(canonical);
			if (reconstruction <= 0)
			{
				Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
					"the external seal's spatial shape is unsupported by this build", "");
				return false;
			}

			KingdomInheritPlacement placement;
			KingdomInheritFault inheritFault;
			if (!KingdomInheritRules.TryPrepare(canonical,
				(KingdomRules.InheritedState)canonical.InheritedState,
				canonical.InterregnumRoll, out placement, out inheritFault) || placement == null)
			{
				Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
					KingdomInheritRules.FailureLine(inheritFault), "");
				return false;
			}

			KingdomInheritBuildSpec[] specs = new KingdomInheritBuildSpec[
				placement.Count + placement.StreetCount];
			int cairns = 0;
			for (int i = 0; i < placement.Count; i++)
			{
				KingdomInheritWork work = placement.WorkAt(i);
				string blueprint;
				int width;
				int height;
				int left;
				int top;
				if (work == null || !KingdomInheritRules.TryResolveBlueprint(work.Key, out blueprint))
				{
					Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
						"the prepared plan contains a semantic key this build cannot resolve", "");
					return false;
				}
				if (work.ArchitectureSnapshot.Length > 0)
				{
					ArchitectureLayoutSnapshot snapshot;
					KingdomInheritanceSpatialRules.Rect rect;
					if (!KingdomArchitectureRules.TryDecodeSnapshot(work.ArchitectureSnapshot,
						out snapshot, out _)
						|| !KingdomInheritanceSpatialRules.TrySnapshotRect(snapshot,
							work.X, work.Y, out rect))
					{
						Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
							"the prepared authored work no longer decodes", "");
						return false;
					}
					width = rect.X2 - rect.X1 + 1;
					height = rect.Y2 - rect.Y1 + 1;
					left = rect.X1;
					top = rect.Y1;
				}
				else if (!KingdomInheritRules.TryFootprint(work.Key, out width, out height))
				{
					Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
						"the prepared plan contains an unknown footprint", "");
					return false;
				}
				else
				{
					left = work.X - (width - 1) / 2;
					top = work.Y - (height - 1) / 2;
				}
				specs[i] = new KingdomInheritBuildSpec(i, work, blueprint, left, top,
					width, height);
				if (work.Key == KingdomInheritRules.FounderCairnKey
					&& work.X == placement.CairnX && work.Y == placement.CairnY)
				{
					cairns++;
				}
			}
			for (int i = 0; i < placement.StreetCount; i++)
				specs[placement.Count + i] = new KingdomInheritBuildSpec(placement.Count + i,
					placement.StreetXAt(i), placement.StreetYAt(i));
			if (cairns != 1)
			{
				Failure = Failed(KingdomInheritApplyFault.PlanInvalid,
					"the prepared plan does not carry exactly one founder cairn", "");
				return false;
			}

			string marker;
			if (!KingdomInheritanceStateRules.TryComposeApplicationMarker(canonical, Receipt,
				TargetZoneId, reconstruction, out marker))
			{
				Failure = Failed(KingdomInheritApplyFault.ReceiptMismatch,
					"the exact reservation could not form its deterministic application marker", "");
				return false;
			}
			Prepared = new Prepared
			{
				Legacy = canonical,
				Placement = placement,
				Specs = specs,
				Marker = marker,
				CairnText = ComposeCairnText(canonical)
			};
			return true;
		}

	}
}
