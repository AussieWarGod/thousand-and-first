#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The rite-source vertical slice: vanilla's only observable covenant edge is its initial
	/// start event, and research must receive the faction through that edge before its existing
	/// SeededBy machinery can do any work.
	/// </summary>
	public class KingdomRiteSeedRulesTests
	{
		[Test]
		public void MayRememberRite_OnlyFirstSharesWithAStorableFactionQualify()
		{
			Assert.IsTrue(KingdomResearchRules.MayRememberRite(true, "Barathrumites"));
			Assert.IsFalse(KingdomResearchRules.MayRememberRite(false, "Barathrumites"));
			Assert.IsFalse(KingdomResearchRules.MayRememberRite(true, null));
			Assert.IsFalse(KingdomResearchRules.MayRememberRite(true, "Fungi|Oozes"),
				"a faction name that cannot round-trip through the roster cannot become a source");
			Assert.IsFalse(KingdomResearchRules.MayRememberRite(true, "Fungi,Oozes"),
				"a faction name that cannot be addressed in a source list cannot become a source");
			Assert.IsFalse(KingdomResearchRules.MayRememberRite(true,
				new string('x', KingdomResearchRules.MaxFounderRiteNameLength + 1)));
			Assert.IsFalse(KingdomResearchRules.IsCanonicalFounderRite("rite:"));
			Assert.IsFalse(KingdomResearchRules.IsCanonicalFounderRite("rite"));
			Assert.IsFalse(KingdomResearchRules.IsCanonicalFounderRite("rite:Fungi,Oozes"));
			Assert.IsTrue(KingdomResearchRules.IsCanonicalFounderRite(" RITE:Barathrumites "));
			CollectionAssert.AreEqual(new List<string> { "rite:barathrumites" },
				KingdomResearchRules.CanonicalFounderRites(
					"rite:Barathrumites|rite:|rite:Fungi,Oozes|machine:Solar Condenser"));
		}

		[Test]
		public void RuntimeHook_RegistersInitialVanillaRiteAndRoutesItThroughResearch()
		{
			string source = KingdomSystemLogicalSource.Read();
			StringAssert.Contains("public partial class KingdomSystem : IPlayerSystem", source,
				"the ritual event is dispatched to the player body, not to the game");
			int registration = source.IndexOf("public override void RegisterPlayer", StringComparison.Ordinal);
			Assert.GreaterOrEqual(registration, 0);
			int registrationEnd = source.IndexOf("\n\t\t/// <summary>", registration + 1,
				StringComparison.Ordinal);
			Assert.Greater(registrationEnd, registration);
			StringAssert.Contains("Registrar.Register(WaterRitualStartEvent.ID)",
				source.Substring(registration, registrationEnd - registration));
			Assert.AreEqual(1, Occurrences(source, "Registrar.Register(WaterRitualStartEvent.ID)"),
				"the event must not also be registered on the game-level registrar");
			int handler = source.IndexOf("public override bool HandleEvent(WaterRitualStartEvent E)",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(handler, 0);
			int handlerEnd = source.IndexOf("\n\t\t/// <summary>", handler + 1, StringComparison.Ordinal);
			Assert.Greater(handlerEnd, handler);
			string body = source.Substring(handler, handlerEnd - handler);
			StringAssert.Contains("E.Initial", body);
			StringAssert.Contains("E.Record", body);
			StringAssert.Contains("E.Record.faction", body);
			StringAssert.DoesNotContain("The.Speaker", body);
			StringAssert.DoesNotContain("GetPrimaryFaction", body);
			StringAssert.Contains("KingdomResearch.RememberRite", body);

			source = ReadRepoSource("Growth/KingdomResearch.cs");
			int remember = source.IndexOf("internal static bool RememberRite", StringComparison.Ordinal);
			Assert.GreaterOrEqual(remember, 0);
			int rememberEnd = source.IndexOf("\n\t\t// The first token", remember, StringComparison.Ordinal);
			Assert.Greater(rememberEnd, remember);
			string rememberBody = source.Substring(remember, rememberEnd - remember);
			StringAssert.Contains("FounderRiteState", rememberBody);
			StringAssert.Contains("FounderRites()", rememberBody);
			StringAssert.DoesNotContain("KingdomZoning.Learn", rememberBody,
				"rite keys belong to the founder and must never enter one city's KeepersRoster");
			StringAssert.DoesNotContain("!System.Founded", rememberBody,
				"a ritual performed before founding or between realms must still be remembered");
			StringAssert.Contains("KingdomZoningRules.SatisfyingKeys(roster, node.SeededBy)", source);
			StringAssert.Contains("ApplySeedSourceReceipt(System, node.Key, seeded[j])", source,
				"every concrete matching source must receive its own durable receipt");
			StringAssert.Contains("SeedBySources(System, node.Key, learnedFrom ?? System.SeatName, sourceCount)", source,
				"recovery must derive a floor from receipts, never add another quarter on retry");
			StringAssert.Contains("SeedReceiptStatePrefix", source);
			StringAssert.Contains("System.CurrentSettlementId", source,
				"a seed receipt must stay with the actual city without changing its archive wire format");
			StringAssert.Contains("DurableSeedSourceCount(sourceCount, nextCount)", source,
				"a later receipt-cap refusal must not erase an earlier durable source count");

			source = KingdomZoningLogicalSource.Read();
			int roster = source.IndexOf("public static List<string> Roster(KingdomSystem System)",
				StringComparison.Ordinal);
			int rosterOf = source.IndexOf("public static List<string> RosterOf(KingdomSettlement City)",
				StringComparison.Ordinal);
			Assert.Greater(rosterOf, roster);
			StringAssert.Contains("KingdomResearch.FounderRites()",
				source.Substring(roster, rosterOf - roster));
			int rosterOfEnd = source.IndexOf("\n\t\t/// <summary>", rosterOf + 1,
				StringComparison.Ordinal);
			Assert.Greater(rosterOfEnd, rosterOf);
			StringAssert.DoesNotContain("FounderRites",
				source.Substring(rosterOf, rosterOfEnd - rosterOf),
				"off-seat city holdings must not absorb the founder's portable rite ledger");
		}

		[Test]
		public void ConcreteSourceResolution_DeduplicatesBareAndQualifiedAliases()
		{
			List<string> roster = new List<string> { "rite:Barathrumites", "disk:Barathrumites" };
			Assert.AreEqual("rite:barathrumites",
				KingdomZoningRules.SatisfyingKey(roster, "Barathrumites"));
			Assert.AreEqual("rite:barathrumites",
				KingdomZoningRules.SatisfyingKey(roster, "rite:Barathrumites"));
			Assert.AreEqual("disk:barathrumites",
				KingdomZoningRules.SatisfyingKey(roster, "disk:Barathrumites"));
			CollectionAssert.AreEqual(new List<string> { "rite:barathrumites" },
				KingdomZoningRules.SatisfyingKeys(roster,
					"Barathrumites,rite:Barathrumites"));
			CollectionAssert.AreEqual(new List<string> { "rite:oozes", "rite:fungi" },
				KingdomZoningRules.SatisfyingKeys(
					new List<string> { "rite:Oozes", "rite:Fungi" }, "rite:Oozes,rite:Fungi"));
			Assert.AreEqual("rite:fungi", KingdomZoningRules.SatisfyingKey(
				new List<string> { "rite:Fungi" }, "rite:Oozes|rite:Fungi"));
			CollectionAssert.AreEqual(new List<string> { "rite:oozes", "rite:fungi" },
				KingdomZoningRules.SatisfyingKeys(
					new List<string> { "rite:Oozes", "rite:Fungi" },
					"rite:Oozes|rite:Fungi"));
		}

		[Test]
		public void SeedReceipts_AreIdempotentDistinctAndNodeExact()
		{
			string encoded;
			int count;
			bool changed;
			Assert.IsTrue(KingdomResearchRules.TryApplySeedReceipt("", "salt", "rite:Mechanimists",
				out encoded, out count, out changed));
			Assert.IsTrue(changed);
			Assert.AreEqual(1, count);

			string once = encoded;
			Assert.IsTrue(KingdomResearchRules.TryApplySeedReceipt(encoded, "salt", "RITE:MECHANIMISTS",
				out encoded, out count, out changed));
			Assert.IsFalse(changed);
			Assert.AreEqual(once, encoded);
			Assert.AreEqual(1, count);

			Assert.IsTrue(KingdomResearchRules.TryApplySeedReceipt(encoded, "salt", "rite:Barathrumites",
				out encoded, out count, out changed));
			Assert.IsTrue(changed);
			Assert.AreEqual(2, count);

			Assert.IsTrue(KingdomResearchRules.TryApplySeedReceipt(encoded, "salt=deep", "rite:Mechanimists",
				out encoded, out count, out changed));
			Assert.AreEqual(1, count);
			Assert.AreEqual(2, KingdomResearchRules.SeedReceiptCount(encoded, "salt"),
				"length-prefixed node identities must not count a longer node sharing its prefix");

			string capped = encoded;
			Assert.IsTrue(KingdomResearchRules.TryApplySeedReceipt(capped, "salt", "rite:Seekers",
				out encoded, out count, out changed));
			Assert.IsFalse(changed, "a third source needs no row once the 50% floor is represented");
			Assert.AreEqual(KingdomResearchRules.MaxSeedSourcesPerNode, count);
			Assert.AreEqual(capped, encoded);
		}

		[Test]
		public void SeedReceipts_RefuseUnboundedGrowth()
		{
			string encoded = "";
			for (int i = 0; i < KingdomResearchRules.MaxSeedReceiptRows; i++)
			{
				int count;
				bool changed;
				Assert.IsTrue(KingdomResearchRules.TryApplySeedReceipt(encoded, "node" + i,
					"rite:faction" + i, out encoded, out count, out changed));
				Assert.IsTrue(changed);
			}
			string refused;
			int refusedCount;
			bool refusedChanged;
			Assert.IsFalse(KingdomResearchRules.TryApplySeedReceipt(encoded, "overflow", "rite:last",
				out refused, out refusedCount, out refusedChanged));
			Assert.AreEqual(encoded, refused);
			Assert.IsFalse(refusedChanged);
		}

		[Test]
		public void SeedReceipts_IgnoreSpoofsAndSanitizeMalformedRows()
		{
			string spoofed = "researchseed:4.salt=x|researchseed:4.salt=y|" +
				"researchseed:04.salt=rite:oozes|researchseed:5.salt=rite:fungi|rite:oozes";
			Assert.AreEqual(0, KingdomResearchRules.SeedReceiptCount(spoofed, "salt"));
			Assert.AreEqual(0, KingdomResearchRules.SeededBySources(20, 0,
				KingdomResearchRules.SeedReceiptCount(spoofed, "salt")));

			string updated;
			int count;
			bool changed;
			Assert.IsTrue(KingdomResearchRules.TryApplySeedReceipt(spoofed, "salt", "rite:Oozes",
				out updated, out count, out changed));
			Assert.IsTrue(changed);
			Assert.AreEqual(1, count);
			Assert.AreEqual(KingdomResearchRules.SeedReceiptKey("salt", "rite:oozes"), updated);
		}

		[Test]
		public void SeedReceipts_RejectAnOversizedDecodedStore()
		{
			List<string> rows = new List<string>();
			for (int i = 0; i <= KingdomResearchRules.MaxSeedReceiptRows; i++)
			{
				rows.Add(KingdomResearchRules.SeedReceiptKey("node" + i, "rite:faction" + i));
			}
			string oversized = string.Join(KingdomZoningRules.RosterSeparator.ToString(),
				rows.ToArray());
			Assert.AreEqual(0, KingdomResearchRules.SeedReceiptCount(oversized, "node0"));

			string updated;
			int count;
			bool changed;
			Assert.IsFalse(KingdomResearchRules.TryApplySeedReceipt(oversized, "target", "rite:last",
				out updated, out count, out changed));
			Assert.AreEqual(oversized, updated);
			Assert.AreEqual(0, count);
			Assert.IsFalse(changed);
		}

		[Test]
		public void SeedReceipts_RejectOversizedComponentsAndSingleRowStores()
		{
			Assert.IsNull(KingdomResearchRules.SeedReceiptKey(
				new string('n', KingdomResearchRules.MaxSeedReceiptNodeLength + 1), "rite:source"));
			Assert.IsNull(KingdomResearchRules.SeedReceiptKey("node", "rite:" +
				new string('s', KingdomResearchRules.MaxSeedReceiptSourceLength)));

			string oversized = new string('x', KingdomResearchRules.MaxSeedReceiptEncodedLength + 1);
			Assert.AreEqual(0, KingdomResearchRules.SeedReceiptCount(oversized, "node"));
			string updated;
			int count;
			bool changed;
			Assert.IsFalse(KingdomResearchRules.TryApplySeedReceipt(oversized, "node", "rite:source",
				out updated, out count, out changed));
			Assert.AreEqual(oversized, updated);
		}

		[Test]
		public void ReceiptCap_PreservesTheLastDurableTargetFloor()
		{
			string encoded = "";
			for (int i = 0; i < KingdomResearchRules.MaxSeedReceiptRows - 1; i++)
			{
				int unrelatedCount;
				bool changed;
				Assert.IsTrue(KingdomResearchRules.TryApplySeedReceipt(encoded, "other" + i,
					"rite:faction" + i, out encoded, out unrelatedCount, out changed));
			}
			int firstCount;
			bool firstChanged;
			Assert.IsTrue(KingdomResearchRules.TryApplySeedReceipt(encoded, "target", "rite:Oozes",
				out encoded, out firstCount, out firstChanged));
			Assert.AreEqual(1, firstCount);

			string refused;
			int refusedCount;
			bool refusedChanged;
			Assert.IsFalse(KingdomResearchRules.TryApplySeedReceipt(encoded, "target", "rite:Fungi",
				out refused, out refusedCount, out refusedChanged));
			int durable = KingdomResearchRules.DurableSeedSourceCount(firstCount, -1);
			Assert.AreEqual(1, durable);
			Assert.AreEqual(KingdomResearchRules.EffortTicks(20) / 4,
				KingdomResearchRules.SeededBySources(20, 0, durable));

			string source = ReadRepoSource("Growth/KingdomResearch.cs");
			StringAssert.Contains("SeedSourceRecorded(System, node.Key, seeded[j])", source,
				"provenance must name only a concrete source whose receipt is durable");
			StringAssert.DoesNotContain("NameOf(seeded[seeded.Count - 1])", source,
				"a refused later source must never be named as the source of an earlier floor");
		}

		[Test]
		public void CrossCityTeaching_UsesTheOtherCityIdentityAsItsStableSource()
		{
			string source = KingdomZoningLogicalSource.Read();
			int teaching = source.IndexOf("private static void SetDownWhatWasLearned", StringComparison.Ordinal);
			int teachingEnd = source.IndexOf("\n\t\tprivate static string AwayName", teaching,
				StringComparison.Ordinal);
			Assert.Greater(teachingEnd, teaching);
			string body = source.Substring(teaching, teachingEnd - teaching);
			StringAssert.Contains("System.Away.City.SettlementId", body);
			StringAssert.Contains("KingdomZoningRules.ComposeKey(\"settlement\", awayId)", body);
			StringAssert.Contains("KingdomResearch.SeedFromSource(System, Carried[i].Key, source", body);
			StringAssert.DoesNotContain("KingdomResearch.Seed(System, Carried[i].Key", body);
		}

		[Test]
		public void RuntimeSeedReceiptRefusesBeforeWriteAndPublishesBeforeBenchCredit()
		{
			string source = ReadRepoSource("Growth/KingdomResearch.cs");
			int seed = source.IndexOf("internal static bool SeedFromSource(",
				StringComparison.Ordinal);
			int seedEnd = source.IndexOf("private static bool SeedBySources(", seed,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(seed, 0);
			Assert.Greater(seedEnd, seed);
			string seedBody = source.Substring(seed, seedEnd - seed);
			int gate = seedBody.IndexOf("!TryGetNode(Key, out node)", StringComparison.Ordinal);
			int receipt = seedBody.IndexOf("ApplySeedSourceReceipt(System, node.Key, ConcreteSource)",
				StringComparison.Ordinal);
			int credit = seedBody.IndexOf("sourceCount > 0 && SeedBySources(",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(gate, 0);
			Assert.Greater(receipt, gate, "all semantic refusals must precede receipt mutation");
			Assert.Greater(credit, receipt, "durable receipt must precede bench credit");

			int apply = source.IndexOf("private static int ApplySeedSourceReceipt(",
				StringComparison.Ordinal);
			int applyEnd = source.IndexOf("private static int SeedSourceCount(", apply,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(apply, 0);
			Assert.Greater(applyEnd, apply);
			string applyBody = source.Substring(apply, applyEnd - apply);
			int merge = applyBody.IndexOf("KingdomResearchRules.TryApplySeedReceipt(",
				StringComparison.Ordinal);
			int changed = applyBody.IndexOf("if (changed)", merge, StringComparison.Ordinal);
			int write = applyBody.IndexOf("The.Game.SetStringGameState(state, updated)", changed,
				StringComparison.Ordinal);
			int reproof = applyBody.IndexOf("The.Game.GetStringGameState(state, \"\")", write,
				StringComparison.Ordinal);
			int publish = applyBody.LastIndexOf("return count;", StringComparison.Ordinal);
			Assert.GreaterOrEqual(merge, 0);
			Assert.Greater(changed, merge);
			Assert.Greater(write, changed);
			Assert.Greater(reproof, write);
			Assert.Greater(publish, reproof);
		}

		[Test]
		public void FounderLedger_ExistsBeforeFoundingAndIsClearedForKingdomSuccession()
		{
			string source = ReadRepoSource("Core/KingdomLoader.cs");
			StringAssert.Contains("[PlayerMutator]", source);
			Assert.GreaterOrEqual(Occurrences(source, "RequireSystem<KingdomSystem>()"), 2,
				"new games and loaded games must both own the player-scoped rite listener before founding");

			source = KingdomSuccessionLogicalSource.Read();
			int reset = source.IndexOf("private static bool TryResetPersonalKnowledge", StringComparison.Ordinal);
			int resetEnd = source.IndexOf("\n\t\tprivate static void RevealRealmGround", reset,
				StringComparison.Ordinal);
			Assert.Greater(resetEnd, reset);
			string body = source.Substring(reset, resetEnd - reset);
			StringAssert.Contains("GetStringGameState(KingdomResearch.FounderRiteState", body);
			StringAssert.Contains("SetStringGameState(KingdomResearch.FounderRiteState, \"\")", body);
			StringAssert.Contains("SetStringGameState(KingdomResearch.FounderRiteState, founderRites)", body,
				"a later honesty-reset failure must restore the old founder's rite ledger");
		}

		private static int Occurrences(string text, string value)
		{
			int count = 0;
			int at = 0;
			while ((at = text.IndexOf(value, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += value.Length;
			}
			return count;
		}

		private static string ReadRepoSource(string relative)
		{
			if (string.Equals(relative, "Growth/KingdomResearch.cs", StringComparison.Ordinal))
				return KingdomResearchLogicalSource.Read();
			return TestMain.ReadRepositoryText(relative);
		}
	}
}
#endif
