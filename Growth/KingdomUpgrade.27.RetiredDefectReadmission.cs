using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		/// <summary>Once-only marker on the predecessor: the id of the job readmitted after a
		/// retired #283 handover defect. Its presence refuses any second readmission.</summary>
		public const string ReadmittedProperty = "r_TAF_ImprovementReadmitted";

		/// <summary>
		/// Readmits, once, a paid improvement that a retired #283 defect left in
		/// InspectionRequired: signature A (the authored lane dropped the yielding mark after the
		/// layout was rebuilt) or signature B (a build before the fix refused the nulled reference
		/// of a landed scaffold). Neither can be written by a build with the fix. Ruled by the
		/// root (Q1) for dev and the 0.3.9 hotfix.
		/// <para>
		/// Plan before effect: every fact is read and judged by the engine-free
		/// <see cref="KingdomUpgradeRules.ClassifyRetiredHandoverDefect"/> first. Only then does it
		/// write the once-only marker, clear the improvement's own quarantine flags (which alone
		/// refuse every content carry), move the job to Outstanding through
		/// <see cref="KingdomConstruction.Readmit"/> and say so in the log. The caller falls through
		/// to <see cref="HandOver"/> in the same call, which re-proves everything; a second refusal
		/// quarantines with its real cause and the marker stops another readmission.
		/// </para>
		/// </summary>
		/// <returns>True when the job was readmitted.</returns>
		internal static bool TryReadmitRetiredHandoverDefect(GameObject Predecessor,
			GameObject Successor, string SuccessorKey, ref KingdomConstructionJob Job)
		{
			if (Job == null || Job.Phase != KingdomConstructionPhase.InspectionRequired
				|| Job.Route != KingdomConstructionRoute.Improvement
				|| KingdomConstructionRules.RetiredHandoverDefectFor(Job.Failure)
					== KingdomRetiredHandoverDefect.None
				|| !GameObject.Validate(Predecessor) || !GameObject.Validate(Successor)
				|| Predecessor.HasStringProperty(ReadmittedProperty)
				|| Predecessor.HasIntProperty(ReadmittedProperty)) return false;
			r_KingdomImprovement improvement = Predecessor.GetPart<r_KingdomImprovement>();
			Zone zone = Predecessor.CurrentZone;
			Cell cell = Predecessor.CurrentCell;
			if (improvement == null || zone == null || cell == null) return false;
			KingdomSystem system = The.Game == null ? null : The.Game.RequireSystem<KingdomSystem>();
			KingdomUpgradeRules.RetiredHandoverObservation observed =
				new KingdomUpgradeRules.RetiredHandoverObservation();
			observed.Owned = KingdomConstruction.Owns(system, zone, Job);
			observed.Current = KingdomConstruction.IsCurrent(Job);
			observed.PredecessorReceipt = KingdomConstruction.HasReceipt(Predecessor, Job);
			observed.SuccessorReceipt = KingdomConstruction.HasReceipt(Successor, Job);
			observed.SuccessorPending = r_KingdomScaffold.IsExactPendingImprovementSuccessor(Successor);
			observed.SuccessorExact = r_KingdomScaffold.IsExactSuccessor(Successor, zone, cell, Job,
				improvement.SuccessorBlueprint);
			observed.Working = improvement.Working;
			observed.EffectsDone = improvement.HandoverEffectsDone;
			// Asked only once the receipt is proved, so the Ensure call can never bind one here.
			observed.PredecessorExact = observed.PredecessorReceipt
				&& EnsureExactImprovementPredecessor(system, zone, Predecessor, Job);
			observed.ScaffoldLanded = TryLandedScaffoldId(improvement, Successor, out _);
			observed.UpgradeQuarantined = KingdomArchitectureStamper.IsUpgradeQuarantined(
				Predecessor, out _);
			observed.LayoutFault = Predecessor.HasStringProperty(KingdomArchitectureStamper.FaultProperty)
				|| Predecessor.HasIntProperty(KingdomArchitectureStamper.FaultProperty);
			observed.ContentCustody = r_KingdomImprovement.VerifyHandoverContentCustody(Predecessor,
				Successor, cell, improvement, true, out _);
			observed.AlreadyReadmitted = false;
			observed.PredecessorMarks = ReadFounderMarks(Predecessor);
			observed.SuccessorMarks = ReadFounderMarks(Successor);
			observed.HasInventory = Successor.Inventory != null;
			observed.HasLiquid = Successor.GetPart<LiquidVolume>() != null;
			observed.SameWear = KingdomWear.SameStableState(Predecessor, Successor);
			observed.UpgradePhase = Predecessor.GetIntProperty(
				KingdomArchitectureStamper.UpgradePhaseProperty);
			observed.SuccessorLayoutComplete = KingdomArchitectureStamper.TryVerifyComplete(
				Successor, zone, out _);
			observed.ScaffoldReferenceLive = GameObject.Validate(improvement.Scaffold);
			KingdomRetiredHandoverDefect defect = KingdomUpgradeRules.ClassifyRetiredHandoverDefect(
				Job, Predecessor.IDIfAssigned, cell.X, cell.Y, Successor.IDIfAssigned, SuccessorKey,
				observed);
			if (defect == KingdomRetiredHandoverDefect.None) return false;

			Predecessor.SetStringProperty(ReadmittedProperty, Job.Id);
			if (Predecessor.GetStringProperty(ReadmittedProperty) != Job.Id) return false;
			improvement.HandoverQuarantined = false;
			improvement.HandoverFailure = null;
			string jobId = Job.Id;
			if (!KingdomConstruction.Readmit(ref Job, defect)) return false;
			KingdomLog.Log("improvement readmitted: job=" + jobId + " defect="
				+ KingdomConstructionRules.RetiredHandoverDefectLetter(defect) + " design="
				+ DesignKeyOf(Predecessor) + "->" + SuccessorKey + " at " + cell.X + "," + cell.Y);
			return true;
		}
	}
}
