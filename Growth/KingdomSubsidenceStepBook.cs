using System;

namespace ThousandAndFirst
{
	internal enum KingdomSubsidenceAdmission : byte { Fresh, Legacy, Admitted }
	internal enum KingdomSubsidenceStepPhase : byte { Departing, Settling, Quarantined }

	internal sealed class KingdomSubsidenceStepBook
	{
		internal readonly KingdomSubsidenceAdmission Admission;
		internal readonly string RealmId, SettlementId;
		internal readonly long Sequence;
		// Anti-replay receipt of the last retired step, not a second elapsed-time clock.
		internal readonly long LastRetiredTick;
		internal readonly KingdomSubsidenceStepOperation Active;
		internal readonly string OptionModel;
		internal readonly string BatchModel;
		internal readonly string FailureModel;
		internal readonly string AnnouncementModel;

		internal KingdomSubsidenceStepBook(KingdomSubsidenceAdmission admission,
			string realmId, string settlementId, long sequence,
			KingdomSubsidenceStepOperation active, long lastRetiredTick = 0,
			string optionModel = KingdomSubsidenceStepRules.NoOption,
			string batchModel = KingdomSubsidenceBatchRules.None,
			string failureModel = KingdomSubsidenceReportArchive.None,
			string announcementModel = KingdomSubsidenceAnnouncementCodec.None)
		{
			Admission = admission; RealmId = realmId; SettlementId = settlementId;
			Sequence = sequence; Active = active; LastRetiredTick = lastRetiredTick;
			OptionModel = optionModel;
			BatchModel = batchModel;
			FailureModel = failureModel;
			AnnouncementModel = announcementModel;
		}

		internal KingdomSubsidenceStepBook With(KingdomSubsidenceStepOperation active,
			long sequence)
		{
			return new KingdomSubsidenceStepBook(Admission, RealmId, SettlementId, sequence,
				active, LastRetiredTick, OptionModel, BatchModel, FailureModel, AnnouncementModel);
		}

		internal KingdomSubsidenceStepBook WithOption(string model)
		{
			return new KingdomSubsidenceStepBook(Admission, RealmId, SettlementId, Sequence,
				Active, LastRetiredTick, model, BatchModel, FailureModel, AnnouncementModel);
		}

		internal KingdomSubsidenceStepBook WithBatch(string model)
		{
			return new KingdomSubsidenceStepBook(Admission, RealmId, SettlementId, Sequence,
				Active, LastRetiredTick, OptionModel, model, FailureModel, AnnouncementModel);
		}

		internal KingdomSubsidenceStepBook WithFailures(string model)
		{
			return new KingdomSubsidenceStepBook(Admission, RealmId, SettlementId, Sequence,
				Active, LastRetiredTick, OptionModel, BatchModel, model, AnnouncementModel);
		}

		internal KingdomSubsidenceStepBook WithAnnouncement(string model)
		{
			return new KingdomSubsidenceStepBook(Admission, RealmId, SettlementId, Sequence,
				Active, LastRetiredTick, OptionModel, BatchModel, FailureModel, model);
		}
	}

	internal sealed class KingdomSubsidenceStepOperation
	{
		internal readonly string Id;
		internal readonly long AnchorTick, DueTick;
		internal readonly long LastActivityTick;
		internal readonly GrowthStage FromStage, ReachedStage;
		internal readonly int Quota, Completed;
		internal readonly int StorageCapacity;
		internal readonly string BindingSupport;
		// Canonical per-ID length frames, at most the step's five exact departure credits.
		internal readonly string CreditedDepartureIds;
		internal readonly KingdomSubsidenceStepPhase Phase;
		internal readonly string PendingDepartureId;
		internal readonly KingdomSubsidenceDepartureIdentity PendingIdentity;
		internal readonly bool PendingCredited, CancelRequested;
		internal readonly long CancelTick, CancelToken;
		internal readonly string RungModel, Fault;
		internal readonly string RungReportModel;

		internal KingdomSubsidenceStepOperation(string id, long anchorTick, long dueTick,
			GrowthStage fromStage, GrowthStage reachedStage, int quota, int completed,
			KingdomSubsidenceStepPhase phase, string pendingDepartureId, bool pendingCredited,
			bool cancelRequested, long cancelTick, long cancelToken, string rungModel, string fault,
			int storageCapacity, string bindingSupport, long lastActivityTick, string creditedDepartureIds = "",
			KingdomSubsidenceDepartureIdentity pendingIdentity = null, string rungReportModel = null)
		{
			Id = id; AnchorTick = anchorTick; DueTick = dueTick;
			FromStage = fromStage; ReachedStage = reachedStage;
			Quota = quota; Completed = completed; Phase = phase;
			PendingDepartureId = pendingDepartureId; PendingCredited = pendingCredited;
			CancelRequested = cancelRequested; CancelTick = cancelTick; CancelToken = cancelToken;
			RungModel = rungModel; Fault = fault;
			CreditedDepartureIds = creditedDepartureIds;
			PendingIdentity = pendingIdentity; StorageCapacity = storageCapacity;
			BindingSupport = bindingSupport;
			LastActivityTick = lastActivityTick;
			RungReportModel = rungReportModel ?? (reachedStage == fromStage
				? KingdomSubsidenceBatchRules.NoReport : KingdomSubsidenceBatchRules.PendingReport);
		}

		internal KingdomSubsidenceStepOperation Copy(int? completed = null,
			GrowthStage? reachedStage = null, KingdomSubsidenceStepPhase? phase = null,
			string pendingDepartureId = null, bool? pendingCredited = null,
			bool? cancelRequested = null, long? cancelTick = null, long? cancelToken = null,
			string rungModel = null, string fault = null, string creditedDepartureIds = null,
			KingdomSubsidenceDepartureIdentity pendingIdentity = null, long? lastActivityTick = null,
			string rungReportModel = null)
		{
			return new KingdomSubsidenceStepOperation(Id, AnchorTick, DueTick, FromStage,
				reachedStage ?? ReachedStage, Quota, completed ?? Completed, phase ?? Phase,
				pendingDepartureId ?? PendingDepartureId, pendingCredited ?? PendingCredited,
				cancelRequested ?? CancelRequested, cancelTick ?? CancelTick, cancelToken ?? CancelToken,
				rungModel ?? RungModel, fault ?? Fault, StorageCapacity, BindingSupport,
				lastActivityTick ?? LastActivityTick,
				creditedDepartureIds ?? CreditedDepartureIds,
				pendingDepartureId == "" ? null : pendingIdentity ?? PendingIdentity,
				rungReportModel ?? RungReportModel);
		}
	}
}
