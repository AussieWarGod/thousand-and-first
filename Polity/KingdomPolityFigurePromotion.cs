using System;

namespace ThousandAndFirst
{
	[Serializable]
	public sealed class KingdomPolityFigurePromotionFacts
	{
		public string PolityId;
		public string SettlementId;
		public int ResidentId;
		public string DisplayName;
		public string RoleKey;
		public KingdomPolityFigureOrigin Origin;
		public string CauseRef;
		public string ChronicleRef;
		public string DeedSummary;
	}

	public static partial class KingdomPolityRules
	{
		/// <summary>Promotes one exact resident only from a typed deed receipt.</summary>
		public static bool TryPromoteNamedFigure(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityFigurePromotionFacts Facts,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) || !ValidPromotion(Facts, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityRecord polity = KingdomPolityAuthority.Polity(Ledger, Facts.PolityId);
			if (polity == null || polity.Source != KingdomPolitySource.CurrentRealm ||
				polity.Lifecycle != KingdomPolityLifecycle.Active)
				return KingdomPolityAuthority.Refuse(Result,
					"promotion does not belong to the active current polity", out Failure);
			string figureId = ActivationId("taf:figure:promotion:v1:",
				"polity-figure-promotion-v1", Facts.PolityId, Facts.SettlementId,
				Facts.ResidentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
				Facts.RoleKey, Facts.CauseRef);
			KingdomPolityNamedFigureRecord expected = new KingdomPolityNamedFigureRecord
			{
				FigureId = figureId, PolityId = Facts.PolityId, DisplayName = Facts.DisplayName,
				RoleKey = Facts.RoleKey, Origin = Facts.Origin,
				Phase = KingdomPolityFigurePhase.Active, CauseRef = Facts.CauseRef,
				ChronicleRef = Facts.ChronicleRef, DeedSummary = Facts.DeedSummary,
				ResidentId = Facts.ResidentId,
				ResidentSettlementId = Facts.SettlementId
			};
			KingdomPolityNamedFigureRecord existing = KingdomPolityAuthority.Figure(Ledger, figureId);
			if (existing != null && ExactPromotion(existing, expected))
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (existing != null || ResidentClaimed(Ledger, Facts.SettlementId, Facts.ResidentId))
				return KingdomPolityAuthority.Refuse(Result,
					"promotion conflicts with an existing named identity", out Failure);
			if (Ledger.NamedFigures.Count >= MaxNamedFigures ||
				KingdomPolityAttentionRules.ActiveNamedFigures(Ledger, Facts.PolityId) >=
					KingdomPolityAttentionRules.MaximumActiveNamedFigures)
				return KingdomPolityAuthority.Refuse(Result,
					"scarce named-figure attention is full", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			InsertFigure(candidate, expected);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		/// <summary>
		/// One migration CAS retires every active legacy office-origin row. Current office
		/// presence is deliberately irrelevant: a title never authorizes a Polity role.
		/// </summary>
		public static bool TryRetireAllOfficeFigures(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CauseRef,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!TryValidate(Ledger, out Failure) || !TypedId(CauseRef,
				"taf:fact:office-retirement:v1:"))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "office retirement evidence is invalid", out Failure);
			bool active = false;
			for (int i = 0; i < Ledger.NamedFigures.Count; i++)
				if (Ledger.NamedFigures[i].Origin == KingdomPolityFigureOrigin.Officeholder &&
					Ledger.NamedFigures[i].Phase == KingdomPolityFigurePhase.Active) active = true;
			if (!active) { Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true; }
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = Clone(Ledger);
			for (int i = 0; i < candidate.NamedFigures.Count; i++)
			{
				KingdomPolityNamedFigureRecord old = candidate.NamedFigures[i];
				if (old.Origin != KingdomPolityFigureOrigin.Officeholder ||
					old.Phase != KingdomPolityFigurePhase.Active) continue;
				old.Phase = KingdomPolityFigurePhase.Transferred;
				old.ConclusionRef = ActivationId("taf:conclusion:office:v1:",
					"polity-title-only-retirement-v1", old.FigureId, CauseRef);
				old.ResidentId = 0; old.ResidentSettlementId = null;
			}
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static bool ValidPromotion(KingdomPolityFigurePromotionFacts F,
			out string Failure)
		{
			Failure = null;
			bool deed = F != null && F.Origin == KingdomPolityFigureOrigin.PromotedByDeed;
			bool role = F != null && deed && PromotionRole(F.RoleKey);
			if (F == null || !SemanticId(F.PolityId) ||
				!TypedId(F.SettlementId, "taf:settlement:v1:") || F.ResidentId < 1 ||
				!Text(F.DisplayName, true) || !role ||
				!KingdomPolityAmbientTransactionRules.SafeText(F.DeedSummary, true) ||
				!deed || !SemanticId(F.CauseRef) ||
				(deed && !F.CauseRef.StartsWith("taf:fact:deed:", StringComparison.Ordinal)) ||
				(!string.IsNullOrEmpty(F.ChronicleRef) && !SemanticId(F.ChronicleRef)))
				return Fail("named promotion lacks an exact deed fact", out Failure);
			return true;
		}

		private static bool PromotionRole(string Role)
		{
			return Role == "guard" || Role == "patrol" || Role == "courier" ||
				Role == "trader" || Role == "migrant" || Role == "envoy" ||
				Role == "salvager";
		}

		/// <summary>The deed-receipt law shares the promotion role set without widening it.</summary>
		internal static bool DeedPromotionRole(string Role)
		{
			return PromotionRole(Role);
		}

		private static bool ExactPromotion(KingdomPolityNamedFigureRecord A,
			KingdomPolityNamedFigureRecord E)
		{
			return A.PolityId == E.PolityId && A.DisplayName == E.DisplayName &&
				A.RoleKey == E.RoleKey && A.Origin == E.Origin &&
				A.Phase == KingdomPolityFigurePhase.Active && A.CauseRef == E.CauseRef &&
				A.ChronicleRef == E.ChronicleRef && A.DeedSummary == E.DeedSummary &&
				string.IsNullOrEmpty(A.ConclusionRef) &&
				A.ResidentId == E.ResidentId &&
				A.ResidentSettlementId == E.ResidentSettlementId;
		}

		private static bool ResidentClaimed(KingdomPolityLedger L, string Settlement, int Resident)
		{
			for (int i = 0; i < L.NamedFigures.Count; i++)
				if (L.NamedFigures[i].Phase == KingdomPolityFigurePhase.Active &&
					L.NamedFigures[i].ResidentId == Resident &&
					L.NamedFigures[i].ResidentSettlementId == Settlement) return true;
			return false;
		}

		private static void InsertFigure(KingdomPolityLedger Ledger,
			KingdomPolityNamedFigureRecord Figure)
		{
			int at = 0;
			while (at < Ledger.NamedFigures.Count && string.CompareOrdinal(
				Ledger.NamedFigures[at].FigureId, Figure.FigureId) < 0) at++;
			Ledger.NamedFigures.Insert(at, Figure);
		}
	}
}
