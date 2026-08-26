using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
	{
	public static partial class KingdomStations
	{
		/// <summary>What the model says about one settler right now: where the hour wants them, and
		/// which cell their post stands on. False when this person is not ours to move.</summary>
		private static bool TryReading(GameObject Settler, Zone Z, long NowTick, Dictionary<int, GameObject> Index, out KingdomPost wanted, out Cell post)
		{
			wanted = KingdomPost.Hearth;
			post = null;
			if (!GameObject.Validate(Settler) || Settler.Brain == null || Z == null
				|| KingdomPhysicalHappenings.IsStaged(Settler)
				|| Settler.IsPlayerLed() || Settler.IsPlayer())
			{
				// A settler the founder charmed or recruited is Abroad, not posted: the model says
				// where they are, it does not take them back (§8.3).
				return false;
			}
			int workId = PostOf(Settler);
			if (workId == 0)
			{
				return false;
			}
			GameObject work;
			Cell at = (Index != null && Index.TryGetValue(workId, out work) && GameObject.Validate(work)) ? work.CurrentCell : null;
			if (at == null)
			{
				return false;
			}
			post = Standing(Z, at);
			if (post == null)
			{
				return false;
			}
			KingdomWorkKind kind = (KingdomWorkKind)Settler.GetIntProperty(PostKindProperty);
			wanted = KingdomPlacementRules.PostFor(
				KingdomResidentRules.DayShapeFor(workId, kind),
				KingdomPlacementRules.BandFor(NowTick));
			return true;
		}

		/// <summary>
		/// This ground's stations, by the work id they carry. Built once per pass and handed to
		/// every settler, because the alternative is a zone walk per person: sixty settlers against
		/// a zone's two thousand objects is a hundred and twenty thousand comparisons for an answer
		/// that does not change between them, and &sect;0.0 prices a whole turn's reify at two
		/// milliseconds.
		/// <para>
		/// Found through the station part rather than by re-hashing every object's id: the station
		/// is already the thing that carries a work row's id on the ground.
		/// </para>
		/// </summary>
		internal static Dictionary<int, GameObject> Index(Zone Z)
		{
			Dictionary<int, GameObject> index = new Dictionary<int, GameObject>();
			List<GameObject> stations = (Z == null) ? null : Z.GetObjectsWithPart("r_KingdomStation");
			for (int i = 0; stations != null && i < stations.Count; i++)
			{
				r_KingdomStation station = stations[i].GetPart<r_KingdomStation>();
				if (station != null && station.WorkId != 0 && !index.ContainsKey(station.WorkId))
				{
					index[station.WorkId] = stations[i];
				}
			}
			return index;
		}

		/// <summary>
		/// What a station says when <c>Bored</c> offers it an idle actor.
		/// <para>
		/// <b>False claims the actor's turn</b>, so a station must be selective or the settlement
		/// stands around doing one thing (&sect;3.2(b) constraint 2). Three gates, in this order,
		/// and every one of them is cheap: this actor is posted HERE; the hour actually wants them
		/// somewhere; and this station has not already spent somebody's turn inside the cooldown.
		/// </para>
		/// </summary>
		internal static bool Claim(GameObject Work, r_KingdomStation Station, GameObject Actor, long NowTick)
		{
			if (!GameObject.Validate(Work) || !GameObject.Validate(Actor) || Actor.Brain == null
				|| KingdomPhysicalHappenings.IsStaged(Actor) || Actor == Work
				|| Actor.IsPlayer() || Actor.IsPlayerLed())
			{
				return false;
			}
			if (Station.WorkId == 0 || PostOf(Actor) != Station.WorkId)
			{
				return false;
			}
			if (!KingdomPlacementRules.MayClaim(Station.LastClaimTick, NowTick))
			{
				return false;
			}
			Cell post = Work.CurrentCell;
			Zone zone = Work.CurrentZone;
			if (post == null || zone == null)
			{
				return false;
			}
			KingdomDayShape shape = KingdomResidentRules.DayShapeFor(Station.WorkId, KindOf(Work));
			KingdomPost wanted = KingdomPlacementRules.PostFor(shape, KingdomPlacementRules.BandFor(NowTick));
			Cell target = (wanted == KingdomPost.Station) ? Standing(zone, post) : Hearth(zone, Actor);
			if (target == null)
			{
				return false;
			}
			Cell standing = Actor.CurrentCell;
			if (standing == target)
			{
				// Already where the hour wants them. Claiming the turn to walk nowhere is exactly
				// the "settlement stands around doing one thing" failure the cooldown is for.
				return false;
			}
			Station.LastClaimTick = NowTick;
			// The anchor moves and vanilla walks them: Bored's own StartingCell branch takes them
			// the rest of the way on every later idle turn, at no cost of ours (Bored.cs:262-266).
			Actor.Brain.Wanders = false;
			Actor.Brain.WandersRandomly = false;
			Actor.Brain.Stay(target);
			Actor.Brain.PushGoal(new MoveTo(target, careful: true));
			return true;
		}

		/// <summary>The cell the founder actually sees somebody standing on: the work's own cell
		/// where it is walkable, and a cell beside it where the work fills its own square.</summary>
		private static Cell Standing(Zone Z, Cell Post)
		{
			if (Post.IsEmptyOfSolid() && Post.IsPassable())
			{
				return Post;
			}
			List<Cell> around = Post.GetAdjacentCells();
			for (int i = 0; around != null && i < around.Count; i++)
			{
				if (around[i].IsEmptyOfSolid() && around[i].IsPassable())
				{
					return around[i];
				}
			}
			return null;
		}

		/// <summary>
		/// Where somebody goes when the hour has no post for them.
		/// <para>
		/// A bed of their own if the zone has one free, and otherwise wherever they are standing
		/// &mdash; which RELEASES the anchor rather than leaving it on the station, and that is the
		/// load-bearing half. An anchor left on a workplace is <c>Bored</c> dragging a settler back
		/// to the mill all night, and vanilla's own <c>Bed</c> fighting it for the same turn.
		/// </para>
		/// </summary>
		private static Cell Hearth(Zone Z, GameObject Actor)
		{
			List<GameObject> beds = Z.GetObjectsWithPart("Bed");
			for (int i = 0; beds != null && i < beds.Count; i++)
			{
				Cell at = beds[i].CurrentCell;
				if (at != null && at.IsPassable())
				{
					List<Cell> around = at.GetAdjacentCells();
					for (int j = 0; around != null && j < around.Count; j++)
					{
						if (around[j].IsEmptyOfSolid() && around[j].IsPassable())
						{
							return around[j];
						}
					}
				}
			}
			return Actor.CurrentCell;
		}
	}
}
