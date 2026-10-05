using System;
using System.IO;
using HarmonyLib;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// #283 heal route, session two, on the FIXED build: a separate fresh profile cold-loads the
	/// stuck save session one wrote on a build without the fix. Before activation it proves the
	/// loaded world is still the saved stall; after vanilla Continue it resumes ordinary turns, so
	/// the real settlement pass (not this harness) readmits the job once and hands it over. It
	/// then reads the result fresh, requires a finished tent-row, a retired tent, a carried
	/// yielding mark and a seal reading without a fault, and writes a second real save. The
	/// readmission's own log line is checked by Tools/check-renovate-heal-results.py.
	/// </summary>
	internal static class KingdomRenovateHealLoad
	{
		internal const string PreactivationRow = "renovate-heal-preactivation";
		internal const string ResumeRow = "renovate-heal-resume";
		internal const string AfterRow = "renovate-heal-after";
		internal const string ResavedRow = "renovate-heal-resaved";
		private static XRLGame Witnessed, Resuming;
		private static int Attempts;
		private static bool Finished, PriorPopup;
		private static string Failure;
		internal static bool OwnsPopups;

		internal static void BeforeActivation()
		{
			try
			{
				Check(++Attempts == 1 && Witnessed == null && KingdomScenarioLoadEntry.Armed
					&& KingdomScenarioLoadReaderWitness.Releases == 1
					&& !KingdomScenarioLoadReaderWitness.HadErrors,
					"heal primary reader or pre-activation witness is not exact");
				XRLGame game = The.Game;
				KingdomRenovateHealSnapshot saved = KingdomScenarioLoadEntry.HealSnapshot;
				Check(game != null && saved != null && game.GameID == saved.GameId
					&& game.TimeTicks == saved.TimeTicks
					&& game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey,
						KingdomScenarioLoadEntry.SnapshotWire), "the loaded game, clock or snapshot changed");
				Zone zone = The.ZoneManager?.ActiveZone;
				Check(zone != null && zone.ZoneID == saved.ZoneId, "the loaded ground is not the saved one");
				KingdomConstructionJob job;
				Check(KingdomConstruction.TryFind(saved.JobId, out job) && job != null
					&& job.Phase == KingdomConstructionPhase.InspectionRequired
					&& job.PhysicalPhase == KingdomPhysicalPhase.None
					&& KingdomRenovateHealScript.DefectOf(job.Failure) == saved.Defect
					&& job.SubjectId == saved.PredecessorId && job.OutputId == saved.SuccessorId,
					"the loaded improvement is not the saved stall");
				GameObject predecessor = KingdomTierUpgradeChecks.ExactIn(zone, saved.PredecessorId);
				GameObject successor = KingdomTierUpgradeChecks.ExactIn(zone, saved.SuccessorId);
				Check(predecessor != null && successor != null
					&& predecessor.GetIntProperty(KingdomArchitectureStamper.UpgradePhaseProperty)
						== saved.UpgradePhase
					&& successor.GetIntProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty) == 1
					&& !predecessor.HasStringProperty(KingdomTierUpgradeChecks.ReadmittedMarker),
					"the loaded predecessor or pending successor differs from the saved stall");
				Witnessed = game;
				Check(KingdomScenarioJournal.Append(PreactivationRow, true,
					"before-AfterGameLoaded=true; stuck=true; defect=" + saved.Defect + "; job="
					+ saved.JobId + "; predecessor=" + saved.PredecessorId + "; successor="
					+ saved.SuccessorId + "; upgrade-phase=" + saved.UpgradePhase + "; time-ticks="
					+ saved.TimeTicks) == null, "heal pre-activation journal unavailable");
			}
			catch (Exception error)
			{
				Failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
				KingdomScenarioJournal.Append(PreactivationRow, false, Failure);
			}
		}

		internal static void Prepare(XRLGame Game, bool OriginalPopup)
		{
			Check(Resuming == null && !Finished && KingdomScenarioLoadEntry.Armed && Game.Running
				&& Attempts == 1 && Failure == null && ReferenceEquals(Witnessed, Game)
				&& ReferenceEquals(Game, The.Game)
				&& Game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true
				&& !KingdomScenarioAdvance.Pending && !KingdomScenarioFrames.Pending
				&& KingdomScenarioScript.TryRead(out var script, out _)
				&& KingdomRenovateHealScript.Matches(script),
				"heal continuation is not the exact running loaded scenario: " + Failure);
			Check(!KingdomScenarioLoadReaderWitness.HadErrors, "engine reported heal deserialization errors");
			KingdomScenarioAdvance.ArmDriver();
			new Harmony("com.thousandandfirst.harness.renovate-heal-turns").Patch(
				AccessTools.Method(typeof(KingdomScenarioAdvance), "Pump"),
				postfix: new HarmonyMethod(typeof(KingdomRenovateHealLoad), nameof(AfterPump)));
			string result = KingdomScenarioAdvance.Run(KingdomRenovateHealScript.HealTurns.ToString(
				System.Globalization.CultureInfo.InvariantCulture), out bool ok);
			Check(ok && KingdomScenarioAdvance.Pending, "loaded heal advance refused: " + result);
			Check(KingdomScenarioJournal.Append(ResumeRow, true, "vanilla-Continue=true; "
				+ "saved-script-considered=true; requested-turns=" + KingdomRenovateHealScript.HealTurns
				+ "; defect=" + KingdomScenarioLoadEntry.HealSnapshot.Defect) == null,
				"heal continuation journal unavailable");
			Resuming = Game;
			PriorPopup = OriginalPopup;
			OwnsPopups = true;
			Popup.Suppress = true;
		}

		private static void AfterPump(bool __result, bool Faulted)
		{
			if (Resuming == null || Finished || __result) return;
			Finished = true;
			try
			{
				Check(!Faulted && !KingdomScenarioLoadEntry.Armed && ReferenceEquals(The.Game, Resuming)
					&& Resuming.Running && !KingdomScenarioAdvance.Pending,
					"loaded heal advance failed or preceded Continue release");
				Complete(Resuming, KingdomScenarioLoadEntry.HealSnapshot);
			}
			catch (Exception error)
			{
				KingdomScenarioJournal.Append("SCRIPT-STOPPED", false,
					"heal cold-load continuation refused: " + KingdomScenarioRules.Bounded(error.Message));
			}
			finally
			{
				if (OwnsPopups) Popup.Suppress = PriorPopup;
				OwnsPopups = false;
			}
		}

		private static void Complete(XRLGame Game, KingdomRenovateHealSnapshot Saved)
		{
			Zone zone = The.ZoneManager?.ActiveZone;
			Check(zone != null && zone.ZoneID == Saved.ZoneId, "the healed ground is not the saved one");
			string evidence = KingdomTierUpgradeChecks.HandoverEvidence(Game, zone, Saved.JobId,
				Saved.PredecessorId, out bool sealFault);
			Journal(AfterRow, "native-renovate-heal after; " + evidence);
			KingdomConstructionJob job;
			Check(KingdomConstruction.TryFind(Saved.JobId, out job) && job != null
				&& job.Phase == KingdomConstructionPhase.Complete
				&& job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled
				&& job.SubjectId == Saved.PredecessorId && job.OutputId == Saved.SuccessorId,
				"the readmitted improvement did not close and settle on its exact endpoints");
			GameObject absent;
			Check(KingdomConstruction.FindExactId(zone, Saved.PredecessorId, out absent)
				== KingdomPhysicalLookupState.Absent && absent == null, "the stuck tent still stands");
			GameObject row = KingdomTierUpgradeChecks.ExactIn(zone, Saved.SuccessorId);
			Check(row != null && KingdomUpgrade.IsFunctionallyBuilt(row)
				&& KingdomUpgrade.DesignKeyOf(row) == KingdomTierUpgradeChecks.ToKey
				&& row.GetIntProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty) != 1
				&& row.GetStringProperty(r_KingdomScaffold.RemovalProofProperty) == Saved.PredecessorId,
				"no finished tent-row stands with exact predecessor-removal proof");
			Check(Saved.Defect != "A" || row.GetIntProperty(KingdomPlots.YieldingProperty) == 1,
				"the healed tent-row dropped the tent's heart-yielding mark");
			Check(!sealFault, "the seal reads the healed work as a fault: " + evidence);
			string root = KingdomScenarioSaveFiles.Root();
			string directory = KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID);
			string imported = KingdomScenarioLoadEntry.Request.PrimarySha256;
			string primary = KingdomUnfoundedSave.SavePrimary(Game, directory);
			string backup = Path.Combine(directory, "Primary.sav.gz.bak");
			Check(File.Exists(backup) && KingdomScenarioSaveFiles.HashFile(backup,
				KingdomScenarioSaveFiles.MaxSaveBytes) == imported, "the engine backup is not the stuck save");
			Check(primary != imported, "the second save did not replace the stuck primary");
			Journal(ResavedRow, "real-save=true; healed=true; defect=" + Saved.Defect + "; save-error=false"
				+ "; backup-is-imported-save=true; primary-changed=true; primary-sha256=" + primary
				+ "; ordinary-acceptance=false");
			Journal("SCRIPT-COMPLETE", "native-renovate-heal cold-load complete; real-save-quit-load=true"
				+ "; readmitted-handover-complete=true; second-real-save=true"
				+ "; new-game-script-replayed=false; ordinary-acceptance=false");
		}

		private static void Journal(string Row, string Message)
		{ Check(KingdomScenarioJournal.Append(Row, true, Message) == null, Row + " journal failed"); }

		private static void Check(bool Condition, string Message)
		{ KingdomScenarioSaveFiles.Require(Condition, Message ?? "heal cold load refused"); }
	}
}
