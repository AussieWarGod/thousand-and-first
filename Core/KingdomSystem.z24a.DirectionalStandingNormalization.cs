using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public partial class KingdomSystem
	{
		private void NormalizeDirectionalStandingState()
		{
			string failure = DirectionalStandingSchemaVersion < 0 ||
				DirectionalStandingSchemaVersion > 1
				? "directional relationship schema is invalid" : null;
			if (failure == null && Founded && DirectionalStandingSchemaVersion != 1 &&
				LoadedSerializationVersion != 8)
				failure = "a published realm lacks directional relationship authority";
			if (failure == null && !Founded && DirectionalStandingSchemaVersion != 0)
				failure = "an unfounded realm retains directional relationship schema";
			if (failure == null) failure = ValidateDirectionalStandingState(Standings,
				RealmPolicyToward, RegardSpilloverRemainders,
				RegardSpilloverObservedReputation, KingdomFactionName);
			if (failure == null && Exiled)
				failure = ValidateDirectionalStandingState(ExiledStandings,
					ExiledRealmPolicyToward, ExiledRegardSpilloverRemainders,
					ExiledRegardSpilloverObservedReputation, ExiledFactionName);
			if (failure == null && !Founded &&
				(Standings.Count != 0 || RealmPolicyToward.Count != 0 ||
				 RegardSpilloverRemainders.Count != 0 ||
				 RegardSpilloverObservedReputation.Count != 0))
				failure = "an unfounded realm retains directional relationship authority";
			if (failure != null) QuarantineIdentity(failure);
		}

		private static string ValidateDirectionalStandingState(
			Dictionary<string, int> regard, Dictionary<string, int> policy,
			Dictionary<string, int> remainders, Dictionary<string, int> observed,
			string realmFaction)
		{
			if (regard == null || policy == null || remainders == null || observed == null)
				return "a directional relationship ledger is absent";
			if (regard.Count > KingdomStandingRules.MaxRelationships ||
				policy.Count > KingdomStandingRules.MaxRelationships ||
				remainders.Count > KingdomStandingRules.MaxRelationships ||
				observed.Count > KingdomStandingRules.MaxRelationships)
				return "a directional relationship ledger exceeds its bound";
			if (!KingdomStandingRules.CanonicalPairs(regard, remainders))
				return "standing and spillover carry are not one canonical scaled ledger";
			foreach (KeyValuePair<string, int> row in regard)
				if (!KingdomStandingRules.EligibleForeignFaction(row.Key, realmFaction))
					return "faction-to-realm regard contains a reserved direction";
			foreach (KeyValuePair<string, int> row in policy)
				if (!KingdomStandingRules.EligibleForeignFaction(row.Key, realmFaction))
					return "realm-to-faction policy contains a reserved direction";
			foreach (KeyValuePair<string, int> row in remainders)
				if (!KingdomStandingRules.EligibleForeignFaction(row.Key, realmFaction) ||
					!KingdomStandingRules.ValidRemainder(row.Value))
					return "standing spillover contains a reserved key or invalid remainder";
			foreach (KeyValuePair<string, int> row in observed)
				if (!KingdomStandingRules.EligibleForeignFaction(row.Key, realmFaction))
					return "standing spillover observation contains a reserved direction";
			return null;
		}

		/// <summary>Runs only after Qud has loaded its faction registry. The positional reader
		/// cannot safely perform this check because faction objects are loaded later.</summary>
		private void ValidateDirectionalFactionRegistryAfterLoad()
		{
			string failure = ValidateDirectionalFactionRegistry(Standings,
				RealmPolicyToward, RegardSpilloverRemainders,
				RegardSpilloverObservedReputation, KingdomFactionName);
			if (failure == null && Exiled)
				failure = ValidateDirectionalFactionRegistry(ExiledStandings,
					ExiledRealmPolicyToward, ExiledRegardSpilloverRemainders,
					ExiledRegardSpilloverObservedReputation, ExiledFactionName);
			if (failure != null) QuarantineIdentity(failure);
		}

		private string ValidateDirectionalFactionRegistry(
			Dictionary<string, int> regard, Dictionary<string, int> policy,
			Dictionary<string, int> remainders, Dictionary<string, int> observed,
			string realmFaction)
		{
			if (regard == null || policy == null || remainders == null || observed == null)
				return "a directional relationship ledger is absent after faction load";
			foreach (string key in regard.Keys)
				if (!RelationshipFactionAvailable(key, realmFaction))
					return "faction-to-realm regard names a missing or reserved faction";
			foreach (string key in policy.Keys)
				if (!RelationshipFactionAvailable(key, realmFaction))
					return "realm-to-faction policy names a missing or reserved faction";
			foreach (string key in remainders.Keys)
				if (!RelationshipFactionAvailable(key, realmFaction))
					return "standing spillover names a missing or reserved faction";
			foreach (string key in observed.Keys)
				if (!RelationshipFactionAvailable(key, realmFaction))
					return "standing spillover observation names a missing or reserved faction";
			return null;
		}
	}
}
