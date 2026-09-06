#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Lexical engine-boundary contracts only. These tests do not execute native callbacks or prove custody.</summary>
	public sealed class KingdomSubsidenceRungFenceSourceTests
	{
		[Test]
		public void SourceContractNativePartReceiptFencesBeforeOptionalRealmLookup()
		{
			string body = Tail("Growth/KingdomSubsidenceRungRuntime.Fences.cs",
				"internal static bool BlocksWork(GameObject work)");
			Before(body, "!GameObject.Validate(work)", "work.GetPart<r_KingdomWear>()");
			Before(body, "wear.IncidentCause == (int)KingdomWearRules.WearCause.Subsidence",
				"The.Game?.GetSystem<KingdomSystem>()");
			Before(body, "wear.IncidentPhase != (int)KingdomWearIncidentPhase.None",
				"The.Game?.GetSystem<KingdomSystem>()");
			StringAssert.Contains("foreach (KingdomCityBook book in system.OwnedCityBooks())", body);
			Before(body, "!book.HasValidSubsidenceStorage()",
				"KingdomSubsidenceRungRules.BlocksWork(book.SubsidenceModel, work.IDIfAssigned)");
			StringAssert.DoesNotContain(".Normalize(", body);
			StringAssert.DoesNotContain("RequirePart", body);
			StringAssert.DoesNotContain("SetStringProperty", body);
		}

		[Test]
		public void SourceContractGenericDamageRefusesSubsidenceAndFrozenWorkBeforeRequirePart()
		{
			string body = Tail("Growth/KingdomWear.06.DamageIncidents.cs",
				"private static bool ApplyDamageIncident(");
			Before(body, "Cause == KingdomWearRules.WearCause.Subsidence", "Work.RequirePart<r_KingdomWear>()");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(Work)", "Work.RequirePart<r_KingdomWear>()");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(Work)", "wear.LastCompletedIncidentId");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(Work)", "wear.IncidentId = IncidentId");
			int created = Position(body, "Work.RequirePart<r_KingdomWear>()");
			int recheck = body.IndexOf("KingdomSubsidenceRungRuntime.BlocksWork(Work)", created, StringComparison.Ordinal);
			Assert.Greater(recheck, created);
			Assert.Less(recheck, Position(body, "HasActiveRepair(Work, out _)"));
			Assert.Less(recheck, Position(body, "wear.LastCompletedIncidentId"));
			Before(body, "wear == null || wear.ParentObject != Work", "wear.IncidentId = IncidentId");
			Before(body, "!ReferenceEquals(Work.GetPart<r_KingdomWear>(), wear)", "wear.IncidentId = IncidentId");
		}

		[Test]
		public void SourceContractOldIncidentReplayStopsBeforeReadingOrContinuingWearReceipts()
		{
			string body = Tail("Growth/KingdomWear.03.Activation.cs", "private static void ResolveSafeReceipts(");
			Before(body, "if (KingdomSubsidenceRungRuntime.BlocksWork(Work)) return;", "Work.GetPart<r_KingdomWear>()");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(Work)", "ApplyDamageIncident(");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(Work)", "ContinueBoundLeak(");
		}

		[Test]
		public void SourceContractOrdinaryWearQueueExcludesFencedWorkBeforeRollLeakAndRepair()
		{
			string body = Tail("Growth/KingdomWear.04.Resolve.cs", "private static void Resolve(");
			Before(body, "GameObject work = Survey.Built[i]", "KingdomSubsidenceRungRuntime.BlocksWork(work)");
			Before(body, "if (KingdomSubsidenceRungRuntime.BlocksWork(work)) continue;", "HasActiveRepair(work");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(work)", "RollWear(");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(work)", "Leak(System, Survey, work");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(work)", "damaged.Add(work)");
		}

		[Test]
		public void SourceContractRepairAdvancementFencesBeforeClockOrEffortConsumption()
		{
			string body = Tail("Growth/KingdomWear.13.RepairCompletion.cs", "private static void AdvanceRepair(");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(Work)", "KingdomMaterials.ReadTick(Work, RepairWorkedProperty)");
			Before(body, "WearPart.IncidentPhase != (int)KingdomWearIncidentPhase.None", "KingdomMaterials.WriteTick(");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(Work)", "WearPart.RepairEffortLeft = left");
		}

		[Test]
		public void SourceContractRepairRecoveryFencesBeforeRemovedPartRecoveryOrCompletion()
		{
			string body = Tail("Growth/KingdomWear.01.RepairRecovery.cs",
				"KingdomSubsidenceRungRuntime.BlocksWork(work)");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(work)", "RecoverRemovedRepair(System, work, Job)");
			Before(body, "wear.IncidentPhase != (int)KingdomWearIncidentPhase.None", "wear.LifecycleQuarantined = true");
			Before(body, "KingdomSubsidenceRungRuntime.BlocksWork(work)", "FinishRepairProjection(System, work, wear");
		}

		[Test]
		public void SourceContractRepairCaptureAndExactReproofBothConsultLiveFence()
		{
			string path = "Growth/KingdomWear.02.RepairTargetAndCarry.cs";
			string capture = Tail(path, "private static bool TryCaptureRepairTarget(");
			Before(capture, "KingdomSubsidenceRungRuntime.BlocksWork(Work)", "Frame = new RepairTargetFrame");
			Before(capture, "Wear.IncidentPhase != (int)KingdomWearIncidentPhase.None", "Id = Work.ID");
			string exact = Tail(path, "private static bool RepairTargetExact(");
			Before(exact, "!KingdomSubsidenceRungRuntime.BlocksWork(Frame.Work)", "Frame.Work.CurrentZone == Frame.Zone");
			Before(exact, "Frame.WearPart.IncidentPhase == (int)KingdomWearIncidentPhase.None",
				"Frame.WearPart.Wear == Frame.Wear");
		}

		[Test]
		public void SourceContractFinalRepairUsesFencedExactReproofBeforeClearingOrRemovingWear()
		{
			string body = Tail("Growth/KingdomWear.13.RepairCompletion.cs", "private static bool FinishRepairProjection(");
			Before(body, "TryCaptureRepairTarget(Work, WearPart, out frame)", "WearPart.Wear = 0");
			Before(body, "RepairTargetExact(frame, Updated.Id)", "WearPart.Wear = 0");
			int latch = Position(body, "Work.SetStringProperty(RepairRemovalAttemptProperty, Updated.Id)");
			int reproof = body.IndexOf("RepairTargetExact(frame, Updated.Id)", latch, StringComparison.Ordinal);
			Assert.Greater(reproof, latch);
			Assert.Less(reproof, Position(body, "WearPart.Wear = 0"));
			Assert.Less(reproof, Position(body, "Work.RemovePart(WearPart)"));
		}

		[Test]
		public void SourceContractStableCarryFencesBothObjectsAndRechecksAfterPartCreation()
		{
			string path = "Growth/KingdomWear.02.RepairTargetAndCarry.cs";
			string preflight = Tail(path, "public static bool CanCarryStableState(");
			Before(preflight, "KingdomSubsidenceRungRuntime.BlocksWork(Source)", "Source.GetPart<r_KingdomWear>()");
			string carry = Tail(path, "public static bool TryCarryStableState(");
			Before(carry, "KingdomSubsidenceRungRuntime.BlocksWork(Target)", "Target.RequirePart<r_KingdomWear>()");
			Before(carry, "!CanCarryStableState(Source, out _)", "Target.RequirePart<r_KingdomWear>()");
			int created = Position(carry, "Target.RequirePart<r_KingdomWear>()");
			int recheck = carry.IndexOf("KingdomSubsidenceRungRuntime.BlocksWork(Target)", created, StringComparison.Ordinal);
			Assert.Greater(recheck, created);
			Assert.Less(recheck, Position(carry, "after.Wear = before.Wear"));
			int sourceRecheck = carry.IndexOf("!CanCarryStableState(Source, out _)", created, StringComparison.Ordinal);
			Assert.Greater(sourceRecheck, created);
			Assert.Less(sourceRecheck, Position(carry, "after.Wear = before.Wear"));
			Before(carry, "!ReferenceEquals(Source.GetPart<r_KingdomWear>(), before)", "after.Wear = before.Wear");
			Before(carry, "!ReferenceEquals(Target.GetPart<r_KingdomWear>(), after)", "after.Wear = before.Wear");
			Before(carry, "before.ParentObject != Source || after.ParentObject != Target", "after.Wear = before.Wear");
		}

		[Test]
		public void SourceContractCarrierRoofWriteFencesBeforeColumnNormalizationAndEveryRoofAssignment()
		{
			string body = Tail("Simulation/City/KingdomCityBook.08.ResidentAndBrinkAccess.cs",
				"public bool TryWriteBrink(");
			StringAssert.Contains("if (kind == BrinkKind.Roof", body);
			string guard = "KingdomSubsidenceRungRules.BlocksRoof(SubsidenceModel, residentId)";
			Before(body, "!HasValidSubsidenceStorage()", guard);
			Before(body, guard, "EnsureResidentColumnsSquare()");
			Before(body, guard, "TryResidentRow(residentId, out index)");
			Before(body, guard, "ResidentRoofStanding[index] =");
			Before(body, guard, "ResidentRoofTicks[index] =");
			Before(body, guard, "ResidentRoofWarnedTicks[index] =");
		}

		[Test]
		public void SourceContractBodyRoofWritersFenceBeforeEnrollmentAndNormalizingReads()
		{
			const string guard = "if (Kind == BrinkKind.Roof && KingdomSubsidenceRungRuntime.BlocksRoof(Subject)) return false;";
			string record = Tail("Core/KingdomBrink.cs", "public static bool Record(GameObject Subject");
			Before(record, guard, "KingdomResidents.TryEnsureRow(");
			Before(record, guard, "Stands(Subject, Kind)");
			string warned = Tail("Core/KingdomBrink.cs", "public static bool MarkWarned(GameObject Subject");
			Before(warned, guard, "Of(Subject, Kind)");
			string lift = Tail("Core/KingdomBrink.cs", "public static bool Lift(GameObject Subject");
			Before(lift, guard, "Stands(Subject, Kind)");
		}

		[Test]
		public void SourceContractNativeRoofFenceReadsAllOwnedStorageWithoutEnrollingOrNormalizing()
		{
			string body = Tail("Growth/KingdomSubsidenceRungRuntime.Fences.cs",
				"internal static bool BlocksRoof(GameObject subject)");
			body = body.Substring(0, Position(body, "internal static bool BlocksWork(GameObject work)"));
			Before(body, "!GameObject.Validate(subject)", "KingdomResidents.IdOf(subject)");
			StringAssert.Contains("foreach (KingdomCityBook book in system.OwnedCityBooks())", body);
			Before(body, "!book.HasValidSubsidenceStorage()",
				"KingdomSubsidenceRungRules.BlocksRoof(book.SubsidenceModel, id)");
			StringAssert.DoesNotContain("TryEnsureRow", body);
			StringAssert.DoesNotContain("TryReadBrink", body);
			StringAssert.DoesNotContain("Normalize(", body);
		}

		private static string Tail(string path, string marker)
		{
			string source = TestMain.ReadRepositoryText(path);
			return source.Substring(Position(source, marker));
		}
		private static int Position(string source, string marker)
		{
			int index = source.IndexOf(marker, StringComparison.Ordinal);
			Assert.GreaterOrEqual(index, 0, "Missing source-contract boundary: " + marker);
			return index;
		}
		private static void Before(string source, string first, string second)
			=> Assert.Less(Position(source, first), Position(source, second), first + " must precede " + second);
	}
}
#endif
