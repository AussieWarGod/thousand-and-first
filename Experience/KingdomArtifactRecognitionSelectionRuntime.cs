using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal sealed class KingdomArtifactRecognitionChoice
	{
		internal GameObject Object;
		internal string Label;
		internal int X;
		internal int Y;
		internal string Blueprint;
		internal string DisplayName;
		internal int Order;
	}

	/// <summary>Bounded explicit-ground selector for D6. Qud 2.0.211.51 evidence: Cell.cs
	/// 4854-4857 returns only that cell's root objects; 7443-7462 bounds local adjacency;
	/// GameObject.cs 424-434 owns IDIfAssigned, the only identity read that does not create one,
	/// and 515-532 owns holder/cell/zone truth. This selector never reads GameObject.ID: that
	/// getter (436-448) writes the id property and its BaseID fallback (400-417) advances the
	/// save's sequence, so looking would change the object and the save.</summary>
	internal static partial class KingdomArtifactRecognitionSelectionRuntime
	{
		internal const int MaxNearbyChoices = 64;

		internal static bool TryCollectNearby(GameObject Founder,
			out List<KingdomArtifactRecognitionChoice> Choices, out string Failure)
		{
			return TryCollectNearby(Founder, out Choices, out _, out Failure);
		}

		/// <summary>
		/// The bounded ground read, plus how many nearby things were passed over because the world
		/// has never identified them.
		/// <para>
		/// That count is reported rather than swallowed. Those objects are skipped precisely because
		/// asking them for an identity would create one, and a founder who is shown an empty page
		/// with no reason cannot tell "there is nothing here" from "the thing you meant cannot be
		/// named yet".
		/// </para>
		/// </summary>
		internal static bool TryCollectNearby(GameObject Founder,
			out List<KingdomArtifactRecognitionChoice> Choices, out int Unidentified,
			out string Failure)
		{
			Choices = new List<KingdomArtifactRecognitionChoice>();
			Unidentified = 0;
			Failure = null;
			Cell origin = Founder?.CurrentCell;
			Zone zone = Founder?.CurrentZone;
			if (!GameObject.Validate(Founder) || !Founder.IsPlayer() || origin == null
				|| zone == null || !ReferenceEquals(origin.ParentZone, zone))
			{
				Failure = "Stand beside the exact object to be recognized.";
				return false;
			}
			List<Cell> cells = new List<Cell> { origin };
			List<Cell> adjacent = origin.GetLocalAdjacentCells();
			for (int i = 0; i < adjacent.Count; i++)
				if (adjacent[i] != null && !cells.Contains(adjacent[i])) cells.Add(adjacent[i]);
			HashSet<GameObject> seen = new HashSet<GameObject>();
			int order = 0;
			for (int c = 0; c < cells.Count; c++)
			{
				List<GameObject> roots = cells[c].GetObjects();
				for (int i = 0; i < roots.Count; i++)
				{
					GameObject item = roots[i];
					if (!Eligible(Founder, item, cells[c], zone) || !seen.Add(item)) continue;
					if (string.IsNullOrEmpty(item.IDIfAssigned))
					{
						Unidentified++;
						continue;
					}
					if (Choices.Count >= MaxNearbyChoices)
					{
						Choices.Clear();
						Failure = "More nearby objects are eligible than one recognition page can name. "
							+ "Move the intended object apart from the pile.";
						return false;
					}
					Choices.Add(new KingdomArtifactRecognitionChoice
					{
						Object = item,
						Label = item.ShortDisplayName + " {{K|at " + cells[c].X + ","
							+ cells[c].Y + "}}",
						X = cells[c].X,
						Y = cells[c].Y,
						Blueprint = item.Blueprint,
						DisplayName = item.ShortDisplayNameStripped,
						Order = order++
					});
				}
			}
			Choices.Sort(CompareChoices);
			return true;
		}

		internal static bool TrySnapshotNearby(GameObject Founder, GameObject Selected,
			string DeedId, string DeedText, long Tick, out KingdomArtifactSnapshot Snapshot,
			out string Failure)
		{
			Snapshot = null;
			Failure = null;
			Cell cell = Selected?.CurrentCell;
			Zone zone = Selected?.CurrentZone;
			if (!Eligible(Founder, Selected, cell, Founder?.CurrentZone)
				|| !StillNearby(Founder, Selected))
			{
				Failure = "The exact selected object is no longer beside the Charter bearer.";
				return false;
			}
			// IDIfAssigned, never ID. Reading GameObject.ID would mint an identity on an object that
			// had none (GameObject.cs 436-448 writes the property; 400-417 advances the save's
			// sequence), so the act of looking would change both the object and the save. An
			// unidentified object is refused instead, and the counter is left exactly where it was.
			string engineId = Selected.IDIfAssigned;
			if (string.IsNullOrEmpty(engineId))
			{
				Failure = "The world has never given that thing an exact identity of its own, so "
					+ "there is nothing the city could name it by. It cannot be recognized.";
				return false;
			}
			if (!UniqueNearbyIdentity(Founder, Selected, engineId))
			{
				Failure = "Another nearby object claims the selected object's exact identity.";
				return false;
			}
			GameObject holder = Selected.Holder;
			string owner = Selected.Physics.Owner;
			string blueprint = Selected.Blueprint;
			string display = Selected.ShortDisplayNameStripped;
			int count = Selected.Count;
			if (!KingdomArtifactRecognitionRuntime.TrySnapshotExplicit(Selected, DeedId,
				DeedText, Tick, out Snapshot, out Failure)) return false;
			if (!GameObject.Validate(Selected) || !ReferenceEquals(Selected.CurrentCell, cell)
				|| !ReferenceEquals(Selected.CurrentZone, zone)
				|| !ReferenceEquals(Selected.Holder, holder) || Selected.Physics.Owner != owner
				|| Selected.Blueprint != blueprint || Selected.ShortDisplayNameStripped != display
				|| Selected.Count != count || Selected.IDIfAssigned != engineId
				|| Snapshot.ObjectId != "taf:object:" + engineId)
			{
				Snapshot = null;
				Failure = "The selected object changed while its non-custodial snapshot was read.";
				return false;
			}
			return true;
		}

		private static bool Eligible(GameObject Founder, GameObject Item, Cell Cell, Zone Zone)
		{
			return GameObject.Validate(Founder) && Founder.IsPlayer()
				&& GameObject.Validate(Item) && !ReferenceEquals(Item, Founder)
				&& Cell != null && Zone != null && ReferenceEquals(Item.CurrentCell, Cell)
				&& ReferenceEquals(Item.CurrentZone, Zone) && Item.Physics != null
				&& Item.Holder == null && !Item.IsCreature && Item.Count == 1
				&& !string.IsNullOrEmpty(Item.Blueprint)
				&& !string.IsNullOrWhiteSpace(Item.ShortDisplayNameStripped);
		}

		private static bool StillNearby(GameObject Founder, GameObject Item)
		{
			if (!GameObject.Validate(Founder) || !GameObject.Validate(Item)
				|| Founder.CurrentCell == null || Item.CurrentCell == null
				|| !ReferenceEquals(Founder.CurrentZone, Item.CurrentZone)) return false;
			return Math.Abs(Founder.CurrentCell.X - Item.CurrentCell.X) <= 1
				&& Math.Abs(Founder.CurrentCell.Y - Item.CurrentCell.Y) <= 1;
		}

		private static bool UniqueNearbyIdentity(GameObject Founder, GameObject Selected,
			string EngineId)
		{
			if (string.IsNullOrEmpty(EngineId) || EngineId.IndexOf('\0') >= 0) return false;
			Cell origin = Founder?.CurrentCell;
			if (origin == null) return false;
			List<Cell> cells = new List<Cell> { origin };
			List<Cell> adjacent = origin.GetLocalAdjacentCells();
			for (int i = 0; i < adjacent.Count; i++)
				if (adjacent[i] != null && !cells.Contains(adjacent[i])) cells.Add(adjacent[i]);
			int matches = 0;
			for (int c = 0; c < cells.Count; c++)
			{
				List<GameObject> roots = cells[c].GetObjects();
				for (int i = 0; i < roots.Count; i++)
					if (GameObject.Validate(roots[i]) && roots[i].IDIfAssigned == EngineId
						&& ++matches > 1) return false;
			}
			return matches == 1 && Selected.IDIfAssigned == EngineId;
		}

		private static int CompareChoices(KingdomArtifactRecognitionChoice Left,
			KingdomArtifactRecognitionChoice Right)
		{
			int compare = Left.X.CompareTo(Right.X);
			if (compare != 0) return compare;
			compare = Left.Y.CompareTo(Right.Y);
			if (compare != 0) return compare;
			compare = string.CompareOrdinal(Left.Blueprint, Right.Blueprint);
			if (compare != 0) return compare;
			compare = string.CompareOrdinal(Left.DisplayName, Right.DisplayName);
			return compare != 0 ? compare : Left.Order.CompareTo(Right.Order);
		}
	}
}
