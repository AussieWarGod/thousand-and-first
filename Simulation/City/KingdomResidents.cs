using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Identity at the engine's edge: who a body is, which book holds their row, and what the
	/// binding registry says about whether they may be minted at all.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;8.3's answer to <i>where a person lives — object or row</i>:
	/// <b>the row is primary and the body is a durable view bound by a stable id.</b> The body
	/// carries <see cref="ResidentIdProperty"/> and nothing else; everything else about the person
	/// that has to survive their zone going to disk lives in a resident row.
	/// </para>
	/// <para>
	/// <b>The id is not a draw.</b> It is the next number off a realm-scope counter, in mint order,
	/// exactly as <c>KingdomCity.DedicationOrderProperty</c> is. Identity is a substrate: a seeded
	/// draw would make who-is-who depend on how many other things had been rolled first, and the
	/// kernel's whole discipline is that draws belong to happenings.
	/// </para>
	/// <para>
	/// Engine-coupled by design and paired with <c>KingdomResidentRules</c> exactly as
	/// <c>KingdomCity</c> is paired with <c>KingdomCityRules</c>: nothing here decides anything, it
	/// reads the ground, asks the rules, and applies the answer.
	/// </para>
	/// </summary>
	public static class KingdomResidents
	{
		/// <summary>
		/// The settler's identity, and the only thing about a person the body itself carries.
		/// Minted once, never re-minted, and never reused: the realm's counter only goes up.
		/// </summary>
		public const string ResidentIdProperty = "KingdomResidentId";

		/// <summary>
		/// The exact job a transient body renders. Production porters and other carriers stamp it
		/// only after durable job publication; the stale-transient sweep reads it before rendering
		/// so a closed model job and a leftover body cannot expose the same cargo twice.
		/// </summary>
		public const string JobIdProperty = "KingdomJobId";

		// ==================================================================================
		// The id
		// ==================================================================================

		/// <summary>This body's resident id, or zero for a body that has never been enrolled.</summary>
		public static int IdOf(GameObject Body)
		{
			return GameObject.Validate(Body) ? Body.GetIntProperty(ResidentIdProperty) : 0;
		}

		/// <summary>
		/// This body's resident id, minting one if it has none. Zero when there is no realm to mint
		/// against — an id from a counter nobody is keeping is not an id.
		/// </summary>
		public static int EnsureId(KingdomSystem System, GameObject Body)
		{
			int existing = IdOf(Body);
			if (existing != 0 || System == null || !GameObject.Validate(Body))
			{
				return existing;
			}
			System.ResidentCounter++;
			Body.SetIntProperty(ResidentIdProperty, System.ResidentCounter);
			return System.ResidentCounter;
		}

		// ==================================================================================
		// The resident-row authority
		// ==================================================================================

		/// <summary>Reads the seated city's bounded living roll. This is the only production bridge
		/// from a realm to resident rows; the three historical parallel lists are projections only.</summary>
		internal static bool TryRoll(KingdomSystem System, out KingdomCityState State,
			out KingdomResidentRollProjection Roll)
		{
			State = null;
			Roll = null;
			KingdomCityFault fault;
			return System != null && System.City != null
				&& System.City.TryRead(out State, out fault)
				&& KingdomResidentRules.TryProject(State, out Roll);
		}

		internal static int OnRollCount(KingdomSystem System)
		{
			KingdomCityState state;
			KingdomResidentRollProjection roll;
			return TryRoll(System, out state, out roll) ? roll.Population : 0;
		}

		internal static int LabourCount(KingdomSystem System)
		{
			KingdomCityState state;
			KingdomResidentRollProjection roll;
			return TryRoll(System, out state, out roll) ? roll.Labour : 0;
		}

		internal static List<KingdomResidentRow> RollRows(KingdomSystem System,
			bool LabourOnly = false)
		{
			List<KingdomResidentRow> rows = new List<KingdomResidentRow>();
			KingdomCityState state;
			KingdomResidentRollProjection ignored;
			if (!TryRoll(System, out state, out ignored)) return rows;
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row)) break;
				if (LabourOnly ? KingdomResidentRules.Labours(row)
					: KingdomResidentRules.OnTheRoll(row)) rows.Add(row);
			}
			return rows;
		}

		internal static bool TryFindByName(KingdomSystem System, string Name,
			out KingdomResidentRow Row)
		{
			Row = default(KingdomResidentRow);
			if (string.IsNullOrEmpty(Name)) return false;
			List<KingdomResidentRow> rows = RollRows(System);
			for (int i = 0; i < rows.Count; i++)
			{
				if (string.Equals(rows[i].Name, Name, StringComparison.Ordinal))
				{
					Row = rows[i];
					return true;
				}
			}
			return false;
		}

		internal static bool TryHead(KingdomSystem System, out KingdomResidentRow Row)
		{
			Row = default(KingdomResidentRow);
			List<KingdomResidentRow> rows = RollRows(System);
			if (rows.Count == 0) return false;
			KingdomResidentRow head = rows[0];
			for (int i = 1; i < rows.Count; i++)
			{
				KingdomResidentRow candidate = rows[i];
				// Unknown legacy arrival (zero) remains senior to dated rows. ResidentId breaks ties
				// without depending on the order a zone happened to be surveyed.
				if (candidate.ArrivedTick < head.ArrivedTick
					|| candidate.ArrivedTick == head.ArrivedTick
					&& candidate.ResidentId < head.ResidentId) head = candidate;
			}
			Row = head;
			return true;
		}

		internal static string HeadName(KingdomSystem System)
		{
			KingdomResidentRow head;
			return TryHead(System, out head) ? head.Name : null;
		}

		/// <summary>One-way compatibility projection after a row publish. Population is a cache of
		/// the on-roll count; the three public lists remain for save ABI/reflection only.</summary>
		// The parallel roster fields are frozen save ABI, not live authority. This adapter is
		// the sole deliberate internal user; keep its obsolete-warning scope narrow and visible.
#pragma warning disable 618
		internal static bool ProjectCompatibility(KingdomSystem System)
		{
			if (System == null) return false;
			bool unresolvedSeat = System.City != null && System.City.ResidentCount == 0
				&& (System.RosterNames?.Count > 0 || System.RosterOrigins?.Count > 0
					|| System.RosterArrived?.Count > 0);
			KingdomResidentRollProjection seatRoll = null;
			bool seat = !unresolvedSeat && ProjectCompatibility(System.City, out seatRoll);
			if (seat)
			{
				System.RosterNames = seatRoll.Names;
				System.RosterOrigins = seatRoll.Origins;
				System.RosterArrived = seatRoll.Arrived;
				System.Population = seatRoll.Population;
				System.WaterCrew = Math.Min(System.WaterCrew, seatRoll.Labour);
				System.AssignedCrew = Math.Min(System.AssignedCrew, seatRoll.Labour);
				System.OriginCounts = Counts(seatRoll.Origins);
			}
			if (System.Away != null)
			{
				bool unresolvedAway = System.Away.City != null && System.Away.City.ResidentCount == 0
					&& (System.Away.RosterNames?.Count > 0 || System.Away.RosterOrigins?.Count > 0
						|| System.Away.RosterArrived?.Count > 0);
				if (!unresolvedAway) ProjectCompatibility(System.Away);
			}
			return seat;
		}

		internal static bool ProjectCompatibility(KingdomSettlement Settlement)
		{
			if (Settlement == null || !ProjectCompatibility(Settlement.City,
				out KingdomResidentRollProjection roll)) return false;
			Settlement.RosterNames = roll.Names;
			Settlement.RosterOrigins = roll.Origins;
			Settlement.RosterArrived = roll.Arrived;
			Settlement.Population = roll.Population;
			Settlement.WaterCrew = Math.Min(Settlement.WaterCrew, roll.Labour);
			Settlement.AssignedCrew = Math.Min(Settlement.AssignedCrew, roll.Labour);
			Settlement.OriginCounts = Counts(roll.Origins);
			return true;
		}

		internal static bool ProjectCompatibility(KingdomCityBook Book,
			out KingdomResidentRollProjection Roll)
		{
			Roll = null;
			KingdomCityState state;
			KingdomCityFault fault;
			return Book != null && Book.TryRead(out state, out fault)
				&& KingdomResidentRules.TryProject(state, out Roll);
		}

		/// <summary>Load boundary. A complete old parallel roll seeds an empty book exactly once as
		/// Abroad claims; a real body later adopts the claim's id. Existing rows always win and are
		/// projected outward. Ragged evidence is retained and logged.</summary>
		internal static void AdoptLegacyAuthority(KingdomSystem System)
		{
			if (System == null) return;
			int counter = Math.Max(0, System.ResidentCounter);
			counter = Math.Max(counter, MaxResidentId(System.City));
			counter = Math.Max(counter, MaxResidentId(System.Away?.City));
			System.ResidentCounter = counter;
			Adopt(System.City, System.RosterNames, System.RosterOrigins, System.RosterArrived,
				ref System.ResidentCounter, "seat");
			if (System.Away != null)
			{
				Adopt(System.Away.City, System.Away.RosterNames, System.Away.RosterOrigins,
					System.Away.RosterArrived, ref System.ResidentCounter, "away");
			}
			ProjectCompatibility(System);
		}
#pragma warning restore 618

		private static void Adopt(KingdomCityBook Book, List<string> Names,
			List<string> Origins, List<string> Arrived, ref int Counter, string Label)
		{
			KingdomCityState state;
			KingdomCityState next;
			KingdomCityFault fault;
			int nextCounter;
			if (Book == null || !Book.TryRead(out state, out fault)) return;
			if (!KingdomResidentRules.TryAdoptLegacy(state, Names, Origins, Arrived, Counter,
				out next, out nextCounter, out fault))
			{
				KingdomLog.Log("resident: " + Label + " legacy roll retained unresolved (" + fault + ")");
				return;
			}
			if (!ReferenceEquals(next, state) && !Book.TryPublish(next, out fault))
			{
				KingdomLog.Log("resident: " + Label + " legacy adoption refused (" + fault + ")");
				return;
			}
			Counter = nextCounter;
		}

		private static int MaxResidentId(KingdomCityBook Book)
		{
			if (Book == null) return 0;
			Book.Normalize();
			int max = 0;
			for (int i = 0; i < Book.ResidentIds.Count; i++)
				if (Book.ResidentIds[i] > max) max = Book.ResidentIds[i];
			return max;
		}

		private static Dictionary<string, int> Counts(List<string> Values)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			for (int i = 0; Values != null && i < Values.Count; i++)
			{
				string value = Values[i] ?? "";
				counts.TryGetValue(value, out int count);
				counts[value] = count + 1;
			}
			return counts;
		}

		// ==================================================================================
		// The registry
		// ==================================================================================

		/// <summary>
		/// Check-before-mint (&sect;3.8), asked at the edge and answered by the rules.
		/// <para>
		/// The presence is resolved here because only the engine knows whether an object id still
		/// names something live and where; the VERDICT is <c>KingdomBindingRules</c>'s, because a
		/// second opinion about duplication is how a settler ends up in two places.
		/// </para>
		/// <para>
		/// <b>W2 ships the verdict. W3 obeys it.</b> Nothing in this wave mints or moves a body, so
		/// this has exactly one production caller today — the roster read below, which only ever
		/// asks about bodies that are already standing in front of it.
		/// </para>
		/// </summary>
		public static KingdomBindingVerdict Judge(KingdomSystem System, int bindingKey, KingdomBindingKind kind, string zoneId)
		{
			KingdomBindingTable table;
			if (!TryTable(System, out table))
			{
				// No registry is not a miss. A miss is a licence to mint, and a realm whose registry
				// could not be read has no way of knowing what it would be minting a second copy of.
				return KingdomBindingVerdict.Refuse;
			}
			KingdomBinding binding;
			if (!table.TryGet(bindingKey, kind, out binding))
			{
				return KingdomBindingRules.Judge(kind, KingdomBodyPresence.None);
			}
			return KingdomBindingRules.Judge(kind, PresenceOf(binding, zoneId));
		}

		/// <summary>
		/// What the ground says about one bound key, unnarrowed by a verdict.
		/// <para>
		/// <see cref="Judge"/> collapses "live in another resident zone" and "on disk" to the same
		/// refusal, which is exactly right when the question is <i>may I mint</i> and exactly wrong
		/// when it is <i>has the model outlived this body</i>. LIVING-CITY-ARCHITECTURE &sect;3.8's
		/// t2 closes a job whose carrier is frozen; it must never close one whose carrier is still
		/// walking somewhere the founder could go and watch.
		/// </para>
		/// </summary>
		public static KingdomBodyPresence PresenceOfKey(KingdomSystem System, int bindingKey, KingdomBindingKind kind, string zoneId)
		{
			KingdomBindingTable table;
			KingdomBinding binding;
			if (!TryTable(System, out table) || !table.TryGet(bindingKey, kind, out binding))
			{
				return KingdomBodyPresence.None;
			}
			return PresenceOf(binding, zoneId);
		}

		/// <summary>The ground a binding names, or null when there is no binding for the key. What a
		/// caller needs to tell "the body is on disk" from "the body was destroyed": both fail to
		/// resolve, and only one of them can be swept later.</summary>
		public static bool TryBoundZone(KingdomSystem System, int bindingKey, KingdomBindingKind kind, out string zoneId)
		{
			zoneId = null;
			KingdomBindingTable table;
			KingdomBinding binding;
			if (!TryTable(System, out table) || !table.TryGet(bindingKey, kind, out binding))
			{
				return false;
			}
			zoneId = binding.ZoneId;
			return !string.IsNullOrEmpty(zoneId);
		}

		/// <summary>
		/// Resolves the exact body named by one resident binding, optionally thawing its recorded
		/// zone. Never mints or substitutes. Resolution follows the engine object-id index and then
		/// re-proves binding kind/key, ground, citizenship, and resident-row authority; it never walks
		/// every object in a cached remote zone.
		/// This is the preflight for accession: a caller may move player identity only after this
		/// method has proved the row, binding, object id, zone and body are the same fact.
		/// </summary>
		internal static bool TryResolveBoundBody(KingdomSystem System, int residentId, bool LoadZone,
			out GameObject Body, out string ZoneId)
		{
			Body = null;
			ZoneId = null;
			if (System == null || residentId == 0 || The.ZoneManager == null)
			{
				return false;
			}
			KingdomBindingTable table;
			KingdomBinding binding;
			if (!TryTable(System, out table)
				|| !table.TryGet(residentId, KingdomBindingKind.Resident, out binding)
				|| string.IsNullOrEmpty(binding.ZoneId) || string.IsNullOrEmpty(binding.ObjectId))
			{
				return false;
			}
			Zone zone = null;
			if (The.ZoneManager.CachedZones != null)
			{
				The.ZoneManager.CachedZones.TryGetValue(binding.ZoneId, out zone);
			}
			if (zone == null && LoadZone)
			{
				try
				{
					zone = The.ZoneManager.GetZone(binding.ZoneId);
				}
				catch (Exception ex)
				{
					KingdomLog.Log("binding: resident resolution could not thaw " + binding.ZoneId + " ("
						+ ex.GetType().Name + ")");
					return false;
				}
			}
			if (zone == null)
			{
				return false;
			}

			GameObject exact = null;
			KingdomSurvey active = KingdomSurvey.ActiveFor(zone);
			if (active != null)
			{
				exact = active.FindBoundBody(binding.ObjectId, KingdomBindingKind.Resident);
				KingdomBodyWitness witness;
				if (!active.TryWitnessResident(residentId, out witness)
					|| witness != KingdomBodyWitness.Present) return false;
			}
			else
			{
				exact = FindExactBindingObject(binding);
			}
			if (!GameObject.Validate(exact) || !exact.IsAlive
				|| exact.IsPlayer() || exact.IsPlayerLed()
				|| !string.Equals(exact.IDIfAssigned, binding.ObjectId, StringComparison.Ordinal)
				|| exact.GetIntProperty(ResidentIdProperty) != residentId
				|| !KingdomCitizenship.BelongsTo(System, exact)
				|| exact.GetIntProperty("KingdomBorn") != 1
				|| exact.CurrentCell == null || !ReferenceEquals(exact.CurrentZone, zone)
				|| !string.Equals(exact.CurrentZone?.ZoneID, binding.ZoneId, StringComparison.Ordinal))
			{
				return false;
			}

			KingdomCityBook book;
			int locatedId;
			KingdomCityState state;
			KingdomResidentRow row;
			int rowIndex;
			KingdomCityFault fault;
			if (!TryLocate(System, exact, out book, out locatedId) || locatedId != residentId
				|| book == null || !book.TryRead(out state, out fault)
				|| !state.TryResidentIndex(residentId, out rowIndex)
				|| !state.TryResident(rowIndex, out row)
				|| (row.Standing != KingdomResidentStanding.Resident
					&& row.Standing != KingdomResidentStanding.Expedition)
				|| (!string.IsNullOrEmpty(row.BoundZoneId)
					&& !string.Equals(row.BoundZoneId, binding.ZoneId, StringComparison.Ordinal)))
			{
				return false;
			}
			Body = exact;
			ZoneId = binding.ZoneId;
			return true;
		}

		/// <summary>Exact engine-index lookup for one binding. This deliberately proves only object
		/// identity and binding kind/key. Transaction owners may additionally accept a narrowly
		/// specified in-flight zone before their copy-on-write binding publish catches up.</summary>
		internal static GameObject FindExactBindingObject(KingdomBinding Binding)
		{
			if (string.IsNullOrEmpty(Binding.ObjectId) || Binding.BindingKey <= 0) return null;
			GameObject exact = GameObject.FindByID(Binding.ObjectId);
			if (!GameObject.Validate(exact)
				|| !string.Equals(exact.IDIfAssigned, Binding.ObjectId, StringComparison.Ordinal))
				return null;
			if (Binding.Kind == KingdomBindingKind.Resident)
				return IdOf(exact) == Binding.BindingKey ? exact : null;
			if (Binding.Kind == KingdomBindingKind.Transient)
				return exact.GetIntProperty(JobIdProperty) == Binding.BindingKey ? exact : null;
			return null;
		}

		/// <summary>
		/// Binds this body to this ground, or moves an existing binding onto it. One call, because
		/// the caller's question is always "this key is here now" and splitting it would make
		/// "bind" and "rebind" two chances to get the same fact wrong.
		/// </summary>
		/// <returns>True when the registry was written.</returns>
		public static bool Bind(KingdomSystem System, int bindingKey, KingdomBindingKind kind, string zoneId, GameObject Body, long TimeTicks)
		{
			KingdomBindingTable table;
			if (!TryTable(System, out table) || bindingKey == 0)
			{
				return false;
			}
			string objectId = GameObject.Validate(Body) ? Body.ID : null;
			KingdomBinding standing;
			bool held = table.TryGet(bindingKey, kind, out standing);
			// A settler who has not moved since the last pass costs nothing. Without this, every
			// check-in would republish the whole registry once per person on the ground, to write
			// down where each of them already was.
			if (held
				&& string.Equals(standing.ZoneId ?? "", zoneId ?? "", StringComparison.Ordinal)
				&& string.Equals(standing.ObjectId ?? "", objectId ?? "", StringComparison.Ordinal))
			{
				return true;
			}
			KingdomBindingTable next;
			KingdomCityFault fault;
			bool written = held
				? table.TryRebind(bindingKey, kind, zoneId, objectId, out next, out fault)
				: table.TryBind(bindingKey, kind, zoneId, objectId, (TimeTicks > 0L) ? TimeTicks : 0L, out next, out fault);
			if (!written)
			{
				Refuse("bind", fault);
				return false;
			}
			return Publish(System, next, "bind");
		}

		/// <summary>
		/// Evicts a binding and says why. The cause reaches the log line; the row it belongs to
		/// carries it durably, which is the division &sect;3.8 sets up by keeping no second list of
		/// closed bindings.
		/// </summary>
		public static bool Unbind(KingdomSystem System, int bindingKey, KingdomBindingKind kind, KingdomUnbindCause cause)
		{
			KingdomBindingTable table;
			if (!TryTable(System, out table))
			{
				return false;
			}
			KingdomBindingTable next;
			KingdomBinding evicted;
			KingdomCityFault fault;
			if (!table.TryUnbind(bindingKey, kind, cause, out next, out evicted, out fault))
			{
				// An unknown key is not an error worth a line: a body unbound twice in one pass is
				// the ordinary shape of a death that two consumers both noticed.
				if (fault != KingdomCityFault.UnknownBinding)
				{
					Refuse("unbind", fault);
				}
				return false;
			}
			if (!Publish(System, next, "unbind"))
			{
				return false;
			}
			KingdomLog.Log("binding: released " + kind + " " + bindingKey + " from "
				+ (string.IsNullOrEmpty(evicted.ZoneId) ? "-" : evicted.ZoneId) + " (" + cause + ")");
			return true;
		}

		/// <summary>
		/// What the stale-transient sweep would say about one object in a thawed zone.
		/// <para>
		/// &sect;3.8's t3, and the whole of it that W2 owns: <b>the detection verdict ships now, the
		/// despawn is W3.</b> A body carrying a job id whose binding the model already evicted is
		/// holding goods the stores were credited for at the dated tick — the one instant they
		/// could exist twice.
		/// </para>
		/// </summary>
		public static KingdomSweepVerdict SweepVerdict(KingdomSystem System, GameObject Body)
		{
			if (!GameObject.Validate(Body))
			{
				return KingdomSweepVerdict.NotTransient;
			}
			int jobId = Body.GetIntProperty(JobIdProperty);
			KingdomBindingTable table;
			if (jobId == 0 || !TryTable(System, out table))
			{
				// A registry that would not read cannot prove a body stale, and the sweep's licence
				// is deduplication rather than destruction: without the proof, nothing is touched.
				return KingdomBindingRules.JudgeStale(jobId, true);
			}
			return KingdomBindingRules.JudgeStale(jobId, table.Holds(jobId, KingdomBindingKind.Transient));
		}

		/// <summary>
		/// Invariant I3 over the whole realm, asserted rather than inferred: no binding key ever
		/// resolves to two living bodies. Returns null when the registry is clean, and the fault's
		/// own name when it is not.
		/// </summary>
		public static string AuditLine(KingdomSystem System)
		{
			KingdomBindingTable table;
			if (!TryTable(System, out table))
			{
				return "bindings unreadable";
			}
			KingdomCityFault fault;
			if (!table.TryAudit(out fault))
			{
				return "bindings " + fault;
			}
			return null;
		}

		// ==================================================================================
		// The rows
		// ==================================================================================

		/// <summary>
		/// The roster, rebuilt from the ground under the founder's feet, and the bindings that go
		/// with it.
		/// <para>
		/// Every settler standing in this zone gets an id and a row; every row the book already had
		/// bound to this zone and NOT found on the ground is witnessed and transitioned, except an
		/// expedition row whose realm job owns that named absence and exact binding. Rows bound
		/// to the city's other zones are carried untouched, because this pass has no honest word
		/// about ground it is not standing in — that is the sighting doctrine, unchanged.
		/// </para>
		/// <para>The book is the roll authority. Legacy parallel lists are rewritten from the rows
		/// after publication and are never consulted here except at the one load migration boundary.</para>
		/// </summary>
		internal static KingdomCityState ReadRoster(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCityState state, long TimeTicks)
		{
			if (System == null || Z == null || Survey == null || state == null)
			{
				return state;
			}
			Dictionary<string, int> homes = HomeWorkIds(Survey);
			List<KingdomResidentRow> rows = new List<KingdomResidentRow>();
			HashSet<int> onTheGround = new HashSet<int>();
			for (int i = 0; i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				int id = IdOf(settler);
				if (id == 0)
				{
					id = ClaimIdFor(state, settler, onTheGround);
					if (id > 0)
					{
						settler.SetIntProperty(ResidentIdProperty, id);
						if (id > System.ResidentCounter) System.ResidentCounter = id;
					}
					else id = EnsureId(System, settler);
				}
				if (id == 0 || onTheGround.Contains(id))
				{
					continue;
				}
				// A Resident row without its matching binding violates the city model. Refuse
				// this reading when the registry cannot accept it; never publish half of the pair.
				if (!Bind(System, id, KingdomBindingKind.Resident, Z.ZoneID, settler, TimeTicks))
				{
					continue;
				}
				onTheGround.Add(id);
				rows.Add(RowFor(state, id, settler, Z.ZoneID, homes, TimeTicks));
			}
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!state.TryResident(i, out row) || onTheGround.Contains(row.ResidentId))
				{
					continue;
				}
				if (!string.Equals(row.BoundZoneId, Z.ZoneID, StringComparison.Ordinal))
				{
					rows.Add(row);
					continue;
				}
				rows.Add(Witnessed(System, Z, Survey, row, TimeTicks));
			}
			if (rows.Count > KingdomCityState.MaxResidents)
			{
				// The cap is KingdomRules.MaxPopulation and the ground cannot hold more people than
				// the settlement is allowed; a book that came back over it is trimmed by Normalize
				// rather than by inventing a rule here about who is dropped.
				rows.RemoveRange(KingdomCityState.MaxResidents, rows.Count - KingdomCityState.MaxResidents);
			}
			KingdomCityState written;
			KingdomCityFault fault;
			if (!state.TryWithResidents(rows.ToArray(), out written, out fault))
			{
				Refuse("roster", fault);
				return state;
			}
			return written;
		}

		/// <summary>Adopts one body into an unresolved migrated claim without minting a second
		/// resident. Exact name and origin disambiguate same-name claims; a bound or already-seen id
		/// cannot be adopted twice.</summary>
		private static int ClaimIdFor(KingdomCityState State, GameObject Body,
			HashSet<int> AlreadySeen)
		{
			if (State == null || !GameObject.Validate(Body)) return 0;
			string name = NameOf(Body, null);
			string origin = Body.GetStringProperty("KingdomOrigin") ?? "";
			for (int i = 0; i < State.ResidentCount; i++)
			{
				KingdomResidentRow row;
				if (!State.TryResident(i, out row) || row.Standing != KingdomResidentStanding.Abroad
					|| row.ArrivedTick != 0L || AlreadySeen.Contains(row.ResidentId)
					|| !string.Equals(row.Name, name, StringComparison.Ordinal)
					|| !string.Equals(row.Origin ?? "", origin, StringComparison.Ordinal)) continue;
				return row.ResidentId;
			}
			return 0;
		}

		/// <summary>
		/// The book that holds this body's row, and where in it. The realm is walked seat first,
		/// because the founder is standing in the seated city and that is where nearly every
		/// question about a settler is asked from.
		/// </summary>
		public static bool TryLocate(KingdomSystem System, GameObject Body, out KingdomCityBook book, out int residentId)
		{
			book = null;
			residentId = IdOf(Body);
			if (System == null || residentId == 0)
			{
				return false;
			}
			foreach (KingdomCityBook candidate in Books(System))
			{
				int index;
				if (candidate.TryResidentRow(residentId, out index))
				{
					book = candidate;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The book that holds this body's row, minting the id and the row if it has none.
		/// <para>
		/// The lazy half of the roster read, and it earns its place: a settler who arrives during
		/// the growth step is enrolled, housed and can reach a brink several steps before the next
		/// check-in would have given them a row. Without this their first warning would have
		/// nowhere to live, and the brink storage swap would have silently changed behaviour on the
		/// one settler most likely to have a brink.
		/// </para>
		/// </summary>
		public static bool TryEnsureRow(KingdomSystem System, GameObject Body, out KingdomCityBook book, out int residentId)
		{
			long tick = (The.Game != null) ? The.Game.TimeTicks : 0L;
			return TryEnsureRow(System, Body, Body?.GetStringProperty("KingdomOrigin"), null,
				tick, out book, out residentId);
		}

		/// <summary>Enrols one accepted body with the exact provenance/date frozen by its owning
		/// transaction. The tick is the sole clock; <paramref name="Arrived"/> is presentation
		/// evidence and may be a legacy label.</summary>
		internal static bool TryEnsureRow(KingdomSystem System, GameObject Body, string Origin,
			string Arrived, long ArrivedTick, out KingdomCityBook book, out int residentId)
		{
			if (TryLocate(System, Body, out book, out residentId))
			{
				return true;
			}
			book = null;
			residentId = 0;
			if (System == null || System.City == null || !Enrollable(System, Body))
			{
				return false;
			}
			int id = EnsureId(System, Body);
			if (id == 0)
			{
				return false;
			}
			Zone zone = Body.CurrentZone;
			string zoneId = (zone != null) ? zone.ZoneID : null;
			KingdomCityBook seated = BookFor(System, zoneId) ?? System.City;
			KingdomCityState state;
			KingdomCityFault fault;
			if (!seated.TryRead(out state, out fault))
			{
				Refuse("enrol", fault);
				return false;
			}
			List<KingdomResidentRow> rows = new List<KingdomResidentRow>();
			for (int i = 0; i < state.ResidentCount; i++)
			{
				KingdomResidentRow existing;
				if (state.TryResident(i, out existing))
				{
					rows.Add(existing);
				}
			}
			if (rows.Count >= KingdomCityState.MaxResidents)
			{
				Refuse("enrol", KingdomCityFault.RowCapExceeded);
				return false;
			}
			long tick = ArrivedTick > 0L ? ArrivedTick
				: ((The.Game != null) ? The.Game.TimeTicks : 0L);
			rows.Add(RowFor(state, id, Body, zoneId,
				HomeWorkIds(zone == null ? null : KingdomSurvey.Take(zone, System)),
				tick, Origin, Arrived));
			KingdomCityState written;
			if (!state.TryWithResidents(rows.ToArray(), out written, out fault) || !seated.TryPublish(written, out fault))
			{
				Refuse("enrol", fault);
				return false;
			}
			if (!Bind(System, id, KingdomBindingKind.Resident, zoneId, Body, tick))
			{
				SafePublish(seated, state, "enrol rollback");
				Body.RemoveIntProperty(ResidentIdProperty);
				return false;
			}
			book = seated;
			residentId = id;
			ProjectCompatibility(System);
			return true;
		}

		/// <summary>Marks one exact resident dead in the row before memorial/report projections are
		/// written. The body is never removed; only its live binding is retired.</summary>
		internal static bool TryMarkDead(KingdomSystem System, GameObject Body,
			KingdomStandingCause Cause, out KingdomResidentRow FormerRow)
		{
			FormerRow = default(KingdomResidentRow);
			KingdomCityBook book;
			int residentId;
			if (!TryLocate(System, Body, out book, out residentId))
			{
				KingdomCityBook enrolled;
				int enrolledId;
				if (!TryEnsureRow(System, Body, out enrolled, out enrolledId)
					|| !TryLocate(System, Body, out book, out residentId)) return false;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			int index;
			KingdomResidentRow after;
			KingdomCityState next;
			if (!book.TryRead(out state, out fault)
				|| !state.TryResidentIndex(residentId, out index)
				|| !state.TryResident(index, out FormerRow)) return false;
			// Second death callbacks are ordinary engine noise. False means no new memorial row
			// may be appended; the first callback already committed the terminal transition.
			if (FormerRow.Standing == KingdomResidentStanding.Dead) return false;
			if (!KingdomResidentRules.TryTransition(FormerRow, KingdomBodyWitness.Killed, Cause,
				out after, out fault) || !state.TryWithResident(index, after, out next, out fault))
				return false;
			if (!PublishRowAndUnbind(System, book, state, next, residentId,
				KingdomUnbindCause.Death)) return false;
			ProjectCompatibility(System);
			return true;
		}

		/// <summary>Removes one exact emigrant by ResidentId. Same-name neighbours cannot be struck
		/// accidentally.</summary>
		internal static bool TryDepart(KingdomSystem System, GameObject Body,
			out KingdomResidentRow FormerRow)
		{
			FormerRow = default(KingdomResidentRow);
			KingdomCityBook book;
			int residentId;
			KingdomCityState state;
			KingdomCityState next;
			KingdomCityFault fault;
			if (!TryLocate(System, Body, out book, out residentId) || book == null
				|| !book.TryRead(out state, out fault)
				|| !KingdomResidentRules.TryRemove(state, residentId, out next, out FormerRow,
					out fault)) return false;
			if (!PublishRowAndUnbind(System, book, state, next, residentId,
				KingdomUnbindCause.Abroad)) return false;
			Body.RemoveIntProperty(ResidentIdProperty);
			ProjectCompatibility(System);
			return true;
		}

		private static bool PublishRowAndUnbind(KingdomSystem System, KingdomCityBook Book,
			KingdomCityState Original, KingdomCityState Advanced, int ResidentId,
			KingdomUnbindCause Cause)
		{
			KingdomBindingTable bindings;
			KingdomCityFault fault;
			if (!TryTable(System, out bindings)) return false;
			KingdomBinding held;
			if (!bindings.TryGet(ResidentId, KingdomBindingKind.Resident, out held))
				return SafePublish(Book, Advanced, "resident row transition");
			KingdomBindingTable nextBindings;
			KingdomBinding evicted;
			if (!bindings.TryUnbind(ResidentId, KingdomBindingKind.Resident, Cause,
				out nextBindings, out evicted, out fault)) return false;
			return PublishAccessionCarriers(Book, System.Bindings, Original, Advanced,
				bindings, nextBindings) == KingdomAccessionOutcome.Committed;
		}

		/// <summary>
		/// Takes one exact, bound resident out of the city model when that real body takes the
		/// charter. The returned row is the accession snapshot used for tenure and creed regard.
		/// <para>
		/// This is deliberately narrower than departure. The person has not died or emigrated:
		/// their body still stands, now as the player. What closes is the model's licence to render
		/// or mint that resident identity. Both replacement snapshots are built before either
		/// carrier is published; a failed second publish rolls the first back.
		/// </para>
		/// </summary>
		internal static KingdomAccessionOutcome TryAccede(KingdomSystem System, GameObject Body,
			out KingdomResidentRow formerRow, out bool Seated)
		{
			formerRow = default(KingdomResidentRow);
			Seated = false;
			if (System == null || System.Bindings == null || !GameObject.Validate(Body) || !Body.IsAlive)
			{
				return KingdomAccessionOutcome.RefusedClean;
			}
			KingdomCityBook book;
			int residentId;
			if (!TryLocate(System, Body, out book, out residentId) || book == null || residentId == 0)
			{
				return KingdomAccessionOutcome.RefusedClean;
			}

			KingdomCityState current;
			KingdomCityFault fault;
			int rowIndex;
			if (!book.TryRead(out current, out fault)
				|| !current.TryResidentIndex(residentId, out rowIndex)
				|| !current.TryResident(rowIndex, out formerRow)
				|| formerRow.Standing != KingdomResidentStanding.Resident)
			{
				Refuse("accession row", fault);
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}

			KingdomBindingTable bindings;
			KingdomBinding held;
			string bodyZone = Body.CurrentZone?.ZoneID;
			if (!TryTable(System, out bindings)
				|| !bindings.TryGet(residentId, KingdomBindingKind.Resident, out held)
				|| string.IsNullOrEmpty(bodyZone)
				|| !string.Equals(held.ObjectId, Body.ID, StringComparison.Ordinal)
				|| !string.Equals(held.ZoneId, bodyZone, StringComparison.Ordinal)
				|| (!string.IsNullOrEmpty(formerRow.BoundZoneId)
					&& !string.Equals(formerRow.BoundZoneId, bodyZone, StringComparison.Ordinal)))
			{
				KingdomLog.Log("binding: accession refused; the chosen body is not the exact live resident binding");
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}

			// Accession is keyed on the row and binding only. Compatibility projections are rebuilt
			// after both durable carriers commit; they never veto or identify the heir.
			Seated = ReferenceEquals(book, System.City);
			KingdomSettlement away = (!Seated && System.Away != null
				&& ReferenceEquals(book, System.Away.City)) ? System.Away : null;
			Dictionary<string, int> creedCounts = Seated ? System.CreedCounts : away?.CreedCounts;
			Dictionary<string, int> creedPastCounts = Seated ? System.CreedPastCounts : away?.CreedPastCounts;
			if ((!Seated && away == null) || creedCounts == null || creedPastCounts == null)
			{
				KingdomLog.Log("binding: accession refused; the chosen resident's settlement tallies are unreadable");
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}
			Dictionary<string, int> nextCreedCounts = new Dictionary<string, int>(creedCounts);
			Dictionary<string, int> nextCreedPastCounts = new Dictionary<string, int>(creedPastCounts);
			string rollName = Body.GetStringProperty("KingdomName");
			string origin = Body.GetStringProperty("KingdomOrigin") ?? "";
			if (string.IsNullOrEmpty(rollName)
				|| !string.Equals(rollName, formerRow.Name, StringComparison.Ordinal)
				|| !string.Equals(origin, formerRow.Origin ?? "", StringComparison.Ordinal))
			{
				KingdomLog.Log("binding: accession refused; city row and living body disagree about the heir");
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}
			string citizenshipFailure;
			if (!KingdomCitizenship.CanRemove(System, Body, out citizenshipFailure))
			{
				KingdomLog.Log("binding: accession refused; citizenship cannot be removed exactly ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}
			DropCount(nextCreedCounts, Body.GetStringProperty(KingdomCreed.CreedProperty));
			List<string> pastCreeds = KingdomCreedRules.DecodeKept(
				Body.GetStringProperty(KingdomCreed.CreedPastProperty));
			for (int i = 0; i < pastCreeds.Count; i++)
			{
				DropCount(nextCreedPastCounts, pastCreeds[i]);
			}

			KingdomCityState nextCity;
			KingdomBindingTable nextBindings;
			KingdomBinding evicted;
			KingdomResidentRow removed;
			if (!KingdomResidentRules.TryRemove(current, residentId, out nextCity, out removed,
					out fault)
				|| !bindings.TryUnbind(residentId, KingdomBindingKind.Resident,
					KingdomUnbindCause.Accession, out nextBindings, out evicted, out fault))
			{
				Refuse("accession prepare", fault);
				formerRow = default(KingdomResidentRow);
				return KingdomAccessionOutcome.RefusedClean;
			}

			KingdomAccessionOutcome outcome = PublishAccessionCarriers(book, System.Bindings,
				current, nextCity, bindings, nextBindings);
			if (outcome != KingdomAccessionOutcome.Committed)
			{
				if (outcome == KingdomAccessionOutcome.RefusedClean)
				{
					formerRow = default(KingdomResidentRow);
				}
				return outcome;
			}
			if (Seated)
			{
				System.CreedCounts = nextCreedCounts;
				System.CreedPastCounts = nextCreedPastCounts;
			}
			else
			{
				away.CreedCounts = nextCreedCounts;
				away.CreedPastCounts = nextCreedPastCounts;
			}
			ProjectCompatibility(System);
			if (!KingdomCitizenship.TryRemove(System, Body,
				KingdomCitizenshipRemovalReason.Accession, out citizenshipFailure))
			{
				KingdomLog.Log("binding: accession citizenship completion requires repair ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				return KingdomAccessionOutcome.RepairRequired;
			}
			FinishAccessionBody(Body, formerRow, residentId);
			return KingdomAccessionOutcome.Committed;
		}

		internal static KingdomAccessionOutcome TryRepairAccession(KingdomSystem System,
			GameObject Body, int ResidentId, bool Seated, string Name, long ArrivedTick,
			string KeptCreeds, out KingdomResidentRow FormerRow)
		{
			FormerRow = default(KingdomResidentRow);
			if (System == null || System.Bindings == null || !GameObject.Validate(Body)
				|| !Body.IsAlive || ResidentId == 0 || string.IsNullOrEmpty(Name))
			{
				return KingdomAccessionOutcome.RepairRequired;
			}
			KingdomSettlement away = Seated ? null : System.Away;
			KingdomCityBook book = Seated ? System.City : away?.City;
			KingdomCityState city;
			KingdomBindingTable bindings;
			KingdomCityFault fault;
			if (book == null || !book.TryRead(out city, out fault)
				|| !System.Bindings.TryRead(out bindings, out fault))
			{
				return KingdomAccessionOutcome.RepairRequired;
			}

			int rowIndex;
			bool hasRow = city.TryResidentIndex(ResidentId, out rowIndex);
			if (hasRow)
			{
				if (!city.TryResident(rowIndex, out FormerRow)
					|| FormerRow.Standing != KingdomResidentStanding.Resident
					|| FormerRow.Name != Name || FormerRow.ArrivedTick != ArrivedTick
					|| (FormerRow.KeptCreeds ?? "") != (KeptCreeds ?? ""))
				{
					return KingdomAccessionOutcome.RepairRequired;
				}
			}
			else
			{
				FormerRow = new KingdomResidentRow(ResidentId, Name, 0, 0, ArrivedTick,
					0, 0, 0, KingdomDayShape.Hearth, KingdomResidentStanding.Resident,
					KingdomStandingCause.None, Body.CurrentZone?.ZoneID,
					KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0, KeptCreeds);
			}

			KingdomBinding held;
			bool hasBinding = bindings.TryGet(ResidentId, KingdomBindingKind.Resident, out held);
			string bodyZone = Body.CurrentZone?.ZoneID;
			if (hasBinding && (string.IsNullOrEmpty(bodyZone)
				|| held.ObjectId != Body.ID || held.ZoneId != bodyZone))
			{
				return KingdomAccessionOutcome.RepairRequired;
			}

			Dictionary<string, int> creedCounts = Seated ? System.CreedCounts : away?.CreedCounts;
			Dictionary<string, int> creedPastCounts = Seated ? System.CreedPastCounts : away?.CreedPastCounts;
			if ((!Seated && away == null) || creedCounts == null || creedPastCounts == null)
			{
				return KingdomAccessionOutcome.RepairRequired;
			}
			Dictionary<string, int> nextCreedCounts = new Dictionary<string, int>(creedCounts);
			Dictionary<string, int> nextCreedPastCounts = new Dictionary<string, int>(creedPastCounts);
			string rollName = Body.GetStringProperty("KingdomName");
			if (rollName != Name)
			{
				return KingdomAccessionOutcome.RepairRequired;
			}
			string citizenshipFailure;
			if (!KingdomCitizenship.CanRemove(System, Body, out citizenshipFailure))
			{
				KingdomLog.Log("binding: accession repair cannot remove citizenship exactly ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				return KingdomAccessionOutcome.RepairRequired;
			}
			DropCount(nextCreedCounts, Body.GetStringProperty(KingdomCreed.CreedProperty));
			List<string> pastCreeds = KingdomCreedRules.DecodeKept(
				Body.GetStringProperty(KingdomCreed.CreedPastProperty));
			for (int i = 0; i < pastCreeds.Count; i++) DropCount(nextCreedPastCounts, pastCreeds[i]);

			if (hasRow)
			{
				KingdomCityState nextCity;
				KingdomResidentRow removed;
				if (!KingdomResidentRules.TryRemove(city, ResidentId, out nextCity, out removed,
						out fault)
					|| !SafePublish(book, nextCity, "accession repair city"))
				{
					return KingdomAccessionOutcome.RepairRequired;
				}
			}
			if (hasBinding)
			{
				KingdomBindingTable nextBindings;
				KingdomBinding evicted;
				if (!bindings.TryUnbind(ResidentId, KingdomBindingKind.Resident,
					KingdomUnbindCause.Accession, out nextBindings, out evicted, out fault)
					|| !SafePublish(System.Bindings, nextBindings, "accession repair registry"))
				{
					return KingdomAccessionOutcome.RepairRequired;
				}
			}
			if (!AccessionAbsent(book, System.Bindings, ResidentId))
			{
				return KingdomAccessionOutcome.RepairRequired;
			}
			if (Seated)
			{
				System.CreedCounts = nextCreedCounts;
				System.CreedPastCounts = nextCreedPastCounts;
			}
			else
			{
				away.CreedCounts = nextCreedCounts;
				away.CreedPastCounts = nextCreedPastCounts;
			}
			ProjectCompatibility(System);
			if (!KingdomCitizenship.TryRemove(System, Body,
				KingdomCitizenshipRemovalReason.Accession, out citizenshipFailure))
			{
				KingdomLog.Log("binding: accession repair left citizenship pending ("
					+ (citizenshipFailure ?? "unknown failure") + ")");
				return KingdomAccessionOutcome.RepairRequired;
			}
			FinishAccessionBody(Body, FormerRow, ResidentId);
			return KingdomAccessionOutcome.Committed;
		}

		private static KingdomAccessionOutcome PublishAccessionCarriers(KingdomCityBook Book,
			KingdomBindingRegistry Registry, KingdomCityState OriginalCity,
			KingdomCityState AdvancedCity, KingdomBindingTable OriginalBindings,
			KingdomBindingTable AdvancedBindings)
		{
			SafePublish(Book, AdvancedCity, "accession city");
			for (int attempt = 0; attempt < 4; attempt++)
			{
				KingdomAccessionCarrierState state = ReadAccessionCarriers(Book, Registry,
					OriginalCity, AdvancedCity, OriginalBindings, AdvancedBindings);
				switch (state)
				{
				case KingdomAccessionCarrierState.Original:
					if (attempt == 0) return KingdomAccessionOutcome.RefusedClean;
					SafePublish(Book, AdvancedCity, "accession city retry");
					break;
				case KingdomAccessionCarrierState.CityAdvanced:
					SafePublish(Registry, AdvancedBindings, "accession registry");
					break;
				case KingdomAccessionCarrierState.BindingAdvanced:
					SafePublish(Book, AdvancedCity, "accession city completion");
					break;
				case KingdomAccessionCarrierState.Committed:
					return KingdomAccessionOutcome.Committed;
				default:
					return KingdomAccessionOutcome.RepairRequired;
				}
			}
			return ReadAccessionCarriers(Book, Registry, OriginalCity, AdvancedCity,
				OriginalBindings, AdvancedBindings) == KingdomAccessionCarrierState.Committed
				? KingdomAccessionOutcome.Committed : KingdomAccessionOutcome.RepairRequired;
		}

		private static KingdomAccessionCarrierState ReadAccessionCarriers(KingdomCityBook Book,
			KingdomBindingRegistry Registry, KingdomCityState OriginalCity,
			KingdomCityState AdvancedCity, KingdomBindingTable OriginalBindings,
			KingdomBindingTable AdvancedBindings)
		{
			try
			{
				KingdomCityState city;
				KingdomBindingTable bindings;
				KingdomCityFault fault;
				if (!Book.TryRead(out city, out fault) || !Registry.TryRead(out bindings, out fault))
				{
					return KingdomAccessionCarrierState.Unknown;
				}
				return KingdomResidentRules.AccessionCarriers(
					KingdomResidentRules.SameCity(city, OriginalCity),
					KingdomResidentRules.SameCity(city, AdvancedCity),
					SameBindings(bindings, OriginalBindings), SameBindings(bindings, AdvancedBindings));
			}
			catch (Exception ex)
			{
				KingdomLog.Log("binding: accession carrier reproof threw " + ex.GetType().Name);
				return KingdomAccessionCarrierState.Unknown;
			}
		}

		private static bool AccessionAbsent(KingdomCityBook Book,
			KingdomBindingRegistry Registry, int ResidentId)
		{
			KingdomCityState city;
			KingdomBindingTable bindings;
			KingdomCityFault fault;
			int row;
			KingdomBinding binding;
			return Book.TryRead(out city, out fault) && Registry.TryRead(out bindings, out fault)
				&& !city.TryResidentIndex(ResidentId, out row)
				&& !bindings.TryGet(ResidentId, KingdomBindingKind.Resident, out binding);
		}

		private static bool SafePublish(KingdomCityBook Book, KingdomCityState State, string Context)
		{
			try
			{
				KingdomCityFault fault;
				if (Book.TryPublish(State, out fault)) return true;
				Refuse(Context, fault);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("binding: " + Context + " threw " + ex.GetType().Name);
			}
			return false;
		}

		private static bool SafePublish(KingdomBindingRegistry Registry,
			KingdomBindingTable State, string Context)
		{
			try
			{
				KingdomCityFault fault;
				if (Registry.TryPublish(State, out fault)) return true;
				Refuse(Context, fault);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("binding: " + Context + " threw " + ex.GetType().Name);
			}
			return false;
		}

		private static void FinishAccessionBody(GameObject Body, KingdomResidentRow FormerRow,
			int ResidentId)
		{
			try
			{
				KingdomStations.Post(Body, 0, KingdomWorkKind.Other);
				Body.RemoveIntProperty(ResidentIdProperty);
				Body.RemoveIntProperty("KingdomCitizen");
				Body.RemoveIntProperty("KingdomBorn");
				Body.RemoveStringProperty("KingdomName");
				Body.RemoveStringProperty(KingdomLodging.HomePlotIdProperty);
				KingdomLog.Log("binding: " + (FormerRow.Name ?? "-") + " (" + ResidentId
					+ ") left the resident roll by accession");
			}
			catch (Exception ex)
			{
				KingdomLog.Log("binding: accession body cleanup remains idempotently pending ("
					+ ex.GetType().Name + ")");
			}
		}

		private static bool SameBindings(KingdomBindingTable A, KingdomBindingTable B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++)
			{
				KingdomBinding a;
				KingdomBinding b;
				if (!A.TryAt(i, out a) || !B.TryAt(i, out b)
					|| a.BindingKey != b.BindingKey || a.Kind != b.Kind || a.ZoneId != b.ZoneId
					|| a.ObjectId != b.ObjectId || a.MintedTick != b.MintedTick) return false;
			}
			return true;
		}

		/// <summary>Removes one person from a per-city tally without leaving zero rows behind.</summary>
		private static void DropCount(Dictionary<string, int> Counts, string Key)
		{
			if (Counts == null || Key == null || !Counts.TryGetValue(Key, out int count))
			{
				return;
			}
			if (count > 1)
			{
				Counts[Key] = count - 1;
			}
			else
			{
				Counts.Remove(Key);
			}
		}

		// ==================================================================================
		// Small shared helpers
		// ==================================================================================

		/// <summary>
		/// One settler's row as the ground reads them: their name, where they walked in from, what
		/// they hold with, and the roof over them. An existing row keeps its arrival tick and its
		/// brink windows — those are facts about a person and not readings off a zone.
		/// </summary>
		private static KingdomResidentRow RowFor(KingdomCityState state, int id, GameObject settler,
			string zoneId, Dictionary<string, int> homes, long TimeTicks, string Origin = null,
			string Arrived = null)
		{
			int homeWorkId = 0;
			string plotId = settler.GetStringProperty(KingdomLodging.HomePlotIdProperty);
			if (!string.IsNullOrEmpty(plotId) && homes != null)
			{
				homes.TryGetValue(plotId, out homeWorkId);
			}
			string origin = Origin ?? settler.GetStringProperty("KingdomOrigin") ?? "";
			int originCode = KingdomResidentRules.OriginCode(origin);
			int creedCode = KingdomCityRules.StableId(settler.GetStringProperty(KingdomCreed.CreedProperty));
			// Addendum 16's recorded fact, read off the person the same way their present creed is.
			// The row keeps the very string the settler carries, so the column costs a reference and
			// the heap nothing.
			string keptCreeds = settler.GetStringProperty(KingdomCreed.CreedPastProperty);
			// W3 stamps the post on the person: KingdomGrowth.AssignWork already knew which
			// settlers it crewed which work with (KingdomCrewRules.CrewOutcome.SettlerIndices) and
			// now writes it down, so the column is a fact rather than a placeholder. A settler the
			// works have no room for still reads zero, and their day shape still derives to the
			// hearth — which is what an unposted settler's day actually is, not a stand-in for one.
			int jobWorkId = KingdomStations.PostOf(settler);
			KingdomWorkKind jobKind = (KingdomWorkKind)settler.GetIntProperty(KingdomStations.PostKindProperty);
			KingdomDayShape dayShape = KingdomResidentRules.DayShapeFor(jobWorkId, jobKind);
			int index;
			KingdomResidentRow existing;
			if (state.TryResidentIndex(id, out index) && state.TryResident(index, out existing))
			{
				return existing
					.WithReading(NameOf(settler, existing.Name), origin, originCode, creedCode,
						homeWorkId, jobWorkId, 0, dayShape)
					.WithKeptCreeds(keptCreeds)
					.WithBoundZone(zoneId)
					.WithStanding(existing.Standing == KingdomResidentStanding.Expedition
						? KingdomResidentStanding.Expedition : KingdomResidentStanding.Resident,
						KingdomStandingCause.None);
			}
			return new KingdomResidentRow(id, NameOf(settler, null), originCode, creedCode,
				(TimeTicks > 0L) ? TimeTicks : 0L, homeWorkId, jobWorkId, 0, dayShape,
				KingdomResidentStanding.Resident, KingdomStandingCause.None, zoneId,
				KingdomBrinkWindow.None, KingdomBrinkWindow.None, null, 0, keptCreeds, origin,
				string.IsNullOrEmpty(Arrived) ? DateAt(TimeTicks) : Arrived);
		}

		private static string DateAt(long Tick)
		{
			if (Tick <= 0L) return "";
			return Calendar.GetDay(Tick) + " of " + Calendar.GetMonth(Tick) + ", "
				+ Calendar.GetYear(Tick) + " AR";
		}

		/// <summary>
		/// What the pass can honestly say about a row bound to this zone whose body is not among
		/// its settlers.
		/// <para>
		/// The survey excludes a settler the founder has charmed or recruited (<c>IsPlayerLed</c>),
		/// so a body still standing here is exactly &sect;8.3's <c>Abroad</c>: on the roll, doing no
		/// work, and honestly reported. A body that is not in the zone at all has gone somewhere
		/// this pass cannot see, and the honest word for that is also <c>Abroad</c> — never Dead,
		/// which nobody witnessed.
		/// </para>
		/// <para>
		/// <b>The binding goes with the standing.</b> A row that stops being <c>Resident</c> stops
		/// having a bound body in this city's ground, which is the equation &sect;8.3 invariant 3
		/// states and <c>KingdomResidentRules.TryReconcile</c> checks.
		/// </para>
		/// </summary>
		private static KingdomResidentRow Witnessed(KingdomSystem System, Zone Z,
			KingdomSurvey Survey, KingdomResidentRow row, long TimeTicks)
		{
			if (row.Standing == KingdomResidentStanding.Expedition)
			{
				// The expedition job owns this named absence, exact binding, and body marker.
				// Check-in must not release the evidence its later semantic lane needs to tell
				// dead/missing/followed apart or to forward-recover an interrupted dispatch.
				return row;
			}
			KingdomBodyWitness witness;
			if (Survey == null || !Survey.TryWitnessResident(row.ResidentId, out witness))
			{
				KingdomLog.Log("resident: duplicate body evidence for " + row.ResidentId
					+ " in " + (Z?.ZoneID ?? "-") + "; row was not changed");
				return row;
			}
			KingdomResidentRow next;
			KingdomCityFault fault;
			if (!KingdomResidentRules.TryTransition(row, witness, KingdomStandingCause.Unwitnessed, out next, out fault))
			{
				// A dead row is terminal and refusing to move it is the rule working, not a fault
				// worth a line in the founder's log.
				return row;
			}
			if (next.Standing != row.Standing && !KingdomResidentRules.Bindable(next.Standing))
			{
				Unbind(System, row.ResidentId, KingdomBindingKind.Resident, KingdomResidentRules.UnbindFor(next.Standing));
				KingdomLog.Log("resident: " + (row.Name ?? "-") + " (" + row.ResidentId + ") reads " + next.Standing
					+ " (" + next.Cause + ") in " + Z.ZoneID);
			}
			return next;
		}

		/// <summary>Every home plot standing in this zone, by the work row id of the object that
		/// carries it — so a row's home is the same id a work row is keyed on rather than a second
		/// identifier for the same building.</summary>
		private static Dictionary<string, int> HomeWorkIds(KingdomSurvey Survey)
		{
			Dictionary<string, int> homes = new Dictionary<string, int>();
			if (Survey == null)
			{
				return homes;
			}
			for (int i = 0; i < Survey.PlotRoots.Count; i++)
			{
				GameObject item = Survey.PlotRoots[i];
				string plotId = item.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (!string.IsNullOrEmpty(plotId) && !homes.ContainsKey(plotId))
				{
					homes[plotId] = KingdomCityRules.StableId(item.ID);
				}
			}
			return homes;
		}

		/// <summary>
		/// Whether this body is one the city would count as its own settler.
		/// <para>
		/// Exactly <c>KingdomSurvey</c>'s own filter, and that is the point: the lazy enrolment
		/// above and the roster read at check-in must agree about who is on the roll, or a
		/// merchant or a founding citizen would take one of the sixty rows the settlement is
		/// allowed. Both brink paths that can reach the lazy enrolment are already gated on the
		/// settler's roll name, which only an arrival carries, so nothing that used to record a
		/// brink stops being able to.
		/// </para>
		/// </summary>
		private static bool Enrollable(KingdomSystem System, GameObject Body)
		{
			return GameObject.Validate(Body)
				&& KingdomCitizenship.BelongsTo(System, Body)
				&& Body.GetIntProperty("KingdomBorn") == 1
				&& !Body.IsPlayer()
				&& !Body.IsPlayerLed();
		}

		private static string NameOf(GameObject settler, string fallback)
		{
			string named = settler.GetStringProperty("KingdomName");
			if (!string.IsNullOrEmpty(named))
			{
				return named;
			}
			return string.IsNullOrEmpty(fallback) ? (settler.BaseDisplayName ?? "") : fallback;
		}

		/// <summary>Where the bound object is, relative to the ground being asked about. A zone the
		/// manager cannot hand back is a zone on disk, and a body in one is frozen.</summary>
		private static KingdomBodyPresence PresenceOf(KingdomBinding binding, string zoneId)
		{
			if (string.IsNullOrEmpty(binding.ObjectId) || string.IsNullOrEmpty(binding.ZoneId))
			{
				return KingdomBodyPresence.Frozen;
			}
			// FindByID asks Qud's exact object-id index over already resident ground and never thaws a
			// zone. Absence therefore remains the durable Frozen verdict; no remote classification is
			// performed merely to answer check-before-mint.
			GameObject exact = FindExactBindingObject(binding);
			if (!GameObject.Validate(exact) || exact.CurrentZone == null) return KingdomBodyPresence.Frozen;
			return string.Equals(exact.CurrentZone.ZoneID, zoneId, StringComparison.Ordinal)
				? KingdomBodyPresence.Here
				: KingdomBodyPresence.Elsewhere;
		}

		/// <summary>Every book the realm holds, seat first. The registry is realm-scope precisely
		/// because a bound body can be in the other city's ground.</summary>
		private static IEnumerable<KingdomCityBook> Books(KingdomSystem System)
		{
			if (System.City != null)
			{
				yield return System.City;
			}
			if (System.Away != null && System.Away.City != null)
			{
				yield return System.Away.City;
			}
		}

		private static KingdomCityBook BookFor(KingdomSystem System, string zoneId)
		{
			if (string.IsNullOrEmpty(zoneId))
			{
				return null;
			}
			if (System.ClaimedZones != null && System.ClaimedZones.Contains(zoneId))
			{
				return System.City;
			}
			if (System.Away != null && System.Away.ClaimedZones != null && System.Away.ClaimedZones.Contains(zoneId))
			{
				return System.Away.City;
			}
			return null;
		}

		private static bool TryTable(KingdomSystem System, out KingdomBindingTable table)
		{
			table = null;
			if (System == null || System.Bindings == null)
			{
				return false;
			}
			KingdomCityFault fault;
			if (!System.Bindings.TryRead(out table, out fault))
			{
				Refuse("registry", fault);
				return false;
			}
			return true;
		}

		private static bool Publish(KingdomSystem System, KingdomBindingTable table, string step)
		{
			KingdomCityFault fault;
			if (!System.Bindings.TryPublish(table, out fault))
			{
				Refuse(step, fault);
				return false;
			}
			return true;
		}

		private static void Refuse(string step, KingdomCityFault fault)
		{
			KingdomLog.Log("binding: " + step + " refused (" + fault + "); the registry is unchanged");
		}
	}
}
