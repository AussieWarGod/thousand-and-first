using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>One planned lawful walk off a raising's ground.</summary>
	public readonly struct KingdomLayoutDisplacement
	{
		public readonly GameObject Body;
		public readonly Cell Target;

		public KingdomLayoutDisplacement(GameObject Body, Cell Target)
		{
			this.Body = Body;
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
			KingdomPlotRules.PlotRect Rect, out string Failure)
		{
			string name = Works.DisplayName ?? "work";
			bool ground = KingdomArchitectureStamper.TryStageLayer(Root, Z,
				ArchitectureLayer.Ground, out Failure);
			if (!ground && KingdomPlotRules.IsOccupantSlotRefusal(Failure))
			{
				if (TryClearManagedOccupants(System, Z, Root, Managed, Rect, out int cleared,
					out KingdomPlotRules.OccupantVerdict verdict, out Cell anchor,
					out string clearanceRefusal))
				{
					SayPlotWorkCleared(System, Root, name, cleared);
					ground = KingdomArchitectureStamper.TryStageLayer(Root, Z,
						ArchitectureLayer.Ground, out Failure);
				}
				else
				{
					KingdomLog.Log("architecture: layout clearance refused: " + clearanceRefusal);
					if (verdict == KingdomPlotRules.OccupantVerdict.AnchorBound && anchor != null)
					{
						SayPlotWorkOccupied(System, Root, "anchor:" + anchor.X + "," + anchor.Y,
							KingdomPlotRules.RefuseOccupiedAnchor(name, anchor.X, anchor.Y));
						return false;
					}
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
			HashSet<int> Managed, KingdomPlotRules.PlotRect Rect, out int Moved,
			out KingdomPlotRules.OccupantVerdict Verdict, out Cell Anchor, out string Refusal)
		{
			Moved = 0;
			Verdict = KingdomPlotRules.OccupantVerdict.Clear;
			Anchor = null;
			Refusal = null;
			if (System == null || Z == null || !GameObject.Validate(Root) || Managed == null)
				return ClearanceFault("the layout was not witnessed", out Refusal);
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z);
			if (survey == null) return ClearanceFault("no ground survey is in hand", out Refusal);
			List<GameObject> occupants = new List<GameObject>();
			bool player = false;
			int residents = 0;
			foreach (int index in Managed)
			{
				Cell cell = Z.GetCell(index % Z.Width, index / Z.Width);
				if (cell == null) continue;
				List<GameObject> objects = cell.GetObjects();
				for (int i = 0; i < objects.Count; i++)
				{
					GameObject item = objects[i];
					if (!GameObject.Validate(item) || ReferenceEquals(item, Root)) continue;
					if (item.IsPlayer()) { player = true; continue; }
					if (!item.IsCreature || occupants.Contains(item)) continue;
					occupants.Add(item);
					if (!IsOwnResident(System, survey, item)) continue;
					residents++;
					Cell anchor = PostAnchorInLayout(Z, item, Managed);
					if (anchor != null && Anchor == null) Anchor = anchor;
				}
			}
			Verdict = KingdomPlotRules.JudgeOccupants(occupants.Count, residents, player,
				Anchor != null);
			if (Verdict == KingdomPlotRules.OccupantVerdict.Clear) return true;
			if (Verdict != KingdomPlotRules.OccupantVerdict.Displace)
				return ClearanceFault(Verdict == KingdomPlotRules.OccupantVerdict.AnchorBound
					? "a resident is posted inside the layout"
					: "somebody standing there is not the settlement's to move", out Refusal);
			List<KingdomLayoutDisplacement> plan = new List<KingdomLayoutDisplacement>();
			HashSet<Cell> taken = new HashSet<Cell>();
			for (int i = 0; i < occupants.Count; i++)
			{
				Cell target = FreeGroundOffLayout(Z, occupants[i], Managed, Rect, taken);
				if (target == null)
					return ClearanceFault("no free ground beside the site to stand them on",
						out Refusal);
				taken.Add(target);
				plan.Add(new KingdomLayoutDisplacement(occupants[i], target));
			}
			for (int i = 0; i < plan.Count; i++)
			{
				KingdomLayoutDisplacement move = plan[i];
				if (!GameObject.Validate(move.Body)
					|| !move.Body.SystemLongDistanceMoveTo(move.Target, 0, forced: true,
						ignoreCombat: true)
					|| move.Body.CurrentCell != move.Target)
					return ClearanceFault("a settler would not stand off the site", out Refusal);
				Moved++;
				KingdomLog.Log("architecture: stood occupant " + move.Body.IDIfAssigned
					+ " off lot " + Root.GetStringProperty(PlotIdProperty) + " onto "
					+ move.Target.X + "," + move.Target.Y);
			}
			return true;
		}

		private static bool ClearanceFault(string Message, out string Refusal)
		{
			Refusal = Message;
			return false;
		}

		/// <summary>
		/// A body this settlement may stand off its own building site: one of our surveyed
		/// settlers, never staged for a happening, and where it carries a roll id, a resident in
		/// standing on that roll.
		/// </summary>
		private static bool IsOwnResident(KingdomSystem System, KingdomSurvey Survey,
			GameObject Body)
		{
			if (Body.IsPlayer() || Body.IsPlayerLed() || !Survey.Settlers.Contains(Body)
				|| Simulation.City.KingdomPhysicalHappenings.IsStaged(Body)) return false;
			int id = Simulation.City.KingdomResidents.IdOf(Body);
			if (id <= 0) return true;
			return Simulation.City.KingdomResidents.TryResident(System.City, id,
				out Simulation.City.KingdomResidentRow row)
				&& row.Standing == Simulation.City.KingdomResidentStanding.Resident;
		}

		/// <summary>The body's post anchor when it lies on a layout slot, else null.</summary>
		private static Cell PostAnchorInLayout(Zone Z, GameObject Body, HashSet<int> Managed)
		{
			XRL.World.Parts.Brain brain = Body.Brain;
			XRL.World.GlobalLocation anchor = brain?.StartingCell;
			if (anchor == null || anchor.World == null
				|| !string.Equals(anchor.ZoneID, Z.ZoneID, global::System.StringComparison.Ordinal))
				return null;
			Cell cell = Z.GetCell(anchor.CellX, anchor.CellY);
			return cell != null && Managed.Contains(cell.Y * Z.Width + cell.X) ? cell : null;
		}

		/// <summary>Nearest free walkable ground off the raising, or null when there is none.</summary>
		private static Cell FreeGroundOffLayout(Zone Z, GameObject Body, HashSet<int> Managed,
			KingdomPlotRules.PlotRect Rect, HashSet<Cell> Taken)
		{
			Cell from = Body.CurrentCell;
			if (from == null) return null;
			for (int radius = 1; radius <= OccupantDisplacementRadius; radius++)
				for (int dy = -radius; dy <= radius; dy++)
					for (int dx = -radius; dx <= radius; dx++)
					{
						if (dx > -radius && dx < radius && dy > -radius && dy < radius) continue;
						Cell candidate = Z.GetCell(from.X + dx, from.Y + dy);
						if (candidate == null || Taken.Contains(candidate)
							|| Managed.Contains(candidate.Y * Z.Width + candidate.X)
							|| candidate.X >= Rect.X1 && candidate.X <= Rect.X2
								&& candidate.Y >= Rect.Y1 && candidate.Y <= Rect.Y2
							|| candidate.HasOpenLiquidVolume()
							|| !candidate.IsPassable(Body) || HoldsLivingBody(candidate)) continue;
						return candidate;
					}
			return null;
		}

		private static bool HoldsLivingBody(Cell Cell)
		{
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (GameObject.Validate(item) && (item.IsCreature || item.IsPlayer())) return true;
			}
			return false;
		}
	}
}
