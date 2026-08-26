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
	public static partial class KingdomStations
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

	}
}
