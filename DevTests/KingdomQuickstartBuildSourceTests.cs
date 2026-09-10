#if TAF_TESTS
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source contracts for the quickstart-build native phase; these do not execute
	/// native fixtures. Pins that the harness drives the exact three-call production
	/// commissioning sequence the Charter UI itself drives for a plotted design
	/// (Core/KingdomCharterPart.Commission.cs:70-105) in that order, never binds a survey or
	/// mints stock, and never reads a GameObject's minting .ID (only the non-minting
	/// IDIfAssigned) anywhere in the build census.</summary>
	[TestFixture]
	public sealed class KingdomQuickstartBuildSourceTests
	{
		private const string Test = "Harness/KingdomQuickstartBuildTest.cs";
		private const string Census = "Harness/KingdomQuickstartBuildCensus.cs";
		private const string Request = "Harness/KingdomQuickstartBootRequest.cs";

		[Test]
		public void DrivesTheExactThreeCallCommissioningSequenceInOrderCitingTheCharterUI()
		{
			string source = Read(Test);
			StringAssert.Contains("Core/KingdomCharterPart.Commission.cs:70-105", source);
			string body = Flat(Method(source, "private static string TryRun("));
			Ordered(body,
				"KingdomPlots.TryQuoteCommission(system, Zone, entry, null,",
				"KingdomGrowth.CountStoredWater(Zone)",
				"KingdomMaterials.CanPay(Zone, BuildKey, out string materialBlocker)",
				"KingdomCommission.Commission(system, BuildKey, null,");
		}

		[Test]
		public void NeverBindsASurveyOrMintsStockAnywhereInTheBuildPhase()
		{
			foreach (string path in new[] { Test, Census })
			{
				string source = Read(path);
				StringAssert.DoesNotContain("BindPass(", source);
				StringAssert.DoesNotContain("KingdomSurvey.Take(", source);
				StringAssert.DoesNotContain("GameObject.Create(", source);
			}
		}

		[Test]
		public void CensusReadsIDIfAssignedOnlyNeverTheMintingID()
		{
			string source = Read(Census);
			StringAssert.Contains("IDIfAssigned", source);
			// A bare ".ID" (not ".IDIfAssigned" and not "job.OutputId"/"works.IDIfAssigned"
			// already covered above) would silently allocate an identity while reading it.
			Assert.That(Regex.IsMatch(source, @"(?<!ID)(?<!If)\.ID\b(?!IfAssigned)"), Is.False,
				"KingdomQuickstartBuildCensus.cs reads a minting .ID instead of IDIfAssigned");
		}

		[Test]
		public void BuildVerbIsAThirdSiblingOfBootAndSaveNeverChangingTheirGrammar()
		{
			string source = Read(Request);
			StringAssert.Contains("internal const string BuildVerb = \"quickstart-build\";", source);
			StringAssert.Contains(
				"fields[0] != Verb && fields[0] != SaveVerb && fields[0] != BuildVerb", source);
		}

		[Test]
		public void JournalRowNamesExistVerbatimForEveryBuildOutcome()
		{
			string source = Read(Test);
			foreach (string verb in new[]
			{
				"QUICKSTART-BUILD-BEGIN", "QUICKSTART-BUILD-QUOTE", "QUICKSTART-BUILD-CANPAY",
				"QUICKSTART-BUILD-COMMISSION", "QUICKSTART-BUILD-COMPLETE",
			})
				StringAssert.Contains("\"" + verb + "\"", source);
			StringAssert.Contains("build-refused=true", source);
			StringAssert.Contains("build-refused=false", source);
		}

		[Test]
		public void SameStockpileComparesEagerlyCapturedRefsNeverTheSameLiveObjectTwice()
		{
			string source = Read(Census);
			string body = Flat(Method(source, "internal static bool SameStockpile("));
			StringAssert.Contains("!ReferenceEquals(Before.GroundCell, After.GroundCell)", body);
			StringAssert.Contains("!ReferenceEquals(Before.Inventory, After.Inventory)", body);
			// The capture happens in TakeStock, not lazily via Stockpile.Physics at compare time.
			string take = Flat(Method(source, "internal static bool TakeStock("));
			StringAssert.Contains("Cell groundCell = Stockpile.Physics._CurrentCell;", take);
			StringAssert.Contains("Inventory inventory = Stockpile.Inventory;", take);
		}

		[Test]
		public void SuccessPathReChecksSurveyScopeLiveAfterCensusAfterBeforeReportingSuccess()
		{
			string source = Read(Test);
			string body = Flat(Method(source, "internal static void Run("));
			Ordered(body,
				"string stopStep = TryRun(",
				"if (succeeded && !KingdomQuickstartBuildCensus.SurveyScopeClear())",
				"succeeded = false; refused = true; refusal = \"a survey scope leaked after success\";");
		}

		[Test]
		public void IntermediateStepRowsCarryTheBootOnlyDisclosureToken()
		{
			string source = Read(Test);
			foreach (string call in new[]
			{
				"Append(\"QUICKSTART-BUILD-QUOTE\", quoted, \"boot-only=false; \"",
				"Append(\"QUICKSTART-BUILD-CANPAY\", blocked == null, \"boot-only=false; \"",
				"Append(\"QUICKSTART-BUILD-COMMISSION\", commissioned, \"boot-only=false; \"",
			})
				StringAssert.Contains(call, source);
		}

		[Test]
		public void CaskCustodyIsExclusiveGroundBothSnapshotsWithPhysicsRefCompared()
		{
			string source = Read(Census);
			string tryWater = Flat(Method(source, "internal static bool TryWater("));
			StringAssert.Contains("cask.Physics._InInventory != null || cask.Physics._Equipped != null", tryWater);
			string debit = Flat(Method(source, "internal static bool ExactWaterDebit("));
			StringAssert.Contains("!ReferenceEquals(Before.Physics, After.Physics)", debit);
		}

		[Test]
		public void ChestIsReResolvedByReceiptIdAfterCommissionBeforeTakingItsAfterCensus()
		{
			string source = Read(Test);
			string body = Flat(Method(source, "private static string TryRun("));
			Ordered(body,
				"KingdomCommission.Commission(system, BuildKey, null,",
				"KingdomQuickstartBuildCensus.TryStockpile(Zone, receipt.StockpileObjectId,",
				"out GameObject stockpileAfterCommission,",
				"KingdomQuickstartBuildCensus.TakeStock(Zone, stockpileAfterCommission, false,");
		}

		[Test]
		public void InventoryPartOwnershipIsCheckedAgainstTheExactChestReference()
		{
			string source = Read(Census);
			StringAssert.Contains("!ReferenceEquals(inventory.ParentObject, Stockpile)", source);
		}

		[Test]
		public void ProjectedOutputGroundZoneReferenceIsCheckedNotOnlyXY()
		{
			string source = Read(Census);
			StringAssert.Contains(
				"!ReferenceEquals(works.Physics?._CurrentCell?.ParentZone, Zone)", source);
		}

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }
		private static string Flat(string Source) { return Regex.Replace(Source, @"\s+", " "); }

		private static string Method(string Source, string Signature)
		{
			int start = Source.IndexOf(Signature);
			ClassicAssert.GreaterOrEqual(start, 0, "method signature not found: " + Signature);
			int open = Source.IndexOf('{', start);
			int depth = 0, i = open;
			for (; i < Source.Length; i++)
			{
				if (Source[i] == '{') depth++;
				else if (Source[i] == '}' && --depth == 0) break;
			}
			return Source.Substring(open, i - open + 1);
		}

		private static void Ordered(string Source, params string[] Tokens)
		{
			int at = 0;
			foreach (string token in Tokens)
			{
				int found = Source.IndexOf(token, at);
				ClassicAssert.GreaterOrEqual(found, at, "expected token in order: " + token);
				at = found + token.Length;
			}
		}
	}
}
#endif
