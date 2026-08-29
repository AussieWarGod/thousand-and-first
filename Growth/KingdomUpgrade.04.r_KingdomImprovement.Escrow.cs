using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace XRL.World.Parts
{
	public partial class r_KingdomImprovement
	{
		private static bool BoundedIdentity(string Value)
		{
			return !string.IsNullOrEmpty(Value) && Value.Length <= 128;
		}

		private static string EscrowKeyFor(GameObject Source, GameObject Item, int MovedBefore)
		{
			return EscrowKeyFor(Source?.ID, Item?.ID, MovedBefore);
		}

		private static string EscrowKeyFor(string SourceId, string ItemId, int MovedBefore)
		{
			if (!BoundedIdentity(SourceId) || !BoundedIdentity(ItemId) || MovedBefore < 0)
				return null;
			byte[] bytes = Encoding.UTF8.GetBytes(SourceId + "\n" + ItemId + "\n"
				+ MovedBefore.ToString(CultureInfo.InvariantCulture));
			byte[] digest;
			using (SHA256 hash = SHA256.Create()) digest = hash.ComputeHash(bytes);
			StringBuilder key = new StringBuilder(HandoverEscrowPrefix, 96);
			for (int i = 0; i < digest.Length; i++)
				key.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
			return key.ToString();
		}

		private static bool BoundedEscrowKey(string Key)
		{
			return !string.IsNullOrEmpty(Key) && Key.Length <= 128
				&& Key.StartsWith(HandoverEscrowPrefix, StringComparison.Ordinal);
		}

		private static bool RootEscrowItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Item)
		{
			string expected = EscrowKeyFor(Source, Item, Receipt.HandoverItemMovedBefore);
			if (The.Game == null || !BoundedEscrowKey(expected)
				|| Receipt.HandoverItemEscrowKey != expected)
				return FailHandover(Receipt, "The inventory escrow key could not be frozen exactly.");
			object collision;
			if (The.Game.ObjectGameState.TryGetValue(expected, out collision)
				&& !ReferenceEquals(collision, Item))
				return FailHandover(Receipt,
					"The inventory escrow key collides with another exact object.");
			The.Game.SetObjectGameState(expected, Item);
			if (!The.Game.ObjectGameState.TryGetValue(expected, out collision)
				|| !ReferenceEquals(collision, Item))
				return FailHandover(Receipt,
					"The exact inventory item did not remain rooted before removal.");
			GameObject rooted;
			if (!TryEscrowItem(Source, Target, Where, Receipt, out rooted)) return false;
			return (ReferenceEquals(rooted, Item)
				&& EscrowTopologyOf(Source, Target, Where, Receipt, Item)
					== KingdomHandoverItemTopology.Source) || FailHandover(Receipt,
						"The rooted inventory item did not remain at its exact source.");
		}

		private static bool TryEscrowItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, out GameObject Item)
		{
			Item = null;
			if (Receipt == null) return false;
			string key = Receipt?.HandoverItemEscrowKey;
			object rooted;
			if (The.Game == null || !BoundedEscrowKey(key)
				|| key != EscrowKeyFor(Source?.IDIfAssigned, Receipt?.HandoverItemId,
					Receipt.HandoverItemMovedBefore)
				|| !The.Game.ObjectGameState.TryGetValue(key, out rooted))
				return FailHandover(Receipt, "The exact inventory escrow root is absent or malformed.");
			Item = rooted as GameObject;
			if (!GameObject.Validate(Item) || Item.IDIfAssigned != Receipt.HandoverItemId
				|| Item.Blueprint != Receipt.HandoverItemBlueprint
				|| Item.Count != Receipt.HandoverItemCount
				|| EscrowTopologyOf(Source, Target, Where, Receipt, Item)
					== KingdomHandoverItemTopology.Invalid)
				return FailHandover(Receipt,
					"The rooted inventory item is missing, duplicated, replaced, or restacked.");
			return true;
		}

		private static bool ReproveEscrowItem(GameObject Source, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Expected)
		{
			GameObject rooted;
			if (!TryEscrowItem(Source, Target, Where, Receipt, out rooted)) return false;
			return ReferenceEquals(rooted, Expected) || FailHandover(Receipt,
				"The inventory callback replaced its exact rooted object reference.");
		}

		private static KingdomHandoverItemTopology EscrowTopologyOf(GameObject Source,
			GameObject Target,
			Cell Where, r_KingdomImprovement Receipt, GameObject Item)
		{
			if (!ExactHandoverObjects(Source, Target, Receipt) || Where == null
				|| Source.CurrentCell != Where || Target.CurrentCell != Where
				|| !ReferenceEquals(The.Game?.GetObjectGameState(
					Receipt.HandoverItemEscrowKey), Item)) return KingdomHandoverItemTopology.Invalid;
			int sourceRefs = ReferenceCount(Source.Inventory?.Objects, Item);
			int targetRefs = ReferenceCount(Target.Inventory?.Objects, Item);
			int cellRefs = ReferenceCount(Where.GetObjects(), Item);
			int idOccurrences;
			int exactOccurrences;
			if (!CountZoneIdentity(Where.ParentZone, Receipt.HandoverItemId, Item,
				out idOccurrences, out exactOccurrences) || Item.Physics == null)
				return KingdomHandoverItemTopology.Invalid;
			int inventoryOwner = Item.Physics.InInventory == null ? 0
				: ReferenceEquals(Item.Physics.InInventory, Source) ? 1
				: ReferenceEquals(Item.Physics.InInventory, Target) ? 2 : 3;
			int cellOwner = Item.CurrentCell == null ? 0
				: ReferenceEquals(Item.CurrentCell, Where) ? 1 : 2;
			return KingdomConstructionRules.HandoverItemTopology(sourceRefs, targetRefs,
				cellRefs, idOccurrences, exactOccurrences, inventoryOwner, cellOwner);
		}

		private static bool CountZoneIdentity(Zone Zone, string Id, GameObject Exact,
			out int Occurrences, out int ExactOccurrences)
		{
			Occurrences = 0;
			ExactOccurrences = 0;
			if (Zone == null || !BoundedIdentity(Id) || Exact == null) return false;
			KingdomSurvey active = KingdomSurvey.ActiveFor(Zone);
			if (active != null)
			{
				IList<GameObject> loaded;
				if (!active.TryLoaded(out loaded)) return false;
				for (int i = 0; i < loaded.Count; i++)
				{
					GameObject item = loaded[i];
					if (item == null || item.IDIfAssigned != Id) continue;
					Occurrences++;
					if (ReferenceEquals(item, Exact)) ExactOccurrences++;
				}
				return Occurrences <= 1 && ExactOccurrences <= 1;
			}
			List<GameObject> pending = new List<GameObject>(Zone.GetObjects());
			HashSet<GameObject> expanded = new HashSet<GameObject>();
			int visited = 0;
			while (pending.Count > 0)
			{
				if (++visited > MaxHandoverTopologyObjects) return false;
				int last = pending.Count - 1;
				GameObject item = pending[last];
				pending.RemoveAt(last);
				if (item == null) continue;
				if (item.IDIfAssigned == Id)
				{
					Occurrences++;
					if (ReferenceEquals(item, Exact)) ExactOccurrences++;
				}
				if (!expanded.Add(item)) return false;
				if (item.Inventory != null)
					for (int i = 0; i < item.Inventory.Objects.Count; i++)
						pending.Add(item.Inventory.Objects[i]);
			}
			return Occurrences <= 1 && ExactOccurrences <= 1;
		}

		private static bool ExactEnteringCell(GameObject Item, GameObject Source,
			GameObject Target, Cell Where, r_KingdomImprovement Receipt)
		{
			return EscrowTopologyOf(Source, Target, Where, Receipt, Item)
				== KingdomHandoverItemTopology.EnteringCell;
		}

		private static bool ExactLiquidEndpoint(GameObject Owner, LiquidVolume Part, int Volume,
			string Composition)
		{
			return GameObject.Validate(Owner) && Part != null && Part.ParentObject == Owner
				&& ReferenceEquals(Owner.GetPart<LiquidVolume>(), Part) && Part.Volume == Volume
				&& EncodeLiquid(Part) == Composition;
		}

		private static bool ExactItemOwner(GameObject Item, GameObject Owner,
			r_KingdomImprovement Receipt)
		{
			return GameObject.Validate(Item) && GameObject.Validate(Owner) && Owner.Inventory != null
				&& Item.Physics != null && Item.Physics.InInventory == Owner
				&& ReferenceCount(Owner.Inventory.Objects, Item) == 1
				&& (Receipt == null || (ExactEscrowReference(Receipt, Item)
					&& Item.IDIfAssigned == Receipt.HandoverItemId
					&& Item.Blueprint == Receipt.HandoverItemBlueprint
					&& Item.Count == Receipt.HandoverItemCount));
		}

		private static bool ExactLooseItem(GameObject Item, r_KingdomImprovement Receipt)
		{
			return GameObject.Validate(Item) && Item.Physics != null
				&& Item.Physics.InInventory == null && Item.CurrentCell == null
				&& ExactEscrowReference(Receipt, Item)
				&& Item.IDIfAssigned == Receipt.HandoverItemId && Item.Blueprint == Receipt.HandoverItemBlueprint
				&& Item.Count == Receipt.HandoverItemCount;
		}

		private static bool ExactDestination(GameObject Item, GameObject Target, Cell Where,
			r_KingdomImprovement Receipt)
		{
			if (!GameObject.Validate(Item) || !ExactEscrowReference(Receipt, Item)
				|| Item.IDIfAssigned != Receipt.HandoverItemId
				|| Item.Blueprint != Receipt.HandoverItemBlueprint
				|| Item.Count != Receipt.HandoverItemCount) return false;
			if (Receipt.HandoverItemDestinationKind == 1)
				return GameObject.Validate(Target) && ExactItemOwner(Item, Target, Receipt)
					&& Target.IDIfAssigned == Receipt.HandoverItemDestinationId;
			return Receipt.HandoverItemDestinationKind == 2 && Where != null
				&& Item.Physics != null && Item.Physics.InInventory == null
				&& Item.CurrentCell == Where
				&& ReferenceCount(Where.GetObjects(), Item) == 1
				&& CellKey(Where) == Receipt.HandoverItemDestinationId;
		}

		private static bool ExactEscrowReference(r_KingdomImprovement Receipt, GameObject Item)
		{
			object rooted;
			return Receipt != null && GameObject.Validate(Item) && The.Game != null
				&& BoundedEscrowKey(Receipt.HandoverItemEscrowKey)
				&& The.Game.ObjectGameState.TryGetValue(Receipt.HandoverItemEscrowKey, out rooted)
				&& ReferenceEquals(rooted, Item);
		}

		private static int ReferenceCount(IList<GameObject> Objects, GameObject Item)
		{
			if (Objects == null || Item == null) return 0;
			int count = 0;
			for (int i = 0; i < Objects.Count; i++) if (ReferenceEquals(Objects[i], Item)) count++;
			return count;
		}

		private static string CellKey(Cell Where)
		{
			if (Where?.ParentZone == null || string.IsNullOrEmpty(Where.ParentZone.ZoneID)) return null;
			return Where.ParentZone.ZoneID + ":" + Where.X.ToString(CultureInfo.InvariantCulture)
				+ "," + Where.Y.ToString(CultureInfo.InvariantCulture);
		}

		private static bool SettlePendingItem(GameObject Target, Cell Where,
			r_KingdomImprovement Receipt, GameObject Item)
		{
			if (!ExactDestination(Item, Target, Where, Receipt))
				return FailHandover(Receipt, "Inventory destination identity is not exact.");
			if (Receipt.HandoverItemPhase < 3) Receipt.HandoverItemPhase = 3;
			int current = Receipt.HandoverMovedItems;
			if (current == Receipt.HandoverItemMovedBefore)
				Receipt.HandoverMovedItems = Receipt.HandoverItemMovedAfter;
			else if (current != Receipt.HandoverItemMovedAfter)
				return FailHandover(Receipt,
					"Inventory moved count has a third value outside its frozen receipt.");
			if (Receipt.HandoverMovedItems != Receipt.HandoverItemMovedAfter) return false;
			Receipt.HandoverItemPhase = 4;
			return RetirePendingItem(Receipt, Item);
		}

		private static bool RetirePendingItem(r_KingdomImprovement Receipt, GameObject Item)
		{
			string key = Receipt?.HandoverItemEscrowKey;
			object rooted;
			if (The.Game == null || !BoundedEscrowKey(key)
				|| !The.Game.ObjectGameState.TryGetValue(key, out rooted)
				|| !ReferenceEquals(rooted, Item))
				return FailHandover(Receipt,
					"The exact inventory escrow root changed before receipt cleanup.");
			The.Game.ObjectGameState.Remove(key);
			if (The.Game.ObjectGameState.ContainsKey(key))
				return FailHandover(Receipt,
					"The exact inventory escrow root could not be retired after settlement.");
			ClearPendingItem(Receipt);
			return true;
		}

		private static void ClearPendingItem(r_KingdomImprovement Receipt)
		{
			// Phase zero is the commit marker. Stale identity properties are harmless if a save lands
			// between these property writes; no later callback consults them while phase is zero.
			Receipt.HandoverItemPhase = 0;
			Receipt.HandoverItemId = null;
			Receipt.HandoverItemBlueprint = null;
			Receipt.HandoverItemDestinationId = null;
			Receipt.HandoverItemEscrowKey = null;
			Receipt.HandoverItemCount = 0;
			Receipt.HandoverItemDestinationKind = 0;
			Receipt.HandoverItemMovedBefore = 0;
			Receipt.HandoverItemMovedAfter = 0;
		}

	}
}
