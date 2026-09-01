using System;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomExpeditions
	{
		/// <summary>
		/// Applies only the deed disposition frozen in the final job receipt. Attention, current
		/// naming, and capacity are never consulted here: those mutable facts were sampled once before
		/// the outbox receipt, so retry can commit or wait but cannot change the historical answer.
		/// </summary>
		private static bool TryRecordExpeditionDeed(KingdomSystem System, KingdomJobRow Row,
			KingdomExpeditionOutcome Resolution, string ChronicleRef, out string Failure)
		{
			Failure = null;
			if (Resolution != KingdomExpeditionOutcome.RichFind)
				return true;
			if (!KingdomJobRules.ValidExpeditionResultReceipt(Row))
				return Refuse("The rich find lacks an exact frozen deed disposition.", out Failure);
			if (Row.ExpeditionDeedDisposition == KingdomExpeditionDeedDisposition.NotApplicable)
				return true;
			if (System?.PolityLedger == null)
				return Refuse("The rich find's frozen polity outbox is unavailable.", out Failure);
			KingdomPolityLedger ledger = System.PolityLedger;
			if (!KingdomPolityRules.TryValidate(ledger, out Failure))
				return Refuse("The expedition is told, but polity memory is quarantined: " + Failure,
					out Failure);
			KingdomPolityRecord polity = null;
			for (int i = 0; i < ledger.Polities.Count; i++)
				if (ledger.Polities[i].PolityId == Row.ExpeditionDeedPolityId)
				{
					if (polity != null) return Refuse("The deed polity identity is duplicated.", out Failure);
					polity = ledger.Polities[i];
				}
			if (polity == null) return Refuse("The deed's frozen polity is absent.", out Failure);
			string settlementId = System.SettlementIdForOwnedZone(Row.SourceZoneId);
			if (string.IsNullOrEmpty(settlementId) ||
				!KingdomPolityExpeditionDeedRules.TryFigureRef(polity.PolityId, settlementId,
					Row.JobId, Row.SubjectId, ChronicleRef, out string causeRef, out string figureRef)
				|| causeRef != Row.ExpeditionDeedCauseRef
				|| figureRef != Row.ExpeditionDeedFigureRef)
				return Refuse("The rich find cannot mint a bounded deed fact.", out Failure);
			if (!TryFindExistingDeedReceipt(ledger, polity.PolityId, settlementId, Row,
				ChronicleRef, causeRef, figureRef, out bool exactRetry, out Failure)) return false;
			if (Row.ExpeditionDeedDisposition == KingdomExpeditionDeedDisposition.Skip)
				return exactRetry
					? Refuse("A skipped deed conflicts with an extant promotion receipt.", out Failure)
					: true;
			if (Row.ExpeditionDeedDisposition != KingdomExpeditionDeedDisposition.Promote)
				return Refuse("The rich find carries an unknown deed disposition.", out Failure);
			if (exactRetry) return true;

			KingdomPolityFigurePromotionFacts facts = new KingdomPolityFigurePromotionFacts
			{
				PolityId = polity.PolityId, SettlementId = settlementId,
				ResidentId = Row.SubjectId, DisplayName = Row.SubjectName, RoleKey = "salvager",
				Origin = KingdomPolityFigureOrigin.PromotedByDeed,
				CauseRef = causeRef, ChronicleRef = ChronicleRef,
				DeedSummary = KingdomPolityExpeditionDeedRules.Summary
			};
			if (!KingdomPolityRules.TryPromoteNamedFigure(ledger, ledger.Revision, facts,
				out KingdomPolityPublicationResult _, out Failure))
				return Refuse("The rich find is told, but its named deed receipt cannot yet commit: " +
					Failure, out Failure);
			return TryConcludeDeadPromotedResident(System, ledger, settlementId, Row, out Failure);
		}

		private static bool TryPlanExpeditionDeed(KingdomSystem System, KingdomJobRow Row,
			KingdomExpeditionOutcome Resolution, string ChronicleRef,
			out KingdomExpeditionDeedDisposition Disposition, out string PolityId,
			out string CauseRef, out string FigureRef, out string Failure)
		{
			Disposition = KingdomExpeditionDeedDisposition.NotApplicable;
			PolityId = null; CauseRef = null; FigureRef = null; Failure = null;
			if (Resolution != KingdomExpeditionOutcome.RichFind || System?.PolityLedger == null)
				return true;
			KingdomPolityLedger ledger = System.PolityLedger;
			if (!KingdomPolityRules.TryValidate(ledger, out Failure)) return false;
			KingdomPolityRecord polity = CurrentActivePolity(ledger);
			if (polity == null) return true;
			string settlementId = System.SettlementIdForOwnedZone(Row.SourceZoneId);
			if (string.IsNullOrEmpty(settlementId)
				|| !KingdomPolityExpeditionDeedRules.TryFigureRef(polity.PolityId,
					settlementId, Row.JobId, Row.SubjectId, ChronicleRef,
					out CauseRef, out FigureRef)) return false;
			PolityId = polity.PolityId;
			if (!TryFindExistingDeedReceipt(ledger, PolityId, settlementId, Row,
				ChronicleRef, CauseRef, FigureRef, out bool exact, out Failure)) return false;
			Disposition = exact || !ResidentAlreadyNamed(ledger, settlementId, Row.SubjectId)
				&& ledger.NamedFigures.Count < KingdomPolityRules.MaxNamedFigures
				&& KingdomPolityAttentionRules.ActiveNamedFigures(ledger, PolityId) <
					KingdomPolityAttentionRules.MaximumActiveNamedFigures
				? KingdomExpeditionDeedDisposition.Promote
				: KingdomExpeditionDeedDisposition.Skip;
			return true;
		}

		private static bool TryConcludeDeadPromotedResident(KingdomSystem System,
			KingdomPolityLedger Ledger, string SettlementId, KingdomJobRow Row,
			out string Failure)
		{
			Failure = null;
			if (!TryReadExactDeedResident(System, SettlementId, Row.SubjectId,
					out KingdomResidentRow resident)
				|| resident.Standing != KingdomResidentStanding.Dead) return true;
			return KingdomPolityRules.TryConcludeDeedResident(Ledger, Ledger.Revision,
				SettlementId, Row.SubjectId, Row.SubjectName, KingdomPolityFigurePhase.Dead,
				out KingdomPolityNamedFigureRecord _, out string _, out Failure);
		}

		private static bool TryReadExactDeedResident(KingdomSystem System, string SettlementId,
			int ResidentId, out KingdomResidentRow Resident)
		{
			Resident = default(KingdomResidentRow);
			int matches = 0;
			bool correctBook = false;
			System.Collections.Generic.List<KingdomCityBook> books = Books(System);
			for (int i = 0; i < books.Count; i++)
			{
				KingdomCityBook book = books[i];
				KingdomCityState state;
				KingdomCityFault fault;
				if (book == null || !book.TryRead(out state, out fault)) return false;
				if (!state.TryResidentIndex(ResidentId, out int index)) continue;
				matches++;
				if (!state.TryResident(index, out KingdomResidentRow row)) return false;
				if (string.Equals(book.SettlementId, SettlementId, StringComparison.Ordinal))
				{
					Resident = row;
					correctBook = true;
				}
			}
			return matches == 1 && correctBook;
		}

		private static KingdomPolityRecord CurrentActivePolity(KingdomPolityLedger Ledger)
		{
			KingdomPolityRecord result = null;
			for (int i = 0; i < Ledger.Polities.Count; i++)
			{
				KingdomPolityRecord row = Ledger.Polities[i];
				if (row.Source != KingdomPolitySource.CurrentRealm ||
					row.Lifecycle != KingdomPolityLifecycle.Active) continue;
				if (result != null) return null;
				result = row;
			}
			return result;
		}

		private static bool ResidentAlreadyNamed(KingdomPolityLedger Ledger,
			string SettlementId, int ResidentId)
		{
			for (int i = 0; i < Ledger.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord row = Ledger.NamedFigures[i];
				if (row.Phase == KingdomPolityFigurePhase.Active && row.ResidentId == ResidentId &&
					row.ResidentSettlementId == SettlementId) return true;
			}
			return false;
		}

		private static bool TryFindExistingDeedReceipt(KingdomPolityLedger Ledger,
			string PolityId, string SettlementId, KingdomJobRow Row, string ChronicleRef,
			string CauseRef, string FigureRef, out bool Exact, out string Failure)
		{
			Exact = false; Failure = null; int candidates = 0;
			for (int i = 0; i < Ledger.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord row = Ledger.NamedFigures[i];
				if (row.FigureId != FigureRef && row.CauseRef != CauseRef) continue;
				candidates++;
				if (!KingdomPolityExpeditionDeedRules.ExactReceipt(row, PolityId, SettlementId,
					Row.JobId, Row.SubjectId, Row.SubjectName, ChronicleRef))
					return Refuse("The rich find conflicts with a divergent deed receipt.", out Failure);
			}
			if (candidates > 1)
				return Refuse("The rich find has duplicate deed authority.", out Failure);
			Exact = candidates == 1;
			return true;
		}
	}
}
