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
		private static bool ExactClearOutput(r_KingdomPlotWorks Works, Zone Z,
			KingdomPlotRules.Material Material, int Amount, out GameObject Exact)
		{
			Exact = null;
			if (Works?.ParentObject == null || Z == null || Amount <= 0
				|| (int)StockMaterial(Material) < 0
				|| ClearString(Works, ClearDestinationZoneProperty) != Z.ZoneID)
				return false;
			string id = ClearString(Works, ClearOutputIdProperty);
			string blueprint = ClearString(Works, ClearOutputBlueprintProperty);
			string marker = ClearString(Works, ClearOutputMarkerProperty);
			if (KingdomConstruction.FindExactId(Z, id, out GameObject item)
				!= KingdomPhysicalLookupState.Exact || item.Blueprint != blueprint
				|| item.Count != Amount || item.GetStringProperty(ClearOutputMark) != marker
				|| CountClearOutputs(Z, marker) != 1) return false;
			int kind = ClearInt(Works, ClearDestinationKindProperty);
			if (kind == 1)
			{
				GameObject destination;
				if (KingdomConstruction.FindExactId(Z,
					ClearString(Works, ClearDestinationIdProperty), out destination)
					!= KingdomPhysicalLookupState.Exact || destination.Inventory == null
					|| destination.GetIntProperty(KingdomMaterials.StockpileProperty) != 1
					|| item.InInventory != destination
					|| ReferenceCount(destination.Inventory.Objects, item) != 1) return false;
			}
			else if (kind == 2)
			{
				Cell cell = ExactClearDestinationCell(Works, Z);
				if (cell == null || item.InInventory != null || item.CurrentCell != cell
					|| ReferenceCount(cell.GetObjects(), item) != 1) return false;
			}
			else return false;
			Exact = item;
			return true;
		}

		private static Cell ExactClearDestinationCell(r_KingdomPlotWorks Works, Zone Z)
		{
			if (Z == null || ClearString(Works, ClearDestinationZoneProperty) != Z.ZoneID)
				return null;
			return Z.GetCell(ClearInt(Works, ClearDestinationXProperty),
				ClearInt(Works, ClearDestinationYProperty));
		}

		private static int CountClearOutputs(Zone Z, string Marker)
		{
			if (Z == null || string.IsNullOrEmpty(Marker)) return int.MaxValue;
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null)
			{
				IList<GameObject> loaded;
				if (!active.TryLoaded(out loaded)) return int.MaxValue;
				int indexedCount = 0;
				for (int i = 0; i < loaded.Count; i++)
					if (GameObject.Validate(loaded[i])
						&& loaded[i].GetStringProperty(ClearOutputMark) == Marker) indexedCount++;
				return indexedCount;
			}
			List<GameObject> pending = new List<GameObject>(Z.GetObjects());
			HashSet<GameObject> seen = new HashSet<GameObject>();
			int count = 0;
			while (pending.Count > 0)
			{
				int last = pending.Count - 1;
				GameObject item = pending[last];
				pending.RemoveAt(last);
				if (!GameObject.Validate(item)) continue;
				if (!seen.Add(item) || seen.Count > 4096) return int.MaxValue;
				if (item.GetStringProperty(ClearOutputMark) == Marker) count++;
				if (item.Inventory == null) continue;
				for (int i = 0; i < item.Inventory.Objects.Count; i++)
					pending.Add(item.Inventory.Objects[i]);
			}
			return count;
		}

		private static int ReferenceCount(IList<GameObject> Objects, GameObject Target)
		{
			if (Objects == null || Target == null) return 0;
			int count = 0;
			for (int i = 0; i < Objects.Count; i++)
				if (ReferenceEquals(Objects[i], Target)) count++;
			return count;
		}

		private static KingdomMaterial StockMaterial(KingdomPlotRules.Material Material)
		{
			switch (Material)
			{
			case KingdomPlotRules.Material.Timber: return KingdomMaterial.Timber;
			case KingdomPlotRules.Material.Stone: return KingdomMaterial.Stone;
			case KingdomPlotRules.Material.Marble: return KingdomMaterial.Marble;
			case KingdomPlotRules.Material.Scrap: return KingdomMaterial.Scrap;
			default: return (KingdomMaterial)(-1);
			}
		}

		private static int ClearTally(r_KingdomPlotWorks Works,
			KingdomPlotRules.Material Material)
		{
			switch (Material)
			{
			case KingdomPlotRules.Material.Timber: return ClearInt(Works, ClearTimberProperty);
			case KingdomPlotRules.Material.Stone: return ClearInt(Works, ClearStoneProperty);
			case KingdomPlotRules.Material.Marble: return ClearInt(Works, ClearMarbleProperty);
			case KingdomPlotRules.Material.Scrap: return ClearInt(Works, ClearScrapProperty);
			default: return -1;
			}
		}

		private static void SetClearTally(r_KingdomPlotWorks Works,
			KingdomPlotRules.Material Material, int Value)
		{
			switch (Material)
			{
			case KingdomPlotRules.Material.Timber: ClearInt(Works, ClearTimberProperty, Value); break;
			case KingdomPlotRules.Material.Stone: ClearInt(Works, ClearStoneProperty, Value); break;
			case KingdomPlotRules.Material.Marble: ClearInt(Works, ClearMarbleProperty, Value); break;
			case KingdomPlotRules.Material.Scrap: ClearInt(Works, ClearScrapProperty, Value); break;
			}
		}

		private static bool QuarantineClear(r_KingdomPlotWorks Works, string Failure)
		{
			ClearInt(Works, ClearQuarantinedProperty, 1);
			string failure = Failure != null && Failure.Length > 1024
				? Failure.Substring(0, 1024) : Failure;
			ClearString(Works, ClearFailureProperty, failure);
			KingdomLog.Log("plot clearance quarantined: " + failure);
			return false;
		}

		private static bool SettleClearRemovalTopology(r_KingdomPlotWorks Works, Zone Z,
			GameObject Attempted, Cell Cell, KingdomPlotRules.Material Material, int Amount)
		{
			string id = ClearString(Works, ClearIdProperty);
			KingdomPhysicalLookupState state = KingdomConstruction.FindExactId(Z, id,
				out GameObject exact);
			if (state == KingdomPhysicalLookupState.Exact)
			{
				if (object.ReferenceEquals(exact, Attempted)
					&& ExactClearSource(Works, Z, exact, Cell, Material, Amount)) return false;
				return QuarantineClear(Works,
					"Clearance removal moved or replaced its exact source.");
			}
			if (state != KingdomPhysicalLookupState.Absent)
				return QuarantineClear(Works, "Clearance removal became ambiguous.");
			KingdomSurvey.ObserveRemovedFromActive(Z, Attempted);
			ClearInt(Works, ClearRemovedProperty, 1);
			ClearInt(Works, ClearPhaseProperty, 2);
			return true;
		}

		private static int ClearInt(r_KingdomPlotWorks Works, string Property)
		{
			return Works?.ParentObject == null ? 0 : Works.ParentObject.GetIntProperty(Property);
		}

		private static void ClearInt(r_KingdomPlotWorks Works, string Property, int Value)
		{
			if (Works?.ParentObject == null) return;
			if (Value == 0) Works.ParentObject.RemoveIntProperty(Property);
			else Works.ParentObject.SetIntProperty(Property, Value);
		}

		private static string ClearString(r_KingdomPlotWorks Works, string Property)
		{
			return Works?.ParentObject?.GetStringProperty(Property);
		}

		private static void ClearString(r_KingdomPlotWorks Works, string Property, string Value)
		{
			if (Works?.ParentObject == null) return;
			Works.ParentObject.SetStringProperty(Property, Value, RemoveIfNull: true);
		}

		private static void TellClearMaterials(int[] Yields, string Name)
		{
			System.Text.StringBuilder earned = new System.Text.StringBuilder();
			for (int i = 1; i < Yields.Length; i++)
			{
				if (Yields[i] <= 0)
				{
					continue;
				}
				KingdomPlotRules.Material material = (KingdomPlotRules.Material)i;
				if (earned.Length > 0)
				{
					earned.Append(", ");
				}
				earned.Append(Yields[i]).Append(' ').Append(material.ToString().ToLowerInvariant());
			}
			if (earned.Length > 0)
			{
				MessageQueue.AddPlayerMessage("{{G|Clearing the ground for the " + (Name ?? "work") + " yields " + earned + ".}}");
			}
		}

		/// <summary>Compatibility reading for old add-ons which asked this class for the stock on
		/// the founder's current ground. The authority is now, and always should have been, the
		/// physical contents of dedicated stockpiles.</summary>
		[Obsolete("Use KingdomMaterials.Stock(zone).Tally; material authority is physical and zone-local.")]
		public static int MaterialsHeld(KingdomPlotRules.Material Of)
		{
			KingdomMaterial material = StockMaterial(Of);
			Zone zone = The.Player?.CurrentZone;
			if ((int)material < 0 || zone == null)
			{
				return 0;
			}
			return KingdomMaterials.Stock(zone).Tally.Get(material);
		}

	}
}
