using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomPlots
	{
		// --- Reading ground ---------------------------------------------------------------

		/// <summary>
		/// What one cell is, in the clearance table's terms, and what is standing there if the
		/// answer refuses the plot.
		/// <para>
		/// Creatures are not read at all: a settler standing on the ground walks off it, and a
		/// plot that refused every cell a wanderer happened to occupy would refuse forever for a
		/// reason the founder could never act on. Everything else that is not natural ground is
		/// <see cref="KingdomPlotRules.GroundKind.Held"/> &mdash; a dropped item, an owned thing,
		/// one of the settlement's own works, or anything this table simply cannot name.
		/// </para>
		/// </summary>
		/// <param name="C">The cell. Null reads as Held with no blocker named.</param>
		/// <param name="Blocker">What refuses the plot here, for the founder-facing sentence, or
		/// null when the ground is clearable.</param>
		public static KingdomPlotRules.GroundKind ReadGround(Cell C, out string Blocker)
		{
			Blocker = null;
			if (C == null)
			{
				Blocker = "the edge of the zone";
				return KingdomPlotRules.GroundKind.Held;
			}
			KingdomPlotRules.GroundKind kind = KingdomPlotRules.GroundKind.Bare;
			foreach (GameObject item in C.GetObjects())
			{
				if (item == null || item.IsCreature || item.IsPlayer())
				{
					continue;
				}
				KingdomPlotRules.GroundKind read = ReadObject(item);
				if (read == KingdomPlotRules.GroundKind.Bare)
				{
					continue;
				}
				if (KingdomPlotRules.Refuses(read))
				{
					Blocker = (read == KingdomPlotRules.GroundKind.Liquid) ? null : item.ShortDisplayNameStripped;
					return read;
				}
				if (KingdomPlotRules.ClearEffort(read) > KingdomPlotRules.ClearEffort(kind))
				{
					// The hardest thing standing in a cell is what clearing it costs. Compared by
					// effort rather than by enum order, so a marble seam under a fallen slab is
					// still read as marble.
					kind = read;
				}
			}
			return kind;
		}

		/// <summary>
		/// What one object makes of the cell it stands in. <see cref="KingdomPlotRules.GroundKind.Bare"/>
		/// means "this object is not in the way at all" &mdash; a floor, a cosmetic, a paint object.
		/// </summary>
		public static KingdomPlotRules.GroundKind ReadObject(GameObject Object)
		{
			if (Object == null)
			{
				return KingdomPlotRules.GroundKind.Bare;
			}
			if (Object.GetIntProperty("KingdomBuilt") == 1 || Object.GetIntProperty("KingdomStores") == 1
				|| Object.GetIntProperty("KingdomLarder") == 1 || Object.GetIntProperty("KingdomDefence") > 0
				|| Object.GetIntProperty(PlotPartProperty) == 1 || Object.HasPart("r_KingdomScaffold")
				|| Object.HasPart("r_KingdomPlanMarker") || Object.HasPart("r_KingdomPlotWorks"))
			{
				// The settlement's own works are not obstructions to be cleared; they are the
				// settlement. A plot never lands on one, and never takes one down to fit.
				return KingdomPlotRules.GroundKind.Held;
			}
			if (Object.GetIntProperty(HeartStakeProperty) == 1 || Object.GetIntProperty(HeartRelicProperty) == 1)
			{
				// A survey stake is the founder's ambition paced out, and the basin is what the
				// first water was poured from. Neither is an obstruction and neither is ever
				// cleared: reading them as bare ground is what lets ordinary plots be built over
				// surveyed ground (the mark is a preference, not a claim) and what lets every rung
				// of the heart be raised AROUND the basin rather than refused by it.
				return KingdomPlotRules.GroundKind.Bare;
			}
			if (Object.HasPart("LiquidVolume"))
			{
				return KingdomPlotRules.GroundKind.Liquid;
			}
			GameObjectBlueprint blueprint = Object.GetBlueprint();
			if (blueprint != null && blueprint.InheritsFrom("Floor"))
			{
				return KingdomPlotRules.GroundKind.Bare;
			}
			if (blueprint != null && blueprint.InheritsFrom("Widget"))
			{
				// Engine bookkeeping: spawn managers, ambient markers, terrain notes. A widget
				// has no physical presence a founder could see, act on, or clear, and refusing a
				// plot for one produces an invisible "[Widget] may not be taken" the player can
				// never resolve. Proven live 2026-08-29: wild marsh zones scatter these, which
				// made every wilderness rectangle read Held.
				return KingdomPlotRules.GroundKind.Bare;
			}
			if (Object.IsTakeable() || Object.IsOwned())
			{
				// A dropped waterskin is inviolate, and so is anything anybody's name is on.
				return KingdomPlotRules.GroundKind.Held;
			}
			if (Object.HasTag("Tree"))
			{
				return KingdomPlotRules.GroundKind.Trees;
			}
			if (Object.HasTag("Plant"))
			{
				return KingdomPlotRules.GroundKind.Brush;
			}
			if (Object.IsWall())
			{
				return WallGround(Object.Blueprint);
			}
			if (Object.IsDoor())
			{
				return KingdomPlotRules.GroundKind.Ruins;
			}
			return KingdomPlotRules.GroundKind.Held;
		}

		/// <summary>
		/// What kind of ground a standing wall is, by what it is made of. Marble is the rare seam
		/// the fine houses need; the manufactured walls &mdash; fulcrete, foamcrete, verdigris,
		/// plate &mdash; are somebody's ruin and yield scrap; everything else is rock.
		/// </summary>
		public static KingdomPlotRules.GroundKind WallGround(string Blueprint)
		{
			if (string.IsNullOrEmpty(Blueprint))
			{
				return KingdomPlotRules.GroundKind.Rock;
			}
			string blueprint = Blueprint.ToLowerInvariant();
			if (blueprint.Contains("marble"))
			{
				return KingdomPlotRules.GroundKind.Marble;
			}
			if (blueprint.Contains("crete") || blueprint.Contains("verdigris") || blueprint.Contains("metal")
				|| blueprint.Contains("plate") || blueprint.Contains("rubble") || blueprint.Contains("debris"))
			{
				return KingdomPlotRules.GroundKind.Ruins;
			}
			return KingdomPlotRules.GroundKind.Rock;
		}

	}
}
