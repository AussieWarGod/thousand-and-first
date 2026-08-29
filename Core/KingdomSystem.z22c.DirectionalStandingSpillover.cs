using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private bool TryApplyPersonalReputationSpillover(string factionName,
			int reputationBefore, int reputationAfter)
		{
			if (!CanOwnRelationship(factionName) ||
				!TryCopyRegardLedgers(out Dictionary<string, int> standings,
					out Dictionary<string, int> remainders) ||
				RegardSpilloverObservedReputation == null) return false;
			try
			{
				Dictionary<string, int> observed = new Dictionary<string, int>(
					RegardSpilloverObservedReputation, StringComparer.Ordinal);
				bool hadStanding = standings.TryGetValue(factionName, out int standing);
				bool hadRemainder = remainders.TryGetValue(factionName, out int remainder);
				bool hadObserved = observed.ContainsKey(factionName);
				if (!KingdomStandingRules.TrySpillover(standing, remainder,
					reputationBefore, reputationAfter, Stage, out int nextStanding,
					out int nextRemainder)) return false;
				if ((!hadStanding && nextStanding != 0 && standings.Count >=
						KingdomStandingRules.MaxRelationships) ||
					(!hadRemainder && nextRemainder != 0 && remainders.Count >=
						KingdomStandingRules.MaxRelationships)) return false;
				if (hadStanding || nextStanding != 0) standings[factionName] = nextStanding;
				if (nextRemainder == 0) remainders.Remove(factionName);
				else remainders[factionName] = nextRemainder;
				if (hadObserved || observed.Count < KingdomStandingRules.MaxRelationships)
					observed[factionName] = reputationAfter;
				if (!TryPublishRegardState(standings, remainders, observed)) return false;
				if (nextStanding != standing) MirrorRegardForRealm(factionName);
				return true;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("standing spillover refused before publication (" +
					ex.Message + ")");
				return false;
			}
		}

		private bool TryObservePersonalReputationPoststate(string factionName, int poststate)
		{
			if (!CanOwnRelationship(factionName) ||
				RegardSpilloverObservedReputation == null ||
				(!RegardSpilloverObservedReputation.ContainsKey(factionName) &&
				 RegardSpilloverObservedReputation.Count >=
					KingdomStandingRules.MaxRelationships)) return false;
			try
			{
				Dictionary<string, int> observed = new Dictionary<string, int>(
					RegardSpilloverObservedReputation, StringComparer.Ordinal);
				observed[factionName] = poststate;
				RegardSpilloverObservedReputation = observed;
				return ReferenceEquals(RegardSpilloverObservedReputation, observed);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("standing observation refused before publication (" +
					ex.Message + ")");
				return false;
			}
		}

		private bool TryPublishRegardState(Dictionary<string, int> standings,
			Dictionary<string, int> remainders, Dictionary<string, int> observed)
		{
			if (observed == null || observed.Count > KingdomStandingRules.MaxRelationships ||
				!TryPublishRegardLedgers(standings, remainders)) return false;
			RegardSpilloverObservedReputation = observed;
			return ReferenceEquals(RegardSpilloverObservedReputation, observed);
		}
	}
}
