using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>One planned lawful walk off a raising's ground, and the ground walked from,
	/// so a set that fails half way through can be put back exactly as it stood.</summary>
	public readonly struct KingdomLayoutDisplacement
	{
		public readonly GameObject Body;
		public readonly Cell Origin;
		public readonly Cell Target;

		public KingdomLayoutDisplacement(GameObject Body, Cell Origin, Cell Target)
		{
			this.Body = Body;
			this.Origin = Origin;
			this.Target = Target;
		}
	}

	public static partial class KingdomPlots
	{
		/// <summary>How far the crew will walk a resident to get them off their own site.</summary>
		private const int OccupantDisplacementRadius = 8;

		/// <summary>
		/// Puts the ground layer down, standing our own residents off the slots when that is what
		/// stands in the way and retrying in the SAME pass, because a body walks back. The player,
		/// a stranger, or one of ours posted into the layout is never moved: that block is said
		/// once and the raising waits for the founder to settle it.
		/// </summary>
		/// <param name="Failure">The stamper refusal left standing, when this returns false.</param>
		internal static bool TryGroundStageWithOccupants(KingdomSystem System, Zone Z,
			GameObject Root, r_KingdomPlotWorks Works, HashSet<int> Managed,
			KingdomArchitectureIntent Authored, KingdomPlotRules.PlotRect Rect, out string Failure)
		{
			string name = Works.DisplayName ?? "work";
			bool ground = KingdomArchitectureStamper.TryStageLayer(Root, Z,
				ArchitectureLayer.Ground, out Failure);
			if (!ground && KingdomPlotRules.IsOccupantSlotRefusal(Failure))
			{
				int cleared = 0;
				KingdomPlotRules.OccupantVerdict verdict = KingdomPlotRules.OccupantVerdict.Clear;
				Cell anchor = null;
				bool stoodOff = KingdomArchitectureStamper.TryBlockingCells(Authored, Z,
						out HashSet<int> blocking, out string clearanceRefusal)
					&& TryClearManagedOccupants(System, Z, Root, Managed, blocking, Rect,
						out cleared, out verdict, out anchor, out clearanceRefusal);
				if (stoodOff)
				{
					ground = KingdomArchitectureStamper.TryStageLayer(Root, Z,
						ArchitectureLayer.Ground, out Failure);
				}
				else KingdomLog.Log("architecture: layout clearance refused: " + clearanceRefusal);
				// Bodies left standing off the site are named whether or not the set succeeded,
				// and the sentence says which of the two the founder is looking at.
				SayPlotWorkCleared(System, Root, name, cleared, ground,
					ground ? null : (stoodOff ? Failure : clearanceRefusal));
				if (!ground && verdict == KingdomPlotRules.OccupantVerdict.AnchorBound
					&& anchor != null)
				{
					SayPlotWorkOccupied(System, Root, "anchor:" + anchor.X + "," + anchor.Y,
						KingdomPlotRules.RefuseOccupiedAnchor(name, anchor.X, anchor.Y));
					return false;
				}
			}
			string slot = ground ? null : KingdomPlotRules.OccupantSlotOf(Failure);
			SayPlotWorkOccupied(System, Root, slot,
				slot == null ? null : KingdomPlotRules.RefuseOccupiedSlot(name, slot));
			return ground;
		}

		/// <summary>
		/// Stands this settlement's own residents off the layout slots of a paid raising so the
		/// ground stage can land. Plan before effect, in the shape the camp builder already uses
		/// (World/KingdomQuickstartCampBuilder.cs:41-65): every occupant is classified and every
		/// destination proved off the layout, off the plot, dry, walkable and unoccupied BEFORE one
		/// body moves; any occupant without a destination refuses the whole set and moves nobody.
		/// The player and any body that is not ours are never moved.
		/// </summary>
		/// <param name="Managed">Layout cell indices (y * Zone.Width + x) the stamper owns.</param>
		/// <param name="Moved">Residents walked off the site.</param>
		/// <param name="Verdict">What the layout's occupants turned out to be.</param>
		/// <param name="Anchor">The post anchor inside the layout, when that is the verdict.</param>
		/// <param name="Refusal">Why nothing was moved, when this returns false.</param>
		internal static bool TryClearManagedOccupants(KingdomSystem System, Zone Z, GameObject Root,
			HashSet<int> Managed, HashSet<int> Blocking, KingdomPlotRules.PlotRect Rect,
			out int Moved, out KingdomPlotRules.OccupantVerdict Verdict, out Cell Anchor,
			out string Refusal)
		{
			Moved = 0;
			Verdict = KingdomPlotRules.OccupantVerdict.Clear;
			Anchor = null;
			Refusal = null;
			if (System == null || Z == null || !GameObject.Validate(Root) || Managed == null
				|| Blocking == null)
				return ClearanceFault("the layout was not witnessed", out Refusal);
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z);
			if (survey == null) return ClearanceFault("no ground survey is in hand", out Refusal);
			List<GameObject> occupants = new List<GameObject>();
			List<KingdomPlotRules.OccupantReason> reasons =
				new List<KingdomPlotRules.OccupantReason>();
			bool player = false;
			int residents = 0;
			// Only Blocked slots are walked: a body on walkable ground or beside an adjacent-use
			// slot is not in the way, so it is not an occupant of this raising at all.
			foreach (int index in Blocking)
			{
				Cell cell = Z.GetCell(index % Z.Width, index / Z.Width);
				if (cell == null) continue;
				List<GameObject> objects = cell.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (!GameObject.Validate(item) || ReferenceEquals(item, Root)) continue;
					// The player is an occupant like any other body: counted, never moved. Passing
					// over them would judge a player-only slot empty and raise the building on them.
					if (!item.IsCreature && !item.IsPlayer()) continue;
					if (occupants.Contains(item)) continue;
					KingdomPlotRules.OccupantReason reason = ReasonFor(System, survey, item);
					occupants.Add(item);
					reasons.Add(reason);
					if (reason == KingdomPlotRules.OccupantReason.Player) { player = true; continue; }
					if (reason != KingdomPlotRules.OccupantReason.Resident) continue;
					residents++;
					Cell anchor = PostAnchorInLayout(Z, item, Managed);
					if (anchor == null) continue;
					reasons[reasons.Count - 1] = KingdomPlotRules.OccupantReason.AnchorBound;
					if (Anchor == null) Anchor = anchor;
				}
			}
			Verdict = KingdomPlotRules.JudgeOccupants(occupants.Count, residents, player,
				Anchor != null);
			if (Verdict == KingdomPlotRules.OccupantVerdict.Clear) return true;
			if (Verdict != KingdomPlotRules.OccupantVerdict.Displace)
			{
				NameOccupants(Z, Blocking, occupants, reasons);
				return ClearanceFault(Verdict == KingdomPlotRules.OccupantVerdict.AnchorBound
					? "a resident is posted inside the layout"
					: "somebody standing there is not the settlement's to move", out Refusal);
			}
			List<KingdomLayoutDisplacement> plan = new List<KingdomLayoutDisplacement>();
			HashSet<Cell> taken = new HashSet<Cell>();
			for (int i = 0; i < occupants.Count; i++)
			{
				Cell target = FreeGroundOffLayout(Z, occupants[i], Managed, Rect, taken);
				if (target == null)
					return ClearanceFault("no free ground beside the site to stand them on",
						out Refusal);
				taken.Add(target);
				plan.Add(new KingdomLayoutDisplacement(occupants[i], occupants[i].CurrentCell,
					target));
			}
			int walked = 0;
			for (int i = 0; i < plan.Count; i++)
			{
				KingdomLayoutDisplacement move = plan[i];
				if (GameObject.Validate(move.Body)
					&& move.Body.SystemLongDistanceMoveTo(move.Target, 0, forced: true,
						ignoreCombat: true)
					&& move.Body.CurrentCell == move.Target)
				{
					walked++;
					KingdomLog.Log("architecture: stood occupant " + move.Body.IDIfAssigned
						+ " off lot " + Root.GetStringProperty(PlotIdProperty) + " onto "
						+ move.Target.X + "," + move.Target.Y);
					continue;
				}
				// Half a cleared site is nobody's intent: put back everyone already walked, and
				// report exactly how many stayed put and how many are still standing off.
				// i is included: a move can return true and still land off-target, leaving that
				// body displaced. Everything in plan[0..i] not standing on its origin comes back.
				int back = WalkBack(plan, i + 1, out int stranded);
				Moved = stranded;
				return ClearanceFault("a settler would not stand off the site; " + back
					+ " stood back" + (stranded > 0
						? " and " + stranded + " could not be stood back" : ""), out Refusal);
			}
			Moved = walked;
			return true;
		}

		/// <summary>Walks every one of the first <paramref name="Count"/> planned bodies that is no
		/// longer standing on its origin back onto it. A body that never left is not touched and
		/// counts as neither. Returns how many stood back; <paramref name="Stranded"/> counts those
		/// that could not.</summary>
		private static int WalkBack(List<KingdomLayoutDisplacement> Plan, int Count,
			out int Stranded)
		{
			int back = 0;
			Stranded = 0;
			for (int i = 0; i < Count; i++)
			{
				KingdomLayoutDisplacement move = Plan[i];
				if (!GameObject.Validate(move.Body)
					|| move.Body.CurrentCell == move.Origin) continue;
				if (move.Origin != null
					&& move.Body.SystemLongDistanceMoveTo(move.Origin, 0, forced: true,
						ignoreCombat: true)
					&& move.Body.CurrentCell == move.Origin)
				{
					back++;
					KingdomLog.Log("architecture: stood occupant " + move.Body.IDIfAssigned
						+ " back onto " + move.Origin.X + "," + move.Origin.Y);
					continue;
				}
				Stranded++;
				KingdomLog.Log("architecture: occupant " + move.Body.IDIfAssigned
					+ " could not be stood back");
			}
			return back;
		}

	}
}
