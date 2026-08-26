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

		// --- Striking -------------------------------------------------------------------------

		/// <summary>
		/// Orders a building the settlement raised taken down, or calls off an order already
		/// standing. Founder-ordered and nothing else: no system anywhere condemns a building on
		/// its own. Nothing comes down the moment this is called &mdash; crew works the order off
		/// over days, and the ceremony is at the end of it.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone the building stands in; must be the kingdom's own ground.</param>
		/// <param name="Building">The building to strike. Must be one the settlement built.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// marked when it does.</param>
		/// <returns>True once the order stands, or once it has been called off.</returns>
		public static bool OrderStrike(KingdomSystem System, Zone Z, GameObject Building,
			out string Failure, string GovernanceVerb = null)
		{
			KingdomConstructionJob ignored = null;
			return OrderStrikeDurable(System, Z, Building, null, true, true,
				GovernanceVerb, out ignored, out Failure);
		}

		/// <summary>Conversion entry: extends the already-funded exact job instead of creating one.</summary>
		internal static bool OrderStrikeForConstruction(KingdomSystem System, Zone Z,
			GameObject Building, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out string Failure)
		{
			return OrderStrikeDurable(System, Z, Building, Job, false, false, null,
				out Updated, out Failure);
		}

		private static bool OrderStrikeDurable(KingdomSystem System, Zone Z,
			GameObject Building, KingdomConstructionJob Supplied, bool AllowCancellation,
			bool Announce, string GovernanceVerb, out KingdomConstructionJob Updated,
			out string Failure)
		{
			Updated = Supplied;
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A building is struck on the kingdom's own ground, not in other people's streets.";
				return false;
			}
			if (Building == null || !GameObject.Validate(Building) || Building.CurrentZone == null || Building.CurrentZone.ZoneID != Z.ZoneID)
			{
				Failure = "There is nothing there to strike.";
				return false;
			}
			if (Building.GetIntProperty("KingdomBuilt") != 1)
			{
				Failure = "The settlement strikes what it raised. That is not one of its buildings.";
				return false;
			}
			KingdomConstructionJob carried = Supplied;
			if (carried == null)
			{
				string receiptId = Building.GetStringProperty(KingdomConstruction.ReceiptProperty);
				if (!string.IsNullOrEmpty(receiptId))
					KingdomConstruction.TryFind(receiptId, out carried);
			}
			if (Supplied == null && carried != null
				&& KingdomConstruction.CanSupersedeTerminalReceipt(System, Z, Building, carried))
			{
				// Keep immutable terminal proof in registry; only carried object pointer is superseded.
				carried = null;
			}
			bool activeStrike = carried != null && KingdomConstruction.Owns(System, Z, carried)
				&& !KingdomConstructionRules.IsTerminal(carried.Phase)
				&& (carried.Route == KingdomConstructionRoute.Strike
					|| carried.Route == KingdomConstructionRoute.SocketConvert)
				&& carried.SourceId == Building.ID;
			if (carried != null && !activeStrike)
			{
				Failure = "That building carries another construction receipt.";
				return false;
			}
			if (activeStrike && carried.PhysicalPhase != KingdomPhysicalPhase.None)
			{
				Updated = carried;
				if (carried.PhysicalPhase == KingdomPhysicalPhase.StrikeCancellationPending)
				{
					if (!FinishStrikeCancellation(Z, Building, ref carried))
					{
						Failure = carried.Failure;
						return false;
					}
					Updated = carried;
					return true;
				}
				if (!AllowCancellation || carried.Route == KingdomConstructionRoute.SocketConvert)
					return true;
				if (carried.PhysicalPhase != KingdomPhysicalPhase.StrikeWorking
					|| Building.GetIntProperty(StrikeEffortProperty) <= 0)
				{
					Failure = "That strike has crossed its physical boundary and cannot be called off.";
					return false;
				}
				if (!KingdomConstruction.UpdatePhysical(ref carried,
					KingdomPhysicalPhase.StrikeCancellationPending, carried.PhysicalIndex,
					carried.PhysicalAmount, carried.PhysicalSpilled, carried.PhysicalItemId,
					carried.PhysicalDestinationId, carried.PhysicalReceipt)
					|| !FinishStrikeCancellation(Z, Building, ref carried))
				{
					Failure = "The strike receipt could not be cancelled safely.";
					return false;
				}
				if (!string.IsNullOrEmpty(GovernanceVerb))
				{
					KingdomGovernanceScope.Commit(GovernanceVerb);
				}
				MessageQueue.AddPlayerMessage("{{K|The order to strike the " + Building.ShortDisplayName + " is called off.}} It stands exactly where it stood.");
				Updated = carried;
				return true;
			}
			bool architectureMarker = Building.HasIntProperty(
				KingdomArchitectureRuntime.SchemaProperty)
				|| Building.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty);
			if (architectureMarker)
			{
				KingdomArchitectureIntent authored;
				if (!KingdomArchitectureRuntime.TryRead(Building, out authored, out Failure))
					return false;
				if (KingdomArchitectureRules.IsCurrentSnapshotEncoding(authored.EncodedSnapshot)
					&& (!KingdomArchitectureStamper.TryPreflightStrike(Building, Z, out Failure)
						|| !KingdomDelveLink.TryPreflightStrike(Building, Z, out Failure))) return false;
			}
			string key = Building.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			KingdomMaterialTally cost;
			int drams;
			int paidSchema = Building.GetIntProperty(
				KingdomConstruction.PaidBuildSchemaProperty);
			if (paidSchema == 0)
			{
				// Explicit compatibility lane for a standing work raised before paid receipts.
				// New work never enters here; its own frozen bill is authoritative below.
				cost = CostFor(key);
				drams = KingdomData.TryGetBuilding(key, out var entry) ? entry.CostDrams : 0;
			}
			else if (paidSchema != KingdomConstruction.PaidBuildSchema
				|| !KingdomConstruction.TryReadPaidBuild(Building,
					out KingdomPaidBuildReceipt paid))
			{
				Failure = "That building's paid construction receipt cannot be read; it was left standing.";
				return false;
			}
			else
			{
				cost = paid.Material.Materials;
				drams = paid.Water;
			}
			int effort = KingdomMaterialRules.StrikeEffort(cost.Total(), drams);
			KingdomMaterialTally salvageTally = KingdomMaterialRules.StrikeSalvage(cost);
			if (salvageTally.Total() > 4096)
			{
				Failure = "The strike salvage exceeds the bounded physical receipt.";
				return false;
			}
			KingdomStrikeIntent intent = new KingdomStrikeIntent
			{
				DisplayName = Building.BaseDisplayNameStripped,
				BuildKey = key,
				TargetDisplayName = activeStrike
					&& carried.Route == KingdomConstructionRoute.SocketConvert
					&& KingdomData.TryGetBuilding(carried.TargetKey, out var targetEntry)
						? targetEntry.Name : null,
				SalvageClaim = new KingdomMaterialDebitCost(salvageTally).ToClaimString(),
				HasPlot = false, Effort = effort,
				Targets = new List<KingdomStrikeTarget>(),
				X1 = -1, Y1 = -1, X2 = -1, Y2 = -1
			};
			bool gatehouseMarker = Building.HasIntProperty(KingdomGatehouse.SchemaProperty)
				|| Building.HasStringProperty(KingdomGatehouse.SchemaProperty);
			if (gatehouseMarker)
			{
				// A gatehouse reserves a rectangle for overlap, but remains a typed network,
				// never a plot. Freeze its six exact owned stone/timber outputs while HasPlot
				// stays false; Socket therefore cannot mint a cleared-plot successor.
				if (!KingdomGatehouse.TryFreezeStrikeTargets(Building, Z,
					out KingdomGatehousePlan gatePlan,
					out List<KingdomStrikeTarget> gateTargets, out Failure)) return false;
				intent.X1 = gatePlan.X1;
				intent.Y1 = gatePlan.Y1;
				intent.X2 = gatePlan.X2;
				intent.Y2 = gatePlan.Y2;
				intent.PlotId = Building.ID; // bounded v2 field is the typed network owner ID
				intent.Targets = gateTargets;
			}
			else if (KingdomPlots.TryReadRect(Building, out KingdomPlotRules.PlotRect plotRect))
			{
				intent.HasPlot = true;
				intent.X1 = plotRect.X1;
				intent.Y1 = plotRect.Y1;
				intent.X2 = plotRect.X2;
				intent.Y2 = plotRect.Y2;
				intent.PlotId = Building.GetStringProperty(KingdomPlots.PlotIdProperty);
				HashSet<string> frozenIds = new HashSet<string>(StringComparer.Ordinal);
				for (int y = plotRect.Y1; y <= plotRect.Y2; y++)
				{
					for (int x = plotRect.X1; x <= plotRect.X2; x++)
					{
						Cell plotCell = Z.GetCell(x, y);
						if (plotCell == null) continue;
						foreach (GameObject part in plotCell.GetObjects())
						{
							if (!GameObject.Validate(part) || part.ID == Building.ID
								|| part.GetIntProperty(KingdomPlots.PlotPartProperty) != 1
								|| part.GetStringProperty(KingdomPlots.PlotIdProperty) != intent.PlotId)
								continue;
							if (!frozenIds.Add(part.ID)
								|| intent.Targets.Count >= KingdomConstructionRules.MaxStrikeTargets)
							{
								Failure = "The strike footprint exceeds or duplicates its exact target receipt.";
								return false;
							}
							intent.Targets.Add(new KingdomStrikeTarget
								{ Id = part.ID, Blueprint = part.Blueprint, X = x, Y = y });
						}
					}
				}
			}
			if (!KingdomConstructionRules.TryEncodeStrikeIntent(intent, out string physicalReceipt))
			{
				Failure = "The strike's exact physical receipt could not be frozen.";
				return false;
			}
			KingdomConstructionJob job = carried;
			if (job == null)
			{
				job = KingdomConstruction.NewJob(System, Z, KingdomConstructionRoute.Strike,
					Building.CurrentCell, Building, key, null, 0,
					new KingdomMaterialDebitCost());
				if (!KingdomConstruction.TryPublish(job, out Failure)) return false;
			}
			if (!KingdomConstruction.IsCurrent(job)
				|| !KingdomConstruction.UpdatePhysical(ref job,
					KingdomPhysicalPhase.StrikeOrdered, 0, 0, 0, null, null,
					physicalReceipt))
			{
				Failure = "The strike's exact physical intent could not be published.";
				return false;
			}
			if (!ResumeStrikeStamp(Z, Building, intent, ref job))
			{
				Updated = job;
				Failure = "The strike work phase could not be published.";
				return false;
			}
			if (!string.IsNullOrEmpty(GovernanceVerb))
			{
				KingdomGovernanceScope.Commit(GovernanceVerb);
			}
			if (Announce)
			{
				string salvage = salvageTally.Describe();
				KingdomChronicle.Record(System, "the " + Building.ShortDisplayName + " of " + KingdomPresentation.Rich(System.KingdomDisplayName) + " was condemned, and the crew set to taking it down");
				int days = KingdomMaterialRules.DaysForOneHand(effort);
				MessageQueue.AddPlayerMessage("{{W|The " + Building.ShortDisplayName + " is condemned.}} The crew will take it down over "
					+ days + ((days == 1) ? " day" : " days") + " of work for a single pair of hands"
					+ ((salvage == null) ? ", and there is nothing in it worth keeping" : (", and " + salvage + " comes back")) + ". No water is refunded.");
			}
			KingdomLog.Log("materials: strike ordered on " + Building.ShortDisplayName + " effort=" + effort);
			Updated = job;
			return true;
		}
	}
}
