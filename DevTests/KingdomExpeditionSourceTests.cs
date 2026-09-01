#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomExpeditionSourceTests
	{
		private static string RealmArchiveSource()
		{
			string[] files =
			{
				"KingdomRealmArchivePhase.cs",
				"KingdomRealmCallbackPhase.cs",
				"KingdomRealmCallbackDisposition.cs",
				"KingdomRealmCallbackScope.cs",
				"KingdomRealmCallbackReceipt.cs",
				"KingdomRealmArchive.00Core.cs",
				"KingdomRealmArchive.01Capture.cs",
				"KingdomRealmArchive.02AuthorityHash.cs",
				"KingdomRealmArchive.03Validation.cs",
				"KingdomRealmArchive.04GraphMatch.cs",
				"KingdomRealmArchive.05BoundedValidation.cs",
				"KingdomRealmArchive.06JobValidation.cs",
				"KingdomRealmArchive.07DeliveryValidation.cs",
				"KingdomRealmArchive.08Clone.cs",
				"KingdomRealmArchive.09ExactGraph.cs",
				"KingdomRealmArchive.10WireEnvelope.cs",
				"KingdomRealmArchive.11WirePrimitives.cs",
				"KingdomRealmArchive.12WireRegistry.cs"
			};
			string[] source = new string[files.Length];
			for (int i = 0; i < files.Length; i++)
				source[i] = TestMain.ReadRepositoryText(Path.Combine("Core", files[i]));
			return string.Join("\n", source);
		}

		[Test]
		public void RealmArchiveV6RetainsV4DeliveryColumnsAndReadsV2()
		{
			string source = RealmArchiveSource();
			StringAssert.Contains("public const int CurrentVersion = 8", source);
			StringAssert.Contains("internal const int SettlementTopologyVersion = 6", source);
			StringAssert.Contains("internal const int DirectionalStandingVersion = 7", source);
			StringAssert.Contains("internal const int ExpeditionResultJobVersion = 8", source);
			StringAssert.Contains("internal const int LegacyJobVersion = 2", source);
			StringAssert.Contains("internal const int MissionJobVersion = 3", source);
			StringAssert.Contains("internal const int ExactDeliveryJobVersion = 4", source);
			StringAssert.Contains("Jobs = ReadJobs(Reader, wireVersion)", source);
			StringAssert.Contains("if (WireVersion >= ExactDeliveryJobVersion)", source);
			StringAssert.Contains("if (WireVersion == ExactDeliveryJobVersion)", source);
			StringAssert.Contains("Realm archive v4 contains a future delivery enum value.", source);
			StringAssert.Contains("if (WireVersion < ExactDeliveryJobVersion) value.Normalize()", source);
			string[] fields = { "SubjectIds", "SubjectNames", "TargetNames", "DueTicks",
				"WaterCosts", "ProvisionCosts", "OutcomeCodes" };
			foreach (string field in fields)
			{
				Assert.GreaterOrEqual(Occurrences(source, "Value." + field), 3,
					field + " must be validated, cloned, and written");
				StringAssert.Contains("Archived." + field, source);
				StringAssert.Contains("Current." + field, source);
			}
			StringAssert.Contains("!BoundedStrings(Value.SubjectNames, 512)", source);
			StringAssert.Contains("!BoundedStrings(Value.TargetNames, 512)", source);
			StringAssert.Contains("Value.WaterCosts[i] < 0", source);
			StringAssert.Contains("Value.OutcomeCodes[i] > 7", source);
			StringAssert.Contains("ValidExpeditionOutcomeForPhase(", source);
		}

		[Test]
		public void ExpeditionLogicalAuthorityKeepsNestedAbiAndMutationOrder()
		{
			string source = KingdomExpeditionsLogicalSource.Read();
			Assert.AreEqual(10, Occurrences(source,
				"public static partial class KingdomExpeditions"));
			Assert.AreEqual(1, Occurrences(source, "private sealed class ResidentChoice"));
			Assert.AreEqual(1, Occurrences(source, "private sealed class TargetChoice"));
			Assert.AreEqual(1, Occurrences(source, "private enum BoundBodyState : byte"));
			AssertOrdered(source,
				"public const string ResidentJobProperty = \"r_TAF_ExpeditionJob\";",
				"public const string ProvisionJobProperty = \"r_TAF_ExpeditionProvisionJob\";",
				"public const string RewardJobProperty = \"r_TAF_ExpeditionRewardJob\";",
				"public const string DebitReceiptProperty = \"r_TAF_ExpeditionDebitReceipt\";",
				"public const string WaterJobProperty = \"r_TAF_ExpeditionWaterJob\";",
				"public const string WaterAfterProperty = \"r_TAF_ExpeditionWaterAfter\";");
			AssertOrdered(source,
				"internal KingdomResidentRow Row;", "internal string ZoneId;",
				"internal JournalMapNote Note;", "internal string ZoneId;",
				"internal string Name;", "internal KingdomExpeditionQuote Quote;");
			AssertOrdered(source,
				"Unreachable = 0,", "Alive = 1,", "Led = 2,", "Dead = 3,",
				"Missing = 4,", "Ambiguous = 5");
			AssertOrdered(source,
				"public static void Open(", "private static void OpenDispatch(",
				"private static bool TryDispatch(", "public static bool OnSettlementPass(",
				"internal static bool TryPrepareResidentDeath(",
				"private static bool TryAdvanceDispatch(",
				"private static bool TryPublishPhase(", "private static bool TryReadReceipt(",
				"private static bool TryResolve(",
				"private static bool TryPublishTerminalResolution(",
				"private static bool TryResumeTerminalResolution(",
				"private static bool TellAndClose(",
				"private static List<ResidentChoice> EligibleResidents(",
				"private static List<TargetChoice> VisitedTargets(",
				"private static bool TrySetResident(",
				"private static BoundBodyState FindBoundBody(",
				"private static bool MoveExact(", "private static bool EnsureReward(",
				"private static string ResultLine(",
				"private static bool TryPrepareDebitReceipt(",
				"private static bool TryApplyPreparedDebit(",
				"private static bool TryApplyProvisionReceipt(",
				"private static bool MarkWaterReceipt(",
				"private static bool TryApplyWaterReceipt(",
				"private static void ClearDebitMarkers(");

			int dispatch = source.IndexOf("private static bool TryDispatch(",
				StringComparison.Ordinal);
			int pass = source.IndexOf("public static bool OnSettlementPass(", dispatch,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(dispatch, 0);
			Assert.Greater(pass, dispatch);
			AssertOrdered(source.Substring(dispatch, pass - dispatch),
				"Body.RemoveIntProperty(ResidentJobProperty);",
				"Body.SetStringProperty(DebitReceiptProperty, null, RemoveIfNull: true);",
				"int jobId = System.Jobs.MintJobId();",
				"KingdomWaterDebit water = Survey.ReserveExactWater(requoted.WaterDrams);",
				"TryPrepareDebitReceipt(Survey, water, jobId, SourceZoneId",
				"KingdomJobRow row = new KingdomJobRow(", "table.TryOpen(row, out opened",
				"System.Jobs.TryPublish(opened", "TryAdvanceDispatch(System, row, Body");

			int advance = source.IndexOf("private static bool TryAdvanceDispatch(",
				StringComparison.Ordinal);
			int publishPhase = source.IndexOf("private static bool TryPublishPhase(", advance,
				StringComparison.Ordinal);
			AssertOrdered(source.Substring(advance, publishPhase - advance),
				"body.SetStringProperty(DebitReceiptProperty, PreparedReceipt);",
				"TryApplyPreparedDebit(System, row, body, receipt, ReservedWater, out Failure)",
				"TryPublishPhase(System, row, KingdomExpeditionPhase.Paid",
				"MoveExact(body, destinationCell)",
				"KingdomResidents.Bind(System, row.SubjectId, KingdomBindingKind.Resident",
				"TrySetResident(System, row.SubjectId, KingdomResidentStanding.Expedition",
				"TryPublishPhase(System, row, KingdomExpeditionPhase.Dispatched");
			AssertOrdered(source,
				"Body.SystemLongDistanceMoveTo(Target, 0, forced: true, ignoreCombat: true)",
				"KingdomSurvey.ObserveRemovedFromActive(before, Body);",
				"KingdomSurvey.ObserveAddedToActive(Body.CurrentZone, Body);");
			AssertOrdered(source,
				"item.SetIntProperty(ProvisionJobProperty, JobId);",
				"item.Destroy(null, Silent: true);",
				"KingdomSurvey.ObserveChangedInActive(Source, larder);");
		}

		[Test]
		public void PortersExplicitlyIgnoreNamedResidentJobs()
		{
			string source = KingdomPortersLogicalSource.Read();
			Assert.GreaterOrEqual(Occurrences(source, "row.Kind != KingdomJobKind.Delivery"), 4);
		}

		[Test]
		public void PreparedAuthorityAndBodyReceiptPrecedeEveryPhysicalDebit()
		{
			string source = KingdomExpeditionsLogicalSource.Read();
			int publish = source.IndexOf("System.Jobs.TryPublish(opened", StringComparison.Ordinal);
			int advance = source.IndexOf("TryAdvanceDispatch(System, row, Body, encodedReceipt",
				StringComparison.Ordinal);
			int receiptAttach = source.IndexOf("body.SetStringProperty(DebitReceiptProperty",
				StringComparison.Ordinal);
			int foodCallback = source.IndexOf("item.Destroy(null, Silent: true)",
				StringComparison.Ordinal);
			int waterCallback = source.IndexOf("ReservedWater.Commit()", StringComparison.Ordinal);
			int recoveryDrain = source.IndexOf("KingdomLiquids.Drain(vessel, remaining)",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(publish, 0);
			Assert.Greater(advance, publish);
			Assert.Greater(receiptAttach, advance);
			Assert.Greater(foodCallback, receiptAttach);
			Assert.Greater(waterCallback, receiptAttach);
			Assert.Greater(recoveryDrain, receiptAttach);
			StringAssert.Contains("HasDebitMarker(source, row.JobId)", source);
			StringAssert.Contains("no new debit was attempted", source);
		}

		[Test]
		public void ExactResidentAuthorityUsesObjectIdAndSettlementPassNeverSurveysRemoteGround()
		{
			string source = KingdomExpeditionsLogicalSource.Read();
			int duplicate = source.IndexOf("HasExpedition(table, Resident.ResidentId)",
				StringComparison.Ordinal);
			int mint = source.IndexOf("System.Jobs.MintJobId()", StringComparison.Ordinal);
			Assert.GreaterOrEqual(duplicate, 0);
			Assert.Greater(mint, duplicate);
			StringAssert.Contains("SameAuthority(Requested, row)", source);
			StringAssert.Contains("LegacyPrepared", source);
			StringAssert.Contains("KingdomResidents.FindExactBindingObject(binding)", source);
			StringAssert.Contains("LoadZone: false, SourceSurvey: Survey", source);
			StringAssert.Contains("Prepared debit waits for the source ground's maintained survey.",
				source);
			StringAssert.DoesNotContain("AddCandidateZone", source);
			int resolver = source.IndexOf("private static BoundBodyState FindBoundBody",
				StringComparison.Ordinal);
			int end = source.IndexOf("private static Cell SafeCell", resolver,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(resolver, 0);
			Assert.Greater(end, resolver);
			string bodyResolution = source.Substring(resolver, end - resolver);
			StringAssert.DoesNotContain("KingdomSurvey.ObjectsFor", bodyResolution);
			StringAssert.DoesNotContain("GetObjects()", bodyResolution);
			StringAssert.Contains("ResidentJobProperty", bodyResolution);
			StringAssert.Contains("KingdomBindingKind.Resident", bodyResolution);
			AssertTerminalBodyLossPublishesReceiptBeforeUnbindingAndResumesWithoutBody();
		}

		private static void AssertTerminalBodyLossPublishesReceiptBeforeUnbindingAndResumesWithoutBody()
		{
			string source = KingdomExpeditionsLogicalSource.Read();
			int settlement = source.IndexOf("public static bool OnSettlementPass",
				StringComparison.Ordinal);
			int advance = source.IndexOf("private static bool TryAdvanceDispatch", settlement,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(settlement, 0);
			Assert.Greater(advance, settlement);
			string settlementPass = source.Substring(settlement, advance - settlement);
			StringAssert.Contains("IsResolutionPrepared(row.OriginCode)", settlementPass);
			StringAssert.Contains("TryResumeTerminalResolution(System, row", settlementPass);

			int resolver = source.IndexOf("private static bool TryResolve(",
				StringComparison.Ordinal);
			int publisher = source.IndexOf("private static bool TryPublishTerminalResolution",
				resolver, StringComparison.Ordinal);
			int resume = source.IndexOf("private static bool TryResumeTerminalResolution",
				publisher, StringComparison.Ordinal);
			int tell = source.IndexOf("private static bool TellAndClose", resume,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(resolver, 0);
			Assert.Greater(publisher, resolver);
			Assert.Greater(resume, publisher);
			Assert.Greater(tell, resume);
			string resolution = source.Substring(resolver, publisher - resolver);
			int residentEvidence = resolution.IndexOf("TryInferTerminalResidentEvidence(System, Row",
				StringComparison.Ordinal);
			int bodyLookup = resolution.IndexOf("FindBoundBody(System, Row", StringComparison.Ordinal);
			int publishCall = resolution.IndexOf("TryPublishTerminalResolution(System, Row",
				StringComparison.Ordinal);
			int resumeCall = resolution.LastIndexOf("TryResumeTerminalResolution(System, Row",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(residentEvidence, 0);
			Assert.Greater(bodyLookup, residentEvidence);
			Assert.GreaterOrEqual(publishCall, 0);
			Assert.Greater(resumeCall, publishCall);
			StringAssert.DoesNotContain("KingdomResidents.Unbind", resolution);

			string publication = source.Substring(publisher, resume - publisher);
			StringAssert.Contains("WithExpeditionResolution", publication);
			StringAssert.Contains("System.Jobs.TryPublish(next", publication);
			int deathPreparation = source.IndexOf("internal static bool TryPrepareResidentDeath",
				StringComparison.Ordinal);
			int dispatchAdvance = source.IndexOf("private static bool TryAdvanceDispatch",
				deathPreparation, StringComparison.Ordinal);
			string death = source.Substring(deathPreparation, dispatchAdvance - deathPreparation);
			int deathPublish = death.IndexOf("TryPublishTerminalResolution(System, row",
				StringComparison.Ordinal);
			int markerClear = death.IndexOf("Body.RemoveIntProperty(ResidentJobProperty)",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(deathPublish, 0);
			Assert.Greater(markerClear, deathPublish);
			string recovery = source.Substring(resume, tell - resume);
			int standing = recovery.IndexOf("TrySetResident(System", StringComparison.Ordinal);
			int unbind = recovery.IndexOf("EnsureResidentUnbound(System", StringComparison.Ordinal);
			int close = recovery.IndexOf("TellAndClose(System", StringComparison.Ordinal);
			Assert.GreaterOrEqual(standing, 0);
			Assert.Greater(unbind, standing);
			Assert.Greater(close, unbind);
			StringAssert.Contains("row.DueTick", recovery);
			StringAssert.Contains("existing.Cause", recovery);
			StringAssert.Contains("ZoneId = binding.ZoneId", source);

			string offices = TestMain.ReadRepositoryText(Path.Combine("Experience",
				"KingdomOffices.cs"));
			int deathReceipt = offices.IndexOf("KingdomExpeditions.TryPrepareResidentDeath(system, Citizen",
				StringComparison.Ordinal);
			int standingDeath = offices.IndexOf("KingdomResidents.TryMarkDead(system, Citizen",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(deathReceipt, 0);
			Assert.Greater(standingDeath, deathReceipt);
			StringAssert.Contains("TryInferTerminalResidentEvidence", source);

			string residents = KingdomResidentsLogicalSource.Read();
			int witness = residents.IndexOf("private static KingdomResidentRow Witnessed(",
				StringComparison.Ordinal);
			int homes = residents.IndexOf("private static Dictionary<string, int> HomeWorkIds",
				witness, StringComparison.Ordinal);
			string witnessBody = residents.Substring(witness, homes - witness);
			StringAssert.Contains("row.Standing == KingdomResidentStanding.Expedition", witnessBody);
			Assert.Less(witnessBody.IndexOf("KingdomResidentStanding.Expedition",
				StringComparison.Ordinal), witnessBody.IndexOf("Survey.TryWitnessResident",
				StringComparison.Ordinal));
		}

		[Test]
		public void PersistedExpeditionNamesStayPlainAndEveryRichSinkEscapesThem()
		{
			string source = KingdomExpeditionsLogicalSource.Read();
			StringAssert.Contains(
				"Name = SafeName(ConsoleLib.Console.ColorUtility.StripFormatting(note.Text), zoneId)",
				source);
			StringAssert.Contains("Resident.Name, Target.Name", source);
			StringAssert.Contains(
				"return KingdomPresentation.Rich(SafeName(Value, Fallback));", source);
			StringAssert.DoesNotContain(
				"options[i + 1] = \"Recall \" + SafeName(row.SubjectName", source);
			StringAssert.DoesNotContain(
				"string who = SafeName(Row.SubjectName", source);
			StringAssert.DoesNotContain(
				"string where = SafeName(Row.TargetName", source);
		}

		[Test]
		public void ProvisionReceiptsExcludeProtectedFoodAndFenceEveryDestroy()
		{
			string source = KingdomExpeditionsLogicalSource.Read();
			StringAssert.Contains("Survey.FoodAvailable < ProvisionCost", source);
			StringAssert.Contains("KingdomOrdinaryFoodAuthority.CanSpend(leases, item)", source);
			StringAssert.Contains("KingdomOrdinaryFoodAuthority.TrySpendNow(item,", source);
			StringAssert.Contains("ProvisionJobProperty, JobId, out Failure", source);
		}

		[Test]
		public void RichFindPublishesExactDeedBeforeJobEviction()
		{
			string source = KingdomExpeditionsLogicalSource.Read();
			int telling = source.IndexOf("private static bool TellAndClose(",
				StringComparison.Ordinal);
			int next = source.IndexOf("private static bool TryRecordExpeditionDeed(",
				telling, StringComparison.Ordinal);
			Assert.GreaterOrEqual(telling, 0);
			Assert.Greater(next, telling);
			string body = source.Substring(telling, next - telling);
			AssertOrdered(body, "KingdomChronicle.RecordOnce(System, eventId",
				"TryRecordExpeditionDeed(System, Row, Resolution, eventId",
				"ledger.NoteExpedition(line);", "current.TryClose(Row.JobId",
				"System.Jobs.TryPublish(next");
			StringAssert.Contains("Resolution != KingdomExpeditionOutcome.RichFind", source);
			StringAssert.Contains("System.SettlementIdForOwnedZone(Row.SourceZoneId)", source);
			AssertOrdered(source.Substring(next), "TryFindExistingDeedReceipt(ledger",
				"if (exactRetry) return true;", "TryReadExactDeedResident(System");
			StringAssert.Contains("matches == 1 && correctBook", source);
			StringAssert.Contains("book.SettlementId, SettlementId", source);
			StringAssert.Contains("resident.Standing != KingdomResidentStanding.Resident", source);
			StringAssert.Contains("resident.BoundZoneId, Row.SourceZoneId", source);
			StringAssert.Contains("KingdomPolityRules.TryPromoteNamedFigure", source);
			StringAssert.Contains("KingdomPolityAttentionRules.MaximumActiveNamedFigures", source);
			StringAssert.DoesNotContain("GameObject.Create", source.Substring(next));
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

		private static void AssertOrdered(string source, params string[] terms)
		{
			int at = -1;
			for (int i = 0; i < terms.Length; i++)
			{
				int next = source.IndexOf(terms[i], at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, terms[i]);
				at = next;
			}
		}
	}
}
#endif
