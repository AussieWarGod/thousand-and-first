using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomRealmRetirementGround
	{
		private static bool TryBuildConstructionAuthority(KingdomSystem System, Zone Zone,
			IList<GameObject> Objects, out HashSet<GameObject> Owned, out string Failure)
		{
			Owned = new HashSet<GameObject>(); Failure = null;
			if (System == null || Zone == null || string.IsNullOrEmpty(
				System.SettlementIdForOwnedZone(Zone.ZoneID)))
				return Fail("loaded ground lacks exact current-realm settlement authority", out Failure);
			Dictionary<string, GameObject> ids = new Dictionary<string, GameObject>(
				StringComparer.Ordinal);
			for (int i = 0; i < (Objects?.Count ?? 0); i++)
			{
				GameObject item = Objects[i]; string id = item?.IDIfAssigned;
				if (string.IsNullOrEmpty(id)) continue;
				if (ids.ContainsKey(id))
					return Fail("loaded ground has duplicate object identity " + id, out Failure);
				ids[id] = item;
				if (ExactConstructionOutput(System, Zone, item)) Owned.Add(item);
			}
			List<GameObject> roots = new List<GameObject>(Owned);
			for (int i = 0; i < roots.Count; i++)
			{
				GameObject root = roots[i];
				if (!root.HasIntProperty(KingdomArchitectureStamper.SchemaProperty)) continue;
				if (!KingdomArchitectureStamper.TryReadOwner(root,
					out KingdomArchitectureIntent intent, out ArchitectureLayoutSnapshot snapshot,
					out string lot, out Failure)
					|| !TryVerifyArchitectureReadOnly(root, Zone, intent, snapshot, lot, ids, Objects,
						out Failure))
					return false;
				for (int p = 0; p < snapshot.Placements.Count; p++)
				{
					ArchitecturePlacement placement = snapshot.Placements[p];
					if (placement.ExistingAuthority) continue;
					string property = KingdomArchitectureStamper.OutputIdPrefix
						+ (placement.Slot ?? "invalid").Replace(':', '_');
					string id = root.GetStringProperty(property);
					if (!ids.TryGetValue(id ?? "", out GameObject component)
						|| component.GetStringProperty(
							KingdomArchitectureStamper.ComponentHashProperty) != intent.SnapshotHash)
						return Fail("architecture output is absent from its exact loaded authority",
							out Failure);
					Owned.Add(component);
				}
			}
			return true;
		}

		private static bool TryVerifyArchitectureReadOnly(GameObject Owner, Zone Zone,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			Dictionary<string, GameObject> Ids, IList<GameObject> Objects, out string Failure)
		{
			Failure = null;
			if (Owner == null || Zone == null || Intent == null || Snapshot == null
				|| Owner.GetIntProperty(KingdomArchitectureStamper.NextLayerProperty) != 3)
				return Fail("architecture owner is not a complete read-only authority", out Failure);
			for (int i = 0; i < Snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = Snapshot.Placements[i];
				string suffix = (placement.Slot ?? "invalid").Replace(':', '_');
				if (Owner.GetIntProperty(KingdomArchitectureStamper.OutputStatePrefix + suffix) != 2)
					return Fail("architecture output state is not terminal", out Failure);
				string id = Owner.GetStringProperty(KingdomArchitectureStamper.OutputIdPrefix + suffix);
				if (!Ids.TryGetValue(id ?? "", out GameObject item)
					|| !ExactArchitectureComponent(item, Zone, Intent, Snapshot, Lot, placement, id,
						Objects, out Failure)) return false;
			}
			return true;
		}

		private static bool ExactArchitectureComponent(GameObject Item, Zone Zone,
			KingdomArchitectureIntent Intent, ArchitectureLayoutSnapshot Snapshot, string Lot,
			ArchitecturePlacement Placement, string ExpectedId, IList<GameObject> Objects,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item) || Item.IDIfAssigned != ExpectedId
				|| Item.CurrentZone != Zone || Item.Blueprint != Placement.Blueprint
				|| Item.GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
					!= KingdomArchitectureStamper.ComponentSchema
				|| Item.GetStringProperty(KingdomPlots.PlotIdProperty) != Lot
				|| Item.GetStringProperty(KingdomArchitectureStamper.ComponentSlotProperty)
					!= Placement.Slot
				|| Item.GetIntProperty(KingdomArchitectureStamper.ComponentLayerProperty)
					!= (int)Placement.Layer
				|| Item.GetStringProperty(KingdomArchitectureStamper.ComponentHashProperty)
					!= Intent.SnapshotHash
				|| Item.GetStringProperty(KingdomArchitectureStamper.ComponentTokenProperty)
					!= ArchitectureToken(Lot, Intent.SnapshotHash, Placement)
				|| Item.GetIntProperty(KingdomArchitectureStamper.ComponentExistingProperty)
					!= (Placement.ExistingAuthority ? 1 : 0)
				|| Item.GetIntProperty(KingdomPlots.PlotPartProperty)
					!= (Placement.ExistingAuthority ? 0 : 1)
				|| (Item.GetStringProperty(KingdomArchitectureStamper.ComponentAnchorProperty) ?? "")
					!= (Placement.StatefulAnchor ?? ""))
				return Fail("architecture component differs from its frozen receipt", out Failure);
			if (!KingdomArchitectureRuntime.TryWorldPlacement(Snapshot, Intent.Rect, Placement,
				out int x, out int y, out Failure) || Item.CurrentCell != Zone.GetCell(x, y))
				return Fail(Failure ?? "architecture component moved from its frozen cell", out Failure);
			int count = 0;
			for (int i = 0; i < (Objects?.Count ?? 0); i++)
				if (GameObject.Validate(Objects[i]) && Objects[i].CurrentZone == Zone
					&& Objects[i].GetStringProperty(KingdomPlots.PlotIdProperty) == Lot
					&& Objects[i].GetStringProperty(
						KingdomArchitectureStamper.ComponentSlotProperty) == Placement.Slot) count++;
			if (count != 1)
				return Fail("architecture slot does not have exactly one loaded component", out Failure);
			return true;
		}

		private static string ArchitectureToken(string Lot, string Hash,
			ArchitecturePlacement Placement)
		{
			string preimage = Lot + "|" + Hash + "|" + Placement.Slot + "|"
				+ ((int)Placement.Layer).ToString(CultureInfo.InvariantCulture) + "|"
				+ Placement.X.ToString(CultureInfo.InvariantCulture) + "|"
				+ Placement.Y.ToString(CultureInfo.InvariantCulture) + "|"
				+ Placement.Blueprint + "|" + (Placement.StatefulAnchor ?? "") + "|"
				+ (Placement.ExistingAuthority ? "1" : "0");
			byte[] digest;
			using (SHA256 sha = SHA256.Create())
				digest = sha.ComputeHash(Encoding.UTF8.GetBytes(preimage));
			StringBuilder result = new StringBuilder(64);
			for (int i = 0; i < digest.Length; i++)
				result.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return result.ToString();
		}

		private static bool ExactConstructionOutput(KingdomSystem System, Zone Zone,
			GameObject Item)
		{
			if (!GameObject.Validate(Item) || Item.CurrentZone != Zone
				|| string.IsNullOrEmpty(Item.IDIfAssigned)) return false;
			string receipt = Item.GetStringProperty(KingdomConstruction.ReceiptProperty);
			if (string.IsNullOrEmpty(receipt) || !KingdomConstruction.TryFind(receipt,
				out KingdomConstructionJob job)) return false;
			return KingdomConstructionRules.IsTerminal(job.Phase)
				&& KingdomConstruction.IsCurrent(job)
				&& job.OwnerKey == KingdomConstruction.OwnerOf(System)
				&& job.ZoneId == Zone.ZoneID && job.OutputId == Item.IDIfAssigned;
		}
	}
}
