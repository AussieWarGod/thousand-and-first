using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomSurvey
	{
		private sealed class SpoilFrame
		{
			internal GameObject Container;
			internal string ContainerId;
			internal Zone Zone;
			internal string ZoneId;
			internal Cell Cell;
			internal Inventory Inventory;
			internal List<GameObject> List;
			internal GameObject[] Items;
			internal string[] ItemIds;
			internal int[] Counts;
			internal bool[] Edible;
			internal List<GameObject> LarderList;
			internal GameObject[] LarderRows;
			internal int FoodStored;
			internal int FoodCapacity;
			internal KingdomRules.PantryTier FoodAbundance;
		}

		/// <summary>
		/// Invokes each destructive food callback once. Every unit is counted only after the exact
		/// same container, Inventory part/list, item ordering, identities, ownership, and counts prove
		/// the one expected transition. A veto with no delta never counts.
		/// </summary>
		public bool TrySpoilFromExact(GameObject Container, int Amount, out int Lost)
		{
			Lost = 0;
			SpoilFrame frame;
			if (!TryCaptureSpoilFrame(Container, Amount, out frame)) return false;
			int[] expected = (int[])frame.Counts.Clone();
			int remaining = Amount;
			for (int i = 0; i < frame.Items.Length && remaining > 0; i++)
			{
				if (!frame.Edible[i]) continue;
				GameObject food = frame.Items[i];
				while (remaining > 0 && expected[i] > 0)
				{
					if (!SpoilTopologyExact(frame, expected)) return false;
					int before = expected[i];
					try
					{
						food.Destroy(null, Silent: true);
					}
					catch
					{
						// The exact post-callback topology below, never the exception or return value,
						// decides whether one physical unit was lost.
					}
					expected[i] = before - 1;
					if (!SpoilTopologyExact(frame, expected))
					{
						expected[i] = before;
						if (SpoilTopologyExact(frame, expected))
						{
							if (!PublishSpoilCounters(frame, Lost)) Lost = 0;
						}
						else Lost = 0;
						return false;
					}
					Lost++;
					remaining--;
				}
			}
			if (!PublishSpoilCounters(frame, Lost))
			{
				Lost = 0;
				return false;
			}
			return Lost == Amount;
		}

		private bool PublishSpoilCounters(SpoilFrame Frame, int Lost)
		{
			if (Frame == null || Lost < 0 || Lost > Frame.FoodStored
				|| FoodStored != Frame.FoodStored || FoodCapacity != Frame.FoodCapacity
				|| FoodAbundance != Frame.FoodAbundance) return false;
			if (Lost > 0)
			{
					FoodStored = Frame.FoodStored - Lost;
					FoodAbundance = KingdomRules.ClassifyPantry(FoodStored);
					SynchronizeReceiptObject(Frame.Container);
			}
			return true;
		}

		private bool TryCaptureSpoilFrame(GameObject Container, int Amount,
			out SpoilFrame Frame)
		{
			Frame = null;
			Inventory inventory = GameObject.Validate(Container) ? Container.Inventory : null;
			if (inventory == null || inventory.Objects == null || inventory.ParentObject != Container
				|| Container.CurrentZone == null || Container.CurrentCell == null
				|| Container.CurrentCell.ParentZone != Container.CurrentZone || Amount <= 0
				|| FoodStored < Amount || !Larders.Contains(Container)) return false;
			GameObject[] items = inventory.Objects.ToArray();
			int[] counts = new int[items.Length];
			string[] ids = new string[items.Length];
			bool[] edible = new bool[items.Length];
			int available = 0;
			for (int i = 0; i < items.Length; i++)
			{
				GameObject item = items[i];
				if (!GameObject.Validate(item) || item.Physics == null || item.InInventory != Container
					|| item.CurrentCell != null || item.Count <= 0 || string.IsNullOrEmpty(item.ID)) return false;
				for (int j = 0; j < i; j++) if (ReferenceEquals(items[j], item)) return false;
				counts[i] = item.Count;
				ids[i] = item.ID;
				edible[i] = item.HasPart("Food") || item.HasPart("PreparedCookingIngredient");
				if (edible[i])
				{
					long next = (long)available + item.Count;
					available = (next > int.MaxValue) ? int.MaxValue : (int)next;
				}
			}
			if (available < Amount) return false;
			Frame = new SpoilFrame
			{
				Container = Container,
				ContainerId = Container.ID,
				Zone = Container.CurrentZone,
				ZoneId = Container.CurrentZone.ZoneID,
				Cell = Container.CurrentCell,
				Inventory = inventory,
				List = inventory.Objects,
				Items = items,
				ItemIds = ids,
				Counts = counts,
				Edible = edible,
				LarderList = Larders,
				LarderRows = Larders.ToArray(),
				FoodStored = FoodStored,
				FoodCapacity = FoodCapacity,
				FoodAbundance = FoodAbundance
			};
			return true;
		}

		private bool SpoilTopologyExact(SpoilFrame Frame, int[] Expected)
		{
			if (Frame == null || Expected == null || Expected.Length != Frame.Items.Length
				|| !GameObject.Validate(Frame.Container) || Frame.Container.ID != Frame.ContainerId
				|| Frame.Container.CurrentZone != Frame.Zone || Frame.Zone.ZoneID != Frame.ZoneId
				|| Frame.Container.CurrentCell != Frame.Cell
				|| Frame.Cell == null || Frame.Cell.ParentZone != Frame.Zone
				|| !ReferenceEquals(Frame.Container.Inventory, Frame.Inventory)
				|| Frame.Inventory.ParentObject != Frame.Container
				|| !ReferenceEquals(Frame.Inventory.Objects, Frame.List)
				|| !ReferenceEquals(Larders, Frame.LarderList)
				|| Larders.Count != Frame.LarderRows.Length
				|| FoodStored != Frame.FoodStored || FoodCapacity != Frame.FoodCapacity
				|| FoodAbundance != Frame.FoodAbundance) return false;
			for (int i = 0; i < Frame.LarderRows.Length; i++)
				if (!ReferenceEquals(Larders[i], Frame.LarderRows[i])) return false;
			int live = 0;
			for (int i = 0; i < Frame.Items.Length; i++) if (Expected[i] > 0) live++;
			if (Frame.List.Count != live) return false;
			int row = 0;
			for (int i = 0; i < Frame.Items.Length; i++)
			{
				GameObject item = Frame.Items[i];
				if (Expected[i] <= 0)
				{
					if (Frame.List.Contains(item) || GameObject.Validate(item)) return false;
					continue;
				}
				if (!ReferenceEquals(Frame.List[row++], item) || !GameObject.Validate(item)
					|| item.ID != Frame.ItemIds[i] || item.Count != Expected[i]
					|| item.InInventory != Frame.Container || item.CurrentCell != null
					|| (item.HasPart("Food") || item.HasPart("PreparedCookingIngredient")) != Frame.Edible[i])
					return false;
			}
			return true;
		}

	}
}
