using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		/// <summary>CAS-binds one exact groomed resident to one active successor figure.</summary>
		public static bool TryEnsureResidentSuccessor(KingdomPolityLedger Ledger,
			long ExpectedRevision, string SettlementId, int ResidentId, string DisplayName,
			int NominationRevision, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) ||
				!TypedId(SettlementId, "taf:settlement:v1:") || ResidentId < 1 ||
				!Text(DisplayName, true) || NominationRevision < 0 || Tick < 0L)
				return Refuse(Result, Failure ?? "resident successor input is invalid", out Failure);
			KingdomPolityRecord current = CurrentPolity(Ledger);
			if (current == null) return Refuse(Result, "current polity is missing", out Failure);
			string figureId = ActivationId("taf:figure:successor:v1:",
				"resident-successor-id-v1", current.PolityId, SettlementId,
				ResidentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
				NominationRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
			string causeRef = ActivationId("taf:fact:successor:v1:",
				"resident-successor-fact-v1", current.PolityId, SettlementId,
				ResidentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
				NominationRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));
			Result.CurrentPolityId = current.PolityId;
			KingdomPolityNamedFigureRecord existing = FindFigure(Ledger, figureId);
			if (existing != null && ExactResidentSuccessor(existing, current.PolityId,
				SettlementId, ResidentId, DisplayName, causeRef))
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (existing != null)
				return Refuse(Result, "successor figure id carries conflicting evidence", out Failure);
			if (Ledger.Revision != ExpectedRevision) return Conflict(Result, out Failure);
			if (Ledger.NamedFigures.Count >= MaxNamedFigures)
				return Refuse(Result, "named figure capacity is exhausted", out Failure);
			if (ActiveSuccessorCount(Ledger, current.PolityId) > 1)
				return Refuse(Result, "current polity has conflicting active successors", out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			for (int i = 0; i < candidate.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord old = candidate.NamedFigures[i];
				if (old.PolityId == current.PolityId &&
					old.Origin == KingdomPolityFigureOrigin.Successor &&
					old.Phase == KingdomPolityFigurePhase.Active)
				{
					old.Phase = KingdomPolityFigurePhase.Transferred;
					old.ConclusionRef = ActivationId("taf:conclusion:office:v1:",
						"successor-office-transfer-v1", old.FigureId, figureId);
					old.ResidentId = 0; old.ResidentSettlementId = null;
				}
			}
			candidate.NamedFigures.Add(new KingdomPolityNamedFigureRecord
			{
				FigureId = figureId, PolityId = current.PolityId, DisplayName = DisplayName,
				RoleKey = "successor", Origin = KingdomPolityFigureOrigin.Successor,
				Phase = KingdomPolityFigurePhase.Active,
				CauseRef = causeRef,
				ResidentId = ResidentId, ResidentSettlementId = SettlementId
			});
			CanonicalSort(candidate);
			if (!Increment(candidate, out Failure) || !TryValidate(candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			Commit(Ledger, candidate, Result); return true;
		}

		public static bool TryRetireResidentSuccessor(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CauseRef, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = BeginResult(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) || !SemanticId(CauseRef) || Tick < 0L)
				return Refuse(Result, Failure ?? "successor retirement input is invalid", out Failure);
			KingdomPolityRecord current = CurrentPolity(Ledger);
			if (current == null) return Refuse(Result, "current polity is missing", out Failure);
			int active = ActiveSuccessorCount(Ledger, current.PolityId);
			if (active > 1)
				return Refuse(Result, "current polity has conflicting active successors", out Failure);
			if (active == 0)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Ledger.Revision != ExpectedRevision) return Conflict(Result, out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			for (int i = 0; i < candidate.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord figure = candidate.NamedFigures[i];
				if (figure.PolityId != current.PolityId || figure.RoleKey != "successor" ||
					figure.Phase != KingdomPolityFigurePhase.Active) continue;
				figure.Phase = KingdomPolityFigurePhase.Transferred;
				figure.ConclusionRef = ActivationId("taf:conclusion:office:v1:",
					"successor-office-retirement-v1", figure.FigureId, CauseRef);
				figure.ResidentId = 0; figure.ResidentSettlementId = null;
			}
			if (!Increment(candidate, out Failure) || !TryValidate(candidate, out Failure))
				return Refuse(Result, Failure, out Failure);
			Commit(Ledger, candidate, Result); return true;
		}

		private static KingdomPolityRecord CurrentPolity(KingdomPolityLedger L)
		{
			KingdomPolityRecord result = null;
			for (int i = 0; i < L.Polities.Count; i++)
				if (L.Polities[i].Source == KingdomPolitySource.CurrentRealm)
				{
					if (result != null) return null; result = L.Polities[i];
				}
			return result;
		}

		private static KingdomPolityNamedFigureRecord FindFigure(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.NamedFigures.Count; i++)
				if (L.NamedFigures[i].FigureId == Id) return L.NamedFigures[i];
			return null;
		}

		private static bool ExactResidentSuccessor(KingdomPolityNamedFigureRecord F,
			string PolityId, string SettlementId, int ResidentId, string DisplayName,
			string CauseRef)
		{
			return F.PolityId == PolityId && F.DisplayName == DisplayName &&
				F.RoleKey == "successor" && F.Origin == KingdomPolityFigureOrigin.Successor &&
				F.Phase == KingdomPolityFigurePhase.Active && F.ResidentId == ResidentId &&
				F.ResidentSettlementId == SettlementId && F.CauseRef == CauseRef &&
				string.IsNullOrEmpty(F.ConclusionRef);
		}

		private static int ActiveSuccessorCount(KingdomPolityLedger L, string PolityId)
		{
			int result = 0;
			for (int i = 0; i < L.NamedFigures.Count; i++)
				if (L.NamedFigures[i].PolityId == PolityId &&
					L.NamedFigures[i].RoleKey == "successor" &&
					L.NamedFigures[i].Phase == KingdomPolityFigurePhase.Active) result++;
			return result;
		}
	}
}
