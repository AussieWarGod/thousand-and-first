using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Reads one unbranched improvement history from the founding terminal's identity.
	/// The caller supplies a validated durable registry and separately proves physical custody.</summary>
	internal static class KingdomFoundingHeartUpgradePathRules
	{
		internal static bool TryRead(IList<KingdomConstructionJob> Jobs, string Origin,
			string Forbidden, string Owner, string Zone, out List<KingdomConstructionJob> Completed,
			out KingdomConstructionJob Pending, int OriginRung = 1)
		{
			Completed = new List<KingdomConstructionJob>();
			Pending = null;
			if (Jobs == null || Jobs.Count > KingdomConstructionRules.MaxRows
				|| string.IsNullOrEmpty(Origin) || string.IsNullOrEmpty(Owner)
				|| string.IsNullOrEmpty(Zone) || Origin == Forbidden
				|| OriginRung < 1 || OriginRung >= KingdomPlotRules.HeartRungKeys.Length) return false;
			var seen = new HashSet<string> { Origin };
			if (!string.IsNullOrEmpty(Forbidden)) seen.Add(Forbidden);
			string subject = Origin;
			for (int hop = 0; hop <= Jobs.Count; hop++)
			{
				KingdomConstructionJob next = null;
				foreach (var row in Jobs)
				{
					if (row == null || row.Route != KingdomConstructionRoute.Improvement
						|| row.SubjectId != subject) continue;
					if (next != null) return false;
					next = row;
				}
				if (next == null) return Completed.Count > 0;
				if (next.OwnerKey != Owner || next.ZoneId != Zone || string.IsNullOrEmpty(next.Id)
					|| !string.IsNullOrEmpty(next.SourceId) && next.SourceId != subject
					|| string.IsNullOrEmpty(next.OutputId) || !seen.Add(next.OutputId)
					|| KingdomPlotRules.HeartRungOf(next.TargetKey) != OriginRung + Completed.Count + 1
					|| Completed.Count > 0 && (next.X != Completed[0].X || next.Y != Completed[0].Y)
					|| next.Phase == KingdomConstructionPhase.Cancelled) return false;
				if (next.Phase != KingdomConstructionPhase.Complete)
				{
					Pending = next;
					return true;
				}
				if (!string.IsNullOrEmpty(next.Failure)
					|| next.PhysicalPhase != KingdomPhysicalPhase.EffectsSettled) return false;
				Completed.Add(next);
				subject = next.OutputId;
			}
			return false;
		}

		internal static bool OutputReceiptMatches(KingdomConstructionJob Completed,
			KingdomConstructionJob Pending, string ObjectId, string Receipt)
		{
			if (Completed == null || Completed.OutputId != ObjectId || string.IsNullOrEmpty(Receipt))
				return false;
			if (Receipt == Completed.Id) return true;
			if (Pending == null) return false;
			return Pending.Route == KingdomConstructionRoute.Improvement
				&& Pending.SubjectId == ObjectId && Pending.OwnerKey == Completed.OwnerKey
				&& Pending.ZoneId == Completed.ZoneId && Pending.X == Completed.X && Pending.Y == Completed.Y
				&& Pending.Phase != KingdomConstructionPhase.Complete
				&& Pending.Phase != KingdomConstructionPhase.Cancelled && Receipt == Pending.Id;
		}
	}
}
