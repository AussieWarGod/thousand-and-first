#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The covenant archive's own rules and its wire, exercised without a game running.
	/// <para>
	/// Every case here is about one question: can a record of a completed rite be forged, lost,
	/// silently reinterpreted, or quietly counted twice. The archive is the only evidence a village
	/// covenant ever happened, so each of those four is a way for a founder's history to become
	/// something nobody can appeal.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomVillageCovenantTests
	{
		internal const string Realm =
			"taf:realm:v1:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
		internal const string OtherRealm =
			"taf:realm:v1:fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
		internal const string Transaction = "0123456789abcdef0123456789abcdef";
		internal const string OtherTransaction = "fedcba9876543210fedcba9876543210";
		internal const string Nonce = "abcdefabcdefabcdefabcdefabcdefab";
		internal const string Digest =
			"11223344556677889900aabbccddeeff11223344556677889900aabbccddeeff";
		internal const string Zone = "JoppaWorld.53.3.1.1.10";
		internal const string OtherZone = "JoppaWorld.53.3.1.1.11";
		internal const string FactionId = "Joppa";
		internal const string Display = "the villagers of Joppa";
		internal const int Sealed = 600;
		internal const long Tick = 4321L;

		internal static string Event(string transaction)
		{
			return KingdomVillageCovenantRules.ChronicleEvent(transaction);
		}

		internal static string Authority(string transaction, string realm, string zone)
		{
			return KingdomFoundingTransactionRules.FormatAuthority(new KingdomFoundingAuthority
			{
				Kind = KingdomFoundingKind.VillageCharter,
				OwnerKind = KingdomFoundingOwnerKind.Basin,
				TransactionID = transaction,
				OwnerNonce = Nonce,
				RealmFaction = realm,
				ZoneID = zone,
				RiteX = 12,
				RiteY = 34,
				PayloadDigest = Digest
			});
		}

		internal static KingdomVillageCovenantReceipt Row(string transaction = Transaction,
			string factionId = FactionId, string display = Display, string zone = Zone,
			string realm = Realm, int sealedStanding = Sealed, long tick = Tick)
		{
			return KingdomVillageCovenantRules.Receipt(realm, transaction,
				Authority(transaction, realm, zone), factionId, display, zone,
				Event(transaction), sealedStanding, tick);
		}

		internal static KingdomVillageCovenantArchive Bound(string realm = Realm)
		{
			KingdomVillageCovenantArchive archive = new KingdomVillageCovenantArchive();
			Assert.IsTrue(KingdomVillageCovenantRules.TryBindEmptyIdentity(archive, realm,
				out string failure), failure);
			return archive;
		}

		internal static KingdomVillageCovenantArchive With(params
			KingdomVillageCovenantReceipt[] rows)
		{
			KingdomVillageCovenantArchive archive = Bound();
			for (int i = 0; i < rows.Length; i++)
			{
				Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(archive, rows[i], Realm,
					out archive, out KingdomVillageCovenantAppend outcome, out _, out string failure),
					failure);
				Assert.AreEqual(KingdomVillageCovenantAppend.Recorded, outcome);
			}
			return archive;
		}

		// ---- the row itself -------------------------------------------------------------

		[Test]
		public void AWellFormedCovenantValidatesAndNamesItself()
		{
			KingdomVillageCovenantReceipt row = Row();
			Assert.IsTrue(KingdomVillageCovenantRules.TryValidateRow(row, out string failure),
				failure);
			StringAssert.StartsWith(KingdomVillageCovenantRules.ReceiptPrefix, row.ReceiptId);
			Assert.AreEqual(KingdomVillageCovenantRules.ReceiptPrefix.Length + 64,
				row.ReceiptId.Length);
			Assert.AreEqual(row.ReceiptId, KingdomVillageCovenantRules.ReceiptId(row));
		}

		[Test]
		public void ARenamedCovenantIsRefusedBecauseItsNameNoLongerDescribesIt()
		{
			KingdomVillageCovenantReceipt row = Row();
			row.VillageDisplayName = "somewhere else entirely";
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("receipt id does not name", failure);
		}

		[Test]
		public void ACovenantWearingAnotherCovenantsNameIsRefused()
		{
			KingdomVillageCovenantReceipt row = Row();
			row.ReceiptId = Row(OtherTransaction).ReceiptId;
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("receipt id does not name", failure);
		}

		[TestCase("0123456789ABCDEF0123456789ABCDEF", TestName = "upper case hex")]
		[TestCase("0123456789abcdef0123456789abcde", TestName = "one digit short")]
		[TestCase("0123456789abcdef0123456789abcdefa", TestName = "one digit long")]
		[TestCase(" 0123456789abcdef0123456789abcde", TestName = "leading space")]
		[TestCase("", TestName = "empty")]
		public void ATransactionThatIsNotCanonicalIsRefused(string transaction)
		{
			KingdomVillageCovenantReceipt row = Row();
			row.TransactionId = transaction;
			row.ReceiptId = KingdomVillageCovenantRules.ReceiptId(row);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("founding transaction is not canonical", failure);
		}

		[TestCase("JoppaWorld.053.3.1.1.10", TestName = "leading zero")]
		[TestCase("JoppaWorld.+53.3.1.1.10", TestName = "leading plus")]
		[TestCase("JoppaWorld.-53.3.1.1.10", TestName = "leading minus")]
		[TestCase("JoppaWorld.53.3.1.1.10 ", TestName = "trailing space")]
		[TestCase("JoppaWorld.53.3.1.1.10.extra", TestName = "trailing suffix")]
		[TestCase("JoppaWorld.53.3.3.1.10", TestName = "zone x past the grid")]
		[TestCase("JoppaWorld.53.3.1.1.50", TestName = "stratum past the layers")]
		[TestCase("JoppaWorld.84.3.1.1.10", TestName = "parasang past the plotted map")]
		[TestCase("the salt dunes", TestName = "prose")]
		public void ASiteLocatorThatIsNotCanonicalIsRefused(string zone)
		{
			KingdomVillageCovenantReceipt row = Row();
			row.SiteZoneId = zone;
			row.ReceiptId = KingdomVillageCovenantRules.ReceiptId(row);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("site locator is not the canonical name", failure);
		}

		/// <summary>
		/// A lone surrogate is an ordinary <c>char</c> and an impossible piece of text: strict UTF-8
		/// will not encode it. It is spelled in the method body rather than in a case attribute,
		/// because an unpaired surrogate in an attribute argument folds to U+FFFD before the test
		/// ever sees it and the case would then quietly prove nothing.
		/// </summary>
		[Test]
		public void AnUnpairedSurrogateInAnyNameIsRefusedAtTheDoor()
		{
			string lonely = "Joppa\ud800";
			KingdomVillageCovenantReceipt faction = Row(factionId: lonely);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(faction, out string first));
			StringAssert.Contains("village faction key is unusable", first);
			KingdomVillageCovenantReceipt display = Row(display: lonely);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(display, out string second));
			StringAssert.Contains("display-name snapshot is unusable", second);
		}

		/// <summary>
		/// Every hostile character here is spelled as an escape rather than typed. A raw C1 control
		/// in C# source is eaten by the compiler before this file's intent survives to be read, and a
		/// raw format character is invisible to the next person who opens the file &mdash; which is
		/// the very property that makes it worth refusing inside a faction key.
		/// </summary>
		[Test]
		public void AControlOrFormatCharacterInsideANameIsRefused()
		{
			// BEL, NEL (a C1 control), the zero-width joiner and the right-to-left override. The last
			// two let two faction keys that read identically compare unequal.
			string[] hostile = { "Jop\u0007pa", "Jop\u0085pa", "Jop\u200dpa", "Jop\u202epa" };
			for (int i = 0; i < hostile.Length; i++)
			{
				KingdomVillageCovenantReceipt row = Row(factionId: hostile[i]);
				Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure),
					"a key carrying U+" + ((int)hostile[i][3]).ToString("X4") + " must be refused");
				StringAssert.Contains("village faction key is unusable", failure);
			}
		}

		[Test]
		public void TheFoundingAuthorityMustDecodeExactlyAndNameThisVeryRite()
		{
			KingdomVillageCovenantReceipt wrongTransaction = Row();
			wrongTransaction.FoundingAuthority = Authority(OtherTransaction, Realm, Zone);
			wrongTransaction.ReceiptId = KingdomVillageCovenantRules.ReceiptId(wrongTransaction);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(wrongTransaction,
				out string first));
			StringAssert.Contains("names another transaction", first);

			KingdomVillageCovenantReceipt wrongSite = Row();
			wrongSite.FoundingAuthority = Authority(Transaction, Realm, OtherZone);
			wrongSite.ReceiptId = KingdomVillageCovenantRules.ReceiptId(wrongSite);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(wrongSite, out string second));
			StringAssert.Contains("names another site", second);

			KingdomVillageCovenantReceipt notACharter = Row();
			notACharter.FoundingAuthority =
				KingdomFoundingTransactionRules.FormatAuthority(new KingdomFoundingAuthority
				{
					Kind = KingdomFoundingKind.SecondCity,
					OwnerKind = KingdomFoundingOwnerKind.Basin,
					TransactionID = Transaction,
					OwnerNonce = Nonce,
					RealmFaction = Realm,
					ZoneID = Zone,
					RiteX = 12,
					RiteY = 34,
					PayloadDigest = Digest
				});
			notACharter.ReceiptId = KingdomVillageCovenantRules.ReceiptId(notACharter);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(notACharter,
				out string third));
			StringAssert.Contains("not a village charter", third);
		}

		/// <summary>
		/// The event id is not merely well formed, it is this rite's own. A grammar check would
		/// accept a second city's founding event, a different lane of this very rite, or another
		/// transaction's entry entirely &mdash; each of which would point the chronicle at
		/// something that happened somewhere else.
		/// </summary>
		[TestCase("taf:founding:v1:3:0123456789abcdef0123456789abcdef:CHRONICLE",
			TestName = "upper case lane")]
		[TestCase("taf:founding:v1:3:0123456789abcdef0123456789abcdef:claim",
			TestName = "another lane of this rite")]
		[TestCase("taf:founding:v1:2:0123456789abcdef0123456789abcdef:chronicle",
			TestName = "a second city's kind")]
		[TestCase("taf:founding:v1:3:fedcba9876543210fedcba9876543210:chronicle",
			TestName = "another transaction")]
		[TestCase("taf:founding:v2:3:0123456789abcdef0123456789abcdef:chronicle",
			TestName = "another prefix")]
		[TestCase("taf:founding:v1:3:0123456789abcdef0123456789abcdef:",
			TestName = "empty lane")]
		public void AChronicleEventIdThatIsNotThisRitesOwnIsRefused(string identifier)
		{
			KingdomVillageCovenantReceipt row = Row();
			row.ChronicleEventId = identifier;
			row.ReceiptId = KingdomVillageCovenantRules.ReceiptId(row);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("chronicle event id is not this rite's own", failure);
			Assert.AreEqual("taf:founding:v1:3:" + Transaction + ":chronicle",
				KingdomVillageCovenantRules.ChronicleEvent(Transaction));
		}

		/// <summary>
		/// Revision 1 owns its own floor. Any positive number would let a row claiming a standing
		/// of one read as a completed covenant, and a completed covenant is a claim about a rite
		/// that was paid for.
		/// </summary>
		[TestCase(0, TestName = "no standing at all")]
		[TestCase(-600, TestName = "a hostile standing")]
		[TestCase(1, TestName = "a token standing")]
		[TestCase(599, TestName = "one short of the floor")]
		public void ASealedStandingBelowAnythingARiteCouldSealIsRefused(int standing)
		{
			KingdomVillageCovenantReceipt row = Row(sealedStanding: standing);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("sealed standing is below", failure);
		}

		/// <summary>
		/// The floor is frozen and there is no ceiling. A realm that has spent a long time being
		/// generous can legitimately stand higher than any number worth writing down, and refusing
		/// its covenant for that would be refusing a real rite for being well liked.
		/// </summary>
		[TestCase(600, TestName = "exactly at the floor")]
		[TestCase(100001, TestName = "past a hundred thousand")]
		[TestCase(int.MaxValue, TestName = "as high as the field goes")]
		public void AnySealedStandingAtOrAboveTheFrozenFloorIsAccepted(int standing)
		{
			Assert.AreEqual(600, KingdomVillageCovenantRules.MinimumSealedStandingV1);
			KingdomVillageCovenantReceipt row = Row(sealedStanding: standing);
			Assert.IsTrue(KingdomVillageCovenantRules.TryValidateRow(row, out string failure),
				failure);
			Assert.AreEqual(standing, row.Copy().SealedStanding);
		}

		[Test]
		public void AReservationTickBeforeTheWorldBeganIsRefused()
		{
			KingdomVillageCovenantReceipt row = Row(tick: -1L);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("before the world began", failure);
		}

		/// <summary>
		/// The realm is frozen in the row and in the digest over it, so a covenant cannot be
		/// re-filed under another realm by editing one field.
		/// </summary>
		[Test]
		public void ACovenantFreezesTheRealmItBelongsTo()
		{
			KingdomVillageCovenantReceipt row = Row();
			Assert.AreEqual(Realm, row.RealmId);
			row.RealmId = OtherRealm;
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string moved));
			StringAssert.Contains("receipt id does not name", moved,
				"the realm is inside the digest, so moving it breaks the row's own name");
			Assert.AreNotEqual(Row().ReceiptId, Row(realm: OtherRealm).ReceiptId,
				"two realms sealing the same rite are two different covenants");
		}

		/// <summary>
		/// A realm has two true names, and a migrated save is where they part company.
		/// <para>
		/// The founding authority freezes the engine faction key the realm was registered under; a
		/// save carried through the immutable-identity migration mints a fresh canonical realm id
		/// and keeps the faction key it already had. Requiring the row to find those two equal
		/// would make a covenant impossible on exactly the saves that have been played longest, so
		/// the row freezes both and compares neither &mdash; the living game is asked instead.
		/// </para>
		/// </summary>
		[Test]
		public void ARealmWhoseFactionKeyIsNotItsRealmIdCanStillSealACovenant()
		{
			const string legacyKey = "Kavvat";
			KingdomVillageCovenantReceipt row = KingdomVillageCovenantRules.Receipt(Realm,
				Transaction, Authority(Transaction, legacyKey, Zone), FactionId, Display, Zone,
				Event(Transaction), Sealed, Tick);
			Assert.IsTrue(KingdomVillageCovenantRules.TryValidateRow(row, out string failure),
				failure);
			Assert.AreEqual(Realm, row.RealmId);
			Assert.IsTrue(KingdomFoundingTransactionRules.TryParseAuthority(row.FoundingAuthority,
				out KingdomFoundingAuthority parsed));
			Assert.AreEqual(legacyKey, parsed.RealmFaction);
			Assert.AreNotEqual(row.RealmId, parsed.RealmFaction,
				"this is the migrated shape: two valid identities that differ");

			KingdomVillageCovenantArchive archive = With(row);
			Assert.AreEqual(1, archive.Rows.Count);
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(archive, out byte[] bytes,
				out string encode), encode);
			Assert.AreEqual(KingdomVillageCovenantState.Compatible,
				KingdomVillageCovenantCodec.Decode(bytes).State);
		}

		[TestCase("taf:realm:v1:nope", TestName = "too short")]
		[TestCase("taf:realm:v2:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
			TestName = "another prefix")]
		[TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
			TestName = "no prefix")]
		public void ARealmIdThatIsNotCanonicalIsRefused(string realm)
		{
			KingdomVillageCovenantReceipt row = Row();
			row.RealmId = realm;
			row.ReceiptId = KingdomVillageCovenantRules.ReceiptId(row);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("realm id is not canonical", failure);
		}

		/// <summary>
		/// Only the basin that poured can seal a covenant. Any other owner path reaching this row
		/// would mean a rite nobody stood over.
		/// </summary>
		[Test]
		public void ACovenantSealedByAnythingButTheBasinIsRefused()
		{
			KingdomVillageCovenantReceipt row = Row();
			row.FoundingAuthority = KingdomFoundingTransactionRules.FormatAuthority(
				new KingdomFoundingAuthority
				{
					Kind = KingdomFoundingKind.VillageCharter,
					OwnerKind = KingdomFoundingOwnerKind.Direct,
					TransactionID = Transaction,
					OwnerNonce = Nonce,
					RealmFaction = Realm,
					ZoneID = Zone,
					RiteX = 12,
					RiteY = 34,
					PayloadDigest = Digest
				});
			row.ReceiptId = KingdomVillageCovenantRules.ReceiptId(row);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("not owned by the basin that poured", failure);
		}

		/// <summary>
		/// A name is bounded twice, and both bounds bite. The founding contract counts characters
		/// and the wire counts bytes, so a name lawful under one and not the other would be lawful
		/// to charter and unlawful to record &mdash; discovered, without both checks, only after
		/// the founder had paid.
		/// </summary>
		[Test]
		public void ANameIsHeldToBothTheCharacterAndTheByteBound()
		{
			string tooManyCharacters = new string('w', KingdomVillageCovenantRules.MaxNameChars + 1);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(
				Row(factionId: tooManyCharacters), out string characters));
			StringAssert.Contains("village faction key is unusable", characters);

			// Two hundred and sixty-two three-byte characters is inside no bound at all; two
			// hundred and fifty-six of them is inside the character bound and exactly on the byte
			// one, which is the pair the row was sized for.
			string wideButLawful = new string('\u4e00', KingdomVillageCovenantRules.MaxNameChars);
			Assert.AreEqual(KingdomVillageCovenantRules.MaxFactionIdBytes,
				new System.Text.UTF8Encoding(false, true).GetByteCount(wideButLawful));
			Assert.IsTrue(KingdomVillageCovenantRules.TryValidateRow(
				Row(factionId: wideButLawful, display: wideButLawful), out string lawful), lawful);
		}

		/// <summary>
		/// Copying a covenant carries every field it has. This is not a style point: a copy that
		/// dropped one would hand a valid row to an archive as an invalid one, and the archive
		/// would refuse a covenant the founder had already sealed.
		/// </summary>
		[Test]
		public void CopyingACovenantCarriesEveryFieldAndSharesNothing()
		{
			KingdomVillageCovenantReceipt row = Row();
			KingdomVillageCovenantReceipt copy = row.Copy();
			Assert.AreNotSame(row, copy);
			Assert.IsTrue(KingdomVillageCovenantRules.Same(row, copy));
			Assert.IsTrue(KingdomVillageCovenantRules.TryValidateRow(copy, out string failure),
				failure);
			row.VillageDisplayName = "edited after the copy";
			Assert.AreEqual(Display, copy.VillageDisplayName);
		}

		/// <summary>
		/// The tripwire for the mistake above: every field a covenant carries has to be named in
		/// its own copy. Adding a field and forgetting the copy is a silent corruption, so the
		/// count is checked by reflection rather than by remembering.
		/// </summary>
		[Test]
		public void EveryFieldOfACovenantIsNamedInItsOwnCopy()
		{
			System.Reflection.FieldInfo[] fields =
				typeof(KingdomVillageCovenantReceipt).GetFields(
					System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.NonPublic
					| System.Reflection.BindingFlags.Instance);
			string source = TestMain.ReadRepositoryText("Core/KingdomVillageCovenantModels.cs");
			int start = source.IndexOf("public KingdomVillageCovenantReceipt Copy()",
				System.StringComparison.Ordinal);
			int end = source.IndexOf("public sealed class KingdomVillageCovenantArchive", start,
				System.StringComparison.Ordinal);
			Assert.Greater(start, -1);
			Assert.Greater(end, start);
			string copy = source.Substring(start, end - start);
			Assert.AreEqual(11, fields.Length,
				"a covenant freezes eleven fields; changing that changes the wire and the digest");
			for (int i = 0; i < fields.Length; i++)
				StringAssert.Contains(fields[i].Name + " = " + fields[i].Name, copy,
					"Copy() must carry " + fields[i].Name);
		}

		/// <summary>
		/// The engine cut for a migrated realm: the row's realm id and the realm's engine faction
		/// key are two different lawful strings, and the covenant still works. The authority is
		/// judged against the key the living game supplies, never against the row's realm id.
		/// </summary>
		[Test]
		public void AMigratedRealmPassesTheEngineIdentityCutWithTwoDifferentLawfulNames()
		{
			const string legacyKey = "Kavvat";
			KingdomVillageCovenantReceipt row = KingdomVillageCovenantRules.Receipt(Realm,
				Transaction, Authority(Transaction, legacyKey, Zone), FactionId, Display, Zone,
				Event(Transaction), Sealed, Tick);
			Assert.AreNotEqual(row.RealmId, legacyKey);

			Assert.IsTrue(KingdomVillageCovenantRules.AuthorityBelongsToRealm(
				row.FoundingAuthority, legacyKey, out string migrated), migrated);
			Assert.IsFalse(KingdomVillageCovenantRules.AuthorityBelongsToRealm(
				row.FoundingAuthority, Realm, out string wrong),
				"the row's realm id is not the faction key and must not be accepted as one");
			StringAssert.Contains("minted under another realm", wrong);
			Assert.IsFalse(KingdomVillageCovenantRules.AuthorityBelongsToRealm(
				row.FoundingAuthority, null, out string absent));
			StringAssert.Contains("minted under another realm", absent);
			Assert.IsFalse(KingdomVillageCovenantRules.AuthorityBelongsToRealm("not an authority",
				legacyKey, out string malformed));
			StringAssert.Contains("does not decode exactly", malformed);

			// And the ordinary shape, where the two names happen to coincide, still passes.
			Assert.IsTrue(KingdomVillageCovenantRules.AuthorityBelongsToRealm(
				Row().FoundingAuthority, Realm, out string ordinary), ordinary);
		}

		/// <summary>
		/// The realm is compared on its own account, not by way of the authority. The two are
		/// separately frozen and a migrated save is exactly the case where they differ, so a
		/// comparison that reached the realm only through the authority would stop noticing.
		/// </summary>
		[Test]
		public void TwoCovenantsDifferingOnlyInTheirRealmAreDifferentFrozenFacts()
		{
			string shared = Authority(Transaction, "Kavvat", Zone);
			KingdomVillageCovenantReceipt here = KingdomVillageCovenantRules.Receipt(Realm,
				Transaction, shared, FactionId, Display, Zone, Event(Transaction), Sealed, Tick);
			KingdomVillageCovenantReceipt elsewhere = KingdomVillageCovenantRules.Receipt(
				OtherRealm, Transaction, shared, FactionId, Display, Zone, Event(Transaction),
				Sealed, Tick);
			Assert.AreEqual(here.FoundingAuthority, elsewhere.FoundingAuthority,
				"these two rows differ in the realm and in nothing else");
			Assert.IsFalse(KingdomVillageCovenantRules.SameFrozenFacts(here, elsewhere),
				"the realm is one of the facts that cannot move");

			Assert.IsTrue(KingdomVillageCovenantRules.SameFrozenFacts(Row(),
				Row(sealedStanding: 900, tick: 42L)),
				"the standing and the tick are the two that can");
			Assert.IsFalse(KingdomVillageCovenantRules.Same(Row(),
				Row(sealedStanding: 900, tick: 42L)),
				"a full comparison still notices them");
		}

		/// <summary>
		/// A realm-less authority cannot exist to be matched, and a realm-less caller matches
		/// nothing that does. The first half is the founding codec's own refusal, quoted here so
		/// the reason this family carries an absent-key guard is on the record: it is a backstop
		/// against a contract change upstream, not a case reachable today.
		/// </summary>
		[Test]
		public void AnAuthorityWithNoRealmCannotBeBuiltAndAnAbsentKeyMatchesNothing()
		{
			Assert.IsNull(KingdomFoundingTransactionRules.FormatAuthority(
				new KingdomFoundingAuthority
				{
					Kind = KingdomFoundingKind.VillageCharter,
					OwnerKind = KingdomFoundingOwnerKind.Basin,
					TransactionID = Transaction,
					OwnerNonce = Nonce,
					RealmFaction = "",
					ZoneID = Zone,
					RiteX = 12,
					RiteY = 34,
					PayloadDigest = Digest
				}), "the founding codec refuses to encode an authority with no realm");
			foreach (string absent in new[] { null, "" })
			{
				Assert.IsFalse(KingdomVillageCovenantRules.AuthorityBelongsToRealm(
					Row().FoundingAuthority, absent, out string failure));
				StringAssert.Contains("minted under another realm", failure);
			}
		}

		[Test]
		public void AnUnknownRowRevisionIsRefusedRatherThanMigrated()
		{
			KingdomVillageCovenantReceipt row = Row();
			row.Version = KingdomVillageCovenantReceipt.CurrentVersion + 1;
			row.ReceiptId = KingdomVillageCovenantRules.ReceiptId(row);
			Assert.IsFalse(KingdomVillageCovenantRules.TryValidateRow(row, out string failure));
			StringAssert.Contains("which this build does not write", failure);
		}
	}
}
#endif
