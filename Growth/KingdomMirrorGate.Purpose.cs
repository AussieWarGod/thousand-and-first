using System;
using System.Collections.Generic;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomMirrorGate
	{
		/// <summary>
		/// Proves the hard purpose prerequisite against both real arches. This is mutation-free:
		/// stale draw state refuses and names the visit that will settle it rather than charging a
		/// dormant city during a preview.
		/// </summary>
		internal static bool TryPurposeConnection(r_KingdomMirrorGate Gate,
			KingdomSystem System, out KingdomPurposeConnection Connection, out string Failure)
		{
			return TryPurposeConnection(Gate, System, out Connection, out _, out Failure);
		}

		/// <summary>The same live proof, with an explicit signal for duplicate physical endpoints.
		/// A preview can simply refuse either condition; a published consignment must quarantine an
		/// ambiguity because retrying cannot choose which arch the frozen route meant.</summary>
		internal static bool TryPurposeConnection(r_KingdomMirrorGate Gate,
			KingdomSystem System, out KingdomPurposeConnection Connection,
			out bool RequiresInspection, out string Failure)
		{
			Connection = null;
			RequiresInspection = false;
			Failure = null;
			if (Gate == null || System == null || !System.Founded || The.Game == null
				|| The.ZoneManager == null || !KingdomPower.Enabled)
				return PurposeConnectionFailure("Both cities must keep power enabled before a purpose consignment can move.", out Failure);
			GameObject sourceObject = Gate.ParentObject;
			Zone sourceZone = sourceObject?.CurrentZone;
			Cell sourceCell = sourceObject?.CurrentCell;
			string sourceCity = CityOf(System, sourceZone?.ZoneID);
			if (sourceZone == null || sourceCell == null || sourceCity == null
				|| (!KingdomUpgrade.IsFunctionallyBuilt(sourceObject)
					&& (sourceObject.GetIntProperty("KingdomGrid") != 1
						|| r_KingdomScaffold.HasPendingImprovementSuccessorAuthority(sourceObject))))
				return PurposeConnectionFailure("Stand at a finished mirror-gate on this city's own ground.", out Failure);
			Anchor(Gate);
			KingdomGateRow[] rows = Register(null);
			int sourceAt = KingdomMirrorGateRules.IndexOfKey(rows, Gate.LocationKey);
			if (sourceAt < 0 || string.IsNullOrEmpty(rows[sourceAt].Partner))
				return PurposeConnectionFailure("Key this mirror-gate and its twin in another city first.", out Failure);
			string destinationKey = rows[sourceAt].Partner;
			int destinationAt = KingdomMirrorGateRules.IndexOfKey(rows, destinationKey);
			if (destinationAt < 0 || rows[destinationAt].Partner != Gate.LocationKey)
				return PurposeConnectionFailure("The gate register is not reciprocal; release and re-key the two arches.", out Failure);
			if (!KingdomMirrorGateRules.TryParseLocationKey(Gate.LocationKey,
				out string sourceZoneId, out int sourceX, out int sourceY)
				|| sourceZoneId != sourceZone.ZoneID || sourceX != sourceCell.X || sourceY != sourceCell.Y
				|| !KingdomMirrorGateRules.TryParseLocationKey(destinationKey,
					out string destinationZoneId, out int destinationX, out int destinationY))
				return PurposeConnectionFailure("The exact gate addresses are malformed; re-key the arches on their standing cells.", out Failure);
			if (!The.ZoneManager.IsZoneBuilt(destinationZoneId))
				return PurposeConnectionFailure("Visit the other gate's ground once so its real arch and power state can be proved.", out Failure);
			Zone destinationZone;
			try { destinationZone = The.ZoneManager.GetZone(destinationZoneId); }
			catch (Exception ex)
			{
				return PurposeConnectionFailure("The other gate's visited ground could not be loaded: "
					+ ex.Message, out Failure);
			}
			Cell destinationCell = destinationZone?.GetCell(destinationX, destinationY);
			r_KingdomMirrorGate destinationGate = ExactGateAt(destinationCell, destinationKey,
				out bool destinationAmbiguous);
			if (destinationAmbiguous)
			{
				RequiresInspection = true;
				return PurposeConnectionFailure("More than one physical mirror-gate answers the frozen destination address; inspect the route rather than choosing an arch.", out Failure);
			}
			GameObject destinationObject = destinationGate?.ParentObject;
			string destinationCity = CityOf(System, destinationZoneId);
			if (destinationGate == null || destinationObject == null || destinationCity == null
				|| (!KingdomUpgrade.IsFunctionallyBuilt(destinationObject)
					&& (destinationObject.GetIntProperty("KingdomGrid") != 1
						|| r_KingdomScaffold.HasPendingImprovementSuccessorAuthority(destinationObject))))
				return PurposeConnectionFailure("Visit the other city and repair or re-key the exact mirror-gate standing there.", out Failure);
			Anchor(destinationGate);
			if (Gate.DestinationKey != destinationKey
				|| destinationGate.LocationKey != destinationKey
				|| destinationGate.DestinationKey != Gate.LocationKey)
				return PurposeConnectionFailure("The two physical arches no longer answer their frozen register; re-key them.", out Failure);
			if (Gate.Dark || destinationGate.Dark)
				return PurposeConnectionFailure("One of the two arches is dark. Visit that city and restore enough charge for its daily draw.", out Failure);
			long now = The.Game.TimeTicks;
			if (Gate.LastDrawTick <= 0L || destinationGate.LastDrawTick <= 0L
				|| now < Gate.LastDrawTick || now < destinationGate.LastDrawTick
				|| now >= Gate.LastDrawTick + KingdomRules.TicksPerDay
				|| now >= destinationGate.LastDrawTick + KingdomRules.TicksPerDay)
				return PurposeConnectionFailure("A gate's power reading is stale. Visit each arch so its daily draw settles, then dispatch before the next day turns.", out Failure);
			string sourceAddress = The.Game.GetStringGameState(Gate.LocationKey, "");
			string destinationAddress = The.Game.GetStringGameState(destinationKey, "");
			if (sourceAddress != sourceCell.GetAddress()
				|| destinationAddress != destinationCell.GetAddress())
				return PurposeConnectionFailure("A physical gate address is stale; visit and re-key that arch on its standing cell.", out Failure);
			Connection = new KingdomPurposeConnection
			{
				SourceGate = Gate, DestinationGate = destinationGate,
				SourceZone = sourceZone, DestinationZone = destinationZone,
				SourceKey = Gate.LocationKey, DestinationKey = destinationKey,
				SourceCity = sourceCity, DestinationCity = destinationCity
			};
			return true;
		}

		private static r_KingdomMirrorGate ExactGateAt(Cell Cell, string Key,
			out bool Ambiguous)
		{
			Ambiguous = false;
			r_KingdomMirrorGate exact = null;
			int count = 0;
			List<GameObject> objects = Cell?.GetObjects();
			for (int i = 0; objects != null && i < objects.Count; i++)
			{
				r_KingdomMirrorGate gate = objects[i]?.GetPart<r_KingdomMirrorGate>();
				if (gate == null) continue;
				Anchor(gate);
				if (gate.LocationKey != Key) continue;
				count++;
				if (count == 1) exact = gate;
			}
			Ambiguous = count > 1;
			return count == 1 ? exact : null;
		}

		private static bool PurposeConnectionFailure(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}

	}
}
