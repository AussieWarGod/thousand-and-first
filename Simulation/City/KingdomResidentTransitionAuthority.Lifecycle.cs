using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentTransitionAuthority
	{
		private static bool TryProjectExpeditionClaims(KingdomSystem System, GameObject Body,
			int ResidentId, ref KingdomResidentTransitionClaim Claims)
		{
			if (System.Jobs == null || !System.Jobs.TryProjectResidentTransition(
				ResidentId, out bool expedition)) return false;
			if (expedition) Claims |= KingdomResidentTransitionClaim.Expedition;
			return true;
		}

		private static bool TryProjectLifecycleClaims(KingdomSystem System, GameObject Body,
			int ResidentId, ref KingdomResidentTransitionClaim Claims)
		{
			if (!ProjectLifecycleBook(System.LifecycleBook, Body, ResidentId,
				ref Claims)) return false;
			List<KingdomSettlement> others = System.NonSeatSettlements();
			for (int i = 0; others != null && i < others.Count; i++)
				if (others[i] == null || !ProjectLifecycleBook(others[i].LifecycleBook,
					Body, ResidentId, ref Claims)) return false;
			return true;
		}

		private static bool ProjectLifecycleBook(KingdomLifecycleBook Book, GameObject Body,
			int ResidentId, ref KingdomResidentTransitionClaim Claims)
		{
			if (Book == null) return false;
			KingdomLifecycleOperation operation = Book.NotableGuest;
			if (operation == null || operation.Action != KingdomLifecycleAction.Lodge)
				return true;
			KingdomLifecycleLodgeTerminalReceipt receipt = operation.LodgeTerminal;
			string objectId = Body.IDIfAssigned;
			bool target = string.Equals(operation.ObjectId, objectId, StringComparison.Ordinal)
				|| receipt != null && (receipt.ResidentId == ResidentId
					|| string.Equals(receipt.ObjectId, objectId, StringComparison.Ordinal));
			if (target && operation.Phase != KingdomLifecyclePhase.Terminal)
				Claims |= KingdomResidentTransitionClaim.OpenLodge;
			if (receipt?.MarketSourcePrepared
				!= KingdomLifecycleLodgeTerminalReceipt.MarketNone
				&& (receipt.MarketSourceResidentId == ResidentId
					|| string.Equals(receipt.MarketSourceBodyObjectId, objectId,
						StringComparison.Ordinal) || target))
				Claims |= KingdomResidentTransitionClaim.PreparedMarketHandoff;
			return true;
		}
	}
}
