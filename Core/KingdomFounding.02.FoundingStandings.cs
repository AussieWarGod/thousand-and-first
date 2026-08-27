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
		/// Freezes the founder's standings before the first standing mutation. A publication
		/// retry must finish the same ledger even if reputation changed while the prior attempt
		/// was interrupted.
		/// </summary>
		private static bool TryReadOrFreezeFoundingStandings(Faction Realm,
			out List<KeyValuePair<string, int>> Targets)
		{
			Targets = null;
			if (Realm == null || string.IsNullOrEmpty(Realm.Name))
			{
				return false;
			}
			if (!Realm.HasProperty(FoundingStandingsProperty))
			{
				List<KeyValuePair<string, int>> captured =
					new List<KeyValuePair<string, int>>();
				foreach (Faction other in Factions.Loop())
				{
					if (other == null || ReferenceEquals(other, Realm) || other.Name == "Player")
					{
						continue;
					}
					if (string.IsNullOrEmpty(other.Name) || other.Name.Length > 512)
					{
						return false;
					}
					captured.Add(new KeyValuePair<string, int>(other.Name,
						The.Game.PlayerReputation.Get(other)));
				}
				captured.Sort(delegate(KeyValuePair<string, int> Left,
					KeyValuePair<string, int> Right)
				{
					return StringComparer.Ordinal.Compare(Left.Key, Right.Key);
				});
				string encoded = EncodeFoundingStandings(captured);
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

		private static bool TryResolveFoundingStandings(Faction Realm,
			List<KeyValuePair<string, int>> Targets,
			out List<KeyValuePair<Faction, int>> Resolved)
		{
			Resolved = new List<KeyValuePair<Faction, int>>();
			if (Realm == null || Targets == null)
			{
				return false;
			}
			foreach (KeyValuePair<string, int> target in Targets)
			{
				Faction other = Factions.GetIfExists(target.Key);
				if (other == null || ReferenceEquals(other, Realm) || other.Name == "Player")
				{
					return false;
				}
				Resolved.Add(new KeyValuePair<Faction, int>(other, target.Value));
			}
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
