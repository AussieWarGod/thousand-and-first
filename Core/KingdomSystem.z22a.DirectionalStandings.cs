using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Exact immutable roots retained across one synchronous compensated civic effect.
	/// Directional writers are copy-on-write, so restoring these references cannot allocate or
	/// replay arithmetic.</summary>
	internal sealed class KingdomRegardLedgerSnapshot
	{
		internal readonly Dictionary<string, int> Standings;
		internal readonly Dictionary<string, int> Remainders;

		internal KingdomRegardLedgerSnapshot(Dictionary<string, int> standings,
			Dictionary<string, int> remainders)
		{
			Standings = standings;
			Remainders = remainders;
		}
	}

	public partial class KingdomSystem
	{
		public bool TrySetRegardForRealm(string factionName, int value, bool mirror = true)
		{
			if (!CanOwnRelationship(factionName) || !TryCopyRegardLedgers(
				out Dictionary<string, int> nextStandings,
				out Dictionary<string, int> nextRemainders) ||
				(!nextStandings.ContainsKey(factionName) && nextStandings.Count >=
				 KingdomStandingRules.MaxRelationships)) return false;
			// An absolute set has no fractional provenance. Clearing carry is part of the same
			// copy-on-write publication, never a later repair.
			nextStandings[factionName] = value;
			nextRemainders.Remove(factionName);
			if (!TryPublishRegardLedgers(nextStandings, nextRemainders)) return false;
			if (mirror) MirrorRegardForRealm(factionName);
			return true;
		}

		/// <summary>Validates every edge and capacity requirement before publishing an
		/// all-or-none set of faction-to-realm deltas. Duplicate directions are refused.</summary>
		public bool TryAdjustRegardForRealmBatch(
			IList<KeyValuePair<string, int>> deltas, bool mirror = true)
		{
			if (deltas == null || deltas.Count >
				KingdomStandingRules.MaxRelationships) return false;
			try
			{
				if (!TryCopyRegardLedgers(out Dictionary<string, int> nextStandings,
					out Dictionary<string, int> nextRemainders)) return false;
				HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
				List<string> targets = new List<string>(deltas.Count);
				for (int i = 0; i < deltas.Count; i++)
				{
					string faction = deltas[i].Key;
					if (!seen.Add(faction) || !CanOwnRelationship(faction)) return false;
					if (deltas[i].Value == 0) continue;
					bool hadStanding = nextStandings.TryGetValue(faction, out int before);
					nextRemainders.TryGetValue(faction, out int carry);
					if (!KingdomStandingRules.TryAdjustPair(before, carry, deltas[i].Value,
						out int after, out int afterCarry)) return false;
					if (!hadStanding && after != 0 && nextStandings.Count >=
						KingdomStandingRules.MaxRelationships) return false;
					if (hadStanding || after != 0) nextStandings[faction] = after;
					if (afterCarry == 0) nextRemainders.Remove(faction);
					else if (!nextRemainders.ContainsKey(faction) && nextRemainders.Count >=
						KingdomStandingRules.MaxRelationships) return false;
					else nextRemainders[faction] = afterCarry;
					targets.Add(faction);
				}
				if (targets.Count == 0) return true;
				if (!TryPublishRegardLedgers(nextStandings, nextRemainders)) return false;
				if (mirror)
					for (int i = 0; i < targets.Count; i++)
						MirrorRegardForRealm(targets[i]);
				return true;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("directional standing batch refused before publication (" +
					ex.Message + ")");
				return false;
			}
		}

		internal bool TryGetRegardPair(string factionName, out int standing,
			out int remainder)
		{
			standing = 0; remainder = 0;
			if (Standings == null || RegardSpilloverRemainders == null || factionName == null)
				return false;
			Standings.TryGetValue(factionName, out standing);
			RegardSpilloverRemainders.TryGetValue(factionName, out remainder);
			return KingdomStandingRules.CanonicalPair(standing, remainder);
		}

		internal bool TryCaptureRegardLedger(out KingdomRegardLedgerSnapshot snapshot)
		{
			snapshot = null;
			if (!KingdomStandingRules.CanonicalPairs(Standings,
				RegardSpilloverRemainders)) return false;
			snapshot = new KingdomRegardLedgerSnapshot(Standings,
				RegardSpilloverRemainders);
			return true;
		}

		internal bool TryRestoreRegardLedger(KingdomRegardLedgerSnapshot snapshot)
		{
			if (snapshot == null || !KingdomStandingRules.CanonicalPairs(snapshot.Standings,
				snapshot.Remainders)) return false;
			Standings = snapshot.Standings;
			RegardSpilloverRemainders = snapshot.Remainders;
			return ReferenceEquals(Standings, snapshot.Standings) &&
				ReferenceEquals(RegardSpilloverRemainders, snapshot.Remainders);
		}

		private bool TryCopyRegardLedgers(out Dictionary<string, int> standings,
			out Dictionary<string, int> remainders)
		{
			standings = null; remainders = null;
			if (!KingdomStandingRules.CanonicalPairs(Standings,
				RegardSpilloverRemainders)) return false;
			try
			{
				standings = new Dictionary<string, int>(Standings, StringComparer.Ordinal);
				remainders = new Dictionary<string, int>(RegardSpilloverRemainders,
					StringComparer.Ordinal);
				return true;
			}
			catch (Exception ex)
			{
				KingdomLog.Log("directional standing copy refused (" + ex.Message + ")");
				standings = null; remainders = null; return false;
			}
		}

		private bool TryPublishRegardLedgers(Dictionary<string, int> standings,
			Dictionary<string, int> remainders)
		{
			if (standings == null || remainders == null ||
				standings.Count > KingdomStandingRules.MaxRelationships ||
				remainders.Count > KingdomStandingRules.MaxRelationships ||
				!KingdomStandingRules.CanonicalPairs(standings, remainders)) return false;
			// All allocation and arithmetic completed above. These single-threaded root swaps cannot
			// invoke engine callbacks, so an exception cannot expose a partly edited dictionary.
			Standings = standings;
			RegardSpilloverRemainders = remainders;
			return ReferenceEquals(Standings, standings) &&
				ReferenceEquals(RegardSpilloverRemainders, remainders);
		}

		public bool TryGetRealmPolicyToward(string factionName, out int value)
		{
			value = 0;
			return RealmPolicyToward != null && factionName != null &&
				RealmPolicyToward.TryGetValue(factionName, out value);
		}

		public int GetRealmPolicyToward(string factionName)
		{
			return TryGetRealmPolicyToward(factionName, out int value) ? value : 0;
		}

		public bool TrySetRealmPolicyToward(string factionName, int value, bool mirror = true)
		{
			if (!CanOwnRelationship(factionName) || RealmPolicyToward == null ||
				(!RealmPolicyToward.ContainsKey(factionName) &&
				 RealmPolicyToward.Count >= KingdomStandingRules.MaxRelationships)) return false;
			RealmPolicyToward[factionName] = value;
			if (mirror) MirrorRealmPolicyToward(factionName);
			return true;
		}

		public void AdjustRealmPolicyToward(string factionName, int delta, bool mirror = true)
		{
			if (delta == 0 || !CanOwnRelationship(factionName)) return;
			TrySetRealmPolicyToward(factionName, KingdomStandingRules.SaturatingAdd(
				GetRealmPolicyToward(factionName), delta), mirror);
		}

		/// <summary>Projects only realm-to-foreign-faction policy. An absent policy writes
		/// nothing, preserving the engine or another owner's prior edge.</summary>
		public void MirrorRealmPolicyToward(string factionName)
		{
			if (!Founded || !CanOwnRelationship(factionName) ||
				!TryGetRealmPolicyToward(factionName, out int policy)) return;
			Guard("realm policy projection " + factionName, delegate
			{
				Faction realm = Factions.GetIfExists(KingdomFactionName);
				Faction foreign = Factions.GetIfExists(factionName);
				if (realm != null && foreign != null)
					realm.SetFactionFeeling(factionName,
						Reputation.GetFeeling((float)policy));
			});
		}

		private bool CanOwnRelationship(string factionName)
		{
			Faction realm = Factions.GetIfExists(KingdomFactionName);
			return CurrentRelationshipAuthorityHealthy() && realm != null &&
				realm.Name == KingdomFactionName &&
				realm.GetIntProperty("PlayerKingdom") == 1 &&
				KingdomFounding.DirectionalAuthorityPublished(realm) &&
				RelationshipFactionAvailable(factionName, KingdomFactionName);
		}

		internal bool CanReserveDirectionalRelationship(string factionName)
		{
			Faction realm = Factions.GetIfExists(KingdomFactionName);
			return CurrentRelationshipAuthorityHealthy() && realm != null &&
				realm.Name == KingdomFactionName &&
				realm.GetIntProperty("PlayerKingdom") == 1 &&
				RelationshipFactionAvailable(factionName, KingdomFactionName);
		}

		private bool RelationshipFactionAvailable(string factionName,
			string realmFactionName)
		{
			if (!KingdomStandingRules.EligibleForeignFaction(
				factionName, realmFactionName) ||
				Factions.GetIfExists(factionName) == null) return false;
			// Every TAF polity owns its own endpoint. Cross-polity diplomacy is recorded by the
			// polity ledger, never smuggled into either half of this realm/foreign-faction pair.
			if ((!string.IsNullOrEmpty(KingdomFactionName) &&
				 factionName == KingdomFactionName) ||
				(!string.IsNullOrEmpty(ExiledFactionName) &&
				 factionName == ExiledFactionName)) return false;
			if (!KingdomPolityRules.Usable(PolityLedger) ||
				(PolityLedger.IdentityBound && PolityLedger.RealmId != RealmId) ||
				PolityLedger.Polities == null) return false;
			for (int i = 0; i < PolityLedger.Polities.Count; i++)
			{
				KingdomPolityRecord polity = PolityLedger.Polities[i];
				if (polity != null && polity.ProjectedFactionId == factionName &&
					polity.ProjectedFactionId != realmFactionName) return false;
			}
			return true;
		}

		private bool CurrentRelationshipAuthorityHealthy()
		{
			if (!Founded || !string.IsNullOrEmpty(IdentityFault)) return false;
			return KingdomIdentityRules.ReproveRealm(RealmId, RealmIdentityVersion,
				RealmIdentityOrigin, RealmIdentityTransactionId, RealmIdentityLegacyFaction,
				RealmIdentityFoundedTick, RealmIdentitySeedHigh, RealmIdentitySeedLow,
				RealmIdentityFirstClaimedZone, out KingdomIdentityFault _);
		}
	}
}
