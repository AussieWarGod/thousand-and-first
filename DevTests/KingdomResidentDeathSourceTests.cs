#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	// Source contracts only. These do not execute an engine death, native callback, or cold load.
	[TestFixture]
	public sealed class KingdomResidentDeathSourceTests
	{
		[Test]
		public void RealBeforeRemovalHookRoutesThroughDurableIntake()
		{
			foreach (string path in new[] { "Core/KingdomCitizenship.Part.cs", "Experience/r_KingdomCitizenLegacy.cs" })
			{
				string source = Read(path); StringAssert.Contains("BeforeDeathRemovalEvent", source);
				StringAssert.Contains("KingdomOffices.RecordDeath(ParentObject, E.Killer)", source);
			}
			string intake = Read("Experience/KingdomOffices.cs");
			StringAssert.Contains("KingdomResidentDeathRuntime.Record(system, Citizen", intake);
			StringAssert.DoesNotContain("KingdomResidents.TryMarkDead", intake);
			string runtime = Read("Growth/KingdomResidentDeathRuntime.cs");
			Ordered(runtime, "Capture(f, body, cause", "Save(f, next)", "Resume(f, index, body)");
		}
		[Test]
		public void JobReceiptPrecedesStandingAndBindingWithoutGuessingMissingJob()
		{
			string source = Read("Growth/KingdomResidentDeathRuntime.cs");
			Ordered(source, "PrepareExpedition(f, r, body)", "TryPublishWitnessedDeath", "RetireBinding(f, r)", "Roles(f, r, body)");
			string expedition = Slice(Read("Experience/KingdomExpeditions.PassAndDeath.cs"),
				"internal static bool TryPrepareWitnessedDeath", "private static bool ReadWitnessJobs");
			Ordered(expedition, "if (!found) return false", "current != before && current != prepared",
				"TryPublishTerminalResolution", "WitnessJob(after) != prepared", "body.RemoveIntProperty");
			StringAssert.Contains("return before == \"\" && prepared == \"\" && !found && exact()", expedition);
			string capture = Slice(Read("Experience/KingdomExpeditions.PassAndDeath.cs"),
				"internal static bool TryCaptureWitnessedDeath", "internal static bool TryPrepareWitnessedDeath");
			Ordered(capture, "KingdomExpeditionRules.IsResolutionPrepared(row.OriginCode)",
				"if (!WitnessPrepared(row, tick)) return false", "before = prepared = WitnessJob(row); return true",
				"row.WithExpeditionResolution");
			Ordered(expedition, "before == prepared && !WitnessPrepared(row, tick)",
				"if (current == before && before != prepared)", "row.WithExpeditionResolution");
			StringAssert.Contains("KingdomJobRules.ValidExpeditionOutcomeForPhase(row.OriginCode, row.OutcomeCode)", expedition);
			StringAssert.Contains("KingdomJobRules.ValidExpeditionResultReceipt(row)", expedition);
			StringAssert.Contains("row.DueTick <= tick", expedition);
			StringAssert.Contains("!string.IsNullOrEmpty(row.DestZoneId)", expedition);
			string recovery = Read("Experience/KingdomExpeditions.Resolution.cs");
			Ordered(recovery, "existing.Standing == KingdomResidentStanding.Dead", "standingCause = existing.Cause");
			StringAssert.Contains("KingdomRealmArchive.CloneJobs(source)", Read("Experience/KingdomExpeditions.PassAndDeath.cs"));
			StringAssert.Contains("WitnessJobs(copy) != raw", Read("Experience/KingdomExpeditions.PassAndDeath.cs"));
		}
		[Test]
		public void FiveTableJournalOwnershipNeverUsesBodyAbsenceToCreateWitness()
		{
			string storage = Read("Growth/KingdomResidentDeathRuntime.Storage.cs");
			foreach (string table in new[] { "StringGameState", "IntGameState", "Int64GameState", "ObjectGameState", "BooleanGameState" })
				StringAssert.Contains(table, storage);
			StringAssert.Contains("KingdomScenarioStateShape.TryAuthorityText", storage);
			StringAssert.Contains("value.Journal.Realm != value.Realm", storage);
			StringAssert.Contains("ReferenceEquals(tables[i], f.Tables[i])", storage);
			string capture = Read("Growth/KingdomResidentDeathRuntime.Capture.cs");
			Ordered(capture, "GameObject.Validate(body)", "binding.ObjectId != body.IDIfAssigned", "value.StepWire = f.City.SubsidenceModel");
			StringAssert.DoesNotContain("TryMarkDead", capture);
		}
		[Test]
		public void PreparedSharedAccountsRecoverBeforeOrdinaryWitnesses()
		{
			string source = Read("Growth/KingdomResidentDeathRuntime.cs");
			Ordered(source, "// Finish the unique prepared shared-account cut", "&& !RecoverOne(f, i)",
				"if (r.Phase == KingdomResidentDeathPhase.Settled)");
			string accounting = Read("Growth/KingdomResidentDeathRuntime.Accounting.cs");
			Ordered(accounting, "TryPrepareAccounts(r, before", "Save(f, index, prepared)", "PutMap(f, i, next)",
				"ProjectCompatibility(f.System, Exact: true)", "accounted.Phase = KingdomResidentDeathPhase.Accounted");
		}
		[Test]
		public void RoleRemovalCallbacksReproveWitnessBeforeTerminalPublication()
		{
			string cook = Slice(Read("Experience/KingdomNamedCook.Lifecycle.cs"),
				"internal static bool TryConcludeWitnessedDeath", "private static bool TryFindCookBook");
			Ordered(cook, "TryWitness", "ReadCook(death.CookBefore)", "body.RemovePart(teaching)", "if (!exact()) return false",
				"body.RemovePart(marker)", "if (!exact() ||", "book.NamedCook = vacant", "TryWitness");
			string roles = Read("Growth/KingdomResidentDeathRuntime.Roles.cs");
			Ordered(roles, "bool cleaned = TryCleanupDeathProjection", "if (!exact()) return false",
				"TryCompleteOfficeDeathVacancy", "TryWitness");
		}
		[Test]
		public void TellingIntentIsDurableAndInterruptedAttemptIsNotReplayed()
		{
			string telling = Read("Growth/KingdomResidentDeathRuntime.Telling.cs");
			Ordered(telling, "r.Telling == KingdomResidentDeathTelling.Attempting", "r.Telling = KingdomResidentDeathTelling.Uncertain",
				"else if (r.Telling == KingdomResidentDeathTelling.Pending)", "Save(f, index, r)", "OwnDeathTelling");
			StringAssert.Contains("r.RemembranceUnavailable", telling);
			StringAssert.Contains("missing body is not roof proof", telling);
			StringAssert.Contains("r.BeforeAccounts = r.AfterAccounts = new string[0]", telling);
		}
		[Test]
		public void ArchiveCheckInPassAndFuneralRespectPendingJournal()
		{
			StringAssert.Contains("KingdomResidentDeathRuntime.TryRecoverPending", Read("Core/KingdomSystem.z21.SemanticPass.cs"));
			StringAssert.Contains("KingdomResidentDeathRuntime.CanProceed", Read("Simulation/City/KingdomCity.z01.CheckIn.cs"));
			StringAssert.Contains("KingdomResidentDeathRuntime.OwnsFuneral", Read("Simulation/City/KingdomHappenings.z03.Funerals.cs"));
			string archive = Read("Core/KingdomRealmArchive.01Capture.cs");
			Ordered(archive, "KingdomResidentDeathRuntime.CanProceed", "capturedSeat = System.Capture()",
				"candidate.CurrentGraphMatches", "KingdomResidentDeathRuntime.CanProceed", "Archive = candidate");
			StringAssert.DoesNotContain("Normalize()", Read("Growth/KingdomResidentDeathRuntime.Storage.cs"));
		}
		[Test]
		public void RecoveryPrecedesMutableWorkAndReproofStaysReadOnly()
		{
			Ordered(Read("Growth/KingdomSubsidence.Reckoning.cs"), "KingdomResidentDeathRuntime.TryRecoverPending",
				"TryResumeAnnouncement", "TryOption", "TryDrive");
			Ordered(Read("Growth/KingdomSubsidenceStepRuntime.Pass.cs"), "KingdomResidentDeathRuntime.TryRecoverPending", "TryExecutionFrame");
			string options = Read("Growth/KingdomSubsidenceStepRuntime.Options.cs");
			Ordered(options, "KingdomResidentDeathRuntime.TryRecoverPending", "TryExecutionFrame", "KingdomSubsidenceAnnouncementDriver.Resume");
			string reproof = Slice(options, "private static bool OptionExact(", "private static bool OptionSeatExact(");
			StringAssert.Contains("KingdomResidentDeathRuntime.CanProceed", reproof);
			StringAssert.DoesNotContain("TryRecoverPending", reproof);
			Ordered(Read("Growth/KingdomSubsidenceStepRuntime.Homecoming.cs"), "KingdomResidentDeathRuntime.TryRecoverPending",
				"TryOptionFrame", "show(digest)", "HomecomingExact(frame)", "ledger.Reset()");
			Ordered(Read("Growth/KingdomSubsidenceStepRuntime.Storage.cs"), "private static bool Publish(",
				"KingdomResidentDeathRuntime.CanProceed", "item.City.SubsidenceModel = wire");
		}
		[Test]
		public void HomecomingRecoveryPreservesTheLaterFailureFallback()
		{
			string source = Read("Growth/KingdomSubsidenceStepRuntime.Homecoming.cs");
			Ordered(source, "refusal = \"The homecoming report changed", "TryRecoverPending(system, out string deathFailure)",
				"{ refusal = deathFailure; return false; }", "if (show == null", "if (ledger == null");
			StringAssert.DoesNotContain("TryRecoverPending(system, out refusal)", source);
		}
		[Test]
		public void LocalRoleCaptureRejectsOneSidedIdentityClaimsBeforeTreatingThemAsAbsent()
		{
			string roles = Read("Growth/KingdomResidentDeathRuntime.Roles.cs");
			Ordered(roles, "RoleClaimAgrees(r, office.HolderResidentId, office.HolderObjectId)", "r.OfficeGeneration = office.Generation");
			Ordered(roles, "RoleClaimAgrees(r, cook.ResidentId, cook.BodyObjectId)", "r.CookGeneration = cook.Generation");
		}
		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Slice(string text, string first, string next)
		{ int a = text.IndexOf(first, StringComparison.Ordinal), b = text.IndexOf(next, a + first.Length, StringComparison.Ordinal);
			Assert.That(a, Is.GreaterThanOrEqualTo(0)); Assert.That(b, Is.GreaterThan(a)); return text.Substring(a, b - a); }
		private static void Ordered(string text, params string[] tokens)
		{
			int at = 0;
			foreach (string token in tokens)
			{ int next = text.IndexOf(token, at, StringComparison.Ordinal); Assert.That(next, Is.GreaterThanOrEqualTo(at), token); at = next + token.Length; }
		}
	}
}
#endif
