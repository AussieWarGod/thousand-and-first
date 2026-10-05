using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// #283, design A4: the evidence the ordinary tier upgrade journals AFTER its handover wait and
	/// before its verdict. The receipt the begun check journaled is a phase-four snapshot; this row
	/// is read fresh, so a stall names its first cause natively: the job's phase, physical phase,
	/// recorded failure and update tick (the job's failure, not the improvement's, which a later
	/// refusal rewrites every pass), the improvement's own quarantine flags, the predecessor's
	/// authored upgrade phase and fault, both works' heart-yielding marks, the readmission
	/// marker, the successor's layout fault, next layer, pending marker and scaffold-removal
	/// intent, and one capture-only seal reading with its reason. Read-only; nothing here drives
	/// the upgrade. The stuck-save heal route reads the same evidence in both of its sessions.
	/// </summary>
	internal static partial class KingdomTierUpgradeChecks
	{
		/// <summary>Journal row the final check lands immediately before its own verdict.</summary>
		internal const string AfterWaitRow = "tier-upgrade-after-wait";

		/// <summary>Production's once-only readmission marker
		/// (<c>Growth/KingdomUpgrade.27.RetiredDefectReadmission.cs</c>), spelled here so this
		/// evidence also compiles against a build without the #283 fix, which the heal route's first
		/// session runs. A DevTests pin keeps the two spellings equal.</summary>
		internal const string ReadmittedMarker = "r_TAF_ImprovementReadmitted";

		/// <summary>The fresh evidence text for one improvement job and its predecessor.</summary>
		internal static string HandoverEvidence(XRLGame Game, Zone Zone, string JobId,
			string PredecessorId, out bool SealFault)
		{
			StringBuilder text = new StringBuilder("tick=").Append(Game.TimeTicks)
				.Append("; job=").Append(JobId);
			KingdomConstructionJob job;
			bool found = KingdomConstruction.TryFind(JobId, out job) && job != null;
			text.Append("; phase=").Append(found ? job.Phase.ToString() : "absent");
			if (found)
				text.Append("; physical=").Append(job.PhysicalPhase)
					.Append("; failure=").Append(KingdomScenarioRules.Bounded(job.Failure ?? "none"))
					.Append("; updated=").Append(job.UpdatedTick);
			GameObject predecessor = ExactIn(Zone, PredecessorId);
			text.Append("; predecessor=").Append(predecessor == null ? "absent" : PredecessorId);
			if (predecessor != null)
				text.Append("; upgrade-phase=").Append(predecessor.GetIntProperty(
						KingdomArchitectureStamper.UpgradePhaseProperty))
					.Append("; upgrade-fault=").Append(Text(predecessor,
						KingdomArchitectureStamper.UpgradeFaultProperty))
					.Append("; predecessor-yielding=").Append(predecessor.GetIntProperty(
						KingdomPlots.YieldingProperty))
					.Append("; readmitted=").Append(Text(predecessor, ReadmittedMarker));
			r_KingdomImprovement intent = predecessor == null ? null
				: predecessor.GetPart<r_KingdomImprovement>();
			text.Append("; intent=").Append(intent == null ? "absent" : "present");
			if (intent != null)
				text.Append("; intent-quarantined=").Append(intent.HandoverQuarantined)
					.Append("; intent-failure=").Append(KingdomScenarioRules.Bounded(
						intent.HandoverFailure ?? "none"))
					.Append("; effects-done=").Append(intent.HandoverEffectsDone);
			GameObject successor = found && !string.IsNullOrEmpty(job.OutputId)
				? ExactIn(Zone, job.OutputId) : null;
			text.Append("; successor=").Append(successor == null ? "absent" : job.OutputId);
			if (successor != null)
				text.Append("; successor-yielding=").Append(successor.GetIntProperty(
						KingdomPlots.YieldingProperty))
					.Append("; layout-fault=").Append(Text(successor,
						KingdomArchitectureStamper.FaultProperty))
					.Append("; next-layer=").Append(successor.GetIntProperty(
						KingdomArchitectureStamper.NextLayerProperty))
					.Append("; pending=").Append(successor.GetIntProperty(
						r_KingdomScaffold.PendingImprovementSuccessorProperty))
					.Append("; scaffold-intent=").Append(Text(successor,
						r_KingdomScaffold.ScaffoldRemovalIntentIdProperty));
			return text.Append("; ").Append(ReadSeal(Game, out SealFault)).ToString();
		}

		/// <summary>One capture-only seal reading: captured, spatial result, the settlement
		/// pass's fault rule and the reason. An observation that changed the staged record, or
		/// no seal authority at all, reads as a fault.</summary>
		internal static string ReadSeal(XRLGame Game, out bool Fault)
		{
			KingdomSeal seal = Game.GetSystem<KingdomSeal>();
			bool captured = false;
			KingdomInheritanceSpatialCaptureResult spatial = default(KingdomInheritanceSpatialCaptureResult);
			string reason = null;
			bool unchanged = seal != null
				&& seal.NativeSpatialCaptureReading(out captured, out spatial, out reason);
			Fault = !unchanged || KingdomSealSpatialRules.SpatialCaptureIsFault(captured, spatial);
			return "seal-captured=" + captured + "; seal-spatial=" + spatial + "; seal-fault="
				+ Fault + "; seal-observation-unchanged=" + unchanged + "; seal-reason="
				+ KingdomScenarioRules.Bounded(string.IsNullOrEmpty(reason) ? "none" : reason);
		}

		internal static GameObject ExactIn(Zone Zone, string Id)
		{
			GameObject found;
			return !string.IsNullOrEmpty(Id) && KingdomConstruction.FindExactId(Zone, Id, out found)
				== KingdomPhysicalLookupState.Exact ? found : null;
		}

		private static string Text(GameObject Item, string Property)
		{
			if (Item.HasIntProperty(Property))
				return "int:" + Item.GetIntProperty(Property);
			string value = Item.GetStringProperty(Property);
			return string.IsNullOrEmpty(value) ? "none" : KingdomScenarioRules.Bounded(value);
		}

		private sealed partial class Frame
		{
			/// <summary>Whether the standing tent carried the heart-yielding mark when the paid
			/// improvement began; the successor must carry it after the handover.</summary>
			internal bool PredecessorYielding;

			internal bool AfterWaitSealFault = true;
			internal string AfterWaitEvidence;

			private void RecordBegunMarks(GameObject Tent)
			{
				PredecessorYielding = Tent.GetIntProperty(KingdomPlots.YieldingProperty) == 1;
				Evidence.Append("; predecessor-yielding=").Append(PredecessorYielding ? 1 : 0);
			}

			private void RecordAfterWait()
			{
				AfterWaitEvidence = HandoverEvidence(Game, Zone, ImprovementJobId, PredecessorId,
					out AfterWaitSealFault);
				Require(KingdomScenarioJournal.Append(AfterWaitRow, true,
					"native-tier-upgrade after-wait; " + AfterWaitEvidence) == null,
					"the tier-upgrade after-wait evidence could not be journalled");
			}

			/// <summary>After the verdict's own exact-endpoint checks: the heart-yielding promise
			/// survived the renovation, and the seal reads the finished work without a fault.
			/// </summary>
			private void RequireCarriedMarksAndCleanSeal(GameObject Successor)
			{
				Require(!PredecessorYielding
					|| Successor.GetIntProperty(KingdomPlots.YieldingProperty) == 1,
					"the successor dropped the predecessor's heart-yielding mark");
				Require(!AfterWaitSealFault,
					"the seal reads the handed-over work as a fault: " + AfterWaitEvidence);
			}
		}
	}
}
