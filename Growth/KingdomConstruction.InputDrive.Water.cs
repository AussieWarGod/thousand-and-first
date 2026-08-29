using System;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool DriveInputWater(KingdomSystem system, Zone zone,
			GameObject carrier, ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, out string failure)
		{
			failure = null;
			GameObject vesselObject;
			FindExactId(zone, source.SourceObjectId, out vesselObject);
			LiquidVolume sourceLiquid = vesselObject?.GetPart<LiquidVolume>();
			if (!ExactRoutedInputWaterSource(zone, source, vesselObject, sourceLiquid, -1))
			{
				failure = "The frozen water vessel changed or left its dedicated source cell.";
				return false;
			}
			if (cargo.Phase == KingdomConstructionInputCargoPhase.Planned)
				return InputCargoPhase(ref job, receipt, cargo.Ordinal,
					KingdomConstructionInputCargoPhase.CreateIntent, out failure);

			GameObject cask;
			int matches = FindCarrierMarker(carrier, cargo.CreationMarker, out cask);
			if (cargo.Phase == KingdomConstructionInputCargoPhase.CreateIntent)
			{
				if (matches == 0)
				{
					// No object or property mutation may precede a fresh proof of the
					// source prestate named by the durable create intent.
					if (!ExactRoutedInputWaterSource(zone, source, vesselObject,
						sourceLiquid, source.Before))
					{
						failure = "The frozen water prestate changed before cask creation.";
						return false;
					}
					if (!KingdomMaster.NewWorkAllowed(system)
						|| !ExactSourcePickupManifest(zone, carrier, job, receipt, cargo,
							source)) return false;
					try
					{
						cask = GameObject.Create(cargo.Blueprint);
						if (GameObject.Validate(cask))
						{
							cask.SetStringProperty(InputMarkerProperty, cargo.CreationMarker);
							cask.SetIntProperty("NeverStack", 1);
							cask.SetIntProperty(Simulation.City.KingdomPorters.StockProperty, 1);
							GameObject accepted = GameObject.Validate(carrier)
								&& carrier.Inventory != null && carrier.CurrentZone == zone
								&& ExactNewRoutedInputCask(cask, job, receipt, cargo)
								&& ExactRoutedInputWaterSource(zone, source, vesselObject, sourceLiquid, -1)
								? carrier.Inventory.AddObject(cask, null, Silent: true, NoStack: true)
								: null;
							if (!ReferenceEquals(accepted, cask)) cask = null;
						}
					}
					catch { cask = null; }
					KingdomSurvey.ObserveChangedInActive(zone, carrier);
					matches = FindCarrierMarker(carrier, cargo.CreationMarker, out cask);
					if (!ExactSourcePickupManifest(zone, carrier, job, receipt, cargo,
						source)) return false;
				}
				if (matches != 1 || !ExactRoutedInputCask(carrier, cask, job, receipt,
					cargo, 0))
				{
					failure = "The exact routed water cask could not be uniquely adopted.";
					return false;
				}
				if (string.IsNullOrEmpty(cargo.ObjectId))
					return InputCargoEvidence(ref job, receipt, cargo.Ordinal, cask.IDIfAssigned,
						cargo.CustodyTopology, cargo.CustodyOwnerId, cargo.CustodyZoneId,
						cargo.CustodyX, cargo.CustodyY, cargo.BeforeWitnessHash,
						cargo.AfterWitnessHash, cargo.Spent, cargo.Lost, out failure);
				if (cargo.ObjectId != cask.IDIfAssigned)
				{
					failure = "The routed water cask identity changed after adoption.";
					return false;
				}
				if (cargo.CustodyTopology != KingdomConstructionInputTopology.CarrierInventory)
					return InputCargoEvidence(ref job, receipt, cargo.Ordinal, cargo.ObjectId,
						KingdomConstructionInputTopology.CarrierInventory, carrier.IDIfAssigned,
						source.SourceZoneId, source.X, source.Y, cargo.BeforeWitnessHash,
						cargo.AfterWitnessHash, cargo.Spent, cargo.Lost, out failure);
				return InputCargoPhase(ref job, receipt, cargo.Ordinal,
					KingdomConstructionInputCargoPhase.AtSource, out failure);
			}

			if (matches != 1 || cask?.IDIfAssigned != cargo.ObjectId
				|| !ExactRoutedInputCask(carrier, cask, job, receipt, cargo, -1))
			{
				failure = "The adopted water cask is absent or ambiguous at source.";
				return false;
			}
			if (source.Phase == KingdomConstructionInputSourcePhase.Reserved)
				return InputSourcePhase(ref job, receipt, source.Ordinal,
					KingdomConstructionInputSourcePhase.TransferIntent, out failure);
			if (cargo.Phase == KingdomConstructionInputCargoPhase.AtSource)
				return InputCargoPhase(ref job, receipt, cargo.Ordinal,
					KingdomConstructionInputCargoPhase.PickupIntent, out failure);
			if (source.Phase != KingdomConstructionInputSourcePhase.TransferIntent
				|| cargo.Phase != KingdomConstructionInputCargoPhase.PickupIntent)
			{
				failure = "The routed water transfer phases disagree.";
				return false;
			}
			return DriveInputWaterPour(system, zone, carrier, vesselObject,
				sourceLiquid, cask, ref job, receipt, source, cargo, out failure);
		}

		private static bool DriveInputWaterPour(KingdomSystem system, Zone zone,
			GameObject carrier, GameObject vesselObject, LiquidVolume sourceLiquid,
			GameObject cask, ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, out string failure)
		{
			failure = null;
			string before = InputWitness("water", source.LineId, source.SourceObjectId,
				cargo.ObjectId, source.Before, 0, 1, 0);
			string after = InputWitness("water", source.LineId, source.SourceObjectId,
				cargo.ObjectId, source.ResidualAfter, source.Take,
				source.ResidualAfter > 0 ? 1 : 0, 1);
			if (string.IsNullOrEmpty(source.BeforeWitnessHash))
				return InputSourceEvidence(ref job, receipt, source.Ordinal,
					source.RemainderObjectId, before, source.AfterWitnessHash,
					source.ProvedLost, out failure);
			if (string.IsNullOrEmpty(source.AfterWitnessHash))
				return InputSourceEvidence(ref job, receipt, source.Ordinal,
					source.RemainderObjectId, source.BeforeWitnessHash, after,
					source.ProvedLost, out failure);

			string observed = ObserveWater(source, cargo, sourceLiquid,
				cask.GetPart<LiquidVolume>());
			KingdomConstructionInputDecision decision =
				KingdomConstructionInputRules.DecidePhysicalMutation(before, after,
					observed, receipt.Paused);
			if (decision == KingdomConstructionInputDecision.WaitPaused) return false;
			if (decision == KingdomConstructionInputDecision.Apply)
			{
				if (!KingdomMaster.NewWorkAllowed(system)) return false;
				if (!ExactSourcePickupManifest(zone, carrier, job, receipt, cargo, source)
					|| !ExactRoutedInputWaterSource(zone, source, vesselObject, sourceLiquid,
						source.Before)
					|| !ExactRoutedInputCask(carrier, cask, job, receipt, cargo, 0))
				{
					failure = "Water source or route cask changed immediately before pouring.";
					return false;
				}
				LiquidVolume target = cask.GetPart<LiquidVolume>();
				try
				{
					if (target != null) target.MixWith(sourceLiquid,
						PouredFrom: vesselObject, Amount: source.Take,
						UseTempSplit: true);
				}
				catch { }
				KingdomSurvey.ObserveChangedInActive(zone, vesselObject);
				KingdomSurvey.ObserveChangedInActive(zone, carrier);
				if (!ExactRoutedInputWaterSource(zone, source, vesselObject, sourceLiquid,
						source.ResidualAfter)
					|| !ExactRoutedInputCask(carrier, cask, job, receipt, cargo, source.Take)
					|| !ExactSourcePickupManifest(zone, carrier, job, receipt, cargo, source))
				{
					failure = "Water callback changed protected evidence or exact route custody.";
					return false;
				}
				observed = ObserveWater(source, cargo, sourceLiquid, target);
			}
			if (observed != after)
			{
				failure = "The exact water transfer has an ambiguous measured aftermath.";
				return false;
			}
			return InputSourcePhase(ref job, receipt, source.Ordinal,
				KingdomConstructionInputSourcePhase.Debited, out failure);
		}

		private static string ObserveWater(KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, LiquidVolume from, LiquidVolume to)
		{
			int fromPure = KingdomLiquids.HasFreshWater(from) ? 1 : 0;
			int toPure = KingdomLiquids.HasFreshWater(to) ? 1 : 0;
			return InputWitness("water", source.LineId, source.SourceObjectId,
				cargo.ObjectId, from == null ? -1 : from.Volume, to == null ? -1 : to.Volume,
				fromPure, toPure);
		}

		private static bool ValidInputCask(GameObject carrier, GameObject cask,
			KingdomConstructionInputCargoLine cargo, int expectedVolume)
		{
			LiquidVolume liquid = cask == null ? null : cask.GetPart<LiquidVolume>();
			return GameObject.Validate(carrier) && carrier.Inventory != null
				&& GameObject.Validate(cask) && ReferenceEquals(cask.InInventory, carrier)
				&& cask.Blueprint == cargo.Blueprint && liquid != null && !liquid.Sealed
				&& liquid.MaxVolume == cargo.Capacity
				&& (expectedVolume < 0 || liquid.Volume == expectedVolume)
				&& (liquid.Volume == 0 || KingdomLiquids.HasFreshWater(liquid));
		}

		private static bool ExactRoutedInputCask(GameObject carrier, GameObject cask,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo, int expectedVolume)
		{
			return ValidInputCask(carrier, cask, cargo, expectedVolume)
				&& (string.IsNullOrEmpty(cargo.ObjectId)
					|| cask.IDIfAssigned == cargo.ObjectId)
				&& cask.HasStringProperty(InputMarkerProperty)
				&& !cask.HasIntProperty(InputMarkerProperty)
				&& cask.GetStringProperty(InputMarkerProperty) == cargo.CreationMarker
				&& cask.GetIntProperty("NeverStack") == 1
				&& cask.GetIntProperty(Simulation.City.KingdomPorters.StockProperty) == 1
				&& ReferenceCount(carrier.Inventory.Objects, cask) == 1
				&& RoutedInputItemAuthorized(job, receipt, cask);
		}

		private static bool ExactNewRoutedInputCask(GameObject cask,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo)
		{
			LiquidVolume liquid = cask?.GetPart<LiquidVolume>();
			return GameObject.Validate(cask) && cask.InInventory == null
				&& cask.CurrentCell == null && cask.Blueprint == cargo.Blueprint
				&& liquid != null && !liquid.Sealed && liquid.MaxVolume == cargo.Capacity
				&& liquid.Volume == 0 && cask.HasStringProperty(InputMarkerProperty)
				&& !cask.HasIntProperty(InputMarkerProperty)
				&& cask.GetStringProperty(InputMarkerProperty) == cargo.CreationMarker
				&& cask.GetIntProperty("NeverStack") == 1
				&& cask.GetIntProperty(Simulation.City.KingdomPorters.StockProperty) == 1
				&& RoutedInputItemAuthorized(job, receipt, cask);
		}

		private static bool ExactRoutedInputWaterSource(Zone zone,
			KingdomConstructionInputSourceLine source, GameObject vessel, LiquidVolume liquid,
			int expectedVolume)
		{
			return zone != null && source != null && GameObject.Validate(vessel)
				&& FindExactId(zone, source.SourceObjectId, out GameObject exact)
					== KingdomPhysicalLookupState.Exact && ReferenceEquals(exact, vessel)
				&& vessel.IDIfAssigned == source.HolderId && vessel.CurrentZone == zone
				&& vessel.CurrentCell == zone.GetCell(source.X, source.Y)
				&& vessel.GetIntProperty("KingdomStores") == 1
				&& !KingdomPurpose.HasProtectedCargoEvidence(vessel)
				&& !vessel.HasStringProperty(InputMarkerProperty)
				&& !vessel.HasIntProperty(InputMarkerProperty)
				&& ReferenceEquals(vessel.GetPart<LiquidVolume>(), liquid)
				&& liquid != null && !liquid.Sealed
				&& (expectedVolume < 0 || liquid.Volume == expectedVolume)
				&& (liquid.Volume == 0 || KingdomLiquids.HasFreshWater(liquid))
				&& ExactInputDedication(zone, source, vessel, vessel);
		}

		private static int FindCarrierMarker(GameObject carrier, string marker,
			out GameObject exact)
		{
			exact = null;
			int count = 0;
			for (int i = 0; carrier != null && carrier.Inventory != null
				&& i < carrier.Inventory.Objects.Count; i++)
			{
				GameObject item = carrier.Inventory.Objects[i];
				if (!GameObject.Validate(item)
					|| item.GetStringProperty(InputMarkerProperty) != marker) continue;
				count++;
				if (count == 1) exact = item;
			}
			if (count != 1) exact = null;
			return count;
		}
	}
}
