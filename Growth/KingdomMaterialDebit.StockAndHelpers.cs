using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomMaterialDebit
	{
		private void AdjustStockFor(KingdomMaterialDebitCost Loss)
		{
			if (StockAdjusted || Loss == null || Loss.IsEmpty) return;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				Stock.Tally.Add((KingdomMaterial)i, -Loss.Materials.Get((KingdomMaterial)i));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				Stock.Bits.Add(i, -Loss.Bits.Get(i));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				Stock.Exotics.Add((KingdomExotic)i, -Loss.Exotics.Get((KingdomExotic)i));
			}
			AdjustedLoss = Loss.Copy();
			StockAdjusted = true;
		}

		private void RestoreStockAdjustment(KingdomMaterialDebitCost Loss)
		{
			KingdomMaterialDebitCost restore = AdjustedLoss ?? Loss;
			if (!StockAdjusted || restore == null) return;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				Stock.Tally.Add((KingdomMaterial)i, restore.Materials.Get((KingdomMaterial)i));
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				Stock.Bits.Add(i, restore.Bits.Get(i));
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				Stock.Exotics.Add((KingdomExotic)i, restore.Exotics.Get((KingdomExotic)i));
			}
			AdjustedLoss = null;
			StockAdjusted = false;
		}

		private void ReconcileStockFor(KingdomMaterialDebitCost Loss)
		{
			if (StockAdjusted)
			{
				RestoreStockAdjustment(AdjustedLoss);
			}
			AdjustStockFor(Loss);
		}

		private void FailReservation(KingdomMaterialDebitFault Fault, string Failure)
		{
			KingdomMaterialDebitCost requested = Reservation.Requested;
			Plan = null;
			Entries.Clear();
			Containers.Clear();
			Removed.Clear();
			ExactObservations.Clear();
			Reservation = KingdomMaterialDebitRules.EmptyResult(
				KingdomMaterialDebitOutcome.InvalidReservation, Fault, requested, Failure);
			Result = Reservation;
		}

		private KingdomMaterialDebitResult Transient(KingdomMaterialDebitFault Fault,
			string Failure)
		{
			return new KingdomMaterialDebitResult(Result.Outcome, Fault, Result.Requested,
				Result.Spent, Result.Outstanding, Result.Lost, Result.FinalizedSources, Failure,
				Result.MeasurementExact);
		}

		private static bool ContainsReference(IList<GameObject> Items, GameObject Candidate)
		{
			for (int i = 0; Items != null && i < Items.Count; i++)
			{
				if (ReferenceEquals(Items[i], Candidate)) return true;
			}
			return false;
		}

		private static bool SameBits(KingdomBitTally A, KingdomBitTally B)
		{
			if (A == null || B == null) return A == B;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				if (A.Get(i) != B.Get(i)) return false;
			}
			return true;
		}

		private static string ReservationFailure(KingdomMaterialDebitFault Fault)
		{
			switch (Fault)
			{
			case KingdomMaterialDebitFault.InsufficientMaterials:
				return "The exact material sources do not cover the claim.";
			case KingdomMaterialDebitFault.InsufficientBits:
				return "The exact bit-stock sources do not cover the claim.";
			case KingdomMaterialDebitFault.InsufficientExotics:
				return "The exact exotic sources do not cover the claim.";
			default:
				return "The exact material claim could not be reserved.";
			}
		}

		private static string Describe(Exception Exception)
		{
			return (Exception == null)
				? "An unknown engine exception interrupted the material receipt."
				: Exception.GetType().Name + ": " + Exception.Message;
		}
	}
}
