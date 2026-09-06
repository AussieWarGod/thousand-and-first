#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Source-only wiring contracts. No native execution, custody, save/load, or recovery proof.</summary>
	public sealed class KingdomSubsidenceRungRuntimeSourceTests
	{
		private const string Capture = "Growth/KingdomSubsidenceStepRuntime.RungCapture.cs";
		private const string Frame = "Growth/KingdomSubsidenceStepRuntime.RungFrame.cs";
		private const string Effects = "Growth/KingdomSubsidenceStepRuntime.RungEffects.cs";
		private const string Release = "Growth/KingdomSubsidenceStepRuntime.RungRelease.cs";
		private const string Driver = "Growth/KingdomSubsidenceReleaseDriver.cs";
		private const string Roof = "Growth/KingdomSubsidenceStepRuntime.RungRoof.cs";
		private const string Construction = "Growth/KingdomSubsidenceRungRuntime.Construction.cs";

		[Test]
		public void SourceContractResumeRefusesBeforeOrdinaryCheckIn()
		{
			string body = Method("Core/KingdomSystem.z21.SemanticPass.cs", "private bool AttendSeatedSemantics(");
			Ordered(body, "survey.BindPass()", "if (!KingdomSubsidenceStepRuntime.TryBeforePass(",
				"return false;", "KingdomCity.CheckIn(");
		}

		[Test]
		public void SourceContractCapturePinsOwnerAndRecipientsBeforeNamingCallbacks()
		{
			string body = Method(Capture, "internal static bool TryPrepareRung(");
			Ordered(body, "XRLGame game = The.Game;", "long token = system.MasterAppliedResumeToken;",
				"ReferenceEquals(game.GetSystem<KingdomSystem>(), system)",
				"new List<GameObject>(survey.Built)", "CaptureRungRoofs(system, owner.City, survey, plot, roofs, residents)",
				"work.ShortDisplayName", "Game = game", "Token = token", "TryFreezeRungPlan(");
		}

		[Test]
		public void SourceContractFreezeRequiresOriginalReferencesAndUnchangedRecipientSet()
		{
			string body = Method(Capture, "internal static bool TryPrepareRung(");
			Ordered(body, "TryCaptureGlobalLiveIds(ids, out frame.Subjects)", "!RungOwnerExact(frame)",
				"candidates.Count != survey.Built.Count", "!ReferenceEquals(body, resident.Value)",
				"!ReferenceEquals(candidates[i], survey.Built[i])", "!ReferenceEquals(exact, candidates[i])",
				"!ReferenceEquals(wear, parts[work.ObjectId])", "work.BeforeWear",
				"currentRoofs.Count != work.Roofs.Count", "currentRoofs[i].ResidentId != work.Roofs[i].ResidentId",
				"currentRoofs[i].BodyObjectId != work.Roofs[i].BodyObjectId", "row.Warned != roof.BeforeWarned",
				"TryFreezeRungPlan(", "SaveRung(frame, next)");
			Ordered(Method(Capture, "private static bool CaptureRungRoofs("),
				"city.TryCaptureSubsidenceRoof(", "!ReferenceEquals(prior, body)",
				"residents[body.IDIfAssigned] = body;", "roofs.Add(");
		}

		[Test]
		public void SourceContractWorkCustodyUsesFullIdentityAndRawPropertyPresence()
		{
			string body = Method(Frame, "private static bool RungWorkExact(");
			Has(body, "!RungOwnerExact(frame)", "!GameObject.Validate(work)", "work.IDIfAssigned != row.ObjectId",
				"work.Blueprint != row.Blueprint", "work.CurrentZone != frame.Zone", "work.CurrentCell.ParentZone != frame.Zone",
				"work.CurrentCell.X != row.X", "work.CurrentCell.Y != row.Y", "work.InInventory != null",
				"!work.HasIntProperty(\"KingdomBuilt\")", "work.HasStringProperty(\"KingdomBuilt\")",
				"KingdomPlots.PlotIdProperty", "KingdomUpgrade.BuildKeyProperty", "ReferenceEquals(item, work)",
				"sameId == 1 && sameReference == 1");
			StringAssert.DoesNotContain("row.WorkId", body);
			Has(Method(Frame, "private static bool RawRungProperty("), "!work.HasIntProperty(key)",
				"work.HasStringProperty(key) == (expected != null)", "StringComparison.Ordinal");
		}

		[Test]
		public void SourceContractFramePublicationReprovesExactOwnerAndParentBytes()
		{
			Has(Method(Frame, "private static bool RungOwnerExact("), "ReferenceEquals(The.Game, frame.Game)",
				"ReferenceEquals(frame.Game.GetSystem<KingdomSystem>(), frame.System)",
				"ReferenceEquals(frame.System.City, frame.Owner.City)", "frame.System.CurrentRealmId == frame.Plan.RealmId",
				"frame.Owner.City.SettlementId == frame.Plan.SettlementId", "MasterAppliedResumeToken == frame.Token",
				"ClaimedZones.Contains(frame.Plan.ZoneId)", "KingdomSurvey.ActiveFor(frame.Zone) == frame.Survey",
				"HasValidSubsidenceStorage()", "frame.Owner.City.SubsidenceModel == frame.Owner.Wire");
			Ordered(Method(Frame, "private static bool SaveRung("), "!RungOwnerExact(frame)", "TryReadRungPlan(next,",
				"TryEncode(next,", "Publish(frame.System, frame.Owner, next)", "frame.Owner = new Snapshot(",
				"frame.Plan = plan;", "return RungOwnerExact(frame);");
		}

		[Test]
		public void SourceContractConstructionReadsAllTablesThroughRegistryRulesWithoutMinting()
		{
			string body = Method(Construction, "private static bool ConstructionAvailable(GameObject work, bool requireCell)");
			foreach (string table in new[] { "String", "Int", "Int64", "Object", "Boolean" })
				Has(body, "game." + table + "GameState == null", "game.Has" + table + "GameState(key)");
			Ordered(body, "KingdomConstruction.RegistryStateKey", "KingdomScenarioStateShape.TryAuthorityText(",
				"if (!present) return true;", "KingdomConstructionRules.TryDecode(wire,", "foreach (KingdomConstructionJob job");
			Has(body, "(requireCell && work.CurrentCell == null)");
			Has(Method(Construction, "internal static bool ConstructionAvailable(GameObject work)"),
				"return ConstructionAvailable(work, true);");
			Has(Method(Construction, "internal static bool ConstructionAvailableForRelease(GameObject work)"),
				"return ConstructionAvailable(work, false);");
			foreach (string banned in new[] { "RequireSystem", "KingdomConstruction.TryFind(",
				"ReceiptBlocksCurrent(", "SetStringGameState(", "GetStringGameState(key, \"\")" })
				StringAssert.DoesNotContain(banned, body);
		}

		[Test]
		public void SourceContractConstructionReservesEverySubjectRoleAndSameCell()
		{
			string body = Method(Construction, "private static bool ConstructionAvailable(GameObject work, bool requireCell)");
			Ordered(body, "string receipt = work.GetStringProperty(KingdomConstruction.ReceiptProperty);",
				"if (KingdomConstructionRules.IsTerminal(job.Phase)) continue;", "job.Id == receipt",
				"job.SubjectId == work.IDIfAssigned", "job.SourceId == work.IDIfAssigned",
				"job.OutputId == work.IDIfAssigned", "job.PhysicalItemId == work.IDIfAssigned",
				"job.PhysicalDestinationId == work.IDIfAssigned", "work.CurrentCell != null && job.ZoneId == work.CurrentZone?.ZoneID",
				"job.X == work.CurrentCell.X && job.Y == work.CurrentCell.Y) return false;");
		}

		[Test]
		public void SourceContractWearIntentAndMeasuredProofArePublishedBeforeRoofs()
		{
			string body = Method(Effects, "internal static bool TryResumeRung(");
			Ordered(body, "TryRungFrame(", "frame.Subjects.TryGetValue(work.ObjectId, out GameObject body)",
				"TryArmRungWear(", "SaveRung(frame, intent)", "KingdomSubsidenceWearRuntime.TryApply(",
				"() => RungWorkExact(frame, work, body)", "!ReferenceEquals(body.GetPart<r_KingdomWear>(), measured)",
				"TryProveRungWear(", "measured.Wear", "SaveRung(frame, proved)", "TryArmRungRoof(",
				"SaveRung(frame, intent)", "ApplyRungRoof(",
				"ResumeRungRelease(frame, i, ref refusal)",
				"KingdomSubsidenceRungRules.ReleasedComplete(frame.Plan)");
			Assert.AreEqual(2, Regex.Matches(body, Regex.Escape(
				"ResumeRungRelease(frame, i, ref refusal)")).Count,
				"Both already-proved and newly-proved physical paths must resume the same release driver.");
			Ordered(Method(Release, "private static bool ResumeRungRelease("),
				"RungReleasePort port = new RungReleasePort(frame, index)",
				"KingdomSubsidenceReleaseDriver.Resume(port, index)", "refusal = port.Refusal;", "return false;");
		}

		[Test]
		public void SourceContractReleaseRequiresPositiveLiveIdentityAndUniqueAttachment()
		{
			string observe = Method(Release, "public bool TryObserve(");
			Ordered(observe, "if (!Current", "row.ReleasePhase == KingdomSubsidenceReleasePhase.Released",
				"!Frame.Subjects.TryGetValue(row.ObjectId, out GameObject body)", "!RungReleaseSubjectExact(Frame, row, body)",
				"!RungReleaseAttachmentExact(body, part)", "!ReferenceEquals(Work, body)", "!ReferenceEquals(Wear, part)",
				"Work = body; Wear = part;", "if (Wear.LifecycleQuarantined)", "Refusal =", "return false;",
				"!ReleaseAvailable()", "receipt = new KingdomSubsidenceWearReceipt(",
				"return Current && RungReleaseAttachmentExact(Work, Wear) && ReleaseAvailable();");
			string subject = Method(Release, "private static bool RungReleaseSubjectExact(");
			Ordered(subject, "!RungOwnerExact(frame)", "!ReleaseDesignationExact(row, work)",
				"TryCaptureGlobalLiveIds(ids,", "!live.TryGetValue(row.ObjectId,", "!ReferenceEquals(exact, work)",
				"return RungOwnerExact(frame) && ReleaseDesignationExact(row, work);");
			string designation = Expression(Release, "private static bool ReleaseDesignationExact(");
			Has(designation, "GameObject.Validate(work)", "work.IDIfAssigned == row.ObjectId",
				"work.Blueprint == row.Blueprint", "work.HasIntProperty(\"KingdomBuilt\")",
				"!work.HasStringProperty(\"KingdomBuilt\")", "work.GetIntProperty(\"KingdomBuilt\") == 1",
				"RawRungProperty(work, KingdomPlots.PlotIdProperty, row.PlotId == \"\" ? null : row.PlotId)",
				"RawRungProperty(work, KingdomUpgrade.BuildKeyProperty, row.DesignStamp)");
			Has(Method(Release, "private static bool RungReleaseAttachmentExact("), "wear == null",
				"!ReferenceEquals(work.GetPart<r_KingdomWear>(), wear)", "!ReferenceEquals(wear.ParentObject, work)",
				"work.PartsList[i] is r_KingdomWear", "return copies == 1;");
			Has(Expression(Release, "private bool ReleaseAvailable()"), "!Wear.LifecycleQuarantined",
				"Wear.RepairEffortLeft == 0", "Wear.LeakPhase == (int)KingdomWearLeakPhase.None",
				"KingdomSubsidenceRungRuntime.ConstructionAvailableForRelease(Work)");
			foreach (string banned in new[] { "return true;", "RungWorkExact(", ".CurrentCell", ".CurrentZone", "row.WorkId" })
				StringAssert.DoesNotContain(banned, observe + subject + designation);
		}

		[Test]
		public void SourceContractPublicationReobservesReceiptAndLocalWritesUseOnlyAdmittedSingleField()
		{
			Ordered(Method(Release, "public bool Publish("), "!Current", "!ReferenceEquals(Plan, expected)",
				"!TryObserve(out KingdomSubsidenceWearReceipt measured)", "row.ReleasePhase == KingdomSubsidenceReleasePhase.Intent",
				"Same(measured, row.ReleaseBefore)", "TryArmRungRelease(", "Same(measured, row.ReleaseAfter)",
				"TryProveRungRelease(", "return Current && ReferenceEquals(Plan, expected)",
				"TryEncode(next, out string wire)", "changed.Active.RungModel == wire && SaveRung(Frame, changed)");
			string body = Method(Release, "public bool Write(");
			Ordered(body, "TryObserve(out KingdomSubsidenceWearReceipt measured)", "Same(measured, expected)",
				"row.ReleasePhase != KingdomSubsidenceReleasePhase.Intent", "TryNextWrite(", "admitted != field || field == 4",
				"KingdomSubsidenceReleaseRules.AfterWrite(", "switch (field)",
				"Wear.LastCompletedIncidentId = target.LastCompletedId", "Wear.IncidentPhase = target.Phase",
				"Wear.IncidentId = target.Id", "Wear.IncidentLine = target.Line",
				"return TryObserve(out measured) && KingdomSubsidenceReleaseRules.Same(measured, target);");
			MatchCollection writes = Regex.Matches(body, @"\bWear\.(\w+)\s*=(?!=)");
			string[] fields = new string[writes.Count];
			for (int i = 0; i < writes.Count; i++) fields[i] = writes[i].Groups[1].Value;
			CollectionAssert.AreEqual(new[] { "LastCompletedIncidentId", "IncidentPhase", "IncidentId", "IncidentLine" }, fields);
		}

		[Test]
		public void SourceContractReleasedParentSkipsBothNativeLookupAndDriverObservation()
		{
			Ordered(Method(Frame, "private static bool TryRungFrame("),
				"if (work.ReleasePhase == KingdomSubsidenceReleasePhase.Released) continue;", "ids.Add(work.ObjectId)",
				"candidate.Subjects = new Dictionary<string, GameObject>(StringComparer.Ordinal)",
				"if (ids.Count != 0 && !KingdomPlots.TryCaptureGlobalLiveIds(ids, out candidate.Subjects)) return false;",
				"if (!RungOwnerExact(candidate)) return false;");
			Ordered(Method(Driver, "internal static bool Resume("), "KingdomSubsidenceRungRules.Valid(plan)",
				"plan.Works[index].ReleasePhase == KingdomSubsidenceReleasePhase.Released",
				"return Current(port, plan);", "if (!Observe(port, plan,");
			Assert.AreEqual("public bool Current => RungOwnerExact(Frame);", Expression(Release, "public bool Current"));
		}

		[Test]
		public void SourceContractNewReportsAndRetirementRequireReleaseButOldReportValidationRemainsPhysical()
		{
			Ordered(Method("Growth/KingdomSubsidenceStepRuntime.Reports.cs", "private static bool ResumeRungReport("),
				"TryReadRungPlan(book,", "KingdomSubsidenceRungRules.ReleasedComplete(rung)",
				"op.RungReportModel == KingdomSubsidenceBatchRules.PendingReport", "SaveRungReport(frame, report)");
			string rules = "Growth/KingdomSubsidenceStepRules.Reporting.cs";
			Has(Method(rules, "private static bool RungComplete("), "KingdomSubsidenceRungRules.ReleasedComplete(plan)");
			string old = Method(rules, "private static bool ValidRungReport(");
			Has(old, "KingdomSubsidenceRungRules.PhysicalComplete(rung)");
			StringAssert.DoesNotContain("ReleasedComplete", old);
		}

		[Test]
		public void SourceContractNativeRoofUsesTypedCasAndReprovesItsSameCarriers()
		{
			Ordered(Method(Roof, "private static bool ApplyRungRoof("), "RungRoofExact(", "RoofAction(",
				"KingdomSubsidenceEffectAction.Apply", "city.TryPublishSubsidenceRoof(frame.Owner.Wire, row,",
				"RungRoofExact(", "!row.SameCarriers(after)", "TryProveRungRoof(", "SaveRung(frame, next)");
			Has(Method(Roof, "private static bool RungRoofExact("), "binding.ObjectId != roof.BodyObjectId",
				"binding.ZoneId != frame.Plan.ZoneId", "row.HomeWorkId != work.WorkId",
				"body.IDIfAssigned != roof.BodyObjectId", "KingdomCitizenship.BelongsTo(frame.System, body)",
				"ReferenceEquals(frame.Survey.FindBoundBody(");
		}

		[Test]
		public void SourceContractGlobalBatchIsBoundedAndTraversesKnownLiveCustodyWithoutMinting()
		{
			string body = Method("Growth/KingdomPlot2.GlobalIdentityBatch.cs", "internal static bool TryCaptureGlobalLiveIds(");
			Ordered(body, "ids.Count > MaximumFoundingHeartCustodyObjects", "ReuseSurvey: true",
				"!expanded.Add(item)", "expanded.Count > MaximumFoundingHeartCustodyObjects",
				"graveyard.Contains(item)", "item.IDIfAssigned", "found.ContainsKey(id)",
				"item.GetInventoryDirectAndEquipment()", "pending.AddRange(children)");
			string roots = Method("Growth/KingdomPlot2.07b.FoundingHeartIdentity.cs", "private static bool TryFoundingHeartCustodyRoots(");
			Has(roots, "The.ZoneManager.ActiveZone", "The.ZoneManager.CachedZones.Values", "KingdomSurvey.ActiveFor(zone)",
				"survey.TryLoaded(out _)", "KingdomSurvey.ObjectsFor(zone)", "zone.GetObjects()",
				"Pending.Add(The.Player)", "The.Game.ObjectGameState", "row.Value is GameObject");
			foreach (string banned in new[] { "GetZone(", "Thaw(", ".ID;", ".ID =", "GameObject.Create(" })
				StringAssert.DoesNotContain(banned, body + roots);
		}

		private static string Expression(string path, string signature)
		{
			string source = Regex.Replace(TestMain.ReadRepositoryText(path), @"\s+", " ");
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, path + ": " + signature);
			int end = source.IndexOf(';', start);
			Assert.Greater(end, start, path + ": expression terminator");
			return source.Substring(start, end - start + 1);
		}
		private static string Method(string path, string signature)
		{
			string source = Regex.Replace(TestMain.ReadRepositoryText(path), @"\s+", " ");
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, path + ": " + signature);
			int open = source.IndexOf('{', start), depth = 0;
			Assert.GreaterOrEqual(open, 0, path);
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
			}
			Assert.Fail("Unclosed method: " + path); return null;
		}
		private static void Has(string source, params string[] needles)
		{ foreach (string needle in needles) StringAssert.Contains(needle, source); }
		private static void Ordered(string source, params string[] needles)
		{
			int previous = -1;
			foreach (string needle in needles)
			{
				int current = source.IndexOf(needle, previous + 1, StringComparison.Ordinal);
				Assert.Greater(current, previous, needle); previous = current;
			}
		}
	}
}
#endif
