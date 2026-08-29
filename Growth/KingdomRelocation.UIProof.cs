using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static bool ReproveApproved(KingdomSystem System, Zone Zone, GameObject Heart,
			string SuccessorKey, PreparedPlan Approved, out PreparedPlan Exact,
			out string Failure)
		{
			Exact = null; Failure = null;
			if (Approved?.Receipt == null || HasActive(Zone))
			{ Failure = "Another ring-call authority now owns this ground."; return false; }
			Dictionary<string, KingdomPlotRules.PlotRect> targets =
				new Dictionary<string, KingdomPlotRules.PlotRect>();
			for (int i = 0; i < Approved.Receipt.Moves.Count; i++)
				targets.Add(Approved.Receipt.Moves[i].PlotId,
					Runtime(Approved.Receipt.Moves[i].Destination));
			if (!TryPreparePlan(System, Zone, Heart, SuccessorKey, targets,
				out Exact, out Failure)) return false;
			if (!SamePlan(Approved.Receipt, Exact.Receipt))
			{
				Exact = null;
				Failure = "The source-to-destination evidence changed after preview; review a new plan.";
				return false;
			}
			return true;
		}

		private static bool SamePlan(KingdomRelocationReceipt A, KingdomRelocationReceipt B)
		{
			if (A == null || B == null || A.ZoneId != B.ZoneId || A.RealmId != B.RealmId
				|| A.HeartId != B.HeartId || A.SuccessorKey != B.SuccessorKey
				|| !KingdomRelocationRules.SameRect(A.HeartGround, B.HeartGround)
				|| A.Moves.Count != B.Moves.Count) return false;
			for (int i = 0; i < A.Moves.Count; i++) if (!SameMove(A.Moves[i], B.Moves[i])) return false;
			return true;
		}

		private static bool SameMove(KingdomRelocationMove A, KingdomRelocationMove B)
		{
			if (A.RootId != B.RootId || A.PlotId != B.PlotId || A.BuildKey != B.BuildKey
				|| !KingdomRelocationRules.SameRect(A.Source, B.Source)
				|| !KingdomRelocationRules.SameRect(A.Destination, B.Destination)
				|| !KingdomRelocationRules.SameRect(A.Footprint, B.Footprint)
				|| A.Roof != B.Roof || A.RequiredTicks != B.RequiredTicks
				|| A.Rows.Count != B.Rows.Count || A.Clearance.Count != B.Clearance.Count
				|| !SameArchitecture(A.Architecture, B.Architecture)) return false;
			for (int i = 0; i < A.Rows.Count; i++)
			{
				KingdomRelocationRow x = A.Rows[i], y = B.Rows[i];
				if (x.ObjectId != y.ObjectId || x.Blueprint != y.Blueprint
					|| x.OffsetX != y.OffsetX || x.OffsetY != y.OffsetY || x.Root != y.Root)
					return false;
			}
			for (int i = 0; i < A.Clearance.Count; i++)
			{
				KingdomRelocationClearRow x = A.Clearance[i], y = B.Clearance[i];
				if (x.ObjectId != y.ObjectId || x.Blueprint != y.Blueprint
					|| x.X != y.X || x.Y != y.Y) return false;
			}
			return true;
		}

		private static bool SameArchitecture(KingdomRelocationArchitecture A,
			KingdomRelocationArchitecture B)
		{
			if (A == null || B == null) return A == null && B == null;
			return A.Schema == B.Schema && A.BuildKey == B.BuildKey
				&& A.PlanKey == B.PlanKey && A.BindingKey == B.BindingKey
				&& A.TierKey == B.TierKey && A.VariantKey == B.VariantKey
				&& A.PaletteKey == B.PaletteKey && A.LotType == B.LotType
				&& A.LotSize == B.LotSize && A.Facing == B.Facing
				&& A.Snapshot == B.Snapshot && A.Hash == B.Hash
				&& A.MainX == B.MainX && A.MainY == B.MainY;
		}
	}
}
