using System;
using XRL;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	/// <summary>What one publication attempt did. Confirmed never wrote anything.</summary>
	internal enum KingdomSubsidenceOptionPublication : byte
	{
		Refused = 0,
		Confirmed = 1,
		Published = 2
	}

	/// <summary>
	/// Immutable capture of one durable key across all five game-state tables.
	/// <para>
	/// Presence comes only from the engine's <c>Has*GameState</c> family. A getter carrying a
	/// default answers identically for a key that was never written and for a key explicitly
	/// holding zero, false, null, or the empty string, so no getter is ever consulted about
	/// absence here. Only the string table's value is read, because the string table is the only
	/// table this receipt may legitimately occupy; the other four contribute presence alone, and
	/// their values are deliberately never fetched.
	/// </para>
	/// </summary>
	internal readonly struct KingdomSubsidenceOptionTables
	{
		internal readonly bool HasString;
		internal readonly string String;
		internal readonly bool HasInt;
		internal readonly bool HasInt64;
		internal readonly bool HasObject;
		internal readonly bool HasBoolean;

		internal KingdomSubsidenceOptionTables(KingdomDurableKeyObservation Source)
		{
			HasString = Source.HasString;
			String = Source.String;
			HasInt = Source.HasInt;
			HasInt64 = Source.HasInt64;
			HasObject = Source.HasObject;
			HasBoolean = Source.HasBoolean;
		}

		/// <summary>Exact bytes: every table's presence and the text the string table holds.</summary>
		internal bool SameBytes(KingdomSubsidenceOptionTables Other)
		{
			return SameOtherTables(Other) && HasString == Other.HasString
				&& string.Equals(String, Other.String, StringComparison.Ordinal);
		}

		/// <summary>The four tables a text publication must leave exactly as it found them.</summary>
		internal bool SameOtherTables(KingdomSubsidenceOptionTables Other)
		{
			return HasInt == Other.HasInt && HasInt64 == Other.HasInt64
				&& HasObject == Other.HasObject && HasBoolean == Other.HasBoolean;
		}

		/// <summary>Hands the frozen capture to the pure classifier instead of a live re-read. The
		/// int value stays default because text authority is decided by presence, never by it.</summary>
		internal KingdomDurableKeyObservation Row()
		{
			return new KingdomDurableKeyObservation
			{
				HasString = HasString,
				String = String,
				HasInt = HasInt,
				HasInt64 = HasInt64,
				HasObject = HasObject,
				HasBoolean = HasBoolean
			};
		}
	}

	/// <summary>Frozen owner, key and evidence for one option observation. Nothing here is
	/// re-resolved at commit; the references are the ones the observation was taken from. The pure
	/// decision is not duplicated here: it is the snapshot's, and is read as Snapshot.Decision.</summary>
	internal sealed class KingdomSubsidenceOptionObservation
	{
		internal readonly XRLGame Game;
		internal readonly KingdomSystem System;
		internal readonly KingdomCityBook City;
		internal readonly string RealmId;
		internal readonly string SettlementId;
		internal readonly string Key;
		internal readonly long MasterToken;
		internal readonly KingdomSubsidenceOptionTables Tables;
		internal readonly KingdomSubsidenceOptionRules.Snapshot Snapshot;

		internal KingdomSubsidenceOptionObservation(XRLGame Game, KingdomSystem System,
			KingdomCityBook City, string RealmId, string SettlementId, string Key, long MasterToken,
			KingdomSubsidenceOptionTables Tables, KingdomSubsidenceOptionRules.Snapshot Snapshot)
		{
			this.Game = Game;
			this.System = System;
			this.City = City;
			this.RealmId = RealmId;
			this.SettlementId = SettlementId;
			this.Key = Key;
			this.MasterToken = MasterToken;
			this.Tables = Tables;
			this.Snapshot = Snapshot;
		}
	}

	// Observations are transient CAS expectations; restart authority lives in the step book.
	internal static partial class KingdomSubsidenceOptionRuntime
	{
		internal const string NoGame = "no live game can observe the subsidence option receipt";
		internal const string NoStringTable = "the game carries no string game-state table";
		internal const string NoIntTable = "the game carries no int game-state table";
		internal const string NoInt64Table = "the game carries no int64 game-state table";
		internal const string NoObjectTable = "the game carries no object game-state table";
		internal const string NoBooleanTable = "the game carries no boolean game-state table";
		internal const string InvalidOwner = "the subsidence option owner is not an exact seated settlement";
		internal const string TornTable = "the subsidence option receipt is torn or on a wrong table";
		internal const string TornRead = "the subsidence option receipt changed between two reads";
		internal const string InvalidDecision = "the subsidence option evidence admits no valid decision";
		internal const string OwnerChanged = "the subsidence option owner changed after observation";
		internal const string TableChanged = "the subsidence option tables changed after observation";
		internal const string ForeignBytes = "the subsidence option receipt holds foreign bytes that are never replaced";
		internal const string NothingToPublish = "the subsidence option decision carries no publishable receipt";
		internal const string TornWrite = "the subsidence option receipt did not retain its exact write";
		internal const string TornConfirm = "the subsidence option receipt did not confirm its exact write";
		internal const string MalformedObservation = "the subsidence option observation is missing its frozen evidence";

		/// <summary>
		/// Captures the exact game, system, seat book, realm/settlement identity, master token, key,
		/// five-table bytes and the snapshot the pure decision produced. Returns false with a fixed
		/// refusal for every expected absence; it never throws for one.
		/// </summary>
		internal static bool TryObserve(bool Enabled, long Now,
			out KingdomSubsidenceOptionObservation Observed, out string Refusal)
		{
			Observed = null;
			XRLGame game = The.Game;
			if (game == null) { Refusal = NoGame; return false; }
			if (!TryTables(game, out Refusal)) return false;
			KingdomSystem system = game.GetSystem<KingdomSystem>();
			KingdomCityBook city = system == null ? null : system.City;
			string realmId = system == null ? null : system.CurrentRealmId;
			string settlementId = KingdomChronicle.SettlementId(system);
			if (system == null || !system.Founded || city == null
				|| !KingdomIdentityRules.IsRealmId(realmId)
				|| !KingdomIdentityRules.IsSettlementId(settlementId)
				|| !string.Equals(city.SettlementId, settlementId, StringComparison.Ordinal))
			{ Refusal = InvalidOwner; return false; }
			string key = KingdomSubsidence.OptionStatePrefix + settlementId;
			KingdomSubsidenceOptionTables tables = new KingdomSubsidenceOptionTables(Observe(game, key));
			if (!tables.SameBytes(new KingdomSubsidenceOptionTables(Observe(game, key))))
			{ Refusal = TornRead; return false; }
			KingdomDurableKeyObservation row = tables.Row();
			if (!KingdomScenarioStateShape.TryAuthorityText(row, out _, out _, out string detail))
			{ Refusal = TornTable + " (" + detail + ")"; return false; }
			long token = system.MasterAppliedResumeToken;
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(row, Enabled,
				token, Now, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			if (!decision.Valid || snapshot == null) { Refusal = InvalidDecision; return false; }
			KingdomSubsidenceOptionObservation candidate = new KingdomSubsidenceOptionObservation(game,
				system, city, realmId, settlementId, key, token, tables, snapshot);
			// Re-proved after the table reads: a seat that moved while the five tables were being
			// read never leaves a usable observation behind.
			if (!ReprovesExact(candidate, out Refusal)) return false;
			Observed = candidate;
			Refusal = null;
			return true;
		}

		/// <summary>
		/// Single-writer compare-and-swap over the frozen observation. The exact game, system, seat
		/// and identity are reproved before the capture, again on it, and again after the write, so
		/// even an idempotent confirmation is owner-proved. Exactly one fresh five-table read decides
		/// the rest, and the pure rules classify what it holds: a value that is already the target
		/// confirms without writing, bytes identical to the prior are the observation's own evidence
		/// and the only ones CanPublish admits, and a racing writer is refused as foreign and never
		/// replaced. A torn write refuses and is deliberately left unrepaired.
		/// </summary>
		internal static bool TryPublish(KingdomSubsidenceOptionObservation Observed,
			out KingdomSubsidenceOptionPublication Result, out string Refusal)
		{
			Result = KingdomSubsidenceOptionPublication.Refused;
			if (Observed == null || Observed.Game == null || Observed.System == null
				|| Observed.City == null || Observed.Snapshot == null)
			{ Refusal = MalformedObservation; return false; }
			if (!ReprovesExact(Observed, out Refusal)) return false;
			XRLGame game = Observed.Game;
			string key = Observed.Key;
			if (string.IsNullOrEmpty(Observed.Snapshot.NextWire))
			{ Refusal = NothingToPublish; return false; }
			// One fresh capture feeds every decision below; no single-table read is ever weighed
			// against a stale one. The already-published target is proved first, and needs no
			// separate flag comparison: the rules admit the exact string table and no other.
			KingdomSubsidenceOptionTables current = new KingdomSubsidenceOptionTables(Observe(game, key));
			if (!ReprovesExact(Observed, out Refusal)) return false;
			if (KingdomSubsidenceOptionRules.ProvesPublished(Observed.Snapshot, current.Row()))
			{
				Result = KingdomSubsidenceOptionPublication.Confirmed;
				Refusal = null;
				return true;
			}
			// A differing string is not torn evidence; it is precisely what the rules classify. Only
			// the four non-string presences must still stand exactly as they were observed.
			if (!Observed.Tables.SameOtherTables(current)) { Refusal = TableChanged; return false; }
			// Bytes identical to the prior are the observation's own evidence and the only ones
			// CanPublish admits, so a writer that raced in afterwards is refused, never overwritten.
			if (!KingdomSubsidenceOptionRules.CanPublish(Observed.Snapshot, current.Row(), out string wire))
			{ Refusal = ForeignBytes; return false; }
			game.SetStringGameState(key, wire);
			if (!ReprovesExact(Observed, out string torn))
			{ Refusal = TornWrite + " (" + torn + ")"; return false; }
			KingdomSubsidenceOptionTables after = new KingdomSubsidenceOptionTables(Observe(game, key));
			if (!ReprovesExact(Observed, out torn))
			{ Refusal = TornWrite + " (" + torn + ")"; return false; }
			if (!Observed.Tables.SameOtherTables(after)
				|| !KingdomSubsidenceOptionRules.ProvesPublished(Observed.Snapshot, after.Row()))
			{ Refusal = TornConfirm; return false; }
			Result = KingdomSubsidenceOptionPublication.Published;
			Refusal = null;
			return true;
		}

		/// <summary>One key's presence across every durable table, and the string table's text.
		/// All five presences are taken first, so the single value fetch cannot sit between them.</summary>
		private static KingdomDurableKeyObservation Observe(XRLGame Game, string Key)
		{
			bool hasString = Game.HasStringGameState(Key);
			bool hasInt = Game.HasIntGameState(Key);
			bool hasInt64 = Game.HasInt64GameState(Key);
			bool hasObject = Game.HasObjectGameState(Key);
			bool hasBoolean = Game.HasBooleanGameState(Key);
			return new KingdomDurableKeyObservation
			{
				HasString = hasString,
				String = hasString ? Game.GetStringGameState(Key, null) : null,
				HasInt = hasInt,
				HasInt64 = hasInt64,
				HasObject = hasObject,
				HasBoolean = hasBoolean
			};
		}

		/// <summary>A missing table dictionary is not an absent key. Each refuses by name.</summary>
		private static bool TryTables(XRLGame Game, out string Refusal)
		{
			Refusal = null;
			if (Game.StringGameState == null) { Refusal = NoStringTable; return false; }
			if (Game.IntGameState == null) { Refusal = NoIntTable; return false; }
			if (Game.Int64GameState == null) { Refusal = NoInt64Table; return false; }
			if (Game.ObjectGameState == null) { Refusal = NoObjectTable; return false; }
			if (Game.BooleanGameState == null) { Refusal = NoBooleanTable; return false; }
			return true;
		}

		/// <summary>Re-proves the exact captured game, system, seat book, identity, token and key.
		/// Called before and after the write; it never re-resolves a new seated owner.</summary>
		private static bool ReprovesExact(KingdomSubsidenceOptionObservation Observed,
			out string Refusal)
		{
			if (!ReferenceEquals(The.Game, Observed.Game)) { Refusal = OwnerChanged; return false; }
			if (!TryTables(Observed.Game, out Refusal)) return false;
			KingdomSystem system = Observed.Game.GetSystem<KingdomSystem>();
			if (!ReferenceEquals(system, Observed.System)
				|| !ReferenceEquals(Observed.System.City, Observed.City)
				|| !Observed.System.Founded
				|| !string.Equals(Observed.System.CurrentRealmId, Observed.RealmId, StringComparison.Ordinal)
				|| !string.Equals(KingdomChronicle.SettlementId(Observed.System), Observed.SettlementId,
					StringComparison.Ordinal)
				|| !string.Equals(Observed.City.SettlementId, Observed.SettlementId, StringComparison.Ordinal)
				|| Observed.System.MasterAppliedResumeToken != Observed.MasterToken
				|| !string.Equals(KingdomSubsidence.OptionStatePrefix + Observed.SettlementId,
					Observed.Key, StringComparison.Ordinal))
			{ Refusal = OwnerChanged; return false; }
			Refusal = null;
			return true;
		}
	}
}
