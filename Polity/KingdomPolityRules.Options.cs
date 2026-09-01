namespace ThousandAndFirst
{
	/// <summary>Immutable, validated polity half of one realm master-resume publication.</summary>
	public sealed class KingdomPolityMasterResumePlan
	{
		internal readonly byte[] SourceLedgerEnvelope;
		internal readonly KingdomPolityDispatchState SourceDispatch;
		internal readonly KingdomPolityLedger TargetLedger;
		internal readonly KingdomPolityDispatchState TargetDispatch;
		public readonly long ResumeTick;

		internal KingdomPolityMasterResumePlan(byte[] sourceLedger,
			KingdomPolityDispatchState sourceDispatch, KingdomPolityLedger targetLedger,
			KingdomPolityDispatchState targetDispatch, long resumeTick)
		{
			SourceLedgerEnvelope = sourceLedger; SourceDispatch = sourceDispatch;
			TargetLedger = targetLedger; TargetDispatch = targetDispatch; ResumeTick = resumeTick;
		}
	}

	public static partial class KingdomPolityRules
	{
		/// <summary>Observes running-save presentation policy. Semantic routes, grievances, and
		/// conclusions stay intact; only future optional projection causes are admitted.</summary>
		public static bool TryObservePresentation(KingdomPolityLedger Ledger,
			KingdomPolityPresentationState Desired, long Tick, out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (Desired != KingdomPolityPresentationState.Enabled &&
				Desired != KingdomPolityPresentationState.Disabled)
				return Fail("presentation observation must be enabled or disabled", out Failure);
			if (Tick < 0L || Tick < Ledger.Options.ObservedTick)
				return Fail("presentation observation regresses time", out Failure);
			if (Ledger.Options.Presentation == Desired) return true;
			if (Ledger.Revision == long.MaxValue)
				return Fail("polity revision is exhausted", out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			if (Desired == KingdomPolityPresentationState.Enabled)
			{
				if (candidate.Options.EnableEpoch == long.MaxValue)
					return Fail("presentation enable epoch is exhausted", out Failure);
				candidate.Options.EnableEpoch++;
				candidate.Options.FutureCauseFloorTick = Tick;
			}
			else candidate.Options.FutureCauseFloorTick = long.MaxValue;
			candidate.Options.Presentation = Desired;
			candidate.Options.ObservedTick = Tick; candidate.Revision++;
			if (!TryValidate(candidate, out Failure)) return false;
			Ledger.CopyFrom(candidate); return true;
		}

		public static bool CanEmitOptionalProjection(KingdomPolityLedger Ledger, long CauseTick)
		{
			return TryValidate(Ledger, out string _) && CauseTick >= 0L &&
				Ledger.Options.Presentation == KingdomPolityPresentationState.Enabled &&
				CauseTick >= Ledger.Options.FutureCauseFloorTick;
		}

		/// <summary>Stages one no-backlog master resume without rewriting any cohort proof or
		/// replacing raw dispatch authority.</summary>
		public static bool TryPrepareMasterResume(KingdomPolityLedger Ledger,
			KingdomPolityDispatchState Dispatch, long ExpectedRevision,
			KingdomPolityPresentationState Desired, long Tick,
			out KingdomPolityMasterResumePlan Plan, out string Failure)
		{
			Plan = null; Failure = null;
			if (!TryValidate(Ledger, out Failure)) return false;
			if (Dispatch == null || ExpectedRevision != Ledger.Revision || Tick < 0L ||
				Tick < Ledger.Options.ObservedTick ||
				(Desired != KingdomPolityPresentationState.Enabled &&
				 Desired != KingdomPolityPresentationState.Disabled))
				return Fail("polity master-resume staging context is invalid", out Failure);
			if (!KingdomPolityDispatchRules.ValidState(Dispatch, out Failure))
				return Fail(Failure ?? "polity master-resume dispatch is invalid", out Failure);
			if (Dispatch.RealmId != null && Dispatch.RealmId != Ledger.RealmId)
				return Fail("polity master-resume dispatch belongs to another realm", out Failure);
			if (Ledger.Revision == long.MaxValue || (Desired ==
				KingdomPolityPresentationState.Enabled &&
				Ledger.Options.EnableEpoch == long.MaxValue))
				return Fail("polity master-resume authority is exhausted", out Failure);
			if (Dispatch.Revision == long.MaxValue)
				return Fail("polity dispatch master-resume revision is exhausted", out Failure);

			byte[] source = KingdomPolityCodec.EncodeEnvelope(Ledger);
			KingdomPolityLedger target = Clone(Ledger); target.Revision++;
			target.Options.Presentation = Desired; target.Options.ObservedTick = Tick;
			if (Desired == KingdomPolityPresentationState.Enabled)
			{
				target.Options.EnableEpoch++;
				target.Options.FutureCauseFloorTick = Tick;
			}
			else target.Options.FutureCauseFloorTick = long.MaxValue;
			if (!TryValidate(target, out Failure)) return false;

			KingdomPolityDispatchState priorDispatch = CloneDispatch(Dispatch);
			KingdomPolityDispatchState targetDispatch = CloneDispatch(Dispatch);
			if (!KingdomPolityDispatchRules.SuppressOpenIntents(targetDispatch, out Failure))
				return false;
			targetDispatch.RealmId = Ledger.RealmId;
			targetDispatch.FutureCauseFloorTick = Tick;
			targetDispatch.Revision++;
			KingdomPolityDispatchRules.SortRecords(targetDispatch.DirectRecords);
			if (!KingdomPolityDispatchRules.ValidState(targetDispatch, out Failure)) return false;
			Plan = new KingdomPolityMasterResumePlan(source, priorDispatch, target,
				targetDispatch, Tick); return true;
		}

		/// <summary>Publishes both staged polity receipts after exact source-state comparison.</summary>
		public static bool TryPublishMasterResume(KingdomPolityLedger Ledger,
			KingdomPolityDispatchState Dispatch, KingdomPolityMasterResumePlan Plan,
			out string Failure)
		{
			Failure = null;
			if (Ledger == null || Dispatch == null || Plan == null ||
				!TryValidate(Ledger, out Failure) ||
				!KingdomPolityDispatchRules.ValidState(Dispatch, out Failure) ||
				!TryValidate(Plan.TargetLedger, out Failure) ||
				!KingdomPolityDispatchRules.ValidState(Plan.TargetDispatch, out Failure))
				return false;
			byte[] current = KingdomPolityCodec.EncodeEnvelope(Ledger);
			if (SameBytes(current, KingdomPolityCodec.EncodeEnvelope(Plan.TargetLedger)) &&
				SameDispatch(Dispatch, Plan.TargetDispatch)) return true;
			if (!CanPublishMasterResume(Ledger, Dispatch, Plan, out Failure)) return false;
			PublishMasterResumePrevalidated(Ledger, Dispatch, Plan); return true;
		}

		/// <summary>Allocation-bounded cross-owner preflight over exact valid source authority.</summary>
		internal static bool CanPublishMasterResume(KingdomPolityLedger Ledger,
			KingdomPolityDispatchState Dispatch, KingdomPolityMasterResumePlan Plan,
			out string Failure)
		{
			Failure = null;
			if (Ledger == null || Dispatch == null || Plan == null ||
				!TryValidate(Ledger, out Failure) ||
				!KingdomPolityDispatchRules.ValidState(Dispatch, out Failure) ||
				!TryValidate(Plan.TargetLedger, out Failure) ||
				!KingdomPolityDispatchRules.ValidState(Plan.TargetDispatch, out Failure))
				return false;
			byte[] current = KingdomPolityCodec.EncodeEnvelope(Ledger);
			return SameBytes(current, Plan.SourceLedgerEnvelope) &&
				SameDispatch(Dispatch, Plan.SourceDispatch) || Fail(
					"polity master-resume publication lost its staged CAS", out Failure);
		}

		/// <summary>Copy-only half used after every realm-level publisher passed preflight.</summary>
		internal static void PublishMasterResumePrevalidated(KingdomPolityLedger Ledger,
			KingdomPolityDispatchState Dispatch, KingdomPolityMasterResumePlan Plan)
		{
			Ledger.CopyFrom(Plan.TargetLedger);
			CopyDispatch(Dispatch, Plan.TargetDispatch);
		}

		private static KingdomPolityDispatchState CloneDispatch(KingdomPolityDispatchState S)
		{
			KingdomPolityDispatchState copy = new KingdomPolityDispatchState();
			CopyDispatch(copy, S); return copy;
		}

		private static void CopyDispatch(KingdomPolityDispatchState D,
			KingdomPolityDispatchState S)
		{
			D.Version = S.Version; D.RealmId = S.RealmId; D.Revision = S.Revision;
			D.HasWindow = S.HasWindow; D.LastWindowOrdinal = S.LastWindowOrdinal;
			D.WindowCauseTick = S.WindowCauseTick;
			D.FutureCauseFloorTick = S.FutureCauseFloorTick;
			D.EndpointDigest = S.EndpointDigest;
			D.EndpointCount = S.EndpointCount; D.CompletedMask = S.CompletedMask;
			D.DirectRecords = new System.Collections.Generic.List<KingdomPolityDirectRecord>();
			for (int i = 0; i < (S.DirectRecords?.Count ?? 0); i++)
				D.DirectRecords.Add(S.DirectRecords[i].Copy());
			D.Fault = S.Fault;
		}

		private static bool SameDispatch(KingdomPolityDispatchState A,
			KingdomPolityDispatchState B)
		{
			return A != null && B != null && A.Version == B.Version && A.RealmId == B.RealmId &&
				A.Revision == B.Revision && A.HasWindow == B.HasWindow &&
				A.LastWindowOrdinal == B.LastWindowOrdinal &&
				A.WindowCauseTick == B.WindowCauseTick &&
				A.FutureCauseFloorTick == B.FutureCauseFloorTick &&
				A.EndpointDigest == B.EndpointDigest &&
				A.EndpointCount == B.EndpointCount && A.CompletedMask == B.CompletedMask &&
				SameDirectRecords(A.DirectRecords, B.DirectRecords) && A.Fault == B.Fault;
		}

		private static bool SameDirectRecords(
			System.Collections.Generic.IList<KingdomPolityDirectRecord> A,
			System.Collections.Generic.IList<KingdomPolityDirectRecord> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++)
				if (A[i].RecordId != B[i].RecordId || A[i].SourceRef != B[i].SourceRef ||
					A[i].SettlementId != B[i].SettlementId || A[i].Purpose != B[i].Purpose ||
					A[i].WindowOrdinal != B[i].WindowOrdinal || A[i].CauseTick != B[i].CauseTick ||
					A[i].EndpointVerb != B[i].EndpointVerb ||
					A[i].AcknowledgedTick != B[i].AcknowledgedTick ||
					!KingdomPolityAmbientTransactionRules.Same(
						A[i].AmbientTransaction, B[i].AmbientTransaction)) return false;
			return true;
		}

		private static bool SameBytes(byte[] A, byte[] B)
		{
			if (A == null || B == null || A.Length != B.Length) return false;
			for (int i = 0; i < A.Length; i++) if (A[i] != B[i]) return false;
			return true;
		}
	}
}
