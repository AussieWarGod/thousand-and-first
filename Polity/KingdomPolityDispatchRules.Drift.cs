using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDispatchRules
	{
		/// <summary>Same window, live facts differ from the frozen digest. A complete window owes
		/// nothing and is accepted without a write. Each open intent is re-proved at its frozen slot
		/// (frozen count, digest and cause tick) from the live facts of its own settlement; one that
		/// cannot be re-proved is withdrawn, its slot completed, and the withdrawal reported. Nothing
		/// is minted or replayed: new work appears only when the next window opens.</summary>
		private static bool TryReproveOpenIntents(KingdomPolityDispatchState State,
			long ExpectedRevision, KingdomPolityDispatchOffer Offer, ulong Window,
			List<KingdomPolityDueWork> Work, List<string> Withdrawn, out string Failure)
		{
			Failure = null;
			if (State.CompletedMask == (1 << State.EndpointCount) - 1)
				return ValidState(State, out Failure);
			KingdomPolityDispatchState candidate = CloneState(State);
			List<KingdomPolityDueWork> reproved = new List<KingdomPolityDueWork>();
			List<string> notes = new List<string>();
			for (int i = 0; i < State.EndpointCount; i++)
			{
				if ((State.CompletedMask & 1 << i) != 0) continue;
				KingdomPolityDirectRecord intent = FindIntent(State, i);
				if (intent == null) return Fail("polity dispatch lost an open intent", out Failure);
				KingdomPolityEndpointFacts live = FindEndpoint(Offer.Endpoints, intent.SettlementId);
				string reason = "its settlement left the realm";
				if (live != null)
				{
					if (TryChoose(Offer.RealmId, live, State.EndpointCount, Window, i,
						State.WindowCauseTick, State.EndpointDigest, out KingdomPolityDueWork row)
						&& ExactOpenWork(State, row))
					{
						reproved.Add(row); continue;
					}
					reason = "its source facts changed after the window opened";
				}
				candidate.DirectRecords.Remove(FindIntent(candidate, i));
				candidate.CompletedMask |= 1 << i; notes.Add(DescribeWithdrawal(intent, reason));
			}
			if (notes.Count != 0)
			{
				if (State.Revision == long.MaxValue)
					return Fail("polity dispatch revision is exhausted", out Failure);
				candidate.Revision++; SortRecords(candidate.DirectRecords);
				if (!TryCommitState(State, candidate, ExpectedRevision, out Failure)) return false;
				Withdrawn.AddRange(notes);
			}
			Work.AddRange(reproved);
			return ValidState(State, out Failure);
		}

		internal static string DescribeWithdrawal(KingdomPolityDirectRecord Intent, string Reason)
		{
			return "window " + Intent.WindowOrdinal.ToString(CultureInfo.InvariantCulture) + " "
				+ Intent.Purpose + " intent for " + (Intent.SettlementId ?? "an unknown settlement")
				+ " withdrawn: " + Reason;
		}

		private static KingdomPolityEndpointFacts FindEndpoint(
			IList<KingdomPolityEndpointFacts> Endpoints, string SettlementId)
		{
			for (int i = 0; i < (Endpoints?.Count ?? 0); i++)
				if (Endpoints[i].SettlementId == SettlementId) return Endpoints[i];
			return null;
		}
	}
}
