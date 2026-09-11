using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		/// <summary>
		/// A raising whose applied stage has stopped short of Done and whose labour target does not
		/// reach past it is stalled in a way nothing else logs. Said only when the labour is fully
		/// PAID (a plot merely accumulating ticks between stages is working, not stalled) and only
		/// when the pair has changed since the last line for this plot, so a stall costs one line,
		/// not one line per plot per pass.
		/// </summary>
		private static void SayPlotStageWaiting(r_KingdomPlotWorks Works,
			KingdomPlotRules.PlotStage Target)
		{
			GameObject root = Works.ParentObject;
			if (root == null) return;
			long remaining = TryGetPlotWorkLong(root, PlotWorkRemainingProperty, out long owed)
				? owed : -1L;
			string pair = KingdomPlotRules.StageWaitingPair(Works.StageApplied, (int)Target);
			if (!KingdomPlotRules.ShouldSayStageWaiting(remaining, Works.StageApplied,
				(int)KingdomPlotRules.PlotStage.Done,
				root.GetStringProperty(PlotStageWaitingLastProperty), pair)) return;
			root.SetStringProperty(PlotStageWaitingLastProperty, pair);
			KingdomLog.Log("plot stage waiting: " + (Works.DisplayName ?? "work")
				+ " lot " + root.GetStringProperty(PlotIdProperty)
				+ " applied=" + (KingdomPlotRules.PlotStage)Works.StageApplied
				+ " target=" + Target);
		}

		/// <summary>
		/// One line per body that refused the raising: who, what, where, which blocking slot, its
		/// declared passability and which test the body failed. The summary sentence the harness
		/// parses is emitted separately and never grows.
		/// </summary>
		private static void NameOccupants(Zone Z, Dictionary<int, ArchitecturePassability> Slots,
			List<GameObject> Occupants, List<KingdomPlotRules.OccupantReason> Reasons)
		{
			for (int i = 0; i < Occupants.Count && i < Reasons.Count; i++)
			{
				GameObject body = Occupants[i];
				if (!GameObject.Validate(body)) continue;
				Cell at = body.CurrentCell;
				int index = at == null ? -1 : at.Y * Z.Width + at.X;
				// The passability printed is the one the MAP declares for the cell this body is
				// actually standing on, read from the same lookup that decided whether it blocks.
				ArchitecturePassability passability = ArchitecturePassability.Walkable;
				bool authored = index >= 0 && Slots.TryGetValue(index, out passability);
				KingdomLog.Log("architecture: occupant " + body.IDIfAssigned + " ("
					+ body.Blueprint + ") at " + (at == null ? "nowhere" : at.X + "," + at.Y)
					+ " on " + (authored && KingdomPlotRules.SlotBlocksOccupant(passability)
						? "a blocked slot" : "no blocked slot")
					+ " passability=" + (authored ? passability.ToString() : "none")
					+ " reason=" + Reasons[i]);
			}
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
		private static KingdomPlotRules.OccupantReason ReasonFor(KingdomSystem System,
			KingdomSurvey Survey, GameObject Body)
		{
			if (Body.IsPlayer()) return KingdomPlotRules.OccupantReason.Player;
			if (Body.IsPlayerLed()) return KingdomPlotRules.OccupantReason.PlayerLed;
			if (!Survey.Settlers.Contains(Body)) return KingdomPlotRules.OccupantReason.NotOurs;
			if (Simulation.City.KingdomPhysicalHappenings.IsStaged(Body))
				return KingdomPlotRules.OccupantReason.Staged;
			// Fails closed: a surveyed body with no roll id is not a PROVEN resident, and an
			// unproven body is never shoved. The roster mints an id for every settler it reads.
			int id = Simulation.City.KingdomResidents.IdOf(Body);
			if (id <= 0) return KingdomPlotRules.OccupantReason.NoRoll;
			return Simulation.City.KingdomResidents.TryResident(System.City, id,
				out Simulation.City.KingdomResidentRow row)
				&& row.Standing == Simulation.City.KingdomResidentStanding.Resident
					? KingdomPlotRules.OccupantReason.Resident
					: KingdomPlotRules.OccupantReason.NotResident;
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
