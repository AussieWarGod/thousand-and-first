using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// A standing source of conversion pressure on a settlement's people &mdash; a shrine
	/// consecrated over a quarter, or anything a later wave builds that works the same way.
	/// <para>
	/// A <c>Protocol</c>-shaped contract rather than a call the source makes once, and
	/// deliberately: pressure is a FACT re-derived on every attended pass, not an event that
	/// happened. A source that fired once and left a counter running would keep pushing a settler
	/// toward the road after the founder had already deconsecrated the shrine, and the founder
	/// would have no way to tell. Asked fresh every pass, taking the pressure off takes it off.
	/// </para>
	/// <para>
	/// Register with <see cref="KingdomConversion.AddPressureSource"/>. Implementations are
	/// untrusted (STANDARDS.md &sect;9): one that throws is logged and skipped, and never takes
	/// the settlement pass down with it.
	/// </para>
	/// </summary>
	public interface IConversionPressure
	{
		/// <summary>
		/// The creed this source is imposing on <paramref name="Settler"/> where they stand, or
		/// null when it is imposing nothing on them.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The claimed zone the settler is standing in.</param>
		/// <param name="Settler">The settler being asked about.</param>
		/// <returns>A faction name, or null. Whether the settler resents it is not this source's
		/// question &mdash; <see cref="KingdomConversionRules.Resents"/> decides that, from the
		/// engine's own faction feelings.</returns>
		string PressingCreed(KingdomSystem System, Zone Z, GameObject Settler);
	}

	/// <summary>
	/// Conversion, and the exit from it. The engine-coupled shell for Addendum 5's two passive
	/// channels and for the guard every channel shares.
	/// <para>
	/// <b>Osmosis.</b> Every attended pass, each housed settler whose household holds a creed by
	/// strict majority is pulled a little toward it, scaled by the closeness ladder: nothing at
	/// all in one open room (the people in it already agree by construction), fastest in a hut,
	/// slowest in quarters of one's own, and nothing across a feeling the quarters would refuse.
	/// The stone house is the only architecture that holds an ambient grudge under one roof, so
	/// the stone house is where a real difference actually gets crossed &mdash; which is the whole
	/// of the healing arc the fault-line ceiling (Addendum 4d) needs. Partition, then build
	/// better, then the quarters dissolve.
	/// </para>
	/// <para>
	/// <b>Culture.</b> Each witnessed shared meal nudges its attendees toward the table's own
	/// majority: small, and capped at <see cref="KingdomConversionRules.MealCeilingPercent"/> of
	/// the road so a settlement can never eat its way to a conversion. A free rider on a ceremony
	/// the founder was already holding for other reasons.
	/// </para>
	/// <para>
	/// <b>The exit.</b> A settler may always emigrate rather than convert. Living beside somebody
	/// generates no pressure &mdash; that is the arc working &mdash; but a creed IMPOSED on a
	/// settler who resents it (a realm declaration against theirs, a rival shrine consecrated in
	/// their quarter) starts them toward the road instead: named once, given
	/// <see cref="KingdomConversionRules.ResentedPasses"/> attended passes for the founder to take
	/// it off, then gone through the settlement's ordinary emigration, chronicled by name and
	/// cause in both registers. <see cref="NotePressure"/> and
	/// <see cref="AddPressureSource"/> are the surface every other channel uses; nothing may build
	/// its own.
	/// </para>
	/// <para>
	/// <b>Counted in shared living, never in time.</b> Nothing in this file reads a tick, a day or
	/// a calendar. Every counter moves in <see cref="OnSettlementPass"/> and in
	/// <see cref="OnSharedMeal"/>, which run only when the founder is standing on the ground, so a
	/// settlement left alone for a season comes home holding exactly what it held when it was
	/// left. Time is labour here, never maturation.
	/// </para>
	/// </summary>
	public static class KingdomConversion
	{
		/// <summary>
		/// Whether the passive channels run at all. Conversion is creed machinery working through
		/// the lodging assignment, so it is off whenever either of those is off rather than
		/// carrying a toggle of its own for a thing that cannot happen without both.
		/// </summary>
		public static bool Enabled
		{
			get { return KingdomCreed.Enabled && KingdomLodging.Enabled; }
		}

		// Standing pressure sources, asked fresh on every attended pass. A list rather than a
		// single hook because two shrines in two quarters are two sources, and the first one that
		// names a creed this settler resents is the one they leave over -- a second grievance does
		// not make anybody leave twice.
		private static readonly List<IConversionPressure> Sources = new List<IConversionPressure>();

		/// <summary>
		/// Registers a standing source of conversion pressure. Idempotent: registering the same
		/// instance twice registers it once, so a loader that re-runs cannot make one shrine press
		/// twice as hard.
		/// </summary>
		/// <param name="Source">The source. Null is ignored.</param>
		public static void AddPressureSource(IConversionPressure Source)
		{
			if (Source == null || Sources.Contains(Source))
			{
				return;
			}
			Sources.Add(Source);
		}

		/// <summary>Forgets every registered pressure source. For a registry loader re-reading its
		/// streams, and for tests; a save carries none of this.</summary>
		public static void ClearPressureSources()
		{
			Sources.Clear();
		}

		/// <summary>
		/// The kingdom's one attended pass over what its people believe: pulls each household's
		/// minority a little toward its majority, turns anyone the road and the draw agree on, and
		/// spends one pass of grace on anyone standing under a creed they resent.
		/// <para>
		/// Preconditions: called from the settlement pass, on claimed ground, AFTER
		/// <c>KingdomLodging.OnSettlementPass</c> &mdash; who sleeps where is the input, so a pass
		/// that ran first would read yesterday's households. Side effects: shared living accrues,
		/// a conversion may be recorded in both registers, a settler may leave through
		/// <c>KingdomGrowth.Emigrate</c>. Failure mode: returns having done nothing.
		/// </para>
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<GameObject> residents = ResidentsIn(Z);
			if (residents.Count == 0)
			{
				return;
			}
			Dictionary<string, List<GameObject>> households = Households(residents);
			for (int i = 0; i < residents.Count; i++)
			{
				Osmosis(System, Z, residents[i], households);
			}
			for (int i = 0; i < residents.Count; i++)
			{
				Pressure(System, Z, residents[i]);
			}
			ForgetDeparted(System);
		}

		/// <summary>
		/// Addendum 5's culture channel: the meal the founder just held nudges everyone who sat
		/// down at it toward the table's own majority creed.
		/// <para>
		/// Preconditions: called from <c>KingdomLarder.HoldSharedMeal</c> once the meal has
		/// actually been spent and recorded, so a meal that failed for want of food nudges
		/// nobody. Side effects: shared living accrues for the attendees, and a conversion may be
		/// recorded. Failure mode: returns having done nothing.
		/// </para>
		/// <para>
		/// The table's majority is read with <c>KingdomCreedRules.DominantCreed</c> over the
		/// people actually standing there &mdash; the same rule that decides what a city believes,
		/// asked of one evening &mdash; so a small or evenly split table nudges nobody at all, and
		/// the meal never invents a majority the settlement does not have.
		/// </para>
		/// </summary>
		public static void OnSharedMeal(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<GameObject> attendees = ResidentsIn(Z);
			if (attendees.Count == 0)
			{
				return;
			}
			Dictionary<string, int> counts = CreedCounts(attendees);
			string majority = KingdomCreedRules.DominantCreed(counts, attendees.Count);
			if (string.IsNullOrEmpty(majority))
			{
				return;
			}
			for (int i = 0; i < attendees.Count; i++)
			{
				GameObject attendee = attendees[i];
				string roll = RollNameOf(attendee);
				if (roll == null || attendee.GetStringProperty(KingdomCreed.CreedProperty) == majority)
				{
					continue;
				}
				ConversionProgress progress = ProgressOf(System, roll);
				// The ceiling applies to progress TOWARD the table's creed. A settler being pulled
				// somewhere else is not accumulating here at all -- the meal is taking points off
				// that other pull -- so the full nudge crosses over uncapped.
				int points = (progress.Creed == null || progress.Creed == majority)
					? KingdomConversionRules.MealSharedFor(progress.Shared)
					: KingdomConversionRules.MealShared;
				SetProgress(System, roll, KingdomConversionRules.Advance(progress, majority, points));
				MaybeConvert(System, Z, attendee, roll, ConversionChannel.Culture);
			}
		}

		/// <summary>
		/// Records that a channel is imposing a creed on one settler right now. The immediate form
		/// of <see cref="IConversionPressure"/>, for the moment of the act itself &mdash; the day
		/// a shrine is consecrated &mdash; so the founder is told on that day rather than on their
		/// next pass.
		/// <para>
		/// Side effects: announces the pressure once by name in both registers and starts the
		/// grace. Failure mode: returns false and changes nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The claimed zone the settler stands in.</param>
		/// <param name="Settler">The settler. Must be on the roll; a settler the registers cannot
		/// name is never walked out of the settlement.</param>
		/// <param name="Channel">The channel imposing it. Refused for any channel
		/// <see cref="KingdomConversionRules.IsImposed"/> rejects &mdash; osmosis and the shared
		/// table are chosen proximity, and a household that could push somebody out for living in
		/// it would make the healing arc into the thing it was written against.</param>
		/// <param name="PressingCreed">The creed being imposed.</param>
		/// <returns>True when the settler resents it and the grace has begun; false when they do
		/// not resent it, which is most people.</returns>
		public static bool NotePressure(KingdomSystem System, Zone Z, GameObject Settler, ConversionChannel Channel, string PressingCreed)
		{
			if (!Enabled || System == null || !System.Founded || Settler == null || string.IsNullOrEmpty(PressingCreed))
			{
				return false;
			}
			if (!KingdomConversionRules.IsImposed(Channel))
			{
				return false;
			}
			string roll = RollNameOf(Settler);
			if (roll == null)
			{
				return false;
			}
			int hostility = KingdomCreed.HostilityBetween(Settler.GetStringProperty(KingdomCreed.CreedProperty), PressingCreed);
			if (!KingdomConversionRules.Resents(hostility))
			{
				return false;
			}
			BeginResentment(System, roll, PressingCreed);
			return true;
		}

		/// <summary>
		/// One settler changes creed. The one path a conversion may take, whichever channel turned
		/// them: the tally moves through <c>KingdomCreed.Forget</c> and <c>KingdomCreed.Record</c>
		/// and never through a second route of its own, both registers are written (disagreeing
		/// with each other where the day is contested), and whatever was pulling at them is
		/// cleared.
		/// <para>
		/// Side effects: the settler's creed property and the city's <c>CreedCounts</c> change,
		/// two chronicle entries and one ledger note are written, and any standing grace this
		/// settler was spending is forgotten &mdash; a person who has taken the creed is no longer
		/// under pressure from it. Failure mode: returns false and changes nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The claimed zone it happened in.</param>
		/// <param name="Settler">The settler.</param>
		/// <param name="Creed">The faction name they now hold with.</param>
		/// <param name="Channel">Which channel turned them, which picks the words in both
		/// registers.</param>
		public static bool Convert(KingdomSystem System, Zone Z, GameObject Settler, string Creed, ConversionChannel Channel)
		{
			if (!Enabled || System == null || !System.Founded || Settler == null || string.IsNullOrEmpty(Creed))
			{
				return false;
			}
			string was = Settler.GetStringProperty(KingdomCreed.CreedProperty);
			if (was == Creed)
			{
				return false;
			}
			string roll = RollNameOf(Settler);
			string named = string.IsNullOrEmpty(roll) ? Settler.ShortDisplayName : roll;
			int hostility = KingdomCreed.HostilityBetween(was, Creed);
			// The existing surfaces, in the only order that keeps the tally honest: the old creed
			// is read off the settler by Forget, so it must go before Record overwrites it.
			KingdomCreed.Forget(System, Settler);
			KingdomCreed.Record(System, Settler, Creed);
			if (roll != null)
			{
				System.ConversionShared.Remove(roll);
				System.ConversionToward.Remove(roll);
				System.ConversionResented.Remove(roll);
			}
			string creedName = KingdomCreed.CreedName(Creed);
			string telling = KingdomConversionRules.ConversionTelling(Channel, named, creedName);
			if (KingdomConversionRules.Contested(hostility))
			{
				KingdomChronicle.RecordDisputed(System, telling, KingdomConversionRules.ConversionRumour(Channel, named, creedName));
			}
			else
			{
				KingdomChronicle.Record(System, telling);
			}
			System.Ledger.Note("{{G|" + KingdomConversionRules.ConversionNote(named, creedName) + "}}");
			KingdomLog.Log("conversion: " + named + " " + (string.IsNullOrEmpty(was) ? "(none)" : was) + " -> " + Creed + " via " + Channel + " hostility=" + hostility);
			return true;
		}

		/// <summary>The conversion line <c>kingdom:dump</c> appends for the zone the founder is
		/// standing in: who is being pulled where, how far along they are, and who is spending a
		/// grace under a creed they resent.</summary>
		public static string DumpLine(KingdomSystem System, Zone Z)
		{
			if (System == null || Z == null)
			{
				return "";
			}
			List<string> pulled = new List<string>();
			foreach (KeyValuePair<string, int> entry in System.ConversionShared)
			{
				string toward;
				System.ConversionToward.TryGetValue(entry.Key, out toward);
				pulled.Add(entry.Key + "->" + (toward ?? "-") + " " + entry.Value + "/" + KingdomConversionRules.SharedLivingForConversion);
			}
			List<string> leaving = new List<string>();
			foreach (KeyValuePair<string, int> entry in System.ConversionResented)
			{
				leaving.Add(entry.Key + " (" + entry.Value + "/" + KingdomConversionRules.ResentedPasses + ")");
			}
			if (pulled.Count == 0 && leaving.Count == 0)
			{
				return "";
			}
			string line = "\nConversion: " + ((pulled.Count == 0) ? "nobody being pulled" : string.Join(", ", pulled));
			if (leaving.Count > 0)
			{
				line += "  resenting a creed: " + string.Join(", ", leaving);
			}
			return line;
		}

		// --- Osmosis ----------------------------------------------------------------------

		private static void Osmosis(KingdomSystem System, Zone Z, GameObject Resident, Dictionary<string, List<GameObject>> Roofs)
		{
			string roll = RollNameOf(Resident);
			if (roll == null)
			{
				// Somebody the roll does not carry: a founding citizen, or a person the settlement
				// never named. Progress is keyed to the roll, so an unnamed resident never enters
				// it, and nothing happens to them. Exactly the rule Addendum 4b's grace uses, and
				// for the same reason: staying as they were is the safe answer to a question the
				// registers cannot record.
				return;
			}
			string plotId = Resident.GetStringProperty(KingdomLodging.HomePlotIdProperty);
			List<GameObject> household;
			if (string.IsNullOrEmpty(plotId) || !Roofs.TryGetValue(plotId, out household) || household.Count < 2)
			{
				// Nobody to be pulled by. A settler sleeping in the open, and a settler alone under
				// their own roof, both simply hold what they held.
				return;
			}
			string creed = Resident.GetStringProperty(KingdomCreed.CreedProperty);
			string majority = KingdomConversionRules.HouseholdMajority(CreedCounts(household), household.Count);
			if (string.IsNullOrEmpty(majority) || majority == creed)
			{
				return;
			}
			int points = KingdomConversionRules.SharedLivingPerPass(
				KingdomLodging.QuartersOf(Z, Resident),
				KingdomCreed.HostilityBetween(creed, majority));
			if (points <= 0)
			{
				return;
			}
			SetProgress(System, roll, KingdomConversionRules.Advance(ProgressOf(System, roll), majority, points));
			MaybeConvert(System, Z, Resident, roll, ConversionChannel.Osmosis);
		}

		private static void MaybeConvert(KingdomSystem System, Zone Z, GameObject Resident, string Roll, ConversionChannel Channel)
		{
			ConversionProgress progress = ProgressOf(System, Roll);
			if (!progress.Any || !KingdomConversionRules.AtMilestone(progress.Shared))
			{
				return;
			}
			if (!KingdomConversionRules.Converts(KingdomChronicle.SettlementId(System.KingdomFactionName), Channel, Roll, progress.Shared))
			{
				return;
			}
			Convert(System, Z, Resident, progress.Creed, Channel);
		}

		// --- The exit ---------------------------------------------------------------------

		// One attended pass of grace, and never anything else: this runs from the settlement pass
		// and from nowhere else, so a founder who is away cannot spend it. Pressure is re-derived
		// here every pass rather than remembered, so taking the pressure off takes it off.
		private static void Pressure(KingdomSystem System, Zone Z, GameObject Resident)
		{
			string roll = RollNameOf(Resident);
			if (roll == null)
			{
				return;
			}
			string pressing = ResentedPressure(System, Z, Resident);
			if (pressing == null)
			{
				// Nothing is being imposed on them, or nothing they mind. Forgetting the count
				// rather than banking it is the same ruling housing makes: the founder is being
				// asked to act on THIS pressure, and if it comes back they get the whole grace
				// again.
				System.ConversionResented.Remove(roll);
				return;
			}
			int resented;
			if (!System.ConversionResented.TryGetValue(roll, out resented))
			{
				resented = KingdomConversionRules.NoResentment;
			}
			bool announce = resented < 0;
			resented = KingdomConversionRules.ResentmentAfterPass(resented);
			System.ConversionResented[roll] = resented;
			if (announce)
			{
				Announce(System, roll, pressing);
			}
			if (!KingdomConversionRules.ResentmentRunOut(resented))
			{
				return;
			}
			string leaving = KingdomConversionRules.LeavingLine(roll);
			System.Ledger.Note("{{r|" + leaving + "}}");
			if (KingdomGrowth.Emigrate(System, Z, null, Resident, KingdomConversionRules.DepartureCause))
			{
				System.ConversionResented.Remove(roll);
				return;
			}
			// The settlement would not let them go -- they are the last of the loyal core, or the
			// emigration machinery could not take them. The grace stays spent and is tried again
			// on the next attended pass rather than being reset, so nothing is lost and nobody is
			// told twice that they are going.
		}

		// The first source naming a creed this settler resents, or null. First rather than worst
		// on purpose: a second grievance does not make anybody leave twice, and the founder is
		// owed one name to act on rather than a list.
		private static string ResentedPressure(KingdomSystem System, Zone Z, GameObject Resident)
		{
			string creed = Resident.GetStringProperty(KingdomCreed.CreedProperty);
			if (Resents(creed, System.DeclaredCreed))
			{
				return System.DeclaredCreed;
			}
			for (int i = 0; i < Sources.Count; i++)
			{
				string pressing = null;
				// Third-party sources are untrusted (STANDARDS 9): one that throws disables itself
				// for the pass and is logged, and never takes the settlement pass down with it.
				KingdomSystem.Guard("conversion pressure source", delegate
				{
					pressing = Sources[i].PressingCreed(System, Z, Resident);
				});
				if (Resents(creed, pressing))
				{
					return pressing;
				}
			}
			return null;
		}

		private static bool Resents(string Creed, string Pressing)
		{
			return !string.IsNullOrEmpty(Pressing)
				&& KingdomConversionRules.Resents(KingdomCreed.HostilityBetween(Creed, Pressing));
		}

		private static void BeginResentment(KingdomSystem System, string Roll, string Pressing)
		{
			if (System.ConversionResented.ContainsKey(Roll))
			{
				return;
			}
			System.ConversionResented[Roll] = KingdomConversionRules.ResentmentAfterPass(KingdomConversionRules.NoResentment);
			Announce(System, Roll, Pressing);
		}

		// STANDARDS 7b, said once and where the founder will see it: the map entry IS the
		// announce flag, so a settler already counting cannot be announced a second time, and one
		// whose pressure lifted and returned is announced afresh.
		private static void Announce(KingdomSystem System, string Roll, string Pressing)
		{
			string creedName = KingdomCreed.CreedName(Pressing);
			KingdomChronicle.Record(System, KingdomConversionRules.PressureTelling(Roll, creedName));
			System.Ledger.Note("{{r|" + KingdomConversionRules.PressureNote(Roll, creedName) + "}}");
		}

		// Names that have left the roll are names nothing will ever pull at again. Pruned so a
		// departed settler's progress cannot be inherited by a later settler of the same name, and
		// so both maps stay the size of the city rather than of its history.
		private static void ForgetDeparted(KingdomSystem System)
		{
			Prune(System.ConversionShared, System.RosterNames);
			Prune(System.ConversionResented, System.RosterNames);
			List<string> stale = null;
			foreach (KeyValuePair<string, string> entry in System.ConversionToward)
			{
				if (!System.ConversionShared.ContainsKey(entry.Key))
				{
					if (stale == null)
					{
						stale = new List<string>();
					}
					stale.Add(entry.Key);
				}
			}
			if (stale == null)
			{
				return;
			}
			for (int i = 0; i < stale.Count; i++)
			{
				System.ConversionToward.Remove(stale[i]);
			}
		}

		private static void Prune(Dictionary<string, int> Map, List<string> Roll)
		{
			if (Map.Count == 0)
			{
				return;
			}
			List<string> gone = null;
			foreach (KeyValuePair<string, int> entry in Map)
			{
				if (!Roll.Contains(entry.Key))
				{
					if (gone == null)
					{
						gone = new List<string>();
					}
					gone.Add(entry.Key);
				}
			}
			if (gone == null)
			{
				return;
			}
			for (int i = 0; i < gone.Count; i++)
			{
				Map.Remove(gone[i]);
			}
		}

		// --- Facts about people, and the two maps that remember them ----------------------

		private static ConversionProgress ProgressOf(KingdomSystem System, string Roll)
		{
			string toward;
			int shared;
			if (!System.ConversionToward.TryGetValue(Roll, out toward) || !System.ConversionShared.TryGetValue(Roll, out shared))
			{
				return ConversionProgress.None;
			}
			return new ConversionProgress(toward, shared);
		}

		private static void SetProgress(KingdomSystem System, string Roll, ConversionProgress Progress)
		{
			if (!Progress.Any)
			{
				System.ConversionShared.Remove(Roll);
				System.ConversionToward.Remove(Roll);
				return;
			}
			System.ConversionShared[Roll] = Progress.Shared;
			System.ConversionToward[Roll] = Progress.Creed;
		}

		private static Dictionary<string, List<GameObject>> Households(List<GameObject> Residents)
		{
			Dictionary<string, List<GameObject>> households = new Dictionary<string, List<GameObject>>();
			for (int i = 0; i < Residents.Count; i++)
			{
				string plotId = Residents[i].GetStringProperty(KingdomLodging.HomePlotIdProperty);
				if (string.IsNullOrEmpty(plotId))
				{
					continue;
				}
				List<GameObject> under;
				if (!households.TryGetValue(plotId, out under))
				{
					under = new List<GameObject>();
					households[plotId] = under;
				}
				under.Add(Residents[i]);
			}
			return households;
		}

		private static Dictionary<string, int> CreedCounts(List<GameObject> People)
		{
			Dictionary<string, int> counts = new Dictionary<string, int>();
			for (int i = 0; i < People.Count; i++)
			{
				string creed = People[i].GetStringProperty(KingdomCreed.CreedProperty);
				if (string.IsNullOrEmpty(creed))
				{
					continue;
				}
				int held;
				counts.TryGetValue(creed, out held);
				counts[creed] = held + 1;
			}
			return counts;
		}

		private static List<GameObject> ResidentsIn(Zone Z)
		{
			List<GameObject> list = new List<GameObject>();
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1)
				{
					list.Add(item);
				}
			}
			return list;
		}

		// The name the roll carries this person under, which is the key both maps are filed by and
		// the name the registers will write. Null for anybody the roll does not carry.
		private static string RollNameOf(GameObject Resident)
		{
			string name = (Resident == null) ? null : Resident.GetStringProperty("KingdomName");
			return string.IsNullOrEmpty(name) ? null : name;
		}
	}
}
