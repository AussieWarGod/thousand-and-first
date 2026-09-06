#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	// Wiring assertions only; these do not execute the engine reader, callbacks, or physical effects.
	[TestFixture]
	public sealed class KingdomWearLoadSafetySourceTests
	{
		private const string Part = "Growth/KingdomWear.00.r_KingdomWear.cs";
		private const string Safety = "Growth/KingdomWear.00a.LoadSafety.cs";
		private const string Repair = "Growth/KingdomWear.01.RepairRecovery.cs";
		private const string Target = "Growth/KingdomWear.02.RepairTargetAndCarry.cs";
		private const string Leak = "Growth/KingdomWear.09.LeakContinuation.cs";
		private const string Damage = "Growth/KingdomWear.06.DamageIncidents.cs";
		private const string LeakEntry = "Growth/KingdomWear.07.LeakEntry.cs";
		private const string Completion = "Growth/KingdomWear.13.RepairCompletion.cs";

		[Test]
		public void PrivateNonserializedLatchCannotBeAssignedByPublicNamedFieldReader()
		{
			string safety = Source(Safety);
			StringAssert.Contains("[NonSerialized] private bool WearReadFailed;", safety);
			StringAssert.Contains("[NonSerialized] private bool WearReadNoticeAttempted;", safety);
			StringAssert.Contains("internal bool LoadFailed => WearReadFailed;", safety);
			StringAssert.DoesNotContain("WearReadFailed = false", safety);
			StringAssert.DoesNotContain("WearReadNoticeAttempted = false", safety);
			StringAssert.Contains("public partial class r_KingdomWear : IPart", Source(Part));
		}

		[Test]
		public void WholeReadIncludingNormalizationLatchesBeforeRethrowAndRefusesReread()
		{
			string read = Method(Safety, "public override void Read(");
			Ordered(read, "try {", "if (WearReadFailed)", "throw new InvalidOperationException(",
				"object first = Reader.ReadObject();", "Reader.ReadNamedFields(this, typeof(r_KingdomWear));",
				"NormalizeSerializedFields();", "catch (Exception)", "LatchWearReadFailure();", "throw;");
			StringAssert.DoesNotContain("public override void Read(", Source(Part));
		}

		[Test]
		public void ExistingNamedWriteAndSevenLegacyFieldsKeepTheirOrder()
		{
			Ordered(Method(Part, "public override void Write("), "Writer.WriteObject(SerializationMagic);",
				"Writer.WriteObject(CurrentSerializationVersion);", "Writer.WriteNamedFields(this, typeof(r_KingdomWear));");
			Ordered(Method(Safety, "public override void Read("), "Wear = Convert.ToInt32(first);",
				"LastCause = Convert.ToInt32(Reader.ReadObject());", "Held = Convert.ToBoolean(Reader.ReadObject());",
				"RepairEffortLeft = Convert.ToInt32(Reader.ReadObject());", "LastLeakTick = Convert.ToInt64(Reader.ReadObject());",
				"LeakAnnounced = Convert.ToBoolean(Reader.ReadObject());", "AnnouncedBlock = Convert.ToInt32(Reader.ReadObject());");
			StringAssert.Contains("private const int SerializationMagic = 1415009618;", Source(Part));
			StringAssert.Contains("private const int CurrentSerializationVersion = 1;", Source(Part));
		}

		[Test]
		public void ReadErrorUsesRealEngineSkipContractWithoutResettingReceiptEvidence()
		{
			Ordered(Method(Safety, "public override bool ReadError("), "LatchWearReadFailure();", "return false;");
			string latch = Method(Safety, "private void LatchWearReadFailure(");
			Ordered(latch, "WearReadFailed = true;", "LifecycleQuarantined = true;", "LogWearReadFailure(");
			foreach (string field in new[] { "Wear", "LastCause", "IncidentPhase", "IncidentId", "IncidentCause",
				"IncidentBeforeWear", "IncidentAfterWear", "IncidentLine", "LastCompletedIncidentId",
				"IncidentMessageState", "LeakPhase", "RepairEffortLeft" })
				StringAssert.DoesNotContain(field + " =", latch);
			StringAssert.DoesNotContain("NormalizeSerializedFields", latch);
		}

		[Test]
		public void AfterLoadWarningIsOneAttemptAndBaseDispatchIsIndependentlyGuarded()
		{
			StringAssert.Contains("ID == AfterGameLoadedEvent.ID", Method(Part, "public override bool WantEvent("));
			string notice = Method(Safety, "public override bool HandleEvent(AfterGameLoadedEvent E)");
			Ordered(notice, "if (WearReadFailed)", "LifecycleQuarantined = true;", "if (!WearReadNoticeAttempted)",
				"WearReadNoticeAttempted = true;", "try {", "MessageQueue.AddPlayerMessage(", "catch (Exception)",
				"try { return base.HandleEvent(E); }", "catch (Exception)", "return true;");
			StringAssert.DoesNotContain("Exception.Message", Source(Safety));
			StringAssert.DoesNotContain("Exception.ToString", Source(Safety));
			Ordered(Method(Safety, "private static void LogWearReadFailure("),
				"try { MetricsManager.LogError(message); }", "catch (Exception)");
		}

		[TestCase(Repair, "internal static void RetryConstruction(", "wear.LoadFailed || wear.LifecycleQuarantined", "if (finishing)")]
		[TestCase(Repair, "internal static void InspectConstruction(", "wear.LoadFailed || wear.LifecycleQuarantined", "if (!finishing")]
		[TestCase(Target, "private static bool TryCaptureRepairTarget(", "Wear.LoadFailed || Wear.LifecycleQuarantined", "new RepairTargetFrame")]
		[TestCase(Target, "private static bool RepairTargetExact(", "!Frame.WearPart.LoadFailed && !Frame.WearPart.LifecycleQuarantined", "Frame.WearPart.Wear == Frame.Wear")]
		[TestCase(Completion, "private static void AdvanceRepair(", "WearPart.LoadFailed || WearPart.LifecycleQuarantined", "KingdomMaterials.ReadTick(")]
		[TestCase(Completion, "private static bool FinishRepairProjection(", "WearPart.LoadFailed || WearPart.LifecycleQuarantined", "KingdomConstruction.BeginProjection(")]
		public void RepairEntriesRefuseStickyOrPersistedQuarantineBeforeEffects(string path, string signature, string guard, string effect)
		{
			Ordered(Method(path, signature), guard, effect);
		}

		[Test]
		public void LeakRefusesBeforeRetirementMutationAndImmediatelyAfterCallbacks()
		{
			string leak = Method(Leak, "private static void ContinueBoundLeak(");
			string guard = "if (Wear.LoadFailed || Wear.LifecycleQuarantined) return;";
			Ordered(leak, "if (Wear == null || Wear.LoadFailed || Wear.LifecycleQuarantined) return;",
				"RetireFoodLeakReceipt(", guard, "Wear.LeakPhase = (int)KingdomWearLeakPhase.MutationIntent;",
				"Survey.TryLeakFromExact(", guard, "LeakWorkExact(", "boundBed.UseCharge(", guard,
				"bool stillExact =", guard, "Wear.LeakActualLost = Wear.LeakWanted;");
		}

		[Test]
		public void DamageRefusesBeforeRepairBasedSuccessAndLeakBeforeFoodRetirement()
		{
			Ordered(Method(Damage, "private static bool ApplyDamageIncident("), "if (wear.LoadFailed) return false;",
				"if (wear.LifecycleQuarantined)", "HasActiveRepair(Work, out _)", "wear.IncidentId = IncidentId;");
			Ordered(Method(LeakEntry, "private static void Leak("), "if (Wear == null || Wear.LoadFailed) return;",
				"if (Wear.LifecycleQuarantined)", "RetireFoodLeakReceipt(Work, Wear);");
		}

		[Test]
		public void CarryRefusesFailedTargetBeforeAndAfterPartAcquisition()
		{
			Ordered(Method(Target, "public static bool TryCarryStableState("),
				"targetWear.LoadFailed || targetWear.LifecycleQuarantined", "Target.RequirePart<r_KingdomWear>();",
				"after.LoadFailed || after.LifecycleQuarantined", "after.Wear = before.Wear;");
		}

		[Test]
		public void DamageNamingRefusesBeforeAnyNewIncidentReceiptFieldIsPublished()
		{
			string damage = Method(Damage, "private static bool ApplyDamageIncident(");
			string callback = "string name = DisplayName(Work);";
			StringAssert.Contains(callback + " if (wear.LoadFailed || wear.LifecycleQuarantined) return false;", damage);
			int at = damage.IndexOf(callback, StringComparison.Ordinal);
			foreach (string field in new[] { "IncidentId", "IncidentCause", "IncidentBeforeWear",
				"IncidentAfterWear", "IncidentLine", "IncidentMessageState", "IncidentPhase" })
				StringAssert.DoesNotContain("wear." + field + " = ", damage.Substring(0, at));
			Ordered(damage, callback, "if (wear.LoadFailed || wear.LifecycleQuarantined) return false;",
				"string line = KingdomWearRules.DamagedLine(name, Cause, afterWear);",
				"wear.IncidentId = IncidentId;", "wear.IncidentLine = line;",
				"wear.IncidentPhase = (int)KingdomWearIncidentPhase.Bound;");
			StringAssert.Contains("HasActiveRepair(Work, out _)) return !wear.LoadFailed && !wear.LifecycleQuarantined;", damage);
		}

		[Test]
		public void DamageChronicleRefusesImmediatelyBeforeAdvancingThePhase()
		{
			string damage = Method(Damage, "private static bool ApplyDamageIncident(");
			StringAssert.Contains("bool recorded = KingdomChronicle.RecordOnce(System, wear.IncidentId + \":chronicle\","
				+ " wear.IncidentLine); if (wear.LoadFailed || wear.LifecycleQuarantined) return false;"
				+ " if (!recorded) return false; wear.IncidentPhase = (int)KingdomWearIncidentPhase.ChronicleDone;", damage);
		}

		[Test]
		public void DamageMessageKeepsItsOneShotIntentButRefusesBeforePhaseAdvance()
		{
			string damage = Method(Damage, "private static bool ApplyDamageIncident(");
			StringAssert.Contains("wear.IncidentPhase = (int)KingdomWearIncidentPhase.MessageIntent;"
				+ " DeliverWearMessage(ref wear.IncidentMessageState, \"{{r|\" + wear.IncidentLine + \"}}\");"
				+ " if (wear.LoadFailed || wear.LifecycleQuarantined) return false;"
				+ " wear.IncidentPhase = (int)KingdomWearIncidentPhase.MessageDone;", damage);
		}

		[Test]
		public void DamageFinalRefusalPrecedesCompletionAcknowledgementAndReceiptClearing()
		{
			string damage = Method(Damage, "private static bool ApplyDamageIncident(");
			Ordered(damage, "KingdomLog.Log(", "if (wear.LoadFailed || wear.LifecycleQuarantined) return false;",
				"if (phase != KingdomWearIncidentPhase.Complete) return false;", "wear.LastCompletedIncidentId = IncidentId;",
				"wear.IncidentPhase = (int)KingdomWearIncidentPhase.None;", "wear.IncidentId = null;",
				"wear.IncidentLine = null;", "return true;");
		}

		[Test]
		public void NoHandsNamingAndLedgerCallbacksRefuseBeforeTheCheckpoint()
		{
			string repair = Method(Completion, "private static void AdvanceRepair(");
			string guard = "if (WearPart.LoadFailed || WearPart.LifecycleQuarantined) return;";
			StringAssert.Contains("string blockName = DisplayName(Work); " + guard, repair);
			StringAssert.Contains("System.Ledger.Note(\"{{r|\" + blockLine + \"}}\"); " + guard, repair);
			Ordered(repair, "string blockName = DisplayName(Work);", guard,
				"WearPart.AnnouncedBlock = (int)KingdomWearRules.RepairVerdict.NoHands;",
				"System.Ledger.Note(", guard,
				"KingdomMaterials.WriteTick(Work, RepairWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));");
		}

		[Test]
		public void RepairNamingAndOutboxCallbackRefuseWithoutPublishingAReplacementFailure()
		{
			string repair = Method(Completion, "private static bool FinishRepairProjection(");
			StringAssert.Contains("string name = DisplayName(Work); " + RepairRefusal, repair);
			StringAssert.Contains("bool prepared = KingdomCeremony.PrepareWearRepaired(System, name, leakStopped, ref Updated); "
				+ RepairRefusal + " if (!prepared", repair);
			Ordered(repair, "string name = DisplayName(Work);", RepairRefusal, "KingdomCeremony.PrepareWearRepaired(",
				RepairRefusal, "KingdomConstruction.Quarantine(ref Updated, Failure);",
				"Work.SetStringProperty(RepairRemovalAttemptProperty, Updated.Id);");
		}

		[Test]
		public void RemovalCallbackRefusesBeforeExactProofAndCompletionWithoutClearingItsIntent()
		{
			string repair = Method(Completion, "private static bool FinishRepairProjection(");
			StringAssert.Contains("catch (Exception) { callbackReturned = false; } " + RepairRefusal
				+ " bool exactRemoval =", repair);
			Ordered(repair, "Work.SetStringProperty(RepairRemovalAttemptProperty, Updated.Id);",
				"Work.RemovePart(WearPart);", RepairRefusal, "bool exactRemoval =",
				"Work.SetStringProperty(RepairRemovalProofProperty, Updated.Id);",
				"Work.RemoveStringProperty(RepairRemovalAttemptProperty);", RepairRefusal,
				"if (!KingdomConstruction.Complete(ref Updated)) return false;");
		}

		[Test]
		public void RepairCompletionDispatchAndFinalSuccessRecheckTheHeldPartFailureLatch()
		{
			string repair = Method(Completion, "private static bool FinishRepairProjection(");
			StringAssert.Contains("if (!KingdomConstruction.Complete(ref Updated)) return false; "
				+ RepairRefusal + " Work.RemoveStringProperty(RepairRemovalProofProperty);", repair);
			StringAssert.Contains("bool dispatched = KingdomCeremony.DispatchPending(System, ref Updated); "
				+ RepairRefusal + " KingdomLog.Log(\"wear: repair complete \" + Work.Blueprint); "
				+ RepairRefusal + " return dispatched;", repair);
		}

		private const string RepairRefusal = "if (WearPart.LoadFailed || WearPart.LifecycleQuarantined) { "
			+ "Failure = \"The wear record is quarantined; its repair will not be resumed.\"; return false; }";

		private static string Source(string path)
		{
			string text = Regex.Replace(TestMain.ReadRepositoryText(path), @"//[^\r\n]*|/\*[\s\S]*?\*/", " ");
			return Regex.Replace(text, @"\s+", " ");
		}

		private static string Method(string path, string signature)
		{
			string text = Source(path); int start = text.IndexOf(signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, signature);
			int open = text.IndexOf('{', start), depth = 0;
			for (int i = open; i < text.Length; i++)
			{
				if (text[i] == '{') depth++;
				else if (text[i] == '}' && --depth == 0) return text.Substring(open, i - open + 1);
			}
			Assert.Fail("Unclosed method: " + signature); return null;
		}

		private static void Ordered(string source, params string[] needles)
		{
			int prior = -1;
			foreach (string needle in needles)
			{
				int at = source.IndexOf(needle, prior + 1, StringComparison.Ordinal);
				Assert.Greater(at, prior, needle); prior = at;
			}
		}
	}
}
#endif
