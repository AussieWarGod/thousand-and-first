using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of worn ground: reading which errands a settlement's own shape
	/// implies, laying the day's walking on the cells those errands cross, letting the ground
	/// show it, and &mdash; only when the founder says so &mdash; paving a path in the material
	/// the settlement builds its walls in.
	/// <para>
	/// No road is ever drawn. The plot grammar already keeps a lane around every plot
	/// (<see cref="KingdomPlotRules.RoadMargin"/>) and the gap between two reserved rects IS the
	/// road; all this does is notice which gaps people cross. Everything resolves on the attended
	/// <c>ZoneActivatedEvent</c> pass out of a stored tick stamp, so a settlement wears its ways
	/// at exactly the rate it was lived in &mdash; the full elapsed, uncapped
	/// (<c>KingdomRules.ElapsedDays</c>), because errands are walked whether or not the founder
	/// is there to watch them being walked (Addendum 8 clause 1).
	/// <para>
	/// What keeps a season away from wearing a canyon is not a ceiling on the calendar but the
	/// labour term the formula already had: traffic is WALKERS x days, walkers come from the
	/// settlement's own population and its own errands, and a settlement with nobody in it walks
	/// nowhere however long the stretch (Addendum 8 clause 2). The per-pass bounds
	/// (<c>KingdomRoadRules.MaxRoutesPerPass</c>, <c>MaxFloorChangesPerPass</c>) stay exactly
	/// what they always were: loop guards on one visit's work, never forgiveness.
	/// </para>
	/// </para>
	/// <para>
	/// The protection law (STANDARDS 7) is the shape of this file, not a check inside it. Wear
	/// only ever ADDS a floor object of ours to a cell that <c>KingdomPlots.ReadGround</c> calls
	/// bare, that nobody owns, that holds no liquid, and that lies in no plot &mdash; and the only
	/// objects it ever destroys are its own, marked with <see cref="PathStateProperty"/>. The
	/// ground the cell already had is never cleared, exactly as vanilla's own
	/// <c>RoadBuilder</c> and <c>JoppaOutskirts</c> add a <c>DirtPath</c> over what is there
	/// rather than replacing it (<c>RoadBuilder.cs:168</c>, <c>JoppaOutskirts.cs:332</c>). Nothing
	/// here ever calls <c>PlaceHut</c> or <c>ClearRect</c>.
	/// </para>
	/// <para>
	/// State lives on the zone, not on the settlement, and deliberately: ways are a property of
	/// ground, and a realm's second city has ground of its own. Zone properties are serialized by
	/// <c>ZoneManager</c> (<c>ZoneManager.cs:507-516</c> and <c>:677-688</c>) and are the idiom
	/// the rite ground already uses (<c>KingdomPlots.RiteXProperty</c>), so this system adds no
	/// serialized field to any part or system and cannot move anyone's save layout.
	/// </para>
	/// </summary>
	public static class KingdomRoads
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.RoadPaving)
			{
				return;
			}
			List<KingdomConstructionCell> cells;
			if (!KingdomConstructionRules.TryDecodeCells(Job.Payload, out cells)) return;
			ProjectPaving(Z, Job.TargetKey, cells, Job, out _, out _, out _);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.RoadPaving) return;
			List<KingdomConstructionCell> cells;
			if (!KingdomConstructionRules.TryDecodeCells(Job.Payload, out cells)) return;
			KingdomConstructionJob inspected = Job;
			if (inspected.Phase == KingdomConstructionPhase.Complete)
			{
				if (inspected.PhysicalPhase == KingdomPhysicalPhase.RoadTallySettled)
					SettleRoadTerminal(System, Z, Job.TargetKey, cells, ref inspected);
				return;
			}
			if (inspected.Phase != KingdomConstructionPhase.InspectionRequired
				&& (inspected.Phase == KingdomConstructionPhase.ProjectionPending
					|| inspected.PhysicalPhase != KingdomPhysicalPhase.None))
				ProjectPaving(Z, Job.TargetKey, cells, inspected, out _, out _, out _);
		}

		/// <summary>Zone property carrying the worn-ground tally, written by
		/// <c>KingdomRoadRules.Encode</c>.</summary>
		public const string TallyProperty = "r_TAF_Roads";

		/// <summary>Zone property carrying the tick the ground was last walked, as a string
		/// because zone properties hold strings and a tick is not one.</summary>
		public const string WalkedProperty = "r_TAF_RoadsWalked";

		/// <summary>Bounded v1 option observation for this zone's own road clock. It also
		/// carries the last applied master-resume token, so master-off time cannot become
		/// walking when the global switch returns.</summary>
		public const string OptionStateProperty = "r_TAF_RoadsOption_v1";

		/// <summary>Exact immutable settlement owning <see cref="OptionStateProperty"/>.</summary>
		public const string OptionOwnerProperty = "r_TAF_RoadsOptionOwner_v1";

		/// <summary>Realm-wide option epoch. A transition first seen in one zone is therefore
		/// still observed as a mismatch when another claimed zone is loaded later.</summary>
		public const string GlobalOptionStatePrefix = "r_TAF_RoadsGlobalOption_v1:";

		/// <summary>Zone property set once the founder has been told the tally is full, so the
		/// reason is given once per stall rather than once per visit (STANDARDS 7b). Cleared the
		/// moment the tally has room again.</summary>
		public const string FullSaidProperty = "r_TAF_RoadsFull";

		/// <summary>Zone property carrying the highest rung of the ladder whose line has already
		/// been given, so the settlement remarks on its own paths once rather than every time a
		/// ninth cell crosses the same threshold.</summary>
		public const string SaidProperty = "r_TAF_RoadsSaid";

		/// <summary>
		/// Property marking a floor this system laid, and which rung of
		/// <c>KingdomRoadRules.WearState</c> it stands for. The whole of the mod's claim over
		/// these objects: nothing without this property is ever removed here, and everything with
		/// it was created here.
		/// </summary>
		public const string PathStateProperty = "KingdomPath";

		/// <summary>Vanilla's packed dirt floor, laid where the grass has gone
		/// (<c>ZoneTerrain.xml:932</c>).</summary>
		public const string TroddenBlueprint = "DirtFloor";

		/// <summary>Vanilla's dirt path, laid where a way has become a way
		/// (<c>ZoneTerrain.xml:937</c>) &mdash; the same floor <c>PlaceHut</c> lays inside a
		/// vanilla village hut.</summary>
		public const string PathBlueprint = "DirtPath";

		/// <summary>Whether ground wears at all. Its own toggle, because a player who likes the
		/// grass exactly as the world generator drew it should be able to keep it (STANDARDS 3).
		/// Defaults to on when the option is missing, so a build whose XML has not landed yet
		/// behaves like the shipped one.</summary>
		public static bool Enabled => Options.GetOption("r_TAF_OptionRoads") != "No";

		/// <summary>One errand: two ends and a reason.</summary>
		private struct Errand
		{
			public int FromX;

			public int FromY;

			public int ToX;

			public int ToY;

			public KingdomRoadRules.RouteKind Kind;

			public Errand(int FromX, int FromY, int ToX, int ToY, KingdomRoadRules.RouteKind Kind)
			{
				this.FromX = FromX;
				this.FromY = FromY;
				this.ToX = ToX;
				this.ToY = ToY;
				this.Kind = Kind;
			}
		}

		// --- Reading and writing the zone's own record ------------------------------------

		/// <summary>The worn-ground tally of a zone. Never null; an unreadable or absent tally
		/// reads as an empty one and says so in the log.</summary>
		/// <param name="Z">The zone. Null yields an empty tally.</param>
		public static List<KingdomRoadRules.WornCell> ReadTally(Zone Z)
		{
			if (Z == null)
			{
				return new List<KingdomRoadRules.WornCell>();
			}
			if (!KingdomRoadRules.TryDecode(Z.GetZoneProperty(TallyProperty, null), out var cells, out var error) && error != null)
			{
				KingdomLog.Log(error);
			}
			return cells;
		}

		/// <summary>Writes a tally back to the zone. An empty tally writes the empty string, so a
		/// settlement nobody walks costs one short property and no bookkeeping.</summary>
		/// <param name="Z">The zone. Null does nothing.</param>
		/// <param name="Cells">The tally. Null writes the empty string.</param>
		public static void WriteTally(Zone Z, IList<KingdomRoadRules.WornCell> Cells)
		{
			if (Z == null)
			{
				return;
			}
			Z.SetZoneProperty(TallyProperty, KingdomRoadRules.Encode(Cells));
		}

		private static long ReadTick(Zone Z, string Property)
		{
			if (Z == null)
			{
				return 0L;
			}
			return long.TryParse(Z.GetZoneProperty(Property, null), out var ticks) ? ticks : 0L;
		}

		private static void WriteTick(Zone Z, string Property, long Ticks)
		{
			Z?.SetZoneProperty(Property, Ticks.ToString());
		}

		private static KingdomElapsedOptionDecision ObserveOption(KingdomSystem System,
			Zone Z, long Now)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			string realmId = System.CurrentRealmId;
			if (The.Game == null || !KingdomIdentityRules.IsSettlementId(settlementId)
				|| !KingdomIdentityRules.IsRealmId(realmId))
			{
				return KingdomElapsedOptionRules.Observe(
					KingdomElapsedOptionRecord.Unobserved, Enabled,
					System.MasterAppliedResumeToken, Now);
			}

			string globalKey = GlobalOptionStatePrefix + realmId;
			KingdomElapsedOptionRecord globalPrior;
			bool globalDecoded = KingdomElapsedOptionRules.TryDecode(
				The.Game.GetStringGameState(globalKey, ""), out globalPrior);
			if (!globalDecoded) globalPrior = KingdomElapsedOptionRecord.Unobserved;
			KingdomElapsedOptionDecision global = KingdomElapsedOptionRules.Observe(globalPrior,
				Enabled, System.MasterAppliedResumeToken, Now);
			if (!global.Valid)
			{
				global = KingdomElapsedOptionRules.Observe(
					KingdomElapsedOptionRecord.Unobserved, Enabled,
					System.MasterAppliedResumeToken, Now);
				globalDecoded = false;
			}
			string current = global.Valid
				? KingdomElapsedOptionRules.Encode(global.Record) : null;
			if (global.Valid && current != null && (!globalDecoded
				|| global.Transition != KingdomElapsedOptionTransition.None))
				The.Game.SetStringGameState(globalKey, current);

			bool ownerMatches = global::System.String.Equals(
				Z.GetZoneProperty(OptionOwnerProperty, null), settlementId,
				global::System.StringComparison.Ordinal);
			string encoded = ownerMatches
				? Z.GetZoneProperty(OptionStateProperty, null) : null;
			KingdomElapsedOptionRecord prior = KingdomElapsedOptionRecord.Unobserved;
			bool zoneDecoded = ownerMatches
				&& KingdomElapsedOptionRules.TryDecode(encoded, out prior);
			bool zoneMatches = zoneDecoded && global.Valid
				&& prior.State == global.Record.State
				&& prior.ObservedTick == global.Record.ObservedTick
				&& prior.MasterResumeToken == global.Record.MasterResumeToken;
			if (!zoneMatches && global.Valid && current != null)
			{
				return new KingdomElapsedOptionDecision(true, global.Record,
					global.Transition, Enabled ? KingdomElapsedOptionAction.AnchorEnabled
						: KingdomElapsedOptionAction.AnchorDisabled);
			}
			return global;
		}

		private static void CommitOption(KingdomSystem System, Zone Z,
			KingdomElapsedOptionRecord Record)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			if (Z == null || !KingdomIdentityRules.IsSettlementId(settlementId)) return;
			string current = KingdomElapsedOptionRules.Encode(Record);
			if (current == null) return;
			// The owned clock is written before this helper is called. State then owner:
			// a cut between these writes leaves a foreign owner and reanchors again.
			Z.SetZoneProperty(OptionStateProperty, current);
			Z.SetZoneProperty(OptionOwnerProperty, settlementId);
		}

		// --- Reading ground ---------------------------------------------------------------

		/// <summary>The floor this system laid on a cell, or null when it laid none.</summary>
		/// <param name="C">The cell. Null answers null.</param>
		public static GameObject OurFloor(Cell C)
		{
			GameObject floor;
			return FindOurFloor(C, out floor) == KingdomPhysicalLookupState.Exact ? floor : null;
		}

		/// <summary>Counts every loaded road-floor identity; duplicates and malformed shapes
		/// are ambiguous, never an absent floor that may be replaced.</summary>
		public static KingdomPhysicalLookupState FindOurFloor(Cell C, out GameObject Floor)
		{
			Floor = null;
			if (C == null) return KingdomPhysicalLookupState.Absent;
			int count = 0;
			bool exactShape = false;
			foreach (GameObject item in C.GetObjects())
			{
				if (item != null && item.GetIntProperty(PathStateProperty) > 0)
				{
					count++;
					if (count == 1)
					{
						Floor = item;
						int state = item.GetIntProperty(PathStateProperty);
						exactShape = GameObject.Validate(item) && item.CurrentCell == C
							&& state >= (int)KingdomRoadRules.WearState.Trodden
							&& state <= (int)KingdomRoadRules.WearState.Paved;
					}
				}
			}
			KingdomPhysicalLookupState result = KingdomConstructionRules.PhysicalLookupState(
				count, exactShape);
			if (result == KingdomPhysicalLookupState.Exact)
			{
				GameObject global;
				if (KingdomConstruction.FindExactId(C.ParentZone, Floor.ID, out global)
						!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(global, Floor))
					result = KingdomPhysicalLookupState.Ambiguous;
			}
			if (result != KingdomPhysicalLookupState.Exact) Floor = null;
			return result;
		}

		/// <summary>The rung a cell has already been brought to by this system.</summary>
		public static KingdomRoadRules.WearState AppliedState(Cell C)
		{
			GameObject floor;
			if (FindOurFloor(C, out floor) != KingdomPhysicalLookupState.Exact)
			{
				return KingdomRoadRules.WearState.Untouched;
			}
			int state = floor.GetIntProperty(PathStateProperty);
			if (state < (int)KingdomRoadRules.WearState.Untouched || state > (int)KingdomRoadRules.WearState.Paved)
			{
				return KingdomRoadRules.WearState.Untouched;
			}
			return (KingdomRoadRules.WearState)state;
		}

		/// <summary>
		/// Whether feet may be allowed to show on this cell at all.
		/// <para>
		/// Deliberately stricter than "empty": the ground must be what
		/// <c>KingdomPlots.ReadGround</c> calls bare &mdash; open ground, or a floor, or this
		/// system's own earlier work &mdash; it must hold no liquid, nothing on it may be owned
		/// by anybody, and it must lie outside every plot, because the floor inside a building
		/// belongs to the building. Anything else and the ground is left exactly as it is.
		/// </para>
		/// </summary>
		/// <param name="C">The cell. Null is never wearable.</param>
		/// <param name="Plots">Plots laid in this zone, from <c>KingdomPlots.ReadPlots</c>. Null
		/// reads as none laid.</param>
		public static bool Wearable(Cell C, IList<KingdomPlotRules.PlotRect> Plots)
		{
			if (C == null)
			{
				return false;
			}
			// Cheapest question first: the ground inside a plot belongs to the building standing
			// on it, and no cell of a plot is ever worn however many people cross it.
			if (Plots != null)
			{
				for (int i = 0; i < Plots.Count; i++)
				{
					if (Plots[i].Contains(C.X, C.Y))
					{
						return false;
					}
				}
			}
			if (KingdomPlots.ReadGround(C, out _) != KingdomPlotRules.GroundKind.Bare)
			{
				return false;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item == null || item.IsCreature)
				{
					continue;
				}
				if (item.GetIntProperty(PathStateProperty) == (int)KingdomRoadRules.WearState.Paved)
				{
					// Paved ground is finished ground. It never accrues again, and nothing is
					// ever laid on top of it.
					return false;
				}
				if (item.IsOwned())
				{
					// ReadGround reads a floor before it reads ownership, so a floor somebody
					// else laid would come back bare. Nothing anyone's name is on is walked over.
					return false;
				}
			}
			return true;
		}

		/// <summary>Whether an errand may be walked through a cell. Solid things turn feet aside;
		/// people do not, because a settler standing in a lane is standing in a lane and will move
		/// off it.</summary>
		public static bool Walkable(Cell C)
		{
			if (C == null)
			{
				return false;
			}
			if (C.HasObjectWithPart("LiquidVolume"))
			{
				return false;
			}
			return C.IsPassable(null, IncludeCombatObjects: false);
		}

		// --- The pass ---------------------------------------------------------------------

		/// <summary>
		/// Walks the settlement's own errands for the days since anyone last walked them, and
		/// lets the ground show it.
		/// <para>
		/// Called from <c>KingdomGrowth.OnZoneActivated</c> after everything that spends water and
		/// everything that spends hands, because wearing ground spends neither. Nobody is stood
		/// down off a work to make a path; the path is what is left behind by people going to the
		/// work they were already assigned to.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom. Does nothing when unfounded.</param>
		/// <param name="Z">The activated ground. Does nothing when it is not the kingdom's.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z)
		{
			if (System == null || !System.Founded || Z == null || The.Game == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			KingdomElapsedOptionDecision option = ObserveOption(System, Z, timeTicks);
			if (!option.Valid) return;
			if (option.Action == KingdomElapsedOptionAction.AnchorDisabled
				|| option.Action == KingdomElapsedOptionAction.AnchorEnabled)
			{
				WriteTick(Z, WalkedProperty, timeTicks);
				CommitOption(System, Z, option.Record);
				return;
			}
			if (option.Action != KingdomElapsedOptionAction.Run) return;
			long walked = ReadTick(Z, WalkedProperty);
			if (walked <= 0L)
			{
				WriteTick(Z, WalkedProperty, timeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(timeTicks - walked);
			if (days <= 0)
			{
				return;
			}
			WriteTick(Z, WalkedProperty, KingdomRules.AdvanceCheckpoint(walked, timeTicks));
			// Nobody living here is NOT a stall: an empty settlement has no errands, and an
			// announcement about it would be a complaint about a thing that is not wrong.
			if (System.Population <= 0)
			{
				return;
			}
			List<KingdomPlotRules.PlotRect> plots = KingdomPlots.ReadPlots(Z);
			List<Errand> errands = Errands(System, Z, plots);
			if (errands.Count == 0)
			{
				return;
			}
			List<KingdomRoadRules.WornCell> tally = ReadTally(Z);
			// One read per cell per pass however many errands cross it: 0 unknown, 1 walkable,
			// 2 not. Without this a settlement with eight errands over the same lane pays for
			// that lane eight times.
			byte[] cache = new byte[Z.Width * Z.Height];
			KingdomRoadRules.CellFilter passable = delegate(int x, int y)
			{
				int index = KingdomRoadRules.Pack(x, y, Z.Width);
				if (index < 0 || index >= cache.Length)
				{
					return false;
				}
				if (cache[index] == 0)
				{
					cache[index] = (byte)(Walkable(Z.GetCell(x, y)) ? 1 : 2);
				}
				return cache[index] == 1;
			};
			int start = KingdomRoadRules.RotationStart(timeTicks, errands.Count);
			int taken = (errands.Count < KingdomRoadRules.MaxRoutesPerPass) ? errands.Count : KingdomRoadRules.MaxRoutesPerPass;
			List<int> route = new List<int>();
			bool full = false;
			int laid = 0;
			for (int i = 0; i < taken; i++)
			{
				Errand errand = errands[(start + i) % errands.Count];
				int walkers = KingdomRoadRules.WalkersFor(errand.Kind, System.Population);
				int traffic = KingdomRoadRules.TrafficFor(walkers, days, errand.Kind);
				if (traffic <= 0)
				{
					continue;
				}
				if (!KingdomRoadRules.TryTrace(passable, Z.Width, Z.Height, errand.FromX, errand.FromY, errand.ToX, errand.ToY,
					KingdomRoadRules.MaxRouteCells, KingdomRoadRules.MaxExploreCells, route))
				{
					continue;
				}
				for (int c = 0; c < route.Count; c++)
				{
					int x = KingdomRoadRules.UnpackX(route[c], Z.Width);
					int y = KingdomRoadRules.UnpackY(route[c], Z.Width);
					if (!Wearable(Z.GetCell(x, y), plots))
					{
						continue;
					}
					if (!KingdomRoadRules.Accrue(tally, x, y, traffic, out _))
					{
						full = true;
						continue;
					}
					laid++;
				}
			}
			KingdomRoadRules.WearState reached = Apply(Z, tally, plots);
			WriteTally(Z, tally);
			Announce(System, Z, reached, full, tally.Count);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("roads: days=" + days + " errands=" + errands.Count + " walked=" + taken
					+ " cells=" + laid + " tracked=" + tally.Count + " reached=" + reached);
			}
		}

		// Everything anyone in this settlement has a reason to walk between, in a stable order:
		// home to the nearest work, each work to the heart, the heart to the way out, and every
		// plot's own door to the lane beside it.
		private static List<Errand> Errands(KingdomSystem System, Zone Z, IList<KingdomPlotRules.PlotRect> Plots)
		{
			List<Errand> errands = new List<Errand>();
			if (Z == null)
			{
				return errands;
			}
			List<KingdomLayoutRules.LayoutMark> marks = KingdomLayout.ReadMarks(Z);
			List<KingdomLayoutRules.LayoutMark> homes = new List<KingdomLayoutRules.LayoutMark>();
			List<KingdomLayoutRules.LayoutMark> works = new List<KingdomLayoutRules.LayoutMark>();
			for (int i = 0; i < marks.Count; i++)
			{
				KingdomLayoutRules.LayoutMark mark = marks[i];
				if (mark.Purpose == KingdomLayoutRules.LayoutPurpose.Housing)
				{
					homes.Add(mark);
				}
				else if (mark.Purpose == KingdomLayoutRules.LayoutPurpose.Civic
					|| mark.Purpose == KingdomLayoutRules.LayoutPurpose.Field
					|| mark.Purpose == KingdomLayoutRules.LayoutPurpose.Storage
					|| mark.Purpose == KingdomLayoutRules.LayoutPurpose.Sited)
				{
					works.Add(mark);
				}
			}
			// The route loop below is the bounded work. Its input must still describe the whole
			// legal city: truncating here made every work and plot after the first twelve invisible
			// forever rather than queued. Canonical coordinate order also keeps the rotating window
			// stable when Qud enumerates objects differently after a reload.
			homes.Sort(CompareMarks);
			works.Sort(CompareMarks);
			bool hasRite = KingdomPlots.TryRiteGround(Z, out var riteX, out var riteY);
			bool hasHeart = KingdomPlotRules.TryHeart(marks, hasRite, riteX, riteY, out var heartX, out var heartY);
			for (int i = 0; i < homes.Count; i++)
			{
				int nearest = Nearest(works, homes[i].X, homes[i].Y);
				if (nearest >= 0)
				{
					errands.Add(new Errand(homes[i].X, homes[i].Y, works[nearest].X, works[nearest].Y,
						KingdomRoadRules.RouteKind.HomeToWork));
				}
			}
			if (hasHeart)
			{
				for (int i = 0; i < works.Count; i++)
				{
					errands.Add(new Errand(works[i].X, works[i].Y, heartX, heartY, KingdomRoadRules.RouteKind.WorkToHeart));
				}
				KingdomRules.Frontier edges = (System != null)
					? KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones)
					: KingdomRules.Frontier.None;
				if (KingdomRoadRules.TryGate(Z.Width, Z.Height, edges, heartX, heartY, out var gateX, out var gateY))
				{
					errands.Add(new Errand(heartX, heartY, gateX, gateY, KingdomRoadRules.RouteKind.HeartToGate));
				}
				AddEntranceErrands(Z, Plots, heartX, heartY, errands);
			}
			return errands;
		}

		private static int CompareMarks(KingdomLayoutRules.LayoutMark A,
			KingdomLayoutRules.LayoutMark B)
		{
			int byY = A.Y.CompareTo(B.Y);
			if (byY != 0) return byY;
			int byX = A.X.CompareTo(B.X);
			if (byX != 0) return byX;
			return ((int)A.Purpose).CompareTo((int)B.Purpose);
		}

		/// <summary>
		/// Adds every current authored public entrance to the road rotation. The immutable
		/// architecture receipt is authority for a finished current-schema building; inventing a
		/// heart-facing door from its rectangle can aim the road at a wall. Receipt-less old plots
		/// retain the deterministic geometric door as their compatibility path. A partial or corrupt
		/// receipt fails closed instead of silently becoming a different building.
		/// </summary>
		private static void AddEntranceErrands(Zone Z, IList<KingdomPlotRules.PlotRect> Plots,
			int HeartX, int HeartY, IList<Errand> Errands)
		{
			if (Z == null || Plots == null || Errands == null) return;
			Dictionary<string, List<GameObject>> roots =
				new Dictionary<string, List<GameObject>>(System.StringComparer.Ordinal);
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			for (int i = 0; i < survey.PlotRoots.Count; i++)
			{
				GameObject item = survey.PlotRoots[i];
				KingdomPlotRules.PlotRect rect;
				if (!KingdomPlots.TryReadRect(item, out rect)) continue;
				string key = RectKey(rect);
				List<GameObject> objects;
				if (!roots.TryGetValue(key, out objects))
				{
					objects = new List<GameObject>();
					roots.Add(key, objects);
				}
				objects.Add(item);
			}

			List<KingdomPlotRules.PlotRect> unique = new List<KingdomPlotRules.PlotRect>();
			HashSet<string> plotKeys = new HashSet<string>(System.StringComparer.Ordinal);
			for (int i = 0; i < Plots.Count; i++)
			{
				string key = RectKey(Plots[i]);
				if (plotKeys.Add(key)) unique.Add(Plots[i]);
			}
			unique.Sort(CompareRects);
			HashSet<string> routes = new HashSet<string>(System.StringComparer.Ordinal);

			for (int p = 0; p < unique.Count; p++)
			{
				KingdomPlotRules.PlotRect rect = unique[p];
				List<GameObject> objects;
				bool receiptEvidence = false;
				bool exactReceipt = false;
				if (roots.TryGetValue(RectKey(rect), out objects))
				{
					for (int o = 0; o < objects.Count; o++)
					{
						GameObject root = objects[o];
						if (HasArchitectureReceiptEvidence(root)) receiptEvidence = true;
						KingdomArchitectureIntent intent;
						ArchitectureLayoutSnapshot snapshot;
						string failure;
						if (!KingdomArchitectureRuntime.TryRead(root, out intent, out failure)
							|| !KingdomArchitectureRuntime.TryDecode(intent, out snapshot, out failure))
							continue;
						exactReceipt = true;
						for (int a = 0; a < snapshot.Anchors.Count; a++)
						{
							ArchitectureAnchor anchor = snapshot.Anchors[a];
							if (anchor == null || !(anchor.Key == "entrance:public"
								|| anchor.Key.StartsWith("entrance:public@",
									System.StringComparison.Ordinal))) continue;
							int doorX;
							int doorY;
							if (!KingdomArchitectureRuntime.TryWorldAnchor(snapshot, rect, anchor,
								out doorX, out doorY, out failure)) continue;
							AddEntranceErrand(rect, doorX, doorY, routes, Errands);
						}
					}
				}
				if (exactReceipt || receiptEvidence) continue;

				// Pre-schema plots and a currently staked plan have no frozen receipt to read.
				// Preserve their old deterministic geometry until the real authored receipt exists.
				int legacyDoorX;
				int legacyDoorY;
				if (KingdomPlotRules.TryDoor(rect, HeartX, HeartY,
					out legacyDoorX, out legacyDoorY))
					AddEntranceErrand(rect, legacyDoorX, legacyDoorY, routes, Errands);
			}
		}

		private static bool HasArchitectureReceiptEvidence(GameObject Object)
		{
			return Object != null && (Object.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Object.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Object.HasIntProperty(KingdomArchitectureRuntime.BuildKeyProperty)
				|| Object.HasStringProperty(KingdomArchitectureRuntime.BuildKeyProperty)
				|| Object.HasIntProperty(KingdomArchitectureRuntime.SnapshotProperty)
				|| Object.HasStringProperty(KingdomArchitectureRuntime.SnapshotProperty)
				|| Object.HasIntProperty(KingdomArchitectureRuntime.HashProperty)
				|| Object.HasStringProperty(KingdomArchitectureRuntime.HashProperty));
		}

		private static void AddEntranceErrand(KingdomPlotRules.PlotRect Rect, int DoorX,
			int DoorY, ISet<string> Routes, IList<Errand> Errands)
		{
			int laneX;
			int laneY;
			if (!KingdomRoadRules.TryLane(Rect, DoorX, DoorY, out laneX, out laneY)) return;
			string key = DoorX + "," + DoorY + ">" + laneX + "," + laneY;
			if (!Routes.Add(key)) return;
			Errands.Add(new Errand(DoorX, DoorY, laneX, laneY,
				KingdomRoadRules.RouteKind.DoorToLane));
		}

		private static int CompareRects(KingdomPlotRules.PlotRect A,
			KingdomPlotRules.PlotRect B)
		{
			int byY = A.Y1.CompareTo(B.Y1);
			if (byY != 0) return byY;
			int byX = A.X1.CompareTo(B.X1);
			if (byX != 0) return byX;
			int byY2 = A.Y2.CompareTo(B.Y2);
			return byY2 != 0 ? byY2 : A.X2.CompareTo(B.X2);
		}

		private static string RectKey(KingdomPlotRules.PlotRect Rect)
		{
			return Rect.X1 + "," + Rect.Y1 + "," + Rect.X2 + "," + Rect.Y2;
		}

		private static int Nearest(IList<KingdomLayoutRules.LayoutMark> Marks, int X, int Y)
		{
			int best = -1;
			int bestDistance = 0;
			for (int i = 0; i < Marks.Count; i++)
			{
				int distance = KingdomLayoutRules.Chebyshev(X, Y, Marks[i].X, Marks[i].Y);
				if (distance == 0)
				{
					continue;
				}
				if (best < 0 || distance < bestDistance)
				{
					best = i;
					bestDistance = distance;
				}
			}
			return best;
		}

		// --- Letting it show ---------------------------------------------------------------

		/// <summary>
		/// Brings the ground up to what the tally says, no more than
		/// <c>KingdomRoadRules.MaxFloorChangesPerPass</c> cells of it at a time, and retires every
		/// cell that has become a path &mdash; from then on the laid path is the record, and the
		/// tally has room for whatever the settlement wears next.
		/// </summary>
		/// <returns>The highest rung any cell was brought to this pass, for the ledger's one
		/// line.</returns>
		private static KingdomRoadRules.WearState Apply(Zone Z, IList<KingdomRoadRules.WornCell> Tally, IList<KingdomPlotRules.PlotRect> Plots)
		{
			KingdomRoadRules.WearState reached = KingdomRoadRules.WearState.Untouched;
			int changes = 0;
			for (int i = Tally.Count - 1; i >= 0; i--)
			{
				if (changes >= KingdomRoadRules.MaxFloorChangesPerPass)
				{
					break;
				}
				KingdomRoadRules.WornCell cell = Tally[i];
				KingdomRoadRules.WearState wanted = KingdomRoadRules.WearAt(cell.Traffic);
				if (wanted <= KingdomRoadRules.WearState.Worn)
				{
					continue;
				}
				Cell ground = Z.GetCell(cell.X, cell.Y);
				GameObject exactFloor;
				KingdomPhysicalLookupState floorState = FindOurFloor(ground, out exactFloor);
				if (floorState == KingdomPhysicalLookupState.Ambiguous) continue;
				KingdomRoadRules.WearState applied = floorState == KingdomPhysicalLookupState.Exact
					? (KingdomRoadRules.WearState)exactFloor.GetIntProperty(PathStateProperty)
					: KingdomRoadRules.WearState.Untouched;
				if (applied >= wanted)
				{
					if (wanted == KingdomRoadRules.WearState.Path)
					{
						Tally.RemoveAt(i);
					}
					continue;
				}
				if (!Wearable(ground, Plots))
				{
					// Something has been set down here since the last pass. The ground keeps its
					// tally and waits; nothing is moved to make room for a floor.
					continue;
				}
				if (!Lay(ground, wanted, null))
				{
					continue;
				}
				changes++;
				if (wanted > reached)
				{
					reached = wanted;
				}
				if (wanted == KingdomRoadRules.WearState.Path)
				{
					Tally.RemoveAt(i);
				}
			}
			return reached;
		}

		/// <summary>
		/// Lays one floor, taking up the one this system laid before it. The only destruction
		/// anywhere in this file, and it is always of an object this file created and marked
		/// (STANDARDS 7).
		/// </summary>
		/// <param name="C">The cell. Must already have passed <see cref="Wearable"/>.</param>
		/// <param name="State">The rung to bring it to. <c>Untouched</c> and <c>Worn</c> lay
		/// nothing, because neither is a floor.</param>
		/// <param name="PavedBlueprint">The blueprint paving is laid as, from
		/// <c>KingdomRoadRules.PavedFloorFor</c>. Ignored except when
		/// <paramref name="State"/> is <c>Paved</c>.</param>
		/// <returns>False when nothing was laid, including when the blueprint does not exist in
		/// this install.</returns>
		public static bool Lay(Cell C, KingdomRoadRules.WearState State, string PavedBlueprint)
		{
			GameObject ignored;
			return Lay(C, State, PavedBlueprint, null, out ignored);
		}

		private static bool Lay(Cell C, KingdomRoadRules.WearState State,
			string PavedBlueprint, KingdomConstructionJob Job, out GameObject Floor)
		{
			Floor = null;
			if (C == null)
			{
				return false;
			}
			string blueprint;
			switch (State)
			{
				case KingdomRoadRules.WearState.Trodden:
					blueprint = TroddenBlueprint;
					break;
				case KingdomRoadRules.WearState.Path:
					blueprint = PathBlueprint;
					break;
				case KingdomRoadRules.WearState.Paved:
					blueprint = string.IsNullOrEmpty(PavedBlueprint) ? PathBlueprint : PavedBlueprint;
					break;
				default:
					return false;
			}
			if (Job != null)
			{
				GameObject existing = null;
				List<GameObject> old = new List<GameObject>();
				foreach (GameObject item in C.GetObjects())
				{
					if (item.GetIntProperty(PathStateProperty) <= 0) continue;
					if (item.Blueprint == blueprint
						&& item.GetIntProperty(PathStateProperty) == (int)State
						&& KingdomConstruction.HasReceipt(item, Job))
					{
						if (existing == null) existing = item;
						else old.Add(item);
					}
					else old.Add(item);
				}
				if (existing != null)
				{
					for (int i = 0; i < old.Count; i++)
					{
						bool removed;
						try { removed = old[i].Obliterate(null, Silent: true); }
						finally
						{
							KingdomSurvey.ObserveCurrentTopologyInActive(C.ParentZone, old[i]);
						}
						if (removed && !GameObject.Validate(old[i]))
							KingdomSurvey.ObserveRemovedFromActive(C.ParentZone, old[i]);
					}
					for (int i = 0; i < old.Count; i++)
					{
						if (old[i].CurrentCell == C) return false;
					}
					Floor = existing;
					return true;
				}
			}
			GameObject floor = GameObject.Create(blueprint);
			if (floor == null)
			{
				KingdomLog.Log("roads: no blueprint named " + blueprint + "; the ground was left as it was");
				return false;
			}
			List<GameObject> previous = new List<GameObject>();
			foreach (GameObject item in C.GetObjects())
			{
				if (item.GetIntProperty(PathStateProperty) > 0) previous.Add(item);
			}
			floor.SetIntProperty(PathStateProperty, (int)State);
			if (Job != null)
			{
				KingdomConstruction.Bind(floor, Job);
			}
			GameObject accepted = null;
			try { accepted = C.AddObject(floor); }
			finally { KingdomSurvey.ObserveAddResultInActive(C.ParentZone, floor, accepted); }
			if (!ReferenceEquals(accepted, floor)) return false;
			if (floor.CurrentCell != C)
			{
				// Measured rather than trusted (STANDARDS 1): if the engine declined the cell for
				// any reason, the ground keeps exactly what it had and nothing is taken up.
				try { floor.Obliterate(); }
				finally { KingdomSurvey.ObserveCurrentTopologyInActive(C.ParentZone, floor); }
				return false;
			}
			for (int i = 0; i < previous.Count; i++)
			{
				bool removed;
				try { removed = previous[i].Obliterate(null, Silent: true); }
				finally
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(C.ParentZone, previous[i]);
				}
				if (removed && !GameObject.Validate(previous[i]))
					KingdomSurvey.ObserveRemovedFromActive(C.ParentZone, previous[i]);
			}
			for (int i = 0; i < previous.Count; i++)
			{
				if (previous[i].CurrentCell == C) return false;
			}
			Floor = floor;
			return true;
		}

		private static void Announce(KingdomSystem System, Zone Z, KingdomRoadRules.WearState Reached, bool Full, int Tracked)
		{
			if (Full)
			{
				if (Z.GetZoneProperty(FullSaidProperty, null) != "1")
				{
					Z.SetZoneProperty(FullSaidProperty, "1");
					System.Ledger.Note(KingdomRoadRules.RefuseTallyFull(KingdomPresentation.Rich(System.SeatName)));
				}
			}
			else if (Tracked < KingdomRoadRules.MaxTrackedCells)
			{
				Z.SetZoneProperty(FullSaidProperty, "0");
			}
			if (Reached <= KingdomRoadRules.WearState.Worn)
			{
				return;
			}
			int said = int.TryParse(Z.GetZoneProperty(SaidProperty, null), out var value) ? value : 0;
			if ((int)Reached <= said)
			{
				return;
			}
			Z.SetZoneProperty(SaidProperty, ((int)Reached).ToString());
			string line = KingdomRoadRules.WearLine(Reached, KingdomPresentation.Rich(System.SeatName));
			if (line != null)
			{
				System.Ledger.Note(line);
			}
			if (Reached == KingdomRoadRules.WearState.Path)
			{
				KingdomChronicle.Record(System, "paths showed themselves through " + KingdomPresentation.Rich(System.KingdomDisplayName)
					+ ", worn by nothing but the going back and forth of the people who live there");
			}
		}

		// --- Paving -------------------------------------------------------------------------

		/// <summary>Cells of this zone that are a path and not yet paved, nearest a given cell
		/// first, ties broken north-then-west so the same order comes back every time.</summary>
		/// <param name="Z">The zone. Null yields an empty list.</param>
		/// <param name="From">Where the founder is standing, for the ordering. Null orders purely
		/// by position.</param>
		public static List<Cell> PathCells(Zone Z, Cell From)
		{
			List<Cell> cells = new List<Cell>();
			if (Z == null)
			{
				return cells;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item == null || item.GetIntProperty(PathStateProperty) != (int)KingdomRoadRules.WearState.Path)
				{
					continue;
				}
				Cell cell = item.CurrentCell;
				if (cell != null)
				{
					cells.Add(cell);
				}
			}
			cells.Sort(delegate(Cell a, Cell b)
			{
				if (From != null)
				{
					int da = KingdomLayoutRules.Chebyshev(a.X, a.Y, From.X, From.Y);
					int db = KingdomLayoutRules.Chebyshev(b.X, b.Y, From.X, From.Y);
					if (da != db)
					{
						return da - db;
					}
				}
				if (a.Y != b.Y)
				{
					return a.Y - b.Y;
				}
				return a.X - b.X;
			});
			return cells;
		}

		/// <summary>
		/// Lays the settlement's worn paths in the material it builds its walls in.
		/// <para>
		/// Consent before cost: the founder is shown the cells and the price and asked, and a
		/// refusal spends nothing and changes nothing. Nothing is paved that is not already a
		/// path &mdash; the founder formalises what the settlement decided by walking, and never
		/// decides it for them.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">The ground; must be the kingdom's own claim.</param>
		/// <param name="From">Where the founder stands, so a great city paves what is underfoot
		/// first. May be null.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// spent and nothing is laid when it does.</param>
		/// <returns>True once ground has actually been paved. A declined confirmation returns
		/// false with a null <paramref name="Failure"/>, because a refusal is free and is not an
		/// error.</returns>
		public static bool Pave(KingdomSystem System, Zone Z, Cell From, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (!Enabled)
			{
				Failure = "Ground here does not wear, so there is nothing worn to lay. (Options: the settlement's ways)";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = KingdomRoadRules.RefuseNotOurGround();
				return false;
			}
			if (KingdomConstruction.HasActive(System, Z, KingdomConstructionRoute.RoadPaving))
			{
				Failure = "A paid paving order on this ground is already in hand.";
				return false;
			}
			List<Cell> paths = PathCells(Z, From);
			if (paths.Count == 0)
			{
				Failure = KingdomRoadRules.RefuseNothingWorn(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			string wall = KingdomPlotRules.WallBlueprintFor(System.Style, System.FoundingRegionName);
			KingdomMaterial material = KingdomRoadRules.PaveMaterialFor(wall);
			if (!KingdomRoadRules.CanPaveIn(material))
			{
				Failure = KingdomRoadRules.RefuseMaterialKind(material);
				return false;
			}
			if (KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew) <= 0)
			{
				Failure = KingdomRoadRules.RefuseHands(KingdomPresentation.Rich(System.SeatName));
				return false;
			}
			int cells = KingdomRoadRules.PaveCells(paths.Count);
			int cost = KingdomRoadRules.PaveCost(cells);
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			int held = stock.Tally.Get(material);
			if (held < cost)
			{
				Failure = KingdomRoadRules.RefuseMaterial(material, cost, held);
				return false;
			}
			if (Popup.ShowYesNo("Lay " + cells + ((cells == 1) ? " cell" : " cells") + " of worn path at " + KingdomPresentation.Rich(System.SeatName)
				+ " in {{C|" + KingdomMaterialRules.MaterialName(material) + "}}?\n\nIt costs " + cost + " of it, and no water. "
				+ ((paths.Count > cells) ? ("There is more worn ground than one order covers; " + (paths.Count - cells) + " more will wait for the next.\n\n") : "")
				+ "Nothing changes about where anyone walks. The settlement only stops pretending it has not decided.") != DialogResult.Yes)
			{
				return false;
			}
			string blueprint = KingdomRoadRules.PavedFloorFor(wall);
			List<KingdomConstructionCell> route = new List<KingdomConstructionCell>();
			for (int i = 0; i < cells; i++)
			{
				route.Add(new KingdomConstructionCell(paths[i].X, paths[i].Y));
			}
			if (!KingdomConstructionRules.TryEncodeCells(route, out string payload))
			{
				Failure = "The paving route could not be recorded safely. Nothing was spent.";
				return false;
			}
			KingdomMaterialTally price = new KingdomMaterialTally();
			price.Add(material, cost);
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(price);
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			KingdomWaterDebit water = survey.ReserveExactWater(0);
			KingdomMaterialDebit materials = KingdomMaterials.ReserveComposite(Z, claim);
			KingdomConstructionJob job = KingdomConstruction.NewJob(System, Z,
				KingdomConstructionRoute.RoadPaving, paths[0], null, blueprint, payload, 0, claim);
			KingdomConstructionStartResult funding = KingdomConstruction.TryFundNew(job,
				water, materials, out job, out string fundingFailure);
			if (funding == KingdomConstructionStartResult.Refused)
			{
				Failure = fundingFailure ?? "The stockpiles could not cover the paving after all.";
				return false;
			}
			KingdomGovernanceScope.Commit("pave ground");
			if (funding == KingdomConstructionStartResult.Outstanding)
			{
				System.Ledger.Note("{{r|The paving receipt remains outstanding and will retry without another charge.}}");
				return true;
			}
			if (!ProjectPaving(Z, blueprint, route, job, out job, out int laid,
				out string projectionFailure))
			{
				System.Ledger.Note("{{r|The paid paving could not all be laid. Its exact remaining cells stay queued.}}");
				KingdomLog.Log("construction: paving projection waits: " + projectionFailure);
				return true;
			}
			// Paving retires cells from the tally, so the ground the settlement is wearing now
			// has room to be recorded again, and the reason it stalled is over.
			SettleRoadTerminal(System, Z, blueprint, route, ref job);
			KingdomLog.Log("roads: paved " + laid + " cells in " + KingdomMaterialRules.MaterialKey(material) + " at " + System.SeatName);
			return true;
		}

		private static bool SettleRoadTerminal(KingdomSystem System, Zone Z,
			string Blueprint, IList<KingdomConstructionCell> Cells,
			ref KingdomConstructionJob Job)
		{
			if (System == null || Job == null || Job.Phase != KingdomConstructionPhase.Complete
				|| Job.PhysicalPhase == KingdomPhysicalPhase.Settled) return Job != null
					&& Job.PhysicalPhase == KingdomPhysicalPhase.Settled;
			if (Job.PhysicalPhase != KingdomPhysicalPhase.RoadTallySettled
				|| !CurrentRoadOwner(Z, Job)
				|| !RoadTerminalExact(Z, Blueprint, Cells, Job)
				|| !KingdomCeremony.EnsureRoadPavedFromReceipt(System, ref Job)) return false;
			return KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.Settled,
				Job.PhysicalIndex, Job.PhysicalAmount, Job.PhysicalSpilled,
				Job.PhysicalItemId, Job.PhysicalDestinationId, Job.PhysicalReceipt);
		}

		private sealed class RoadReceipt
		{
			public string TallyBefore, TallyAfter, FullBefore, FullAfter;
			public int State;
			public List<RoadRow> Rows = new List<RoadRow>();
		}

		private sealed class RoadRow
		{
			public int X, Y;
			public string OldId, OldBlueprint, NewId;
			public bool Settled;
		}

		private static bool RoadTerminalExact(Zone Z, string Blueprint,
			IList<KingdomConstructionCell> Cells, KingdomConstructionJob Job)
		{
			if (Z == null || string.IsNullOrEmpty(Blueprint) || Cells == null || Job == null
				|| !TryDecodeRoadReceipt(Job.PhysicalReceipt, out var receipt)
				|| receipt.State != 2 || receipt.Rows.Count != Cells.Count
				|| Job.PhysicalIndex != receipt.Rows.Count
				|| (Z.GetZoneProperty(TallyProperty, null) ?? "") != receipt.TallyAfter
				|| (Z.GetZoneProperty(FullSaidProperty, null) ?? "") != receipt.FullAfter) return false;
			for (int i = 0; i < receipt.Rows.Count; i++)
			{
				RoadRow row = receipt.Rows[i];
				if (!row.Settled || row.X != Cells[i].X || row.Y != Cells[i].Y
					|| !ExactRoadFloor(Z, row, Blueprint, Job, true)) return false;
			}
			return true;
		}

		private static bool ProjectPaving(Zone Z, string Blueprint,
			IList<KingdomConstructionCell> Cells, KingdomConstructionJob Job,
			out KingdomConstructionJob Updated, out int NewlyLaid, out string Failure)
		{
			Updated = Job;
			NewlyLaid = 0;
			Failure = null;
			if (Z == null || string.IsNullOrEmpty(Blueprint) || Cells == null || Cells.Count == 0
				|| !CurrentRoadOwner(Z, Job))
				return false;
			if (KingdomConstructionRules.IsTerminal(Updated.Phase))
				return Updated.Phase == KingdomConstructionPhase.Complete
					&& Updated.PhysicalPhase == KingdomPhysicalPhase.RoadTallySettled;
			RoadReceipt receipt;
			if (Updated.PhysicalPhase == KingdomPhysicalPhase.None)
			{
				if (Updated.Phase != KingdomConstructionPhase.ProjectionPending
					&& !KingdomConstruction.BeginProjection(ref Updated, out Failure)) return false;
				if (!FreezeRoadReceipt(Z, Cells, out receipt))
				{
					Failure = "The exact old road-floor identities or tally could not be frozen.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (!KingdomConstruction.UpdatePhysical(ref Updated,
					KingdomPhysicalPhase.RoadPlanFrozen, 0, 0, 0, null, null,
					EncodeRoadReceipt(receipt))) return false;
			}
			if (!TryDecodeRoadReceipt(Updated.PhysicalReceipt, out receipt)
				|| receipt.Rows.Count != Cells.Count || Updated.PhysicalIndex > receipt.Rows.Count)
			{
				Failure = "The frozen road receipt is malformed.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			for (int i = 0; i < receipt.Rows.Count; i++)
			{
				RoadRow row = receipt.Rows[i];
				if (row.X != Cells[i].X || row.Y != Cells[i].Y)
				{
					Failure = "Road receipt coordinates no longer match the frozen route.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (row.Settled)
				{
					if (!ExactRoadFloor(Z, row, Blueprint, Updated, true))
					{
						Failure = "A settled paved floor moved, changed, or was replaced.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					continue;
				}
				Cell cell = Z.GetCell(row.X, row.Y);
				GameObject old;
				GameObject floor;
				KingdomPhysicalLookupState oldState = FindRoadId(Z, row.OldId, out old);
				KingdomPhysicalLookupState floorState = FindRoadId(Z, row.NewId, out floor);
				if (oldState == KingdomPhysicalLookupState.Ambiguous
					|| floorState == KingdomPhysicalLookupState.Ambiguous)
				{
					Failure = "A road receipt ID resolves to more than one loaded physical object.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (Updated.PhysicalPhase == KingdomPhysicalPhase.RoadOutputPending
					&& Updated.PhysicalIndex == i)
				{
					if (!ExactRoadOld(old, cell, row) || !ExactRoadFloor(Z, row,
						Blueprint, Updated, false))
					{
						Failure = "Road AddObject was interrupted without exact old/new proof.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!KingdomConstruction.UpdatePhysical(ref Updated,
						KingdomPhysicalPhase.RoadOutputSettled, i, 0, 0,
						row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
				}
				else if (Updated.PhysicalPhase == KingdomPhysicalPhase.RoadRemovalPending
					&& Updated.PhysicalIndex == i)
				{
					Failure = "Road predecessor removal was interrupted before callback-success proof.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				else if (Updated.PhysicalPhase == KingdomPhysicalPhase.RoadPlanFrozen)
				{
					if (!ExactRoadOld(old, cell, row)
						|| floorState != KingdomPhysicalLookupState.Absent)
					{
						Failure = "A frozen old road floor changed before paving.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (string.IsNullOrEmpty(row.NewId))
					{
						do { row.NewId = System.Guid.NewGuid().ToString("N"); }
						while (FindRoadId(Z, row.NewId, out _)
							== KingdomPhysicalLookupState.Exact);
						if (FindRoadId(Z, row.NewId, out _)
							!= KingdomPhysicalLookupState.Absent
							|| !KingdomConstruction.UpdatePhysical(ref Updated,
								KingdomPhysicalPhase.RoadPlanFrozen, i, 0, 0,
								row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
						floorState = FindRoadId(Z, row.NewId, out floor);
					}
					if (floorState != KingdomPhysicalLookupState.Absent)
					{
						Failure = "The frozen road output ID is absent, duplicated, or already occupied.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					try { floor = GameObject.Create(Blueprint); }
					catch (System.Exception ex)
					{
						Failure = "Road floor creation threw: " + ex.Message;
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!GameObject.Validate(floor))
					{
						Failure = "Road floor blueprint created no exact output.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!CurrentRoadOwner(Z, Updated) || !ExactRoadOld(old, cell, row)
						|| FindRoadId(Z, row.NewId, out _) != KingdomPhysicalLookupState.Absent)
					{
						RemoveRoadObject(floor, Z);
						Failure = "Road endpoints changed during output creation.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					floor.ID = row.NewId;
					floor.SetIntProperty(PathStateProperty,
						(int)KingdomRoadRules.WearState.Paved);
					KingdomConstruction.Bind(floor, Updated);
					if (!KingdomConstruction.UpdatePhysical(ref Updated,
						KingdomPhysicalPhase.RoadOutputPending, i, 0, 0,
						row.OldId, row.NewId, EncodeRoadReceipt(receipt)))
					{
						RemoveRoadObject(floor, Z);
						return false;
					}
					GameObject accepted;
					try
					{
						accepted = cell.AddObject(floor);
						KingdomSurvey.ObserveAddResultInActive(Z, floor, accepted);
					}
					catch (System.Exception ex)
					{
						bool cleaned = RemoveRoadObject(floor, Z);
						Failure = (cleaned ? "Road AddObject threw after output publication: "
							: "Road AddObject threw and cleanup failed: ") + ex.Message;
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!ReferenceEquals(accepted, floor) || !CurrentRoadOwner(Z, Updated)
						|| !ExactRoadOld(old, cell, row)
						|| !ExactRoadFloor(Z, row, Blueprint, Updated, false))
					{
						Failure = "Road endpoints changed during AddObject.";
						KingdomConstruction.Quarantine(ref Updated, Failure);
						return false;
					}
					if (!KingdomConstruction.UpdatePhysical(ref Updated,
						KingdomPhysicalPhase.RoadOutputSettled, i, 0, 0,
						row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
				}
				if (Updated.PhysicalPhase != KingdomPhysicalPhase.RoadOutputSettled
					|| !KingdomConstruction.UpdatePhysical(ref Updated,
						KingdomPhysicalPhase.RoadRemovalPending, i, 0, 0,
						row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
				bool removed;
				try { removed = old.Obliterate(null, Silent: true); }
				catch (System.Exception ex)
				{
					KingdomSurvey.ObserveCurrentTopologyInActive(Z, old);
					Failure = "Road predecessor removal threw: " + ex.Message;
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				if (removed && !GameObject.Validate(old))
					KingdomSurvey.ObserveRemovedFromActive(Z, old);
				KingdomPhysicalLookupState oldAfter = FindRoadId(Z, row.OldId, out var oldReplacement);
				if (!removed || GameObject.Validate(old)
					|| oldAfter != KingdomPhysicalLookupState.Absent
					|| GameObject.Validate(oldReplacement) || !CurrentRoadOwner(Z, Updated)
					|| !ExactRoadFloor(Z, row, Blueprint, Updated, false))
				{
					Failure = "Road predecessor removal was vetoed, moved, or replaced.";
					KingdomConstruction.Quarantine(ref Updated, Failure);
					return false;
				}
				row.Settled = true;
				NewlyLaid++;
				if (!KingdomConstruction.UpdatePhysical(ref Updated,
					KingdomPhysicalPhase.RoadPlanFrozen, i + 1, 0, 0,
					row.OldId, row.NewId, EncodeRoadReceipt(receipt))) return false;
			}
			if (receipt.State == 0)
			{
				receipt.State = 1;
				if (!KingdomConstruction.UpdatePhysical(ref Updated,
					KingdomPhysicalPhase.RoadTallyPending, receipt.Rows.Count, 0, 0,
					null, null, EncodeRoadReceipt(receipt))) return false;
			}
			if (receipt.State != 1 || Updated.PhysicalPhase != KingdomPhysicalPhase.RoadTallyPending)
			{
				Failure = "Road tally receipt carries an impossible state.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			string tally = Z.GetZoneProperty(TallyProperty, null) ?? "";
			if (tally == receipt.TallyBefore)
			{
				Z.SetZoneProperty(TallyProperty, receipt.TallyAfter);
				if (!CurrentRoadOwner(Z, Updated)) return false;
			}
			else if (tally != receipt.TallyAfter)
			{
				Failure = "Road tally changed outside its frozen before/after values.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			string full = Z.GetZoneProperty(FullSaidProperty, null) ?? "";
			if (full == receipt.FullBefore)
			{
				Z.SetZoneProperty(FullSaidProperty, receipt.FullAfter);
				if (!CurrentRoadOwner(Z, Updated)) return false;
			}
			else if (full != receipt.FullAfter)
			{
				Failure = "Road full-tally notice changed outside its frozen before/after values.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			if ((Z.GetZoneProperty(TallyProperty, null) ?? "") != receipt.TallyAfter
				|| (Z.GetZoneProperty(FullSaidProperty, null) ?? "") != receipt.FullAfter)
				return false;
			receipt.State = 2;
			if (!KingdomConstruction.UpdatePhysical(ref Updated,
				KingdomPhysicalPhase.RoadTallySettled, receipt.Rows.Count, 0, 0,
				null, null, EncodeRoadReceipt(receipt))) return false;
			return KingdomConstruction.Complete(ref Updated);
		}

		private static bool FreezeRoadReceipt(Zone Z, IList<KingdomConstructionCell> Cells,
			out RoadReceipt Receipt)
		{
			Receipt = null;
			string raw = Z.GetZoneProperty(TallyProperty, null) ?? "";
			if (!KingdomRoadRules.TryDecode(raw, out var tally, out _)) return false;
			RoadReceipt receipt = new RoadReceipt
			{
				TallyBefore = raw,
				FullBefore = Z.GetZoneProperty(FullSaidProperty, null) ?? "",
				FullAfter = "0"
			};
			HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal);
			for (int i = 0; i < Cells.Count; i++)
			{
				Cell cell = Z.GetCell(Cells[i].X, Cells[i].Y);
				GameObject old = null;
				foreach (GameObject item in cell?.GetObjects() ?? new List<GameObject>())
				{
					if (GameObject.Validate(item) && item.GetIntProperty(PathStateProperty) > 0)
					{
						if (old != null) return false;
						old = item;
					}
				}
				if (!GameObject.Validate(old)
					|| old.GetIntProperty(PathStateProperty) != (int)KingdomRoadRules.WearState.Path
					|| !ids.Add(old.ID)) return false;
				if (KingdomConstruction.FindExactId(Z, old.ID, out var exactOld)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exactOld, old)) return false;
				string outputId;
				do { outputId = System.Guid.NewGuid().ToString("N"); }
				while (ids.Contains(outputId));
				if (KingdomConstruction.FindExactId(Z, outputId, out _)
					!= KingdomPhysicalLookupState.Absent || !ids.Add(outputId)) return false;
				receipt.Rows.Add(new RoadRow { X = Cells[i].X, Y = Cells[i].Y,
					OldId = old.ID, OldBlueprint = old.Blueprint, NewId = outputId });
				KingdomRoadRules.Retire(tally, Cells[i].X, Cells[i].Y);
			}
			receipt.TallyAfter = KingdomRoadRules.Encode(tally) ?? "";
			Receipt = receipt;
			return true;
		}

		private static string EncodeRoadReceipt(RoadReceipt Receipt)
		{
			if (Receipt == null || Receipt.Rows == null
				|| Receipt.Rows.Count > KingdomRoadRules.MaxRouteCells) return null;
			System.Text.StringBuilder text = new System.Text.StringBuilder("r1|")
				.Append(RoadText(Receipt.TallyBefore)).Append('|').Append(RoadText(Receipt.TallyAfter))
				.Append('|').Append(RoadText(Receipt.FullBefore)).Append('|')
				.Append(RoadText(Receipt.FullAfter)).Append('|').Append(Receipt.State.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture));
			for (int i = 0; i < Receipt.Rows.Count; i++)
			{
				RoadRow row = Receipt.Rows[i];
				if (row == null || string.IsNullOrEmpty(row.OldId)
					|| string.IsNullOrEmpty(row.OldBlueprint)) return null;
				text.Append(';').Append(row.X.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture)).Append(',')
					.Append(row.Y.ToString(global::System.Globalization.CultureInfo.InvariantCulture)).Append(',')
					.Append(RoadText(row.OldId)).Append(',').Append(RoadText(row.OldBlueprint))
					.Append(',').Append(RoadText(row.NewId ?? "")).Append(',')
					.Append(row.Settled ? '1' : '0');
			}
			return text.Length <= KingdomConstructionRules.MaxPhysicalReceiptChars
				? text.ToString() : null;
		}

		private static bool TryDecodeRoadReceipt(string Text, out RoadReceipt Receipt)
		{
			Receipt = null;
			if (string.IsNullOrEmpty(Text)
				|| Text.Length > KingdomConstructionRules.MaxPhysicalReceiptChars) return false;
			string[] terms = Text.Split(';');
			string[] head = terms[0].Split('|');
			if (head.Length != 6 || head[0] != "r1" || terms.Length - 1 > KingdomRoadRules.MaxRouteCells
				|| !TryRoadInt(head[5], 2, out int state)) return false;
			try
			{
				RoadReceipt parsed = new RoadReceipt { TallyBefore = UnroadText(head[1]),
					TallyAfter = UnroadText(head[2]), FullBefore = UnroadText(head[3]),
					FullAfter = UnroadText(head[4]), State = state };
				HashSet<string> ids = new HashSet<string>(System.StringComparer.Ordinal);
				for (int i = 1; i < terms.Length; i++)
				{
					string[] f = terms[i].Split(',');
					if (f.Length != 6 || (f[5] != "0" && f[5] != "1")
						|| !TryRoadInt(f[0], 1023, out int x)
						|| !TryRoadInt(f[1], 1023, out int y)) return false;
					string id = UnroadText(f[2]), blueprint = UnroadText(f[3]);
					string output = UnroadText(f[4]);
					if (string.IsNullOrEmpty(id) || id.Length > 128
						|| string.IsNullOrEmpty(blueprint) || blueprint.Length > 256
						|| output.Length > 128 || !ids.Add(id)
						|| (output.Length > 0 && !ids.Add(output))) return false;
					parsed.Rows.Add(new RoadRow { X = x, Y = y, OldId = id,
						OldBlueprint = blueprint, NewId = output.Length == 0 ? null : output,
						Settled = f[5] == "1" });
				}
				if (EncodeRoadReceipt(parsed) != Text) return false;
				Receipt = parsed;
				return true;
			}
			catch { return false; }
		}

		private static bool TryRoadInt(string Text, int Maximum, out int Value)
		{
			return int.TryParse(Text, global::System.Globalization.NumberStyles.None,
				global::System.Globalization.CultureInfo.InvariantCulture, out Value)
				&& Value >= 0 && Value <= Maximum
				&& Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture) == Text;
		}

		private static string RoadText(string Value)
		{
			return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static string UnroadText(string Value)
		{
			return System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(Value));
		}

		private static KingdomPhysicalLookupState FindRoadId(Zone Z, string Id,
			out GameObject Exact)
		{
			return KingdomConstruction.FindExactId(Z, Id, out Exact);
		}

		private static bool ExactRoadOld(GameObject Old, Cell Cell, RoadRow Row)
		{
			GameObject global;
			return GameObject.Validate(Old) && Cell != null && Old.ID == Row.OldId
				&& Old.CurrentCell == Cell
				&& Old.Blueprint == Row.OldBlueprint
				&& Old.GetIntProperty(PathStateProperty) == (int)KingdomRoadRules.WearState.Path
				&& FindRoadId(Cell.ParentZone, Row.OldId, out global)
					== KingdomPhysicalLookupState.Exact
				&& ReferenceEquals(global, Old);
		}

		private static bool ExactRoadFloor(Zone Z, RoadRow Row, string Blueprint,
			KingdomConstructionJob Job, bool RequireOldAbsent)
		{
			GameObject floor;
			if (FindRoadId(Z, Row.NewId, out floor) != KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(floor) || floor.CurrentCell != Z.GetCell(Row.X, Row.Y)
				|| floor.Blueprint != Blueprint
				|| floor.GetIntProperty(PathStateProperty) != (int)KingdomRoadRules.WearState.Paved
				|| !KingdomConstruction.HasReceipt(floor, Job)) return false;
			if (RequireOldAbsent && FindRoadId(Z, Row.OldId, out _)
				!= KingdomPhysicalLookupState.Absent) return false;
			foreach (GameObject item in floor.CurrentCell.GetObjects())
				if (item != floor && item.GetIntProperty(PathStateProperty) > 0) return false;
			return true;
		}

		private static bool RemoveRoadObject(GameObject Object, Zone Z)
		{
			if (!GameObject.Validate(Object))
			{
				KingdomSurvey.ObserveRemovedFromActive(Z, Object);
				return true;
			}
			try
			{
				return Object.Obliterate(null, Silent: true) && !GameObject.Validate(Object);
			}
			catch { return false; }
			finally
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Z, Object);
			}
		}

		private static bool CurrentRoadOwner(Zone Z, KingdomConstructionJob Job)
		{
			KingdomSystem system = The.Game == null
				? null : The.Game.RequireSystem<KingdomSystem>();
			return KingdomConstruction.Owns(system, Z, Job)
				&& KingdomConstruction.IsCurrent(Job);
		}

		/// <summary>
		/// One line for the status report: what the settlement's own feet have made of its
		/// ground. Never null, and never silent about a full tally (STANDARDS 7b).
		/// </summary>
		/// <param name="Z">The zone. Null answers the line for ground nobody walks.</param>
		public static string WornLine(Zone Z)
		{
			if (!Enabled)
			{
				return "Ground here does not wear. (Options: the settlement's ways)";
			}
			if (Z == null)
			{
				return "No ground here is walked enough to show it.";
			}
			int paths = 0;
			int paved = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				int state = (item == null) ? 0 : item.GetIntProperty(PathStateProperty);
				if (state == (int)KingdomRoadRules.WearState.Path)
				{
					paths++;
				}
				else if (state == (int)KingdomRoadRules.WearState.Paved)
				{
					paved++;
				}
			}
			int worn = ReadTally(Z).Count;
			if (paths == 0 && paved == 0 && worn == 0)
			{
				return "No ground here is walked enough to show it.";
			}
			string line = "The ground shows " + worn + ((worn == 1) ? " cell" : " cells") + " of wearing, "
				+ paths + ((paths == 1) ? " cell" : " cells") + " of path, and "
				+ paved + ((paved == 1) ? " cell" : " cells") + " of paving.";
			if (paths > 0)
			{
				line += " (Charter: pave a worn path)";
			}
			return line;
		}
	}
}
