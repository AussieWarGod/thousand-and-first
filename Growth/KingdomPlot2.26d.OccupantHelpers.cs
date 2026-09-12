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
			bool ours = Survey.Settlers.Contains(Body);
			// Fails closed: a surveyed body with no roll id is not a PROVEN resident, and an
			// unproven body is never shoved. The roster mints an id for every settler it reads.
			int id = ours ? Simulation.City.KingdomResidents.IdOf(Body) : 0;
			bool standing = id > 0
				&& Simulation.City.KingdomResidents.TryResident(System.City, id,
					out Simulation.City.KingdomResidentRow row)
				&& row.Standing == Simulation.City.KingdomResidentStanding.Resident;
			return KingdomPlotRules.JudgeOccupant(new KingdomPlotRules.OccupantFacts(
				Body.IsPlayer(), Body.IsPlayerLed(),
				Simulation.City.KingdomPhysicalHappenings.IsStaged(Body), ours,
				Body.HasProperName, Body.IsMerchant(), AnimalKind(Body), id > 0, standing));
		}

		/// <summary>
		/// Whether this body's blueprint descends from the base "Animal" blueprint. Inheritance is
		/// the engine's own kind test (GameObjectBlueprint.InheritsFrom, ILSpy 9.1 of core
		/// 2.0.211.51, XRL/World/GameObjectBlueprint.cs:237-249) and is what separates a croc
		/// (Croc -> BaseReptile -> Animal -> Creature) from the villagers, merchants and named NPCs
		/// who descend from Humanoid. A blueprint the factory does not know is not an animal.
		/// </summary>
		private static bool AnimalKind(GameObject Body)
		{
			XRL.World.GameObjectBlueprint blueprint = Body.GetBlueprint(false);
			return blueprint != null && (blueprint.Name == AnimalBlueprint
				|| blueprint.InheritsFrom(AnimalBlueprint));
		}

		/// <summary>
		/// Whether this body's post could be moved with it. Mirrors the engine's own guard on
		/// Brain.Stay (ILSpy 9.1 of core 2.0.211.51, XRL/World/Parts/Brain.cs:2507-2523): Stay only
		/// writes StartingCell for a mobile body that is not set to wander, which is exactly the
		/// shape a posted resident has (KingdomStations.Claim clears both wander flags before it
		/// calls Stay). Asked BEFORE anyone walks, so an immovable post refuses the whole set.
		/// </summary>
		private static bool CanMovePost(GameObject Body)
		{
			Brain brain = GameObject.Validate(Body) ? Body.Brain : null;
			return brain != null && brain.IsMobile() && !brain.Wanders && !brain.WandersRandomly;
		}

		/// <summary>
		/// Moves a resident's post to the ground they were just stood on, through the engine's own
		/// anchor API (Brain.Stay, Brain.cs:2507-2523 -- the same call KingdomStations.Claim uses
		/// to set a post). Verified by re-reading StartingCell: a post that did not move is a
		/// failure, because the body would walk back onto the site on the next pass.
		/// </summary>
		private static bool TryMovePost(GameObject Body, Cell Target)
		{
			if (!CanMovePost(Body) || Target == null) return false;
			Body.Brain.Stay(Target);
			XRL.World.GlobalLocation anchor = Body.Brain.StartingCell;
			return anchor != null && anchor.World != null && anchor.CellX == Target.X
				&& anchor.CellY == Target.Y
				&& string.Equals(anchor.ZoneID, Target.ParentZone?.ZoneID,
					global::System.StringComparison.Ordinal);
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
