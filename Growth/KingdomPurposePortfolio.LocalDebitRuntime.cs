using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private enum PurposeDebitObservation : byte { Before = 1, After = 2, Invalid = 3 }

		private static bool DriveLocalDebit(KingdomPurposePairReceipt Pair,
			out KingdomPurposePairReceipt Published, out string Failure)
		{
			Published = Pair;
			Failure = null;
			KingdomPurposeOperationReceipt operation = Pair?.Operation;
			if (operation == null || !KingdomPurposePortfolioRules.TryDecodeLocalDebit(
				operation.LocalDebitReceipt, out KingdomPurposeLocalDebitReceipt debit)
				|| !TryPurposeZone(operation.SourceZoneId, out Zone zone))
				return Fail("The frozen local purpose debit cannot be read.", out Failure);
			if (!TryObserveDebit(debit, zone, out PurposeDebitObservation[] states,
				out int water, out int food, out string materials, out Failure))
				return QuarantinePortfolio(Pair, Failure, out Published, out Failure);
			bool complete = true;
			int next = -1;
			for (int i = 0; i < states.Length; i++)
				if (states[i] == PurposeDebitObservation.Before)
				{
					complete = false;
					if (next < 0) next = i;
				}
			KingdomPurposeOperationReceipt evidence = operation.Copy();
			evidence.WaterSpent = water;
			evidence.FoodSpent = food;
			evidence.MaterialSpent = materials;
			if (!complete)
			{
				if (!ApplyDebitLine(debit.Lines[next], zone, out Failure))
					return QuarantinePortfolio(Pair, Failure, out Published, out Failure);
				if (!TryObserveDebit(debit, zone, out states, out water, out food,
					out materials, out Failure))
					return QuarantinePortfolio(Pair, Failure, out Published, out Failure);
				evidence.WaterSpent = water;
				evidence.FoodSpent = food;
				evidence.MaterialSpent = materials;
			}
			complete = true;
			for (int i = 0; i < states.Length; i++)
				if (states[i] != PurposeDebitObservation.After) { complete = false; break; }
			evidence.Phase = complete ? KingdomPurposeOperationPhase.LocalDebited
				: KingdomPurposeOperationPhase.LocalDebitPending;
			if (SameOperationEvidence(operation, evidence)) return true;
			evidence.Revision++;
			return TryPublishOperation(Pair, evidence, Pair.Phase, out Published, out Failure);
		}

		private static bool TryObserveDebit(KingdomPurposeLocalDebitReceipt Debit, Zone Zone,
			out PurposeDebitObservation[] States, out int Water, out int Food,
			out string Materials, out string Failure)
		{
			States = new PurposeDebitObservation[Debit.Lines.Count];
			Water = 0;
			Food = 0;
			Failure = null;
			KingdomMaterialTally tally = new KingdomMaterialTally();
			for (int i = 0; i < Debit.Lines.Count; i++)
			{
				KingdomPurposeDebitLine line = Debit.Lines[i];
				States[i] = ObserveDebitLine(line, Zone);
				if (States[i] == PurposeDebitObservation.Invalid)
				{
					Materials = null;
					return Fail("A frozen local-debit object is neither at its exact before nor after state.",
						out Failure);
				}
				if (States[i] != PurposeDebitObservation.After) continue;
				int spent = line.Before - line.After;
				if (line.Kind == KingdomPurposeDebitKind.Water) Water += spent;
				else if (line.Kind == KingdomPurposeDebitKind.Food) Food += spent;
				else tally.Add((KingdomMaterial)line.TypeIndex, spent);
			}
			Materials = new KingdomMaterialDebitCost(tally).ToClaimString();
			return true;
		}

		private static PurposeDebitObservation ObserveDebitLine(
			KingdomPurposeDebitLine Line, Zone Zone)
		{
			KingdomPhysicalLookupState state = FindPortfolioObject(Line.ObjectId,
				out GameObject item, out bool graveyard);
			if ((state == KingdomPhysicalLookupState.Absent || graveyard) && Line.After == 0)
				return PurposeDebitObservation.After;
			if (state != KingdomPhysicalLookupState.Exact || graveyard
				|| !GameObject.Validate(item)) return PurposeDebitObservation.Invalid;
			int amount;
			if (Line.Kind == KingdomPurposeDebitKind.Water)
			{
				LiquidVolume liquid = item.GetPart<LiquidVolume>();
				if (item.ID != Line.ContainerId || item.CurrentZone != Zone
					|| item.GetIntProperty("KingdomStores") != 1 || liquid == null
					|| liquid.MaxVolume != Line.Capacity || !KingdomLiquids.HasFreshWater(liquid))
					return PurposeDebitObservation.Invalid;
				amount = liquid.Volume;
			}
			else
			{
				if (item.Blueprint != Line.Blueprint || item.InInventory == null
					|| item.InInventory.ID != Line.ContainerId
					|| item.InInventory.CurrentZone != Zone) return PurposeDebitObservation.Invalid;
				if (Line.Kind == KingdomPurposeDebitKind.Material
					&& (!KingdomMaterials.TryOrdinaryMaterialOf(item, out KingdomMaterial material)
						|| (int)material != Line.TypeIndex)) return PurposeDebitObservation.Invalid;
				if (Line.Kind == KingdomPurposeDebitKind.Food && !item.HasPart("Food")
					&& !item.HasPart("PreparedCookingIngredient"))
					return PurposeDebitObservation.Invalid;
				amount = item.Count;
			}
			return amount == Line.Before ? PurposeDebitObservation.Before
				: amount == Line.After ? PurposeDebitObservation.After
				: PurposeDebitObservation.Invalid;
		}

		private static bool ApplyDebitLine(KingdomPurposeDebitLine Line, Zone Zone,
			out string Failure)
		{
			Failure = null;
			if (ObserveDebitLine(Line, Zone) != PurposeDebitObservation.Before
				|| FindPortfolioObject(Line.ObjectId, out GameObject item, out bool graveyard)
					!= KingdomPhysicalLookupState.Exact || graveyard)
				return Fail("The next exact local-debit object changed before callback.", out Failure);
			if (!KingdomConstructionInputLeaseAuthority.TryObjectAvailableForLocalDebit(
				item, out Failure)) return false;
			try
			{
				int amount = Line.Before - Line.After;
				if (Line.Kind == KingdomPurposeDebitKind.Water)
				{
					if (KingdomLiquids.Drain(item.GetPart<LiquidVolume>(), amount) != amount)
						return Fail("The exact purpose water callback removed a different amount.",
							out Failure);
					KingdomSurvey.ObserveChangedInActive(Zone, item);
				}
				else
				{
					GameObject container = item.InInventory;
					for (int i = 0; i < amount && GameObject.Validate(item); i++)
						item.Destroy(null, Silent: true);
					KingdomSurvey.ObserveChangedInActive(Zone, container);
				}
			}
			catch (Exception ex)
			{
				return Fail("The exact local-debit callback threw: " + ex.Message, out Failure);
			}
			return ObserveDebitLine(Line, Zone) == PurposeDebitObservation.After
				|| Fail("The exact local-debit callback reached an ambiguous aftermath.", out Failure);
		}
	}
}
