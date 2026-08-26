#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst.Api;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomResidentIdentityRulesTests
	{
		[Test]
		public void CanonicalTalliesFoldAliasesDropDeadRowsAndClampOverflow()
		{
			Dictionary<string, int> source = new Dictionary<string, int>
			{
				{ " Mechanimist ", 2 },
				{ "MECHANIMIST", 3 },
				{ "Mopango", 1 },
				{ "gone", 0 },
				{ "bad|wire", 7 },
				{ "too-many", KingdomResidentIdentityRules.MaxFactCount }
			};

			Dictionary<string, int> result = KingdomResidentIdentityRules.CanonicalTallies(
				source, KingdomZoningRules.KindCulture);

			Assert.AreEqual(3, result.Count);
			Assert.AreEqual(5, result["mechanimist"]);
			Assert.AreEqual(1, result["mopango"]);
			Assert.AreEqual(KingdomResidentIdentityRules.MaxFactCount, result["too-many"]);
			Assert.IsFalse(result.ContainsKey("gone"));
			Assert.IsFalse(result.ContainsKey("bad|wire"));
			Assert.AreEqual(6, source.Count, "canonicalization must not rewrite its evidence");
		}

		[Test]
		public void BodyReceiptTransitionIsIdempotentAndLastBearerLapses()
		{
			Dictionary<string, int> facts = new Dictionary<string, int>();
			Assert.IsTrue(KingdomResidentIdentityRules.Transition(facts,
				KingdomZoningRules.KindCulture, null, "Mechanimist"));
			Assert.AreEqual(1, facts["mechanimist"]);

			Assert.IsFalse(KingdomResidentIdentityRules.Transition(facts,
				KingdomZoningRules.KindCulture, "Mechanimist", "MECHANIMIST"));
			Assert.AreEqual(1, facts["mechanimist"]);

			Assert.IsTrue(KingdomResidentIdentityRules.Transition(facts,
				KingdomZoningRules.KindCulture, "mechanimist", "Mopango"));
			Assert.IsFalse(facts.ContainsKey("mechanimist"));
			Assert.AreEqual(1, facts["mopango"]);

			Assert.IsTrue(KingdomResidentIdentityRules.Transition(facts,
				KingdomZoningRules.KindCulture, "mopango", null));
			Assert.AreEqual(0, facts.Count);
			Assert.IsFalse(KingdomResidentIdentityRules.Transition(facts,
				KingdomZoningRules.KindCulture, "mopango", null));
		}

		[Test]
		public void RosterKeysAreLiveCanonicalAndDeterministicallySorted()
		{
			Dictionary<string, int> facts = new Dictionary<string, int>
			{
				{ "Mopango", 2 }, { "human", 1 }, { "absent", -1 }
			};
			CollectionAssert.AreEqual(new[] { "species:human", "species:mopango" },
				KingdomResidentIdentityRules.RosterKeys(facts,
					KingdomZoningRules.KindSpecies));
		}

		[Test]
		public void ExtensionKeyReceiptIsOwnedBoundedCanonicalAndIdempotent()
		{
			List<string> keys = new List<string>
			{
				"my-mod:glassworking", "my-mod:glassworking", "OTHER:stolen",
				"my-mod:desert lore"
			};
			string receipt = KingdomResidentIdentityRules.EncodeIdentityKeys(keys);
			Assert.AreEqual("my-mod:desert lore|my-mod:glassworking", receipt);
			CollectionAssert.AreEqual(new[] { "my-mod:desert lore", "my-mod:glassworking" },
				KingdomResidentIdentityRules.DecodeIdentityKeys(receipt));
			Assert.IsNull(KingdomResidentIdentityRules.CanonicalIdentityKey("OTHER:stolen"));

			Dictionary<string, int> tallies = new Dictionary<string, int>();
			List<string> current = KingdomResidentIdentityRules.DecodeIdentityKeys(receipt);
			Assert.IsTrue(KingdomResidentIdentityRules.TransitionIdentityKeys(tallies, null,
				current));
			Assert.IsFalse(KingdomResidentIdentityRules.TransitionIdentityKeys(tallies, current,
				current));
			Assert.AreEqual(1, tallies["my-mod:glassworking"]);
			Assert.IsTrue(KingdomResidentIdentityRules.TransitionIdentityKeys(tallies, current,
				new[] { "my-mod:glassworking" }));
			Assert.IsFalse(tallies.ContainsKey("my-mod:desert lore"));
			CollectionAssert.AreEqual(new[] { "my-mod:glassworking" },
				KingdomResidentIdentityRules.IdentityRosterKeys(tallies));
		}

		[Test]
		public void BuiltInBodyReceiptCarriesGenotypeAndVanillaConditionsInSameLane()
		{
			List<string> keys = KingdomResidentIdentityRules.BuiltInIdentityKeys(
				"True Kin", Robot: true, WetBodied: true, BroadBodied: true);
			CollectionAssert.AreEqual(new[]
			{
				"body:broad-bodied", "body:robot", "body:wet-bodied", "genotype:true kin"
			}, keys);
			CollectionAssert.IsEmpty(KingdomResidentIdentityRules.BuiltInIdentityKeys(
				null, Robot: false, WetBodied: false, BroadBodied: false));

			Dictionary<string, int> tallies = new Dictionary<string, int>
			{
				{ "body:robot", 2 }, { "body:wet-bodied", 0 },
				{ "genotype:true kin", 1 }, { "my-mod:robot", 9 }, { "BODY:spoof", 4 }
			};
			CollectionAssert.AreEqual(new[] { "robot" },
				KingdomResidentIdentityRules.IdentityNames(tallies,
					KingdomResidentIdentityRules.KindBody));
			CollectionAssert.AreEqual(new[] { "true kin" },
				KingdomResidentIdentityRules.IdentityNames(tallies,
					KingdomResidentIdentityRules.KindGenotype));
		}

		[Test]
		public void HistoricalArchivesRestoreOnlyIdentityFactsTheirSchemaCarried()
		{
			KingdomSettlement source = new KingdomSettlement();
			source.CultureCounts["mechanimist"] = 2;
			source.SpeciesCounts["mopango"] = 1;
			source.IdentityCounts["my-mod:glassworking"] = 1;

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeRaidV3ForTests(source,
				out byte[] v3, out string failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.RaidVersion,
				BitConverter.ToInt32(v3, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v3,
				out KingdomSettlement migrated, out int future, out failure), failure);
			Assert.AreEqual(0, future);
			Assert.IsNotNull(migrated.CultureCounts);
			Assert.IsNotNull(migrated.SpeciesCounts);
			Assert.IsNotNull(migrated.IdentityCounts);
			Assert.AreEqual(0, migrated.CultureCounts.Count);
			Assert.AreEqual(0, migrated.SpeciesCounts.Count);
			Assert.AreEqual(0, migrated.IdentityCounts.Count);

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncodeResidentIdentityV4ForTests(
				source, out byte[] v4, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.ResidentIdentityVersion,
				BitConverter.ToInt32(v4, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v4,
				out KingdomSettlement migratedV4, out future, out failure), failure);
			Assert.AreEqual(2, migratedV4.CultureCounts["mechanimist"]);
			Assert.AreEqual(1, migratedV4.SpeciesCounts["mopango"]);
			Assert.AreEqual(0, migratedV4.IdentityCounts.Count);

			Assert.IsTrue(KingdomArchivedSettlementCodec.TryEncode(source,
				out byte[] v5, out failure), failure);
			Assert.AreEqual(KingdomArchivedSettlementCodec.CurrentVersion,
				BitConverter.ToInt32(v5, 4));
			Assert.IsTrue(KingdomArchivedSettlementCodec.TryDecode(v5,
				out KingdomSettlement roundTrip, out future, out failure), failure);
			Assert.AreEqual(2, roundTrip.CultureCounts["mechanimist"]);
			Assert.AreEqual(1, roundTrip.SpeciesCounts["mopango"]);
			Assert.AreEqual(1, roundTrip.IdentityCounts["my-mod:glassworking"]);
		}

		[Test]
		public void RuntimeReadsVanillaIdentityAndHooksEveryDeparturePath()
		{
			string identity = TestMain.ReadRepositoryText(Path.Combine("Growth",
				"KingdomResidentIdentity.cs"));
			StringAssert.Contains("settler.GetCulture()", identity);
			StringAssert.Contains("settler.GetSpecies()", identity);
			StringAssert.Contains("CultureProperty", identity);
			StringAssert.Contains("SpeciesProperty", identity);
			StringAssert.Contains("KingdomIdentity.Read(settler)", identity);
			StringAssert.Contains("KingdomExtensions.IdentityKeys(reading)", identity);
			StringAssert.Contains("KingdomResidentIdentityRules.BuiltInIdentityKeys(", identity);
			StringAssert.Contains("KingdomQol.TruthOf(settler)", identity);
			StringAssert.Contains("truth.Aquatic && !truth.Flying", identity);
			StringAssert.Contains("truth.BroadBodied", identity);
			StringAssert.Contains("IdentityKeysProperty", identity);
			string qol = TestMain.ReadRepositoryText(Path.Combine("Core", "KingdomQolResidents.cs"));
			StringAssert.Contains("Resident.HasTagOrProperty(\"Gigantic\")", qol);

			string system = TestMain.ReadRepositoryText(Path.Combine("Core", "KingdomSystem.cs"));
			StringAssert.Contains("KingdomResidentIdentity.Reconcile(this, survey.Settlers)", system);
			StringAssert.Contains("KingdomResearch.ApplySources(this)", system);
			string offices = TestMain.ReadRepositoryText(Path.Combine("Experience",
				"KingdomOffices.cs"));
			StringAssert.Contains("KingdomResidentIdentity.Forget(system, Citizen)", offices);
			string growth = TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomGrowth.cs"));
			StringAssert.Contains("KingdomResidentIdentity.Forget(System, leaver)", growth);
		}

		[Test]
		public void ShippedIdentityContentKeepsCultureKnowledgeDistinctFromSpeciesAnatomy()
		{
			string research = TestMain.ReadRepositoryText("KingdomResearch.xml");
			StringAssert.Contains("Requires=\"node:notes,culture:Snapjaw\"", research);
			StringAssert.Contains("SeededBy=\"culture:Snapjaw\"", research);
			StringAssert.DoesNotContain("species:snapjaw\" Grants=\"node:trailreading", research);
			StringAssert.Contains("Requires=\"node:vat,species:snapjaw\"", research);
			StringAssert.Contains("Grants=\"node:muzzlecraft\"", research);

			string procedures = TestMain.ReadRepositoryText("KingdomProcedures.xml");
			StringAssert.Contains("Key=\"moonfang\"", procedures);
			StringAssert.Contains("Slots=\"Face\"", procedures);
			StringAssert.Contains("Knowledge=\"node:muzzlecraft\"", procedures);
		}

		[Test]
		public void ActiveResearchRechecksLiveDoorsWithoutErasingAccrual()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomResearch.cs"));
			int advance = source.IndexOf("public static long Advance(", StringComparison.Ordinal);
			int stall = source.IndexOf("private static void Stall(", advance,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(advance, 0);
			Assert.Greater(stall, advance);
			string body = source.Substring(advance, stall - advance);
			StringAssert.Contains("!Admissible(System, node)", body);
			StringAssert.Contains("MissingKnowledge(roster, node.Requires)", body);
			StringAssert.Contains("ClosedSubjectLine", body);
			StringAssert.Contains("MissingSourceLine", body);
			Assert.IsFalse(body.Contains("ResearchAccrued = 0", StringComparison.Ordinal)
				&& body.IndexOf("ResearchAccrued = 0", StringComparison.Ordinal)
				< body.IndexOf("int worked", StringComparison.Ordinal),
				"a live-source stall must preserve already-paid thought");
		}
	}
}
#endif
