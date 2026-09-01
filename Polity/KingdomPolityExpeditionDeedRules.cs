using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Pure identity law for a resident's proved salvage deed.</summary>
	public static class KingdomPolityExpeditionDeedRules
	{
		public const string CausePrefix = "taf:fact:deed:v1:";
		public const string FigurePrefix = "taf:figure:promotion:v1:";
		public const int RichFindOutcomeCode = 3;
		public const string Summary = "returned from a salvage expedition with a rich find";
		public const string RoleKey = "salvager";

		public static bool TryCauseRef(string PolityId, string SettlementId, int JobId,
			int ResidentId, string ChronicleRef, out string CauseRef)
		{
			CauseRef = null;
			string expectedChronicle = ExpectedChronicleRef(JobId);
			if (!KingdomPolityRules.SemanticId(PolityId) ||
				!KingdomPolityRules.TypedId(SettlementId, "taf:settlement:v1:") ||
				JobId < 1 || ResidentId < 1 ||
				!string.Equals(ChronicleRef, expectedChronicle,
					System.StringComparison.Ordinal)) return false;
			CauseRef = KingdomPolityRules.ActivationId(CausePrefix,
				"polity-rich-salvage-deed-v1", PolityId, SettlementId,
				JobId.ToString(CultureInfo.InvariantCulture),
				ResidentId.ToString(CultureInfo.InvariantCulture), ChronicleRef);
			return KingdomPolityRules.TypedId(CauseRef, CausePrefix);
		}

		public static string ExpectedChronicleRef(int JobId)
		{
			if (JobId < 1) return null;
			return "taf:expedition:" + JobId.ToString(CultureInfo.InvariantCulture) + ":" +
				RichFindOutcomeCode.ToString(CultureInfo.InvariantCulture);
		}

		public static bool TryFigureRef(string PolityId, string SettlementId, int JobId,
			int ResidentId, string ChronicleRef, out string CauseRef, out string FigureRef)
		{
			FigureRef = null;
			if (!TryCauseRef(PolityId, SettlementId, JobId, ResidentId, ChronicleRef,
				out CauseRef)) return false;
			FigureRef = KingdomPolityRules.ActivationId(FigurePrefix,
				"polity-figure-promotion-v1", PolityId, SettlementId,
				ResidentId.ToString(CultureInfo.InvariantCulture), RoleKey, CauseRef);
			return KingdomPolityRules.TypedId(FigureRef, FigurePrefix);
		}

		/// <summary>Matches the immutable deed receipt before or after its resident bridge ends.</summary>
		public static bool ExactReceipt(KingdomPolityNamedFigureRecord Row, string PolityId,
			string SettlementId, int JobId, int ResidentId, string DisplayName,
			string ChronicleRef)
		{
			if (Row == null || !TryFigureRef(PolityId, SettlementId, JobId, ResidentId,
				ChronicleRef, out string cause, out string figure)) return false;
			bool active = Row.Phase == KingdomPolityFigurePhase.Active;
			bool bridge = active
				? Row.ResidentId == ResidentId && Row.ResidentSettlementId == SettlementId &&
					string.IsNullOrEmpty(Row.ConclusionRef)
				: Row.ResidentId == 0 && string.IsNullOrEmpty(Row.ResidentSettlementId) &&
					KingdomPolityRules.SemanticId(Row.ConclusionRef);
			return Row.FigureId == figure && Row.PolityId == PolityId &&
				Row.DisplayName == DisplayName && Row.RoleKey == RoleKey &&
				Row.Origin == KingdomPolityFigureOrigin.PromotedByDeed &&
				Row.CauseRef == cause && Row.ChronicleRef == ChronicleRef &&
				Row.DeedSummary == Summary && bridge;
		}
	}
}
