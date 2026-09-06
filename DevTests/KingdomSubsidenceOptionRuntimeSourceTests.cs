#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// SOURCE-ONLY CONTRACTS. Every SourceContract assertion here reads repository TEXT and pins a
	/// wiring boundary. None of it executes the adapter, a live game, a real state table, a save, or
	/// a reload, so none of it is runtime, durability, restart-safety, or cancellation proof. The
	/// adapter's own header says the same: its snapshot is a transient publication expectation and
	/// never durable authority. Compiler, registration and executable coverage are the release
	/// root's, not this file's. The pure-rule cases at the end of the file DO execute, but they
	/// execute the engine-free rules alone: they prove what the adapter delegates, never that the
	/// adapter is wired, nor that any write ever reached a table.
	/// </summary>
	[TestFixture]
	public sealed class KingdomSubsidenceOptionRuntimeSourceTests
	{
		private const string Runtime = "Growth/KingdomSubsidenceOptionRuntime.cs";
		private const string Rules = "Growth/KingdomSubsidenceOptionRules.cs";
		private const string Publish = "internal static bool TryPublish(";
		private const string Observer = "internal static bool TryObserve(";

		[Test]
		public void SourceContractRecoveryUsesSavedBookAuthorityNotLiveOptionReconstruction()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomSubsidenceOptionRuntime.Recovery.cs");
			Has(Flat(source), "city.SubsidenceModel != stepWire", "book.RealmId != system.CurrentRealmId",
				"KingdomSubsidenceOptionIntentRules.TryDecode(book.OptionModel,",
				"KingdomSubsidenceOptionIntentRules.Matches(intent, book)",
				"KingdomSubsidenceOptionIntentRules.TrySnapshot(intent,",
				"KingdomSubsidenceOptionRules.CanPublish(snapshot, current,",
				"KingdomSubsidenceOptionRules.ProvesPublished(snapshot, current)");
			StringAssert.DoesNotContain("TryObserve(", source);
			StringAssert.DoesNotContain("Options.GetOption(", source);
		}

		[Test]
		public void SourceContractAdaptsThePureRulesAndNeverWiresTheOldOptionPath()
		{
			string source = Flat(TestMain.ReadRepositoryText(Runtime));
			Has(source, "KingdomSubsidenceOptionRules.Snapshot", "KingdomSubsidenceOptionRules.Observe(",
				"KingdomSubsidenceOptionRules.CanPublish(", "KingdomSubsidenceOptionRules.ProvesPublished(",
				"KingdomSubsidence.OptionStatePrefix");
			StringAssert.DoesNotContain("ObserveOption(", source);
			StringAssert.DoesNotContain("CommitOption(", source);
			// The pure contract the adapter is built on must still expose exactly these members.
			Has(Flat(TestMain.ReadRepositoryText(Rules)), "internal sealed class Snapshot",
				"internal static KingdomElapsedOptionDecision Observe(",
				"internal static bool CanPublish(", "internal static bool ProvesPublished(");
		}

		[Test]
		public void SourceContractObserverProvesEveryTableByPresenceNeverByADefaultingGetter()
		{
			string reader = Method(Runtime, "private static KingdomDurableKeyObservation Observe(XRLGame Game, string Key)");
			// All five presences are taken before the one value fetch; no Get may sit between them.
			Ordered(reader, "bool hasString = Game.HasStringGameState(Key);",
				"bool hasInt = Game.HasIntGameState(Key);",
				"bool hasInt64 = Game.HasInt64GameState(Key);",
				"bool hasObject = Game.HasObjectGameState(Key);",
				"bool hasBoolean = Game.HasBooleanGameState(Key);",
				"String = hasString ? Game.GetStringGameState(Key, null) : null,");
			Has(reader, "HasString = hasString,", "HasInt = hasInt,", "HasInt64 = hasInt64,",
				"HasObject = hasObject,", "HasBoolean = hasBoolean");
			Assert.AreEqual(1, Count(reader, "GetStringGameState("), "One value fetch, and only one.");
			string observer = Method(Runtime, Observer);
			int tables = observer.IndexOf("TryTables(game, out Refusal)", StringComparison.Ordinal);
			int first = observer.IndexOf("Observe(game, key)", StringComparison.Ordinal);
			Assert.GreaterOrEqual(tables, 0, "The observer must prove every table dictionary exists.");
			Assert.Greater(first, tables, "No key may be read before the tables are proved present.");
			string source = Flat(TestMain.ReadRepositoryText(Runtime));
			foreach (string banned in new[] { "GetIntGameState(", "GetInt64GameState(",
				"GetObjectGameState(", "GetBooleanGameState(", "GameState[", "TryGetValue(" })
				StringAssert.DoesNotContain(banned, source);
		}

		[Test]
		public void SourceContractRefusesNullGameEveryMissingTableInvalidOwnerAndTornTable()
		{
			string source = Flat(TestMain.ReadRepositoryText(Runtime));
			// Every fixed refusal the adapter can return, pinned literally.
			Has(source,
				"NoGame = \"no live game can observe the subsidence option receipt\";",
				"NoStringTable = \"the game carries no string game-state table\";",
				"NoIntTable = \"the game carries no int game-state table\";",
				"NoInt64Table = \"the game carries no int64 game-state table\";",
				"NoObjectTable = \"the game carries no object game-state table\";",
				"NoBooleanTable = \"the game carries no boolean game-state table\";",
				"InvalidOwner = \"the subsidence option owner is not an exact seated settlement\";",
				"TornTable = \"the subsidence option receipt is torn or on a wrong table\";",
				"TornRead = \"the subsidence option receipt changed between two reads\";",
				"InvalidDecision = \"the subsidence option evidence admits no valid decision\";",
				"OwnerChanged = \"the subsidence option owner changed after observation\";",
				"TableChanged = \"the subsidence option tables changed after observation\";",
				"ForeignBytes = \"the subsidence option receipt holds foreign bytes that are never replaced\";",
				"NothingToPublish = \"the subsidence option decision carries no publishable receipt\";",
				"TornWrite = \"the subsidence option receipt did not retain its exact write\";",
				"TornConfirm = \"the subsidence option receipt did not confirm its exact write\";",
				"MalformedObservation = \"the subsidence option observation is missing its frozen evidence\";");
			Ordered(Method(Runtime, "private static bool TryTables(XRLGame Game, out string Refusal)"),
				"if (Game.StringGameState == null) { Refusal = NoStringTable; return false; }",
				"if (Game.IntGameState == null) { Refusal = NoIntTable; return false; }",
				"if (Game.Int64GameState == null) { Refusal = NoInt64Table; return false; }",
				"if (Game.ObjectGameState == null) { Refusal = NoObjectTable; return false; }",
				"if (Game.BooleanGameState == null) { Refusal = NoBooleanTable; return false; }");
			string body = Method(Runtime, Observer);
			Ordered(body, "if (game == null) { Refusal = NoGame; return false; }",
				"if (!TryTables(game, out Refusal)) return false;",
				"{ Refusal = InvalidOwner; return false; }", "{ Refusal = TornRead; return false; }",
				"{ Refusal = TornTable + \" (\" + detail + \")\"; return false; }",
				"{ Refusal = InvalidDecision; return false; }",
				"if (!ReprovesExact(candidate, out Refusal)) return false;");
			// TryAuthorityText always sets Detail on a false return, so no fallback text is reachable.
			StringAssert.DoesNotContain("unknown fault", body);
		}

		[Test]
		public void SourceContractObserverFreezesExactGameSystemSeatIdentityKeyTokenAndSnapshot()
		{
			string body = Method(Runtime, Observer);
			Ordered(body, "XRLGame game = The.Game;", "KingdomSystem system = game.GetSystem<KingdomSystem>();",
				"KingdomCityBook city = system == null ? null : system.City;",
				"string realmId = system == null ? null : system.CurrentRealmId;",
				"string settlementId = KingdomChronicle.SettlementId(system);",
				"!KingdomIdentityRules.IsRealmId(realmId)",
				"!KingdomIdentityRules.IsSettlementId(settlementId)",
				"!string.Equals(city.SettlementId, settlementId, StringComparison.Ordinal)",
				"string key = KingdomSubsidence.OptionStatePrefix + settlementId;",
				"long token = system.MasterAppliedResumeToken;",
				"KingdomSubsidenceOptionRules.Observe(row, Enabled, token, Now, out KingdomSubsidenceOptionRules.Snapshot snapshot)",
				"KingdomSubsidenceOptionObservation candidate = new KingdomSubsidenceOptionObservation(game, system, city, realmId, settlementId, key, token, tables, snapshot);",
				"if (!ReprovesExact(candidate, out Refusal)) return false;", "Observed = candidate;");
			// The torn-table cut is a genuine double read of the same key, not a single read.
			Has(body, "KingdomSubsidenceOptionTables tables = new KingdomSubsidenceOptionTables(Observe(game, key));",
				"if (!tables.SameBytes(new KingdomSubsidenceOptionTables(Observe(game, key))))");
			// The frozen owner is re-proved AFTER the table reads, never only before them.
			int capture = body.IndexOf("KingdomSubsidenceOptionTables tables = new", StringComparison.Ordinal);
			int reprove = body.IndexOf("ReprovesExact(", StringComparison.Ordinal);
			Assert.GreaterOrEqual(capture, 0, "The observer must capture the five tables.");
			Assert.Greater(reprove, capture, "The captured owner must be re-proved after the reads.");
			string source = Flat(TestMain.ReadRepositoryText(Runtime));
			Has(source,
				"internal readonly struct KingdomSubsidenceOptionTables", "internal readonly bool HasString;",
				"internal readonly bool HasInt;", "internal readonly bool HasInt64;",
				"internal readonly bool HasObject;", "internal readonly bool HasBoolean;",
				"internal readonly XRLGame Game;", "internal readonly KingdomSystem System;",
				"internal readonly KingdomCityBook City;", "internal readonly string RealmId;",
				"internal readonly string SettlementId;", "internal readonly string Key;",
				"internal readonly long MasterToken;",
				"internal readonly KingdomSubsidenceOptionRules.Snapshot Snapshot;");
			// The decision is the snapshot's; the observation never carries a second copy of it.
			StringAssert.DoesNotContain("internal readonly KingdomElapsedOptionDecision Decision;", source);
		}

		[Test]
		public void SourceContractPublishReadsBeforeItWritesAndReprovesOnBothSidesOfTheWrite()
		{
			string body = Method(Runtime, Publish);
			Ordered(body,
				"if (Observed == null || Observed.Game == null || Observed.System == null || Observed.City == null || Observed.Snapshot == null) { Refusal = MalformedObservation; return false; }",
				"if (!ReprovesExact(Observed, out Refusal)) return false;",
				"if (string.IsNullOrEmpty(Observed.Snapshot.NextWire)) { Refusal = NothingToPublish; return false; }",
				"KingdomSubsidenceOptionTables current = new KingdomSubsidenceOptionTables(Observe(game, key));",
				"if (!ReprovesExact(Observed, out Refusal)) return false;",
				"if (KingdomSubsidenceOptionRules.ProvesPublished(Observed.Snapshot, current.Row()))",
				"Result = KingdomSubsidenceOptionPublication.Confirmed;",
				"if (!Observed.Tables.SameOtherTables(current)) { Refusal = TableChanged; return false; }",
				"if (!KingdomSubsidenceOptionRules.CanPublish(Observed.Snapshot, current.Row(), out string wire))",
				"{ Refusal = ForeignBytes; return false; }", "game.SetStringGameState(key, wire);",
				"if (!ReprovesExact(Observed, out string torn))",
				"{ Refusal = TornWrite + \" (\" + torn + \")\"; return false; }",
				"KingdomSubsidenceOptionTables after = new KingdomSubsidenceOptionTables(Observe(game, key));",
				"if (!ReprovesExact(Observed, out torn))",
				"{ Refusal = TornWrite + \" (\" + torn + \")\"; return false; }",
				"!Observed.Tables.SameOtherTables(after)",
				"!KingdomSubsidenceOptionRules.ProvesPublished(Observed.Snapshot, after.Row())",
				"{ Refusal = TornConfirm; return false; }",
				"Result = KingdomSubsidenceOptionPublication.Published;");
			int capture = body.IndexOf("KingdomSubsidenceOptionTables current =", StringComparison.Ordinal);
			int write = body.IndexOf("game.SetStringGameState(", StringComparison.Ordinal);
			Assert.GreaterOrEqual(capture, 0, "Publish must capture the five tables before it decides.");
			Assert.Greater(write, capture, "Publish must read before it writes.");
			// One capture before the write and one after it; publish never reads a single table.
			Assert.AreEqual(2, Count(body, "Observe(game, key)"), "Exactly two five-table captures.");
			StringAssert.DoesNotContain("GetStringGameState(", body);
			int firstReprove = body.IndexOf("ReprovesExact(", StringComparison.Ordinal);
			int lastReprove = body.LastIndexOf("ReprovesExact(", StringComparison.Ordinal);
			Assert.GreaterOrEqual(firstReprove, 0, "A re-prove must open the publication.");
			Assert.Less(firstReprove, write, "A re-prove must precede the write.");
			Assert.Greater(lastReprove, write, "A re-prove must follow the write.");
			Assert.Greater(lastReprove,
				body.IndexOf("KingdomSubsidenceOptionTables after =", StringComparison.Ordinal),
				"Final capture must be owner-proved before success.");
			string source = Flat(TestMain.ReadRepositoryText(Runtime));
			Assert.AreEqual(1, Count(source, "SetStringGameState("),
				"Exactly one single-writer publication call may exist.");
			foreach (string banned in new[] { "RemoveStringGameState(", "AppendStringGameState(" })
				StringAssert.DoesNotContain(banned, source);
		}

		[Test]
		public void SourceContractPublishConfirmsIdempotentlyAndNeverReplacesForeignBytes()
		{
			string body = Method(Runtime, Publish);
			int capture = body.IndexOf("KingdomSubsidenceOptionTables current =", StringComparison.Ordinal);
			int write = body.IndexOf("game.SetStringGameState(", StringComparison.Ordinal);
			Assert.GreaterOrEqual(capture, 0, "Publish must capture the five tables.");
			Assert.Greater(write, capture, "The write must follow that capture.");
			string decision = body.Substring(capture, write - capture);
			Has(decision, "if (!ReprovesExact(Observed, out Refusal)) return false;",
				"if (KingdomSubsidenceOptionRules.ProvesPublished(Observed.Snapshot, current.Row())) { Result = KingdomSubsidenceOptionPublication.Confirmed; Refusal = null; return true; }",
				"if (!Observed.Tables.SameOtherTables(current)) { Refusal = TableChanged; return false; }",
				"if (!KingdomSubsidenceOptionRules.CanPublish(Observed.Snapshot, current.Row(), out string wire)) { Refusal = ForeignBytes; return false; }");
			StringAssert.DoesNotContain("SetStringGameState(", decision);
			// The rules own the classification: no hand-rolled prior or empty-string test survives.
			foreach (string banned in new[] { "IsNullOrEmpty(published)", "string published",
				"Snapshot.PriorWire", "SameBytes(" })
				StringAssert.DoesNotContain(banned, body);
			// The already-published target is proved first, on the one fresh capture, owner-proved.
			int proves = body.IndexOf("KingdomSubsidenceOptionRules.ProvesPublished(", StringComparison.Ordinal);
			int flags = body.IndexOf("SameOtherTables(", StringComparison.Ordinal);
			int admits = body.IndexOf("KingdomSubsidenceOptionRules.CanPublish(", StringComparison.Ordinal);
			int confirmed = body.IndexOf("Result = KingdomSubsidenceOptionPublication.Confirmed;",
				StringComparison.Ordinal);
			int reprove = body.IndexOf("ReprovesExact(", capture, StringComparison.Ordinal);
			Assert.GreaterOrEqual(confirmed, 0, "Publish must be able to confirm without writing.");
			Assert.Greater(proves, capture, "The target is proved on the fresh capture.");
			Assert.Greater(flags, proves, "No table comparison may precede that confirmation.");
			Assert.Greater(admits, proves, "The publish admission may not precede that confirmation.");
			Assert.Greater(reprove, capture, "The owner is re-proved on the capture...");
			Assert.Less(reprove, confirmed, "...and before an idempotent confirmation returns.");
		}

		[Test]
		public void SourceContractReproveNeverResolvesANewSeatedOwnerAfterObservation()
		{
			string body = Method(Runtime, "private static bool ReprovesExact(");
			Has(body, "if (!ReferenceEquals(The.Game, Observed.Game)) { Refusal = OwnerChanged; return false; }",
				"if (!TryTables(Observed.Game, out Refusal)) return false;",
				"KingdomSystem system = Observed.Game.GetSystem<KingdomSystem>();",
				"!ReferenceEquals(system, Observed.System)",
				"!ReferenceEquals(Observed.System.City, Observed.City)",
				"!string.Equals(Observed.System.CurrentRealmId, Observed.RealmId, StringComparison.Ordinal)",
				"!string.Equals(KingdomChronicle.SettlementId(Observed.System), Observed.SettlementId, StringComparison.Ordinal)",
				"!string.Equals(Observed.City.SettlementId, Observed.SettlementId, StringComparison.Ordinal)",
				"Observed.System.MasterAppliedResumeToken != Observed.MasterToken",
				"!string.Equals(KingdomSubsidence.OptionStatePrefix + Observed.SettlementId, Observed.Key, StringComparison.Ordinal)",
				"{ Refusal = OwnerChanged; return false; }");
		}

		[Test]
		public void SourceContractRuntimeMintsNoIdentityBuildsNoBookAndDestroysNothing()
		{
			string source = Flat(TestMain.ReadRepositoryText(Runtime));
			foreach (string banned in new[] { ".ID", "IDIfAssigned", "GameID", "new KingdomCityBook",
				"new KingdomSystem", "new KingdomSettlement", "Obliterate", "Destroy",
				"RequireSystem<", "TryAddNonSeatSettlement", "ProjectCompatibility" })
				StringAssert.DoesNotContain(banned, source);
			// The seat book is read and compared, never assigned through or rebuilt.
			Has(Method(Runtime, Observer), "system.City;");
			// The trailing space bans an assignment without banning a "== null" guard.
			foreach (string assignment in new[] { "system.City = ", "city.SettlementId = ",
				"Observed.City = ", "Observed.System = ", ".SubsidenceModel = " })
				StringAssert.DoesNotContain(assignment, source);
			Has(source, "internal static partial class KingdomSubsidenceOptionRuntime");
		}

		// EXECUTABLE PURE-RULE CASES (rules only; the runtime adapter is source-pinned, not executed)

		private const string PriorWire = "v1|E|100|2";
		private const string TargetWire = "v1|D|101|2";
		private const string ForeignWire = "v1|E|100|3";

		/// <summary>One frozen row: present under the string table alone, or absent when null.</summary>
		private static KingdomDurableKeyObservation Row(string wire)
		{
			return new KingdomDurableKeyObservation { HasString = wire != null, String = wire };
		}

		/// <summary>The disable transition the adapter publishes: prior bytes in, next bytes out.</summary>
		private static KingdomSubsidenceOptionRules.Snapshot DisableSnapshot()
		{
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(Row(PriorWire),
				false, 2L, 101L, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			Assert.IsTrue(decision.Valid, "The disable observation must be a valid decision.");
			Assert.AreEqual(PriorWire, snapshot.PriorWire);
			Assert.AreEqual(TargetWire, snapshot.NextWire);
			return snapshot;
		}

		[Test]
		public void PureRulesProveTheExactTargetAndNeverTheObservedPriorBytes()
		{
			KingdomSubsidenceOptionRules.Snapshot snapshot = DisableSnapshot();
			Assert.IsTrue(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, Row(TargetWire)),
				"The exact target proves an idempotent publication.");
			Assert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, Row(PriorWire)),
				"The observed prior is evidence to publish from, never proof of publication.");
		}

		[Test]
		public void PureRulesAdmitOnlyThePriorBytesAndRefuseTargetAndForeignAlike()
		{
			KingdomSubsidenceOptionRules.Snapshot snapshot = DisableSnapshot();
			Assert.IsTrue(KingdomSubsidenceOptionRules.CanPublish(snapshot, Row(PriorWire), out string wire));
			Assert.AreEqual(snapshot.NextWire, wire);
			Assert.AreEqual(TargetWire, wire);
			// A racing writer's well-formed record is foreign, not a licence to overwrite.
			Assert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(snapshot, Row(ForeignWire),
				out string foreign), "Foreign bytes never admit a write.");
			Assert.IsNull(foreign);
			// The idempotent path is ProvesPublished; the target is never rewritten over itself.
			Assert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(snapshot, Row(TargetWire),
				out string republished), "The already-published target never admits a rewrite.");
			Assert.IsNull(republished);
		}

		[Test]
		public void PureRulesRefuseAnExplicitlyStoredEmptyStringAndAnAbsentKeyOnBothSides()
		{
			KingdomSubsidenceOptionRules.Snapshot snapshot = DisableSnapshot();
			Assert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(snapshot, Row(""), out string stored),
				"A stored empty string is present and unreadable, never a fresh key.");
			Assert.IsNull(stored);
			Assert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, Row("")));
			Assert.IsFalse(KingdomSubsidenceOptionRules.CanPublish(snapshot, Row(null), out string absent),
				"An absent key no longer matches an observation taken from a present one.");
			Assert.IsNull(absent);
			Assert.IsFalse(KingdomSubsidenceOptionRules.ProvesPublished(snapshot, Row(null)));
		}

		private static int Count(string source, string needle)
		{
			int total = 0;
			for (int i = source.IndexOf(needle, StringComparison.Ordinal); i >= 0;
				i = source.IndexOf(needle, i + 1, StringComparison.Ordinal)) total++;
			return total;
		}
		private static string Flat(string source) => System.Text.RegularExpressions.Regex.Replace(source, @"\s+", " ");
		private static void Has(string source, params string[] needles)
		{ foreach (string needle in needles) StringAssert.Contains(needle, source); }
		private static void Ordered(string source, params string[] needles)
		{
			int previous = -1;
			foreach (string needle in needles)
			{
				int current = source.IndexOf(needle, previous + 1, StringComparison.Ordinal);
				Assert.That(current, Is.GreaterThan(previous), needle);
				previous = current;
			}
		}
		private static string Method(string path, string signature)
		{
			string source = Flat(TestMain.ReadRepositoryText(path));
			int start = source.IndexOf(Flat(signature), StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), path + ": " + signature);
			int open = source.IndexOf('{', start), depth = 0;
			Assert.GreaterOrEqual(open, 0, "Unopened source method: " + path + ": " + signature);
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return Flat(source.Substring(start, i - start + 1));
			}
			Assert.Fail("Unclosed source method: " + path + ": " + signature);
			return null;
		}
	}
}
#endif
