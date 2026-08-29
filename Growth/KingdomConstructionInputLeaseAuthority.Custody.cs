using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomConstructionInputLeaseAuthority
	{
		/// <summary>Ordinary movement may carry a whole container, but never any purpose-owned or
		/// construction-leased object anywhere in its bounded custody graph.</summary>
		internal static bool TryObjectGraphAvailableForOrdinaryTransfer(GameObject item,
			out string failure)
		{
			failure = null;
			if (!KingdomOrdinaryCustody.TryCollect(item, out List<GameObject> graph, out failure))
				return false;
			KingdomConstructionInputLeaseSnapshot snapshot;
			if (!TryCapture(out snapshot, out failure)) return false;
			for (int i = 0; i < graph.Count; i++)
			{
				GameObject held = graph[i];
				if (KingdomPurpose.HasProtectedCargoEvidence(held))
				{
					failure = "Protected purpose cargo cannot move through an ordinary transfer.";
					return false;
				}
				if (IsLeased(snapshot, held))
				{
					failure = "A durable construction receipt owns an object in this custody graph.";
					return false;
				}
			}
			return true;
		}
	}
}
