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
			if (!r_KingdomImprovement.TryPublishInventoryManifest(Predecessor, Successor,
				cell, intent))
			{
				StopContentHandover(intent, ref job,
					"The exact inventory manifest remains retryable.");
				return false;
			}
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
			if (!r_KingdomImprovement.TryPublishLiquidCustody(Predecessor, Successor, intent))
			{
				StopContentHandover(intent, ref job,
					"The exact liquid-custody receipt remains retryable.");
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
			string custodyFailure;
			if (!r_KingdomImprovement.VerifyHandoverContentCustody(Predecessor, Successor,
				cell, intent, true, out custodyFailure))
			{
				r_KingdomImprovement.FailHandover(intent, custodyFailure);
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			if (!intent.HandoverEffectsDone)
			{
				// Authored plots settle their frozen scenery delta and metadata. Save-era plots retain
				// their durable procedural growth receipt. CarryMarks is the final publication.
				string layoutFailure = null;
				bool settled = false;
				try
				{
					settled = authoredUpgrade
						? KingdomArchitectureStamper.TryApplyUpgrade(Predecessor, Successor,
							Predecessor.CurrentZone, authoredSuccessor, out layoutFailure)
							&& KingdomPlots.TryStampAuthoredGrowth(Predecessor, Successor,
								authoredSuccessor, out layoutFailure)
						: KingdomPlots.GrowInPlace(Predecessor, Successor, SuccessorKey);
				}
				catch (System.Exception ex)
				{
					layoutFailure = "Plot growth threw during handover: " + ex.Message;
					if (!r_KingdomImprovement.VerifyHandoverContentCustody(Predecessor,
						Successor, cell, intent, true, out custodyFailure))
					{
						r_KingdomImprovement.FailHandover(intent, custodyFailure);
						if (job != null) KingdomConstruction.Quarantine(ref job,
							intent.HandoverFailure);
						return false;
					}
					if (authoredUpgrade)
						return RetryOrQuarantineAuthoredLayout(Predecessor, intent, ref job,
							layoutFailure);
					r_KingdomImprovement.FailHandover(intent,
						layoutFailure);
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				if (!settled)
				{
					if (!r_KingdomImprovement.VerifyHandoverContentCustody(Predecessor,
						Successor, cell, intent, true, out custodyFailure))
					{
						r_KingdomImprovement.FailHandover(intent, custodyFailure);
						if (job != null) KingdomConstruction.Quarantine(ref job,
							intent.HandoverFailure);
						return false;
					}
					layoutFailure = layoutFailure
						?? "The frozen plot-growth receipt did not settle exactly.";
					if (authoredUpgrade)
						return RetryOrQuarantineAuthoredLayout(Predecessor, intent, ref job,
							layoutFailure);
					r_KingdomImprovement.FailHandover(intent, layoutFailure);
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
				if (!r_KingdomImprovement.VerifyHandoverContentCustody(Predecessor,
					Successor, cell, intent, true, out custodyFailure))
				{
					r_KingdomImprovement.FailHandover(intent, custodyFailure);
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				CarryMarks(Predecessor, Successor, SuccessorKey);
				if (!ExactCarriedMarks(Predecessor, Successor, SuccessorKey)
					|| !r_KingdomImprovement.VerifyHandoverContentCustody(Predecessor,
						Successor, cell, intent, true, out custodyFailure))
				{
					r_KingdomImprovement.FailHandover(intent,
						custodyFailure ?? "Founder marks did not settle exactly on the successor.");
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				intent.HandoverEffectsDone = true;
			}
			else
			{
				string layoutFailure = null;
				bool settled = false;
				try
				{
					settled = authoredUpgrade
						? KingdomArchitectureStamper.TryApplyUpgrade(Predecessor, Successor,
							Predecessor.CurrentZone, authoredSuccessor, out layoutFailure)
							&& KingdomPlots.TryStampAuthoredGrowth(Predecessor, Successor,
								authoredSuccessor, out layoutFailure)
						: KingdomPlots.GrowInPlace(Predecessor, Successor, SuccessorKey);
				}
				catch (Exception exception)
				{
					layoutFailure = "Settled plot replay threw during handover: "
						+ exception.Message;
				}
				if (!r_KingdomImprovement.VerifyHandoverContentCustody(Predecessor,
					Successor, cell, intent, true, out custodyFailure))
				{
					r_KingdomImprovement.FailHandover(intent, custodyFailure);
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				if (!settled || !ExactCarriedMarks(Predecessor, Successor, SuccessorKey))
				{
					if (!settled && authoredUpgrade)
						return RetryOrQuarantineAuthoredLayout(Predecessor, intent, ref job,
							layoutFailure ?? "Settled authored plot state changed before removal.");
					r_KingdomImprovement.FailHandover(intent, layoutFailure
						?? "Settled founder marks changed before predecessor removal.");
					if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
			}
			return true;
		}

		private static void StopContentHandover(r_KingdomImprovement Receipt,
			ref KingdomConstructionJob Job, string RetryFailure)
		{
			if (Job == null) return;
			if (Receipt.HandoverQuarantined)
				KingdomConstruction.Quarantine(ref Job, Receipt.HandoverFailure);
			else KingdomConstruction.FinishProjection(ref Job, false, false, RetryFailure);
		}

		private static bool RetryOrQuarantineAuthoredLayout(GameObject Owner,
			r_KingdomImprovement Receipt, ref KingdomConstructionJob Job, string Failure)
		{
			if (KingdomArchitectureStamper.IsUpgradeQuarantined(Owner,
				out string quarantine))
			{
				r_KingdomImprovement.FailHandover(Receipt, quarantine ?? Failure);
				if (Job != null) KingdomConstruction.Quarantine(ref Job, Receipt.HandoverFailure);
				return false;
			}
			Receipt.HandoverFailure = Failure != null && Failure.Length > 2048
				? Failure.Substring(0, 2048) : Failure;
			if (Job != null) KingdomConstruction.FinishProjection(ref Job, false, false,
				Receipt.HandoverFailure ?? "The authored renovation remains retryable.");
			return false;
		}
	}
}
