using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomFounding
	{
		/// <summary>Freezes personal reputation once, before callback-bearing publication.
		/// Existing version-one freezes retain their original observation-only interpretation.</summary>
		private static bool TryReadOrFreezeFoundingStandings(KingdomSystem System, Faction Realm,
			out List<KeyValuePair<string, int>> Targets, out int Version)
		{
			Targets = null; Version = 0;
			if (System == null || Realm == null || string.IsNullOrEmpty(Realm.Name)) return false;
			if (!Realm.HasProperty(FoundingStandingsProperty))
			{
				if (The.Game?.PlayerReputation == null) return false;
				var snapshot = new List<KeyValuePair<string, int>>();
				foreach (Faction faction in Factions.Loop())
				{
					if (faction == null || !System.CanReserveDirectionalRelationship(faction.Name)) continue;
					if (snapshot.Count >= KingdomStandingRules.MaxRelationships) return false;
					snapshot.Add(new KeyValuePair<string, int>(faction.Name,
						The.Game.PlayerReputation.Get(faction)));
				}
				snapshot.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
				if (!KingdomFoundingRegardRules.TryEncode(2, snapshot, out string encoded)) return false;
				Realm.SetProperty(FoundingStandingsProperty, encoded);
				if (Realm.GetStringProperty(FoundingStandingsProperty, null) != encoded) return false;
			}
			return KingdomFoundingRegardRules.TryDecode(
				Realm.GetStringProperty(FoundingStandingsProperty, null), out Version, out Targets);
		}

		private static bool TryResolveFoundingStandings(KingdomSystem System, Faction Realm,
			List<KeyValuePair<string, int>> Targets, out List<KeyValuePair<Faction, int>> Resolved)
		{
			Resolved = new List<KeyValuePair<Faction, int>>();
			if (System == null || Realm == null || Targets == null ||
				Targets.Count > KingdomStandingRules.MaxRelationships) return false;
			foreach (KeyValuePair<string, int> target in Targets)
			{
				Faction other = Factions.GetIfExists(target.Key);
				if (other == null || ReferenceEquals(other, Realm) ||
					!System.CanReserveDirectionalRelationship(target.Key)) return false;
				Resolved.Add(new KeyValuePair<Faction, int>(other, target.Value));
			}
			return true;
		}

		/// <summary>Publishes only an exact frozen snapshot or a matching interrupted subset.
		/// Outgoing policy stays unspecified; later kingdom effects own subsequent differences.</summary>
		private static bool TryPublishFoundingStandings(KingdomSystem System,
			List<KeyValuePair<Faction, int>> Targets, int Version)
		{
			if (System == null || Targets == null ||
				Targets.Count > KingdomStandingRules.MaxRelationships) return false;
			var frozen = new List<KeyValuePair<string, int>>(Targets.Count);
			foreach (KeyValuePair<Faction, int> target in Targets)
			{
				if (target.Key == null || !System.CanReserveDirectionalRelationship(target.Key.Name)) return false;
				frozen.Add(new KeyValuePair<string, int>(target.Key.Name, target.Value));
			}
			if (!KingdomFoundingRegardRules.TryPreparePublication(Version, frozen,
				System.Standings, System.RealmPolicyToward, System.RegardSpilloverRemainders,
				System.RegardSpilloverObservedReputation, out var standings, out var observations)) return false;
			System.Standings = standings;
			System.RegardSpilloverObservedReputation = observations;
			System.DirectionalStandingSchemaVersion = 1;
			return ReferenceEquals(System.Standings, standings) &&
				ReferenceEquals(System.RegardSpilloverObservedReputation, observations);
		}
	}
}
