#if TAF_TESTS
using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source contracts only: these do not execute a popup, engine owner, or native reset.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceHomecomingSourceTests
	{
		private const string Runtime = "Growth/KingdomSubsidenceStepRuntime.Homecoming.cs";
		private const string Civic = "Core/KingdomCharterPart.Civic.cs";
		private const string Options = "Growth/KingdomSubsidenceStepRuntime.Options.cs";
		private const string Entry = "internal static bool TryReadHomecoming(";

		[Test]
		public void SourceContract_CharterDelegatesDisplayAndDoesNotResetUnconditionally()
		{
			string body = Body(Civic, "public void ShowHomecoming(");
			ContainsAll(body, "if (!KingdomSubsidenceStepRuntime.TryReadHomecoming(System,",
				"text => Popup.Show(text), out string refusal)) Popup.Show(refusal);");
			StringAssert.DoesNotContain(".Reset(", body);
			StringAssert.DoesNotContain("HomecomingDays =", body);
			StringAssert.DoesNotContain(".Digest(", body);
		}

		[Test]
		public void SourceContract_NoNewsRequiresNoArchivedFailureAndDoesNotReset()
		{
			string body = Body(Runtime, Entry);
			Ordered(body, "if (!ledger.Any && failures == KingdomSubsidenceReportArchive.None && noticeWarning.Length == 0 && departureWarnings.Length == 0)",
				"show(\"Nothing has happened here since you last stood on this ground.\");",
				"if (!HomecomingExact(frame)) return false;", "refusal = null; return true;",
				"string digest = ledger.Digest(system.SeatName, frame.Days);");
		}

		[Test]
		public void SourceContract_SnapshotFreezesAllResetCarriersWithoutNormalization()
		{
			string body = Body(Runtime, Entry);
			Ordered(body, "!TryOptionFrame(system, out OptionFrame owner)", "KingdomLedger ledger = system.Ledger;",
				"ledger.BrinkLines == null || ledger.ExpeditionLines == null",
				"ledger.BrinkLines.Count > KingdomLedger.MaxBrinkLines",
				"ledger.ExpeditionLines.Count > Simulation.City.KingdomJobRules.MaxOpenJobs",
				"system.HomecomingDays < 0", "KingdomSubsidenceReportRules.TryHash(ledger.Notes, out string noteHash)",
				"HomecomingFrame frame = new HomecomingFrame",
				"Notes = ledger.Notes, NoteCount = ledger.Notes.Count", "NoteHash = noteHash, Days = system.HomecomingDays",
				"Counts = HomecomingCounts(ledger)", "Brinks = ledger.BrinkLines, BrinkValues = ledger.BrinkLines.ToArray()",
				"Expeditions = ledger.ExpeditionLines, ExpeditionValues = ledger.ExpeditionLines.ToArray()");
			StringAssert.DoesNotContain(".Normalize(", Read(Runtime));
		}

		[Test]
		public void SourceContract_EveryResetCounterHasAnExactTransientSnapshot()
		{
			string reset = Body("Core/KingdomLedger.cs", "public void Reset(");
			string[] cleared = Regex.Matches(reset, @"\b(\w+)\s*=\s*0;")
				.Cast<Match>().Select(match => match.Groups[1].Value).OrderBy(value => value).ToArray();
			string[] captured = Regex.Matches(Body(Runtime, "private static int[] HomecomingCounts("), @"\bledger\.(\w+)")
				.Cast<Match>().Select(match => match.Groups[1].Value).OrderBy(value => value).ToArray();
			ClassicAssert.Greater(cleared.Length, 0);
			CollectionAssert.AreEqual(cleared, captured);
			ContainsAll(Body(Runtime, "private static bool HomecomingExact("),
				"int[] counts = HomecomingCounts(frame.Ledger);",
				"if (counts[i] != frame.Counts[i]) return false;");
		}

		[Test]
		public void SourceContract_ExactProofCoversOwnerTokenWireListsCountsHashesAndDays()
		{
			ContainsAll(Body(Runtime, "private static bool HomecomingExact("),
				"!OptionExact(frame.Owner)", "ReferenceEquals(frame.Owner.System.Ledger, frame.Ledger)",
				"ReferenceEquals(frame.Ledger.Notes, frame.Notes)",
				"ReferenceEquals(frame.Ledger.BrinkLines, frame.Brinks)",
				"ReferenceEquals(frame.Ledger.ExpeditionLines, frame.Expeditions)",
				"frame.Owner.System.HomecomingDays != frame.Days", "frame.Notes.Count != frame.NoteCount",
				"KingdomSubsidenceReportRules.TryHash(frame.Notes, out string hash)",
				"string.Equals(hash, frame.NoteHash, StringComparison.Ordinal)",
				"HomecomingListExact(frame.Brinks, frame.BrinkValues)",
				"HomecomingListExact(frame.Expeditions, frame.ExpeditionValues)");
			ContainsAll(Body(Runtime, "private static bool HomecomingListExact("),
				"current.Count != prior.Length", "string.Equals(current[i], prior[i], StringComparison.Ordinal)");
			ContainsAll(Body(Options, "private static bool OptionExact("),
				"OptionSeatExact(frame)", "frame.Owner.City.SubsidenceModel == frame.Owner.Wire",
				"frame.Owner.City.HasValidSubsidenceStorage()");
			ContainsAll(Body(Options, "private static bool OptionSeatExact("),
				"ReferenceEquals(The.Game, frame.Game)", "ReferenceEquals(frame.Game.GetSystem<KingdomSystem>(), frame.System)",
				"ReferenceEquals(frame.System.City, frame.Owner.City)", "frame.System.CurrentRealmId == frame.Realm",
				"KingdomChronicle.SettlementId(frame.System) == frame.Settlement",
				"frame.System.MasterAppliedResumeToken == frame.Token");
		}

		[Test]
		public void SourceContract_DigestAndDisplayCallbacksBothPrecedeReproofAndSettlement()
		{
			Ordered(Body(Runtime, Entry), "if (!HomecomingExact(frame)) return false;",
				"KingdomSubsidenceReportArchive.Digest(owner.Owner.Step)", "if (!HomecomingExact(frame)",
				"string digest = ledger.Digest(system.SeatName, frame.Days);",
				"if (!HomecomingExact(frame)) return false;", "show(digest);", "if (!HomecomingExact(frame)",
				"TryPrepareHomecoming(owner.Owner.Step, frame.Notes, out KingdomSubsidenceStepBook next)");
		}

		[Test]
		public void SourceContract_OnlyExactAfterProvesIntentOtherwiseResetFreezesLoss()
		{
			Ordered(Body(Runtime, "private static bool TrySettleHomecomingReport("),
				"LedgerAction(report, intent, notes) == KingdomSubsidenceEffectAction.Confirm",
				"? KingdomSubsidenceReportRules.TryProveLedger(report, intent, notes, out settled)",
				": KingdomSubsidenceReportRules.TryLoseLedger(report, intent, notes, true, out settled)",
				"return ready && KingdomSubsidenceReportCodec.TryEncode(settled, out next);");
			StringAssert.DoesNotContain(".Note(", Read(Runtime));
			StringAssert.DoesNotContain("TryProveChronicle", Read(Runtime));
		}

		[Test]
		public void SourceContract_NoReportIsNotAnIntentAndEachRealReportHasOneExactOwnerFrontier()
		{
			Ordered(Body(Runtime, "private static bool TrySettleHomecomingReport("), "next = wire;",
				"wire == KingdomSubsidenceBatchRules.NoReport || wire == KingdomSubsidenceBatchRules.PendingReport",
				"KingdomSubsidenceReportCodec.TryDecode(wire, out KingdomSubsidenceReportPlan report)",
				"report.OwnerId != ownerId || report.RealmId != book.RealmId",
				"report.SettlementId != book.SettlementId", "int intent = -1;",
				"report.Entries[i].LedgerPhase == ReportLedgerPhase.Intent",
				"if (intent != -1) return false;", "intent = i;", "if (intent == -1) return true;");
		}

		[Test]
		public void SourceContract_BothReportsArePreparedAndValidatedBeforeOneBookPublication()
		{
			string prepare = Body(Runtime, "private static bool TryPrepareHomecoming(");
			Ordered(prepare, "next = null;", "!KingdomSubsidenceStepRules.Valid(book)", "if (book.Active != null)",
				"TrySettleHomecomingReport(book.Active.RungReportModel, book.Active.Id, book,",
				"value.With(book.Active.Copy(rungReportModel: rung), book.Sequence)",
				"if (book.BatchModel != KingdomSubsidenceBatchRules.None)",
				"KingdomSubsidenceBatchCodec.TryDecode(book.BatchModel, out KingdomSubsidenceBatch batch)",
				"if (batch.Closing)", "TrySettleHomecomingReport(batch.ReportModel, batch.Id, book, notes,",
				"KingdomSubsidenceBatchCodec.TryEncode(batch.Copy(reportModel: report), out string wire)",
				"value.WithBatch(wire)", "!KingdomSubsidenceStepRules.Valid(value)", "next = value; return true;");
			StringAssert.DoesNotContain("SaveOption", prepare);
			Ordered(Body(Runtime, Entry), "TryPrepareHomecoming(", "KingdomSubsidenceStepRules.Valid(next)",
				"KingdomSubsidenceStepCodec.TryEncode(next, out string wire)", "!HomecomingExact(frame)",
				"if (wire != owner.Owner.Wire && !SaveOption(owner, next)) return false;");
			ClassicAssert.AreEqual(1, Regex.Matches(Read(Runtime), @"\bSaveOption\(").Count);
		}

		[Test]
		public void SourceContract_OnlyShownExactArchiveIsAcknowledgedWithoutClaimingDelivery()
		{
			Ordered(Body(Runtime, Entry), "string failures = owner.Owner.Step.FailureModel;",
				"KingdomSubsidenceReportArchive.Digest(owner.Owner.Step)", "if (!HomecomingExact(frame) || warnings == null",
				"(failures == KingdomSubsidenceReportArchive.None) != (warnings.Length == 0)",
				"digest += warnings + noticeWarning + departureWarnings;",
				"show(digest);", "if (!HomecomingExact(frame)", "TryPrepareHomecoming(",
				"if (failures != KingdomSubsidenceReportArchive.None)",
				"next = next.WithFailures(KingdomSubsidenceReportArchive.None);", "!SaveOption(owner, next)");
			ClassicAssert.AreEqual(1, Regex.Matches(Read(Runtime), @"\bWithFailures\(").Count);
			ContainsAll(Body("Growth/KingdomSubsidenceReportArchive.cs", "internal static string Digest("),
				"Unconfirmed subsidence reports", "Reading acknowledges this saved warning; delivery is not claimed.");
			StringAssert.DoesNotContain("WithBatch(KingdomSubsidenceBatchRules.None)", Read(Runtime));
			StringAssert.DoesNotContain("With(null", Read(Runtime));
		}

		[Test]
		public void SourceContract_FinalBarrierHasNoCallbackBeforeCapturedLedgerReset()
		{
			string body = Body(Runtime, Entry);
			ContainsAll(body, "if (!HomecomingExact(frame)) return false; "
				+ "// Reset is non-virtual BCL bookkeeping; no external callback follows this barrier. "
				+ "system.ResidentDepartureCapacityWarnings = acknowledgedDepartures; "
				+ "ledger.Reset(); system.HomecomingDays = 0; refusal = null; return true;");
			ClassicAssert.AreEqual(1, Regex.Matches(Read(Runtime), @"\bledger\.Reset\(").Count);
			StringAssert.DoesNotContain("system.Ledger.Reset", Read(Runtime));
			StringAssert.DoesNotContain(".Clear(", Read(Runtime));
		}

		[Test]
		public void SourceContract_RefusalsRetainEvidenceAndNoOtherDomainIsMutated()
		{
			string source = Read(Runtime);
			Ordered(Body(Runtime, Entry), "refusal = \"The homecoming report changed or its saved account could not be proved.",
				"try", "catch (Exception) { return false; }");
			foreach (string forbidden in new[] { "KingdomChronicle.", "TryRetire(", "TryCancel(", "TryAdmit(",
				"LastSubsidenceTick =", "TimeTicks =", "SetStringGameState(", "RemoveGameState(",
				".Destroy(", ".Obliterate(", "KingdomGovernanceScope." })
				StringAssert.DoesNotContain(forbidden, source);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Body(string path, string signature)
		{
			string source = Read(path);
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, signature);
			int open = source.IndexOf('{', start), depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0)
					return Regex.Replace(source.Substring(start, i - start + 1), @"\s+", " ");
			}
			Assert.Fail("Unclosed source method: " + signature); return null;
		}
		private static void ContainsAll(string source, params string[] tokens)
		{
			foreach (string token in tokens) StringAssert.Contains(token, source);
		}
		private static void Ordered(string source, params string[] tokens)
		{
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(token, cursor, StringComparison.Ordinal);
				ClassicAssert.GreaterOrEqual(at, cursor, "Missing or reordered source contract: " + token);
				cursor = at + token.Length;
			}
		}
	}
}
#endif
