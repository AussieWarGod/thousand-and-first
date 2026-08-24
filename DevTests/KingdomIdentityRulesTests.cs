using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomIdentityRulesTests
	{
		private const string First = "00112233445566778899aabbccddeeff";
		private const string Second = "ffeeddccbbaa99887766554433221100";

		private static string Realm(string transaction = First)
		{
			string value;
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryMintRealm(transaction, out value, out fault), Is.True);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.None));
			return value;
		}

		private static string Settlement(string transaction = First)
		{
			string value;
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryMintSettlement(Realm(), transaction,
				out value, out fault), Is.True);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.None));
			return value;
		}

		[Test]
		public void FoundingIds_AreExactStableTypedHashes()
		{
			string realm = Realm();
			string settlement = Settlement();
			Assert.That(realm, Is.EqualTo(
				"taf:realm:v1:096e7294f9cd60b1a039723c33e45ff1c04b89994698f7113dd3f85e2b700d14"));
			Assert.That(settlement, Is.EqualTo(
				"taf:settlement:v1:e862365c763944741a01ed33c2e078091429e31abce7de35dbe0b382328e673e"));
			Assert.That(realm, Is.EqualTo(Realm()));
			Assert.That(settlement, Is.EqualTo(Settlement()));
			Assert.That(realm, Does.StartWith(KingdomIdentityRules.RealmPrefix));
			Assert.That(settlement, Does.StartWith(KingdomIdentityRules.SettlementPrefix));
			Assert.That(realm.Length, Is.EqualTo(KingdomIdentityRules.RealmPrefix.Length + 64));
			Assert.That(settlement.Length, Is.EqualTo(KingdomIdentityRules.SettlementPrefix.Length + 64));
			Assert.That(KingdomIdentityRules.IsRealmId(realm), Is.True);
			Assert.That(KingdomIdentityRules.IsSettlementId(settlement), Is.True);
			Assert.That(KingdomIdentityRules.IsSettlementId(realm), Is.False);
			Assert.That(KingdomIdentityRules.IsRealmId(settlement), Is.False);
		}

		[Test]
		public void FoundingIds_DomainSeparateRealmCitiesAndTransactions()
		{
			string realm = Realm();
			string first = Settlement(First);
			string second = Settlement(Second);
			Assert.That(first, Is.Not.EqualTo(second));
			Assert.That(first, Is.Not.EqualTo(realm));
			string otherRealm = Realm(Second);
			string sameTransactionOtherRealm;
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryMintSettlement(otherRealm, First,
				out sameTransactionOtherRealm, out fault), Is.True);
			Assert.That(sameTransactionOtherRealm, Is.Not.EqualTo(first));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("00112233445566778899aabbccddee")]
		[TestCase("00112233445566778899AABBCCDDEEFF")]
		[TestCase("00112233445566778899aabbccddeefg")]
		public void FoundingTransaction_RejectsNonCanonicalInput(string transaction)
		{
			string value;
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryMintRealm(transaction, out value, out fault), Is.False);
			Assert.That(value, Is.Null);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidTransaction));
		}

		[Test]
		public void LiveMint_RefusesANameDerivedRealm()
		{
			string value;
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryMintSettlement("taf:realm:kavvat", First,
				out value, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidRealm));
		}

		[Test]
		public void LegacyMigration_IsDeterministicAndClaimSensitive()
		{
			string realmA;
			string realmB;
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryMigrateRealm("The Keepers of Kavvat", 1200L,
				4UL, 9UL, "JoppaWorld.11.22.1.1.10", out realmA, out fault), Is.True);
			Assert.That(KingdomIdentityRules.TryMigrateRealm("The Keepers of Kavvat", 1200L,
				4UL, 9UL, "JoppaWorld.11.22.1.1.10", out realmB, out fault), Is.True);
			Assert.That(realmB, Is.EqualTo(realmA));
			Assert.That(realmA, Is.EqualTo(
				"taf:realm:v1:aba38be606bc99fc88ddda4e60e9c10471e21e55a78d42dccc2df03be5bba13c"));
			string cityA;
			string cityB;
			Assert.That(KingdomIdentityRules.TryMigrateSettlement(realmA, 1200L,
				"JoppaWorld.11.22.1.1.10", out cityA, out fault), Is.True);
			Assert.That(KingdomIdentityRules.TryMigrateSettlement(realmA, 1200L,
				"JoppaWorld.12.22.1.1.10", out cityB, out fault), Is.True);
			Assert.That(cityA, Is.EqualTo(
				"taf:settlement:v1:f4c1e81aac24e852ea82275c38fa7b91d665c7a98209bbc29c402bc27ddd9d96"));
			Assert.That(cityB, Is.Not.EqualTo(cityA));
		}

		[Test]
		public void LegacyMigration_AcceptsBoundedUnicodeAndRejectsHostileText()
		{
			string value;
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryMigrateRealm("Kavvat συνοικία", 0L,
				0UL, 0UL, "zone-水", out value, out fault), Is.True);
			Assert.That(KingdomIdentityRules.IsRealmId(value), Is.True);
			Assert.That(KingdomIdentityRules.TryMigrateRealm(new string('x', 513), 0L,
				0UL, 0UL, "zone", out value, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidEvidence));
			Assert.That(KingdomIdentityRules.TryMigrateRealm("realm", 0L,
				0UL, 0UL, "bad\ud800", out value, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidEvidence));
		}

		[Test]
		public void LegacyMigration_NormalizesUnicodeAndEnforcesBothBounds()
		{
			string composed;
			string decomposed;
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryMigrateRealm("Caf\u00e9", 1L, 2UL, 3UL,
				"zone", out composed, out fault), Is.True);
			Assert.That(KingdomIdentityRules.TryMigrateRealm("Cafe\u0301", 1L, 2UL, 3UL,
				"zone", out decomposed, out fault), Is.True);
			Assert.That(decomposed, Is.EqualTo(composed));
			Assert.That(KingdomIdentityRules.TryMigrateRealm(new string('\u00e9', 512), 1L,
				2UL, 3UL, "zone", out composed, out fault), Is.True);
			Assert.That(KingdomIdentityRules.TryMigrateRealm(new string('x', 513), 1L,
				2UL, 3UL, "zone", out composed, out fault), Is.False);
			Assert.That(KingdomIdentityRules.TryMigrateRealm(new string('\u6c34', 341), 1L,
				2UL, 3UL, "zone", out composed, out fault), Is.True);
			Assert.That(KingdomIdentityRules.TryMigrateRealm(new string('\u6c34', 342), 1L,
				2UL, 3UL, "zone", out composed, out fault), Is.False);
			Assert.That(KingdomIdentityRules.TryMigrateRealm(null, 1L, 2UL, 3UL,
				"zone", out composed, out fault), Is.False);
			Assert.That(KingdomIdentityRules.TryMigrateRealm("", 1L, 2UL, 3UL,
				"zone", out composed, out fault), Is.False);
			Assert.That(KingdomIdentityRules.TryMigrateRealm("realm", -1L, 2UL, 3UL,
				"zone", out composed, out fault), Is.False);
		}

		[Test]
		public void ProviderFailure_RemainsATryFailure()
		{
			Func<System.Security.Cryptography.SHA256> original =
				KingdomIdentityRules.TestProviderFactory;
			try
			{
				string value;
				KingdomIdentityFault fault;
				KingdomIdentityRules.TestProviderFactory = delegate { return null; };
				Assert.That(KingdomIdentityRules.TryMintRealm(First, out value, out fault), Is.False);
				Assert.That(value, Is.Null);
				Assert.That(fault, Is.EqualTo(KingdomIdentityFault.CryptographicFailure));
				KingdomIdentityRules.TestProviderFactory = delegate
				{
					throw new NotSupportedException("provider unavailable");
				};
				Assert.That(KingdomIdentityRules.TryMintRealm(First, out value, out fault), Is.False);
				Assert.That(fault, Is.EqualTo(KingdomIdentityFault.CryptographicFailure));
			}
			finally
			{
				KingdomIdentityRules.TestProviderFactory = original;
			}
		}

		[Test]
		public void SettlementSet_RejectsEveryDuplicateAndInvalidMember()
		{
			string first = Settlement(First);
			string second = Settlement(Second);
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.ValidateSettlementSet(
				new List<string> { first, second }, out fault), Is.True);
			Assert.That(KingdomIdentityRules.ValidateSettlementSet(
				new List<string> { first, first }, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.DuplicateSettlement));
			Assert.That(KingdomIdentityRules.ValidateSettlementSet(
				new List<string> { first, "taf:settlement:mutable-name" }, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidSettlement));
		}

		[Test]
		public void SettlementSet_CapsBeforeExaminingAField()
		{
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.ValidateSettlementSet(new List<string>
			{
				Settlement(First), Settlement(Second), Settlement("11111111111111111111111111111111"),
				Settlement("22222222222222222222222222222222"), "malformed"
			}, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.TooManySettlements));
		}

		[Test]
		public void SettlementSet_HandlesNullEmptyAndExactCap()
		{
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.ValidateSettlementSet(null, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.NullSet));
			Assert.That(KingdomIdentityRules.ValidateSettlementSet(
				new List<string>(), out fault), Is.True);
			Assert.That(KingdomIdentityRules.ValidateSettlementSet(new List<string>
			{
				Settlement(First), Settlement(Second),
				Settlement("11111111111111111111111111111111"),
				Settlement("22222222222222222222222222222222")
			}, out fault), Is.True);
		}

		[Test]
		public void FoundedTopology_RequiresRealmAndAtLeastOneExactCity()
		{
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.ValidateRealmTopology(Realm(),
				new List<string>(), out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.EmptySettlementSet));
			Assert.That(KingdomIdentityRules.ValidateRealmTopology("mutable realm",
				new List<string> { Settlement() }, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidRealm));
			Assert.That(KingdomIdentityRules.ValidateRealmTopology(Realm(),
				new List<string> { Settlement(), Settlement() }, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.DuplicateSettlement));
		}

		[Test]
		public void MutableNameLookupRequiresOneExactMatchAcrossTheWholeSet()
		{
			string first = Settlement(First);
			string second = Settlement(Second);
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryResolveUniqueSettlementName(
				new[] { "Kavvat", "Ezra" }, new[] { first, second }, "Ezra",
				out string resolved, out fault), Is.True);
			Assert.That(resolved, Is.EqualTo(second));
			Assert.That(KingdomIdentityRules.TryResolveUniqueSettlementName(
				new[] { "Kavvat", "Kavvat" }, new[] { first, second }, "Kavvat",
				out resolved, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.AmbiguousSettlementName));
			Assert.That(resolved, Is.Null);
			Assert.That(KingdomIdentityRules.TryResolveUniqueSettlementName(
				new[] { "Kavvat" }, new[] { first, second }, "Kavvat",
				out resolved, out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.RaggedSettlementNames));
		}

		[Test]
		public void FoundingProvenance_ReprovesOnlyExactOriginVersionAndEvidence()
		{
			string realm = Realm();
			string city = Settlement();
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.ReproveRealm(realm,
				KingdomIdentityRules.RulesVersion,
				KingdomIdentityOrigin.FoundingTransaction, First, null, 0L, 0UL, 0UL,
				"zone-a", out fault), Is.True);
			Assert.That(KingdomIdentityRules.ReproveSettlement(city, realm,
				KingdomIdentityRules.RulesVersion,
				KingdomIdentityOrigin.FoundingTransaction, First, 0L, "zone-a",
				out fault), Is.True);
			Assert.That(KingdomIdentityRules.ReproveRealm(realm,
				KingdomIdentityRules.RulesVersion + 1,
				KingdomIdentityOrigin.FoundingTransaction, First, null, 0L, 0UL, 0UL,
				"zone-a", out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidVersion));
			Assert.That(KingdomIdentityRules.ReproveRealm(realm,
				KingdomIdentityRules.RulesVersion,
				KingdomIdentityOrigin.FoundingTransaction, First, "name injection", 0L,
				0UL, 0UL, "zone-a", out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidEvidence));
			Assert.That(KingdomIdentityRules.ReproveSettlement(city, realm,
				KingdomIdentityRules.RulesVersion, KingdomIdentityOrigin.Quarantined,
				First, 0L, "zone-a", out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidOrigin));
		}

		[Test]
		public void LegacyProvenance_ReprovesFrozenEvidenceButRejectsThirdValues()
		{
			string realm;
			string city;
			KingdomIdentityFault fault;
			Assert.That(KingdomIdentityRules.TryMigrateRealm("legacy-faction", 1200L,
				7UL, 9UL, "zone-a", out realm, out fault), Is.True);
			Assert.That(KingdomIdentityRules.TryMigrateSettlement(realm, 1200L,
				"zone-a", out city, out fault), Is.True);
			Assert.That(KingdomIdentityRules.ReproveRealm(realm,
				KingdomIdentityRules.RulesVersion, KingdomIdentityOrigin.LegacyMigration,
				null, "legacy-faction", 1200L, 7UL, 9UL, "zone-a", out fault), Is.True);
			Assert.That(KingdomIdentityRules.ReproveSettlement(city, realm,
				KingdomIdentityRules.RulesVersion, KingdomIdentityOrigin.LegacyMigration,
				null, 1200L, "zone-a", out fault), Is.True);
			Assert.That(KingdomIdentityRules.ReproveRealm(realm,
				KingdomIdentityRules.RulesVersion, KingdomIdentityOrigin.LegacyMigration,
				Second, "legacy-faction", 1200L, 7UL, 9UL, "zone-a", out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidEvidence));
			Assert.That(KingdomIdentityRules.ReproveSettlement(city, realm,
				KingdomIdentityRules.RulesVersion, KingdomIdentityOrigin.LegacyMigration,
				Second, 1200L, "zone-a", out fault), Is.False);
			Assert.That(fault, Is.EqualTo(KingdomIdentityFault.InvalidEvidence));
		}

		[Test]
		public void TypedIdValidation_RejectsWrongLengthCaseAndAlphabet()
		{
			string id = Settlement();
			Assert.That(KingdomIdentityRules.IsSettlementId(id.Substring(0, id.Length - 1)), Is.False);
			string upperPayload = id.Substring(0, KingdomIdentityRules.SettlementPrefix.Length)
				+ char.ToUpperInvariant(id[KingdomIdentityRules.SettlementPrefix.Length])
				+ id.Substring(KingdomIdentityRules.SettlementPrefix.Length + 1);
			Assert.That(KingdomIdentityRules.IsSettlementId(upperPayload), Is.False);
			Assert.That(KingdomIdentityRules.IsSettlementId(
				id.Substring(0, id.Length - 1) + "g"), Is.False);
		}
	}
}
