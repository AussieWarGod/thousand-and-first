using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// What a staged seal is allowed to become, from the two facts the engine can prove about the
	/// run it came from. <c>INHERITANCE-SEAMS.md:66-76</c>.
	/// </summary>
	internal enum KingdomSealEligibility
	{
		/// <summary>No score and a save still standing: the run is being played, or was put down.
		/// Nothing crosses.</summary>
		Living = 0,
		/// <summary>A score and a save both: a checkpoint death, permadeath switched off, or a
		/// cleanup that did not finish. Not proof of an end, so nothing crosses automatically.</summary>
		Checkpointed = 1,
		/// <summary>A score and no save: the engine itself ended and cleared that run. This is the
		/// only automatic crossing.</summary>
		Ended = 2,
		/// <summary>Neither. A save deleted by hand, a cleared scoreboard, a stage left by a
		/// vanished game. Never automatic; recoverable only by asking.</summary>
		Orphaned = 3
	}

	/// <summary>What the next world does about a sealed realm, before anyone is asked anything.</summary>
	internal enum KingdomImportPolicy
	{
		/// <summary>Nothing crosses. The next world is clean, and is never asked about it.</summary>
		Off = 0,
		/// <summary>The most recent eligible seal is offered, once. Addendum 22 C10's default.</summary>
		LatestEligible = 1
	}

	/// <summary>
	/// The realm-scope facts a seal needs that a settlement does not hold: who the lineage is,
	/// which game it came from, and how deep the line runs.
	/// <para>
	/// Separate from <see cref="KingdomSealRecord"/> because these are the fields the interregnum
	/// draw is seeded from, and the draw must be seeded from <b>immutable legacy data only</b>
	/// &mdash; never the target world's seed, the calendar, or anything the player can turn over
	/// again (<c>DECISIONS.md:174-186</c>). Keeping them in one named place is what makes that
	/// reviewable.
	/// </para>
	/// </summary>
	internal sealed class KingdomSealLineage
	{
		public string LineageId = "";

		public string LegacyId = "";

		public string OriginGameId = "";

		public int Generation;

		public int Revision;

		public KingdomSealLineage()
		{
		}

		public KingdomSealLineage(string LineageId, string LegacyId, string OriginGameId, int Generation, int Revision)
		{
			this.LineageId = LineageId ?? "";
			this.LegacyId = LegacyId ?? "";
			this.OriginGameId = OriginGameId ?? "";
			this.Generation = Generation;
			this.Revision = Revision;
		}
	}

	/// <summary>Whole immutable realm/city authority captured beside a seal. Display names and
	/// seat role are absent: the exact full topology and both provenance chains are the proof.</summary>
	internal sealed class KingdomSealIdentity
	{
		public string RealmId;
		public string SettlementId;
		public List<string> SettlementIds = new List<string>();
		/// <summary>One canonical row per sorted SettlementIds entry. Each row binds the id to
		/// its complete immutable city provenance; a topology list without these rows is inert.</summary>
		public List<string> SettlementProvenanceRows = new List<string>();
		public int RealmIdentityVersion;
		public KingdomIdentityOrigin RealmIdentityOrigin;
		public string RealmIdentityTransactionId = "";
		public string RealmIdentityLegacyFaction = "";
		public long RealmIdentityFoundedTick;
		public ulong RealmIdentitySeedHigh = 0UL;
		public ulong RealmIdentitySeedLow = 0UL;
		public string RealmIdentityFirstClaimedZone = "";
		public int SettlementIdentityVersion;
		public KingdomIdentityOrigin SettlementIdentityOrigin;
		public string SettlementIdentityTransactionId = "";
		public long SettlementIdentityFoundedTick;
		public string SettlementIdentityFirstClaimedZone = "";
		public string SettlementIdentityLegacyId = "";
	}

	/// <summary>
	/// The rules that turn a living settlement into a sealed record, judge whether one may cross,
	/// and draw the one fortune between lives. Engine-free by design: everything here is testable
	/// without a game, which is the only way the exploit class this guards against
	/// (<c>DECISIONS.md:167-172</c>) stays caught.
	/// </summary>
	internal static class KingdomSealRules
	{
		/// <summary>The alphabet an identifier may use. Deliberately no slash, no backslash, no
		/// dollar, no brace, no space: an id from a file must never be able to become a path
		/// fragment, a format template, or a markup tag.</summary>
		public const string TokenAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.:";

		/// <summary>What a settler's name may not be longer than once sanitized.</summary>
		public const int MaxNameChars = KingdomSealRecord.MaxNameChars;

		/// <summary>
		/// True when a string is one this build will accept as an identifier: the token alphabet,
		/// nothing else, and never empty at a site that requires one.
		/// </summary>
		public static bool IsToken(string Value)
		{
			if (string.IsNullOrEmpty(Value))
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				if (TokenAlphabet.IndexOf(Value[i]) < 0)
				{
					return false;
				}
			}
			return true;
		}

		public static bool ExactIdentity(KingdomSealIdentity Identity,
			KingdomSettlement Seat)
		{
			if (Identity == null || Seat?.City == null || Identity.SettlementIds == null ||
				!string.Equals(Seat.City.SettlementId, Identity.SettlementId,
					StringComparison.Ordinal)) return false;
			KingdomIdentityFault fault;
			return KingdomIdentityRules.ReproveRealm(Identity.RealmId,
				Identity.RealmIdentityVersion, Identity.RealmIdentityOrigin,
				Identity.RealmIdentityTransactionId, Identity.RealmIdentityLegacyFaction,
				Identity.RealmIdentityFoundedTick, Identity.RealmIdentitySeedHigh,
				Identity.RealmIdentitySeedLow, Identity.RealmIdentityFirstClaimedZone,
				out fault) && KingdomIdentityRules.ValidateRealmTopology(Identity.RealmId,
					Identity.SettlementIds, out fault) &&
				Identity.SettlementIds.Contains(Identity.SettlementId) &&
				ExactTopologyProvenance(Identity.RealmId, Identity.SettlementIds,
					Identity.SettlementProvenanceRows, Identity.SettlementId,
					Identity.SettlementIdentityVersion, Identity.SettlementIdentityOrigin,
					Identity.SettlementIdentityTransactionId,
					Identity.SettlementIdentityFoundedTick,
					Identity.SettlementIdentityFirstClaimedZone,
					Identity.SettlementIdentityLegacyId) &&
				KingdomIdentityRules.ReproveSettlement(Identity.SettlementId,
					Identity.RealmId, Identity.SettlementIdentityVersion,
					Identity.SettlementIdentityOrigin,
					Identity.SettlementIdentityTransactionId,
					Identity.SettlementIdentityFoundedTick,
					Identity.SettlementIdentityFirstClaimedZone, out fault);
		}

		internal static bool TryBuildSettlementProvenance(string SettlementId, int Version,
			KingdomIdentityOrigin Origin, string TransactionId, long FoundedTick,
			string FirstClaimedZone, string LegacyId, out string Row)
		{
			Row = null;
			if (!KingdomIdentityRules.IsSettlementId(SettlementId) || Version < 0 || Version > 32 ||
				Origin < KingdomIdentityOrigin.None || Origin > KingdomIdentityOrigin.LegacyMigration ||
				FoundedTick < 0L) return false;
			if (!TryHex(TransactionId ?? "", 1024, out string transaction) ||
				!TryHex(FirstClaimedZone ?? "", 1024, out string zone) ||
				!TryHex(LegacyId ?? "", 1024, out string legacy)) return false;
			Row = SettlementId + "." + Version.ToString(CultureInfo.InvariantCulture) + "." +
				((int)Origin).ToString(CultureInfo.InvariantCulture) + "." +
				FoundedTick.ToString(CultureInfo.InvariantCulture) +
				"." + transaction + "." + zone + "." + legacy;
			return Row.Length <= 4300 && IsToken(Row);
		}

		internal static bool ExactTopologyProvenance(string RealmId, IList<string> SettlementIds,
			IList<string> Rows, string SeatedId = null, int SeatedVersion = 0,
			KingdomIdentityOrigin SeatedOrigin = KingdomIdentityOrigin.None,
			string SeatedTransaction = null, long SeatedFounded = 0L,
			string SeatedZone = null, string SeatedLegacy = null)
		{
			KingdomIdentityFault topologyFault;
			if (!KingdomIdentityRules.ValidateRealmTopology(RealmId, SettlementIds,
				out topologyFault) || SettlementIds == null || Rows == null ||
				SettlementIds.Count != Rows.Count) return false;
			for (int i = 0; i < SettlementIds.Count; i++)
			{
				if (i > 0 && string.CompareOrdinal(SettlementIds[i - 1], SettlementIds[i]) >= 0)
					return false;
				if (!TryParseSettlementProvenance(Rows[i], out string id, out int version,
					out KingdomIdentityOrigin origin, out string transaction, out long founded,
					out string zone, out string legacy) || id != SettlementIds[i] ||
					!KingdomIdentityRules.ReproveSettlement(id, RealmId, version, origin,
						transaction, founded, zone, out topologyFault)) return false;
				if (id == SeatedId && (version != SeatedVersion || origin != SeatedOrigin ||
					transaction != (SeatedTransaction ?? "") || founded != SeatedFounded ||
					zone != (SeatedZone ?? "") || legacy != (SeatedLegacy ?? ""))) return false;
			}
			return SeatedId == null || SettlementIds.Contains(SeatedId);
		}

		private static bool TryParseSettlementProvenance(string Row, out string SettlementId,
			out int Version, out KingdomIdentityOrigin Origin, out string TransactionId,
			out long FoundedTick, out string FirstClaimedZone, out string LegacyId)
		{
			SettlementId = null; Version = 0; Origin = KingdomIdentityOrigin.None;
			TransactionId = null; FoundedTick = 0L; FirstClaimedZone = null; LegacyId = null;
			if (string.IsNullOrEmpty(Row) || Row.Length > 4300 || !IsToken(Row)) return false;
			string[] parts = Row.Split(new char[] { '.' }, StringSplitOptions.None);
			if (parts.Length != 7 || !KingdomIdentityRules.IsSettlementId(parts[0]) ||
				!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture,
					out Version) || Version < 0 || Version > 32 ||
				!int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture,
					out int origin) || origin < 0 || origin > 2 ||
				!long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture,
					out FoundedTick) || FoundedTick < 0L ||
				!TryUnhex(parts[4], 1024, out TransactionId) ||
				!TryUnhex(parts[5], 1024, out FirstClaimedZone) ||
				!TryUnhex(parts[6], 1024, out LegacyId)) return false;
			SettlementId = parts[0]; Origin = (KingdomIdentityOrigin)origin;
			return true;
		}

		private static bool TryHex(string Value, int MaxBytes, out string Hex)
		{
			Hex = null;
			try
			{
				byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value ?? "");
				if (bytes.Length > MaxBytes) return false;
				StringBuilder text = new StringBuilder(bytes.Length * 2);
				for (int i = 0; i < bytes.Length; i++) text.Append(bytes[i].ToString("x2"));
				Hex = text.ToString();
				return true;
			}
			catch { return false; }
		}

		private static bool TryUnhex(string Hex, int MaxBytes, out string Value)
		{
			Value = null;
			if (Hex == null || (Hex.Length & 1) != 0 || Hex.Length > MaxBytes * 2) return false;
			try
			{
				byte[] bytes = new byte[Hex.Length / 2];
				for (int i = 0; i < bytes.Length; i++)
				{
					int high = HexNibble(Hex[i * 2]); int low = HexNibble(Hex[i * 2 + 1]);
					if (high < 0 || low < 0) return false;
					bytes[i] = (byte)((high << 4) | low);
				}
				Value = new UTF8Encoding(false, true).GetString(bytes);
				return true;
			}
			catch { return false; }
		}

		private static int HexNibble(char Value)
		{
			if (Value >= '0' && Value <= '9') return Value - '0';
			if (Value >= 'a' && Value <= 'f') return Value - 'a' + 10;
			return -1;
		}

		/// <summary>
		/// True when a string is safe to keep as prose: no control characters, no markup a later
		/// renderer would obey, no brace or ampersand sequence at all.
		/// <para>
		/// Qud's own markup is <c>{{colour|text}}</c> and <c>&amp;Y</c>. A settlement name carrying
		/// either would be obeyed by every string it is later concatenated into &mdash; including
		/// popups and the founding book. Names are sanitized at capture; this is the gate that
		/// proves it happened, and it is checked again on the way in from a file nobody here wrote.
		/// </para>
		/// </summary>
		public static bool IsSafeText(string Value)
		{
			if (Value == null)
			{
				return false;
			}
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (c < ' ' || c == '\u007F' || c == '{' || c == '}' || c == '&' || c == '\\')
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// A player-chosen name as it may be written into a seal: markup removed rather than
		/// escaped, whitespace collapsed, and cut to length on a word where it can be.
		/// </summary>
		/// <param name="Value">Anything, including null.</param>
		/// <param name="MaxChars">The cut. At or below zero returns empty.</param>
		/// <returns>A string satisfying <see cref="IsSafeText"/>; never null.</returns>
		public static string SanitizeText(string Value, int MaxChars)
		{
			if (Value == null || MaxChars <= 0)
			{
				return "";
			}
			StringBuilder sb = new StringBuilder(Value.Length);
			bool space = false;
			int i = 0;
			while (i < Value.Length)
			{
				char c = Value[i];
				// {{colour|text}} keeps its text and loses its tag. Dropping the braces alone would
				// leave the colour word and the bar behind, which reads as garbage in a name and is
				// worse than either keeping the markup or losing the whole thing.
				if (c == '{' && i + 1 < Value.Length && Value[i + 1] == '{')
				{
					i = OpenTag(Value, i);
					continue;
				}
				if (c == '{' || c == '}' || c == '\\')
				{
					i++;
					continue;
				}
				// A colour code is the ampersand and the letter after it. A trailing ampersand with
				// nothing to colour is simply dropped.
				if (c == '&')
				{
					i += (i + 1 < Value.Length) ? 2 : 1;
					continue;
				}
				i++;
				if (c < ' ' || c == '\u007F')
				{
					c = ' ';
				}
				if (c == ' ')
				{
					if (space || sb.Length == 0)
					{
						continue;
					}
					space = true;
					sb.Append(' ');
					continue;
				}
				space = false;
				sb.Append(c);
			}
			while (sb.Length > 0 && sb[sb.Length - 1] == ' ')
			{
				sb.Length--;
			}
			if (sb.Length <= MaxChars)
			{
				return sb.ToString();
			}
			string cut = sb.ToString(0, MaxChars);
			int lastSpace = cut.LastIndexOf(' ');
			if (lastSpace >= MaxChars / 2)
			{
				cut = cut.Substring(0, lastSpace);
			}
			return cut.TrimEnd();
		}

		/// <summary>
		/// Where reading resumes after a <c>{{</c>: past the tag's separator when it has one, past
		/// the braces when it does not. The scan is bounded by the string, so an unclosed tag costs
		/// the rest of the name rather than looping.
		/// </summary>
		private static int OpenTag(string Value, int At)
		{
			for (int i = At + 2; i < Value.Length; i++)
			{
				if (Value[i] == '|')
				{
					return i + 1;
				}
				if (Value[i] == '}' || Value[i] == '{')
				{
					return i;
				}
			}
			return Value.Length;
		}

		/// <summary>
		/// An identifier as it may be written into a seal: everything outside the token alphabet
		/// replaced with a dot, and cut to length. A caller that hands in a path gets a token, not
		/// a path.
		/// </summary>
		/// <returns>A string satisfying <see cref="IsToken"/>; never null; empty for null input.</returns>
		public static string SanitizeToken(string Value, int MaxChars)
		{
			if (Value == null || MaxChars <= 0)
			{
				return "";
			}
			StringBuilder sb = new StringBuilder(Value.Length);
			for (int i = 0; i < Value.Length && sb.Length < MaxChars; i++)
			{
				sb.Append((TokenAlphabet.IndexOf(Value[i]) >= 0) ? Value[i] : '.');
			}
			return sb.ToString();
		}

		/// <summary>
		/// The chronicle as a seal keeps it: the beginning and the end, with a scribe's note where
		/// the copy skips.
		/// <para>
		/// A book longer than the cap loses its middle rather than its head, because the founding
		/// is the half a stranger reads a dead town's book for. The note is written into the copy
		/// so the gap is visible rather than seamless &mdash; a chronicle that quietly omits is
		/// worse than one that says it is a copy.
		/// </para>
		/// </summary>
		/// <param name="Lines">The living register. Null is empty.</param>
		/// <param name="MaxLines">The cap; at or below two returns at most that many head lines.</param>
		/// <returns>A new list; never null; never longer than <paramref name="MaxLines"/>.</returns>
		public static List<string> PinChronicle(IList<string> Lines, int MaxLines)
		{
			List<string> kept = new List<string>();
			if (Lines == null || MaxLines <= 0)
			{
				return kept;
			}
			if (Lines.Count <= MaxLines)
			{
				for (int i = 0; i < Lines.Count; i++)
				{
					kept.Add(SanitizeText(Lines[i], KingdomSealRecord.MaxLineChars));
				}
				return kept;
			}
			if (MaxLines <= 2)
			{
				for (int i = 0; i < MaxLines; i++)
				{
					kept.Add(SanitizeText(Lines[i], KingdomSealRecord.MaxLineChars));
				}
				return kept;
			}
			int head = (MaxLines - 1) / 2;
			int tail = MaxLines - 1 - head;
			for (int i = 0; i < head; i++)
			{
				kept.Add(SanitizeText(Lines[i], KingdomSealRecord.MaxLineChars));
			}
			kept.Add("Here the copy skips " + (Lines.Count - head - tail) + " entries the book no longer holds.");
			for (int i = Lines.Count - tail; i < Lines.Count; i++)
			{
				kept.Add(SanitizeText(Lines[i], KingdomSealRecord.MaxLineChars));
			}
			return kept;
		}

		/// <summary>
		/// Seals a settlement: the whole of what crosses, and nothing else.
		/// <para>
		/// Preconditions: <paramref name="Seat"/> is a settlement whose <c>City</c> book has been
		/// normalized (which <c>KingdomSettlement.Normalize</c> guarantees on every read and every
		/// seat swap). Side effects: none &mdash; the settlement is read and never written, so a
		/// seal taken mid-play cannot perturb the run it is describing.
		/// </para>
		/// <para>
		/// The record comes back <see cref="KingdomSealStatus.Living"/> and unresolved. A stage is
		/// not a fate; the draw happens once, at promotion, and never here.
		/// </para>
		/// </summary>
		/// <param name="Seat">The settlement being sealed.</param>
		/// <param name="Lineage">Who this is, and where it came from.</param>
		/// <param name="RealmName">The realm's display name.</param>
		/// <param name="FounderName">The founder as the world would name them.</param>
		/// <param name="Chronicle">The official register.</param>
		/// <param name="Outsider">The rumour register.</param>
		/// <param name="WrittenTick">The world tick this record was taken at. Diagnostics only.</param>
		/// <returns>A complete record; never null.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="Seat"/> or
		/// <paramref name="Lineage"/> is null.</exception>
		public static KingdomSealRecord Capture(KingdomSettlement Seat, KingdomSealIdentity Identity,
			KingdomSealLineage Lineage, string RealmName, string FounderName,
			IList<string> Chronicle, IList<string> Outsider, long WrittenTick)
		{
			if (Seat == null)
			{
				throw new ArgumentNullException("Seat");
			}
			if (Lineage == null)
			{
				throw new ArgumentNullException("Lineage");
			}
			if (!ExactIdentity(Identity, Seat))
				throw new ArgumentException("Seal capture requires exact immutable realm topology and provenance.",
					"Identity");
			Simulation.City.KingdomCityBook book = Seat.City ?? new Simulation.City.KingdomCityBook();
			KingdomSealRecord record = new KingdomSealRecord();
			record.Status = KingdomSealStatus.Living;
			record.LineageId = SanitizeToken(Lineage.LineageId, KingdomSealRecord.MaxIdChars);
			record.LegacyId = SanitizeToken(Lineage.LegacyId, KingdomSealRecord.MaxIdChars);
			record.OriginGameId = SanitizeToken(Lineage.OriginGameId, KingdomSealRecord.MaxIdChars);
			record.Generation = (Lineage.Generation > 0) ? Lineage.Generation : 0;
			record.Revision = (Lineage.Revision > 0) ? Lineage.Revision : 0;
			record.WrittenTick = (WrittenTick > 0L) ? WrittenTick : 0L;
			record.FounderName = SanitizeText(FounderName, KingdomSealRecord.MaxNameChars);
			record.RealmName = SanitizeText(RealmName, KingdomSealRecord.MaxNameChars);
			record.SettlementName = SanitizeText(Seat.SettlementName, KingdomSealRecord.MaxNameChars);
			// Identity-labelled seal payloads never promote a mutable display name. A corrupt or
			// pre-v8 city remains visibly unbound until an explicit migration supplies exact proof.
			record.RealmId = Identity.RealmId;
			record.RealmSettlementIds = new List<string>(Identity.SettlementIds);
			record.RealmSettlementIds.Sort(StringComparer.Ordinal);
			record.RealmSettlementProvenance =
				new List<string>(Identity.SettlementProvenanceRows);
			record.RealmIdentityVersion = Identity.RealmIdentityVersion;
			record.RealmIdentityOrigin = Identity.RealmIdentityOrigin;
			record.RealmIdentityTransactionId = Identity.RealmIdentityTransactionId ?? "";
			record.RealmIdentityLegacyFaction = Identity.RealmIdentityLegacyFaction ?? "";
			record.RealmIdentityFoundedTick = Identity.RealmIdentityFoundedTick;
			record.RealmIdentitySeedHigh = Identity.RealmIdentitySeedHigh;
			record.RealmIdentitySeedLow = Identity.RealmIdentitySeedLow;
			record.RealmIdentityFirstClaimedZone = Identity.RealmIdentityFirstClaimedZone ?? "";
			record.SettlementId = Identity.SettlementId;
			record.SettlementIdentityVersion = Identity.SettlementIdentityVersion;
			record.SettlementIdentityOrigin = Identity.SettlementIdentityOrigin;
			record.SettlementIdentityTransactionId = Identity.SettlementIdentityTransactionId ?? "";
			record.SettlementIdentityFoundedTick = Identity.SettlementIdentityFoundedTick;
			record.SettlementIdentityFirstClaimedZone =
				Identity.SettlementIdentityFirstClaimedZone ?? "";
			record.SettlementIdentityLegacyId = Identity.SettlementIdentityLegacyId ?? "";
			record.Vocation = SanitizeText(Seat.Vocation, KingdomSealRecord.MaxNameChars);
			record.Style = SanitizeText(Seat.Style, KingdomSealRecord.MaxNameChars);
			record.FoundedTick = (Seat.FoundedTick > 0L) ? Seat.FoundedTick : 0L;
			record.RegionName = SanitizeText(Seat.FoundingRegionName, KingdomSealRecord.MaxNameChars);
			record.TerrainBlueprint = SanitizeToken(Seat.FoundingTerrainBlueprint, KingdomSealRecord.MaxIdChars);
			record.Depth = Clamp(Seat.FoundingZLevel, -128, 128);

			string ground = ChooseGround(book, Seat.ClaimedZones);
			record.GroundZoneId = SanitizeToken(ground, KingdomSealRecord.MaxIdChars);

			record.Stage = Clamp((int)Seat.Stage, 0, 8);
			record.Population = Clamp(Seat.Population, 0, KingdomSealRecord.MaxRoll);
			record.Defence = Clamp(DefenceOf(book, ground), 0, 4096);
			record.StoredWater = Clamp((int)ClampLong(book.WaterLevel, 0L, 1000000L), 0, 1000000);
			record.Withered = Seat.Withered;
			record.Vigour = KingdomRules.SealedVigour((GrowthStage)record.Stage, record.Population, record.Defence, record.StoredWater, record.Withered);

			CaptureWorks(book, ground, record);
			CaptureRoll(Seat, record);
			CaptureTallies(Seat.OriginCounts, KingdomSealRecord.MaxTallies, record.OriginKeys, record.OriginCounts);
			CaptureTallies(Seat.CreedCounts, KingdomSealRecord.MaxTallies, record.CreedKeys, record.CreedCounts);
			record.Chronicle = PinChronicle(Chronicle, KingdomSealRecord.MaxChronicle);
			record.Outsider = PinChronicle(Outsider, KingdomSealRecord.MaxChronicle);
			CaptureDead(Seat, record);
			return record;
		}

		/// <summary>
		/// The same record with a death written into it. Copy-on-write: the staged record is not
		/// touched, because a terminal attempt that a checkpoint later undoes must leave the stage
		/// exactly as it was.
		/// </summary>
		/// <param name="Record">The staged record.</param>
		/// <param name="CauseText">One clause naming how the founder died.</param>
		/// <param name="CauseKind">A short token for the kind of death.</param>
		/// <param name="CauseTurn">The turn it happened on.</param>
		/// <returns>A new record at <see cref="KingdomSealStatus.Terminal"/>.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="Record"/> is null.</exception>
		public static KingdomSealRecord WithTerminalCause(KingdomSealRecord Record, string CauseText, string CauseKind, long CauseTurn)
		{
			KingdomSealRecord copy = Copy(Record);
			if (Record.Status != KingdomSealStatus.Living)
			{
				throw new InvalidOperationException("Only a living stage can become a terminal attempt.");
			}
			if (Record.Revision == int.MaxValue)
			{
				throw new InvalidOperationException("The seal revision cannot advance further.");
			}
			copy.Status = KingdomSealStatus.Terminal;
			copy.CauseText = SanitizeText(CauseText, KingdomSealRecord.MaxLineChars);
			copy.CauseKind = SanitizeToken(CauseKind, KingdomSealRecord.MaxIdChars);
			if (copy.CauseText.Length == 0)
			{
				copy.CauseText = "death";
			}
			if (copy.CauseKind.Length == 0)
			{
				copy.CauseKind = "unknown";
			}
			copy.CauseTurn = (CauseTurn > 0L) ? CauseTurn : 0L;
			copy.Revision = Record.Revision + 1;
			return copy;
		}

		/// <summary>
		/// The same record sealed by the founder's own hand. Retirement is a separate act from
		/// death and keeps the save alive; what it settles is that <i>this generation</i> of the
		/// lineage can no longer be rewritten by playing on.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="Record"/> is null.</exception>
		public static KingdomSealRecord WithRetirement(KingdomSealRecord Record)
		{
			KingdomSealRecord copy = Copy(Record);
			if (Record.Status != KingdomSealStatus.Living)
			{
				throw new InvalidOperationException("Only a living stage can be retired explicitly.");
			}
			if (Record.Revision == int.MaxValue)
			{
				throw new InvalidOperationException("The seal revision cannot advance further.");
			}
			copy.Status = KingdomSealStatus.Retired;
			copy.Revision = Record.Revision + 1;
			return copy;
		}

		/// <summary>
		/// The seed the interregnum is drawn from: lineage, origin, generation, revision, and
		/// nothing else in the world.
		/// <para>
		/// Never the target world's seed, the calendar, system time, the founder's last visit, or
		/// any stream a player can turn over again. An earlier draft of the design mixed in the
		/// destination's seed, which would have handed back exactly the reroll the whole rule
		/// exists to prevent: regenerate the world, draw again. Because the seed is the legacy's
		/// own, a legacy's fate is fixed the moment it is promoted and arrives in every world the
		/// same way.
		/// </para>
		/// </summary>
		/// <param name="Lineage">The immutable legacy identity.</param>
		/// <returns>A stable seed for <c>KingdomRules.InterregnumRoll</c>.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="Lineage"/> is null.</exception>
		public static long InterregnumSeed(KingdomSealLineage Lineage)
		{
			if (Lineage == null)
			{
				throw new ArgumentNullException("Lineage");
			}
			// FNV-1a over the four immutable fields, with a separator no token alphabet contains
			// so that ("ab","c") and ("a","bc") cannot fold to the same seed.
			ulong hash = 14695981039346656037UL;
			hash = Fold(hash, Lineage.LineageId);
			hash = FoldByte(hash, 0x1F);
			hash = Fold(hash, Lineage.OriginGameId);
			hash = FoldByte(hash, 0x1F);
			hash = FoldInt(hash, Lineage.Generation);
			hash = FoldByte(hash, 0x1F);
			hash = FoldInt(hash, Lineage.Revision);
			return unchecked((long)hash);
		}

		/// <summary>
		/// Draws the one fortune and fixes the inherited state. The record comes back
		/// <see cref="KingdomSealStatus.Promoted"/> and immutable in meaning: nothing later
		/// redraws it, and retrying world generation reproduces it exactly.
		/// </summary>
		/// <param name="Record">A terminal attempt.</param>
		/// <param name="Verdict">The engine's score/save verdict for its origin.</param>
		/// <returns>A new promoted record.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="Record"/> is null.</exception>
		/// <exception cref="InvalidOperationException"><paramref name="Record"/> was already
		/// promoted. A second promotion would be a second fate for one life.</exception>
		public static KingdomSealRecord Promote(KingdomSealRecord Record, KingdomSealEligibility Verdict)
		{
			if (Record == null)
			{
				throw new ArgumentNullException("Record");
			}
			if (!MayPromote(Record.Status, Verdict))
			{
				throw new InvalidOperationException("Automatic promotion requires a terminal attempt from an ended run.");
			}
			return ResolvePromotion(Record);
		}

		/// <summary>Resolves an explicit retirement. Separate from automatic terminal promotion so
		/// engine eligibility can never turn a living or retired stage into an automatic import.</summary>
		public static KingdomSealRecord PromoteRetirement(KingdomSealRecord Record)
		{
			if (Record == null)
			{
				throw new ArgumentNullException("Record");
			}
			if (Record.Status != KingdomSealStatus.Retired)
			{
				throw new InvalidOperationException("Explicit retirement promotion requires a retired record.");
			}
			return ResolvePromotion(Record);
		}

		private static KingdomSealRecord ResolvePromotion(KingdomSealRecord Record)
		{
			KingdomSealRecord copy = Copy(Record);
			long seed = InterregnumSeed(new KingdomSealLineage(Record.LineageId, Record.LegacyId,
				Record.OriginGameId, Record.Generation, Record.Revision));
			int roll = KingdomRules.InterregnumRoll(seed);
			copy.Status = KingdomSealStatus.Promoted;
			copy.InterregnumRoll = roll;
			copy.InheritedState = (int)KingdomRules.ResolveInheritedState(Record.Vigour, roll, Record.Population);
			return copy;
		}

		/// <summary>
		/// What the engine's two facts about an origin run mean.
		/// <para>
		/// The one automatic crossing is a score with no save: the engine itself scored the run and
		/// then deleted it, which only permadeath's own terminal block does. A score with a save is
		/// a checkpoint death, permadeath switched off, or a cleanup that did not finish, and none
		/// of those is an ending. Neither fact is an orphan &mdash; a stage whose game nobody can
		/// account for &mdash; and an orphan is never taken silently.
		/// </para>
		/// </summary>
		/// <param name="ScoreForOrigin">A scoreboard entry exists for the origin game id.</param>
		/// <param name="OriginSaveStands">A valid primary save still exists for it.</param>
		public static KingdomSealEligibility Judge(bool ScoreForOrigin, bool OriginSaveStands)
		{
			if (ScoreForOrigin)
			{
				return OriginSaveStands ? KingdomSealEligibility.Checkpointed : KingdomSealEligibility.Ended;
			}
			return OriginSaveStands ? KingdomSealEligibility.Living : KingdomSealEligibility.Orphaned;
		}

		/// <summary>
		/// Whether a staged record of this status, with this verdict, may be promoted without
		/// asking anyone.
		/// <para>
		/// Retirement is deliberately absent. It uses <see cref="PromoteRetirement"/> and never
		/// enters the score/save automatic path.
		/// </para>
		/// </summary>
		public static bool MayPromote(KingdomSealStatus Status, KingdomSealEligibility Verdict)
		{
			return Status == KingdomSealStatus.Terminal && Verdict == KingdomSealEligibility.Ended;
		}

		/// <summary>
		/// Chooses the legacy a new world is offered, under the configured policy.
		/// <para>
		/// Deterministic and independent of the order the caller found the files in: latest is the
		/// deepest generation, then the highest revision, then the latest written tick, then the
		/// legacy id in ordinal order. A directory listing is not an ordering, and two players
		/// with the same legacies must be offered the same one.
		/// </para>
		/// </summary>
		/// <param name="Legacies">Promoted legacies. Null or empty selects nothing.</param>
		/// <param name="Spent">Legacy ids already consumed or declined; null means none.</param>
		/// <param name="Policy">The import policy.</param>
		/// <returns>The chosen legacy, or null when there is nothing to offer.</returns>
		public static KingdomSealRecord Select(IList<KingdomSealRecord> Legacies, ICollection<string> Spent, KingdomImportPolicy Policy)
		{
			if (Policy != KingdomImportPolicy.LatestEligible || Legacies == null)
			{
				return null;
			}
			KingdomSealRecord best = null;
			for (int i = 0; i < Legacies.Count; i++)
			{
				KingdomSealRecord candidate = Legacies[i];
				if (candidate == null || candidate.Status != KingdomSealStatus.Promoted || !candidate.IsResolved)
				{
					continue;
				}
				if (Spent != null && Spent.Contains(candidate.LegacyId))
				{
					continue;
				}
				if (best == null || Later(candidate, best))
				{
					best = candidate;
				}
			}
			return best;
		}

		/// <summary>True when <paramref name="A"/> is the later legacy under the selection order.</summary>
		public static bool Later(KingdomSealRecord A, KingdomSealRecord B)
		{
			if (A.Generation != B.Generation)
			{
				return A.Generation > B.Generation;
			}
			if (A.Revision != B.Revision)
			{
				return A.Revision > B.Revision;
			}
			if (A.WrittenTick != B.WrittenTick)
			{
				return A.WrittenTick > B.WrittenTick;
			}
			return string.CompareOrdinal(A.LegacyId, B.LegacyId) > 0;
		}

		/// <summary>
		/// A deep copy. Used by every transition, because a seal's states are derived rather than
		/// mutated: a terminal attempt that a checkpoint undoes must leave the stage untouched.
		/// </summary>
		/// <exception cref="ArgumentNullException"><paramref name="Record"/> is null.</exception>
		public static KingdomSealRecord Copy(KingdomSealRecord Record)
		{
			if (Record == null)
			{
				throw new ArgumentNullException("Record");
			}
			KingdomSealRecord copy = new KingdomSealRecord();
			copy.WriterVersion = Record.WriterVersion;
			copy.EngineVersion = Record.EngineVersion;
			copy.Status = Record.Status;
			copy.LineageId = Record.LineageId;
			copy.LegacyId = Record.LegacyId;
			copy.OriginGameId = Record.OriginGameId;
			copy.Generation = Record.Generation;
			copy.Revision = Record.Revision;
			copy.WrittenTick = Record.WrittenTick;
			copy.FounderName = Record.FounderName;
			copy.CauseText = Record.CauseText;
			copy.CauseKind = Record.CauseKind;
			copy.CauseTurn = Record.CauseTurn;
			copy.RealmName = Record.RealmName;
			copy.SettlementName = Record.SettlementName;
			copy.SettlementId = Record.SettlementId;
			copy.RealmId = Record.RealmId;
			copy.RealmSettlementIds = new List<string>(Record.RealmSettlementIds);
			copy.RealmSettlementProvenance =
				new List<string>(Record.RealmSettlementProvenance);
			copy.RealmIdentityVersion = Record.RealmIdentityVersion;
			copy.RealmIdentityOrigin = Record.RealmIdentityOrigin;
			copy.RealmIdentityTransactionId = Record.RealmIdentityTransactionId;
			copy.RealmIdentityLegacyFaction = Record.RealmIdentityLegacyFaction;
			copy.RealmIdentityFoundedTick = Record.RealmIdentityFoundedTick;
			copy.RealmIdentitySeedHigh = Record.RealmIdentitySeedHigh;
			copy.RealmIdentitySeedLow = Record.RealmIdentitySeedLow;
			copy.RealmIdentityFirstClaimedZone = Record.RealmIdentityFirstClaimedZone;
			copy.SettlementIdentityVersion = Record.SettlementIdentityVersion;
			copy.SettlementIdentityOrigin = Record.SettlementIdentityOrigin;
			copy.SettlementIdentityTransactionId = Record.SettlementIdentityTransactionId;
			copy.SettlementIdentityFoundedTick = Record.SettlementIdentityFoundedTick;
			copy.SettlementIdentityFirstClaimedZone =
				Record.SettlementIdentityFirstClaimedZone;
			copy.SettlementIdentityLegacyId = Record.SettlementIdentityLegacyId;
			copy.Vocation = Record.Vocation;
			copy.Style = Record.Style;
			copy.FoundedTick = Record.FoundedTick;
			copy.GroundZoneId = Record.GroundZoneId;
			copy.RegionName = Record.RegionName;
			copy.TerrainBlueprint = Record.TerrainBlueprint;
			copy.Depth = Record.Depth;
			copy.Stage = Record.Stage;
			copy.Population = Record.Population;
			copy.Defence = Record.Defence;
			copy.StoredWater = Record.StoredWater;
			copy.Withered = Record.Withered;
			copy.Vigour = Record.Vigour;
			copy.InterregnumRoll = Record.InterregnumRoll;
			copy.InheritedState = Record.InheritedState;
			copy.WorkKeys = new List<string>(Record.WorkKeys);
			copy.WorkX = new List<int>(Record.WorkX);
			copy.WorkY = new List<int>(Record.WorkY);
			copy.WorkConditions = new List<int>(Record.WorkConditions);
			copy.SpatialVersion = Record.SpatialVersion;
			copy.SpatialWidth = Record.SpatialWidth;
			copy.SpatialHeight = Record.SpatialHeight;
			copy.SpatialEntrySide = Record.SpatialEntrySide;
			copy.SpatialEntryX = Record.SpatialEntryX;
			copy.SpatialEntryY = Record.SpatialEntryY;
			copy.WorkSnapshots = new List<string>(Record.WorkSnapshots);
			copy.WorkSnapshotHashes = new List<string>(Record.WorkSnapshotHashes);
			copy.StreetX = new List<int>(Record.StreetX);
			copy.StreetY = new List<int>(Record.StreetY);
			copy.RollNames = new List<string>(Record.RollNames);
			copy.RollOrigins = new List<string>(Record.RollOrigins);
			copy.RollArrived = new List<string>(Record.RollArrived);
			copy.OriginKeys = new List<string>(Record.OriginKeys);
			copy.OriginCounts = new List<int>(Record.OriginCounts);
			copy.CreedKeys = new List<string>(Record.CreedKeys);
			copy.CreedCounts = new List<int>(Record.CreedCounts);
			copy.Chronicle = new List<string>(Record.Chronicle);
			copy.Outsider = new List<string>(Record.Outsider);
			copy.DeadNames = new List<string>(Record.DeadNames);
			copy.DeadCauses = new List<string>(Record.DeadCauses);
			return copy;
		}

		/// <summary>
		/// The seat's ground: the zone holding the most of the settlement's works, ties broken by
		/// zone id so the answer never depends on the order a dictionary happened to enumerate in.
		/// <para>
		/// The MVP inherits one seat zone (<c>DECISIONS.md:222-227</c>). Which one is not a
		/// judgement call about importance: it is where the settlement most is.
		/// </para>
		/// </summary>
		public static string ChooseGround(Simulation.City.KingdomCityBook Book, IList<string> ClaimedZones)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			if (Book != null)
			{
				for (int i = 0; i < Book.WorkZoneIds.Count; i++)
				{
					string zone = Book.WorkZoneIds[i];
					if (string.IsNullOrEmpty(zone))
					{
						continue;
					}
					int count;
					counts[zone] = (counts.TryGetValue(zone, out count) ? count : 0) + 1;
				}
			}
			string best = null;
			int bestCount = -1;
			foreach (KeyValuePair<string, int> pair in counts)
			{
				if (pair.Value > bestCount || (pair.Value == bestCount && string.CompareOrdinal(pair.Key, best) < 0))
				{
					best = pair.Key;
					bestCount = pair.Value;
				}
			}
			if (best != null)
			{
				return best;
			}
			// A settlement with no works at all still stood somewhere. The first claimed zone in
			// ordinal order is the honest answer, and an unfounded one has no ground to name.
			string firstClaim = null;
			if (ClaimedZones != null)
			{
				for (int i = 0; i < ClaimedZones.Count; i++)
				{
					string zone = ClaimedZones[i];
					if (!string.IsNullOrEmpty(zone) && (firstClaim == null || string.CompareOrdinal(zone, firstClaim) < 0))
					{
						firstClaim = zone;
					}
				}
			}
			return firstClaim ?? "";
		}

		private static int DefenceOf(Simulation.City.KingdomCityBook Book, string Ground)
		{
			if (Book == null || string.IsNullOrEmpty(Ground))
			{
				return 0;
			}
			for (int i = 0; i < Book.ZoneIds.Count && i < Book.ZoneDefences.Count; i++)
			{
				if (Book.ZoneIds[i] == Ground)
				{
					return Book.ZoneDefences[i];
				}
			}
			return 0;
		}

		private static void CaptureWorks(Simulation.City.KingdomCityBook Book, string Ground, KingdomSealRecord Record)
		{
			if (Book == null || string.IsNullOrEmpty(Ground))
			{
				return;
			}
			int rows = Book.WorkIds.Count;
			for (int i = 0; i < rows && Record.WorkKeys.Count < KingdomSealRecord.MaxWorks; i++)
			{
				if (i >= Book.WorkZoneIds.Count || Book.WorkZoneIds[i] != Ground)
				{
					continue;
				}
				if (i >= Book.WorkDesignKeys.Count || i >= Book.WorkAnchorsX.Count || i >= Book.WorkAnchorsY.Count || i >= Book.WorkConditions.Count)
				{
					continue;
				}
				string design = Book.WorkDesignKeys[i];
				string key;
				if (!KingdomInheritRules.TrySemanticKeyForBlueprint(design, out key))
				{
					// Compatibility for early/dev books which wrote the semantic key itself.
					// A blueprint-shaped or malformed unknown is dropped fail-closed.
					key = SanitizeToken(design, KingdomSealRecord.MaxIdChars);
					if (!KingdomInheritRules.IsStableSemanticKey(key))
					{
						continue;
					}
				}
				if (key.Length == 0)
				{
					continue;
				}
				int x = Book.WorkAnchorsX[i];
				int y = Book.WorkAnchorsY[i];
				// Out-of-zone coordinates are dropped rather than clamped. A clamped anchor would
				// pile works on an edge cell in the next world; a dropped one is one work the ruin
				// does not have, which is a smaller lie.
				if (x < 0 || x > 255 || y < 0 || y > 255)
				{
					continue;
				}
				Record.WorkKeys.Add(key);
				Record.WorkX.Add(x);
				Record.WorkY.Add(y);
				Record.WorkConditions.Add(Clamp(Book.WorkConditions[i], 0, 100));
			}
		}

		private static void CaptureRoll(KingdomSettlement Seat, KingdomSealRecord Record)
		{
			Simulation.City.KingdomCityState state;
			Simulation.City.KingdomCityFault fault;
			Simulation.City.KingdomResidentRollProjection roll;
			if (Seat?.City == null || !Seat.City.TryRead(out state, out fault)
				|| !Simulation.City.KingdomResidentRules.TryProject(state, out roll)) return;
			int rows = roll.Names.Count;
			for (int i = 0; i < rows && Record.RollNames.Count < KingdomSealRecord.MaxRoll; i++)
			{
				string name = SanitizeText(roll.Names[i], KingdomSealRecord.MaxNameChars);
				if (name.Length == 0)
				{
					continue;
				}
				Record.RollNames.Add(name);
				Record.RollOrigins.Add(SanitizeText((i < roll.Origins.Count) ? roll.Origins[i] : "", KingdomSealRecord.MaxNameChars));
				Record.RollArrived.Add(SanitizeText((i < roll.Arrived.Count) ? roll.Arrived[i] : "", KingdomSealRecord.MaxNameChars));
			}
		}

		private static void CaptureDead(KingdomSettlement Seat, KingdomSealRecord Record)
		{
			int rows = Seat.DeadNames.Count;
			for (int i = 0; i < rows && Record.DeadNames.Count < KingdomSealRecord.MaxDead; i++)
			{
				string name = SanitizeText(Seat.DeadNames[i], KingdomSealRecord.MaxNameChars);
				if (name.Length == 0)
				{
					continue;
				}
				Record.DeadNames.Add(name);
				Record.DeadCauses.Add(SanitizeText((i < Seat.DeadCauses.Count) ? Seat.DeadCauses[i] : "", KingdomSealRecord.MaxLineChars));
			}
		}

		/// <summary>
		/// Tallies as a seal keeps them: sorted by key so the file is canonical, since a
		/// dictionary's enumeration order is not a fact about a settlement.
		/// </summary>
		private static void CaptureTallies(Dictionary<string, int> Source, int MaxRows, List<string> Keys, List<int> Counts)
		{
			if (Source == null)
			{
				return;
			}
			Dictionary<string, int> folded = new Dictionary<string, int>();
			foreach (KeyValuePair<string, int> pair in Source)
			{
				if (pair.Value <= 0)
				{
					continue;
				}
				string key = SanitizeToken(pair.Key, KingdomSealRecord.MaxIdChars);
				if (key.Length == 0)
				{
					continue;
				}
				// Two source keys can sanitize to one token, and the tally they share is their sum
				// rather than whichever the enumeration reached last.
				int running;
				folded[key] = (folded.TryGetValue(key, out running) ? running : 0) + pair.Value;
			}
			List<string> ordered = new List<string>(folded.Keys);
			ordered.Sort(StringComparer.Ordinal);
			for (int i = 0; i < ordered.Count && Keys.Count < MaxRows; i++)
			{
				Keys.Add(ordered[i]);
				Counts.Add(Clamp(folded[ordered[i]], 0, 100000));
			}
		}

		private static ulong Fold(ulong Hash, string Value)
		{
			string value = Value ?? "";
			for (int i = 0; i < value.Length; i++)
			{
				Hash = FoldByte(Hash, (byte)(value[i] & 0xFF));
				Hash = FoldByte(Hash, (byte)(value[i] >> 8));
			}
			return Hash;
		}

		private static ulong FoldInt(ulong Hash, int Value)
		{
			uint value = unchecked((uint)Value);
			Hash = FoldByte(Hash, (byte)(value >> 24));
			Hash = FoldByte(Hash, (byte)(value >> 16));
			Hash = FoldByte(Hash, (byte)(value >> 8));
			return FoldByte(Hash, (byte)value);
		}

		private static ulong FoldByte(ulong Hash, byte Value)
		{
			Hash ^= Value;
			return unchecked(Hash * 1099511628211UL);
		}

		private static int Clamp(int Value, int Low, int High)
		{
			return (Value < Low) ? Low : ((Value > High) ? High : Value);
		}

		private static long ClampLong(long Value, long Low, long High)
		{
			return (Value < Low) ? Low : ((Value > High) ? High : Value);
		}
	}
}
