using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		private static bool TryCreateFreshGrant(XRLGame Game,
			Func<KingdomQuickstartGrantScope<GameObject>, GameObject> PrepareAndPlace,
			Func<GameObject, bool> Verify, out GameObject Grant)
		{
			string token = Guid.NewGuid().ToString("N");
			bool ownsQuarantine = false;
			return KingdomQuickstartGrantScope<GameObject>.TryExecute(PrepareAndPlace,
				Verify, RemoveFreshGrantObject, () => GrantQuarantined(Game),
				() => ownsQuarantine = QuarantineGrant(Game, token),
				() =>
				{
					if (ownsQuarantine && string.Equals(Game.GetStringGameState(
						KingdomQuickstartRules.QuarantineState, null), token, StringComparison.Ordinal))
						Game.StringGameState.Remove(KingdomQuickstartRules.QuarantineState);
				}, out Grant);
		}

		private static bool GrantQuarantined(XRLGame Game)
		{
			string key = KingdomQuickstartRules.QuarantineState;
			return Game == null || (Game.StringGameState?.ContainsKey(key) ?? false)
				|| (Game.IntGameState?.ContainsKey(key) ?? false)
				|| (Game.Int64GameState?.ContainsKey(key) ?? false)
				|| (Game.BooleanGameState?.ContainsKey(key) ?? false)
				|| (Game.ObjectGameState?.ContainsKey(key) ?? false);
		}

		private static bool QuarantineGrant(XRLGame Game, string Token)
		{
			if (Game == null) throw new InvalidOperationException("Quickstart lost its game.");
			if (GrantQuarantined(Game)) return false;
			Game.SetStringGameState(KingdomQuickstartRules.QuarantineState, Token);
			if (!string.Equals(Game.GetStringGameState(KingdomQuickstartRules.QuarantineState,
				null), Token, StringComparison.Ordinal))
				throw new InvalidOperationException("Quickstart quarantine did not persist.");
			return true;
		}

		private static bool RemoveFreshGrantObject(GameObject Object)
		{
			if (Object == null) return true;
			// Children allocated by this attempt were visited first. Any contents left now
			// are unproved custody: never discover ownership by walking a container.
			if (HasUnprovedGrantContents(Object)) return false;
			Cell cell = Object.CurrentCell;
			IEnumerable<GameObject> inventory = Object.InInventory?.Inventory?.Objects;
			GameObject wearer = Object.Equipped;
			try { if (GameObject.Validate(Object)) Object.Obliterate(null, Silent: true); }
			catch { /* A callback may throw after removal; measure exact custody below. */ }
			return !HasUnprovedGrantContents(Object)
				&& !GameObject.Validate(Object) && Object.CurrentCell == null
				&& Object.InInventory == null && Object.Equipped == null
				&& !ContainsExact(cell?.GetObjects(), Object)
				&& !ContainsExact(inventory, Object)
				&& !ContainsExact(wearer?.Body?.GetEquippedObjects(), Object);
		}

		private static bool HasUnprovedGrantContents(GameObject Object)
		{
			if (Object.Inventory != null && Object.Inventory.Objects.Count != 0) return true;
			if (Object.Body != null)
				foreach (GameObject equipped in Object.Body.GetEquippedObjects())
					if (GameObject.Validate(equipped)) return true;
			return false;
		}

		private static bool ContainsExact(IEnumerable<GameObject> Objects, GameObject Target)
		{
			if (Objects != null)
				foreach (GameObject item in Objects)
					if (ReferenceEquals(item, Target)) return true;
			return false;
		}
	}
}
