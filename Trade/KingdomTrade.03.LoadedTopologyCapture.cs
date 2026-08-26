using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		private static LoadedTopologyWitness CaptureLoadedTopology()
		{
			try
			{
				ZoneManager manager;
				Zone zone;
				KingdomSurvey survey;
				if (!TryBoundTopologyGround(out manager, out zone, out survey)) return null;
				IList<GameObject> indexed;
				if (!survey.TryLoaded(out indexed) || indexed == null) return null;
				LoadedTopologyWitness witness = new LoadedTopologyWitness
				{
					Manager = manager,
					Survey = survey,
					Active = zone,
					RootList = survey.Objects
				};
				LoadedZoneWitness zoneWitness = new LoadedZoneWitness
				{
					Zone = zone,
					Roots = survey.Objects.ToArray()
				};
				witness.Zones.Add(zoneWitness);
				HashSet<GameObject> visited = new HashSet<GameObject>();
				for (int i = 0; i < zoneWitness.Roots.Length; i++)
					if (!CaptureLoadedObject(witness, zoneWitness.Roots[i],
						zoneWitness.Roots[i], zone, visited)) return null;
				return witness;
			}
			catch { return null; }
		}

		private static bool TryBindTopologyGround(KingdomSystem System, Zone Z,
			KingdomSurvey Survey)
		{
			try
			{
				ZoneManager manager = The.ZoneManager;
				KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
				if (System == null || Z == null || Survey == null || manager == null
					|| !ReferenceEquals(manager.ActiveZone, Z)
					|| !ReferenceEquals(Survey.Ground, Z)
					|| (active != null && !ReferenceEquals(active, Survey))
					|| !Survey.TryLoaded(out IList<GameObject> loaded) || loaded == null)
					return false;
				lock (InFlightSync)
				{
					if (InFlight == null || !ReferenceEquals(InFlight.System, System)) return false;
					InFlight.Zone = Z;
					InFlight.Survey = Survey;
				}
				return true;
			}
			catch { return false; }
		}

		private static bool TryBoundTopologyGround(out ZoneManager Manager,
			out Zone Z, out KingdomSurvey Survey)
		{
			Manager = null;
			Z = null;
			Survey = null;
			lock (InFlightSync)
			{
				if (InFlight == null) return false;
				Z = InFlight.Zone;
				Survey = InFlight.Survey;
			}
			Manager = The.ZoneManager;
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			return Manager != null && Z != null && Survey != null
				&& ReferenceEquals(Manager.ActiveZone, Z)
				&& ReferenceEquals(Survey.Ground, Z)
				&& (active == null || ReferenceEquals(active, Survey));
		}

		private static KingdomSurvey BoundTradeSurvey(Zone Z)
		{
			ZoneManager manager;
			Zone ground;
			KingdomSurvey survey;
			return TryBoundTopologyGround(out manager, out ground, out survey)
				&& ReferenceEquals(ground, Z) ? survey : null;
		}

		private static bool CaptureLoadedObject(LoadedTopologyWitness Topology,
			GameObject Object, GameObject Root, Zone Zone, HashSet<GameObject> Visited)
		{
			if (Topology == null || !GameObject.Validate(Object) || Root == null || Zone == null
				|| !Visited.Add(Object) || Topology.Objects.Count >= 200000) return false;
			Inventory inventory = Object.Inventory;
			List<GameObject> inventoryObjects = inventory?.Objects;
			if (inventory != null && (inventoryObjects == null
				|| !ReferenceEquals(Object.GetPart<Inventory>(), inventory)
				|| inventory.ParentObject != Object)) return false;
			List<GameObject> contents = Object.GetInventoryAndEquipmentAndDefaultEquipment();
			if (contents == null) return false;
			List<GameObject> installed = Object.Body?.GetInstalledCybernetics();
			if (installed != null)
				for (int i = 0; i < installed.Count; i++)
					if (!ContainsObjectReference(contents, installed[i])) contents.Add(installed[i]);
			if (inventoryObjects != null)
				for (int i = 0; i < inventoryObjects.Count; i++)
					if (!ContainsObjectReference(contents, inventoryObjects[i]))
						contents.Add(inventoryObjects[i]);
			LoadedObjectWitness row = new LoadedObjectWitness
			{
				Object = Object,
				Root = Root,
				Zone = Zone,
				Inventory = inventory,
				InventoryObjects = inventoryObjects,
				InventoryRows = inventoryObjects?.ToArray(),
				Contents = contents.ToArray()
			};
			Topology.Objects.Add(row);
			for (int i = 0; i < row.Contents.Length; i++)
				if (!CaptureLoadedObject(Topology, row.Contents[i], Root, Zone, Visited)) return false;
			return true;
		}

		private static bool ContainsObjectReference(IList<GameObject> Values, GameObject Value)
		{
			if (Values == null) return false;
			for (int i = 0; i < Values.Count; i++)
				if (ReferenceEquals(Values[i], Value)) return true;
			return false;
		}

		private static bool ExactLoadedInfrastructure(LoadedTopologyWitness Expected,
			LoadedTopologyWitness Current)
		{
			if (Expected == null || Current == null
				|| !ReferenceEquals(Expected.Manager, Current.Manager)
				|| !ReferenceEquals(Expected.Survey, Current.Survey)
				|| !ReferenceEquals(Expected.Active, Current.Active)
				|| !ReferenceEquals(Expected.RootList, Current.RootList)
				|| Expected.Zones.Count != Current.Zones.Count) return false;
			for (int i = 0; i < Expected.Zones.Count; i++)
				if (!ReferenceEquals(Expected.Zones[i].Zone, Current.Zones[i].Zone)) return false;
			return true;
		}

		private static LoadedObjectWitness FindLoadedRow(LoadedTopologyWitness Topology,
			GameObject Object)
		{
			if (Topology == null) return null;
			for (int i = 0; i < Topology.Objects.Count; i++)
				if (ReferenceEquals(Topology.Objects[i].Object, Object)) return Topology.Objects[i];
			return null;
		}

		private static bool ExactLoadedRow(LoadedObjectWitness Expected,
			LoadedObjectWitness Current)
		{
			if (Expected == null || Current == null
				|| !ReferenceEquals(Expected.Object, Current.Object)
				|| !ReferenceEquals(Expected.Root, Current.Root)
				|| !ReferenceEquals(Expected.Zone, Current.Zone)
				|| !ReferenceEquals(Expected.Inventory, Current.Inventory)
				|| !ReferenceEquals(Expected.InventoryObjects, Current.InventoryObjects)
				|| !ExactObjectRows(Expected.InventoryRows, Current.InventoryRows)
				|| !ExactObjectRows(Expected.Contents, Current.Contents)) return false;
			return true;
		}

		private static bool ExactObjectRows(GameObject[] Left, GameObject[] Right)
		{
			if (Left == null || Right == null) return Left == null && Right == null;
			if (Left.Length != Right.Length) return false;
			for (int i = 0; i < Left.Length; i++)
				if (!ReferenceEquals(Left[i], Right[i])) return false;
			return true;
		}

		private static bool ExactLoadedTopology(LoadedTopologyWitness Expected)
		{
			LoadedTopologyWitness current = CaptureLoadedTopology();
			if (!ExactLoadedInfrastructure(Expected, current)
				|| Expected.Objects.Count != current.Objects.Count) return false;
			for (int i = 0; i < Expected.Zones.Count; i++)
				if (!ExactObjectRows(Expected.Zones[i].Roots, current.Zones[i].Roots)) return false;
			for (int i = 0; i < Expected.Objects.Count; i++)
				if (!ExactLoadedRow(Expected.Objects[i], current.Objects[i])) return false;
			return true;
		}

	}
}
