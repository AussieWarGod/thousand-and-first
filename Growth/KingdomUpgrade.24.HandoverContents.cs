using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		private static bool TryCarryHandoverContents(GameObject Predecessor,
			GameObject Successor, Cell cell, string SuccessorKey,
			r_KingdomImprovement intent, KingdomArchitectureIntent authoredSuccessor,
			bool authoredUpgrade, ref KingdomConstructionJob job,
			out int carriedLiquid, out int carriedItems)
		{
			carriedLiquid = 0;
			carriedItems = 0;
			if (!r_KingdomImprovement.CarryLiquidDurable(Predecessor, Successor, intent,
				out carriedLiquid))
			{
				if (job != null)
				{
					if (intent.HandoverQuarantined)
						KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					else KingdomConstruction.FinishProjection(ref job, false, false,
						"The exact liquid handover was restored and remains retryable.");
				}
				return false;
			}
			if (!ExactHandoverEndpointsAfterCallback(Predecessor, Successor, cell,
				SuccessorKey, intent, job))
			{
				r_KingdomImprovement.FailHandover(intent,
					"A construction endpoint changed during liquid handover.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			if (!r_KingdomImprovement.CarryInventoryDurable(Predecessor, Successor, cell, intent,
				out carriedItems))
			{
				if (job != null)
				{
					if (intent.HandoverQuarantined)
						KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					else KingdomConstruction.FinishProjection(ref job, false, false,
						"The exact item handover was restored and remains retryable.");
				}
				return false;
			}
			if (!ExactHandoverEndpointsAfterCallback(Predecessor, Successor, cell,
				SuccessorKey, intent, job))
			{
				r_KingdomImprovement.FailHandover(intent,
					"A construction endpoint changed during item handover.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			if (!intent.HandoverEffectsDone)
			{
				// Authored plots settle their frozen scenery delta and metadata. Save-era plots retain
				// their durable procedural growth receipt. CarryMarks is the final publication.
				try
				{
					string layoutFailure = null;
					bool settled = authoredUpgrade
						? KingdomArchitectureStamper.TryApplyUpgrade(Predecessor, Successor,
							Predecessor.CurrentZone, authoredSuccessor, out layoutFailure)
							&& KingdomPlots.TryStampAuthoredGrowth(Predecessor, Successor,
								authoredSuccessor, out layoutFailure)
						: KingdomPlots.GrowInPlace(Predecessor, Successor, SuccessorKey);
					if (!settled)
						throw new InvalidOperationException(layoutFailure
							?? "The frozen plot-growth receipt did not settle exactly.");
				}
				catch (System.Exception ex)
				{
					r_KingdomImprovement.FailHandover(intent,
						"Plot growth threw during handover: " + ex.Message);
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				if (!ExactHandoverEndpointsAfterCallback(Predecessor, Successor, cell,
					SuccessorKey, intent, job))
				{
					r_KingdomImprovement.FailHandover(intent,
						"An improvement endpoint changed during plot growth.");
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				CarryMarks(Predecessor, Successor, SuccessorKey);
				if (!ExactCarriedMarks(Predecessor, Successor, SuccessorKey))
				{
					r_KingdomImprovement.FailHandover(intent,
						"Founder marks did not settle exactly on the successor.");
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				intent.HandoverEffectsDone = true;
			}
			else
			{
				string layoutFailure = null;
				bool settled = authoredUpgrade
					? KingdomArchitectureStamper.TryApplyUpgrade(Predecessor, Successor,
						Predecessor.CurrentZone, authoredSuccessor, out layoutFailure)
						&& KingdomPlots.TryStampAuthoredGrowth(Predecessor, Successor,
							authoredSuccessor, out layoutFailure)
					: KingdomPlots.GrowInPlace(Predecessor, Successor, SuccessorKey);
				if (!settled || !ExactCarriedMarks(Predecessor, Successor, SuccessorKey))
				{
					r_KingdomImprovement.FailHandover(intent, layoutFailure
						?? "Settled founder marks changed before predecessor removal.");
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
			}
			return true;
		}
	}
}
