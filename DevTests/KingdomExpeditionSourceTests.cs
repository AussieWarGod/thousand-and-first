#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomExpeditionSourceTests
	{
		[Test]
		public void RealmArchiveV4RetainsTheV3MissionEnvelopeAndReadsV2()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine("Core", "KingdomRealmArchive.cs"));
			StringAssert.Contains("public const int CurrentVersion = 4", source);
			StringAssert.Contains("internal const int LegacyJobVersion = 2", source);
			StringAssert.Contains("internal const int MissionJobVersion = 3", source);
			StringAssert.Contains("Jobs = ReadJobs(Reader, wireVersion)", source);
			StringAssert.Contains("if (WireVersion < CurrentVersion) value.Normalize()", source);
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
		public void PortersExplicitlyIgnoreNamedResidentJobs()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine("Simulation", "City",
				"KingdomPorters.cs"));
			Assert.GreaterOrEqual(Occurrences(source, "row.Kind != KingdomJobKind.Delivery"), 4);
		}

		[Test]
		public void PreparedAuthorityAndBodyReceiptPrecedeEveryPhysicalDebit()
		{
			string source = TestMain.ReadRepositoryText(Path.Combine("Experience",
				"KingdomExpeditions.cs"));
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
			string source = TestMain.ReadRepositoryText(Path.Combine("Experience",
				"KingdomExpeditions.cs"));
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
			string source = TestMain.ReadRepositoryText(Path.Combine("Experience",
				"KingdomExpeditions.cs"));
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

			string residents = TestMain.ReadRepositoryText(Path.Combine("Simulation", "City",
				"KingdomResidents.cs"));
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
			string source = TestMain.ReadRepositoryText(Path.Combine("Experience",
				"KingdomExpeditions.cs"));
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
	}
}
#endif
