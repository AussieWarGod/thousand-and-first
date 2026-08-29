using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		/// <summary>Lands exactly this operation's carried provision in the destination settlement's
		/// dedicated larders, between the proof that the cargo sits inside the frozen destination
		/// store and the single Delivered publish. Retry-safe by measurement against durable
		/// progress: the exact marked servings are counted before anything is created, the measured
		/// delta is proved sound before it is credited, and a repeat of the hop re-observes rather
		/// than re-credits. Reads no clock, so master-off time cannot become provision here.
		/// <para>Severity is decided once, here. A shortfall of room is the one recoverable stall;
		/// every ambiguous aftermath &mdash; forged marks, a changed larder, a staple that stopped
		/// being makeable after the founder committed, or a serving the engine put somewhere we did
		/// not choose &mdash; cuts the transaction instead.</para></summary>
		private static bool TryLandCarriedFood(KingdomSystem System,
			KingdomPurposeOperationReceipt Operation, GameObject Cargo, Zone DestinationZone,
			out bool Ambiguous, out string Failure)
		{
			Ambiguous = false;
			Failure = null;
			if (System == null || Operation == null || DestinationZone == null)
				return Fail("The purpose landing has no realm authority or destination ground.",
					out Failure);
			if (!KingdomPurposePortfolioRules.TryCarriedFood(Operation.SourceKind,
				Operation.DestinationKind, out _, out int carried, out _))
				return Fail("The purpose provision row is outside its frozen carriage bounds.",
					out Failure);
			if (carried <= 0) return true;
			if (!TryPurposeLandingMark(Operation, out string receipt, out int prefilter))
				return Fail("The purpose landing has no exact canonical receipt identity.",
					out Failure);
			string blueprint = KingdomData.CropForStyle(System.Style);
			KingdomSurvey survey = KingdomSurvey.Take(DestinationZone, System);
			// The roster is taken once and never rebuilt, so no callback can drop an emptied
			// larder out of a later universal proof or inject one that was never measured.
			if (!TryPurposeLarderRoster(survey, DestinationZone, out List<GameObject> larders))
				return FaultedLanding(Cargo, receipt, 0, 0,
					"The destination's dedicated larders cannot all be proved on this ground.",
					out Ambiguous, out Failure);
			int physical = MarkedPurposeFood(larders, receipt, prefilter, blueprint,
				out int unmarked, out bool exact);
			if (!TryPurposeLandedRecord(Cargo, receipt, carried, out int recorded))
				return FaultedLanding(Cargo, receipt, 0, physical,
					"The landing cargo carries a durable record this operation cannot claim.",
					out Ambiguous, out Failure);
			// An offer that was never reconciled outranks everything below it. The witness is
			// written before the engine ever sees a serving, so it outlives the serving, the save
			// cut, and a refused quarantine publication: only an exactly reproved one-step
			// increment retires it, and until then this pass offers nothing further.
			KingdomPurposeLandingAttemptState pending =
				ReadPurposeLandingAttempt(Cargo, receipt, physical, exact);
			if (pending == KingdomPurposeLandingAttemptState.Ambiguous)
				return FaultedLanding(Cargo, receipt, 0, physical,
					"An earlier provision offer left a callback witness this pass cannot reconcile.",
					out Ambiguous, out Failure);

			// A record that outruns the surviving marks is finished work only when nothing anywhere
			// still wears the receipt. A mark that merely moved is a callback's doing, and calling
			// it consumption would publish Delivered over provision the destination never kept.
			// The reconciled attempt of an entry recovery is authorised to stand through this proof
			// and is cleared strictly below; nothing else under an owned name is permitted.
			bool reconciled = pending == KingdomPurposeLandingAttemptState.Settled;
			if (!TryPurposeCustodyStrays(larders, DestinationZone, Cargo, receipt, prefilter,
				carried, reconciled, out int strays))
				return FaultedLanding(Cargo, receipt, 0, physical,
					"The destination's loaded custody could not be proved complete.",
					out Ambiguous, out Failure);
			KingdomPurposeLandingRecordState state =
				KingdomPurposePortfolioRules.ClassifyLandingRecord(physical, recorded, strays);
			if (state != KingdomPurposeLandingRecordState.Intact
				&& state != KingdomPurposeLandingRecordState.Consumed)
				return FaultedLanding(Cargo, receipt, 0, physical,
					"A serving wearing this operation's exact receipt is outside the destination larders.",
					out Ambiguous, out Failure);
			// Conservation is against this operation's own durable record, never against a count
			// other hands can lower: eating landed provision must not buy a second helping.
			if (!KingdomPurposePortfolioRules.TryLandingOutstanding(carried, physical, recorded,
				out int outstanding, out int progress))
				return FaultedLanding(Cargo, receipt, 0, physical,
					"More provision wears this operation's exact receipt than it ever carried.",
					out Ambiguous, out Failure);
			// The pure verdict mirrors the scalar exemplar branch for branch; this caller
			// deliberately does not. Central logistics retries next pass on both Refuse and
			// Interference (KingdomCentralLogistics.02:57-60) because its stop can be re-swept; a
			// purpose hop is one durable checkpoint with no later reconciliation, so both cut here.
			if (!KingdomPurposePortfolioRules.TryRecoverCarriedFood(carried, unmarked,
					survey.FoodStored, exact, physical,
					out KingdomPurposeFoodLandingAction action)
				|| action == KingdomPurposeFoodLandingAction.Interference)
				return FaultedLanding(Cargo, receipt, 0, physical,
					"The destination larders changed under this operation's exact provision receipt.",
					out Ambiguous, out Failure);
			// Only now, past every cut, is the reconciled offer retired and the durable record
			// raised to the servings a previous pass left behind. The retirement is strict: a
			// witness a callback replaced or removed is evidence, and blessing either is the
			// escape it exists to close.
			if (reconciled && !TryClearPurposeLandingAttempt(Cargo, receipt, physical))
				return FaultedLanding(Cargo, receipt, physical, physical,
					"The pending provision witness is not the one this pass reconciled.",
					out Ambiguous, out Failure);
			RecordPurposeLanded(Cargo, receipt, carried, progress);
			// The record is read back, never assumed: a silent no-op here would let a later
			// equality against a stale figure carry the landing to its checkpoint.
			if (!TryPurposeLandedRecord(Cargo, receipt, carried, out int baseline)
				|| baseline != progress)
				return FaultedLanding(Cargo, receipt, progress, physical,
					"The durable landing record did not take this operation's proved progress.",
					out Ambiguous, out Failure);
			if (outstanding <= 0)
				return CompletePurposeLanding(Operation, survey, larders, Cargo, DestinationZone,
					receipt, prefilter, blueprint, carried, progress, out Ambiguous, out Failure);
			if (survey.FoodSpace < outstanding)
				return Fail("Dedicated larders at the destination cannot cover the exact carried provision.",
					out Failure);
			int landed = progress + AddPurposeFood(survey, larders, Cargo, receipt, prefilter,
				outstanding, blueprint, out KingdomPurposeServingAftermath aftermath);
			// Progress is raised only by a fully settled, remeasured partition, and only after the
			// ambiguous aftermaths have cut. Raising it first would let a rejected, moved, mutated
			// or obliterated callback lift the high-water before the quarantine publishes, and a
			// refused quarantine CAS would leave the retry reading that lift as consumption. Each
			// offer is witnessed on the durable cargo before the engine sees it, so every one of
			// those aftermaths, the obliterated serving included (which leaves no physical trace at
			// all), keeps this operation ambiguous across any number of refused publications,
			// and no further serving is offered until the witness reconciles exactly.
			if (aftermath == KingdomPurposeServingAftermath.Stranded)
				return FaultedLanding(Cargo, receipt, carried, landed > carried ? carried : landed,
					"A marked purpose serving did not settle inside the exact destination larder.",
					out Ambiguous, out Failure);
			// The staple was proved makeable before this operation was ever published, so a refusal
			// now is content that changed under a committed hop, not a shortage of room.
			if (aftermath == KingdomPurposeServingAftermath.Unavailable)
				return FaultedLanding(Cargo, receipt, carried, landed > carried ? carried : landed,
					"The realm's staple stopped making exact servings after this operation was committed.",
					out Ambiguous, out Failure);
			// Everything the callbacks could have moved under this landing is reproved before the
			// record rises: the larders measured, the survey's agreement with them, and the whole
			// loaded custody walked fresh rather than read from an index the callbacks never told.
			if (!TryPurposeLandedRecord(Cargo, receipt, carried, out int carriedOver)
				|| carriedOver != baseline)
				return FaultedLanding(Cargo, receipt, baseline, landed > carried ? carried : landed,
					"The durable landing record changed under the provision callbacks.",
					out Ambiguous, out Failure);
			if (!TryRevalidateLandingGround(survey, larders, DestinationZone, Cargo, receipt,
				prefilter, carried, out string ground))
				return FaultedLanding(Cargo, receipt, carried, landed > carried ? carried : landed,
					ground, out Ambiguous, out Failure);
			return CompletePurposeLanding(Operation, survey, larders, Cargo, DestinationZone,
				receipt, prefilter, blueprint, carried, landed, out Ambiguous, out Failure);
		}

		/// <summary>The one end of a successful landing. The record is written and read back, then
		/// the exact marks are retired and their retirement is reproved, all before the Delivered
		/// checkpoint is ever offered. Retiring here rather than at the later credit is the whole
		/// point: between Delivered and credit a marked, takeable serving can be carried off,
		/// nested under a resident, or leave the ground entirely, and a cleanup that ran then would
		/// miss it &mdash; the mark would outlive its operation and return as evidence of an owner
		/// nobody can name. If the checkpoint is refused, the record already stands at the whole
		/// carriage, so the absence of marks reads as a completed landing whose provision was
		/// consumed: the retry republishes and cannot mint.</summary>
		private static bool CompletePurposeLanding(KingdomPurposeOperationReceipt Operation,
			KingdomSurvey Survey, List<GameObject> Larders, GameObject Cargo, Zone DestinationZone,
			string Receipt, int Prefilter, string Blueprint, int Carried, int Landed,
			out bool Ambiguous, out string Failure)
		{
			Ambiguous = false;
			Failure = null;
			RecordPurposeLanded(Cargo, Receipt, Carried, Landed);
			if (!TryPurposeLandedRecord(Cargo, Receipt, Carried, out int written)
				|| written != Landed)
				return FaultedLanding(Cargo, Receipt, Landed, Landed,
					"The durable landing record did not take the measured landing.",
					out Ambiguous, out Failure);
			if (Landed != Carried)
				return Fail("The destination larders took only part of the exact carried provision.",
					out Failure);
			// The cargo, its canonical root, and the frozen destination store are reproved here,
			// before anything is retired. Retiring first and failing this afterwards would leave a
			// completed record, no marks, and no fault: a later repaired condition would then read
			// as a consumed completion and publish Delivered over a landing that never proved out.
			if (!PurposeLardersWithinCapacity(Larders))
				return FaultedLanding(Cargo, Receipt, Carried, Carried,
					"A measured destination larder holds more than it can hold.",
					out Ambiguous, out Failure);
			if (!PurposeLandingStillExact(Operation, Cargo, out string moved))
				return FaultedLanding(Cargo, Receipt, Carried, Carried, moved, out Ambiguous,
					out Failure);
			if (!TryRetirePurposeLandingMarks(DestinationZone, Receipt, Prefilter))
				return FaultedLanding(Cargo, Receipt, Carried, Carried,
					"The destination's loaded custody could not be walked to retire this landing.",
					out Ambiguous, out Failure);
			if (MarkedPurposeFood(Larders, Receipt, Prefilter, Blueprint, out _, out bool exact) != 0
				|| !exact
				|| !TryPurposeCustodyStrays(Larders, DestinationZone, Cargo, Receipt, Prefilter,
					Carried, false, out int left) || left != 0
				|| !SamePurposeLarderRoster(Larders, Survey))
				return FaultedLanding(Cargo, Receipt, Carried, Carried,
					"Evidence under this operation's landing fields survived its own retirement.",
					out Ambiguous, out Failure);
			return true;
		}

		/// <summary>Every ambiguous end of the landing goes through here, so the durable fault is
		/// stamped before the ambiguity is ever returned. The semantic quarantine that follows may
		/// be refused; the fault is what makes that harmless, because the next pass reads it,
		/// stays ambiguous, and offers nothing.</summary>
		private static bool FaultedLanding(GameObject Cargo, string Receipt, int Expected,
			int Observed, string Reason, out bool Ambiguous, out string Failure)
		{
			Ambiguous = true;
			return StampPurposeLandingFault(Cargo, Receipt, Expected, Observed)
				? Fail(Reason, out Failure)
				: Fail(Reason + " The durable landing fault could not be stamped.", out Failure);
		}

		/// <summary>The founder's arrival word. Written only once the landing's durable Delivered
		/// checkpoint has published, and always for the whole carried amount rather than for
		/// whatever a partial retry happened to add last. A crash may cost this cosmetic line, which
		/// is acceptable; claiming an arrival that did not durably happen is not. The Delivered
		/// publish succeeds once per operation, so this cannot repeat.</summary>
		private static void NotePurposeProvisionArrival(KingdomSystem System,
			KingdomPurposeOperationReceipt Operation)
		{
			if (System?.Ledger == null || Operation == null
				|| !KingdomPurposePortfolioRules.TryCarriedFood(Operation.SourceKind,
					Operation.DestinationKind, out _, out int carried, out _)
				|| carried <= 0) return;
			System.Ledger.Note("{{C|" + Simulation.City.KingdomCityRules.CarryNote(
				Simulation.City.KingdomStockKind.Food, carried,
				KingdomPresentation.Rich(System.KingdomDisplayName)) + "}}");
		}

		/// <summary>The exact landing discriminator the status surfaces read, so a delivery whose
		/// provision cannot be proved is reported as unknown rather than inferred. Applicable is
		/// false when the row carries no provision at all. A true return means this operation's own
		/// durable record, stamped with this operation's own canonical receipt on a cargo that
		/// still reproves its exact identity, accounts for the whole carriage; a false return with
		/// Applicable true means unverified &mdash; a legacy delivery written before the record
		/// existed, or a cargo that no longer proves itself. Read-only: it never migrates a root
		/// key, so rendering status cannot mutate game state.</summary>
		internal static bool TryPurposeProvisionLanded(KingdomPurposeOperationReceipt Operation,
			out int Landed, out bool Applicable)
		{
			Landed = 0;
			Applicable = false;
			if (Operation == null || !KingdomPurposePortfolioRules.TryCarriedFood(
					Operation.SourceKind, Operation.DestinationKind, out _, out int carried, out _)
				|| carried <= 0) return false;
			Applicable = true;
			if (!TryPurposeLandingMark(Operation, out string receipt, out _)
				|| !TryRootedPurposeCargoExact(Operation, out GameObject cargo)
				|| !TryPurposeLandedRecord(cargo, receipt, carried, out Landed)) return false;
			return KingdomPurposePortfolioRules.LandingIsProved(
				cargo.GetStringProperty(PortfolioLandedReceiptProperty) == receipt, Landed,
				carried);
		}

		/// <summary>This operation's canonical landing identity and its cheap integer index. The
		/// string is the proof; the index only makes the common case a comparison instead of a
		/// scan, and a raw zero is normalised rather than refused, because a lawful receipt may
		/// hash to zero and zero also reads as "unmarked".</summary>
		private static bool TryPurposeLandingMark(KingdomPurposeOperationReceipt Operation,
			out string Receipt, out int Prefilter)
		{
			Prefilter = 0;
			if (!KingdomPurposePortfolioRules.TryLandingReceipt(Operation.PairId,
				Operation.PairEpoch, Operation.OperationId, out Receipt)) return false;
			Prefilter = KingdomPurposePortfolioRules.LandingIndex(
				Simulation.City.KingdomCityRules.StableId(Receipt));
			return true;
		}

		/// <summary>Refuses an operation whose provision cannot land before the founder is ever
		/// asked to consent to it: both that the destination has room, and that the realm's own
		/// staple can be made into an exact serving at all. Proving the staple here is what makes a
		/// later refusal an ambiguity rather than a wait nobody can clear.</summary>
		private static bool TryPreflightCarriedFood(KingdomSystem System,
			KingdomPurposeOperationReceipt Operation, Zone DestinationZone, out string Failure)
		{
			Failure = null;
			if (!KingdomPurposePortfolioRules.TryCarriedFood(Operation.SourceKind,
				Operation.DestinationKind, out _, out int carried, out _))
				return Fail("The purpose provision row is outside its frozen carriage bounds.",
					out Failure);
			if (carried <= 0) return true;
			if (!PurposeServingIsMakeable(KingdomData.CropForStyle(System.Style)))
				return Fail("The realm's own staple cannot become an exact landed serving.",
					out Failure);
			return KingdomSurvey.Take(DestinationZone, System).FoodSpace >= carried
				|| Fail("Dedicated larders at the destination cannot cover the exact carried provision.",
					out Failure);
		}
	}
}
