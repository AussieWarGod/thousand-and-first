using System;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		/// <summary>Projects only deed-promoted resident bridges. It never concludes or rewrites
		/// Polity history; the owning Polity workflow must retire the bridge first.</summary>
		internal static bool TryProjectResidentTransitionClaim(KingdomPolityLedger Ledger,
			string RealmId, string SettlementId, int ResidentId, string ResidentName,
			out bool ClaimsResident)
		{
			ClaimsResident = false;
			if (Ledger == null) return true;
			if (string.IsNullOrEmpty(RealmId) || string.IsNullOrEmpty(SettlementId)
				|| ResidentId <= 0 || string.IsNullOrEmpty(ResidentName)
				|| !string.Equals(Ledger.RealmId, RealmId, StringComparison.Ordinal)
				|| !TryValidate(Ledger, out string _)) return false;
			int bridges = 0;
			for (int i = 0; i < Ledger.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord row = Ledger.NamedFigures[i];
				if (row == null || row.ResidentId != ResidentId) continue;
				bridges++;
				if (row.Phase != KingdomPolityFigurePhase.Active
					|| !string.Equals(row.ResidentSettlementId, SettlementId,
						StringComparison.Ordinal)
					|| !string.Equals(row.DisplayName, ResidentName,
						StringComparison.Ordinal)) return false;
				if (row.Origin == KingdomPolityFigureOrigin.PromotedByDeed)
					ClaimsResident = true;
			}
			return bridges <= 1;
		}

		internal static bool TryCaptureDeedResident(KingdomPolityLedger Ledger,
			string RealmId, string SettlementId, int ResidentId, string ResidentName,
			out KingdomPolityNamedFigureRecord Prior, out string ConclusionRef)
		{
			Prior = null; ConclusionRef = null;
			if (!TryProjectResidentTransitionClaim(Ledger, RealmId, SettlementId,
				ResidentId, ResidentName, out bool claims)) return false;
			if (!claims) return true;
			for (int i = 0; i < Ledger.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord row = Ledger.NamedFigures[i];
				if (row?.Phase == KingdomPolityFigurePhase.Active
					&& row.Origin == KingdomPolityFigureOrigin.PromotedByDeed
					&& row.ResidentId == ResidentId)
				{
					Prior = CopyResidentFigure(row); break;
				}
			}
			if (Prior == null) return false;
			ConclusionRef = ResidentTransitionConclusionRef(Ledger.RealmId, SettlementId,
				ResidentId, KingdomPolityFigurePhase.Departed);
			return true;
		}

		internal static bool TryConcludeDeedResident(KingdomPolityLedger Ledger,
			long ExpectedRevision, string SettlementId, int ResidentId, string ResidentName,
			KingdomPolityFigurePhase TerminalPhase,
			out KingdomPolityNamedFigureRecord Prior, out string ConclusionRef,
			out string Failure)
		{
			Prior = null; ConclusionRef = null; Failure = null;
			if (!TryValidate(Ledger, out Failure) || ResidentId <= 0
				|| string.IsNullOrEmpty(SettlementId) || string.IsNullOrEmpty(ResidentName)
				|| TerminalPhase != KingdomPolityFigurePhase.Departed
					&& TerminalPhase != KingdomPolityFigurePhase.Dead
					&& TerminalPhase != KingdomPolityFigurePhase.Transferred)
				return Fail(Failure ?? "deed-figure transition input is invalid", out Failure);
			ConclusionRef = ResidentTransitionConclusionRef(Ledger.RealmId, SettlementId,
				ResidentId, TerminalPhase);
			KingdomPolityNamedFigureRecord exact = null;
			for (int i = 0; i < Ledger.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord row = Ledger.NamedFigures[i];
				if (row.ConclusionRef == ConclusionRef && row.Phase == TerminalPhase
					&& row.Origin == KingdomPolityFigureOrigin.PromotedByDeed
					&& row.ResidentId == 0
					&& string.IsNullOrEmpty(row.ResidentSettlementId)) return true;
				if (row.Phase != KingdomPolityFigurePhase.Active
					|| row.Origin != KingdomPolityFigureOrigin.PromotedByDeed
					|| row.ResidentId != ResidentId) continue;
				if (exact != null || row.ResidentSettlementId != SettlementId
					|| row.DisplayName != ResidentName)
					return Fail("deed-figure resident bridge is divergent", out Failure);
				exact = row;
			}
			if (exact == null) return true;
			if (Ledger.Revision != ExpectedRevision)
				return Fail("deed-figure transition revision conflict", out Failure);
			Prior = CopyResidentFigure(exact);
			KingdomPolityLedger candidate = Clone(Ledger);
			KingdomPolityNamedFigureRecord changed =
				KingdomPolityAuthority.Figure(candidate, exact.FigureId);
			changed.Phase = TerminalPhase; changed.ConclusionRef = ConclusionRef;
			changed.ResidentId = 0; changed.ResidentSettlementId = null;
			KingdomPolityPublicationResult result = KingdomPolityAuthority.Begin(Ledger);
			if (KingdomPolityAuthority.Commit(Ledger, candidate, result, out Failure)) return true;
			Prior = null; return false;
		}

		internal static bool TryRollbackDeedResident(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityNamedFigureRecord Prior, string ConclusionRef,
			out string Failure)
		{
			Failure = null;
			if (!TryValidate(Ledger, out Failure) || Prior == null
				|| Prior.Phase != KingdomPolityFigurePhase.Active
				|| Prior.Origin != KingdomPolityFigureOrigin.PromotedByDeed
				|| string.IsNullOrEmpty(ConclusionRef) || Ledger.Revision != ExpectedRevision)
				return Fail(Failure ?? "deed-figure rollback input is invalid", out Failure);
			KingdomPolityNamedFigureRecord current =
				KingdomPolityAuthority.Figure(Ledger, Prior.FigureId);
			if (current == null || current.ConclusionRef != ConclusionRef
				|| current.Phase == KingdomPolityFigurePhase.Active || current.ResidentId != 0
				|| !string.IsNullOrEmpty(current.ResidentSettlementId))
				return Fail("deed-figure rollback lost its terminal CAS", out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			int at = candidate.NamedFigures.FindIndex(F => F.FigureId == Prior.FigureId);
			if (at < 0) return Fail("deed-figure rollback target is absent", out Failure);
			candidate.NamedFigures[at] = CopyResidentFigure(Prior);
			return KingdomPolityAuthority.Commit(Ledger, candidate,
				KingdomPolityAuthority.Begin(Ledger), out Failure);
		}

		internal static KingdomPolityNamedFigureRecord CopyResidentTransitionFigure(
			KingdomPolityNamedFigureRecord Row)
		{
			return CopyResidentFigure(Row);
		}

		private static string ResidentTransitionConclusionRef(string RealmId,
			string SettlementId, int ResidentId, KingdomPolityFigurePhase TerminalPhase)
		{
			return ActivationId("taf:conclusion:resident-transition:v1:",
				"polity-deed-resident-transition-v1", RealmId, SettlementId,
				ResidentId.ToString(CultureInfo.InvariantCulture),
				((int)TerminalPhase).ToString(CultureInfo.InvariantCulture));
		}

		private static KingdomPolityNamedFigureRecord CopyResidentFigure(
			KingdomPolityNamedFigureRecord Row)
		{
			return Row == null ? null : new KingdomPolityNamedFigureRecord
			{
				FigureId = Row.FigureId, PolityId = Row.PolityId,
				DisplayName = Row.DisplayName, RoleKey = Row.RoleKey, Origin = Row.Origin,
				Phase = Row.Phase, CauseRef = Row.CauseRef, DeedSummary = Row.DeedSummary,
				ChronicleRef = Row.ChronicleRef, ConclusionRef = Row.ConclusionRef,
				ResidentId = Row.ResidentId, ResidentSettlementId = Row.ResidentSettlementId
			};
		}
	}
}
