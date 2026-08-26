using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{
		private static void RemoveStrikePlotPart(Zone Z, KingdomStrikeIntent Intent,
			GameObject Part, ref KingdomConstructionJob Job)
		{
			KingdomStrikeTarget frozen = Intent != null && Intent.Targets != null
				&& Job.PhysicalIndex >= 0 && Job.PhysicalIndex < Intent.Targets.Count
				? Intent.Targets[Job.PhysicalIndex] : null;
			bool networkStrike = KingdomGatehouseRules.IsNetworkStrike(Intent.BuildKey,
				Intent.HasPlot, Intent.X1, Intent.Y1, Intent.X2, Intent.Y2,
				Intent.PlotId, Intent.Targets.Count);
			bool owned = networkStrike
				? KingdomGatehouse.IsOwnedSatellite(Part, Intent.PlotId)
				: GameObject.Validate(Part)
					&& Part.GetIntProperty(KingdomPlots.PlotPartProperty) == 1
					&& Part.GetStringProperty(KingdomPlots.PlotIdProperty) == Intent.PlotId;
			if (frozen == null || !GameObject.Validate(Part) || Part.ID != frozen.Id
				|| Part.Blueprint != frozen.Blueprint
				|| Part.CurrentCell != Z.GetCell(frozen.X, frozen.Y)
				|| Part.CurrentZone != Z || Part.ID == Job.SourceId
				|| !owned
				|| !ReferenceEquals(ExactObject(Part.ID), Part))
			{
				QuarantineStrike(Job, "A plot part changed before exact removal intent published.");
				return;
			}
			string id = Part.ID;
			if (!KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.PlotPartRemovalPending, Job.PhysicalIndex, 0,
				Job.PhysicalSpilled, id, null, Job.PhysicalReceipt)) return;
			bool removed;
			try { removed = Part.Obliterate(null, Silent: true); }
			catch (Exception ex)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, Part);
				QuarantineStrike(Job, "Plot-part removal threw: " + ex.Message);
				return;
			}
			if (removed || !GameObject.Validate(Part))
				KingdomSurvey.ObserveRemovedFromActive(Z, Part);
			if (!removed || GameObject.Validate(Part) || ExactObject(id) != null)
			{
				QuarantineStrike(Job, "Plot-part removal was vetoed, moved, or replaced.");
				return;
			}
			KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.StrikeWorkComplete,
				Job.PhysicalIndex + 1, 0, Job.PhysicalSpilled, null, null, Job.PhysicalReceipt);
		}

		private static void RemoveStrikePredecessor(Zone Z, GameObject Building,
			ref KingdomConstructionJob Job)
		{
			GameObject source = GameObject.Validate(Building) ? Building : ExactObject(Job.SourceId);
			Cell expected = Z.GetCell(Job.X, Job.Y);
			if (!GameObject.Validate(source) || source.ID != Job.SourceId
				|| !ReferenceEquals(ExactObject(Job.SourceId), source)
				|| source.CurrentZone != Z || source.CurrentCell != expected
				|| source.GetIntProperty("KingdomBuilt") != 1)
			{
				QuarantineStrike(Job, "The exact strike predecessor changed before removal.");
				return;
			}
			if (!KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.PredecessorRemovalPending, 0, 0, 0, null, null,
				Job.PhysicalReceipt)) return;
			bool removed;
			try { removed = source.Obliterate(null, Silent: true); }
			catch (Exception ex)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, source);
				QuarantineStrike(Job, "Strike predecessor removal threw: " + ex.Message);
				return;
			}
			if (removed || !GameObject.Validate(source))
				KingdomSurvey.ObserveRemovedFromActive(Z, source);
			if (!removed || GameObject.Validate(source) || ExactObject(Job.SourceId) != null)
			{
				QuarantineStrike(Job, "Strike predecessor removal was vetoed, moved, or replaced.");
				return;
			}
			KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.PredecessorRemoved, 0, 0, 0, null, null,
				Job.PhysicalReceipt);
		}

		private static bool ContinueStrikeSalvage(Zone Z, KingdomStrikeIntent Intent,
			ref KingdomConstructionJob Job)
		{
			if (!KingdomMaterialDebitCost.TryParseClaim(Intent.SalvageClaim,
				out KingdomMaterialDebitCost salvage) || !salvage.Bits.IsEmpty()
				|| !salvage.Exotics.IsEmpty())
			{
				QuarantineStrike(Job, "The frozen strike salvage claim cannot be read.");
				return false;
			}
			if (Job.PhysicalPhase == KingdomPhysicalPhase.SalvageAddPending)
			{
				GameObject pending = ExactObject(Job.PhysicalItemId);
				if (!ExactSalvageDestination(Z, pending, Job))
				{
					QuarantineStrike(Job, "Pending strike salvage is missing, replaced, merged, or moved.");
					return false;
				}
				int next = Job.PhysicalIndex + Job.PhysicalAmount;
				if (!KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.PredecessorRemoved, next, 0,
					Job.PhysicalSpilled + (Job.PhysicalDestinationId == null
						? Job.PhysicalAmount : 0), null, null, Job.PhysicalReceipt)) return false;
			}
			if (!TryMaterialAtOrdinal(salvage.Materials, Job.PhysicalIndex,
				out KingdomMaterial material, out int remaining))
			{
				return KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.SalvageSettled, Job.PhysicalIndex, 0,
					Job.PhysicalSpilled, null, null, Job.PhysicalReceipt);
			}
			string blueprint = BlueprintFor(material);
			GameObject item = string.IsNullOrEmpty(blueprint) ? null : GameObject.Create(blueprint);
			if (!GameObject.Validate(item))
			{
				QuarantineStrike(Job, "The exact strike salvage blueprint could not be created.");
				return false;
			}
			int amount = item.HasPart("Stacker") ? remaining : 1;
			item.Count = amount;
			item.SetStringProperty(StrikeSalvageReceiptProperty, Job.Id);
			GameObject destination = null;
			MaterialStock stock = Stock(Z);
			for (int i = 0; i < stock.Stockpiles.Count; i++)
			{
				GameObject candidate = stock.Stockpiles[i];
				if (GameObject.Validate(candidate) && candidate.CurrentZone == Z
					&& candidate.Inventory != null)
				{
					destination = candidate;
					break;
				}
			}
			string destinationId = destination == null ? null : destination.ID;
			if (!KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.SalvageAddPending, Job.PhysicalIndex, amount,
				Job.PhysicalSpilled, item.ID, destinationId, Job.PhysicalReceipt))
			{
				item.Obliterate(null, Silent: true);
				return false;
			}
			try
			{
				if (destination != null)
				{
					GameObject accepted = destination.Inventory.AddObject(item, null, Silent: true);
					KingdomSurvey.ObserveChangedInActive(Z, destination);
					KingdomSurvey.ObserveAddResultInActive(Z, item, accepted);
				}
				else
				{
					GameObject accepted = Z.GetCell(Job.X, Job.Y)?.AddObject(item);
					KingdomSurvey.ObserveAddResultInActive(Z, item, accepted);
				}
			}
			catch (Exception ex)
			{
				if (destination != null)
					KingdomSurvey.ObserveChangedInActive(Z, destination);
				else if (GameObject.Validate(item) && item.CurrentZone == Z)
					KingdomSurvey.ObserveChangedInActive(Z, item);
				QuarantineStrike(Job, "Strike salvage insertion threw: " + ex.Message);
				return false;
			}
			if (!KingdomConstruction.IsCurrent(Job)
				|| !ExactSalvageDestination(Z, item, Job))
			{
				QuarantineStrike(Job, "Strike salvage insertion was vetoed, merged, replaced, or moved.");
				return false;
			}
			int spilled = Job.PhysicalSpilled + (destination == null ? amount : 0);
			return KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.PredecessorRemoved, Job.PhysicalIndex + amount, 0,
				spilled, null, null, Job.PhysicalReceipt);
		}

		private static bool TryMaterialAtOrdinal(KingdomMaterialTally Tally, int Ordinal,
			out KingdomMaterial Material, out int Remaining)
		{
			Material = KingdomMaterial.Mud;
			Remaining = 0;
			if (Tally == null || Ordinal < 0) return false;
			int offset = Ordinal;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				int count = Tally.Get((KingdomMaterial)i);
				if (offset < count)
				{
					Material = (KingdomMaterial)i;
					Remaining = count - offset;
					return true;
				}
				offset -= count;
			}
			return false;
		}

		private static bool ExactSalvageDestination(Zone Z, GameObject Item,
			KingdomConstructionJob Job)
		{
			if (!GameObject.Validate(Item) || Item.ID != Job.PhysicalItemId
				|| Item.Count != Job.PhysicalAmount
				|| Item.GetStringProperty(StrikeSalvageReceiptProperty) != Job.Id) return false;
			if (Job.PhysicalDestinationId == null)
				return Item.Physics != null && Item.Physics.InInventory == null
					&& Item.CurrentCell == Z.GetCell(Job.X, Job.Y);
			GameObject destination = ExactObject(Job.PhysicalDestinationId);
			return GameObject.Validate(destination) && destination.CurrentZone == Z
				&& destination.Inventory != null && Item.Physics != null
				&& Item.Physics.InInventory == destination
				&& destination.Inventory.Objects.Contains(Item);
		}

		private static void SettleStrikeTellings(KingdomSystem System,
			KingdomStrikeIntent Intent, ref KingdomConstructionJob Job)
		{
			bool converted = Job.Route == KingdomConstructionRoute.SocketConvert;
			if (Job.Outbox == null)
			{
				string returned = null;
				if (KingdomMaterialDebitCost.TryParseClaim(Intent.SalvageClaim,
					out KingdomMaterialDebitCost salvage)) returned = salvage.Materials.Describe();
				string target = Intent.TargetDisplayName ?? Job.TargetKey ?? "the new work";
				string standing = KingdomPresentation.Rich(Intent.DisplayName);
				string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
				string newWork = KingdomPresentation.Rich(XRL.Language.Grammar.A(target));
				KingdomConstructionOutbox box = new KingdomConstructionOutbox
				{
					EventId = "construction:" + Job.Id + ":strike",
					Mode = converted ? 2 : 1,
					Chronicle = converted
						? "the " + standing + " of " + realm
							+ " came down, and " + newWork
							+ " rose on its ground"
						: "the " + standing + " of " + realm
							+ " was struck, and the crew stood in the gap where it had been",
					ChronicleState = KingdomConstructionSinkDisposition.Pending,
					LedgerState = KingdomConstructionSinkDisposition.Skipped,
					Message = converted
						? "{{W|The " + standing
							+ " comes down,}} {{G|and the ground is already rising into "
							+ newWork + ".}}"
						: "{{W|The " + standing + " comes down.}} "
							+ (returned == null ? "Nothing of it was worth keeping."
								: returned + " is carried to the stockpiles.")
							+ (Job.PhysicalSpilled > 0
								? " Some of it went on the ground for want of a stockpile." : ""),
					MessageState = KingdomConstructionSinkDisposition.Pending,
					Deed = converted ? null : "the striking of the " + standing
						+ " at " + realm,
					DeedState = converted ? KingdomConstructionSinkDisposition.Skipped
						: KingdomConstructionSinkDisposition.Pending
				};
				if (!KingdomConstruction.UpdateOutbox(ref Job, box)) return;
			}
			if (!KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.TellingsPending, Job.PhysicalIndex, 0,
				Job.PhysicalSpilled, null, null, Job.PhysicalReceipt)) return;
			if (!converted && !KingdomConstructionRules.IsTerminal(Job.Phase)
				&& !KingdomConstruction.Complete(ref Job)) return;
			if (!KingdomCeremony.DispatchPending(System, ref Job)) return;
			KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.Settled,
				Job.PhysicalIndex, 0, Job.PhysicalSpilled, null, null, Job.PhysicalReceipt);
			KingdomLog.Log("materials: struck " + Intent.DisplayName + " salvage="
				+ Job.PhysicalIndex + " spilled=" + Job.PhysicalSpilled);
		}

	}
}
