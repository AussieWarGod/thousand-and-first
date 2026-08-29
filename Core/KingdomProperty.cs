using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Charter UI and bounded ground selection for explicit realm property.</summary>
	public static partial class KingdomProperty
	{
		private sealed class NearbyObject
		{
			internal GameObject Object;
			internal string Label;
		}

		public static void Open(KingdomSystem System, GameObject Founder)
		{
			Zone zone = Founder?.CurrentZone;
			Cell origin = Founder?.CurrentCell;
			if (System == null || !System.Founded || zone == null || origin == null
				|| origin.ParentZone != zone || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("Realm property is designated on the kingdom's own ground.");
				return;
			}
			List<NearbyObject> rows;
			string failure;
			if (!TryNearby(System, Founder, origin, out rows, out failure))
			{
				Popup.Show(failure);
				return;
			}
			if (rows.Count == 0)
			{
				Popup.Show("Put one whole, takeable object you own at your feet or beside you. "
					+ "Nothing else becomes realm property merely because the ground is claimed.");
				return;
			}
			string[] options = new string[rows.Count];
			for (int i = 0; i < rows.Count; i++) options[i] = rows[i].Label;
			int pick = Popup.PickOption(Title: "Property of "
				+ KingdomPresentation.Rich(System.SeatName),
				Intro: "Designation is exact and reversible. Qud's native ownership law warns "
					+ "and calls nearby members for theft or damage. Choosing a private object also "
					+ "records that exact object's durable physical identity.",
				Options: options, AllowEscape: true);
			if (pick < 0 || pick >= rows.Count) return;
			GameObject selected = rows[pick].Object;
			if (!StillNearby(Founder, selected, zone))
			{
				Popup.Show("That exact object moved before the Charter could be written.");
				return;
			}
			r_KingdomProperty receipt = selected.GetPart<r_KingdomProperty>();
			bool releasing = receipt != null && (receipt.Phase == KingdomPropertyPhase.Designated
				|| receipt.Phase == KingdomPropertyPhase.ReleasePrepared);
			bool changed;
			if (releasing)
			{
				changed = TryRelease(System, Founder, selected, out failure);
			}
			else
			{
				changed = TryDesignate(System, Founder, selected, out failure);
			}
			if (!changed)
			{
				Popup.Show(failure ?? "The property record did not change.");
				return;
			}
			KingdomGovernanceScope.Commit(releasing
				? "release realm property" : "designate realm property");
			r_KingdomProperty after = selected.GetPart<r_KingdomProperty>();
			Popup.Show(after == null || after.Phase == KingdomPropertyPhase.Released
				? "The " + selected.ShortDisplayName + " is private property again."
				: "The " + selected.ShortDisplayName + " is entered as property of "
					+ KingdomPresentation.Rich(System.SeatName) + ".");
		}

		private static bool TryNearby(KingdomSystem System, GameObject Founder, Cell Origin,
			out List<NearbyObject> Rows, out string Failure)
		{
			Rows = new List<NearbyObject>();
			Failure = null;
			List<Cell> cells = new List<Cell> { Origin };
			foreach (Cell cell in Origin.GetLocalAdjacentCells())
				if (cell != null && !cells.Contains(cell)) cells.Add(cell);
			HashSet<GameObject> seen = new HashSet<GameObject>();
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int c = 0; c < cells.Count; c++)
			{
				List<GameObject> objects = cells[c].GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (!GameObject.Validate(item) || ReferenceEquals(item, Founder)
						|| !seen.Add(item) || item.CurrentCell != cells[c]) continue;
					r_KingdomProperty receipt = item.GetPart<r_KingdomProperty>();
					if (!Candidate(System, Founder, item, receipt)) continue;
					string assignedId = item.IDIfAssigned;
					if (!string.IsNullOrEmpty(assignedId) && !ids.Add(assignedId))
					{
						Failure = "Two nearby property candidates already share one physical identity.";
						Rows.Clear();
						return false;
					}
					if (Rows.Count >= KingdomPropertyRules.MaxNearbyCandidates)
					{
						Failure = "More nearby objects are eligible than one Charter page can name. "
							+ "Move the intended object apart from the pile.";
						Rows.Clear();
						return false;
					}
					string state = receipt == null ? "{{K|[private]}}"
						: "{{G|[realm property]}}";
					Rows.Add(new NearbyObject { Object = item,
						Label = item.ShortDisplayName + " " + state + " {{K|at "
							+ item.CurrentCell.X + "," + item.CurrentCell.Y + "}}" });
				}
			}
			Rows.Sort(delegate(NearbyObject left, NearbyObject right)
			{
				return string.Compare(left.Object.IDIfAssigned, right.Object.IDIfAssigned,
					StringComparison.Ordinal);
			});
			return true;
		}

		private static bool Candidate(KingdomSystem System, GameObject Founder,
			GameObject Item, r_KingdomProperty Receipt)
		{
			if (Receipt != null)
				return Receipt.Phase == KingdomPropertyPhase.Prepared
					|| Receipt.Phase == KingdomPropertyPhase.Designated
					|| Receipt.Phase == KingdomPropertyPhase.ReleasePrepared;
			return KingdomPropertyRules.JudgeDesignation(System.Founded, true,
				Founder.IsPlayer(), Item.Physics != null, Item.IsCreature, Item.IsImportant(),
				Item.IsTakeable(), FounderOwned(Item), Item.Physics?.Owner,
				System.KingdomFactionName, false) == KingdomPropertyVerdict.Allowed;
		}

		private static bool FounderOwned(GameObject Item)
		{
			return GameObject.Validate(Item) && (Item.OwnedByPlayer
				|| Item.GetIntProperty("DroppedByPlayer") > 0);
		}

		private static bool StillNearby(GameObject Founder, GameObject Item, Zone Zone)
		{
			if (!GameObject.Validate(Founder) || !Founder.IsPlayer()
				|| !GameObject.Validate(Item) || Item.CurrentZone != Zone
				|| Item.CurrentCell == null || Founder.CurrentCell == null) return false;
			int dx = Math.Abs(Item.CurrentCell.X - Founder.CurrentCell.X);
			int dy = Math.Abs(Item.CurrentCell.Y - Founder.CurrentCell.Y);
			return dx <= 1 && dy <= 1;
		}
	}
}
