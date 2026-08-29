using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using XRL;
using XRL.Language;
using XRL.Rules;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	public static partial class KingdomFounding
	{
		/// <summary>
		/// Freezes an empty, bounded retry marker before directional authority is published. A new
		/// civic entity begins with both foreign directions unspecified; no personal regard is copied.
		/// </summary>
		private static bool TryReadOrFreezeFoundingStandings(KingdomSystem System, Faction Realm,
			out List<KeyValuePair<string, int>> Targets)
		{
			Targets = null;
			if (System == null || Realm == null || string.IsNullOrEmpty(Realm.Name))
			{
				return false;
			}
			if (!Realm.HasProperty(FoundingStandingsProperty))
			{
				// New civic entities inherit no personal relationship edge. Retain the bounded
				// empty freeze marker only so a cut through founding remains retryable by the same
				// transaction protocol used by grandfathered builds.
				string encoded = EncodeFoundingStandings(
					new List<KeyValuePair<string, int>>());
				if (encoded == null)
				{
					return false;
				}
				Realm.SetProperty(FoundingStandingsProperty, encoded);
				if (Realm.GetStringProperty(FoundingStandingsProperty, null) != encoded)
				{
					return false;
				}
			}
			return TryDecodeFoundingStandings(
				Realm.GetStringProperty(FoundingStandingsProperty, null), out Targets);
		}

		private static bool TryResolveFoundingStandings(KingdomSystem System, Faction Realm,
			List<KeyValuePair<string, int>> Targets,
			out List<KeyValuePair<Faction, int>> Resolved)
		{
			Resolved = new List<KeyValuePair<Faction, int>>();
			if (System == null || Realm == null || Targets == null ||
				Targets.Count > KingdomStandingRules.MaxRelationships)
			{
				return false;
			}
			foreach (KeyValuePair<string, int> target in Targets)
			{
				Faction other = Factions.GetIfExists(target.Key);
				if (other == null || ReferenceEquals(other, Realm) || other.Name == "Player" ||
					!System.CanReserveDirectionalRelationship(target.Key))
				{
					return false;
				}
				Resolved.Add(new KeyValuePair<Faction, int>(other, target.Value));
			}
			return true;
		}

		/// <summary>Publishes the frozen empty relationship set without invoking engine callbacks.
		/// A cut may leave an exact subset; retry accepts only that subset and completes it. Both civic
		/// directions and the advisory observation cache remain empty and Unspecified.</summary>
		private static bool TryPublishFoundingStandings(KingdomSystem System,
			List<KeyValuePair<Faction, int>> Targets)
		{
			if (System == null || Targets == null ||
				Targets.Count > KingdomStandingRules.MaxRelationships ||
				System.Standings == null || System.RealmPolicyToward == null ||
				System.RegardSpilloverRemainders == null ||
				System.RegardSpilloverObservedReputation == null ||
				System.RegardSpilloverRemainders.Count != 0 ||
				System.Standings.Count != 0 ||
				System.RealmPolicyToward.Count != 0 ||
				System.RegardSpilloverObservedReputation.Count > Targets.Count) return false;

			Dictionary<string, int> desired = new Dictionary<string, int>(
				StringComparer.Ordinal);
			for (int i = 0; i < Targets.Count; i++)
			{
				Faction faction = Targets[i].Key;
				if (faction == null || !System.CanReserveDirectionalRelationship(faction.Name) ||
					desired.ContainsKey(faction.Name)) return false;
				desired.Add(faction.Name, Targets[i].Value);
			}
			if (!ExactSubset(System.RegardSpilloverObservedReputation, desired)) return false;
			foreach (KeyValuePair<string, int> row in desired)
				System.RegardSpilloverObservedReputation[row.Key] = row.Value;
			System.DirectionalStandingSchemaVersion = 1;
			return System.DirectionalStandingSchemaVersion == 1 &&
				System.Standings.Count == 0 &&
				System.RealmPolicyToward.Count == 0 &&
				System.RegardSpilloverObservedReputation.Count == desired.Count &&
				ExactSubset(System.RegardSpilloverObservedReputation, desired);
		}

		private static bool ExactSubset(Dictionary<string, int> Actual,
			Dictionary<string, int> Desired)
		{
			if (Actual == null || Desired == null) return false;
			foreach (KeyValuePair<string, int> row in Actual)
				if (!Desired.TryGetValue(row.Key, out int value) || value != row.Value)
					return false;
			return true;
		}

		private static string EncodeFoundingStandings(
			List<KeyValuePair<string, int>> Targets)
		{
			StringBuilder encoded = new StringBuilder("v1");
			string previous = null;
			for (int i = 0; i < Targets.Count; i++)
			{
				string name = Targets[i].Key;
				if (string.IsNullOrEmpty(name) || name.Length > 512 ||
					(previous != null && StringComparer.Ordinal.Compare(previous, name) >= 0))
				{
					return null;
				}
				previous = name;
				encoded.Append(';').Append(Convert.ToBase64String(
					Encoding.UTF8.GetBytes(name))).Append(':').Append(
					Targets[i].Value.ToString(CultureInfo.InvariantCulture));
				if (encoded.Length > MaxFoundingStandingsLength)
				{
					return null;
				}
			}
			return encoded.ToString();
		}

		private static bool TryDecodeFoundingStandings(string Encoded,
			out List<KeyValuePair<string, int>> Targets)
		{
			Targets = null;
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxFoundingStandingsLength)
			{
				return false;
			}
			string[] rows = Encoded.Split(';');
			if (rows.Length == 0 || rows[0] != "v1")
			{
				return false;
			}
			List<KeyValuePair<string, int>> decoded =
				new List<KeyValuePair<string, int>>(rows.Length - 1);
			string previous = null;
			try
			{
				UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
				for (int i = 1; i < rows.Length; i++)
				{
					int separator = rows[i].IndexOf(':');
					if (separator <= 0 || separator != rows[i].LastIndexOf(':'))
					{
						return false;
					}
					string nameText = rows[i].Substring(0, separator);
					byte[] nameBytes = Convert.FromBase64String(nameText);
					if (Convert.ToBase64String(nameBytes) != nameText)
					{
						return false;
					}
					string name = strictUtf8.GetString(nameBytes);
					if (string.IsNullOrEmpty(name) || name.Length > 512 ||
						(previous != null && StringComparer.Ordinal.Compare(previous, name) >= 0) ||
						!int.TryParse(rows[i].Substring(separator + 1),
							NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
							out int standing))
					{
						return false;
					}
					previous = name;
					decoded.Add(new KeyValuePair<string, int>(name, standing));
				}
			}
			catch (FormatException)
			{
				return false;
			}
			catch (DecoderFallbackException)
			{
				return false;
			}
			if (EncodeFoundingStandings(decoded) != Encoded)
			{
				return false;
			}
			Targets = decoded;
			return true;
		}

	}
}
