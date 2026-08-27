using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Deposits arrived scalar stops through exact marked target receipts. Earlier
		/// stops must already be proved; a later destination cannot leapfrog the frozen itinerary.</summary>
		internal static int SettleScalarArrivals(KingdomSystem system, Zone zone,
			KingdomSurvey survey, long now, string cropBlueprint)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (system == null || zone == null || survey == null || system.Jobs == null
				|| !system.Jobs.TryRead(out table, out fault)) return 0;
			int landedRows = 0;
			int[] ids = table.OpenIds();
			for (int n = 0; n < ids.Length; n++)
			{
				KingdomJobRow row;
				if (!table.TryGet(ids[n], out row) || row.DeliveryPhase != KingdomDeliveryPhase.InFlight
					|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.ScalarStock
					|| row.CargoAmount <= 0
					|| !string.Equals(row.DestZoneId, zone.ZoneID, StringComparison.Ordinal)
					|| !PriorStopsLanded(table, row)) continue;
				KingdomItineraryFix fix;
				if (!KingdomItineraryRules.TryAt(row.Legs(), row.LegCount, now, out fix, out fault)
					|| fix.Phase != KingdomItineraryPhase.Delivered) continue;
				GameObject target;
				LiquidVolume water;
				long observed;
				if (!TryExactScalarTarget(survey, row, out target, out water, out observed)) continue;
				string receipt = Receipt(row);
				string standing = target.GetStringProperty(TargetReceiptProperty);
				if (row.DeliveryTargetReceiptState == KingdomDeliveryTargetReceiptState.None)
				{
					if (!string.IsNullOrEmpty(standing) && !string.Equals(standing, receipt,
						StringComparison.Ordinal)) continue;
					target.SetStringProperty(TargetReceiptProperty, receipt);
					KingdomJobRow prepared = row.WithTargetReceipt(observed,
						KingdomDeliveryTargetReceiptState.Prepared);
					KingdomJobTable next;
					if (!table.TryReplace(prepared, out next, out fault)
						|| !system.Jobs.TryPublish(next, out fault)) continue;
					table = next;
					row = prepared;
					standing = receipt;
				}
				int marked = row.Cargo == KingdomStockKind.Food
					? MarkedFood(target, row.JobId) : 0;
				KingdomScalarReceiptAction action;
				if (!KingdomScalarReceiptRules.TryRecover(row.Cargo,
					row.DeliveryTargetBeforeAmount, row.CargoAmount, observed,
					string.Equals(standing, receipt, StringComparison.Ordinal), marked,
					out action) || action == KingdomScalarReceiptAction.Interference) continue;
				int landed = row.CargoAmount;
				if (action == KingdomScalarReceiptAction.Apply)
				{
					landed = row.Cargo == KingdomStockKind.Water
						? survey.StoreIn(water, row.CargoAmount)
						: AddMarkedFood(survey, target, row.JobId, row.CargoAmount,
							cropBlueprint);
				}
				else if (action == KingdomScalarReceiptAction.ContinueFood)
					landed = marked + AddMarkedFood(survey, target, row.JobId,
						row.CargoAmount - marked, cropBlueprint);
				if (landed != row.CargoAmount) continue;
				KingdomJobRow closedStop = row.WithCargoLanded();
				KingdomJobTable replaced;
				if (!table.TryReplace(closedStop, out replaced, out fault)
					|| !system.Jobs.TryPublish(replaced, out fault)) continue;
				table = replaced;
				landedRows++;
				if (system.City != null && system.City.DistanceCache != null)
				{
					int targetZone;
					if (!system.City.DistanceCache.Matrix.Graph.TryIndexOf(zone.ZoneID,
						out targetZone) || !system.City.DistanceCache.TryFill(targetZone,
							row.DeliveryTargetEndpointId, row.Cargo, row.CargoAmount))
						system.City.DistanceCache = null;
				}
				if (TripLanded(table, row.DeliveryTripId))
				{
					KingdomJobTable without;
					KingdomJobRow[] closed;
					if (table.TryCloseTrip(row.DeliveryTripId, out without, out closed, out fault)
						&& system.Jobs.TryPublish(without, out fault))
					{
						table = without;
						KingdomPorters.RetireCentralCarrier(system, row.DeliveryTripId);
					}
				}
				system.Ledger.Note("{{C|" + KingdomCityRules.CarryNote(row.Cargo,
					row.CargoAmount, KingdomPresentation.Rich(system.KingdomDisplayName)) + "}}");
			}
			return landedRows;
		}

		/// <summary>Removes only stale receipt tags, never their objects. `_stock` remains vanilla's
		/// ownership marker; this merely prevents an old closed trip from blocking a future receipt.</summary>
		internal static void SweepReceiptMarkers(KingdomSystem system, KingdomSurvey survey)
		{
			KingdomJobTable table;
			KingdomCityFault fault;
			if (system == null || survey == null || system.Jobs == null
				|| !system.Jobs.TryRead(out table, out fault)) return;
			for (int i = 0; i < survey.Stores.Count; i++)
				SweepTarget(table, survey.Stores[i] == null ? null : survey.Stores[i].ParentObject);
			for (int i = 0; i < survey.Larders.Count; i++) SweepTarget(table, survey.Larders[i]);
		}
	}
}
