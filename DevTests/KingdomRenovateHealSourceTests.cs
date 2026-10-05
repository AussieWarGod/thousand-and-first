#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// #283 heal route source contracts: session one's harness must compile against a build
	/// WITHOUT the fix, the heal verbs only observe and save, and session two is routed through
	/// the shared load entry exactly like the other resumed cold loads.
	/// </summary>
	[TestFixture]
	public sealed class KingdomRenovateHealSourceTests
	{
		private static string Read(string Path) => TestMain.ReadRepositoryText(Path);

		private static readonly string[] FixSymbols = {
			"HandoverMarksFailure", "HandoverEndpointsFailure", "HandoverEndpointsUnproven",
			"KingdomRetiredHandoverDefect",
			"ReadmittedProperty", "FounderMarks", "LandedScaffoldIdentity", "ClassifyRetiredHandoverDefect",
			"TryReadmitRetiredHandoverDefect", "KingdomConstruction.Readmit", "IsRetiredDefectReadmission",
			"ReadmissionPrefix", "TryLandedScaffoldId" };

		/// <summary>Every harness shard compiles into session one's dev profile, whose production has
		/// none of the A1-A3 symbols; a harness reference to one would make that build refuse.</summary>
		[Test]
		public void NoHarnessShardNamesAFixSymbol()
		{
			string root = TestMain.RepositoryRoot;
			foreach (string file in Directory.GetFiles(Path.Combine(root, "Harness"), "*.cs"))
			{
				string text = File.ReadAllText(file);
				foreach (string symbol in FixSymbols)
					StringAssert.DoesNotContain(symbol, text, Path.GetFileName(file));
			}
			StringAssert.Contains("internal const string ReadmittedMarker = \"r_TAF_ImprovementReadmitted\";",
				Read("Harness/KingdomTierUpgradeAfterWait.cs"));
			StringAssert.Contains("public const string ReadmittedProperty = \"r_TAF_ImprovementReadmitted\";",
				Read("Growth/KingdomUpgrade.27.RetiredDefectReadmission.cs"));
		}

		/// <summary>The heal shards observe and save; the real settlement pass does every handover.</summary>
		[Test]
		public void TheHealShardsDriveNoUpgrade()
		{
			foreach (string path in new[] { "Harness/KingdomTierUpgradeHeal.cs", "Harness/KingdomRenovateHealLoad.cs",
				"Harness/KingdomRenovateHealProvider.cs", "Harness/KingdomTierUpgradeAfterWait.cs" })
			{
				string text = Read(path);
				foreach (string driver in new[] { "HandOver(", "TryApplyUpgrade(", "Begin(", "PollHandover(",
					"Quarantine(", "FinishProjection(", "TryFundNew(", "SetIntProperty(KingdomPlots.YieldingProperty" })
					StringAssert.DoesNotContain(driver, text, path + " drives " + driver);
			}
		}

		[Test]
		public void SessionOneSavesOnlyTheRetiredStall()
		{
			string save = Read("Harness/KingdomTierUpgradeHeal.cs");
			string[] order = { "Require(Armed && !Done && Phase == 4,", "RecordAfterWait();",
				"job.Phase == KingdomConstructionPhase.InspectionRequired", "defect != null",
				"Require(AfterWaitSealFault,", "KingdomRenovateHealSnapshot.TryEncode(", "SaveStuck(wire)" };
			int at = 0;
			foreach (string term in order)
			{
				int found = save.IndexOf(term, at, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, "missing ordered term: " + term);
				at = found + term.Length;
			}
			StringAssert.Contains("KingdomUnfoundedSave.SavePrimary(Game, directory)", save);
			StringAssert.Contains("|| KingdomRenovateHealScript.Matches(script)", Read("Harness/KingdomTierUpgradeProvider.cs"));
			StringAssert.Contains("KingdomRenovateHealScript.Matches(script)", Read("Harness/KingdomRenovateHealProvider.cs"));
		}

		[Test]
		public void SessionTwoIsAResumedColdLoadRoutedByItsOwnSnapshot()
		{
			string entry = Read("Harness/KingdomScenarioLoadEntry.cs");
			StringAssert.Contains("KingdomRenovateHealSnapshot.TryDecode(SnapshotWire, out HealSnapshot)", entry);
			StringAssert.Contains("KingdomRenovateHealLoad.Prepare(loaded, priorPopup);", entry);
			StringAssert.Contains("&& !KingdomRenovateHealLoad.OwnsPopups) Popup.Suppress = false;", entry);
			StringAssert.Contains("KingdomRenovateHealLoad.BeforeActivation();", Read("Harness/KingdomScenarioLoadWitness.cs"));
			string load = Read("Harness/KingdomRenovateHealLoad.cs");
			string[] order = { "internal static void BeforeActivation()", "job.Phase == KingdomConstructionPhase.InspectionRequired",
				"internal static void Prepare(", "KingdomRenovateHealScript.Matches(script)",
				"KingdomScenarioAdvance.Run(", "private static void AfterPump(", "HandoverEvidence(",
				"job.Phase == KingdomConstructionPhase.Complete", "KingdomPhysicalLookupState.Absent",
				"KingdomPlots.YieldingProperty) == 1", "Check(!sealFault,", "KingdomUnfoundedSave.SavePrimary(",
				"\"SCRIPT-COMPLETE\"" };
			int at = 0;
			foreach (string term in order)
			{
				int found = load.IndexOf(term, at, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(found, 0, "missing ordered term: " + term);
				at = found + term.Length;
			}
		}
	}
}
#endif
