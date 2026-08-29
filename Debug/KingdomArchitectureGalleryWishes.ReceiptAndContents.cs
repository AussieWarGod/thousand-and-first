using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using XRL;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public partial class KingdomArchitectureGalleryWishes
	{
		private static bool StampExactGallerySet(GameObject Owner, Zone Zone,
			ArchitectureLayoutSnapshot Snapshot, string Lot, string Receipt, out string Failure)
		{
			Failure = null;
			List<GameObject> components = Components(Zone, Lot);
			if (components.Count != Snapshot.Placements.Count)
				return Fail("The complete stamper output count disagrees with the snapshot.", out Failure);
			StampGallery(Owner, Receipt, Owner.GetStringProperty(GalleryCaseProperty));
			FreezeContents(Owner);
			for (int i = 0; i < components.Count; i++)
			{
				StampGallery(components[i], Receipt, "component");
				FreezeContents(components[i]);
			}
			return true;
		}

		private static void StampGallery(GameObject Item, string Receipt, string CaseKey)
		{
			Item.SetStringProperty(GalleryReceiptProperty, Receipt);
			Item.SetStringProperty(GalleryCaseProperty, CaseKey);
			Item.SetIntProperty(GallerySchemaProperty, GallerySchema);
		}

		private static void FreezeContents(GameObject Item)
		{
			Item.SetStringProperty(GalleryInventoryProperty, InventoryHash(Item));
			Item.SetStringProperty(GalleryLiquidProperty, LiquidHash(Item));
		}

		private static bool FrozenContents(GameObject Item)
		{
			return Item.GetStringProperty(GalleryInventoryProperty) == InventoryHash(Item)
				&& Item.GetStringProperty(GalleryLiquidProperty) == LiquidHash(Item);
		}

		private static string LiquidHash(GameObject Item)
		{
			LiquidVolume liquid = Item?.GetPart<LiquidVolume>();
			if (liquid == null) return "<none>";
			List<string> rows = new List<string>
			{
				"volume=" + liquid.Volume.ToString(CultureInfo.InvariantCulture),
				"maximum=" + liquid.MaxVolume.ToString(CultureInfo.InvariantCulture),
				"flags=" + liquid.Flags.ToString(CultureInfo.InvariantCulture)
			};
			if (liquid.ComponentLiquids != null)
				foreach (KeyValuePair<string, int> component in liquid.ComponentLiquids)
					rows.Add("component=" + (component.Key ?? "<null>") + "="
						+ component.Value.ToString(CultureInfo.InvariantCulture));
			rows.Sort(StringComparer.Ordinal);
			return Hash(string.Join("\n", rows.ToArray()));
		}

		private static string InventoryHash(GameObject Item)
		{
			List<string> rows = new List<string>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			AppendInventory(Item, "<root>", rows, seen, 0);
			rows.Sort(StringComparer.Ordinal);
			return Hash(string.Join("\n", rows.ToArray()));
		}

		private static void AppendInventory(GameObject Parent, string ParentKey,
			List<string> Rows, HashSet<GameObject> Seen, int Depth)
		{
			if (Parent == null || Rows == null || Seen == null) return;
			if (Depth > 64 || !Seen.Add(Parent))
			{
				Rows.Add(ParentKey + "\t<cycle-or-depth>");
				return;
			}
			Inventory inventory = Parent.Inventory;
			for (int i = 0; inventory != null && i < inventory.Objects.Count; i++)
			{
				GameObject child = inventory.Objects[i];
				string id = child?.ID ?? "<null>";
				string blueprint = child?.Blueprint ?? "<null>";
				int count = child == null ? 0 : child.Count;
				Rows.Add(ParentKey + "\t" + id + "\t" + blueprint + "\t"
					+ count.ToString(CultureInfo.InvariantCulture));
				if (child != null) AppendInventory(child, id, Rows, Seen, Depth + 1);
			}
		}
	}
}
