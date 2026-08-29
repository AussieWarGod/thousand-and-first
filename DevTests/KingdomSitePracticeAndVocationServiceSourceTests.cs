#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst.Tests;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomSitePracticeAndVocationServiceSourceTests
	{
		[Test]
		public void PureAuthoritiesContainNoPassiveOrRemoteMutationSurface()
		{
			string source = Read("Core/KingdomSitePracticeRules.cs") +
				Read("Core/KingdomVocationServiceRules.cs") +
				Read("Core/KingdomVocationServiceRules.Transaction.cs") +
				Read("Core/KingdomVocationServiceRules.Legacy.cs") +
				Read("Core/KingdomVocationServiceTransactions.cs") +
				Read("Core/KingdomCivicPracticeRuntime.Transactions.cs");
			Assert.IsFalse(source.Contains("Stat.Random"));
			Assert.IsFalse(source.Contains("System.Random"));
			Assert.IsFalse(source.Contains("ZoneManager"));
			Assert.IsFalse(source.Contains("GetZone("));
			Assert.IsFalse(source.Contains("Inventory"));
			Assert.IsFalse(source.Contains("GameObjectFactory"));
			Assert.IsFalse(source.Contains("EndTurn"));
			Assert.IsFalse(source.Contains("UseEnergy"));
			Assert.IsFalse(source.Contains("Journal"));
		}

		[Test]
		public void ExplicitServiceAppendIsCopyFirstBoundedAndZeroEconomy()
		{
			string rules = Read("Core/KingdomVocationServiceRules.Transaction.cs");
			int serve = rules.IndexOf("public static bool TryServe(",
				StringComparison.Ordinal);
			int match = rules.IndexOf("internal static bool TryMatchAvailableOffers(", serve,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(serve, 0);
			Assert.Greater(match, serve);
			string method = rules.Substring(serve, match - serve);
			StringAssert.Contains("CopyBook(book)", method);
			StringAssert.Contains("candidate.Rows.Add", method);
			StringAssert.Contains("candidate.Revision++", method);
			StringAssert.Contains("OutputUnits = 0", method);
			StringAssert.Contains("InputUnits = 0", rules);
			StringAssert.Contains("book.Revision == long.MaxValue", method);
			Assert.IsFalse(method.Contains("OutputUnits = 1"));
			StringAssert.Contains("private static string RequestDigest(", rules);
			Assert.IsFalse(rules.Contains("public static string RequestDigest("));
		}

		[Test]
		public void CurrentCityAdaptersUseExactLoadedOwnersWithoutRemoteLoads()
		{
			string context = Read("Core/KingdomCurrentCityEvidenceRuntime.cs");
			string site = Read("Core/KingdomSitePracticeRuntime.cs");
			string service = Read("Core/KingdomVocationServiceRuntime.cs");
			string all = context + site + service;
			StringAssert.Contains("TryGetCurrentIdentity", context);
			StringAssert.Contains("SettlementIdForOwnedZone", context);
			StringAssert.Contains("Survey.Built", context);
			StringAssert.Contains("KingdomConstruction.TryFind", context);
			StringAssert.Contains("OneExactCityRow", context);
			StringAssert.Contains("FoundingEventID", site);
			StringAssert.Contains("TryCaptureRealmRegistry", site);
			StringAssert.Contains("IsTerminal", site);
			StringAssert.Contains("TryGetCurrentIdentity", service);
			StringAssert.Contains("public static bool TryOpenCurrent", service);
			Assert.IsFalse(service.Contains("public static bool TryDescribeCurrent"));
			StringAssert.Contains("SectionCivicArtifacts", service);
			StringAssert.Contains("memory.TryReadSection", service);
			StringAssert.Contains("KingdomCivicArtifactsStore.ReadForRealm", service);
			StringAssert.Contains("ledger.IdentityBound", service);
			StringAssert.Contains("ledger.RealmId, exactRealmId", service);
			StringAssert.Contains("KingdomCivicArtifactsStore.TryValidateIdentity", service);
			StringAssert.Contains("artifacts.IdentityBound", service);
			StringAssert.Contains("artifacts.RealmId, exactRealmId", service);
			StringAssert.Contains("SupportRoof", service);
			int direct = context.IndexOf("internal static bool TryBuiltWorksReadOnly(",
				StringComparison.Ordinal);
			int common = context.IndexOf("private static bool TryBuiltWorksFrom(", direct,
				StringComparison.Ordinal);
			Assert.That(direct, Is.GreaterThanOrEqualTo(0));
			Assert.That(common, Is.GreaterThan(direct));
			string shelterRead = context.Substring(direct, common - direct);
			StringAssert.Contains("context.Zone.GetObjects()", shelterRead);
			StringAssert.Contains("new BuiltWorkSnapshot", shelterRead);
			Assert.IsFalse(shelterRead.Contains("KingdomSurvey"));
			Assert.IsFalse(shelterRead.Contains("Survey.Take"));
			StringAssert.Contains("TryBuiltWorksReadOnly", service);
			Assert.IsFalse(all.Contains("GetZone("));
			Assert.IsFalse(all.Contains("ZoneManager"));
			Assert.IsFalse(all.Contains("JournalAPI"));
			Assert.IsFalse(all.Contains("AddAccomplishment"));
			Assert.IsFalse(all.Contains("SetStringGameState"));
			Assert.IsFalse(service.Contains("TryCommitSection"));
		}

		[Test]
		public void D12SourcesUseExactPhaseReceiptsAndReproofBeforeC18()
		{
			string adapters = Read("Core/KingdomVocationServiceRuntime.cs");
			string route = Read("Core/KingdomVocationServiceRuntime.Sources.cs");
			string runtime = Read("Core/KingdomVocationServiceRuntime.Transaction.cs");
			string transaction = Read("Core/KingdomVocationServiceTransactions.cs");
			string governance = Read("Core/KingdomGovernance.cs");
			StringAssert.Contains("selectedReceipt", adapters);
			StringAssert.Contains("shelter.WorkReceiptId", adapters);
			StringAssert.Contains("selected.RecognitionId", adapters);
			StringAssert.Contains("route.ReturnReceiptId", route);
			StringAssert.Contains("route.DeliveryReceiptId", route);
			StringAssert.Contains("route.DepartureReceiptId", route);
			StringAssert.Contains("route.OriginId", route);
			StringAssert.Contains("route.DestinationId", route);
			StringAssert.Contains("route.OrderedPath", route);
			StringAssert.Contains("route.Phase", route);
			StringAssert.Contains("route.SegmentIndex", route);
			StringAssert.Contains("shelter.WorkReceiptId", route);
			StringAssert.Contains("recognition.Kind", route);
			StringAssert.Contains("recognition.AttributionName", route);
			StringAssert.Contains("recognition.Source.DeedId", route);
			StringAssert.Contains("recognition.Source.DeedText", route);
			Assert.IsFalse(route.Contains("route.Phase == KingdomPolityRoutePhase.Cancelled"));
			Assert.IsFalse(route.Contains("route.Phase == KingdomPolityRoutePhase.Preparing"));
			int reserve = runtime.IndexOf("KingdomGovernanceScope.TryReserve(",
				StringComparison.Ordinal);
			int pause = runtime.IndexOf("KingdomMaster.NewWorkAllowed(system)",
				StringComparison.Ordinal);
			int reopen = runtime.IndexOf("TryOpenCurrent(system, zone", pause,
				StringComparison.Ordinal);
			int match = runtime.IndexOf("TryMatchAvailableOffers", reopen,
				StringComparison.Ordinal);
			int record = runtime.IndexOf("KingdomVocationServiceTransactions.TryRecordGoverned",
				match, StringComparison.Ordinal);
			int publication = transaction.IndexOf(
				"publication.TryPublish(() => port.TryCommitSection", StringComparison.Ordinal);
			int invoke = governance.IndexOf("if (!publish()) return false;",
				StringComparison.Ordinal);
			int mark = governance.IndexOf("Committed = true", invoke,
				StringComparison.Ordinal);
			Assert.That(reserve, Is.GreaterThanOrEqualTo(0));
			Assert.That(pause, Is.GreaterThan(reserve));
			Assert.That(reopen, Is.GreaterThan(pause));
			Assert.That(match, Is.GreaterThan(reopen));
			Assert.That(record, Is.GreaterThan(match));
			Assert.That(publication, Is.GreaterThanOrEqualTo(0));
			Assert.That(invoke, Is.GreaterThanOrEqualTo(0));
			Assert.That(mark, Is.GreaterThan(invoke));
			StringAssert.Contains("internal static bool TryExecuteCurrent", runtime);
			StringAssert.Contains("synchronous and non-yielding", governance);
			Assert.IsFalse(Read("Core/KingdomCivicPracticeRuntime.UI.cs")
				.Contains("offer.ResultText"), "pre-choice UI must not expose unrecorded result");
			Assert.IsFalse((adapters + route + runtime).Contains("GetZone("));
			Assert.IsFalse((adapters + route + runtime).Contains("ZoneManager"));
		}

		[Test]
		public void D12C18TransactionHasNoProhibitedValueOrSourceMutationApis()
		{
			string code = Read("Core/KingdomVocationServiceRules.cs") +
				Read("Core/KingdomVocationServiceRules.Transaction.cs") +
				Read("Core/KingdomVocationServiceTransactions.cs") +
				Read("Core/KingdomVocationServiceRuntime.Transaction.cs");
			string[] forbidden = { "AwardXP", "NoXP", "Experience", "Inventory",
				"GameObjectFactory", "AddObject", "TakeObject", "UseEnergy", "EndTurn",
				"WaterRitual", "JournalAPI", "AddAccomplishment", "SetStringGameState",
				"PolityLedger =", "Recognitions.Rows.Add", "Built.Add" };
			for (int i = 0; i < forbidden.Length; i++)
				Assert.IsFalse(code.Contains(forbidden[i]), forbidden[i]);
			string transaction = Read("Core/KingdomVocationServiceTransactions.cs");
			int read = transaction.IndexOf("if (!TryRead(port", StringComparison.Ordinal);
			int prepare = transaction.IndexOf("TryPrepareRequest", read, StringComparison.Ordinal);
			int serve = transaction.IndexOf("TryServe", prepare, StringComparison.Ordinal);
			int write = transaction.IndexOf("KingdomCivicPracticeStore.TryWrite", serve,
				StringComparison.Ordinal);
			int cas = transaction.IndexOf("port.TryCommitSection", write,
				StringComparison.Ordinal);
			Assert.That(read, Is.GreaterThanOrEqualTo(0));
			Assert.That(prepare, Is.GreaterThan(read));
			Assert.That(serve, Is.GreaterThan(prepare));
			Assert.That(write, Is.GreaterThan(serve));
			Assert.That(cas, Is.GreaterThan(write));
			StringAssert.Contains("port.TryReadSection", transaction);
			StringAssert.Contains("KingdomCivicPracticeStore.ReadForRealm", transaction);
			Assert.IsFalse(transaction.Contains("internal static bool TryRecord("),
				"no ungoverned C18 mutation entrypoint may remain");
		}

		[Test]
		public void D1ReprovesEvidenceBeforeOpeningC18AndCommitsOneSectionCas()
		{
			string runtime = Read("Core/KingdomCivicPracticeRuntime.cs");
			int freshSurvey = runtime.IndexOf("KingdomSurvey.Take(zone, system)",
				runtime.IndexOf("TryChooseCurrent", StringComparison.Ordinal),
				StringComparison.Ordinal);
			int freshPreview = runtime.IndexOf("TryPreviewCurrent", freshSurvey,
				StringComparison.Ordinal);
			int match = runtime.IndexOf("openedView.Matches", freshPreview,
				StringComparison.Ordinal);
			int transaction = runtime.IndexOf("KingdomCivicPracticeTransactions.TryChoose",
				match, StringComparison.Ordinal);
			Assert.That(freshSurvey, Is.GreaterThanOrEqualTo(0));
			Assert.That(freshPreview, Is.GreaterThan(freshSurvey));
			Assert.That(match, Is.GreaterThan(freshPreview));
			Assert.That(transaction, Is.GreaterThan(match));

			string commit = Read("Core/KingdomCivicPracticeRuntime.Transactions.cs");
			int lease = commit.IndexOf("port.TryReadSection", StringComparison.Ordinal);
			int realmRead = commit.IndexOf("KingdomCivicPracticeStore.ReadForRealm", lease,
				StringComparison.Ordinal);
			int nestedAppend = commit.IndexOf("KingdomSitePracticeRules.TryRead", realmRead,
				StringComparison.Ordinal);
			int encode = commit.IndexOf("KingdomCivicPracticeStore.TryWrite", nestedAppend,
				StringComparison.Ordinal);
			int outerCas = commit.IndexOf("port.TryCommitSection", encode,
				StringComparison.Ordinal);
			Assert.That(lease, Is.GreaterThanOrEqualTo(0));
			Assert.That(realmRead, Is.GreaterThan(lease));
			Assert.That(nestedAppend, Is.GreaterThan(realmRead));
			Assert.That(encode, Is.GreaterThan(nestedAppend));
			Assert.That(outerCas, Is.GreaterThan(encode));
			StringAssert.Contains("SectionCivicPractice", commit);
			StringAssert.Contains("new KingdomCivicPracticeCommitResult(false", commit);
			Assert.IsFalse(commit.Contains("TryServe"));
			Assert.IsFalse(commit.Contains("VocationServices ="));
			Assert.IsFalse(commit.Contains(".Vocation ="));
			Assert.IsFalse((runtime + commit).Contains("EndTurn"));
			Assert.IsFalse((runtime + commit).Contains("UseEnergy"));
		}

		[Test]
		public void CharterViewRendersExactD1ChoicesAndMutatesOnlyAfterChangedReceipt()
		{
			string menu = Read("Core/KingdomCharterMenuRules.cs");
			string charter = Read("Core/KingdomCharterPart.cs");
			string ui = Read("Core/KingdomCivicPracticeRuntime.UI.cs");
			StringAssert.Contains("PracticeAndVocation = 42", menu);
			Assert.AreEqual(1, Count(menu,
				"Read site practice & vocation"), "one Charter route");
			StringAssert.Contains("case KingdomCharterAction.PracticeAndVocation:", menu);
			StringAssert.Contains(
				"KingdomCivicPracticeRuntime.OpenPracticeAndVocation(System, ParentObject);",
				charter);
			StringAssert.Contains("Options: new string[3]", ui);
			StringAssert.Contains("Hotkeys: new char[3] { 'a', 'b', 'x' }", ui);
			StringAssert.Contains("KingdomPresentation.Rich(view.SourceSummary)", ui);
			StringAssert.Contains("KingdomPresentation.Rich(view.FirstReading)", ui);
			StringAssert.Contains("KingdomPresentation.Rich(view.SecondReading)", ui);
			StringAssert.Contains("KingdomPresentation.Rich(view.EvidenceDigest)", ui);

			int practice = ui.IndexOf("private static void OpenPractice(",
				StringComparison.Ordinal);
			int picker = ui.IndexOf("int choice = Popup.PickOption(", practice,
				StringComparison.Ordinal);
			int paused = ui.IndexOf("if (!KingdomMaster.NewWorkAllowed(system))", picker,
				StringComparison.Ordinal);
			int choose = ui.IndexOf("TryChooseCurrent(system, zone, view, choice + 1",
				paused, StringComparison.Ordinal);
			int unchanged = ui.IndexOf("if (!result.Changed)", choose,
				StringComparison.Ordinal);
			int commit = ui.IndexOf("KingdomGovernanceScope.Commit(\"choose civic practice\")",
				unchanged, StringComparison.Ordinal);
			Assert.That(practice, Is.GreaterThanOrEqualTo(0));
			Assert.That(picker, Is.GreaterThan(practice));
			Assert.That(paused, Is.GreaterThan(picker), "paused view renders before refusing work");
			Assert.That(choose, Is.GreaterThan(paused));
			Assert.That(unchanged, Is.GreaterThan(choose));
			Assert.That(commit, Is.GreaterThan(unchanged));
			Assert.AreEqual(1, Count(ui, "TryChooseCurrent("));
			Assert.AreEqual(1, Count(ui, "KingdomGovernanceScope.Commit("));
			Assert.IsFalse(ui.Contains("UseEnergy"));
			Assert.IsFalse(ui.Contains("EndTurn"));
		}

		[Test]
		public void VocationViewReportsFirstThenExplicitlyRecordsOnlyAvailableService()
		{
			string ui = Read("Core/KingdomCivicPracticeRuntime.UI.cs");
			int start = ui.IndexOf("private static void ShowVocation(",
				StringComparison.Ordinal);
			int end = ui.IndexOf("private static string VocationReport(", start,
				StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0));
			Assert.That(end, Is.GreaterThan(start));
			string opener = ui.Substring(start, end - start);
			StringAssert.Contains("KingdomVocationServiceRuntime.TryOpenCurrent", opener);
			StringAssert.Contains("KingdomVocationServiceRules.TryValidateOffer", opener);
			StringAssert.Contains("TryReadCurrentView", opener);
			StringAssert.Contains("int choice = Popup.PickOption(", opener);
			StringAssert.Contains("KingdomMaster.NewWorkAllowed(system)", opener);
			StringAssert.Contains("TryExecuteCurrent(system, zone, offer", opener);
			StringAssert.Contains("if (!result.Changed)", opener);
			Assert.IsFalse(opener.Contains(
				"KingdomGovernanceScope.Commit(\"record vocation service\")"));
			Assert.IsFalse(opener.Contains("TryChooseCurrent"));
			Assert.IsFalse(opener.Contains("TryServe"));
			Assert.IsFalse(opener.Contains("UseEnergy"));
			int reportFirst = opener.IndexOf("string report = VocationReport", StringComparison.Ordinal);
			int picker = opener.IndexOf("int choice = Popup.PickOption(", StringComparison.Ordinal);
			int pause = opener.IndexOf("KingdomMaster.NewWorkAllowed(system)", picker,
				StringComparison.Ordinal);
			int execute = opener.IndexOf("TryExecuteCurrent(system, zone, offer", pause,
				StringComparison.Ordinal);
			Assert.That(picker, Is.GreaterThan(reportFirst));
			Assert.That(pause, Is.GreaterThan(picker));
			Assert.That(execute, Is.GreaterThan(pause));

			string report = ui.Substring(end);
			StringAssert.Contains("KingdomVocationServiceOfferState.Available", report);
			StringAssert.Contains("KingdomVocationServiceOfferState.Unavailable", report);
			StringAssert.Contains("KingdomVocationServiceOfferState.Neutral", report);
			StringAssert.Contains("KingdomVocationServiceActionState.AlreadyRecorded", report);
			StringAssert.Contains("KingdomVocationServiceRules.MaxRowsPerSeries", report);
			StringAssert.Contains("KingdomVocationServiceRules.MaxRows", report);
			StringAssert.Contains("offer.Report", report);
			StringAssert.Contains("offer.SourceReceiptId", report);
			StringAssert.Contains("offer.UnavailableCause", report);
			StringAssert.Contains("offer.Remedy", report);
			StringAssert.Contains("1000-energy Charter action", report);
			StringAssert.Contains("No service action opens; no governance charge applies", report);
			StringAssert.Contains("0 material/value input", report);
			StringAssert.Contains("Cancel, leave, and exact retry are free", report);
			StringAssert.Contains("TryReadRealmResults", report);
			StringAssert.Contains("no Journal entry, item, value, or governance charge", report);
			int closure = report.IndexOf("KingdomPresentation.Rich(offer.Closure)",
				StringComparison.Ordinal);
			int costGate = report.IndexOf(
				"if (offer.State == KingdomVocationServiceOfferState.Available && status != null &&",
				closure,
				StringComparison.Ordinal);
			int charged = report.IndexOf("1000-energy Charter action", costGate,
				StringComparison.Ordinal);
			int noCharge = report.IndexOf("No service action opens; no governance charge applies",
				charged, StringComparison.Ordinal);
			Assert.That(costGate, Is.GreaterThan(closure));
			Assert.That(charged, Is.GreaterThan(costGate));
			Assert.That(noCharge, Is.GreaterThan(charged));
			StringAssert.Contains("This exact retry is read-only and uses no governance charge", report);
			Assert.IsFalse(report.Contains("KingdomGovernanceScope"));
			Assert.IsFalse(report.Contains("TryServe"));
		}

		[Test]
		public void OwnedProductionFilesStayBelowThreeHundredLines()
		{
			string[] paths = {
				"Core/KingdomSitePracticeModels.cs",
				"Core/KingdomSitePracticeRules.cs",
				"Core/KingdomSitePracticeRuntime.cs",
				"Core/KingdomVocationServiceModels.cs",
				"Core/KingdomVocationServiceRules.cs",
				"Core/KingdomVocationServiceCodec.cs",
				"Core/KingdomVocationServiceRules.Status.cs",
				"Core/KingdomVocationServiceRules.Transaction.cs",
				"Core/KingdomVocationServiceRules.Legacy.cs",
				"Core/KingdomVocationServiceRuntime.cs",
				"Core/KingdomVocationServiceRuntime.Sources.cs",
				"Core/KingdomVocationServiceRuntime.Transaction.cs",
				"Core/KingdomVocationServiceTransactions.cs",
				"Core/KingdomCurrentCityEvidenceRuntime.cs",
				"Core/KingdomGovernance.cs",
				"Core/KingdomCivicPracticeCodec.cs",
				"Core/KingdomCivicPracticeRuntime.Models.cs",
				"Core/KingdomCivicPracticeRuntime.Transactions.cs",
				"Core/KingdomCivicPracticeRuntime.cs",
				"Core/KingdomCivicPracticeRuntime.UI.cs"
			};
			for (int i = 0; i < paths.Length; i++)
			{
				int lines = Read(paths[i]).Replace("\r\n", "\n").Split('\n').Length;
				Assert.Less(lines, 300, paths[i] + " has " + lines + " lines");
			}
		}

		private static string Read(string path)
		{
			return TestMain.ReadRepositoryText(path);
		}

		private static int Count(string text, string value)
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
	}
}
#endif
