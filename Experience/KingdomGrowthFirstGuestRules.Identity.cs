using System;
using System.IO;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		public static bool GrowthFirstGuestBlueprintAllowed(string Blueprint)
		{
			switch (Blueprint)
			{
			case "r_KingdomSettler":
			case "r_KingdomSettlerHand":
			case "r_KingdomSettlerDrifter":
			case "r_KingdomSettlerTinker":
			case "r_KingdomSettlerScribe":
			case "r_KingdomSettlerYoung":
			case "r_KingdomSettlerPhysicker":
			case "r_KingdomSettlerMechanimist":
			case "r_KingdomSettlerSnapjaw": return true;
			default: return false;
			}
		}

		public static string GrowthFirstGuestOpportunityId(string SettlementId, long Sequence)
		{
			return KingdomGrowthFirstGuestIdentityRules.OpportunityId(SettlementId, Sequence);
		}

		public static string GrowthFirstGuestCauseId(string SettlementId, long Sequence,
			long CauseTick, long CadenceTicks)
		{
			return KingdomGrowthFirstGuestIdentityRules.CauseId(
				SettlementId, Sequence, CauseTick, CadenceTicks);
		}

		public static string GrowthFirstGuestAudienceReservationId(string OpportunityId)
		{
			if (!ValidGeneratedId(OpportunityId)) return null;
			return HashId("experience-audience:first-guest:v1", delegate(BinaryWriter w)
			{
				CanonicalString(w, OpportunityId);
			});
		}

		public static string GrowthFirstGuestBodyReservationId(string OpportunityId)
		{
			if (!ValidGeneratedId(OpportunityId)) return null;
			return HashId("experience-body:first-guest:v1", delegate(BinaryWriter w)
			{
				CanonicalString(w, OpportunityId);
			});
		}

		private static string GrowthFirstGuestReceiptId(string OpportunityId, string Kind,
			long Tick)
		{
			if (!ValidGeneratedId(OpportunityId) || string.IsNullOrEmpty(Kind) || Tick < 0L)
				return null;
			return HashId("growth-first-guest-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, OpportunityId); CanonicalString(w, Kind); w.Write(Tick);
			});
		}
	}
}
