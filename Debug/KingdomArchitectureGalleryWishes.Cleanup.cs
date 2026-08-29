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
		private static bool TryClearExact(GameObject Owner, Zone Zone, out string Failure)
		{
			Failure = null;
			KingdomArchitectureIntent intent;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (!ExactGalleryObject(Owner, Owner.GetStringProperty(GalleryReceiptProperty))
				|| !KingdomArchitectureStamper.TryReadOwner(Owner, out intent, out snapshot,
					out lot, out Failure)
				|| !KingdomArchitectureStamper.TryVerifyComplete(Owner, Zone, out Failure)) return false;
			List<GameObject> components = Components(Zone, lot);
			if (components.Count != snapshot.Placements.Count)
				return Fail("The exact gallery component set is absent or duplicated.", out Failure);
			string receipt = Owner.GetStringProperty(GalleryReceiptProperty);
			if (!FrozenContents(Owner))
				return Fail("The gallery behavior root gained or lost contents; empty or restore it first.",
					out Failure);
			for (int i = 0; i < components.Count; i++)
				if (!ExactGalleryObject(components[i], receipt) || !FrozenContents(components[i]))
					return Fail("A gallery component changed contents or ownership; cleanup stopped before "
						+ "selecting any removal.", out Failure);
			for (int i = 0; i < components.Count; i++)
				if (!components[i].Destroy(null, Silent: true) || GameObject.Validate(components[i]))
					return Fail("An exact gallery component refused removal; remaining receipts stay named.",
						out Failure);
			if (!Owner.Destroy(null, Silent: true) || GameObject.Validate(Owner))
				return Fail("The exact gallery behavior root refused removal.", out Failure);
			return true;
		}

		private static List<GameObject> Components(Zone Zone, string Lot)
		{
			List<GameObject> result = new List<GameObject>();
			foreach (GameObject item in Zone.GetObjects())
				if (GameObject.Validate(item)
					&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& item.GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
						== KingdomArchitectureStamper.ComponentSchema) result.Add(item);
			result.Sort(delegate(GameObject a, GameObject b)
			{
				return string.CompareOrdinal(a.ID, b.ID);
			});
			return result;
		}

		private static void RollBackCreated(Zone Zone, string Lot, GameObject Works,
			GameObject Final, GameObject Synthetic)
		{
			List<GameObject> created = string.IsNullOrEmpty(Lot)
				? new List<GameObject>() : Components(Zone, Lot);
			for (int i = 0; i < created.Count; i++) SafeDestroy(created[i]);
			SafeDestroy(Final);
			SafeDestroy(Works);
			SafeDestroy(Synthetic);
		}

		private static void SafeDestroy(GameObject Item)
		{
			if (!GameObject.Validate(Item)) return;
			try { Item.Destroy(null, Silent: true); }
			catch { }
		}

		private static bool TryUniqueGallery(Zone Zone, out GameObject Owner, out string Failure)
		{
			Owner = null;
			Failure = null;
			int count = 0;
			foreach (GameObject item in Zone.GetObjects())
				if (GameObject.Validate(item)
					&& item.GetIntProperty(GallerySchemaProperty) == GallerySchema
					&& item.HasIntProperty(KingdomArchitectureStamper.SchemaProperty))
				{
					Owner = item;
					count++;
				}
			if (count > 1)
			{
				Owner = null;
				return Fail("This zone has multiple gallery owners. Inspect their exact receipts; "
					+ "automatic staging and cleanup are disabled.", out Failure);
			}
			return true;
		}

		private static bool ExactGalleryObject(GameObject Item, string Receipt)
		{
			return GameObject.Validate(Item)
				&& Item.GetIntProperty(GallerySchemaProperty) == GallerySchema
				&& !string.IsNullOrEmpty(Receipt)
				&& Item.GetStringProperty(GalleryReceiptProperty) == Receipt;
		}
	}
}
