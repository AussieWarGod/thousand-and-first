using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		private static bool ResumeClearPayout(r_KingdomPlotWorks Works, Zone Z)
		{
			int phase = ClearInt(Works, ClearPhaseProperty);
			string sourceId = ClearString(Works, ClearIdProperty);
			int materialCode = ClearInt(Works, ClearMaterialProperty);
			int amount = ClearInt(Works, ClearAmountProperty);
			if (phase == 1)
			{
				// A save taken after intent but before Destroy is replayable because the exact source
				// still stands on the frozen cell. Absence without our callback-success tombstone is
				// ambiguous across engine callbacks and is never guessed into a second removal.
				if (ClearInt(Works, ClearRemovedProperty) != 1)
				{
					GameObject exact;
					KingdomPhysicalLookupState source = KingdomConstruction.FindExactId(Z,
						sourceId, out exact);
					KingdomPlotRules.Material material = (KingdomPlotRules.Material)materialCode;
					Cell cell = Z?.GetCell(ClearInt(Works, ClearXProperty),
						ClearInt(Works, ClearYProperty));
					if (source != KingdomPhysicalLookupState.Exact || cell == null
						|| !ExactClearSource(Works, Z, exact, cell, material, amount))
						return QuarantineClear(Works,
							"Interrupted clearance cannot prove its exact still-standing source.");
					bool removed;
					try { removed = exact.Destroy(null, Silent: true); }
					catch (System.Exception ex)
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(Z, exact);
						return QuarantineClear(Works,
							"Resumed clearance removal threw: " + ex.Message);
					}
					if (!removed || GameObject.Validate(exact)
						|| GameObject.Validate(GameObject.FindByID(sourceId)))
						return QuarantineClear(Works,
							"Resumed clearance removal was vetoed, moved, or replaced its source.");
					KingdomSurvey.ObserveRemovedFromActive(Z, exact);
					ClearInt(Works, ClearRemovedProperty, 1);
				}
				if (GameObject.Validate(GameObject.FindByID(sourceId)))
					return QuarantineClear(Works,
						"Removed clearance source reappeared before payout.");
				phase = 2;
				ClearInt(Works, ClearPhaseProperty, phase);
			}
			if (phase < 2 || phase > 6 || materialCode < 1 || materialCode > 4
				|| amount <= 0 || ClearInt(Works, ClearRemovedProperty) != 1)
				return QuarantineClear(Works, "Clearance receipt is malformed or ambiguous.");
			if (GameObject.Validate(GameObject.FindByID(sourceId)))
				return QuarantineClear(Works,
					"Clearance source reappeared before its economic receipts settled.");
			try
			{
				KingdomPlotRules.Material material =
					(KingdomPlotRules.Material)materialCode;
				if (phase == 2)
				{
					if (!PrepareClearOutput(Works, Z, material, amount)) return false;
					phase = ClearInt(Works, ClearPhaseProperty);
				}
				if (phase == 3)
				{
					if (!PlaceOrProveClearOutput(Works, Z, material, amount)) return false;
					phase = 4;
					ClearInt(Works, ClearPhaseProperty, phase);
				}
				if (phase == 4)
				{
					if (!ExactClearOutput(Works, Z, material, amount, out _))
						return QuarantineClear(Works,
							"Clearance payout changed before its settlement tally was prepared.");
					int tallyBefore = ClearTally(Works, material);
					if (!KingdomConstructionRules.TryCounterAfter(tallyBefore, amount,
						out int tallyAfter))
						return QuarantineClear(Works, "Clearance settlement tally would overflow.");
					ClearInt(Works, ClearTallyBeforeProperty, tallyBefore);
					ClearInt(Works, ClearTallyAfterProperty, tallyAfter);
					phase = 5;
					ClearInt(Works, ClearPhaseProperty, phase);
				}
				if (phase == 5)
				{
					if (!ExactClearOutput(Works, Z, material, amount, out _))
						return QuarantineClear(Works,
							"Clearance payout changed before its settlement tally committed.");
					KingdomConstructionCasAction action = KingdomConstructionRules.CounterCasAction(
						ClearTally(Works, material), ClearInt(Works, ClearTallyBeforeProperty),
						ClearInt(Works, ClearTallyAfterProperty));
					if (action == KingdomConstructionCasAction.Quarantine)
						return QuarantineClear(Works, "Clearance settlement tally has a third value.");
					if (action == KingdomConstructionCasAction.Apply)
						SetClearTally(Works, material, ClearInt(Works, ClearTallyAfterProperty));
					if (ClearTally(Works, material) != ClearInt(Works, ClearTallyAfterProperty))
						return QuarantineClear(Works, "Clearance settlement tally could not be proved.");
					phase = 6;
					ClearInt(Works, ClearPhaseProperty, phase);
				}
				if (phase == 6)
				{
					if (!ExactClearOutput(Works, Z, material, amount, out GameObject paid))
						return QuarantineClear(Works,
							"Clearance payout changed before its receipt could close.");
					paid.SetStringProperty(ClearOutputMark, null, RemoveIfNull: true);
					if (paid.GetStringProperty(ClearOutputMark) != null)
						return QuarantineClear(Works,
							"Clearance payout marker could not be retired.");
				}
			}
			catch (System.Exception ex)
			{
				return QuarantineClear(Works, "Clearance credit became ambiguous: " + ex.Message);
			}
			ClearInt(Works, ClearPhaseProperty, 0);
			ClearString(Works, ClearIdProperty, null);
			ClearString(Works, ClearBlueprintProperty, null);
			ClearInt(Works, ClearXProperty, 0);
			ClearInt(Works, ClearYProperty, 0);
			ClearInt(Works, ClearMaterialProperty, 0);
			ClearInt(Works, ClearAmountProperty, 0);
			ClearInt(Works, ClearRemovedProperty, 0);
			ClearString(Works, ClearOutputIdProperty, null);
			ClearString(Works, ClearOutputBlueprintProperty, null);
			ClearString(Works, ClearOutputMarkerProperty, null);
			ClearInt(Works, ClearDestinationKindProperty, 0);
			ClearString(Works, ClearDestinationIdProperty, null);
			ClearString(Works, ClearDestinationZoneProperty, null);
			ClearInt(Works, ClearDestinationXProperty, 0);
			ClearInt(Works, ClearDestinationYProperty, 0);
			ClearInt(Works, ClearTallyBeforeProperty, 0);
			ClearInt(Works, ClearTallyAfterProperty, 0);
			return true;
		}

		/// <summary>
		/// Freezes one real material output and its exact destination before the AddObject
		/// callback. Nothing is credited in an integer ledger: the receipt closes only around a
		/// takeable Qud object in a dedicated stockpile, or on the works cell when none exists.
		/// </summary>
		private static bool PrepareClearOutput(r_KingdomPlotWorks Works, Zone Z,
			KingdomPlotRules.Material Material, int Amount)
		{
			if (Works?.ParentObject == null || Z == null || Amount <= 0
				|| Works.ParentObject.CurrentZone != Z)
				return QuarantineClear(Works, "Clearance payout has no exact settlement ground.");
			KingdomMaterial stockMaterial = StockMaterial(Material);
			string blueprint = KingdomMaterials.BlueprintFor(stockMaterial);
			if (string.IsNullOrEmpty(blueprint))
				return QuarantineClear(Works, "Clearance payout has no physical material blueprint.");

			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			GameObject destination = null;
			for (int i = 0; i < stock.Stockpiles.Count; i++)
			{
				GameObject candidate = stock.Stockpiles[i];
				if (GameObject.Validate(candidate) && candidate.CurrentZone == Z
					&& candidate.Inventory != null
					&& candidate.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
				{
					destination = candidate;
					break;
				}
			}
			Cell fallback = Works.ParentObject.CurrentCell;
			if (destination == null && (fallback == null || fallback.ParentZone != Z))
				return QuarantineClear(Works,
					"Clearance payout has neither a stockpile nor a ground cell.");

			string marker = "plot-clear:" + Works.ParentObject.ID + ":"
				+ ClearString(Works, ClearIdProperty);
			if (marker.Length > 1024)
				return QuarantineClear(Works, "Clearance payout identity is too long.");
			ClearString(Works, ClearOutputBlueprintProperty, blueprint);
			ClearString(Works, ClearOutputMarkerProperty, marker);
			ClearString(Works, ClearDestinationZoneProperty, Z.ZoneID);
			if (destination != null)
			{
				ClearInt(Works, ClearDestinationKindProperty, 1);
				ClearString(Works, ClearDestinationIdProperty, destination.ID);
			}
			else
			{
				ClearInt(Works, ClearDestinationKindProperty, 2);
				ClearInt(Works, ClearDestinationXProperty, fallback.X);
				ClearInt(Works, ClearDestinationYProperty, fallback.Y);
			}
			if (CountClearOutputs(Z, marker) != 0)
				return QuarantineClear(Works,
					"Clearance payout marker already names a rooted object before creation.");

			GameObject item = GameObject.Create(blueprint);
			if (!GameObject.Validate(item) || string.IsNullOrEmpty(item.ID)
				|| item.Blueprint != blueprint || item.Physics == null || !item.Physics.Takeable)
				return QuarantineClear(Works, "Clearance material object could not be created exactly.");
			item.Count = Amount;
			item.SetStringProperty(ClearOutputMark, marker);
			ClearString(Works, ClearOutputIdProperty, item.ID);
			ClearInt(Works, ClearPhaseProperty, 3);
			return PlaceOrProveClearOutput(Works, Z, Material, Amount, item);
		}

		private static bool PlaceOrProveClearOutput(r_KingdomPlotWorks Works, Zone Z,
			KingdomPlotRules.Material Material, int Amount, GameObject Created = null)
		{
			if (ExactClearOutput(Works, Z, Material, Amount, out _)) return true;
			string outputId = ClearString(Works, ClearOutputIdProperty);
			string blueprint = ClearString(Works, ClearOutputBlueprintProperty);
			string marker = ClearString(Works, ClearOutputMarkerProperty);
			GameObject item = Created;
			if (!GameObject.Validate(item) || item.ID != outputId)
			{
				GameObject globally = GameObject.FindByID(outputId);
				if (GameObject.Validate(globally)) item = globally;
			}
			if (!GameObject.Validate(item))
			{
				// A detached create-intent is not serialized. Exact absence of both its ID and its
				// marker means no spendable object exists, so cold-load recovery may recreate it.
				if (CountClearOutputs(Z, marker) != 0)
					return QuarantineClear(Works,
						"Clearance output identity is absent but its marker is not.");
				item = GameObject.Create(blueprint);
				if (!GameObject.Validate(item) || string.IsNullOrEmpty(item.ID)
					|| item.Blueprint != blueprint || item.Physics == null || !item.Physics.Takeable)
					return QuarantineClear(Works,
						"Clearance output could not be recovered from a detached create-intent.");
				item.Count = Amount;
				item.SetStringProperty(ClearOutputMark, marker);
				ClearString(Works, ClearOutputIdProperty, item.ID);
				outputId = item.ID;
			}
			if (item.Blueprint != blueprint || item.Count != Amount
				|| item.GetStringProperty(ClearOutputMark) != marker
				|| item.InInventory != null || item.CurrentCell != null)
				return QuarantineClear(Works,
					"Clearance output changed before its exact AddObject callback.");

			GameObject accepted = null;
			int kind = ClearInt(Works, ClearDestinationKindProperty);
			if (kind == 1)
			{
				GameObject destination;
				if (KingdomConstruction.FindExactId(Z,
					ClearString(Works, ClearDestinationIdProperty), out destination)
					!= KingdomPhysicalLookupState.Exact || destination.Inventory == null
					|| destination.GetIntProperty(KingdomMaterials.StockpileProperty) != 1)
					return QuarantineClear(Works,
						"Clearance payout's exact stockpile disappeared before placement.");
				try { accepted = destination.Inventory.AddObject(item, null, Silent: true, NoStack: true); }
				finally
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Z, destination);
					KingdomSurvey.ObserveAddResultInActive(Z, item, accepted);
				}
			}
			else if (kind == 2)
			{
				Cell cell = ExactClearDestinationCell(Works, Z);
				if (cell == null)
					return QuarantineClear(Works,
						"Clearance payout's exact ground cell disappeared before placement.");
				try { accepted = cell.AddObject(item, NoStack: true, Silent: true); }
				finally { KingdomSurvey.ObserveAddResultInActive(Z, item, accepted); }
			}
			else return QuarantineClear(Works, "Clearance payout destination is malformed.");
			if (!ReferenceEquals(accepted, item)
				|| !ExactClearOutput(Works, Z, Material, Amount, out GameObject exact)
				|| !ReferenceEquals(exact, item))
				return QuarantineClear(Works,
					"Clearance AddObject callback did not leave one exact no-stack output.");
			return true;
		}

	}
}
