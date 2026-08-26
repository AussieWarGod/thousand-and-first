using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of Addendum 6 (<see cref="KingdomReachRules"/> owns every decision
	/// that has one right answer given the facts): the <c>Reach</c> registry, the derivation of a
	/// design's band from the ground it stands on and its place in its own chain, the measured
	/// quarter, and the office seat a great work is.
	/// <para>
	/// <b>What reaches what.</b> The three binding goods stay citywide pools &mdash; water, food
	/// and roofs are drawn and carried, and nothing here touches them. Everything else a work
	/// gives shades only what the work reaches, so the ground around a temple is different ground
	/// from the ground around a tannery and neither of them needed a district to say so.
	/// </para>
	/// <para>
	/// <b>The great work is an office.</b> An XL's citywide effect is live only while a named
	/// notable heads it. Nobody is appointed: the seat is filled by the office machinery from the
	/// settlers who are actually here, scored on the attributes they already have
	/// (<see cref="KingdomReachRules.SeatFitness"/>), the way
	/// <c>KingdomOffices.UpdateOffice</c> fills the settlement's own office from whoever has
	/// served longest. An unheaded great work is not a broken one: it keeps its own zone and says
	/// so once (STANDARDS 7b).
	/// </para>
	/// <para>
	/// <b>State.</b> Almost none. Bands are registry data, recomputed from the merged catalogue
	/// every load, so a save carries none of it. A seat is two string properties on the work
	/// itself, which is the object that would be destroyed if the work were struck. The one
	/// realm-level record &mdash; what a claimed zone's headed great works shade the city with
	/// &mdash; lives in the game's own already-serialized state slots under
	/// <see cref="CityStatePrefix"/>, exactly as <c>KingdomPlots.MaterialStatePrefix</c> does, so
	/// no positionally-reflected field layout on <c>KingdomSystem</c> is touched and there is no
	/// seat-carry field to keep symmetric.
	/// </para>
	/// </summary>
	public static class KingdomReach
	{
		/// <summary>The <c>learning</c> support, named once so callers asking the chronicle's own
		/// question do not spell it themselves.</summary>
		public const string LearningSupport = "learning";

		/// <summary>Raw property AssignWork stamps on every crewed work, and the one this file
		/// reads to know how well a work is running. Spelled as the literal, following
		/// <c>KingdomFaith</c>'s own precedent rather than inventing a second const for it.
		/// </summary>
		private const string EffectivenessProperty = "KingdomEffectiveness";

		private const string StaffNeededProperty = "KingdomStaffNeeded";

		// --- The Reach registry --------------------------------------------------------------

		// Keyed by building Key like every other registry beside the catalogue (STANDARDS 6): a
		// later file re-using a key owns that design's Reach, and re-declaring the design WITHOUT
		// the attribute correctly returns it to the derivation. Raw strings, parsed on read,
		// because the merge layer hands this the merged attribute and merges happen before
		// anything is parsed.
		private static readonly Dictionary<string, ReachBand> Declared = new Dictionary<string, ReachBand>();

		private static readonly Dictionary<string, ReachBand> BandCache = new Dictionary<string, ReachBand>();

		private static readonly Dictionary<string, ChainPlace> PlaceCache = new Dictionary<string, ChainPlace>();

		private sealed class ChainPlace
		{
			public int Index;

			public int Count;
		}

		/// <summary>Forgets every declared and derived band. Called by the registry loader before
		/// it re-reads the XML streams, beside <c>KingdomLodging.ClearCloseness</c>.</summary>
		public static void ClearReach()
		{
			Declared.Clear();
			BandCache.Clear();
			PlaceCache.Clear();
		}

		/// <summary>
		/// Registers one entry's <c>Reach</c> override as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the merged raw
		/// attribute; null or blank registers "derive me", which is every design in the catalogue
		/// and every design any mod will ever write without thinking about reach at all.
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="Reach">Raw <c>Reach</c> attribute: <c>plot</c>, <c>quarter</c>,
		/// <c>zone</c>, <c>city</c> or <c>realm</c>. A word this build does not know is logged and
		/// the design falls back to the derivation &mdash; hostile-input discipline, STANDARDS 9:
		/// a malformed attribute disables itself and never takes a design out of the
		/// catalogue.</param>
		public static void RegisterReach(string Key, string Reach)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			Declared.Remove(Key);
			BandCache.Clear();
			PlaceCache.Clear();
			ReachBand band;
			string error;
			if (KingdomReachRules.TryParseBand(Reach, out band, out error))
			{
				Declared[Key] = band;
				return;
			}
			if (error != null)
			{
				KingdomLog.Log("KingdomBuildings: building " + Key + " declares Reach=" + error
					+ ". Deriving it from the plot it stands on instead.");
			}
		}

		/// <summary>What one design was registered as reaching, where it declared anything.
		/// </summary>
		/// <returns>False for the ordinary case: a design that says nothing and is derived.
		/// </returns>
		public static bool TryGetDeclared(string Key, out ReachBand Band)
		{
			Band = ReachBand.Plot;
			return !string.IsNullOrEmpty(Key) && Declared.TryGetValue(Key, out Band);
		}

		/// <summary>
		/// How far a design carries: its declared <c>Reach</c>, else derived from its plot tier
		/// and its place in its own improvement chain. Cached per key and dropped whenever the
		/// registry is re-read.
		/// </summary>
		/// <param name="Key">A registry key. Blank reaches its own ground.</param>
		public static ReachBand BandOf(string Key)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return ReachBand.Plot;
			}
			ReachBand cached;
			if (BandCache.TryGetValue(Key, out cached))
			{
				return cached;
			}
			ReachBand declared;
			if (Declared.TryGetValue(Key, out declared))
			{
				BandCache[Key] = declared;
				return declared;
			}
			KingdomPlotRules.PlotSpec spec;
			KingdomPlotRules.PlotSize size = KingdomPlots.TryGetSpec(Key, out spec)
				? spec.Size
				: KingdomPlotRules.PlotSize.None;
			ChainPlace place = PlaceOf(Key);
			ReachBand band = KingdomReachRules.Derive(size, place.Index, place.Count);
			BandCache[Key] = band;
			return band;
		}

		/// <summary>The band the design a standing work was raised under carries, before any seat
		/// is considered.</summary>
		public static ReachBand BandOf(GameObject Work)
		{
			return (Work == null) ? ReachBand.Plot : BandOf(KingdomUpgrade.DesignKeyOf(Work));
		}

		/// <summary>
		/// What a standing work actually reaches right now: its band, dropped to the zone it
		/// stands in while a great work has nobody heading it
		/// (<see cref="KingdomReachRules.Unheaded"/>).
		/// </summary>
		public static ReachBand EffectiveBandOf(GameObject Work)
		{
			ReachBand band = BandOf(Work);
			if (!KingdomReachRules.RequiresSeat(band) || IsHeaded(Work))
			{
				return band;
			}
			return KingdomReachRules.Unheaded(band);
		}

		/// <summary>How far into its quarter a standing work shades, which is where tier moves
		/// the edge inside the band.</summary>
		public static int QuarterRadiusOf(GameObject Work)
		{
			string key = (Work == null) ? null : KingdomUpgrade.DesignKeyOf(Work);
			return KingdomReachRules.QuarterRadius(string.IsNullOrEmpty(key) ? 0 : PlaceOf(key).Index);
		}

		// A design's place in its own chain: how many designs improve INTO it, and how many links
		// the whole chain has. Walked from the registry rather than stored, and cached until the
		// catalogue is re-read. Both walks are ring-guarded; the catalogue validator already
		// reports a ring, and a ring must not also hang the first pass that reads one.
		private static ChainPlace PlaceOf(string Key)
		{
			ChainPlace cached;
			if (PlaceCache.TryGetValue(Key, out cached))
			{
				return cached;
			}
			List<string> back = new List<string> { Key };
			string at = PredecessorOf(Key);
			while (at != null && !back.Contains(at))
			{
				back.Add(at);
				at = PredecessorOf(at);
			}
			List<string> forward = new List<string> { Key };
			KingdomUpgradeRules.UpgradeChain chain;
			string next = KingdomUpgrade.TryGetChain(Key, out chain) ? chain.SuccessorKey : null;
			while (next != null && !forward.Contains(next))
			{
				forward.Add(next);
				next = KingdomUpgrade.TryGetChain(next, out chain) ? chain.SuccessorKey : null;
			}
			ChainPlace place = new ChainPlace
			{
				Index = back.Count - 1,
				Count = (back.Count - 1) + forward.Count
			};
			PlaceCache[Key] = place;
			return place;
		}

		private static string PredecessorOf(string Key)
		{
			List<KingdomRules.BuildEntry> buildings = KingdomData.Buildings;
			for (int i = 0; i < buildings.Count; i++)
			{
				KingdomUpgradeRules.UpgradeChain chain;
				if (KingdomUpgrade.TryGetChain(buildings[i].Key, out chain) && chain.SuccessorKey == Key)
				{
					return buildings[i].Key;
				}
			}
			return null;
		}

		// --- The seat ------------------------------------------------------------------------

		/// <summary>The settler's <c>KingdomName</c> heading this work, or absent for a great work
		/// nobody heads. Written only by <see cref="UpdateSeats"/>.</summary>
		public const string SeatHolderProperty = "KingdomSeatHolder";

		/// <summary>What the holder is called, from <c>KingdomReachRules.SeatTitle</c>, kept on
		/// the work so a rename of the design never renames the office already announced.
		/// </summary>
		public const string SeatTitleProperty = "KingdomSeatTitle";

		/// <summary>What the seated holder scored when they took it, so a challenger is measured
		/// against the notable actually sitting there rather than re-derived from a roster
		/// position that says nothing about fitness.</summary>
		public const string SeatScoreProperty = "KingdomSeatScore";

		/// <summary>STANDARDS 7b's once-only flag: set the first pass a great work stands
		/// unheaded, cleared the pass somebody takes the seat.</summary>
		public const string SeatUnheadedAnnouncedProperty = "KingdomSeatUnheadedSaid";

		/// <summary>Whether a named notable heads this work right now.</summary>
		public static bool IsHeaded(GameObject Work)
		{
			return Work != null && !string.IsNullOrEmpty(Work.GetStringProperty(SeatHolderProperty));
		}

		/// <summary>What the founder calls whoever heads this work, or an empty string when
		/// nobody does.</summary>
		public static string SeatTitleOf(GameObject Work)
		{
			string title = (Work == null) ? null : Work.GetStringProperty(SeatTitleProperty);
			return string.IsNullOrEmpty(title) ? "" : title;
		}

		/// <summary>
		/// The kingdom's one attended pass over this zone's great works: fills, keeps, or passes
		/// the seat each one is, and records what the headed ones shade the city with. Call from
		/// <c>KingdomSystem.HandleEvent(ZoneActivatedEvent)</c> after growth has resolved this
		/// pass's staffing and after <c>KingdomOffices.OnZoneActivated</c>, so the settlement's
		/// own office is settled before its buildings' are. Wrapped by the caller's own
		/// <c>Guard</c>, like every other module's pass.
		/// </summary>
		/// <param name="System">The kingdom. Unfounded, or a zone the realm does not claim, does
		/// nothing.</param>
		/// <param name="Z">The activated zone.</param>
		/// <param name="Survey">This pass's already-taken survey, for its <c>Settlers</c>.</param>
		public static void OnZoneActivated(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!KingdomOffices.Enabled || System == null || !System.Founded || Z == null || Survey == null
				|| !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<KindAmount> shaded = new List<KindAmount>();
			List<KindAmount> realm = new List<KindAmount>();
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject item = Survey.Built[i];
				KingdomRules.BuildEntry entry;
				string key = KingdomUpgrade.DesignKeyOf(item);
				if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
				{
					continue;
				}
				ReachBand band = BandOf(key);
				if (!KingdomReachRules.RequiresSeat(band))
				{
					continue;
				}
				UpdateSeat(System, item, entry, Survey.Settlers);
				if (IsHeaded(item))
				{
					Gather(shaded, entry, item);
					if (EffectiveBandOf(item) == ReachBand.Realm)
					{
						Gather(realm, entry, item);
					}
				}
			}
			Record(Z, shaded, realm);
		}

		private static void UpdateSeat(KingdomSystem System, GameObject Work, KingdomRules.BuildEntry Entry, List<GameObject> Settlers)
		{
			string held = Work.GetStringProperty(SeatHolderProperty);
			// A holder the roster no longer carries is a holder who died, was exiled, or walked
			// out of the settlement: the seat is empty however far away their object may still be
			// standing. A holder the roster keeps but who is not in this zone this pass keeps the
			// seat, exactly as the settlement's own office does.
			Simulation.City.KingdomResidentRow heldRow;
			if (!string.IsNullOrEmpty(held)
				&& !Simulation.City.KingdomResidents.TryFindByName(System, held, out heldRow))
			{
				Vacate(System, Work, Entry, held);
				held = null;
			}
			string title = KingdomReachRules.SeatTitle(Entry.Category);
			int bestScore = -1;
			GameObject best = null;
			int heldScore = -1;
			for (int i = 0; (Settlers != null) && i < Settlers.Count; i++)
			{
				GameObject settler = Settlers[i];
				int score = FitnessOf(Entry.Category, settler);
				string name = settler.GetStringProperty("KingdomName");
				if (!string.IsNullOrEmpty(held) && name == held)
				{
					heldScore = score;
					continue;
				}
				if (score > bestScore || (score == bestScore && Tenure(System, name) < Tenure(System, NameOf(best))))
				{
					bestScore = score;
					best = settler;
				}
			}
			if (!string.IsNullOrEmpty(held))
			{
				// The seated notable's own score is re-read while they are here, so a challenger
				// is measured against the person actually sitting there.
				if (heldScore >= 0)
				{
					Work.SetIntProperty(SeatScoreProperty, heldScore);
				}
				if (best == null || !KingdomReachRules.ShouldUnseat(Work.GetIntProperty(SeatScoreProperty), bestScore))
				{
					return;
				}
				Seat(System, Work, Entry, best, bestScore, title, KingdomOfficeRules.OfficeTransition.Passed);
				return;
			}
			if (best == null)
			{
				Unheaded(Work, Entry, title);
				return;
			}
			Seat(System, Work, Entry, best, bestScore, title, KingdomOfficeRules.OfficeTransition.FirstHolder);
		}

		private static void Seat(KingdomSystem System, GameObject Work, KingdomRules.BuildEntry Entry, GameObject Holder, int Score, string Title, KingdomOfficeRules.OfficeTransition Transition)
		{
			string name = NameOf(Holder);
			if (string.IsNullOrEmpty(name))
			{
				return;
			}
			Work.SetStringProperty(SeatHolderProperty, name);
			Work.SetStringProperty(SeatTitleProperty, Title);
			Work.SetIntProperty(SeatScoreProperty, (Score < 0) ? 0 : Score);
			Work.SetIntProperty(SeatUnheadedAnnouncedProperty, 0);
			Holder.RequirePart<SocialRoles>().RequireRole(Title + " of " + Entry.Name);
			KingdomChronicle.Record(System, KingdomReachRules.SeatChronicle(Transition, Title, name, Entry.Name));
			MessageQueue.AddPlayerMessage(KingdomReachRules.SeatMessage(Transition, Title, name, Entry.Name));
			KingdomLog.Log("reach: seat " + Transition + " title=" + Title + " holder=" + name + " work=" + Entry.Key);
		}

		private static void Vacate(KingdomSystem System, GameObject Work, KingdomRules.BuildEntry Entry, string Held)
		{
			string title = SeatTitleOf(Work);
			if (title.Length == 0)
			{
				title = KingdomReachRules.SeatTitle(Entry.Category);
			}
			Work.SetStringProperty(SeatHolderProperty, null, RemoveIfNull: true);
			Work.SetIntProperty(SeatScoreProperty, 0);
			KingdomChronicle.Record(System, KingdomReachRules.SeatChronicle(KingdomOfficeRules.OfficeTransition.Vacant, title, Held, Entry.Name));
			MessageQueue.AddPlayerMessage(KingdomReachRules.SeatMessage(KingdomOfficeRules.OfficeTransition.Vacant, title, Held, Entry.Name));
			KingdomLog.Log("reach: seat vacated title=" + title + " was=" + Held + " work=" + Entry.Key);
		}

		private static void Unheaded(GameObject Work, KingdomRules.BuildEntry Entry, string Title)
		{
			if (Work.GetIntProperty(SeatUnheadedAnnouncedProperty) == 1)
			{
				return;
			}
			Work.SetIntProperty(SeatUnheadedAnnouncedProperty, 1);
			MessageQueue.AddPlayerMessage(KingdomReachRules.UnheadedLine(Entry.Name, Title));
			KingdomLog.Log("reach: unheaded work=" + Entry.Key + " title=" + Title);
		}

		/// <summary>
		/// How well one settler would head a work of this purpose, read off the attributes the
		/// engine already gives every creature. Nothing is stored on them and nothing is trained:
		/// a settler another mod shipped is scored by exactly the same six numbers.
		/// </summary>
		public static int FitnessOf(string Category, GameObject Settler)
		{
			if (Settler == null)
			{
				return 0;
			}
			return KingdomReachRules.SeatFitness(Category,
				Settler.GetStatValue("Strength"),
				Settler.GetStatValue("Agility"),
				Settler.GetStatValue("Toughness"),
				Settler.GetStatValue("Intelligence"),
				Settler.GetStatValue("Willpower"),
				Settler.GetStatValue("Ego"));
		}

		private static int Tenure(KingdomSystem System, string Name)
		{
			if (System == null || string.IsNullOrEmpty(Name))
			{
				return int.MaxValue;
			}
			List<Simulation.City.KingdomResidentRow> rows =
				Simulation.City.KingdomResidents.RollRows(System);
			for (int i = 0; i < rows.Count; i++)
				if (string.Equals(rows[i].Name, Name, StringComparison.Ordinal)) return i;
			return int.MaxValue;
		}

		private static string NameOf(GameObject Settler)
		{
			return (Settler == null) ? null : Settler.GetStringProperty("KingdomName");
		}

		// --- What reaches a place --------------------------------------------------------------

		/// <summary>
		/// Whether a standing work reaches a resident. The question the shrine, the scriptorium
		/// and every later channel ask; the shrine's "quarter" is this and nothing else.
		/// </summary>
		/// <param name="System">The realm, for what ground it holds.</param>
		/// <param name="WorkZone">The zone the work stands in.</param>
		/// <param name="Work">The standing work. Null reaches nothing.</param>
		/// <param name="At">The resident. Null, or one standing nowhere, is not reached.</param>
		public static bool Reaches(KingdomSystem System, Zone WorkZone, GameObject Work, GameObject At)
		{
			Cell cell = (At == null) ? null : At.CurrentCell;
			return cell != null && ReachesCell(System, WorkZone, Work, cell.ParentZone, cell.X, cell.Y);
		}

		/// <summary>Whether a standing work reaches one cell of one zone.</summary>
		/// <param name="System">The realm, for what ground it holds.</param>
		/// <param name="WorkZone">The zone the work stands in.</param>
		/// <param name="Work">The standing work.</param>
		/// <param name="AtZone">The zone the place is in.</param>
		/// <param name="X">The place.</param>
		/// <param name="Y">The place.</param>
		public static bool ReachesCell(KingdomSystem System, Zone WorkZone, GameObject Work, Zone AtZone, int X, int Y)
		{
			if (Work == null || WorkZone == null || AtZone == null)
			{
				return false;
			}
			return KingdomReachRules.Covers(EffectiveBandOf(Work), RelationOf(System, WorkZone, Work, AtZone, X, Y));
		}

		private static ReachRelation RelationOf(KingdomSystem System, Zone WorkZone, GameObject Work, Zone AtZone, int X, int Y)
		{
			bool sameZone = WorkZone.ZoneID == AtZone.ZoneID;
			bool sameRealm = Holds(System, AtZone.ZoneID);
			bool sameCity = SameCity(System, WorkZone.ZoneID, AtZone.ZoneID);
			bool onFootprint = false;
			bool inQuarter = false;
			if (sameZone)
			{
				KingdomPlotRules.PlotRect footprint;
				onFootprint = KingdomPlots.TryReadFootprint(Work, out footprint) && footprint.Contains(X, Y);
				if (!onFootprint)
				{
					Cell cell = Work.CurrentCell;
					inQuarter = cell != null && KingdomReachRules.InQuarter(MarksOf(WorkZone),
						cell.X, cell.Y, X, Y, KingdomReachRules.QuarterLinkCells, QuarterRadiusOf(Work));
				}
			}
			return KingdomReachRules.RelationAt(sameRealm, sameCity, sameZone, inQuarter, onFootprint);
		}

		// The zone's marks, read once per zone per tick rather than once per settler per shrine.
		// A quarter is measured from what is standing, and nothing is raised or struck between two
		// questions asked on the same tick, so the only thing this drops is a repeated full-zone
		// walk in a loop that asks the same question about twenty people.
		private static List<KingdomLayoutRules.LayoutMark> _marks;

		private static string _marksZone;

		private static long _marksTick = -1L;

		private static List<KingdomLayoutRules.LayoutMark> MarksOf(Zone Z)
		{
			long tick = (The.Game == null) ? 0L : The.Game.TimeTicks;
			if (_marks != null && _marksZone == Z.ZoneID && _marksTick == tick)
			{
				return _marks;
			}
			_marks = KingdomLayout.ReadMarks(Z);
			_marksZone = Z.ZoneID;
			_marksTick = tick;
			return _marks;
		}

		/// <summary>Whether the realm holds this ground at all, either city's.</summary>
		public static bool Holds(KingdomSystem System, string ZoneID)
		{
			if (System == null || string.IsNullOrEmpty(ZoneID))
			{
				return false;
			}
			return System.ClaimedZones.Contains(ZoneID)
				|| (System.Away != null && System.Away.ClaimedZones.Contains(ZoneID));
		}

		private static bool SameCity(KingdomSystem System, string WorkZoneID, string AtZoneID)
		{
			if (System == null)
			{
				return false;
			}
			if (System.ClaimedZones.Contains(WorkZoneID))
			{
				return System.ClaimedZones.Contains(AtZoneID);
			}
			return System.Away != null && System.Away.ClaimedZones.Contains(WorkZoneID)
				&& System.Away.ClaimedZones.Contains(AtZoneID);
		}

		// --- What one piece of ground is like ---------------------------------------------------

		/// <summary>
		/// Everything in reach of one cell, folded into what that ground is like to stand on.
		/// Reads only the zone given, which is the whole of what an attended pass can honestly
		/// see; the citywide half is <see cref="CityShade"/>, which reads what earlier passes
		/// recorded.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone the place is in.</param>
		/// <param name="X">The place.</param>
		/// <param name="Y">The place.</param>
		/// <returns>Never null.</returns>
		public static GroundCharacter CharacterAt(KingdomSystem System, Zone Z, int X, int Y)
		{
			List<KindAmount> lifts = new List<KindAmount>();
			if (Z != null)
			{
				List<KingdomLayoutRules.LayoutMark> marks = MarksOf(Z);
				foreach (GameObject item in KingdomSurvey.ObjectsFor(Z))
				{
					if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
					{
						continue;
					}
					KingdomRules.BuildEntry entry;
					string key = KingdomUpgrade.DesignKeyOf(item);
					if (string.IsNullOrEmpty(key) || !KingdomData.TryGetBuilding(key, out entry))
					{
						continue;
					}
					if (!CoversWithin(item, marks, X, Y))
					{
						continue;
					}
					Gather(lifts, entry, item);
				}
			}
			// Everything standing HERE has just been counted from the ground itself, so the
			// recorded half deliberately skips this zone: the record exists to carry a great work
			// the founder cannot presently see, never to count one twice.
			for (int i = 0; i < KingdomReachRules.LiftOrder.Length; i++)
			{
				string kind = KingdomReachRules.LiftOrder[i];
				int city = CityShadeExcept(System, kind, (Z == null) ? null : Z.ZoneID);
				if (city > 0)
				{
					lifts.Add(new KindAmount(kind, city));
				}
			}
			return KingdomReachRules.Character(lifts);
		}

		// The same-zone half of RelationOf, kept separate so a whole-zone sweep reads the marks
		// once instead of once per work.
		private static bool CoversWithin(GameObject Work, List<KingdomLayoutRules.LayoutMark> Marks, int X, int Y)
		{
			ReachBand band = EffectiveBandOf(Work);
			if (band >= ReachBand.Zone)
			{
				return true;
			}
			KingdomPlotRules.PlotRect footprint;
			if (KingdomPlots.TryReadFootprint(Work, out footprint) && footprint.Contains(X, Y))
			{
				return true;
			}
			if (band != ReachBand.Quarter)
			{
				return false;
			}
			Cell cell = Work.CurrentCell;
			return cell != null && KingdomReachRules.InQuarter(Marks, cell.X, cell.Y, X, Y,
				KingdomReachRules.QuarterLinkCells, QuarterRadiusOf(Work));
		}

		/// <summary>
		/// Whether anything in reach of a place shades it with one kind. The re-based form of
		/// every hand-authored scope: a knowledge work softens the quarrel of whoever it
		/// <em>reaches</em>, not of whoever happens to share a zone with it.
		/// </summary>
		public static bool ShadedAt(KingdomSystem System, Zone Z, int X, int Y, string Kind)
		{
			GroundCharacter character = CharacterAt(System, Z, X, Y);
			for (int i = 0; i < character.Lifts.Count; i++)
			{
				if (character.Lifts[i].Kind == Kind && character.Lifts[i].Amount > 0)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Whether a staffed knowledge work reaches this home &mdash; the re-based form
		/// of <c>KingdomFaith.ZoneEducated</c>. A home standing nowhere is reached by
		/// nothing.</summary>
		public static bool EducatedAt(KingdomSystem System, Zone Z, GameObject Home)
		{
			Cell cell = (Home == null) ? null : Home.CurrentCell;
			return cell != null && ShadedAt(System, Z, cell.X, cell.Y, LearningSupport);
		}

		/// <summary>One line for the status report naming what shades the ground the founder is
		/// standing on (Addendum 6: a quarter's character must be readable).</summary>
		public static string QuarterLine(KingdomSystem System, Zone Z)
		{
			Cell cell = The.Player?.CurrentCell;
			if (cell == null || Z == null || cell.ParentZone == null || cell.ParentZone.ZoneID != Z.ZoneID)
			{
				return "";
			}
			return KingdomReachRules.QuarterLine(CharacterAt(System, Z, cell.X, cell.Y));
		}

		// --- The citywide record -----------------------------------------------------------------

		/// <summary>Game-state key prefix a claimed zone's headed city-band lift is recorded
		/// under, per kind. A generic, already-serialized slot on the game rather than a new field
		/// on <c>KingdomSystem</c>, exactly as <c>KingdomPlots.MaterialStatePrefix</c> is &mdash;
		/// so a citywide effect can be read from a zone that is not loaded without touching any
		/// positionally-reflected field layout.</summary>
		public const string CityStatePrefix = "r_TAF_ReachCity_";

		/// <summary>The same, for a great work whose reach is the whole realm and which therefore
		/// carries into the realm's other city.</summary>
		public const string RealmStatePrefix = "r_TAF_ReachRealm_";

		/// <summary>
		/// What the realm's headed great works shade this city with. Summed from what each
		/// claimed zone's own attended pass last recorded, so nothing here advances while the
		/// founder is away: a zone the founder has not visited since the temple was struck goes on
		/// reporting the temple until they walk back in and see the ground.
		/// </summary>
		/// <param name="System">The realm. Null shades nothing.</param>
		/// <param name="Kind">A lifting support.</param>
		public static int CityShade(KingdomSystem System, string Kind)
		{
			return CityShadeExcept(System, Kind, null);
		}

		/// <summary>The same, less one zone's own record &mdash; for a caller that has just
		/// counted that zone's ground for itself and must not count it twice.</summary>
		public static int CityShadeExcept(KingdomSystem System, string Kind, string ExceptZoneID)
		{
			if (System == null || string.IsNullOrEmpty(Kind) || The.Game == null)
			{
				return 0;
			}
			int total = 0;
			for (int i = 0; i < System.ClaimedZones.Count; i++)
			{
				if (System.ClaimedZones[i] != ExceptZoneID)
				{
					total += The.Game.GetIntGameState(CityStatePrefix + System.ClaimedZones[i] + "_" + Kind);
				}
			}
			if (System.Away != null)
			{
				for (int i = 0; i < System.Away.ClaimedZones.Count; i++)
				{
					if (System.Away.ClaimedZones[i] != ExceptZoneID)
					{
						total += The.Game.GetIntGameState(RealmStatePrefix + System.Away.ClaimedZones[i] + "_" + Kind);
					}
				}
			}
			return total;
		}

		/// <summary>Whether any headed great work shades this city with one kind &mdash; the
		/// question the outsider register asks about a great scriptorium.</summary>
		public static bool CityShaded(KingdomSystem System, string Kind)
		{
			return CityShade(System, Kind) > 0;
		}

		// Rewrites this zone's own record from what is standing here now, including to zero: a
		// great work that was struck, or whose seat emptied, stops shading the city the pass the
		// founder sees it, and never before.
		private static void Record(Zone Z, List<KindAmount> Shaded, List<KindAmount> Realm)
		{
			if (The.Game == null || Z == null)
			{
				return;
			}
			// The realm-band half is written from its own filter rather than derived from the
			// city half: only a work that reaches the whole realm carries into the other city, so
			// a city-band cathedral never shades a city it cannot see.
			GroundCharacter cityCharacter = KingdomReachRules.Character(Shaded);
			GroundCharacter realmCharacter = KingdomReachRules.Character(Realm);
			for (int i = 0; i < KingdomReachRules.LiftOrder.Length; i++)
			{
				string kind = KingdomReachRules.LiftOrder[i];
				The.Game.SetIntGameState(CityStatePrefix + Z.ZoneID + "_" + kind, AmountIn(cityCharacter, kind));
				The.Game.SetIntGameState(RealmStatePrefix + Z.ZoneID + "_" + kind, AmountIn(realmCharacter, kind));
			}
		}

		private static int AmountIn(GroundCharacter Character, string Kind)
		{
			for (int i = 0; i < Character.Lifts.Count; i++)
			{
				if (Character.Lifts[i].Kind == Kind)
				{
					return Character.Lifts[i].Amount;
				}
			}
			return 0;
		}

		// What one standing work actually contributes: its declared lifts, scaled by how well it
		// is running. A work that declares no crew runs at full; a crewed work runs at whatever
		// the staffing pass gave it, so an idle shrine shades nothing and says nothing new.
		private static void Gather(List<KindAmount> Into, KingdomRules.BuildEntry Entry, GameObject Work)
		{
			// A malformed Carries is already reported by the catalogue validator, and whatever
			// parsed before the bad pair still counts, so the verdict is deliberately unread.
			List<KindAmount> carries;
			KingdomCatalogueRules.TryParseTally(Entry.Carries, out carries, out _);
			// Crewed or not, a work shades its ground by what it is actually managing (Addendum
			// 10(b)). KingdomWear no longer folds condition back into KingdomEffectiveness - that
			// property is the staffing pass's crew stretch and nothing else - so this asks for the
			// combined figure directly, the way KingdomSubsidence and KingdomPower do.
			int percent = KingdomWear.EffectivenessOf(Work);
			for (int i = 0; i < carries.Count; i++)
			{
				if (!KingdomReachRules.ScopedByReach(carries[i].Kind))
				{
					continue;
				}
				int amount = KingdomReachRules.Scaled(carries[i].Amount, percent);
				if (amount > 0)
				{
					Into.Add(new KindAmount(carries[i].Kind, amount));
				}
			}
		}
	}
}
