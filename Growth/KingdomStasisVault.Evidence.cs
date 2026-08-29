using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomStasisVault
	{
		private static GameObject CurrentDominator(GameObject Subject)
		{
			Dominated effect = Subject?.GetEffect<Dominated>();
			GameObject body = effect?.Dominator;
			Dominating projection = body?.GetEffect<Dominating>();
			return GameObject.Validate(body) && projection?.Target == Subject ? body : null;
		}

		private static string LotOf(GameObject Vault)
		{
			string lot = Vault?.GetStringProperty(KingdomArchitectureStamper.LotIdProperty);
			return string.IsNullOrEmpty(lot)
				? Vault?.GetStringProperty(KingdomPlots.PlotIdProperty) : lot;
		}

		private static List<GameObject> Cradles(GameObject Vault, bool Assign)
		{
			List<GameObject> rows = new List<GameObject>();
			Zone zone = Vault?.CurrentZone;
			string lot = LotOf(Vault);
			if (zone == null || string.IsNullOrEmpty(lot)) return rows;
			foreach (GameObject item in zone.GetObjects())
				if (GameObject.Validate(item) && item.Blueprint == "r_KingdomStasisCradle"
					&& item.GetStringProperty(KingdomPlots.PlotIdProperty) == lot) rows.Add(item);
			rows.Sort(CompareCells);
			if (Assign && rows.Count == KingdomStasisVaultRules.MaxSlots)
			{
				bool clean = true;
				bool[] used = new bool[KingdomStasisVaultRules.MaxSlots];
				for (int i = 0; i < rows.Count; i++)
				{
					int stored = rows[i].GetIntProperty(BayIndexProperty);
					if (stored == 0) continue;
					if (stored < 1 || stored > used.Length || used[stored - 1]) clean = false;
					else used[stored - 1] = true;
				}
				if (clean)
					for (int i = 0; i < rows.Count; i++)
						if (rows[i].GetIntProperty(BayIndexProperty) == 0)
							rows[i].SetIntProperty(BayIndexProperty, i + 1);
			}
			return rows;
		}

		private static int CompareCells(GameObject Left, GameObject Right)
		{
			Cell a = Left?.CurrentCell;
			Cell b = Right?.CurrentCell;
			int byX = (a?.X ?? -1).CompareTo(b?.X ?? -1);
			if (byX != 0) return byX;
			int byY = (a?.Y ?? -1).CompareTo(b?.Y ?? -1);
			if (byY != 0) return byY;
			return string.Compare(Left?.ID, Right?.ID, StringComparison.Ordinal);
		}

		private static GameObject CradleAt(GameObject Vault, int Slot)
		{
			List<GameObject> rows = Cradles(Vault, true);
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].GetIntProperty(BayIndexProperty) == Slot + 1) return rows[i];
			return null;
		}

		private static bool CradleCellClear(GameObject Cradle, GameObject Body)
		{
			Cell cell = Cradle?.CurrentCell;
			if (cell == null || Body?.CurrentCell != cell) return false;
			foreach (GameObject item in cell.GetObjects())
			{
				if (item == Cradle || item == Body || !GameObject.Validate(item)) continue;
				int phase = item.GetPhase();
				if ((phase == 2 || phase == 3) && Stasis.EligibleForStasis(item)) return false;
			}
			return true;
		}

		private static bool CanPhaseIn(GameObject Body)
		{
			Cell cell = Body?.CurrentCell;
			if (cell == null) return false;
			foreach (GameObject item in cell.GetObjectsWithPart("Physics"))
			{
				if (item == Body || item.Physics == null || !item.Physics.Solid
					|| (item.HasTagOrProperty("Flyover") && Body.IsFlying)) continue;
				return false;
			}
			return true;
		}

		private static string InventoryFingerprint(GameObject Body)
		{
			List<string> rows = new List<string>();
			if (Body?.Inventory != null)
				foreach (GameObject item in Body.Inventory.GetObjects())
					if (GameObject.Validate(item)) rows.Add(ObjectRow(item));
			rows.Sort(StringComparer.Ordinal);
			return KingdomStasisVaultRules.Fingerprint(rows.ToArray());
		}

		private static string EquipmentFingerprint(GameObject Body)
		{
			List<string> rows = new List<string>();
			if (Body?.Body != null)
				foreach (GameObject item in Body.Body.GetEquippedObjects())
					if (GameObject.Validate(item)) rows.Add(ObjectRow(item));
			rows.Sort(StringComparer.Ordinal);
			return KingdomStasisVaultRules.Fingerprint(rows.ToArray());
		}

		private static string EffectFingerprint(GameObject Body)
		{
			List<string> rows = new List<string>();
			if (Body != null)
				foreach (Effect effect in Body.Effects)
					if (!(effect is Stasis) && !(effect is Phased)
						&& !(effect is Dominating) && !(effect is Dominated))
						rows.Add(effect.GetType().FullName);
			rows.Sort(StringComparer.Ordinal);
			return KingdomStasisVaultRules.Fingerprint(rows.ToArray());
		}

		private static string ObjectRow(GameObject Item)
		{
			return Item.ID + "|" + (Item.Blueprint ?? "") + "|" + Item.Count;
		}
	}
}
