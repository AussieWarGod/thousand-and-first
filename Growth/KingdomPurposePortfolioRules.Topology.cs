using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		/// <summary>Projects secession/rejoin onto one exact pair epoch. A no-op returns
		/// an equal-revision copy; only a lawful orphan/resume changes revision.</summary>
		public static bool TryReconcileTopology(KingdomPurposePairReceipt Current,
			IList<string> ActiveSettlementIds, out KingdomPurposePairReceipt Reconciled,
			out KingdomPurposePairFault Fault)
		{
			Reconciled = null;
			Fault = KingdomPurposePairFault.Identity;
			if (!ValidPair(Current, out _) || ActiveSettlementIds == null
				|| ActiveSettlementIds.Count < 1 || ActiveSettlementIds.Count >
					KingdomSettlementTopologyRules.MaxOwnedSettlements
				|| !ExactSettlementSet(ActiveSettlementIds)) return false;
			Reconciled = Current.Copy();
			if (Current.Phase == KingdomPurposePairPhase.Dormant
				|| Current.Phase == KingdomPurposePairPhase.Quarantined)
			{
				Fault = KingdomPurposePairFault.None;
				return true;
			}
			bool bothLive = Contains(ActiveSettlementIds, Current.FirstSettlementId)
				&& Contains(ActiveSettlementIds, Current.SecondSettlementId);
			if (Current.Phase == KingdomPurposePairPhase.Orphaned)
			{
				if (!bothLive)
				{
					Fault = KingdomPurposePairFault.None;
					return true;
				}
				if (Current.Revision == int.MaxValue)
				{
					Fault = KingdomPurposePairFault.Bounds;
					Reconciled = null;
					return false;
				}
				Reconciled.Phase = Current.ResumePhase;
				Reconciled.ResumePhase = KingdomPurposePairPhase.Invalid;
				Reconciled.Revision++;
			}
			else if (!bothLive)
			{
				if (Current.Revision == int.MaxValue)
				{
					Fault = KingdomPurposePairFault.Bounds;
					Reconciled = null;
					return false;
				}
				Reconciled.ResumePhase = Current.Phase;
				Reconciled.Phase = KingdomPurposePairPhase.Orphaned;
				Reconciled.Revision++;
			}
			if (Reconciled.Revision != Current.Revision
				&& !PairRevisionHeadroomIsValid(Reconciled))
			{
				Fault = KingdomPurposePairFault.Bounds;
				Reconciled = null;
				return false;
			}
			if (Reconciled.Revision != Current.Revision
				&& !ValidTransition(Current, Reconciled, out Fault))
			{
				Reconciled = null;
				return false;
			}
			Fault = KingdomPurposePairFault.None;
			return true;
		}

		private static bool ExactSettlementSet(IList<string> Values)
		{
			for (int i = 0; i < Values.Count; i++)
			{
				if (!Id(Values[i])) return false;
				for (int j = i + 1; j < Values.Count; j++)
					if (Values[i] == Values[j]) return false;
			}
			return true;
		}

		private static bool Contains(IList<string> Values, string Expected)
		{
			for (int i = 0; i < Values.Count; i++)
				if (Values[i] == Expected) return true;
			return false;
		}
	}
}
