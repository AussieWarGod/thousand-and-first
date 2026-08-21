using System;
using System.Collections.Generic;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The city book as the save file holds it: one settlement's whole model, written as columns.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;1.3 puts the model on <c>KingdomSettlement</c> as a named-field
	/// composite, and the frozen-model doctrine says why this type exists at all instead of the
	/// carrier being <see cref="KingdomCityState"/> itself: <b>a named-field reader must assign
	/// fields, and the rules layer must not.</b> So the rules layer keeps <see cref="KingdomCityState"/>
	/// sealed, frozen and total; this holds the same rows in mutable columns the engine can fill,
	/// and the two meet at exactly two methods — <see cref="TryRead"/> and <see cref="TryPublish"/>.
	/// </para>
	/// <para>
	/// <b>Columns, not a list of row objects.</b> &sect;0.0(c) budgets the model with no per-row
	/// object header, and a <c>List</c> of row composites would put one on every row and hold them
	/// for the life of the game. Flat primitive columns carry the same fields at the same widths,
	/// and <c>List&lt;int&gt;</c> / <c>List&lt;long&gt;</c> / <c>List&lt;string&gt;</c> are exactly
	/// what this mod already writes through named fields elsewhere.
	/// </para>
	/// <para>
	/// <b>One publisher.</b> Every column is rewritten in one call from one frozen snapshot, after
	/// the rules have succeeded. Nothing here is ever partially incremented, so a fault leaves the
	/// settlement byte-identical &mdash; the same contract <c>FixedPeriodToyState</c> keeps.
	/// </para>
	/// </summary>
	[Serializable]
	public class KingdomCityBook
#if !TAF_TESTS
		: IComposite
#endif
	{
		public int SchemaVersion = KingdomCityRules.SchemaVersion;

		public int RulesVersion = KingdomCityRules.RulesVersion;

		/// <summary>Which city this book is. The settlement's own name at founding; never read for
		/// display, only for telling two books apart in a log line.</summary>
		public string SettlementId = "";

		/// <summary>How far the model has been advanced. LIVING-CITY-ARCHITECTURE &sect;2.2.</summary>
		public long ProcessedThroughTick;

		public long WaterLevel;

		public long WaterCapacity;

		public long FoodLevel;

		public long FoodCapacity;

		public long MaterialsLevel;

		public long MaterialsCapacity;

		// ---- Zone rows -----------------------------------------------------------------------

		public List<string> ZoneIds = new List<string>();

		public List<int> ZoneDistrictCodes = new List<int>();

		public List<long> ZoneLastReadTicks = new List<long>();

		public List<long> ZoneWaterLevels = new List<long>();

		public List<long> ZoneWaterCapacities = new List<long>();

		public List<long> ZoneFoodLevels = new List<long>();

		public List<long> ZoneFoodCapacities = new List<long>();

		public List<long> ZoneMaterialsLevels = new List<long>();

		public List<long> ZoneMaterialsCapacities = new List<long>();

		public List<int> ZoneRoofs = new List<int>();

		public List<int> ZoneDefences = new List<int>();

		public List<int> ZoneWaterCarries = new List<int>();

		public List<int> ZoneFoodCarries = new List<int>();

		public List<int> ZoneOwedWater = new List<int>();

		public List<int> ZoneOwedFood = new List<int>();

		public List<int> ZoneOwedMaterials = new List<int>();

		// ---- Work rows -----------------------------------------------------------------------

		public List<int> WorkIds = new List<int>();

		public List<string> WorkZoneIds = new List<string>();

		public List<int> WorkAnchorsX = new List<int>();

		public List<int> WorkAnchorsY = new List<int>();

		public List<string> WorkDesignKeys = new List<string>();

		public List<int> WorkConditions = new List<int>();

		public List<int> WorkCrews = new List<int>();

		public List<long> WorkRanThroughTicks = new List<long>();

		public List<int> WorkKinds = new List<int>();

		public List<int> WorkStages = new List<int>();

		public List<int> WorkProgress = new List<int>();

		public List<long> WorkNextTicks = new List<long>();

		// ---- Resident rows -------------------------------------------------------------------

		public List<int> ResidentIds = new List<int>();

		public List<string> ResidentNames = new List<string>();

		public List<int> ResidentOriginCodes = new List<int>();

		public List<int> ResidentCreedCodes = new List<int>();

		public List<long> ResidentArrivedTicks = new List<long>();

		public List<int> ResidentHomeWorkIds = new List<int>();

		public List<int> ResidentJobWorkIds = new List<int>();

		public List<int> ResidentJobRoles = new List<int>();

		public List<int> ResidentDayShapes = new List<int>();

		public List<int> ResidentStandings = new List<int>();

		/// <summary>Why a row left <c>Resident</c>. LIVING-CITY-ARCHITECTURE &sect;8.3: a body the
		/// player killed reads back as Dead <b>with a cause</b>, and a cause nobody wrote down is
		/// the half of that sentence that would have gone missing.</summary>
		public List<int> ResidentCauses = new List<int>();

		public List<string> ResidentBoundZoneIds = new List<string>();

		/// <summary>One when a roof brink stands over this settler at all. Kept apart from the
		/// warned tick so that "recorded, and the word has not gone out yet" and "no brink" are
		/// different states rather than the same zero &mdash; <c>KingdomBrink</c>'s own rule, and
		/// the reason the property it replaced existed.</summary>
		public List<int> ResidentRoofStanding = new List<int>();

		public List<long> ResidentRoofTicks = new List<long>();

		/// <summary>The tick the founder was warned, and the anchor the whole window runs from.
		/// A <c>long</c>, not a flag: <c>KingdomBrinkRules.WindowSpent</c> counts world-days from
		/// this number.</summary>
		public List<long> ResidentRoofWarnedTicks = new List<long>();

		public List<int> ResidentCreedStanding = new List<int>();

		public List<long> ResidentCreedTicks = new List<long>();

		public List<long> ResidentCreedWarnedTicks = new List<long>();

		/// <summary>The creed a brink pulls toward, by faction name. A name and not a code: creeds
		/// are open-ended faction names, and the conversion that fires at the end of the window
		/// needs the one it was recorded with.</summary>
		public List<string> ResidentCreedToward = new List<string>();

		public List<int> ResidentCreedChannels = new List<int>();

		// ---- Clocks --------------------------------------------------------------------------

		public List<int> ClockKinds = new List<int>();

		public List<long> ClockNextDueTicks = new List<long>();

		public List<int> ClockOrdinals = new List<int>();

		// ---- The told-log ring, written oldest first ------------------------------------------

		public List<int> ToldKinds = new List<int>();

		public List<long> ToldTicks = new List<long>();

		public List<int> ToldSubjectsA = new List<int>();

		public List<int> ToldSubjectsB = new List<int>();

		public List<string> ToldPlaceZoneIds = new List<string>();

		public List<int> ToldOutcomes = new List<int>();

#if !TAF_TESTS
		public bool WantFieldReflection => false;

		public void Write(SerializationWriter Writer)
		{
			Writer.WriteNamedFields(this, typeof(KingdomCityBook));
		}

		public void Read(SerializationReader Reader)
		{
			Reader.ReadNamedFields(this, typeof(KingdomCityBook));
			Normalize();
		}
#endif

		/// <summary>How many zone rows the book holds after normalization.</summary>
		public int ZoneCount => ZoneIds.Count;

		public int WorkCount => WorkIds.Count;

		public int ResidentCount => ResidentIds.Count;

		public int ToldCount => ToldKinds.Count;

		/// <summary>
		/// Repairs a book read from a save written by an older build, or handed in by a caller.
		/// <para>
		/// Two failures matter here and nothing else does. A null column is an absent named field
		/// and becomes an empty one. <b>Ragged columns are truncated to the shortest</b>, because a
		/// row half of whose fields are missing is not a row — a reader that trusted the longest
		/// column would invent a zone out of a default id, and nothing is invented for ground the
		/// game has never looked at. Everything past a cap is dropped for the same reason
		/// &sect;1.4 states the caps at all: no dimension of this model grows.
		/// </para>
		/// </summary>
		public void Normalize()
		{
			ZoneIds = Repair(ZoneIds);
			ZoneDistrictCodes = Repair(ZoneDistrictCodes);
			ZoneLastReadTicks = Repair(ZoneLastReadTicks);
			ZoneWaterLevels = Repair(ZoneWaterLevels);
			ZoneWaterCapacities = Repair(ZoneWaterCapacities);
			ZoneFoodLevels = Repair(ZoneFoodLevels);
			ZoneFoodCapacities = Repair(ZoneFoodCapacities);
			ZoneMaterialsLevels = Repair(ZoneMaterialsLevels);
			ZoneMaterialsCapacities = Repair(ZoneMaterialsCapacities);
			ZoneRoofs = Repair(ZoneRoofs);
			ZoneDefences = Repair(ZoneDefences);
			ZoneWaterCarries = Repair(ZoneWaterCarries);
			ZoneFoodCarries = Repair(ZoneFoodCarries);
			ZoneOwedWater = Repair(ZoneOwedWater);
			ZoneOwedFood = Repair(ZoneOwedFood);
			ZoneOwedMaterials = Repair(ZoneOwedMaterials);
			int zones = Shortest(new int[16]
			{
				ZoneIds.Count, ZoneDistrictCodes.Count, ZoneLastReadTicks.Count,
				ZoneWaterLevels.Count, ZoneWaterCapacities.Count, ZoneFoodLevels.Count,
				ZoneFoodCapacities.Count, ZoneMaterialsLevels.Count, ZoneMaterialsCapacities.Count,
				ZoneRoofs.Count, ZoneDefences.Count, ZoneWaterCarries.Count, ZoneFoodCarries.Count,
				ZoneOwedWater.Count, ZoneOwedFood.Count, ZoneOwedMaterials.Count
			});
			if (zones > KingdomCityState.MaxZones)
			{
				zones = KingdomCityState.MaxZones;
			}
			Trim(ZoneIds, zones);
			Trim(ZoneDistrictCodes, zones);
			Trim(ZoneLastReadTicks, zones);
			Trim(ZoneWaterLevels, zones);
			Trim(ZoneWaterCapacities, zones);
			Trim(ZoneFoodLevels, zones);
			Trim(ZoneFoodCapacities, zones);
			Trim(ZoneMaterialsLevels, zones);
			Trim(ZoneMaterialsCapacities, zones);
			Trim(ZoneRoofs, zones);
			Trim(ZoneDefences, zones);
			Trim(ZoneWaterCarries, zones);
			Trim(ZoneFoodCarries, zones);
			Trim(ZoneOwedWater, zones);
			Trim(ZoneOwedFood, zones);
			Trim(ZoneOwedMaterials, zones);

			WorkIds = Repair(WorkIds);
			WorkZoneIds = Repair(WorkZoneIds);
			WorkAnchorsX = Repair(WorkAnchorsX);
			WorkAnchorsY = Repair(WorkAnchorsY);
			WorkDesignKeys = Repair(WorkDesignKeys);
			WorkConditions = Repair(WorkConditions);
			WorkCrews = Repair(WorkCrews);
			WorkRanThroughTicks = Repair(WorkRanThroughTicks);
			WorkKinds = Repair(WorkKinds);
			WorkStages = Repair(WorkStages);
			WorkProgress = Repair(WorkProgress);
			WorkNextTicks = Repair(WorkNextTicks);
			int works = Shortest(new int[12]
			{
				WorkIds.Count, WorkZoneIds.Count, WorkAnchorsX.Count, WorkAnchorsY.Count,
				WorkDesignKeys.Count, WorkConditions.Count, WorkCrews.Count, WorkRanThroughTicks.Count,
				WorkKinds.Count, WorkStages.Count, WorkProgress.Count, WorkNextTicks.Count
			});
			if (works > KingdomCityState.MaxWorks)
			{
				works = KingdomCityState.MaxWorks;
			}
			Trim(WorkIds, works);
			Trim(WorkZoneIds, works);
			Trim(WorkAnchorsX, works);
			Trim(WorkAnchorsY, works);
			Trim(WorkDesignKeys, works);
			Trim(WorkConditions, works);
			Trim(WorkCrews, works);
			Trim(WorkRanThroughTicks, works);
			Trim(WorkKinds, works);
			Trim(WorkStages, works);
			Trim(WorkProgress, works);
			Trim(WorkNextTicks, works);

			ResidentIds = Repair(ResidentIds);
			ResidentNames = Repair(ResidentNames);
			ResidentOriginCodes = Repair(ResidentOriginCodes);
			ResidentCreedCodes = Repair(ResidentCreedCodes);
			ResidentArrivedTicks = Repair(ResidentArrivedTicks);
			ResidentHomeWorkIds = Repair(ResidentHomeWorkIds);
			ResidentJobWorkIds = Repair(ResidentJobWorkIds);
			ResidentJobRoles = Repair(ResidentJobRoles);
			ResidentDayShapes = Repair(ResidentDayShapes);
			ResidentStandings = Repair(ResidentStandings);
			ResidentCauses = Repair(ResidentCauses);
			ResidentBoundZoneIds = Repair(ResidentBoundZoneIds);
			ResidentRoofStanding = Repair(ResidentRoofStanding);
			ResidentRoofTicks = Repair(ResidentRoofTicks);
			ResidentRoofWarnedTicks = Repair(ResidentRoofWarnedTicks);
			ResidentCreedStanding = Repair(ResidentCreedStanding);
			ResidentCreedTicks = Repair(ResidentCreedTicks);
			ResidentCreedWarnedTicks = Repair(ResidentCreedWarnedTicks);
			ResidentCreedToward = Repair(ResidentCreedToward);
			ResidentCreedChannels = Repair(ResidentCreedChannels);
			int residents = Shortest(new int[20]
			{
				ResidentIds.Count, ResidentNames.Count, ResidentOriginCodes.Count,
				ResidentCreedCodes.Count, ResidentArrivedTicks.Count, ResidentHomeWorkIds.Count,
				ResidentJobWorkIds.Count, ResidentJobRoles.Count, ResidentDayShapes.Count,
				ResidentStandings.Count, ResidentCauses.Count, ResidentBoundZoneIds.Count,
				ResidentRoofStanding.Count, ResidentRoofTicks.Count, ResidentRoofWarnedTicks.Count,
				ResidentCreedStanding.Count, ResidentCreedTicks.Count, ResidentCreedWarnedTicks.Count,
				ResidentCreedToward.Count, ResidentCreedChannels.Count
			});
			if (residents > KingdomCityState.MaxResidents)
			{
				residents = KingdomCityState.MaxResidents;
			}
			Trim(ResidentIds, residents);
			Trim(ResidentNames, residents);
			Trim(ResidentOriginCodes, residents);
			Trim(ResidentCreedCodes, residents);
			Trim(ResidentArrivedTicks, residents);
			Trim(ResidentHomeWorkIds, residents);
			Trim(ResidentJobWorkIds, residents);
			Trim(ResidentJobRoles, residents);
			Trim(ResidentDayShapes, residents);
			Trim(ResidentStandings, residents);
			Trim(ResidentCauses, residents);
			Trim(ResidentBoundZoneIds, residents);
			Trim(ResidentRoofStanding, residents);
			Trim(ResidentRoofTicks, residents);
			Trim(ResidentRoofWarnedTicks, residents);
			Trim(ResidentCreedStanding, residents);
			Trim(ResidentCreedTicks, residents);
			Trim(ResidentCreedWarnedTicks, residents);
			Trim(ResidentCreedToward, residents);
			Trim(ResidentCreedChannels, residents);
			for (int i = 0; i < residents; i++)
			{
				if (ResidentNames[i] == null)
				{
					ResidentNames[i] = "";
				}
				if (ResidentBoundZoneIds[i] == null)
				{
					ResidentBoundZoneIds[i] = "";
				}
				// A row whose standing and cause disagree is repaired toward the STANDING, because
				// the standing is what every consumer branches on and a mismatched cause would let
				// a living settler carry a death clause into a memorial.
				if (!KingdomResidentRules.CauseFits((KingdomResidentStanding)ResidentStandings[i], (KingdomStandingCause)ResidentCauses[i]))
				{
					ResidentCauses[i] = (int)DefaultCauseFor((KingdomResidentStanding)ResidentStandings[i]);
				}
			}

			ClockKinds = Repair(ClockKinds);
			ClockNextDueTicks = Repair(ClockNextDueTicks);
			ClockOrdinals = Repair(ClockOrdinals);
			int clocks = Shortest(new int[3] { ClockKinds.Count, ClockNextDueTicks.Count, ClockOrdinals.Count });
			if (clocks > KingdomCityState.MaxClocks)
			{
				clocks = KingdomCityState.MaxClocks;
			}
			Trim(ClockKinds, clocks);
			Trim(ClockNextDueTicks, clocks);
			Trim(ClockOrdinals, clocks);

			ToldKinds = Repair(ToldKinds);
			ToldTicks = Repair(ToldTicks);
			ToldSubjectsA = Repair(ToldSubjectsA);
			ToldSubjectsB = Repair(ToldSubjectsB);
			ToldPlaceZoneIds = Repair(ToldPlaceZoneIds);
			ToldOutcomes = Repair(ToldOutcomes);
			int told = Shortest(new int[6]
			{
				ToldKinds.Count, ToldTicks.Count, ToldSubjectsA.Count, ToldSubjectsB.Count,
				ToldPlaceZoneIds.Count, ToldOutcomes.Count
			});
			if (told > KingdomCityState.MaxToldEntries)
			{
				// The ring forgets its OLDEST lines, never its newest: a book that came back with
				// more than the ring holds keeps the end of the story.
				DropOldest(ToldKinds, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldTicks, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldSubjectsA, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldSubjectsB, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldPlaceZoneIds, told - KingdomCityState.MaxToldEntries);
				DropOldest(ToldOutcomes, told - KingdomCityState.MaxToldEntries);
				told = KingdomCityState.MaxToldEntries;
			}
			Trim(ToldKinds, told);
			Trim(ToldTicks, told);
			Trim(ToldSubjectsA, told);
			Trim(ToldSubjectsB, told);
			Trim(ToldPlaceZoneIds, told);
			Trim(ToldOutcomes, told);

			if (SettlementId == null)
			{
				SettlementId = "";
			}
			// A stamp below zero is a corrupt reading and not a model in debt: the book fails
			// closed to "nothing reckoned yet" rather than refusing to load a whole city.
			if (ProcessedThroughTick < 0L)
			{
				ProcessedThroughTick = 0L;
			}
			for (int i = 0; i < ZoneIds.Count; i++)
			{
				if (ZoneIds[i] == null)
				{
					ZoneIds[i] = "";
				}
				if (ZoneLastReadTicks[i] < 0L)
				{
					ZoneLastReadTicks[i] = 0L;
				}
			}
		}

		/// <summary>Repairs the resident columns only if they are ragged. Square columns are the
		/// ordinary case and cost one length comparison per column to confirm.</summary>
		private void EnsureResidentColumnsSquare()
		{
			// A null column is an absent named field, which is ragged in the strongest sense; Rows
			// answers -1 for one so the comparison below can never be true.
			int count = Rows(ResidentIds);
			if (count >= 0
				&& Rows(ResidentNames) == count && Rows(ResidentOriginCodes) == count && Rows(ResidentCreedCodes) == count
				&& Rows(ResidentArrivedTicks) == count && Rows(ResidentHomeWorkIds) == count
				&& Rows(ResidentJobWorkIds) == count && Rows(ResidentJobRoles) == count
				&& Rows(ResidentDayShapes) == count && Rows(ResidentStandings) == count
				&& Rows(ResidentCauses) == count && Rows(ResidentBoundZoneIds) == count
				&& Rows(ResidentRoofStanding) == count && Rows(ResidentRoofTicks) == count
				&& Rows(ResidentRoofWarnedTicks) == count && Rows(ResidentCreedStanding) == count
				&& Rows(ResidentCreedTicks) == count && Rows(ResidentCreedWarnedTicks) == count
				&& Rows(ResidentCreedToward) == count && Rows(ResidentCreedChannels) == count)
			{
				return;
			}
			Normalize();
		}

		private static int Rows<T>(List<T> column)
		{
			return (column == null) ? -1 : column.Count;
		}

		/// <summary>
		/// The cause a standing carries when the stored one did not fit it. <c>Resident</c> carries
		/// none; a <c>Dead</c> or <c>Abroad</c> row falls back to the honestly-unknown member of its
		/// own family rather than to a story nobody witnessed.
		/// </summary>
		private static KingdomStandingCause DefaultCauseFor(KingdomResidentStanding standing)
		{
			switch (standing)
			{
			case KingdomResidentStanding.Dead:
				return KingdomStandingCause.Unwitnessed;
			case KingdomResidentStanding.Abroad:
				return KingdomStandingCause.Astray;
			default:
				return KingdomStandingCause.None;
			}
		}

		/// <summary>
		/// The resident row for this id, or false. The lookup every reader that starts from a
		/// settler's body goes through &mdash; <c>KingdomBrink</c> above all, whose whole storage
		/// layer is now this index plus a column read.
		/// </summary>
		public bool TryResidentRow(int residentId, out int index)
		{
			index = -1;
			// Zero is not an identity, and a null column is a book nothing has ever been written
			// to: both are "no row here" rather than a reason to fault.
			if (residentId == 0 || ResidentIds == null)
			{
				return false;
			}
			for (int i = 0; i < ResidentIds.Count; i++)
			{
				if (ResidentIds[i] == residentId)
				{
					index = i;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// One settler's brink of one kind, straight off the columns.
		/// <para>
		/// Reads the columns rather than materialising the whole model, exactly as
		/// <c>KingdomCity.OtherZones</c> does and for the same reason: this is called once per
		/// settler per pass by three separate consumers, and a full <see cref="TryRead"/> per call
		/// would allocate a city to answer a question about one person.
		/// </para>
		/// <para>
		/// <see cref="Normalize"/> runs only when the resident columns are NOT square, which is a
		/// state only a save written by another build can produce &mdash; every load path and the
		/// one publisher leave them square. A repair on every read would be O(rows) over thirty
		/// columns, several hundred times a pass, to fix something that is already fixed.
		/// </para>
		/// </summary>
		/// <returns>False when this book holds no row for that id, which is the caller's signal
		/// that the settler belongs to some other city or to none.</returns>
		public bool TryReadBrink(int residentId, BrinkKind kind, out bool stands, out long reachedTick, out long warnedTick, out string toward, out int channel)
		{
			stands = false;
			reachedTick = 0L;
			warnedTick = 0L;
			toward = null;
			channel = 0;
			EnsureResidentColumnsSquare();
			int index;
			if (!TryResidentRow(residentId, out index) || (kind != BrinkKind.Roof && kind != BrinkKind.Creed))
			{
				return false;
			}
			bool creed = kind == BrinkKind.Creed;
			stands = (creed ? ResidentCreedStanding[index] : ResidentRoofStanding[index]) != 0;
			if (!stands)
			{
				return true;
			}
			reachedTick = creed ? ResidentCreedTicks[index] : ResidentRoofTicks[index];
			warnedTick = creed ? ResidentCreedWarnedTicks[index] : ResidentRoofWarnedTicks[index];
			if (creed)
			{
				toward = string.IsNullOrEmpty(ResidentCreedToward[index]) ? null : ResidentCreedToward[index];
				channel = ResidentCreedChannels[index];
			}
			return true;
		}

		/// <summary>
		/// Writes one settler's brink of one kind back into the columns.
		/// <para>
		/// A single-row write and not a republish, because that is what this actually is: the brink
		/// consumers change one person's window and nothing else, and rebuilding the whole book
		/// around each of those would make a fault in an unrelated row able to swallow a warning.
		/// A lifted brink clears its own fields, so a forgotten brink leaves nothing behind for a
		/// later read to half-believe.
		/// </para>
		/// </summary>
		public bool TryWriteBrink(int residentId, BrinkKind kind, bool stands, long reachedTick, long warnedTick, string toward, int channel)
		{
			EnsureResidentColumnsSquare();
			int index;
			if (!TryResidentRow(residentId, out index) || (kind != BrinkKind.Roof && kind != BrinkKind.Creed))
			{
				return false;
			}
			long reached = stands ? ((reachedTick > 0L) ? reachedTick : 0L) : 0L;
			long warned = stands ? ((warnedTick > 0L) ? warnedTick : 0L) : 0L;
			if (kind == BrinkKind.Creed)
			{
				ResidentCreedStanding[index] = stands ? 1 : 0;
				ResidentCreedTicks[index] = reached;
				ResidentCreedWarnedTicks[index] = warned;
				ResidentCreedToward[index] = (stands && !string.IsNullOrEmpty(toward)) ? toward : "";
				ResidentCreedChannels[index] = stands ? channel : 0;
				return true;
			}
			ResidentRoofStanding[index] = stands ? 1 : 0;
			ResidentRoofTicks[index] = reached;
			ResidentRoofWarnedTicks[index] = warned;
			return true;
		}

		/// <summary>The zone row for this id, or false. The one lookup every re-plumbed sighting
		/// reader goes through.</summary>
		public bool TryZoneRow(string zoneId, out int index)
		{
			index = -1;
			if (zoneId == null)
			{
				return false;
			}
			for (int i = 0; i < ZoneIds.Count; i++)
			{
				if (string.Equals(ZoneIds[i], zoneId, StringComparison.Ordinal))
				{
					index = i;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The book as the frozen model the rules layer works on. Refuses and publishes nothing
		/// rather than handing back a half-built city.
		/// </summary>
		internal bool TryRead(out KingdomCityState state, out KingdomCityFault fault)
		{
			state = null;
			Normalize();
			KingdomZoneRow[] zones = new KingdomZoneRow[ZoneIds.Count];
			for (int i = 0; i < zones.Length; i++)
			{
				zones[i] = new KingdomZoneRow(
					ZoneIds[i],
					ZoneDistrictCodes[i],
					ZoneLastReadTicks[i],
					new KingdomStocks(
						new KingdomStockPair(ZoneWaterLevels[i], ZoneWaterCapacities[i]),
						new KingdomStockPair(ZoneFoodLevels[i], ZoneFoodCapacities[i]),
						new KingdomStockPair(ZoneMaterialsLevels[i], ZoneMaterialsCapacities[i])),
					ZoneRoofs[i],
					ZoneDefences[i],
					ZoneWaterCarries[i],
					ZoneFoodCarries[i],
					ZoneOwedWater[i],
					ZoneOwedFood[i],
					ZoneOwedMaterials[i]);
			}
			KingdomWorkRow[] works = new KingdomWorkRow[WorkIds.Count];
			for (int i = 0; i < works.Length; i++)
			{
				works[i] = new KingdomWorkRow(
					WorkIds[i],
					WorkZoneIds[i],
					(short)WorkAnchorsX[i],
					(short)WorkAnchorsY[i],
					WorkDesignKeys[i],
					WorkConditions[i],
					WorkCrews[i],
					WorkRanThroughTicks[i],
					new KingdomWorkRunState((KingdomWorkKind)WorkKinds[i], (byte)WorkStages[i], WorkProgress[i], WorkNextTicks[i]));
			}
			KingdomResidentRow[] residents = new KingdomResidentRow[ResidentIds.Count];
			for (int i = 0; i < residents.Length; i++)
			{
				residents[i] = new KingdomResidentRow(
					ResidentIds[i],
					ResidentNames[i],
					ResidentOriginCodes[i],
					ResidentCreedCodes[i],
					ResidentArrivedTicks[i],
					ResidentHomeWorkIds[i],
					ResidentJobWorkIds[i],
					(byte)ResidentJobRoles[i],
					(KingdomDayShape)ResidentDayShapes[i],
					(KingdomResidentStanding)ResidentStandings[i],
					(KingdomStandingCause)ResidentCauses[i],
					ResidentBoundZoneIds[i],
					new KingdomBrinkWindow(ResidentRoofStanding[i] != 0, ResidentRoofTicks[i], ResidentRoofWarnedTicks[i]),
					new KingdomBrinkWindow(ResidentCreedStanding[i] != 0, ResidentCreedTicks[i], ResidentCreedWarnedTicks[i]),
					ResidentCreedToward[i],
					(byte)ResidentCreedChannels[i]);
			}
			KingdomClockRow[] clocks = new KingdomClockRow[ClockKinds.Count];
			for (int i = 0; i < clocks.Length; i++)
			{
				clocks[i] = new KingdomClockRow((KingdomClockKind)ClockKinds[i], ClockNextDueTicks[i], ClockOrdinals[i]);
			}
			KingdomCityState built;
			if (!KingdomCityState.TryCreate(
				SchemaVersion,
				RulesVersion,
				SettlementId,
				ProcessedThroughTick,
				new KingdomStocks(
					new KingdomStockPair(WaterLevel, WaterCapacity),
					new KingdomStockPair(FoodLevel, FoodCapacity),
					new KingdomStockPair(MaterialsLevel, MaterialsCapacity)),
				zones,
				works,
				residents,
				clocks,
				out built,
				out fault))
			{
				return false;
			}
			for (int i = 0; i < ToldKinds.Count; i++)
			{
				KingdomCityState told;
				if (!built.TryTell(
					new KingdomToldRow((KingdomToldKind)ToldKinds[i], ToldTicks[i], ToldSubjectsA[i], ToldSubjectsB[i], ToldPlaceZoneIds[i], ToldOutcomes[i]),
					out told,
					out fault))
				{
					return false;
				}
				built = told;
			}
			state = built;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Writes one frozen snapshot into the columns, in one call and after the rules have
		/// succeeded. The single publisher &sect;1.3 requires.
		/// </summary>
		internal bool TryPublish(KingdomCityState state, out KingdomCityFault fault)
		{
			if (state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			SchemaVersion = state.SchemaVersion;
			RulesVersion = state.RulesVersion;
			SettlementId = state.SettlementId ?? "";
			ProcessedThroughTick = state.ProcessedThroughTick;
			WaterLevel = state.Stocks.Water.Level;
			WaterCapacity = state.Stocks.Water.Capacity;
			FoodLevel = state.Stocks.Food.Level;
			FoodCapacity = state.Stocks.Food.Capacity;
			MaterialsLevel = state.Stocks.Materials.Level;
			MaterialsCapacity = state.Stocks.Materials.Capacity;

			Clear();
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				ZoneIds.Add(row.ZoneId ?? "");
				ZoneDistrictCodes.Add(row.DistrictCode);
				ZoneLastReadTicks.Add(row.LastReadTick);
				ZoneWaterLevels.Add(row.Stocks.Water.Level);
				ZoneWaterCapacities.Add(row.Stocks.Water.Capacity);
				ZoneFoodLevels.Add(row.Stocks.Food.Level);
				ZoneFoodCapacities.Add(row.Stocks.Food.Capacity);
				ZoneMaterialsLevels.Add(row.Stocks.Materials.Level);
				ZoneMaterialsCapacities.Add(row.Stocks.Materials.Capacity);
				ZoneRoofs.Add(row.Roofs);
				ZoneDefences.Add(row.Defence);
				ZoneWaterCarries.Add(row.WaterCarry);
				ZoneFoodCarries.Add(row.FoodCarry);
				ZoneOwedWater.Add(row.OwedWater);
				ZoneOwedFood.Add(row.OwedFood);
				ZoneOwedMaterials.Add(row.OwedMaterials);
			}
			for (int i = 0; i < state.WorkCount; i++)
			{
				KingdomWorkRow row;
				if (!state.TryWork(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				WorkIds.Add(row.WorkId);
				WorkZoneIds.Add(row.ZoneId ?? "");
				WorkAnchorsX.Add(row.AnchorX);
				WorkAnchorsY.Add(row.AnchorY);
				WorkDesignKeys.Add(row.DesignKey ?? "");
				WorkConditions.Add(row.ConditionPercent);
				WorkCrews.Add(row.CrewAssigned);
				WorkRanThroughTicks.Add(row.RanThroughTick);
				WorkKinds.Add((int)row.RunState.Kind);
				WorkStages.Add(row.RunState.Stage);
				WorkProgress.Add(row.RunState.Progress);
				WorkNextTicks.Add(row.RunState.NextTick);
			}
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				ResidentIds.Add(row.ResidentId);
				ResidentNames.Add(row.Name ?? "");
				ResidentOriginCodes.Add(row.OriginCode);
				ResidentCreedCodes.Add(row.CreedCode);
				ResidentArrivedTicks.Add(row.ArrivedTick);
				ResidentHomeWorkIds.Add(row.HomeWorkId);
				ResidentJobWorkIds.Add(row.JobWorkId);
				ResidentJobRoles.Add(row.JobRole);
				ResidentDayShapes.Add((int)row.DayShape);
				ResidentStandings.Add((int)row.Standing);
				ResidentCauses.Add((int)row.Cause);
				ResidentBoundZoneIds.Add(row.BoundZoneId ?? "");
				ResidentRoofStanding.Add(row.RoofBrink.Stands ? 1 : 0);
				ResidentRoofTicks.Add(row.RoofBrink.ReachedTick);
				ResidentRoofWarnedTicks.Add(row.RoofBrink.WarnedTick);
				ResidentCreedStanding.Add(row.CreedBrink.Stands ? 1 : 0);
				ResidentCreedTicks.Add(row.CreedBrink.ReachedTick);
				ResidentCreedWarnedTicks.Add(row.CreedBrink.WarnedTick);
				ResidentCreedToward.Add(row.CreedToward ?? "");
				ResidentCreedChannels.Add(row.CreedChannel);
			}
			for (int i = 0; i < state.ClockCount; i++)
			{
				KingdomClockRow row;
				if (!state.TryClock(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				ClockKinds.Add((int)row.Kind);
				ClockNextDueTicks.Add(row.NextDueTick);
				ClockOrdinals.Add(row.Ordinal);
			}
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow row;
				if (!state.TryTold(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				ToldKinds.Add((int)row.Kind);
				ToldTicks.Add(row.Tick);
				ToldSubjectsA.Add(row.SubjectA);
				ToldSubjectsB.Add(row.SubjectB);
				ToldPlaceZoneIds.Add(row.PlaceZoneId ?? "");
				ToldOutcomes.Add(row.Outcome);
			}
			fault = KingdomCityFault.None;
			return true;
		}

		private void Clear()
		{
			ZoneIds.Clear();
			ZoneDistrictCodes.Clear();
			ZoneLastReadTicks.Clear();
			ZoneWaterLevels.Clear();
			ZoneWaterCapacities.Clear();
			ZoneFoodLevels.Clear();
			ZoneFoodCapacities.Clear();
			ZoneMaterialsLevels.Clear();
			ZoneMaterialsCapacities.Clear();
			ZoneRoofs.Clear();
			ZoneDefences.Clear();
			ZoneWaterCarries.Clear();
			ZoneFoodCarries.Clear();
			ZoneOwedWater.Clear();
			ZoneOwedFood.Clear();
			ZoneOwedMaterials.Clear();
			WorkIds.Clear();
			WorkZoneIds.Clear();
			WorkAnchorsX.Clear();
			WorkAnchorsY.Clear();
			WorkDesignKeys.Clear();
			WorkConditions.Clear();
			WorkCrews.Clear();
			WorkRanThroughTicks.Clear();
			WorkKinds.Clear();
			WorkStages.Clear();
			WorkProgress.Clear();
			WorkNextTicks.Clear();
			ResidentIds.Clear();
			ResidentNames.Clear();
			ResidentOriginCodes.Clear();
			ResidentCreedCodes.Clear();
			ResidentArrivedTicks.Clear();
			ResidentHomeWorkIds.Clear();
			ResidentJobWorkIds.Clear();
			ResidentJobRoles.Clear();
			ResidentDayShapes.Clear();
			ResidentStandings.Clear();
			ResidentCauses.Clear();
			ResidentBoundZoneIds.Clear();
			ResidentRoofStanding.Clear();
			ResidentRoofTicks.Clear();
			ResidentRoofWarnedTicks.Clear();
			ResidentCreedStanding.Clear();
			ResidentCreedTicks.Clear();
			ResidentCreedWarnedTicks.Clear();
			ResidentCreedToward.Clear();
			ResidentCreedChannels.Clear();
			ClockKinds.Clear();
			ClockNextDueTicks.Clear();
			ClockOrdinals.Clear();
			ToldKinds.Clear();
			ToldTicks.Clear();
			ToldSubjectsA.Clear();
			ToldSubjectsB.Clear();
			ToldPlaceZoneIds.Clear();
			ToldOutcomes.Clear();
		}

		private static List<T> Repair<T>(List<T> column)
		{
			return column ?? new List<T>();
		}

		private static int Shortest(int[] counts)
		{
			int shortest = int.MaxValue;
			for (int i = 0; i < counts.Length; i++)
			{
				if (counts[i] < shortest)
				{
					shortest = counts[i];
				}
			}
			return (shortest == int.MaxValue) ? 0 : shortest;
		}

		private static void Trim<T>(List<T> column, int count)
		{
			if (column.Count > count)
			{
				column.RemoveRange(count, column.Count - count);
			}
		}

		private static void DropOldest<T>(List<T> column, int count)
		{
			int drop = (count < column.Count) ? count : column.Count;
			if (drop > 0)
			{
				column.RemoveRange(0, drop);
			}
		}
	}
}
