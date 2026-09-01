using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRuntime
	{
		/// <summary>
		/// Prepares an ordinary tier on its standing envelope when possible, otherwise proves and
		/// freezes the first deterministic containing envelope for an explicitly authored size-growth
		/// mode. No unconstrained siting or layout preference may move an occupied building.
		/// </summary>
		public static bool TryPrepareSuccessorForUpgrade(KingdomSystem System, Zone Z,
			GameObject Owner, KingdomArchitectureIntent Before, string SuccessorBuildKey,
			out KingdomArchitectureIntent Intent, out string Failure)
		{
			if (TryPrepareSuccessor(System, Z, Before, SuccessorBuildKey,
				out Intent, out Failure)) return true;
			string fixedEnvelopeFailure = Failure;
			Intent = null;

			ArchitectureLayoutSnapshot before;
			if (!TryValidateIntent(Before, out before, out Failure)
				|| !KingdomArchitectureRules.IsLatestSnapshotEncoding(Before.EncodedSnapshot))
				return Failure != null ? false : Fail(
					"legacy architecture has no authored plot-envelope transition", out Failure);
			if (KingdomArchitecture.HasExactOrdinarySuccessor(before.BuildKey,
				SuccessorBuildKey, before.PlanKey, before.BindingKey, before.LotType,
				before.LotSize))
				return Fail(fixedEnvelopeFailure
					?? "declared same-envelope successor refused without a reason", out Failure);
			ArchitectureLayoutSnapshot after;
			string expansionResolutionFailure;
			if (!KingdomArchitecture.TryResolveExpandingSuccessor(before.BuildKey,
				before.VariantKey, SuccessorBuildKey, before.PlanKey, before.BindingKey,
				before.LotType, before.LotSize, before.Facing, out after,
				out expansionResolutionFailure))
			{
				Failure = fixedEnvelopeFailure ?? expansionResolutionFailure;
				return false;
			}
			if (System == null || !System.Founded || Z == null
				|| !ValidRectInZone(Before.Rect, Z))
				return Fail("authored plot-envelope successor needs its founded settlement "
					+ "and exact loaded lot", out Failure);
			ArchitectureLayoutDelta delta;
			if (!KingdomArchitectureRules.TryBuildDelta(before, after,
				after.IncomingTransitionMode, out delta, out Failure)) return false;
			int canonicalWidth;
			int canonicalHeight;
			KingdomPlotRules.PlotRect interior;
			if (!KingdomArchitectureRules.TryCanonicalDimensions(after.LotSize,
				out canonicalWidth, out canonicalHeight)
				|| !KingdomPlotRules.TryInterior(Z.Width, Z.Height, out interior))
				return Fail("larger authored successor has no exact settlement interior envelope",
					out Failure);
			List<KingdomPlotPoseCandidate> candidates =
				KingdomPlotPoseSitingRules.EnumerateContaining(Before.Rect, interior,
					canonicalWidth, canonicalHeight);
			if (candidates.Count == 0)
				return Fail("no larger authored envelope can contain the standing plot",
					out Failure);

			string encoded;
			string hash;
			if (!KingdomArchitectureRules.TryEncodeSnapshot(after, out encoded, out Failure)
				|| !KingdomArchitectureRules.TrySnapshotHash(after, out hash, out Failure)) return false;
			SitingProbe probe;
			if (!TryCreateSitingProbe(System, Z, candidates[0].Rect, after.BuildKey,
				after.LotType, out probe, out Failure)) return false;
			int worldWidth;
			int worldHeight;
			if (!KingdomArchitectureRules.TryWorldDimensions(after.Width, after.Height,
				after.Facing, out worldWidth, out worldHeight))
				return Fail("larger authored successor has no exact frozen world pose", out Failure);

			bool aligned = false;
			string firstCandidateFailure = null;
			for (int i = 0; i < candidates.Count; i++)
			{
				KingdomPlotRules.PlotRect rect = candidates[i].Rect;
				if (rect.Width != worldWidth || rect.Height != worldHeight) continue;
				int mainX;
				int mainY;
				string candidateFailure;
				if (!TryWorldCoordinate(after, rect, after.MainX, after.MainY,
					out mainX, out mainY, out candidateFailure))
				{
					if (firstCandidateFailure == null) firstCandidateFailure = candidateFailure;
					continue;
				}
				if (mainX != Before.MainWorldX || mainY != Before.MainWorldY) continue;
				aligned = true;
				if (!probe.TryAcceptExact(rect, after, true, out candidateFailure))
				{
					if (firstCandidateFailure == null) firstCandidateFailure = candidateFailure;
					continue;
				}
				KingdomArchitectureIntent prepared = KingdomArchitectureIntent.Create(after,
					encoded, hash, rect, mainX, mainY);
				ArchitectureLayoutSnapshot checkedSnapshot;
				if (!TryValidateIntent(prepared, out checkedSnapshot, out candidateFailure)
					|| !KingdomArchitectureStamper.TryProveEnvelopeGrowth(System, Z, Owner, null,
						prepared, false, out candidateFailure))
				{
					if (firstCandidateFailure == null) firstCandidateFailure = candidateFailure;
					continue;
				}
				Intent = prepared;
				Failure = null;
				return true;
			}
			return Fail(firstCandidateFailure ?? (aligned
				? "no containing authored envelope has clear owned ground and public road ingress"
				: "larger authored successor cannot contain the standing plot without moving "
					+ "its frozen main root"), out Failure);
		}
	}
}
