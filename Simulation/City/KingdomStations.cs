using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Placement by the hour, at the engine's edge.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.2(b): <b>vanilla ships no NPC scheduler</b> &mdash; no
	/// <c>GoToPartyLocation</c>, no <c>Schedule</c> class, no calendar-driven villager behaviour
	/// anywhere. What it ships is one hook, and it is enough:
	/// </para>
	/// <list type="bullet">
	/// <item><description><b>The anchor is free.</b> A settler with <c>Brain.Wanders = false</c>,
	/// <c>WandersRandomly = false</c> and no <c>NoStay</c> tag self-anchors to the cell it first
	/// stands in (<c>D/XRL/World/Parts/Brain.cs:2056</c>, on <c>EnteredCellEvent</c>), and
	/// <c>Brain.Stay(Cell)</c> sets it explicitly (<c>:2507-2521</c>). The <c>Bored</c> goal walks
	/// it back there forever (<c>D/XRL/World/AI/GoalHandlers/Bored.cs:262-266</c>). So
	/// materialisation does not <i>place</i> people so much as <b>move the anchor</b> &mdash; and
	/// vanilla's own AI does the walking.</description></item>
	/// <item><description><b>The daily life is <c>IdleQueryEvent</c>.</b> <c>Bored</c> gathers every
	/// object in the zone that wants the event, shuffles them, and offers each one the idle actor;
	/// <b>returning <c>false</c> claims that actor's turn</b> (<c>Bored.cs:285-320</c>). That is
	/// literally how vanilla beds send villagers to sleep at night
	/// (<c>D/XRL/World/Parts/Bed.cs:187-224</c>). There is no other mechanism in the
	/// game.</description></item>
	/// </list>
	/// <para>
	/// This file is the second and third clauses of &sect;3.2(b)'s sentence. The first &mdash; what
	/// the model decides &mdash; is <see cref="KingdomPlacementRules"/>, and nothing here decides
	/// anything: it reads the row, asks the rules for a post, and moves an anchor.
	/// </para>
	/// </summary>
	public static class KingdomStations
	{
		/// <summary>
		/// The work a settler is posted to, stamped by <c>KingdomGrowth.AssignWork</c> on the pass
		/// that crewed it.
		/// <para>
		/// Until W3 the crew was a fact about a WORK and never about a person, so every resident
		/// row read <c>JobWorkId = 0</c> and every day shape derived to the hearth &mdash; honestly,
		/// and uselessly for placement. Ablest-first assignment already knows exactly which settlers
		/// it put on which work (<c>KingdomCrewRules.CrewOutcome.SettlerIndices</c>); this is where
		/// that fact stops being thrown away.
		/// </para>
		/// </summary>
		public const string PostWorkProperty = "KingdomPostWorkId";

		/// <summary>The kind of work a settler is posted to, so the day shape can be derived
		/// without a second zone walk. One of <see cref="KingdomWorkKind"/>, as an int.</summary>
		public const string PostKindProperty = "KingdomPostWorkKind";

		/// <summary>Stamps one settler with the post this pass gave them. Cleared rather than left
		/// standing when nobody crewed them, because a stale post is a settler walking to a mill
		/// they were taken off.</summary>
		internal static void Post(GameObject Settler, int WorkId, KingdomWorkKind Kind)
		{
			if (!GameObject.Validate(Settler))
			{
				return;
			}
			Settler.SetIntProperty(PostWorkProperty, WorkId, RemoveIfZero: true);
			Settler.SetIntProperty(PostKindProperty, (int)Kind, RemoveIfZero: true);
		}

		/// <summary>The post one settler stands at, or zero.</summary>
		public static int PostOf(GameObject Settler)
		{
			return GameObject.Validate(Settler) ? Settler.GetIntProperty(PostWorkProperty) : 0;
		}

		/// <summary>What a work is, for the day shape. The same classification the work row's
		/// run-state carries, so a settler's day and their work's row cannot disagree.</summary>
		internal static KingdomWorkKind KindOf(GameObject Work)
		{
			if (!GameObject.Validate(Work))
			{
				return KingdomWorkKind.Other;
			}
			return KingdomWorkRules.Classify(new KingdomWorkTraits(
				KingdomCrops.FieldOf(Work) != null,
				Work.GetIntProperty(KingdomConstructionPresence.ActiveProperty) == 1,
				Work.GetIntProperty(KingdomAdopt.StoresProperty) == 1
					|| Work.GetIntProperty(KingdomAdopt.LarderProperty) == 1,
				Work.HasPart("SolarArray") || Work.HasPart("Capacitor")
					|| Work.HasPart("Generator"),
				Work.HasPart("ItemConvertor") || Work.HasPart("Mill")
					|| Work.HasPart("FoodProcessor"),
				Work.HasPart("LiquidProducer")));
		}

		/// <summary>
		/// Gives every crewed work a station, so the settlers posted to it have something to be
		/// claimed by.
		/// <para>
		/// <c>RequirePart</c> and not a blueprint edit: a work is any of forty designs and a part
		/// added at render is picked up by <c>Bored</c>'s own <c>WantEvent</c> scan
		/// (<c>Bored.cs:288-300</c>), whose <c>IdleObjects</c> cache is zone-scoped and rebuilt on
		/// <c>IdleDirty</c>. So there is no registration list to maintain and nothing to migrate.
		/// </para>
		/// </summary>
		public static void Attend(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (System == null || !System.Founded || Z == null || Survey == null)
			{
				return;
			}
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				GameObject work = Survey.Works[i];
				if (!GameObject.Validate(work) || work.CurrentCell == null)
				{
					continue;
				}
				r_KingdomStation station = work.RequirePart<r_KingdomStation>();
				station.WorkId = KingdomCityRules.StableId(work.ID);
				station.Kind = (int)KindOf(work);
			}
		}

		/// <summary>
		/// Whether this settler's anchor disagrees with where the hour puts them &mdash; the one
		/// question the reify budget asks about a person, and one heavy unit to answer.
		/// <para>
		/// Asymmetric on purpose. Wanting a post is <i>the anchor is not the post's cell</i>;
		/// wanting a hearth is <i>the anchor is still the post's cell</i>. A symmetric rule that
		/// compared against "wherever a hearth is" would re-anchor a settler every turn they took a
		/// step, which is a heavy unit a turn for a person who is already home.
		/// </para>
		/// </summary>
		internal static bool Misplaced(GameObject Settler, Zone Z, long NowTick, Dictionary<int, GameObject> Index)
		{
			Cell post;
			KingdomPost wanted;
			if (!TryReading(Settler, Z, NowTick, Index, out wanted, out post))
			{
				return false;
			}
			Cell anchored = (Settler.Brain.StartingCell == null) ? null : Settler.Brain.StartingCell.ResolveCell();
			if (wanted == KingdomPost.Station)
			{
				return anchored != post;
			}
			return anchored == post;
		}

		/// <summary>
		/// Moves one settler's anchor to where the hour puts them, and lets vanilla walk them.
		/// <para>
		/// &sect;3.2(b): <b>materialisation does not place people so much as move the anchor</b>
		/// &mdash; <c>Bored</c>'s own <c>StartingCell</c> branch takes them the rest of the way
		/// (<c>D/XRL/World/AI/GoalHandlers/Bored.cs:126-140, 262-266</c>) at no per-turn cost of
		/// ours. This is what a heavy reify unit buys.
		/// </para>
		/// </summary>
		internal static bool Place(Zone Z, GameObject Settler, long NowTick, Dictionary<int, GameObject> Index)
		{
			Cell post;
			KingdomPost wanted;
			if (!TryReading(Settler, Z, NowTick, Index, out wanted, out post))
			{
				return false;
			}
			Cell target = (wanted == KingdomPost.Station) ? post : Hearth(Z, Settler);
			if (target == null)
			{
				return false;
			}
			Settler.Brain.Wanders = false;
			Settler.Brain.WandersRandomly = false;
			Settler.Brain.Stay(target);
			Settler.Brain.PushGoal(new MoveTo(target, careful: true));
			return true;
		}

		/// <summary>Ends a temporary construction post and restores a home anchor. The body keeps
		/// its identity and schedule; vanilla MoveTo walks it home, and no cell placement occurs.</summary>
		internal static bool Release(Zone Z, GameObject Settler)
		{
			if (Z == null || !GameObject.Validate(Settler) || Settler.Brain == null
				|| KingdomPhysicalHappenings.IsStaged(Settler)
				|| Settler.IsPlayerLed() || Settler.IsPlayer())
			{
				return false;
			}
			Post(Settler, 0, KingdomWorkKind.Other);
			Cell target = Hearth(Z, Settler);
			if (target == null) return false;
			Settler.Brain.Wanders = false;
			Settler.Brain.WandersRandomly = false;
			Settler.Brain.Stay(target);
			Settler.Brain.PushGoal(new MoveTo(target, careful: true));
			return true;
		}

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
