using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomSuccessionRules
	{
		// ==================================================================================
		// The ledger scrub (Addendum 22 C4/C5, QB-2/QB-3/QB-4)
		// ==================================================================================

		/// <summary>
		/// Whether the honesty rule forgets one journal entry.
		/// <para>
		/// Three rulings meet in this one table, and it is worth naming which is which. C5 says the
		/// founder's journal dies with the founder. <b>QB-3</b> exempts accomplishments: they are the
		/// realm's own record, they feed vanilla's mural machinery unfiltered
		/// (<c>D/XRL/World/Parts/PlayerMuralController.cs:232-233</c> reads the list without checking
		/// <c>Revealed</c>), and forgetting them would rewrite the founder's history out of the walls
		/// rather than out of anyone's memory. <b>QB-2</b> keeps the chart: vanilla marks map notes
		/// unforgettable outright (<c>D/Qud/API/JournalMapNote.cs:305-308</c> returns false
		/// unconditionally), so forcing it would mean field surgery against the engine's own intent
		/// for the one inheritance players actually treasure.
		/// </para>
		/// <para>
		/// Everything else is forgotten if and only if the engine agrees it is forgettable, which is
		/// how the sultan notes that function as map knowledge keep themselves
		/// (<c>D/Qud/API/JournalSultanNote.cs:91-111</c>).
		/// </para>
		/// </summary>
		/// <param name="Kind">Which list the entry lives in.</param>
		/// <param name="EngineForgettable">What the entry's own <c>Forgettable()</c> answered.</param>
		public static bool Forgets(JournalKind Kind, bool EngineForgettable)
		{
			if (Kind == JournalKind.Accomplishment || Kind == JournalKind.MapNote)
			{
				return false;
			}
			return EngineForgettable;
		}

		/// <summary>Namespaced prefix for the per-entry attribute that records which founder knew a
		/// thing, so the corpse-read can give back exactly what that founder lost and nothing else.
		/// <c>Attributes</c> is already used semantically by vanilla's own amnesia
		/// (<c>D/XRL/World/Parts/Mutation/Amnesia.cs:61-75</c>), so this rides a shipped surface.</summary>
		public const string FounderAttributePrefix = "taf:founder:";

		/// <summary>Quest metadata belongs to Qud's game-scoped ledger. Succession never moves,
		/// fails, completes, hides, or rewrites that state; these keys support only the v1.5
		/// flavor layer settled in QUEST-HANDLING-RESEARCH.md.</summary>
		public const string InheritedQuestMarker = "taf:succession:inherited:v1";
		public const string InheritedQuestSuffix = " (inherited)";
		public const string QuestOriginAttribute = "taf:succession:quest-origin:v1";
		public const int MaxQuestIdentityChars = 1024;
		public const int MaxQuestTellingLabelChars = 512;

		private static readonly HashSet<string> PersonalQuestIds = new HashSet<string>(
			StringComparer.Ordinal)
		{
			"Fetch Argyve a Knickknack",
			"Fetch Argyve Another Knickknack",
			"Weirdwire Conduit... Eureka!",
			"A Canticle for Barathrum",
			"A Signal in the Noise",
			"O Glorious Shekhinah!",
			"The Assessment",
			"Pax Klanq, I Presume?",
			"Petals on the Wind",
			"Find Eskhind",
			"Love and Fear",
			"Kith and Kin",
			"If, Then, Else"
		};

		/// <summary>Whether an open vanilla quest is one of the documented person-tinted class-B
		/// undertakings. ID is authoritative; name is only the compatibility fallback for a
		/// synthetic or older quest record with no ID.</summary>
		public static bool PersonalQuest(string QuestId, string QuestName)
		{
			string identity = string.IsNullOrEmpty(QuestId) ? WithoutInheritedSuffix(QuestName)
				: QuestId;
			return !string.IsNullOrEmpty(identity) && PersonalQuestIds.Contains(identity);
		}

		/// <summary>Live-name precedent from Reclamation, applied idempotently. This is display
		/// flavor only; the quest ID, steps, system, giver, completion, and rewards stay untouched.</summary>
		public static string InheritedQuestName(string QuestName)
		{
			if (string.IsNullOrEmpty(QuestName) || QuestName.EndsWith(InheritedQuestSuffix,
				StringComparison.Ordinal)) return QuestName;
			return QuestName + InheritedQuestSuffix;
		}

		public static string WithoutInheritedSuffix(string QuestName)
		{
			if (string.IsNullOrEmpty(QuestName) || !QuestName.EndsWith(InheritedQuestSuffix,
				StringComparison.Ordinal)) return QuestName;
			return QuestName.Substring(0, QuestName.Length - InheritedQuestSuffix.Length);
		}

		/// <summary>One exact open-quest telling for the succession chronicle.</summary>
		public static string InheritedQuestChronicle(string FounderName, string QuestName)
		{
			string founder = BoundQuestLabel(FounderName, "the founder");
			string quest = BoundQuestLabel(WithoutInheritedSuffix(QuestName), "an undertaking");
			return founder + " died with " + quest + " undone, and the heir inherited the undertaking";
		}

		/// <summary>Permanent Chronicle identity for one death and one open quest.</summary>
		public static string InheritedQuestEventId(string DeathToken, string QuestId)
		{
			string hash = SuccessionQuestHash("unfinished", DeathToken, QuestId);
			return hash == null ? null : "taf:succession:unfinished:v1:" + hash;
		}

		/// <summary>Permanent Chronicle identity for the accession rite owned by one exact founder
		/// death. The bounded digest lets the retry receipt survive a cut after either Chronicle
		/// list sink without carrying an arbitrary object-id token into the receipt key.</summary>
		public static string AccessionRiteEventId(string DeathToken)
		{
			string hash = SuccessionQuestHash("accession-rite", DeathToken, "rite");
			return hash == null ? null : "taf:succession:accession-rite:v1:" + hash;
		}

		/// <summary>Journal secret identity for one founder-death/quest-giver origin. A bounded
		/// cryptographic digest prevents arbitrary third-party quest IDs from bloating the save and
		/// prevents field-boundary aliases.</summary>
		public static string QuestOriginSecretId(string DeathToken, string QuestId)
		{
			string hash = SuccessionQuestHash("quest-origin", DeathToken, QuestId);
			return hash == null ? null : "taf:succession:quest-origin:v1:" + hash;
		}

		private static string BoundQuestLabel(string Value, string Fallback)
		{
			string value = string.IsNullOrWhiteSpace(Value) ? Fallback : Value.Trim();
			if (value.Length <= MaxQuestTellingLabelChars) return value;
			return value.Substring(0, MaxQuestTellingLabelChars - 1) + "…";
		}

		private static string SuccessionQuestHash(string Domain, string DeathToken, string QuestId)
		{
			if (string.IsNullOrEmpty(Domain) || string.IsNullOrEmpty(DeathToken)
				|| DeathToken.Length > MaxDeathTokenChars || string.IsNullOrEmpty(QuestId)
				|| QuestId.Length > MaxQuestIdentityChars) return null;
			try
			{
				byte[] bytes;
				using (MemoryStream stream = new MemoryStream())
				using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{
					writer.Write("TAF-SUCCESSION-QUEST-V1");
					writer.Write(Domain);
					writer.Write(DeathToken);
					writer.Write(QuestId);
					writer.Flush();
					bytes = stream.ToArray();
				}
				using (SHA256 sha = SHA256.Create())
				{
					if (sha == null) return null;
					byte[] digest = sha.ComputeHash(bytes);
					StringBuilder text = new StringBuilder(digest.Length * 2);
					for (int i = 0; i < digest.Length; i++)
						text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
					return text.ToString();
				}
			}
			catch
			{
				return null;
			}
		}
	}
}
