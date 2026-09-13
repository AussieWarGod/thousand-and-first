#if TAF_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomReleaseProtocolSourceTests
	{
		[TestCase("KingdomPolityExpressionNativeTests", "LocateBase", "TAF_QUD_BASE")]
		[TestCase("KingdomFrontierWallSourceTests", "LocateBase", "TAF_QUD_BASE")]
		[TestCase("KingdomLiquidRuntimeSourceTests", "LocateAssembly", "TAF_QUD_BASE")]
		[TestCase("KingdomFounderHistoryRulesTests", "LocateDecompiledQud", "TAF_QUD_DECOMPILED")]
		[TestCase("KingdomShopStockSourceTests", "LocateDecompiledQud", "TAF_QUD_DECOMPILED")]
		public void ExplicitBrokenLicensedInputsFailWithoutFallback(string typeName,
			string methodName, string variable)
		{
			Type type = Array.Find(typeof(KingdomReleaseProtocolSourceTests).Assembly.GetTypes(),
				candidate => candidate.Name == typeName);
			ClassicAssert.IsNotNull(type);
			MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
			ClassicAssert.IsNotNull(method);
			string missing = Path.Combine(Path.GetTempPath(), "taf-absent-licensed-" + Guid.NewGuid().ToString("N"));
			ClassicAssert.IsFalse(Directory.Exists(missing));
			string previous = Environment.GetEnvironmentVariable(variable);
			try
			{
				foreach (string value in new[] { " ", missing })
				{
					Environment.SetEnvironmentVariable(variable, value);
					TargetInvocationException failure = Assert.Throws<TargetInvocationException>(
						() => method.Invoke(null, null));
					ClassicAssert.IsInstanceOf<InvalidOperationException>(failure.InnerException);
				}
			}
			finally { Environment.SetEnvironmentVariable(variable, previous); }
		}

		[Test]
		public void PartialMarketSourceDoesNotBorrowFilesFromDefaultRoots()
		{
			string root = Path.Combine(Path.GetTempPath(), "taf-market-source-" + Guid.NewGuid().ToString("N"));
			ClassicAssert.IsFalse(Directory.Exists(root));
			Directory.CreateDirectory(root);
			string previous = Environment.GetEnvironmentVariable("TAF_QUD_DECOMPILED");
			try
			{
				Directory.CreateDirectory(Path.Combine(root, "XRL.UI"));
				string path = Path.Combine(root, "XRL.UI", "TradeUI.cs");
				File.WriteAllText(path, "owned partial source fixture");
				Environment.SetEnvironmentVariable("TAF_QUD_DECOMPILED", root);
				InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
					new KingdomShopStockSourceTests().InstalledQudGroundsPhysicalSourceMarketSinkAndEmptyTrade());
				StringAssert.Contains("AllowTradeWithNoInventoryEvent.cs", failure.Message);
				ClassicAssert.AreEqual("owned partial source fixture", File.ReadAllText(path));
			}
			finally
			{
				Environment.SetEnvironmentVariable("TAF_QUD_DECOMPILED", previous);
				Directory.Delete(root, true);
			}
		}

		[Test]
		public void HostedCiNamesExactLicensedExclusionsForEachSuite()
		{
			string[] portable = {
				"KingdomPolityExpressionNativeTests.EveryResolvedEngineKeyExistsInInstalledQud",
				"KingdomFounderHistoryRulesTests.InstalledQudHasBroadRelicAndDungeonConsumersOfGlobalHistory",
				"KingdomQuickstartTests.InstalledQudGroundAndCarrierBlueprintsMatchTheContract",
				"KingdomShopStockSourceTests.InstalledQudGroundsPhysicalSourceMarketSinkAndEmptyTrade" };
			string[] fullOnly = {
				"KingdomCreedContentTests.Installed21151CensusIsAnExactThirtyThreeAndChiliadAddsNone",
				"KingdomGatehouseNativeTests.GateRootRetainsVanillaDoorPartAndOwnsOnlyTopology",
				"KingdomInheritanceSpatialNativeTests.ReconstructedStreetUsesVanillaPassableDirtPath",
				"KingdomFrontierWallSourceTests.InstalledVanillaParentsProvideExactPaintedWallVocabulary",
				"KingdomGatehouseNativeTests.V2FormsUseOnlyVerifiedWallsAndFunctionalWatchWrappers",
				"KingdomLiquidRuntimeSourceTests.InstalledEngineStillProvidesChosenRenderAndInteractionSeams" };
			string[] full = new string[portable.Length + fullOnly.Length];
			portable.CopyTo(full, 0); fullOnly.CopyTo(full, portable.Length);
			MatchCollection blocks = Regex.Matches(Source(".github/workflows/portable.yml"),
				@"TAF_ALLOWED_SKIPS: >-\r?\n((?:[ ]{12}[^\r\n]+\r?\n)+)");
			ClassicAssert.AreEqual(2, blocks.Count);
			for (int i = 0; i < blocks.Count; i++)
			{
				string[] labels = blocks[i].Groups[1].Value.Split(';');
				for (int j = 0; j < labels.Length; j++) labels[j] = labels[j].Trim();
				CollectionAssert.AreEquivalent(i == 0 ? full : portable, labels);
			}
		}

		private static string Source(string relative)
		{
			return TestMain.ReadRepositoryText(relative);
		}

		private static XmlElement Building(string key)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(TestMain.ReadRepositoryText("KingdomBuildings.xml"));
			XmlElement found = null;
			int matches = 0;
			foreach (XmlElement building in document.GetElementsByTagName("building"))
			{
				if (!string.Equals(building.GetAttribute("Key"), key,
					StringComparison.Ordinal)) continue;
				found = building;
				matches++;
			}
			ClassicAssert.AreEqual(1, matches, "base catalogue must declare exact key once: " + key);
			return found;
		}

		[Test]
		public void ProtocolUsesLiveLarderAndContainsNoRemovedCaskRackTerm()
		{
			string protocol = Source("TESTING.md");
			ClassicAssert.IsFalse(Regex.IsMatch(protocol, @"\bcask(?:[\s-]*rack)s?\b",
				RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
				"Production protocol must not name removed cask-rack design.");

			XmlElement larder = Building("larder");
			ClassicAssert.IsTrue(larder.GetAttribute("DisplayName").StartsWith("larder shed",
				StringComparison.Ordinal), larder.GetAttribute("DisplayName"));
			ClassicAssert.AreEqual("4", larder.GetAttribute("Cost"));
			ClassicAssert.AreEqual("1200", larder.GetAttribute("Ticks"));
			ClassicAssert.AreEqual("timber:3", larder.GetAttribute("Materials"));
			StringAssert.Contains("larder shed (Key `larder`; 4 drams and 3 timber available)",
				protocol);
			StringAssert.Contains("Three real timber items reach the stockpile", protocol);
			StringAssert.Contains("Wait 1200 ticks", protocol);
		}

		[Test]
		public void ProtocolUsesCurrentCisternCourtNameAndNumbers()
		{
			string protocol = Source("TESTING.md");
			XmlElement cistern = Building("cistern");
			ClassicAssert.IsTrue(cistern.GetAttribute("DisplayName").StartsWith("cistern court",
				StringComparison.Ordinal), cistern.GetAttribute("DisplayName"));
			ClassicAssert.AreEqual("16", cistern.GetAttribute("Cost"));
			ClassicAssert.AreEqual("3600", cistern.GetAttribute("Ticks"));
			ClassicAssert.IsFalse(protocol.Contains("great cistern"));
			StringAssert.Contains(
				"commission the cistern court (Key `cistern`; 16 drams) and wait 3600 ticks",
				protocol);
		}

		[Test]
		public void TributeLiteralEqualsRuntimeConstant()
		{
			string protocol = Source("TESTING.md");
			string rules = KingdomRulesLogicalSource.Read();
			Match sourceDemand = Regex.Match(rules,
				@"public\s+const\s+int\s+RaidTributeDrams\s*=\s*(\d+)\s*;",
				RegexOptions.CultureInvariant);
			Match protocolDemand = Regex.Match(protocol,
				@"\*\*Pay tribute\*\*\s*\((\d+)\s+drams\)",
				RegexOptions.CultureInvariant);
			ClassicAssert.IsTrue(sourceDemand.Success, "RaidTributeDrams source constant not found.");
			ClassicAssert.IsTrue(protocolDemand.Success, "Protocol tribute literal not found.");
			ClassicAssert.AreEqual(sourceDemand.Groups[1].Value, protocolDemand.Groups[1].Value);
			StringAssert.Contains("exact tribute, envoy", protocol);
		}

		[Test]
		public void CatalogueStepDescribesMergedDataWithoutPinnedCount()
		{
			string protocol = Source("TESTING.md");
			Match step = Regex.Match(protocol, @"(?m)^\| 25c \|.*$",
				RegexOptions.CultureInvariant);
			ClassicAssert.IsTrue(step.Success, "Protocol step 25c not found.");
			ClassicAssert.IsFalse(Regex.IsMatch(step.Value, @"\b\d+\s+entr(?:y|ies)\b",
				RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), step.Value);
			StringAssert.Contains("merged, data-driven view", step.Value);
			StringAssert.Contains("every loaded `<kingdombuildings>` stream", step.Value);
			StringAssert.Contains("extend or override them by key", step.Value);
		}

		[Test]
		public void PlotStepsRequireFrozenAuthoredLotInsteadOfGenericFallback()
		{
			string protocol = Source("TESTING.md");
			StringAssert.Contains("reserves a **typed lot**, not a generic rectangle", protocol);
			StringAssert.Contains("reserved typed lot", protocol);
			StringAssert.Contains("exact authored map", protocol);
			StringAssert.Contains("authored tier", protocol);
			StringAssert.Contains("authored entrance", protocol);
			StringAssert.Contains("authored fixtures", protocol);
			StringAssert.Contains("material palette", protocol);
			StringAssert.Contains("never a row-major furnishing fallback", protocol);
			StringAssert.Contains("furnishings are never spread row-major", protocol);
			ClassicAssert.IsFalse(protocol.Contains("stakes a **rectangle**"));
		}

		[Test]
		public void ReleaseTargetComesFromMetadataAndMatchesCompilerSymbol()
		{
			string metadata = Source(Path.Combine("Tools", "workshop_metadata.py"));
			Match core = Regex.Match(metadata,
				@"GAME_CORE_BUILD\s*=\s*""(\d+)\.(\d+)\.(\d+)\.(\d+)""",
				RegexOptions.CultureInvariant);
			ClassicAssert.IsTrue(core.Success, "Workshop metadata must own one exact core-build target.");
			string version = core.Groups[1].Value + "." + core.Groups[2].Value + "." +
				core.Groups[3].Value + "." + core.Groups[4].Value;
			string symbol = "BUILD_" + core.Groups[1].Value + "_" + core.Groups[2].Value +
				"_" + core.Groups[3].Value;

			StringAssert.Contains(symbol, Source(Path.Combine("DevTests", "refs.rsp")));
			StringAssert.Contains("core build " + version, Source("README.md"));
			StringAssert.Contains("core build " + version,
				Source(Path.Combine("docs", "RELEASING.md")));

			string checker = Source(Path.Combine("Tools", "release-check.sh"));
			StringAssert.Contains("from Tools.workshop_metadata import GAME_CORE_BUILD", checker);
			StringAssert.Contains("[Reflection.AssemblyName]::GetAssemblyName", checker);
			StringAssert.Contains("configured Qud core is", checker);
			StringAssert.Contains("Assembly-CSharp SHA-256", checker);
			StringAssert.Contains("TAF_QUD_BASE_WIN", checker);
			StringAssert.Contains("$env:TAF_QUD_BASE = $env:TAF_QUD_BASE_WIN", checker);
			StringAssert.Contains("render-qud-refs.py",
				Source(Path.Combine("Tools", "gate.sh")));
			StringAssert.Contains("compile_mode baseline",
				Source(Path.Combine("Tools", "gate.sh")));
			StringAssert.Contains("compile_mode compatibility",
				Source(Path.Combine("Tools", "gate.sh")));
			string testRunner = Source(Path.Combine("DevTests", "test.ps1"));
			StringAssert.Contains("Get-Command dotnet -CommandType Application", testRunner);
			StringAssert.Contains("No tests ran", testRunner);
			StringAssert.Contains("exit 127", testRunner);
			StringAssert.Contains("TAF_FORBID_SKIPS", testRunner);
			string testMain = Source(Path.Combine("DevTests", "TestMain.cs"));
			StringAssert.Contains("failure is IgnoreException", testMain);
			StringAssert.Contains("TAF_FORBID_SKIPS", testMain);
			StringAssert.Contains("TAF_ALLOWED_SKIPS", testMain);
			StringAssert.Contains("unauthorized skip", testMain);
			StringAssert.Contains("expected skip did not occur", testMain);
			StringAssert.Contains("TestExecutionContext.IsolatedContext", testMain,
				"direct invocation must isolate NUnit result state per selected case");
			ClassicAssert.AreEqual(2, Regex.Matches(testMain, @"InvokeIsolated\(method,").Count,
				"plain tests and TestCase rows must share the isolated invocation path");
			string workflow = Source(Path.Combine(".github", "workflows", "portable.yml"));
			StringAssert.Contains("TAF_ALLOWED_SKIPS", workflow);
			foreach (string label in new[]
			{
				"KingdomCreedContentTests.Installed21151CensusIsAnExactThirtyThreeAndChiliadAddsNone",
				"KingdomGatehouseNativeTests.GateRootRetainsVanillaDoorPartAndOwnsOnlyTopology",
				"KingdomInheritanceSpatialNativeTests.ReconstructedStreetUsesVanillaPassableDirtPath"
			})
			{
				StringAssert.Contains(label, workflow);
			}
			foreach (string installedTest in new[]
			{
				"KingdomCreedContentTests.cs", "KingdomGatehouseNativeTests.cs",
				"KingdomInheritanceSpatialNativeTests.cs"
			})
			{
				StringAssert.Contains("TAF_QUD_BASE is set but",
					Source(Path.Combine("DevTests", installedTest)));
			}
			StringAssert.Contains("gameAssemblySha256",
				Source(Path.Combine("docs", "RELEASE_EVIDENCE.example.json")));
		}

		[Test]
		public void ReleaseEvidenceBindsEveryNativeAndHumanGateToRetainedArtifacts()
		{
			string metadata = Source(Path.Combine("Tools", "workshop_metadata.py"));
			string example = Source(Path.Combine("docs", "RELEASE_EVIDENCE.example.json"));
			StringAssert.Contains("RELEASE_EVIDENCE_SCHEMA = 6", metadata);
			foreach (string lane in new[]
			{
				"nativeCompileLoad", "architectureGallery", "controllerAndColor",
				"denseCityPerformance", "oneSurveyReceipt", "compatibilityMatrix",
				"numberedProtocols"
			})
			{
				StringAssert.Contains("\"" + lane + "\"", example, lane);
			}
			StringAssert.Contains("\"artifactRef\"", example);
			StringAssert.Contains("\"artifactSha256\"", example);
			StringAssert.Contains("\"passIds\"", example);
			StringAssert.Contains("exact individual TESTING.md IDs", metadata);
			StringAssert.Contains("VERIFICATION_PASS_IDS", metadata);
			StringAssert.Contains("EVIDENCE_ARTIFACT_ROOT", metadata);
			StringAssert.Contains("_safe_evidence_artifact_ref", metadata);
			StringAssert.Contains("artifactSha256 must match retained artifact", metadata);
			foreach (string passId in new[]
			{
				"native-compile-load", "architecture-gallery",
				"controller-color-accessibility", "dense-city-performance",
				"one-survey-receipt", "compatibility-matrix"
			})
			{
				StringAssert.Contains("\"passId\": \"" + passId + "\"", example,
					passId);
				StringAssert.Contains("\"" + passId + "\"", metadata, passId);
			}
			string protocol = Source("TESTING.md");
			foreach (string passId in new[] { "0a", "55f3", "124i" })
			{
				StringAssert.Contains("\"" + passId + "\"", example, passId);
				ClassicAssert.IsTrue(Regex.IsMatch(protocol,
					@"(?m)^\| " + Regex.Escape(passId) + @" \|"), passId);
			}
		}

		[Test]
		public void ProtocolMatchesWorldClockDissentAndProducerTruth()
		{
			string protocol = Source("TESTING.md");
			Match dissent = Regex.Match(protocol, @"(?m)^\| 54c \|.*$");
			ClassicAssert.IsTrue(dissent.Success, "Protocol step 54c not found.");
			StringAssert.Contains("every elapsed world-day", dissent.Value);
			StringAssert.Contains("full nine-world-day response window", dissent.Value);
			ClassicAssert.IsFalse(dissent.Value.Contains("It has not moved"), dissent.Value);

			Match production = Regex.Match(protocol, @"(?m)^\| 90q \|.*$");
			ClassicAssert.IsTrue(production.Success, "Protocol step 90q not found.");
			StringAssert.Contains("air-well field", production.Value);
			ClassicAssert.IsFalse(production.Value.Contains("Raise a reservoir"), production.Value);
		}

		[Test]
		public void PrefetchAndProductionDocsMatchShippedWiring()
		{
			string options = Source("Options.xml");
			string protocol = Source("TESTING.md");
			string api = Source(Path.Combine("docs", "API.md"));
			StringAssert.Contains("ID=\"r_TAF_OptionPrefetch\"", options);
			StringAssert.Contains("ID=\"r_TAF_OptionPrefetch\" DisplayText=", options);
			StringAssert.Contains("Default=\"No\"", Regex.Match(options,
				@"<option\s+ID=""r_TAF_OptionPrefetch""[^>]+>").Value);
			ClassicAssert.IsFalse(protocol.Contains("there is no checkbox for it yet"));
			ClassicAssert.IsFalse(api.Contains("with no line for it in `Options.xml`"));
			StringAssert.Contains("Per-zone water production rates are live", api);
			ClassicAssert.IsFalse(api.Contains("production **rates** stay unwired"));
		}
	}
}
#endif
