using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>The cold side of the completed-heart witness. Before activation it captures the exact
	/// heart the save left standing, proves it is still unworn, proves the loaded rung wire is the
	/// saved one and carries production's own expected work set, and arms the passive release
	/// observer over the primary and, when this rung selected it, the heart companion. After recovery
	/// it proves the same final object, the companion's new attachment and released receipt, and the
	/// observed companion lane. It never removes, mends or reselects anything.</summary>
	internal static class KingdomSubsidenceRungLoadHeartWitness
	{
		private static KingdomSubsidenceRungHeart Bound;
		private static int Owed;

		/// <summary>The dated reports this loaded rung owes: two, plus one per planned work.</summary>
		internal static int Reports { get { return Owed; } }

		internal static void Arm(XRLGame Game, KingdomSystem System, Zone Ground,
			KingdomSubsidenceRungSaveSnapshot Snapshot, KingdomSubsidenceRungPlan Plan)
		{
			Check(Bound == null && Game != null && System != null && Ground != null
				&& Snapshot != null && Plan != null && Plan.Works != null,
				"the cold completed-heart witness is already bound or lacks its exact loaded state");
			KingdomSubsidenceRungHeart heart = KingdomSubsidenceRungSaveHeartProof.Bind(Ground,
				Plan.SettlementId, Plan.DueTick, Plan.From);
			KingdomSubsidenceRungSaveHeartProof.ProveFreshHeartAbsent(heart.Final,
				KingdomSubsidenceRungSaveHeartProof.ColdPreactivation);
			Check(KingdomSubsidenceRungCodec.TryEncode(Plan, out string wire)
				&& wire == Snapshot.RungWire,
				"the loaded rung plan does not re-encode to the exact saved frozen wire");
			KingdomSubsidenceRungSaveHeartProof.ProveShape(Plan, Snapshot.Work.ObjectId, heart);
			Bound = heart;
			Owed = 2 + Plan.Works.Count;
			KingdomSubsidenceRungReleaseCut.ArmObserver(Game, System, Snapshot.Work.ObjectId,
				KingdomSubsidenceRungSaveChecks.Index, Snapshot.Sequence, Snapshot.StepId,
				companionWorkObjectId: heart.Selected ? heart.Id : null);
		}

		/// <summary>After the cold recovery: the same exact heart still stands under the same exact
		/// terminal authority bytes and pose it was bound with, a selected heart carries its one new
		/// wear attachment at production's released ten-field target, an unselected heart is still
		/// exactly as unworn as it was saved, and the observed companion lane matches either three
		/// admitted writes or no companion events at all.</summary>
		internal static void Recovered(Zone Ground, string StepId)
		{
			Check(Bound != null && Ground != null && !string.IsNullOrEmpty(StepId),
				"the cold completed-heart witness was never bound before recovery");
			KingdomSubsidenceRungSaveHeartProof.ProveTerminalExact(Bound, Ground,
				KingdomSubsidenceRungSaveHeartProof.ColdRecovery);
			if (Bound.Selected)
				KingdomSubsidenceRungSaveHeartProof.ProveCompanionReleased(Bound.Final, StepId,
					Bound.AfterWear);
			else
				KingdomSubsidenceRungSaveHeartProof.ProveFreshHeartAbsent(Bound.Final,
					KingdomSubsidenceRungSaveHeartProof.ColdRecovery);
			KingdomSubsidenceRungSaveHeartProof.ProveCompanionEvidence(Bound.Selected);
		}

		/// <summary>Idempotent, and never destructive: the dated-report count this witness proved is
		/// retained for the caller that already read it, and so is every counter the release cut
		/// recorded. Only the live cold binding is dropped, so nothing holds the heart past its own
		/// recovery. Never throws, so it is safe in the same guarded finally as the cut's own Disarm.</summary>
		internal static void Disarm()
		{
			Bound = null;
		}

		private static void Check(bool Condition, string Failure)
		{
			KingdomScenarioSaveFiles.Require(Condition, Failure ?? "native rung heart witness refused");
		}
	}
}
