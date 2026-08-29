using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		private static string EscrowKey(KingdomRelocationReceipt Receipt, string Id,
			bool Clearance)
		{
			return EscrowPrefix + Receipt.PlanId + ":" + (Clearance ? "c:" : "p:") + Id;
		}

		private static GameObject Escrow(KingdomRelocationReceipt Receipt, string Id,
			bool Clearance)
		{
			if (The.Game == null) return null;
			return The.Game.ObjectGameState.TryGetValue(EscrowKey(Receipt, Id, Clearance),
				out object rooted) ? rooted as GameObject : null;
		}

		private static bool RootEscrow(KingdomRelocationReceipt Receipt, GameObject Item,
			bool Clearance, out string Failure)
		{
			Failure = null;
			if (The.Game == null || !GameObject.Validate(Item))
			{ Failure = "The game cannot root relocation identity."; return false; }
			string key = EscrowKey(Receipt, Item.ID, Clearance);
			if (The.Game.ObjectGameState.TryGetValue(key, out object existing)
				&& !ReferenceEquals(existing, Item))
			{ Failure = "Relocation escrow identity is already occupied."; return false; }
			The.Game.SetObjectGameState(key, Item);
			return The.Game.ObjectGameState.TryGetValue(key, out existing)
				&& ReferenceEquals(existing, Item);
		}

		private static bool ClearEscrow(KingdomRelocationReceipt Receipt, string Id,
			bool Clearance, GameObject Expected)
		{
			if (The.Game == null) return false;
			string key = EscrowKey(Receipt, Id, Clearance);
			if (!The.Game.ObjectGameState.TryGetValue(key, out object existing)) return true;
			if (Expected != null && !ReferenceEquals(existing, Expected)) return false;
			The.Game.ObjectGameState.Remove(key);
			return !The.Game.ObjectGameState.ContainsKey(key);
		}

		private static bool ExactAt(GameObject Item, Zone Zone, string Blueprint, int X, int Y)
		{
			return GameObject.Validate(Item) && Item.Blueprint == Blueprint
				&& Item.CurrentZone == Zone && Item.CurrentCell == Zone.GetCell(X, Y);
		}
	}
}
