using System;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool RecoverCancelledWater(KingdomSystem system,
			Zone zone, GameObject carrier, ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, out string failure)
		{
			failure = null;
			GameObject vessel;
			if (zone == null || zone.ZoneID != source.SourceZoneId
				|| KingdomSurvey.ActiveFor(zone) == null
				|| FindExactId(zone, source.SourceObjectId, out vessel)
					!= KingdomPhysicalLookupState.Exact)
			{
				failure = "Cancelled water source custody is absent or ambiguous.";
				return false;
			}
			if (!GameObject.Validate(carrier) || carrier.CurrentZone != zone
				|| carrier.CurrentCell == null || carrier.Inventory == null
				|| carrier.IDIfAssigned != cargo.CustodyOwnerId
					&& !string.IsNullOrEmpty(cargo.CustodyOwnerId))
			{
				failure = "Cancelled water carrier is not the receipt's active custody parent.";
				return false;
			}
			if (!ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
				carrier)) return false;
			LiquidVolume sourceLiquid = vessel.GetPart<LiquidVolume>();
			if (!ExactRoutedInputWaterSource(zone, source, vessel, sourceLiquid, -1))
			{
				failure = "The cancelled water source is no longer an open vessel.";
				return false;
			}

			GameObject cask = null;
			bool graveyard = false;
			bool provedPreCreate = false;
			if (string.IsNullOrEmpty(cargo.ObjectId))
			{
				int marked = FindActiveCancellationMarker(zone, cargo.CreationMarker, out cask);
				if (marked < 0 || marked > 1
					|| marked == 1 && (!ReferenceEquals(cask.InInventory, carrier)
						|| ReferenceCount(carrier.Inventory.Objects, cask) != 1
						|| !ExactCancelledWaterCask(job, receipt, cargo, carrier, cask,
							cask.GetPart<LiquidVolume>(), -1)))
				{
					failure = "The unpublished water cask is foreign or ambiguous.";
					return false;
				}
				if (marked == 1)
					return InputCargoEvidence(ref job, receipt, cargo.Ordinal, cask.IDIfAssigned,
						KingdomConstructionInputTopology.CarrierInventory, carrier.IDIfAssigned,
						source.SourceZoneId, source.X, source.Y, cargo.BeforeWitnessHash,
						cargo.AfterWitnessHash, cargo.Spent, cargo.Lost, out failure);
				// No marker is a legal pre-callback state only while the source still proves Before.
				if (sourceLiquid.Volume != source.Before)
				{
					failure = "Water changed without a durably adopted cancellation cask.";
					return false;
				}
				provedPreCreate = true;
			}
			KingdomPhysicalLookupState caskState = string.IsNullOrEmpty(cargo.ObjectId)
				? KingdomPhysicalLookupState.Absent
				: FindGlobalInputId(receipt, cargo.ObjectId, out cask, out graveyard);
			if (caskState == KingdomPhysicalLookupState.Ambiguous)
			{
				failure = "The cancelled water cask identity is ambiguous.";
				return false;
			}
			if (caskState == KingdomPhysicalLookupState.Exact && !graveyard)
			{
				LiquidVolume caskLiquid = cask.GetPart<LiquidVolume>();
				if (!ExactCancelledWaterCask(job, receipt, cargo, carrier, cask, caskLiquid, -1))
				{
					failure = "The cancelled water cask changed physical shape.";
					return false;
				}
				if (caskLiquid.Volume == source.Take
					&& sourceLiquid.Volume == source.ResidualAfter)
				{
					if (receipt.Paused || !KingdomMaster.NewWorkAllowed(system)) return false;
					if (!ExactRoutedInputWaterSource(zone, source, vessel, sourceLiquid,
							source.ResidualAfter)
						|| !ExactCancelledWaterCask(job, receipt, cargo, carrier, cask, caskLiquid,
							source.Take)
						|| !ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
							carrier))
					{
						failure = "Cancelled water custody changed immediately before reversal.";
						return false;
					}
					try { sourceLiquid.MixWith(caskLiquid, PouredFrom: cask,
						Amount: source.Take, UseTempSplit: true); } catch { }
					KingdomSurvey.ObserveChangedInActive(zone, vessel);
					KingdomSurvey.ObserveCurrentTopologyInActive(cask.CurrentZone, cask);
					if (!ExactRoutedInputWaterSource(zone, source, vessel, sourceLiquid,
							source.Before)
						|| !ExactCancelledWaterCask(job, receipt, cargo, carrier, cask, caskLiquid, 0)
						|| !ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
							carrier))
					{
						failure = "Reverse water callback changed protected evidence or custody.";
						return false;
					}
				}
				if (sourceLiquid.Volume != source.Before || caskLiquid.Volume != 0)
				{
					failure = "The reverse water transfer has an ambiguous measured aftermath.";
					return false;
				}
				if (!KingdomMaster.NewWorkAllowed(system)) return false;
				if (!ExactCancelledWaterCask(job, receipt, cargo, carrier, cask, caskLiquid, 0)
					|| !ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
						carrier))
				{
					failure = "Cancelled water cask changed immediately before exact cleanup.";
					return false;
				}
				Zone caskZone = cask.CurrentZone;
				bool removed = false;
				try { removed = cask.Obliterate(null, Silent: true); } catch { }
				KingdomSurvey.ObserveCurrentTopologyInActive(caskZone, cask);
				caskState = FindGlobalInputId(receipt, cargo.ObjectId, out cask, out graveyard);
				if (!removed && !graveyard)
					return false;
				if (!ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
					carrier)) return false;
			}
			if (sourceLiquid.Volume != source.Before
				|| !provedPreCreate
					&& (caskState != KingdomPhysicalLookupState.Exact || !graveyard))
			{
				failure = "The cancelled water line cannot prove full return and cask release.";
				return false;
			}
			return CloseCancelledLine(system, zone, ref job, receipt, source, cargo,
				"returned-water:" + job.Id, source.SourceZoneId, source.X, source.Y,
				out failure);
		}

		private static bool ExactCancelledWaterCask(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputCargoLine cargo,
			GameObject carrier, GameObject cask, LiquidVolume liquid, int expectedVolume)
		{
			return GameObject.Validate(carrier) && carrier.Inventory != null
				&& carrier.CurrentCell != null && GameObject.Validate(cask)
				&& (string.IsNullOrEmpty(cargo.ObjectId) || cask.IDIfAssigned == cargo.ObjectId)
				&& ReferenceEquals(cask.InInventory, carrier)
				&& ReferenceCount(carrier.Inventory.Objects, cask) == 1
				&& cask.Blueprint == cargo.Blueprint
				&& cask.HasStringProperty(InputMarkerProperty)
				&& !cask.HasIntProperty(InputMarkerProperty)
				&& cask.GetStringProperty(InputMarkerProperty) == cargo.CreationMarker
				&& cask.GetIntProperty("NeverStack") == 1
				&& cask.GetIntProperty(Simulation.City.KingdomPorters.StockProperty) == 1
				&& ReferenceEquals(cask.GetPart<LiquidVolume>(), liquid)
				&& liquid != null && !liquid.Sealed && liquid.MaxVolume == cargo.Capacity
				&& (expectedVolume < 0 || liquid.Volume == expectedVolume)
				&& (liquid.Volume == 0 || KingdomLiquids.HasFreshWater(liquid))
				&& RoutedInputItemAuthorized(job, receipt, cask);
		}

		private static bool ExactGraveyardCancelledWaterCask(KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputCargoLine cargo,
			GameObject cask)
		{
			LiquidVolume liquid = cask?.GetPart<LiquidVolume>();
			return cask != null && cask.IsInGraveyard()
				&& cask.IDIfAssigned == cargo.ObjectId && cask.Blueprint == cargo.Blueprint
				&& cask.HasStringProperty(InputMarkerProperty)
				&& !cask.HasIntProperty(InputMarkerProperty)
				&& cask.GetStringProperty(InputMarkerProperty) == cargo.CreationMarker
				&& cask.GetIntProperty("NeverStack") == 1
				&& cask.GetIntProperty(Simulation.City.KingdomPorters.StockProperty) == 1
				&& liquid != null && !liquid.Sealed && liquid.MaxVolume == cargo.Capacity
				&& liquid.Volume == 0
				&& KingdomOrdinaryCustody.TryProveRetiredEmpty(cask, out string _)
				&& RetiredRoutedInputItemAuthorized(job, receipt, cask);
		}
	}
}
