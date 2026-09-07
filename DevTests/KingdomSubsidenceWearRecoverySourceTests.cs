#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source-only wiring contracts; no native recovery, internal callback custody, or save/load proof.</summary>
	[TestFixture]
	public sealed class KingdomSubsidenceWearRecoverySourceTests
	{
		private const string Path = "Growth/KingdomWear.SubsidenceRecovery.cs";
		private const string Entry = "internal static bool TryRecoverBeforeSubsidenceRung(";

		[Test]
		public void SourceContractCaptureRequiresUnplannedSettlingAndRetainsExistingFullIds()
		{
			string body = Method("private static bool TrySubsidenceRecoveryFrame(");
			Ordered(body, "XRLGame game = The.Game;", "KingdomSubsidenceStepCodec.TryEncode(expected, out string wire)",
				"expected.Active.Phase != KingdomSubsidenceStepPhase.Settling",
				"expected.Active.RungModel != KingdomSubsidenceStepRules.UnplannedRungs",
				"City = system.City", "Token = system.MasterAppliedResumeToken", "foreach (GameObject work in survey.Built)",
				"!ids.Add(work.IDIfAssigned)", "Body = work, Id = work.IDIfAssigned",
				"KingdomSubsidenceRules.RollRuin(expected.SettlementId, work.IDIfAssigned,",
				"(ulong)expected.Active.DueTick, expected.Active.FromStage)", "SubsidenceRecoveryOwnerExact(value)");
		}

		[Test]
		public void SourceContractOwnerProofPinsParentBytesTokenAndOriginalSurveySlots()
		{
			Has(Method("private static bool SubsidenceRecoveryOwnerExact("), "ReferenceEquals(The.Game, frame.Game)",
				"ReferenceEquals(frame.Game.GetSystem<KingdomSystem>(), frame.System)", "!frame.System.Founded",
				"ReferenceEquals(frame.System.City, frame.City)", "frame.System.CurrentRealmId != frame.Realm",
				"frame.City.SettlementId != frame.Settlement", "MasterAppliedResumeToken != frame.Token",
				"frame.City.SubsidenceModel != frame.Wire", "HasValidSubsidenceStorage()",
				"ClaimedZones.Contains(frame.ZoneId)", "frame.Survey.Ground != frame.Zone",
				"KingdomSurvey.ActiveFor(frame.Zone) != frame.Survey", "frame.Survey.Built.Count != frame.Works.Count",
				"ReferenceEquals(frame.Works[i].Body, frame.Survey.Built[i])");
		}

		[Test]
		public void SourceContractWorkProofKeepsOriginalObjectCellAndFrozenRungBarrier()
		{
			Has(Method("private static bool SubsidenceRecoveryExact("), "SubsidenceRecoveryOwnerExact(frame)",
				"GameObject.Validate(work.Body)", "work.Body.IDIfAssigned == work.Id",
				"work.Body.Blueprint == work.Blueprint", "work.Body.CurrentZone == frame.Zone",
				"work.Body.CurrentCell == work.Cell", "work.Cell.ParentZone == frame.Zone",
				"work.Cell.X == work.X && work.Cell.Y == work.Y", "work.Body.InInventory == null",
				"!KingdomSubsidenceRungRuntime.BlocksWork(work.Body)");
		}

		[Test]
		public void SourceContractOnlySelectedWorksResumeExistingDamageAndBoundLeaks()
		{
			string body = Method(Entry);
			Ordered(body, "if (!work.Selected) continue;", "ReadSubsidenceRecoveryWear(work,",
				"InvokeSubsidenceRecovery(frame, work, () => ResolveSafeReceipts(",
				"wear.IncidentPhase == (int)KingdomWearIncidentPhase.Complete",
				"() => ApplyDamageIncident(", "wear.IncidentId)",
				"wear.LeakPhase == (int)KingdomWearLeakPhase.Bound",
				"() => ContinueBoundLeak(", "RecoverSubsidenceRepair(frame, work, out refusal)", "if (gang != null)");
		}

		[Test]
		public void SourceContractFirstGangIsFrozenBeforeSelectionAndAdvancesAtMostOnce()
		{
			string body = Method(Entry);
			Ordered(body, "SubsidenceRecoveryWork gang = null;", "ReadSubsidenceRecoveryJob(frame, work,",
				"if (gang == null", "wear.RepairEffortLeft > 0", "KingdomConstructionRules.FullyFundedExact(job))) gang = work;",
				"if (!work.Selected) continue;", "if (gang != null)", "!gang.Selected && !RecoverSubsidenceRepair(",
				"if (!Enabled)", "ReadSubsidenceRecoveryJob(frame, gang,", "SubsidencePaidRepair(frame, gang, job)",
				"KingdomMaterialRules.FreeHands(system.Population, system.AssignedCrew)",
				"() => AdvanceRepair(system, gang.Body, wear, hands, The.Game.TimeTicks)");
			ClassicAssert.AreEqual(1, Regex.Matches(body, @"\bAdvanceRepair\(").Count);
			ClassicAssert.AreEqual(1, Regex.Matches(body, @"\bgang = work;").Count);
		}

		[Test]
		public void SourceContractEveryOwnerHelperIsBracketedByReproofEvenWhenItThrows()
		{
			Ordered(Method("private static bool InvokeSubsidenceRecovery("), "!SubsidenceRecoveryExact(frame, work)",
				"ReadSubsidenceRecoveryWear(work, out r_KingdomWear before)", "try { callback(); } finally",
				"SubsidenceRecoveryExact(frame, work)", "ReadSubsidenceRecoveryWear(work, out r_KingdomWear after)",
				"ReferenceEquals(before, after) || allowRemovedWear && before != null && after == null", "return exact;");
			StringAssert.Contains("catch (Exception) { refusal = SubsidenceRecoveryWait; return false; }", Method(Entry));
		}

		[Test]
		public void SourceContractRepairDelegatesOnlyPaidCurrentJobAndRereadsAfterInspection()
		{
			Ordered(Method("private static bool RecoverSubsidenceRepair("), "ReadSubsidenceRecoveryJob(frame, work,",
				"!SubsidenceReceiptsClear(wear)", "!SubsidencePaidRepair(frame, work, job)", "SubsidenceConstructionWait",
				"KingdomConstructionRules.ResumeAction(job)", "() => InspectConstruction(",
				"ReadSubsidenceRecoveryJob(frame, work, out job)", "KingdomConstructionResumeAction.RetryProjection",
				"!SubsidencePaidRepair(frame, work, job)", "() => RetryConstruction(");
			Has(Method("private static bool SubsidencePaidRepair("), "job.Route == KingdomConstructionRoute.WearRepair",
				"!KingdomConstructionRules.IsTerminal(job.Phase)", "KingdomConstructionRules.FullyFundedExact(job)",
				"job.X == work.X && job.Y == work.Y", "RepairSubjectExact(frame.System, frame.Zone, work.Body, job)");
		}

		[Test]
		public void SourceContractKeyedLookupProvesFiveTableShapeBeforeExistingRegistryLookup()
		{
			string body = Method("private static bool ReadSubsidenceRecoveryJob(");
			foreach (string table in new[] { "String", "Int", "Int64", "Object", "Boolean" })
				Has(body, "game." + table + "GameState == null", "game.Has" + table + "GameState(key)");
			Ordered(body, "!SubsidenceRecoveryExact(frame, work)", "KingdomConstruction.RegistryStateKey",
				"KingdomScenarioStateShape.TryAuthorityText(", "work.Body.GetStringProperty(KingdomConstruction.ReceiptProperty)",
				"KingdomConstruction.TryFind(receipt, out job)", "return SubsidenceRecoveryExact(frame, work);");
		}

		[Test]
		public void SourceContractSuccessRequiresEverySelectedReceiptClearAndConstructionAvailable()
		{
			string body = Method(Entry);
			int start = body.LastIndexOf("foreach (SubsidenceRecoveryWork work in frame.Works)", StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0);
			Ordered(body.Substring(start), "if (!work.Selected) continue;", "!SubsidenceRecoveryExact(frame, work)",
				"!SubsidenceReceiptsClear(wear) || wear.RepairEffortLeft != 0",
				"KingdomSubsidenceRungRuntime.ConstructionAvailable(work.Body)", "SubsidenceConstructionWait",
				"!SubsidenceRecoveryOwnerExact(frame)", "refusal = null; return true;");
			Has(Method("private static bool ReadSubsidenceRecoveryWear("), "copies == (wear == null ? 0 : 1)",
				"wear.ParentObject == work.Body", "!wear.LifecycleQuarantined", "KingdomWearIncidentPhase.Quarantined",
				"KingdomWearLeakPhase.Quarantined");
		}

		[Test]
		public void SourceContractWrapperNeverStartsFundingConstructionNewWearOrAWholePass()
		{
			foreach (string banned in new[] { "TryFundNew(", "TryResumeFunding(", "StartRepair(", "RollWear(",
				"BindLeak(", "LeakWater(", "LeakCharge(", "RequirePart<", "GameObject.Create(", "NewJob(",
				"OnSettlementPass(", "OnZoneActivated(", "KingdomCity.CheckIn(", "TryFreezeRungPlan(", ".ID;", ".ID =" })
				StringAssert.DoesNotContain(banned, Source());
		}

		private static string Source()
		{
			string text = Regex.Replace(TestMain.ReadRepositoryText(Path), @"//[^\r\n]*|/\*[\s\S]*?\*/", " ");
			return Regex.Replace(text, @"\s+", " ");
		}
		private static string Method(string signature)
		{
			string text = Source(); int start = text.IndexOf(signature, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, signature);
			int open = text.IndexOf('{', start), depth = 0;
			for (int i = open; i < text.Length; i++)
			{
				if (text[i] == '{') depth++;
				else if (text[i] == '}' && --depth == 0) return text.Substring(open, i - open + 1);
			}
			Assert.Fail("Unclosed method: " + signature); return null;
		}
		private static void Has(string source, params string[] needles)
		{ foreach (string needle in needles) StringAssert.Contains(needle, source); }
		private static void Ordered(string source, params string[] needles)
		{
			int prior = -1;
			foreach (string needle in needles)
			{
				int at = source.IndexOf(needle, prior + 1, StringComparison.Ordinal);
				ClassicAssert.Greater(at, prior, needle); prior = at;
			}
		}
	}
}
#endif
