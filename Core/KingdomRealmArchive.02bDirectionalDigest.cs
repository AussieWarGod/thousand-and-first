using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	public sealed partial class KingdomRealmArchive
	{
		internal bool TryRefreshDirectionalStandingDigest(out string failure)
		{
			if (!TryDirectionalStandingDigest(FactionName, RealmPolicyToward,
				RegardSpilloverRemainders, RegardSpilloverObservedReputation,
				out string digest, out failure)) return false;
			DirectionalStandingDigest = digest;
			return true;
		}

		private bool DirectionalStandingDigestMatches(out string failure)
		{
			if (!TryDirectionalStandingDigest(FactionName, RealmPolicyToward,
				RegardSpilloverRemainders, RegardSpilloverObservedReputation,
				out string digest, out failure)) return false;
			if (!string.Equals(digest, DirectionalStandingDigest,
				StringComparison.Ordinal))
			{
				failure = "directional standing digest differs";
				return false;
			}
			return true;
		}

		internal static bool TryDirectionalStandingDigest(string factionName,
			Dictionary<string, int> policy, Dictionary<string, int> remainders,
			Dictionary<string, int> observed, out string digest, out string failure)
		{
			digest = null;
			failure = null;
			if (!BoundedUtf8(factionName, 512, 2048) || !BoundedStandings(policy) ||
				!BoundedRemainders(remainders) || !BoundedStandings(observed))
			{
				failure = "directional standing digest input is malformed";
				return false;
			}
			try
			{
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, StrictUtf8, true))
				{
					writer.Write(0x54414431); // TAD1
					WriteGraphString(writer, factionName);
					WriteGraphDictionary(writer, policy);
					WriteGraphDictionary(writer, remainders);
					WriteGraphDictionary(writer, observed);
					writer.Flush();
					using (global::System.Security.Cryptography.SHA256 sha =
						global::System.Security.Cryptography.SHA256.Create())
					{
						byte[] hash = sha.ComputeHash(stream.ToArray());
						System.Text.StringBuilder text =
							new System.Text.StringBuilder(64);
						for (int i = 0; i < hash.Length; i++)
							text.Append(hash[i].ToString("x2"));
						digest = text.ToString();
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				failure = Bound(ex.Message, 512);
				return false;
			}
		}
	}
}
