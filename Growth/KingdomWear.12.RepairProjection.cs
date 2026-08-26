using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomWear
	{
		// ==================================================================================
		// Repair: costed and timed exactly as a strike is (KingdomMaterials.WorkStrike), one job
		// settlement-wide at a time.
		// ==================================================================================

		private static KingdomWearRules.RepairVerdict Assess(Zone Z, GameObject Work, r_KingdomWear WearPart, int FreeHands)
		{
			if (WearPart.Held)
			{
				return KingdomWearRules.RepairVerdict.Held;
			}
			bool covered = Covers(Z, Work, WearPart.Wear);
			return KingdomWearRules.AssessRepair(WearPart.Held, FreeHands, covered);
		}

		private static bool Covers(Zone Z, GameObject Work, int Wear)
		{
			BuildTallies(Work, Wear, out KingdomMaterialTally cost, out KingdomBitTally bitCost);
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			return KingdomMaterialRules.Covers(stock.Tally, cost) && KingdomMaterialRules.CoversBits(stock.Bits, bitCost);
		}

		private static void BuildTallies(GameObject Work, int Wear, out KingdomMaterialTally Cost, out KingdomBitTally BitCost)
		{
			string designKey = KingdomUpgrade.DesignKeyOf(Work);
			KingdomMaterialTally buildCost = string.IsNullOrEmpty(designKey) ? null : KingdomMaterials.CostFor(designKey);
			KingdomBitTally buildBits = string.IsNullOrEmpty(designKey) ? null : KingdomMaterials.BitCostFor(designKey);
			Cost = KingdomMaterialRules.RepairCost(buildCost, Wear);
			BitCost = KingdomMaterialRules.RepairBits(buildBits, Wear);
		}

		private static void StartRepair(KingdomSystem System, GameObject Work, r_KingdomWear WearPart, long TimeTicks)
		{
			RepairTargetFrame targetFrame;
			if (!TryCaptureRepairTarget(Work, WearPart, out targetFrame)) return;
			BuildTallies(Work, WearPart.Wear, out KingdomMaterialTally cost, out KingdomBitTally bitCost);
			Zone zone = Work.CurrentZone;
			if (zone == null || HasActiveRepair(Work, out _)
				|| KingdomConstruction.HasActiveSubject(System, zone,
					KingdomConstructionRoute.WearRepair, Work))
			{
				return;
			}
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(cost, bitCost, null);
			KingdomSurvey survey = KingdomSurvey.Take(zone, System);
			KingdomWaterDebit water = survey.ReserveExactWater(0);
			KingdomMaterialDebit materials = cost.IsEmpty()
				? KingdomMaterials.ReserveBits(zone, bitCost)
				: KingdomMaterials.ReserveComposite(zone, claim);
			string target = KingdomUpgrade.DesignKeyOf(Work);
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, zone,
				KingdomConstructionRoute.WearRepair, Work.CurrentCell, Work, target,
				RepairPayload(WearPart.Wear, false),
				0, claim, TimeTicks, TimeTicks);
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (!RepairTargetExact(targetFrame, targetFrame.Receipt))
			{
				if (job != null && !KingdomConstructionRules.IsTerminal(job.Phase))
				{
					KingdomConstruction.Quarantine(ref job,
						"A funding callback changed the exact repair work, wear part, cell, zone, or state.");
				}
				KingdomLog.Log("wear: repair funding target became uncertain");
				return;
			}
			if (funding == KingdomConstructionStartResult.Refused)
			{
				KingdomLog.Log("wear: repair refused cleanly " + (fundingFailure ?? Work.Blueprint));
				return;
			}
			KingdomConstruction.Bind(Work, job);
			if (!RepairTargetExact(targetFrame, job.Id))
			{
				KingdomConstruction.Quarantine(ref job,
					"The funded repair receipt did not bind to its exact original work and wear part.");
				return;
			}
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				System.Ledger.Note("{{r|The mending receipt remains outstanding. The damaged work stays queued without another charge.}}");
				return;
			}
			if (!ProjectRepair(System, Work, WearPart, job, out job, out string projectionFailure))
			{
				System.Ledger.Note("{{r|The paid mending could not yet be put in hand. Its receipt remains queued.}}");
				KingdomLog.Log("construction: repair projection waits: " + projectionFailure);
				return;
			}
			string name = DisplayName(Work);
			System.Ledger.Note("{{K|" + KingdomWearRules.RepairBegunLine(name) + "}}");
			KingdomLog.Log("wear: repair begun " + Work.Blueprint + " wear=" + WearPart.Wear + " effort=" + WearPart.RepairEffortLeft);
		}

		private static bool ProjectRepair(KingdomSystem System, GameObject Work,
			r_KingdomWear WearPart,
			KingdomConstructionJob Job, out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			if (!GameObject.Validate(Work) || Work.CurrentCell == null || WearPart == null
				|| WearPart.Wear <= 0)
			{
				Failure = "The paid damaged work is absent.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			if (WearPart.RepairEffortLeft > 0 && KingdomConstruction.HasReceipt(Work, Job))
			{
				KingdomConstruction.FinishProjection(ref Updated, true, true);
				return true;
			}
			int requestedWear;
			bool finishing;
			KingdomMaterialDebitCost requested;
			if (!TryRepairPayload(Job.Payload, out requestedWear, out finishing)
				|| WearPart.Wear != requestedWear
				|| !KingdomMaterialDebitCost.TryParseClaim(Job.Claims.MaterialRequested, out requested))
			{
				Failure = "The paid repair target no longer matches its durable receipt.";
				return false;
			}
			if (finishing)
			{
				return FinishRepairProjection(System, Work, WearPart, Job,
					out Updated, out Failure);
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			int effort = KingdomMaterialRules.RepairEffort(
				requested.Materials.Total() + requested.Bits.Total(), requestedWear);
			if (effort <= 0)
			{
				Failure = "The repair receipt resolved to no measurable work.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			KingdomConstruction.Bind(Work, Updated);
			WearPart.RepairEffortLeft = effort;
			KingdomMaterials.WriteTick(Work, RepairWorkedProperty, The.Game.TimeTicks);
			WearPart.AnnouncedBlock = 0;
			if (!KingdomConstruction.HasReceipt(Work, Updated)
				|| WearPart.RepairEffortLeft != effort)
			{
				Failure = "The repair work could not be verified on its damaged work.";
				KingdomConstruction.FinishProjection(ref Updated, false, false, Failure);
				return false;
			}
			KingdomConstruction.FinishProjection(ref Updated, true, true);
			return true;
		}

	}
}
