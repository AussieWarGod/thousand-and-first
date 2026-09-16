using System;
using System.Collections.Generic;
using System.Globalization;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		private static bool TryRollbackNewLayout(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			out string Failure)
		{
			Failure = null;
			for (int i = Snapshot.Placements.Count - 1; i >= 0; i--)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				if (placement.ExistingAuthority
					|| Owner.GetIntProperty(OutputState(placement)) == 2
						&& FindCarriedComponent(Z, Owner.GetStringProperty(OutputId(placement)))
					|| Owner.GetIntProperty(OutputState(placement)) == 0) continue;
				string id = Owner.GetStringProperty(OutputId(placement));
				GameObject item;
				if (KingdomConstruction.FindExactId(Z, id, out item)
					!= KingdomPhysicalLookupState.Exact
					// Rollback carries no upgrade receipt: no other generation may stand.
					|| !ExactComponent(Owner, item, Z, Intent, Lot, placement, id, null))
					return Fail("rollback cannot prove exact slot " + placement.Slot, out Failure);
				bool removed;
				try { removed = item.Obliterate(null, Silent: true); }
				catch (Exception exception)
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Z, item);
					return Fail("rollback of slot " + placement.Slot + " threw: "
						+ exception.Message, out Failure);
				}
				if (removed && !GameObject.Validate(item))
					KingdomSurvey.ObserveRemovedFromActive(Z, item);
				if (!removed || GameObject.Validate(item)
					|| KingdomConstruction.FindExactId(Z, id, out _)
						!= KingdomPhysicalLookupState.Absent)
					return Fail("rollback could not remove exact slot " + placement.Slot, out Failure);
				Owner.SetStringProperty(OutputId(placement), null, RemoveIfNull: true);
				Owner.RemoveIntProperty(OutputState(placement));
			}
			return true;
		}

		private static bool FindCarriedComponent(Zone Z, string Id)
		{
			GameObject exact;
			return KingdomConstruction.FindExactId(Z, Id, out exact)
				== KingdomPhysicalLookupState.Exact
				&& exact.GetIntProperty(ComponentCarriedProperty) == 1;
		}

		internal static HashSet<int> ConnectionCells(Zone Z)
		{
			HashSet<int> result = new HashSet<int>();
			foreach (ZoneConnection connection in Z.EnumerateConnections())
				AddConnection(result, Z, connection);
			if (Z.ZoneConnectionCache != null)
				for (int i = 0; i < Z.ZoneConnectionCache.Count; i++)
					AddConnection(result, Z, Z.ZoneConnectionCache[i]);
			return result;
		}

		private static void AddConnection(HashSet<int> Into, Zone Z, ZoneConnection Connection)
		{
			if (Connection != null && Connection.X >= 0 && Connection.X < Z.Width
				&& Connection.Y >= 0 && Connection.Y < Z.Height)
				Into.Add(Connection.Y * Z.Width + Connection.X);
		}

		private static string OutputId(ArchitecturePlacement Placement)
		{
			return OutputIdPrefix + PropertySlot(Placement.Slot);
		}

		private static string OutputState(ArchitecturePlacement Placement)
		{
			return OutputStatePrefix + PropertySlot(Placement.Slot);
		}

		private static string PropertySlot(string Slot)
		{
			return Slot == null ? "invalid" : Slot.Replace(':', '_');
		}

		private static string ComponentToken(string Lot, string Hash,
			ArchitecturePlacement Placement)
		{
			return KingdomArchitectureComponentCensusRules.ComponentTokenText(Lot, Hash,
				Placement);
		}

		private static bool ValidLotId(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxLotIdChars
				|| Value != Value.Trim()) return false;
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return false;
			return true;
		}

		private static string Coordinate(int X, int Y)
		{
			return X.ToString(CultureInfo.InvariantCulture) + ","
				+ Y.ToString(CultureInfo.InvariantCulture);
		}

		private static bool Quarantine(GameObject Owner, string Message, out string Failure)
		{
			Failure = Bounded(Message);
			try { Owner.SetStringProperty(FaultProperty, Failure); } catch { }
			return false;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Bounded(Message);
			return false;
		}

		private static string Bounded(string Value)
		{
			if (string.IsNullOrEmpty(Value)) return "authored layout refused without a reason";
			return Value.Length <= MaxFailureChars ? Value : Value.Substring(0, MaxFailureChars);
		}
	}
}
