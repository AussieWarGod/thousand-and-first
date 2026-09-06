namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRules
	{
		internal static bool TryAssociate(KingdomSubsidenceStepBook prior, KingdomResidentDepartureOperation departure,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Open(prior) || !KingdomResidentDepartureRules.Valid(departure)
				|| departure.RealmId != prior.RealmId || departure.SettlementId != prior.SettlementId
				|| departure.Phase != (int)KingdomResidentDeparturePhase.Prepared) return false;
			string departureId = departure.OperationId;
			KingdomSubsidenceStepOperation op = prior.Active;
			if (op.PendingDepartureId == departureId)
			{
				if (!op.PendingIdentity.Matches(departure)) return false;
				next = prior; return true;
			}
			if (op.Phase != KingdomSubsidenceStepPhase.Departing || op.CancelRequested
				|| prior.OptionModel != NoOption
				|| op.PendingDepartureId != "" || AlreadyCredited(op, departureId)
				|| departure.PreparedTick < op.LastActivityTick) return false;
			return With(prior, op.Copy(pendingDepartureId: departureId,
				lastActivityTick: departure.PreparedTick,
				pendingIdentity: new KingdomSubsidenceDepartureIdentity(departure.ResidentId,
					departure.BodyObjectId, departure.ZoneId, departure.PreparedTick)), out next);
		}

		/// <summary>Called only after exact removed citizenship, resident row and binding have
		/// been proved. The returned wire atomically carries both the credit and its acknowledgement.</summary>
		internal static bool TryCredit(KingdomSubsidenceStepBook prior, string departureId,
			GrowthStage reachedStage, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Open(prior) || prior.Active.PendingDepartureId != departureId
				|| !Hashed(departureId, DeparturePrefix)) return false;
			KingdomSubsidenceStepOperation op = prior.Active;
			if (op.PendingCredited)
			{
				if (reachedStage != op.ReachedStage) return false;
				next = prior; return true;
			}
			if (op.Completed >= op.Quota || !Stage(reachedStage) || reachedStage > op.ReachedStage) return false;
			int completed = op.Completed + 1;
			return With(prior, op.Copy(completed: completed, reachedStage: reachedStage,
				pendingCredited: true, phase: completed == op.Quota || op.CancelRequested
					? KingdomSubsidenceStepPhase.Settling : KingdomSubsidenceStepPhase.Departing,
				rungModel: reachedStage == op.FromStage ? NoRungs : UnplannedRungs,
				rungReportModel: reachedStage == op.FromStage
					? KingdomSubsidenceBatchRules.NoReport : KingdomSubsidenceBatchRules.PendingReport,
				creditedDepartureIds: op.CreditedDepartureIds + CreditLength + departureId), out next);
		}

		/// <summary>Requires proof that the acknowledged exact journal has retired. An empty
		/// global journal alone is not a credit receipt.</summary>
		internal static bool TryReleaseRetired(KingdomSubsidenceStepBook prior,
			string departureId, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Open(prior) || prior.Active.PendingDepartureId != departureId
				|| !prior.Active.PendingCredited) return false;
			return With(prior, prior.Active.Copy(pendingDepartureId: "", pendingCredited: false), out next);
		}

		/// <summary>Requires exact restored citizenship, resident row, binding and absent marker.
		/// Neither a false engine return nor an absent journal establishes this rollback proof.</summary>
		internal static bool TryReleaseRolledBack(KingdomSubsidenceStepBook prior,
			string departureId, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Open(prior) || !Hashed(departureId, DeparturePrefix)
				|| prior.Active.PendingDepartureId != departureId || prior.Active.PendingCredited) return false;
			KingdomSubsidenceStepOperation op = prior.Active;
			return With(prior, op.Copy(pendingDepartureId: "", phase: op.CancelRequested
				? KingdomSubsidenceStepPhase.Settling : KingdomSubsidenceStepPhase.Departing), out next);
		}

		internal static bool TryCancel(KingdomSubsidenceStepBook prior, long tick, long token,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Open(prior) || tick < prior.Active.DueTick || token < 0) return false;
			KingdomSubsidenceStepOperation op = prior.Active;
			if (op.CancelRequested)
			{
				if (tick != op.CancelTick || token != op.CancelToken) return false;
				next = prior; return true;
			}
			if (op.Completed == op.Quota || tick < op.LastActivityTick) return false;
			return With(prior, op.Copy(cancelRequested: true, cancelTick: tick, cancelToken: token,
				phase: op.PendingDepartureId == "" || op.PendingCredited
					? KingdomSubsidenceStepPhase.Settling : KingdomSubsidenceStepPhase.Departing), out next);
		}

		/// <summary>The caller first persists Settling, then writes the existing checkpoint,
		/// then retires. A cut after that clock write accepts only its exact target on recovery.</summary>
		internal static bool TryCheckpoint(KingdomSubsidenceStepBook prior, long observed,
			out long checkpoint)
		{
			checkpoint = observed;
			if (!Open(prior) || prior.Active.Phase != KingdomSubsidenceStepPhase.Settling
				|| prior.Active.PendingDepartureId != "" || !RungComplete(prior)) return false;
			KingdomSubsidenceStepOperation op = prior.Active;
			// A pending last departure can prove the whole quota after cancellation was requested.
			// That fully earned step still spends its own due tick; an option transition is separate.
			long target = op.CancelRequested && op.Completed < op.Quota ? op.CancelTick : op.DueTick;
			if (observed != op.AnchorTick && observed != target) return false;
			checkpoint = target; return true;
		}

		internal static bool TryRetire(KingdomSubsidenceStepBook prior, long checkpoint,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!TryCheckpoint(prior, checkpoint, out long target) || target != checkpoint) return false;
			if (!KingdomSubsidenceReportArchive.TryRetain(prior, prior.Active.RungReportModel,
				out KingdomSubsidenceStepBook retained)) return false;
			string batchWire = prior.BatchModel;
			if (batchWire != KingdomSubsidenceBatchRules.None
				&& (!KingdomSubsidenceBatchCodec.TryDecode(batchWire, out KingdomSubsidenceBatch batch)
					|| !KingdomSubsidenceBatchRules.TryRetire(batch, prior, target, out KingdomSubsidenceBatch advanced)
					|| !KingdomSubsidenceBatchCodec.TryEncode(advanced, out batchWire))) return false;
			next = new KingdomSubsidenceStepBook(prior.Admission, prior.RealmId,
				prior.SettlementId, prior.Sequence, null, target, prior.OptionModel, batchWire, retained.FailureModel, prior.AnnouncementModel);
			return Valid(next);
		}

		private static bool Open(KingdomSubsidenceStepBook book)
		{
			return Valid(book) && book.Active != null
				&& book.Active.Phase != KingdomSubsidenceStepPhase.Quarantined;
		}

		private static bool With(KingdomSubsidenceStepBook prior, KingdomSubsidenceStepOperation op,
			out KingdomSubsidenceStepBook next)
		{
			next = prior.With(op, prior.Sequence);
			if (Valid(next)) return true;
			next = null; return false;
		}
	}
}
