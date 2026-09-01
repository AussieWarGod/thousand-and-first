using XRL.World;

namespace ThousandAndFirst
{
	internal enum KingdomPolityResidentTransitionCause : byte
	{
		Departure = 1,
		Death = 2,
		Accession = 3
	}

	internal readonly struct KingdomPolityResidentTransitionPreparation
	{
		internal readonly KingdomPolityNamedFigureRecord Prior;
		internal readonly string ConclusionRef;

		internal KingdomPolityResidentTransitionPreparation(
			KingdomPolityNamedFigureRecord prior, string conclusionRef)
		{
			Prior = prior; ConclusionRef = conclusionRef;
		}
	}

	internal static class KingdomPolityResidentTransition
	{
		internal static bool TryConclude(KingdomSystem System, GameObject Body, int ResidentId,
			KingdomPolityResidentTransitionCause Cause,
			out KingdomPolityResidentTransitionPreparation Preparation, out string Failure)
		{
			Preparation = default(KingdomPolityResidentTransitionPreparation); Failure = null;
			if (System?.PolityLedger == null) return true;
			string settlementId = System.SettlementIdForOwnedZone(Body?.CurrentZone?.ZoneID);
			string name = Body?.GetStringProperty("KingdomName");
			KingdomPolityFigurePhase phase = Cause == KingdomPolityResidentTransitionCause.Death
				? KingdomPolityFigurePhase.Dead
				: Cause == KingdomPolityResidentTransitionCause.Departure
					? KingdomPolityFigurePhase.Departed
					: KingdomPolityFigurePhase.Transferred;
			if (!KingdomPolityRules.TryConcludeDeedResident(System.PolityLedger,
				System.PolityLedger.Revision, settlementId, ResidentId, name, phase,
				out KingdomPolityNamedFigureRecord prior, out string conclusion,
				out Failure)) return false;
			Preparation = new KingdomPolityResidentTransitionPreparation(prior, conclusion);
			return true;
		}

		internal static bool TryRollback(KingdomSystem System,
			KingdomPolityResidentTransitionPreparation Preparation, out string Failure)
		{
			Failure = null;
			if (Preparation.Prior == null) return true;
			return System?.PolityLedger != null
				&& KingdomPolityRules.TryRollbackDeedResident(System.PolityLedger,
					System.PolityLedger.Revision, Preparation.Prior,
					Preparation.ConclusionRef, out Failure);
		}
	}
}
