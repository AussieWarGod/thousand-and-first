using System.IO;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// #283 heal route, session one: the retained tier-upgrade frame saves the stuck renovation.
	/// This runs on a build WITHOUT the fix, after the third ordinary wait, and refuses unless the
	/// paid renovate stands quarantined by one of the two retired causes with its successor still
	/// pending: that refusal is what proves session one reproduced the defect. It journals the
	/// same fresh evidence the tier-upgrade check does, then the snapshot session two compares,
	/// then one real engine save and its receipt. It drives nothing.
	/// </summary>
	internal static partial class KingdomTierUpgradeChecks
	{
		internal static string HealSave(XRLGame Game, Zone Zone)
		{
			Require(Retained != null && ReferenceEquals(Retained.Game, Game)
				&& ReferenceEquals(Retained.Zone, Zone), "the retained tier-upgrade frame is absent");
			return Retained.HealSave();
		}

		private sealed partial class Frame
		{
			internal string HealSave()
			{
				Require(Armed && !Done && Phase == 4,
					"the heal save must follow the begun check exactly once");
				RecordAfterWait();
				KingdomConstructionJob job;
				Require(KingdomConstruction.TryFind(ImprovementJobId, out job) && job != null,
					"the paid improvement receipt is absent");
				string defect = KingdomRenovateHealScript.DefectOf(job.Failure);
				Require(job.Route == KingdomConstructionRoute.Improvement
					&& job.Phase == KingdomConstructionPhase.InspectionRequired
					&& job.PhysicalPhase == KingdomPhysicalPhase.None && defect != null,
					"session one did not leave the paid renovation under inspection by a retired "
						+ "#283 cause: " + AfterWaitEvidence);
				GameObject predecessor = Exact(PredecessorId);
				GameObject successor = string.IsNullOrEmpty(job.OutputId) ? null : Exact(job.OutputId);
				Require(predecessor != null && successor != null && job.SubjectId == PredecessorId
					&& successor.GetIntProperty(r_KingdomScaffold.PendingImprovementSuccessorProperty) == 1,
					"the stuck renovation's predecessor or pending successor is not exact");
				int upgradePhase = predecessor.GetIntProperty(KingdomArchitectureStamper.UpgradePhaseProperty);
				Require(defect != "A" || PredecessorYielding && upgradePhase == 5
					&& successor.GetIntProperty(KingdomPlots.YieldingProperty) != 1,
					"the founder-marks stall lacks its yielding and phase-five shape");
				Require(!predecessor.HasStringProperty(ReadmittedMarker)
					&& !predecessor.HasIntProperty(ReadmittedMarker),
					"a build without the fix carries a readmission marker");
				Require(AfterWaitSealFault, "the stuck renovation did not read as a seal fault");
				string intentId = successor.GetStringProperty(r_KingdomScaffold.ScaffoldRemovalIntentIdProperty);
				KingdomRenovateHealSnapshot snapshot = new KingdomRenovateHealSnapshot(Game.GameID,
					Zone.ZoneID, job.Id, PredecessorId, job.OutputId, defect, upgradePhase, intentId,
					Game.TimeTicks);
				string wire;
				Require(KingdomRenovateHealSnapshot.TryEncode(snapshot, out wire),
					"the heal snapshot could not encode");
				string primary = SaveStuck(wire);
				Done = true;
				return "renovate-heal-save=true; defect=" + defect + "; job=" + job.Id
					+ "; predecessor=" + PredecessorId + "; successor=" + job.OutputId
					+ "; upgrade-phase=" + upgradePhase + "; scaffold-intent=" + intentId
					+ "; save=" + Game.GameID + "; time-ticks=" + Game.TimeTicks
					+ "; primary-sha256=" + primary
					+ "; snapshot-sha256=" + KingdomScenarioSaveFiles.HashText(wire);
			}

			/// <summary>Publishes the snapshot, writes one real engine save into the exact owned
			/// save directory and its five-line receipt. Same evidence shape as every sealed
			/// scenario save Tools/prepare-scenario-load.py carries.</summary>
			private string SaveStuck(string Wire)
			{
				string root = KingdomScenarioSaveFiles.Root();
				string directory = KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID);
				Require(!KingdomScenarioSaveFiles.LoadPresent()
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile))
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile))
					&& !KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioSaveFiles.SnapshotKey)
					&& Directory.GetDirectories(directory).Length == 0, "heal save evidence is not empty");
				foreach (string path in Directory.GetFiles(directory))
					Require(Path.GetFileName(path) == "Cache.db", "the heal save already has primary evidence");
				Game.SetStringGameState(KingdomScenarioSaveFiles.SnapshotKey, Wire);
				Require(KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, Wire),
					"the heal snapshot did not publish exactly");
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), Wire);
				long ticks = Game.TimeTicks;
				The.ZoneManager.CheckCached(true, true);
				string primary = KingdomUnfoundedSave.SavePrimary(Game, directory);
				Require(ReferenceEquals(The.Game, Game) && Game.TimeTicks == ticks,
					"the stuck renovation changed across serialization");
				string receipt = "taf-scenario-save-v1\n" + Game.GameID + "\n" + primary + "\n"
					+ KingdomScenarioSaveFiles.HashFile(Path.Combine(directory, "Primary.json"), 1048576)
					+ "\n" + KingdomScenarioSaveFiles.HashText(Wire) + "\n";
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), receipt);
				Require(KingdomScenarioSaveFiles.ReadText(Path.Combine(root,
					KingdomScenarioSaveFiles.ReceiptFile), 512) == receipt, "the heal save receipt changed");
				return primary;
			}
		}
	}
}
