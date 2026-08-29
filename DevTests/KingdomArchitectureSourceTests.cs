#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomArchitectureSourceTests
	{
		private static string Loader()
		{
			return KingdomArchitectureLogicalSource.Read();
		}

		[Test]
		public void LogicalLoaderKeepsDeclarationAbiAndAuthorityOrder()
		{
			string source = Loader();
			Assert.AreEqual(1, Occurrences(source,
				"public sealed class KingdomArchitectureFault"));
			Assert.AreEqual(1, Occurrences(source,
				"public sealed class KingdomArchitectureMapping"));
			Ordered(source,
				"public sealed class KingdomArchitectureFault",
				"public string Name { get; private set; }",
				"public string Message { get; private set; }",
				"internal KingdomArchitectureFault(string Name, string Message)",
				"public sealed class KingdomArchitectureMapping",
				"private readonly string[] variantKeys;",
				"public string BuildKey { get; private set; }",
				"public string DefaultPaletteKey { get; private set; }",
				"internal KingdomArchitectureMapping(",
				"Array.Sort(variantKeys, StringComparer.Ordinal);",
				"private class RawRecord",
				"private sealed class LoadState",
				"private static LoadState state = new LoadState();",
				"public static void Reload(",
				"private static void LoadXml(",
				"private static void HandlePalette(",
				"private static RawPalette GetPalette(",
				"private static void Materialise(",
				"private static bool TryPalette(",
				"private static bool TryRecord(",
				"private static void IndexRecord(",
				"public static bool TryResolve(",
				"private static bool Required(",
				"private static bool TryList(",
				"private static void AddFault(");
		}

		private static void Ordered(string source, params string[] terms)
		{
			int cursor = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], cursor + 1, StringComparison.Ordinal);
				Assert.Greater(next, cursor, terms[i]);
				cursor = next;
			}
		}

		[Test]
		public void LoaderUsesExactUppercaseSchemaOneRoot()
		{
			string source = Loader();
			StringAssert.Contains("DataManager.YieldXMLStreamsWithRoot(\"KingdomArchitectures\")",
				source);
			StringAssert.Contains("{ \"KingdomArchitectures\", delegate(XmlDataHelper root)",
				source);
			StringAssert.Contains("public const int Schema = 1;", source);
			StringAssert.Contains("string schema = Xml.GetAttribute(\"Schema\")", source);
			Assert.IsFalse(source.Contains("YieldXMLStreamsWithRoot(\"kingdomarchitectures\")"));
		}

		[Test]
		public void MergeKeepsOmissionsAndReplacesEachDeclaredRowBlockAtomically()
		{
			string source = Loader();
			StringAssert.Contains("if (Value == null) return; // omission is inheritance across XML streams.",
				source);
			StringAssert.Contains("State.RawPalettes.TryGetValue(Key, out result)", source);
			StringAssert.Contains("State.RawMaps.TryGetValue(Key, out result)", source);
			StringAssert.Contains("State.RawPlans.TryGetValue(Key, out result)", source);
			StringAssert.Contains("Plan.Bindings.TryGetValue(Key, out result)", source);
			StringAssert.Contains("Binding.Tiers.TryGetValue(Key, out result)", source);
			StringAssert.Contains("Records.TryGetValue(Key, out result)", source);

			int rowGuard = source.IndexOf("if (rowBlock)", StringComparison.Ordinal);
			int replace = source.IndexOf("map.Rows = rows;", rowGuard, StringComparison.Ordinal);
			Assert.Greater(rowGuard, 0);
			Assert.Greater(replace, rowGuard);
			Assert.IsFalse(source.Contains("map.Rows.AddRange"),
				"later row blocks must never splice into an older declaration");
		}

		[Test]
		public void ReloadFreezesMergedBuildingsAndResolveNeverRereadsMutableCatalogues()
		{
			string source = Loader();
			StringAssert.Contains(
				"public static void Reload(IEnumerable<KingdomRules.BuildEntry> Buildings)", source);
			StringAssert.Contains("FreezeBuildings(next, Buildings);", source);
			Assert.AreEqual(1, Occurrences(source, "LoadXml(next);"),
				"a reload must enumerate every merge stream exactly once");
			StringAssert.Contains("state = next;", source);
			StringAssert.Contains("does not exist in the frozen KingdomBuildings view", source);
			StringAssert.Contains("authored binding size is smaller than its merged Plot minimum", source);
			StringAssert.Contains("architecture Type does not match its merged Category", source);
			Assert.IsFalse(source.Contains("KingdomData.TryGetBuilding"));
			Assert.IsFalse(source.Contains("KingdomData.Buildings"));

			int resolve = source.IndexOf("public static bool TryResolve(", StringComparison.Ordinal);
			int parsing = source.IndexOf("// --- Attribute parsing and validation", resolve,
				StringComparison.Ordinal);
			Assert.Greater(resolve, 0);
			Assert.Greater(parsing, resolve);
			string frozenPath = source.Substring(resolve, parsing - resolve);
			Assert.IsFalse(frozenPath.Contains("KingdomData"));
			Assert.IsFalse(frozenPath.Contains("KingdomPlots"));
			Assert.IsFalse(frozenPath.Contains("GameObjectFactory"));
			StringAssert.Contains("TryEncodeSnapshot", frozenPath);
			StringAssert.Contains("TryDecodeSnapshot", frozenPath);
		}

		[Test]
		public void ExactDuplicateTiersAreNamedAndNeverIndexedThroughAFallback()
		{
			string source = Loader();
			StringAssert.Contains(
				"BuildKey and typed actual lot are declared by more than one architecture tier", source);
			StringAssert.Contains("string exactKey = ExactRecordKey(tier.BuildKey", source);
			StringAssert.Contains("if (exactCounts[exactKey] != 1)", source);
			StringAssert.Contains("if (TryRecord(State, plan.Key, binding, tier, usedMaps,",
				source);
			StringAssert.Contains("usedPalettes, out record)) IndexRecord(State, record);", source);
			StringAssert.Contains("plot design has no valid authored architecture mapping", source);
			Assert.IsFalse(source.Contains("GenericRectangle"));
			Assert.IsFalse(source.Contains("GenericShell"));
		}

		[Test]
		public void ExactTypedActualLotIndexSupportsLargerAuthoredStakesWithoutNearestFallback()
		{
			string source = Loader();
			StringAssert.Contains("public const int MaxMappings = 512;", source);
			StringAssert.Contains("Dictionary<string, List<ResolvedRecord>> RecordsByBuild", source);
			StringAssert.Contains("Dictionary<string, Dictionary<string, ResolvedRecord>> RecordsByBinding",
				source);
			StringAssert.Contains("public static bool TryGetMapping(string BuildKey, string LotType,",
				source);
			StringAssert.Contains("ArchitectureLotSize ActualLotSize, out KingdomArchitectureMapping Mapping)",
				source);
			StringAssert.Contains("ExactRecordKey(BuildKey, type, ActualLotSize)", source);
			StringAssert.Contains("records.Count != 1", source);
			StringAssert.Contains("if (building.LotSize > Binding.Size)", source);
			Assert.IsFalse(source.Contains("building.LotSize != Binding.Size"));
			StringAssert.Contains("The requested size is identity, not a", source);
			StringAssert.Contains("missing larger map always refuses", source);
		}

		[Test]
		public void LoaderAuditsEveryCommissionableActualSizeAndExcludesRiteOwnedHeartRungs()
		{
			string source = Loader();
			StringAssert.Contains(
				"KingdomPlotRules.HeartRungOf(building.Key) > 0", source);
			StringAssert.Contains(
				"for (int value = (int)building.LotSize;", source);
			StringAssert.Contains(
				"value <= (int)ArchitectureLotSize.Huge; value++", source);
			StringAssert.Contains(
				"State.Records.ContainsKey(ExactRecordKey(", source);
			StringAssert.Contains(
				"commissionable actual lot has no exact valid authored architecture mapping", source);
		}

		[Test]
		public void ExactResolveAndFrozenSameBindingSuccessorCannotRebindThroughCurrentCatalogue()
		{
			string source = Loader();
			StringAssert.Contains("public static bool TryResolve(string BuildKey, string LotType,",
				source);
			StringAssert.Contains("public static bool TryResolveSuccessor(string SuccessorBuildKey, string PlanKey,",
				source);
			StringAssert.Contains("BindingRecordKey(PlanKey, BindingKey, type, ActualLotSize)", source);
			StringAssert.Contains("binding.TryGetValue(SuccessorBuildKey, out record)", source);
			StringAssert.Contains("no valid authored successor ", source);
			StringAssert.Contains("string PredecessorVariantKey, string SuccessorBuildKey", source);
			StringAssert.Contains("TrySelectFrozenSuccessorVariant(", source);

			int successor = source.IndexOf("public static bool TryResolveSuccessor(",
				StringComparison.Ordinal);
			int helper = source.IndexOf("private static bool TryUniqueRecord(", successor,
				StringComparison.Ordinal);
			Assert.Greater(successor, 0);
			Assert.Greater(helper, successor);
			string frozenPath = source.Substring(successor, helper - successor);
			StringAssert.Contains("LoadState frozen = state;", frozenPath);
			StringAssert.Contains("TrySelectVariant(record.Tier.Variants, Context", frozenPath);
			StringAssert.Contains("record.Tier.Variants, PredecessorVariantKey", frozenPath);
			StringAssert.Contains("CompileFrozen(frozen, record, variant, Facing", frozenPath);
			Assert.IsFalse(frozenPath.Contains("KingdomData"));
			Assert.IsFalse(frozenPath.Contains("KingdomPlots"));
			Assert.IsFalse(frozenPath.Contains("GameObjectFactory"));
		}

		[Test]
		public void PaletteKnowledgeAndPowerAreOptionalBoundedAuthoredMetadata()
		{
			string source = Loader();
			StringAssert.Contains("Set(State, slot, \"Knowledge\", Xml.GetAttribute(\"Knowledge\"));",
				source);
			StringAssert.Contains("Set(State, slot, \"Power\", Xml.GetAttribute(\"Power\"));",
				source);
			StringAssert.Contains("string knowledge = Optional(raw, \"Knowledge\");", source);
			StringAssert.Contains("string power = Optional(raw, \"Power\");", source);
			StringAssert.Contains("!ValidOptionalKey(role) || !ValidOptionalKey(knowledge)", source);
			StringAssert.Contains("|| !ValidOptionalKey(power)", source);
			StringAssert.Contains("Knowledge = knowledge, Power = power, Natural = natural", source);
		}

		[Test]
		public void LoaderMergesIdentitySelectorsIntoExistingVariantLane()
		{
			string source = Loader();
			foreach (string name in new[] { "Cultures", "Species", "Genotypes", "Bodies" })
			{
				StringAssert.Contains("Xml.GetAttribute(\"" + name + "\")", source);
				StringAssert.Contains("Set(State, variant, \"" + name + "\"", source);
				StringAssert.Contains("Has(Raw, \"" + name + "\")", source);
				StringAssert.Contains("Optional(Raw, \"" + name + "\")", source);
			}
			Assert.IsFalse(source.Contains("CultureArchitecture"));
			Assert.IsFalse(source.Contains("BodyArchitecture"));
		}

		[Test]
		public void ShippedIdentityArchitectureChangesTopologyAndUsesCorrectSemanticAxis()
		{
			string housing = TestMain.ReadRepositoryText(Path.Combine("Architecture",
				"KingdomArchitectures-HousingWater.xml"));
			StringAssert.DoesNotContain("Creeds=\"Hindren\"", housing,
				"Hindren is a shipped culture/species fact, not a kingdom creed proxy");
			StringAssert.Contains("Cultures=\"Hindren\"", housing);
			StringAssert.Contains("Species=\"hindren\"", housing);
			StringAssert.Contains("Bodies=\"broad-bodied\"", housing);
			StringAssert.Contains("Anchors=\"circulation:broad-turn\"", housing);
			StringAssert.Contains("Map=\"water-reservoir-wet-l0\"", housing);
			StringAssert.Contains("Anchors=\"circulation:water-edge\"", housing);

			string production = TestMain.ReadRepositoryText(Path.Combine("Architecture",
				"KingdomArchitectures-Production.xml"));
			StringAssert.Contains("Bodies=\"robot\"", production);
			StringAssert.Contains("Map=\"production-chargingpost-robot-s0\"", production);
			StringAssert.Contains("Anchors=\"service:robot-bay\"", production);

			string deep = TestMain.ReadRepositoryText(Path.Combine("Architecture",
				"KingdomArchitectures-DeepEndgame.xml"));
			StringAssert.Contains("Genotypes=\"True Kin\"", deep);
			StringAssert.Contains("Map=\"deepend-becomingannexe-truekin-xl0\"", deep);
			StringAssert.Contains("Anchors=\"service:lineage-scan\"", deep);
		}

		private static int Occurrences(string Source, string Fragment)
		{
			int count = 0;
			for (int at = 0; (at = Source.IndexOf(Fragment, at, StringComparison.Ordinal)) >= 0;
				at += Fragment.Length) count++;
			return count;
		}
	}
}
#endif
