using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Who sleeps where. The engine-coupled shell for cohabitation (Addendum 4): reads the
	/// residents and the housing standing in a claimed zone, assigns each unhoused resident to a
	/// specific home, and keeps that assignment stable across passes unless something about the
	/// city changed. <see cref="KingdomLodgingRules"/> owns every decision that has one right
	/// answer given the facts; this file only gathers the facts (real settlers, real buildings,
	/// the engine's own creed feelings) and writes down what got decided.
	/// <para>
	/// <b>One vocabulary.</b> What a resident needs, prefers and refuses is
	/// <c>KingdomQol.ProfileOf</c> &mdash; derived from vanilla truth first (a robot needs charge
	/// whether or not anybody authored it) and then refined by the blueprint's own
	/// <c>r_TAF_*</c> tags. What a home offers is <c>KingdomQol.OfferOf</c>, which is the design's
	/// declared <c>Provides</c> plus what its roof gives. This file reads no tag off an object
	/// itself: there is one vocabulary and one place it is assembled.
	/// </para>
	/// <para>
	/// <b>Where a design lives.</b> Storing "who lives where" needs an identity for the specific
	/// building, not just its design key &mdash; a settlement can raise two timber huts. The one
	/// identity that survives an in-place upgrade is the plot's own
	/// <c>KingdomPlots.PlotIdProperty</c>, so a resident's assignment is stored as that plot id
	/// rather than as a reference to the object itself. A resident whose plot id no longer
	/// resolves is exactly a resident whose home is gone &mdash; reassigned honestly, not
	/// silently kept pointing at nothing.
	/// </para>
	/// <para>
	/// <b>Housing binds (Addendum 4b).</b> A settler joins only if a home exists they would take
	/// (<see cref="WouldTakeArrival"/>, called by the arrival loop). A settler who loses every
	/// acceptable home is named once, given <c>KingdomLodgingRules.GracePasses</c> ATTENDED
	/// passes for the founder to act, and then leaves through <c>KingdomGrowth.Emigrate</c>, the
	/// machinery the settlement already has. The grace is counted in attended passes and in
	/// nothing else: no clock is read here, so a founder who is away spends nobody's grace.
	/// Nothing is a meter and nothing decays; the whole of the state is one small per-city map of
	/// name to passes elapsed, and it empties the moment somebody is housed.
	/// </para>
	/// </summary>
	public static class KingdomLodging
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionLodging") != "No";

		/// <summary>The plot id (<see cref="KingdomPlots.PlotIdProperty"/>) of the resident's
		/// assigned home, stamped on the resident. Absent means unhoused &mdash; the ordinary
		/// state for a settler this pass has not reached yet, not a fault.</summary>
		public const string HomePlotIdProperty = "KingdomLodgingPlotId";

		/// <summary>STANDARDS 7b's once-only announce flag, scoped to the resident: set the first
		/// pass a settler has no acceptable home, cleared the pass they are finally housed, so the
		/// chronicle says it once per spell of going without rather than once per visit.</summary>
		public const string UnhousedAnnouncedProperty = "KingdomLodgingUnhousedAnnounced";

		/// <summary>
		/// The kingdom's one attended pass over one claimed zone's lodging: keeps every valid
		/// standing assignment untouched, clears any pointing at a home that is no longer there,
		/// assigns everyone left over, and spends one pass of grace on everyone it could not
		/// house. Call once per zone activation, after <c>KingdomPlot.OnSettlementPass</c> so a
		/// building finished raising this very pass is already a candidate.
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z)
		{
			Settle(System, Z, SpendGrace: true);
		}

		/// <summary>
		/// Addendum 4b's arrival gate, and the whole of it: whether some home standing here would
		/// take this newcomer &mdash; meets their Needs, has a bed free, and holds nobody either
		/// of them refuses. Assignment-level, not a bed tally, because a settlement with ten empty
		/// beds and no charging post genuinely has no room for a robot.
		/// <para>
		/// Re-settles the zone first, so an arrival earlier in this same visit already holds the
		/// bed it was going to hold and the second arrival is judged against the truth.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone the arrival would walk into.</param>
		/// <param name="Newcomer">The settler themselves, created but not yet placed.</param>
		/// <param name="Reason">Why nobody would take them, for the founder's line.</param>
		public static bool WouldTakeArrival(KingdomSystem System, Zone Z, GameObject Newcomer, out KingdomLodgingRules.UnhousedReason Reason)
		{
			Reason = KingdomLodgingRules.UnhousedReason.Housed;
			if (!Enabled)
			{
				return true;
			}
			if (System == null || Z == null)
			{
				return true;
			}
			// Settling first, and without spending anybody's grace: this is a question, not a
			// pass. A founder is never charged a pass of somebody else's grace for asking whether
			// a stranger could stay.
			Dictionary<string, List<GameObject>> occupancy = Settle(System, Z, SpendGrace: false);
			if (occupancy == null)
			{
				return true;
			}
			QolProfile profile = KingdomQol.ProfileOf(Newcomer);
			List<string> needs = new List<string>(profile.Needs);
			List<string> refuses = new List<string>(profile.Refuses);
			List<string> selfTags = SelfTagsOf(profile);
			string creed = (Newcomer == null) ? null : Newcomer.GetStringProperty(KingdomCreed.CreedProperty);
			List<GameObject> homes = HousingIn(Z);
			List<KingdomLodgingRules.ArrivalHome> offers = new List<KingdomLodgingRules.ArrivalHome>();
			for (int i = 0; i < homes.Count; i++)
			{
				string plotId = homes[i].GetStringProperty(KingdomPlots.PlotIdProperty);
				KingdomRules.BuildEntry entry;
				if (string.IsNullOrEmpty(plotId) || !TryGetBuiltEntry(homes[i], out entry))
				{
					continue;
				}
				int capacity = RoofCapacity(entry);
				if (capacity <= 0)
				{
					continue;
				}
				List<GameObject> occupants;
				occupancy.TryGetValue(plotId, out occupants);
				offers.Add(new KingdomLodgingRules.ArrivalHome(
					new List<string>(KingdomQol.OfferOf(entry.Key)),
					capacity,
					(occupants == null) ? 0 : occupants.Count,
					occupants != null && AnyOccupantConflicts(refuses, selfTags, creed, occupants)));
			}
			return KingdomLodgingRules.AnyWouldTake(offers, needs, out Reason);
		}

		/// <summary>The design key of the home this resident sleeps in, for a caller that wants to
		/// ask the vocabulary about their quarters &mdash; the ceremony's Prefers shade does.
		/// Null for a resident with no home, which is a resident whose Prefers are simply their
		/// default.</summary>
		public static string HomeDesignKeyOf(Zone Z, GameObject Resident)
		{
			GameObject home = HomeOf(Z, Resident);
			KingdomRules.BuildEntry entry;
			return (home != null && TryGetBuiltEntry(home, out entry)) ? entry.Key : null;
		}

		// --- The pass itself -------------------------------------------------------------

		// Returns the occupancy map it settled on, or null when the module has nothing to do here.
		// SpendGrace is false for the arrival gate, which asks the same question without charging
		// anybody a pass of the grace Addendum 4b gives them.
		private static Dictionary<string, List<GameObject>> Settle(KingdomSystem System, Zone Z, bool SpendGrace)
		{
			if (!Enabled || System == null || !System.Founded || Z == null)
			{
				return null;
			}
			List<GameObject> residents = ResidentsIn(Z);
			if (residents.Count == 0)
			{
				// Nobody to settle, and an EMPTY map rather than none at all: a settlement with no
				// citizens standing in it still has housing, or has none, and the arrival gate has
				// to be able to tell those two apart. A camp with no roof yet takes nobody, which
				// is the rule that shipped before this module and is unchanged by it.
				return new Dictionary<string, List<GameObject>>();
			}
			List<GameObject> homes = HousingIn(Z);
			Dictionary<string, GameObject> homeByPlot = new Dictionary<string, GameObject>();
			for (int i = 0; i < homes.Count; i++)
			{
				string plotId = homes[i].GetStringProperty(KingdomPlots.PlotIdProperty);
				if (!string.IsNullOrEmpty(plotId) && !homeByPlot.ContainsKey(plotId))
				{
					homeByPlot[plotId] = homes[i];
				}
			}
			Dictionary<string, List<GameObject>> occupancy = new Dictionary<string, List<GameObject>>();
			List<GameObject> unassigned = new List<GameObject>();
			for (int i = 0; i < residents.Count; i++)
			{
				GameObject resident = residents[i];
				string plotId = resident.GetStringProperty(HomePlotIdProperty);
				if (!string.IsNullOrEmpty(plotId) && homeByPlot.ContainsKey(plotId))
				{
					AddOccupant(occupancy, plotId, resident);
					continue;
				}
				if (!string.IsNullOrEmpty(plotId))
				{
					// The plot they were assigned to is gone (struck, or never built after a
					// save from an older version). Something changed, so the stale pointer is
					// cleared rather than left dangling, and they are reconsidered below exactly
					// like a resident who never had a home.
					resident.SetStringProperty(HomePlotIdProperty, null);
				}
				unassigned.Add(resident);
			}
			for (int i = 0; i < unassigned.Count; i++)
			{
				AssignOne(System, Z, unassigned[i], homes, occupancy, SpendGrace);
			}
			if (SpendGrace)
			{
				ForgetDeparted(System);
			}
			return occupancy;
		}

		private static void AssignOne(KingdomSystem System, Zone Z, GameObject Resident, List<GameObject> Homes, Dictionary<string, List<GameObject>> Occupancy, bool SpendGrace)
		{
			QolProfile profile = KingdomQol.ProfileOf(Resident);
			List<string> needs = new List<string>(profile.Needs);
			List<string> refuses = new List<string>(profile.Refuses);
			List<string> selfTags = SelfTagsOf(profile);
			string creed = Resident.GetStringProperty(KingdomCreed.CreedProperty);

			bool anyRoofAtAll = Homes.Count > 0;
			bool anyMeetsNeeds = false;
			bool anyHasCapacity = false;
			bool anyWithoutRefusal = false;
			List<KingdomLodgingRules.LodgingCandidate> eligible = new List<KingdomLodgingRules.LodgingCandidate>();
			List<GameObject> eligibleHomes = new List<GameObject>();

			for (int i = 0; i < Homes.Count; i++)
			{
				GameObject home = Homes[i];
				string plotId = home.GetStringProperty(KingdomPlots.PlotIdProperty);
				if (string.IsNullOrEmpty(plotId))
				{
					continue;
				}
				KingdomRules.BuildEntry entry;
				if (!TryGetBuiltEntry(home, out entry))
				{
					continue;
				}
				int capacity = RoofCapacity(entry);
				if (capacity <= 0)
				{
					continue;
				}
				List<string> provides = new List<string>(KingdomQol.OfferOf(entry.Key));
				if (!KingdomLodgingRules.MeetsNeeds(needs, provides))
				{
					continue;
				}
				anyMeetsNeeds = true;
				List<GameObject> occupants;
				Occupancy.TryGetValue(plotId, out occupants);
				int occupantCount = (occupants == null) ? 0 : occupants.Count;
				if (!KingdomLodgingRules.HasFreeBed(capacity, occupantCount))
				{
					continue;
				}
				anyHasCapacity = true;
				if (occupants != null && AnyOccupantConflicts(refuses, selfTags, creed, occupants))
				{
					continue;
				}
				anyWithoutRefusal = true;
				eligible.Add(new KingdomLodgingRules.LodgingCandidate(plotId, capacity, occupantCount));
				eligibleHomes.Add(home);
			}

			int chosen = KingdomLodgingRules.ChooseIndex(eligible);
			string residentName = NameOf(Resident);
			if (chosen < 0)
			{
				KingdomLodgingRules.UnhousedReason reason = KingdomLodgingRules.Diagnose(anyRoofAtAll, anyMeetsNeeds, anyHasCapacity, anyWithoutRefusal);
				AnnounceUnhoused(System, Resident, residentName, reason);
				if (SpendGrace)
				{
					SpendOnePassOfGrace(System, Z, Resident, RollNameOf(Resident));
				}
				return;
			}
			string winningPlotId = eligible[chosen].PlotId;
			Resident.SetStringProperty(HomePlotIdProperty, winningPlotId);
			bool wasUnhoused = Resident.GetIntProperty(UnhousedAnnouncedProperty) == 1;
			if (wasUnhoused)
			{
				Resident.SetIntProperty(UnhousedAnnouncedProperty, 0);
			}
			// Housed is housed: the grace a settler was spending is not banked, halved or
			// remembered. It is forgotten, and if they lose their home again they get the whole of
			// it back, because the founder is being asked to act on THIS loss.
			string housedRoll = RollNameOf(Resident);
			if (!string.IsNullOrEmpty(housedRoll))
			{
				System.LodgingGrace.Remove(housedRoll);
			}
			AddOccupant(Occupancy, winningPlotId, Resident);
			if (wasUnhoused)
			{
				KingdomRules.BuildEntry winEntry;
				TryGetBuiltEntry(eligibleHomes[chosen], out winEntry);
				string matched = KingdomLodgingRules.MatchedTag(needs, (winEntry == null) ? null : new List<string>(KingdomQol.OfferOf(winEntry.Key)));
				string line = residentName + " found shelter: " + KingdomLodgingRules.HomeSuffix((winEntry != null) ? winEntry.Name : null, matched) + ".";
				KingdomChronicle.Record(System, line);
			}
		}

		// --- Addendum 4b: the grace, and the leaving ---------------------------------------

		// One attended pass of grace, and never anything else: this runs from the settlement pass
		// and from nowhere else, so a founder who is away cannot spend it. When it runs out the
		// settler leaves through the emigration machinery the settlement already has, chronicled
		// by name and cause in both registers by that machinery.
		private static void SpendOnePassOfGrace(KingdomSystem System, Zone Z, GameObject Resident, string ResidentName)
		{
			if (string.IsNullOrEmpty(ResidentName))
			{
				// Somebody the roll does not carry: a founding citizen, or a person the settlement
				// never named. The grace is keyed to the roll, so an unnamed resident simply never
				// enters it and never leaves for want of a roof. Staying is the safe answer to a
				// question the registers cannot record, and it is the one taken here.
				return;
			}
			int grace;
			if (!System.LodgingGrace.TryGetValue(ResidentName, out grace))
			{
				grace = KingdomLodgingRules.NoGrace;
			}
			grace = KingdomLodgingRules.GraceAfterPass(grace);
			System.LodgingGrace[ResidentName] = grace;
			if (!KingdomLodgingRules.GraceRunOut(grace))
			{
				return;
			}
			string leaving = KingdomLodgingRules.LeavingLine(ResidentName);
			System.Ledger.Note("{{r|" + leaving + "}}");
			if (KingdomGrowth.Emigrate(System, Z, null, Resident, KingdomLodgingRules.DepartureCause))
			{
				System.LodgingGrace.Remove(ResidentName);
				return;
			}
			// The settlement would not let them go &mdash; they are the last of the loyal core, or
			// the emigration machinery could not take them. The grace stays spent and is tried
			// again on the next attended pass rather than being reset, so nothing is lost and
			// nobody is told twice that they are going.
		}

		// Names that have left the roll are names nothing will ever house again. Pruned here so a
		// dead or departed settler's grace cannot be inherited by a later settler of the same
		// name, and so the map stays the size of the city rather than of its history.
		private static void ForgetDeparted(KingdomSystem System)
		{
			if (System.LodgingGrace.Count == 0)
			{
				return;
			}
			List<string> gone = null;
			foreach (KeyValuePair<string, int> entry in System.LodgingGrace)
			{
				if (!System.RosterNames.Contains(entry.Key))
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
				System.LodgingGrace.Remove(gone[i]);
			}
		}

		// --- Facts about people and places ------------------------------------------------

		private static List<string> SelfTagsOf(QolProfile Profile)
		{
			List<string> tags = new List<string>();
			if (Profile == null)
			{
				return tags;
			}
			tags.AddRange(Profile.Needs);
			tags.AddRange(Profile.Prefers);
			return tags;
		}

		private static bool AnyOccupantConflicts(List<string> Refuses, List<string> SelfTags, string Creed, List<GameObject> Occupants)
		{
			for (int i = 0; i < Occupants.Count; i++)
			{
				GameObject occupant = Occupants[i];
				string occupantCreed = occupant.GetStringProperty(KingdomCreed.CreedProperty);
				// Which creed feelings actually break a household is the vocabulary's own ruling
				// (KingdomQolRules.CohabitHostility): the game's flat -100 fault lines do, and the
				// standing -50 several factions hold toward everyone they have not troubled to name
				// does not, or half a mixed settlement could never share a wall. Everything milder
				// is texture, and texture is what Refuses tags are for. Anything below the
				// threshold is handed on as no hostility at all, so the pure rule goes on asking
				// its own question -- "is there enmity here" -- and one answer decides it.
				int felt = KingdomCreed.HostilityBetween(Creed, occupantCreed);
				int hostility = (felt >= KingdomQolRules.CohabitHostility) ? felt : 0;
				QolProfile theirs = KingdomQol.ProfileOf(occupant);
				List<string> occupantSelfTags = SelfTagsOf(theirs);
				List<string> occupantRefuses = new List<string>(theirs.Refuses);
				if (KingdomLodgingRules.Conflicts(Refuses, SelfTags, occupantRefuses, occupantSelfTags, hostility))
				{
					return true;
				}
			}
			return false;
		}

		private static void AnnounceUnhoused(KingdomSystem System, GameObject Resident, string ResidentName, KingdomLodgingRules.UnhousedReason Reason)
		{
			if (Resident.GetIntProperty(UnhousedAnnouncedProperty) == 1)
			{
				return;
			}
			Resident.SetIntProperty(UnhousedAnnouncedProperty, 1);
			string line = KingdomLodgingRules.UnhousedLine(ResidentName, Reason);
			KingdomChronicle.Record(System, line);
			System.Ledger.Note("{{r|" + line + "}}");
		}

		private static void AddOccupant(Dictionary<string, List<GameObject>> Occupancy, string PlotId, GameObject Resident)
		{
			List<GameObject> list;
			if (!Occupancy.TryGetValue(PlotId, out list))
			{
				list = new List<GameObject>();
				Occupancy[PlotId] = list;
			}
			list.Add(Resident);
		}

		// The name the roll carries this person under, which is the key the grace is filed by and
		// the name the registers will write when they leave. Null for anybody the roll does not
		// carry.
		private static string RollNameOf(GameObject Resident)
		{
			string name = (Resident == null) ? null : Resident.GetStringProperty("KingdomName");
			return string.IsNullOrEmpty(name) ? null : name;
		}

		private static string NameOf(GameObject Resident)
		{
			string name = (Resident == null) ? null : Resident.GetStringProperty("KingdomName");
			if (!string.IsNullOrEmpty(name))
			{
				return name;
			}
			return (Resident == null) ? "" : Resident.ShortDisplayName;
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

		private static List<GameObject> HousingIn(Zone Z)
		{
			List<GameObject> list = new List<GameObject>();
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty(KingdomUpgrade.BuiltProperty) != 1)
				{
					continue;
				}
				if (string.IsNullOrEmpty(item.GetStringProperty(KingdomPlots.PlotIdProperty)))
				{
					continue;
				}
				KingdomRules.BuildEntry entry;
				if (!TryGetBuiltEntry(item, out entry) || RoofCapacity(entry) <= 0)
				{
					continue;
				}
				list.Add(item);
			}
			return list;
		}

		private static bool TryGetBuiltEntry(GameObject Work, out KingdomRules.BuildEntry Entry)
		{
			string key = KingdomUpgrade.DesignKeyOf(Work);
			if (string.IsNullOrEmpty(key))
			{
				Entry = null;
				return false;
			}
			return KingdomData.TryGetBuilding(key, out Entry);
		}

		private static int RoofCapacity(KingdomRules.BuildEntry Entry)
		{
			if (Entry == null)
			{
				return 0;
			}
			List<KindAmount> carries;
			KingdomCatalogueRules.TryParseTally(Entry.Carries, out carries, out _);
			return KingdomCatalogueRules.AmountOf(carries, KingdomCatalogueRules.SupportRoof);
		}

		private static GameObject FindResidentByName(Zone Z, string ResidentName)
		{
			if (Z == null || string.IsNullOrEmpty(ResidentName))
			{
				return null;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1 && item.GetStringProperty("KingdomName") == ResidentName)
				{
					return item;
				}
			}
			return null;
		}

		private static GameObject HomeOf(Zone Z, GameObject Resident)
		{
			if (Z == null || Resident == null)
			{
				return null;
			}
			string plotId = Resident.GetStringProperty(HomePlotIdProperty);
			if (string.IsNullOrEmpty(plotId))
			{
				return null;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetStringProperty(KingdomPlots.PlotIdProperty) == plotId && item.GetIntProperty(KingdomUpgrade.BuiltProperty) == 1)
				{
					return item;
				}
			}
			return null;
		}

		/// <summary>The suffix the roll of settlers appends to a resident's own line: where they
		/// sleep, or that they do not yet. Empty when this resident is not standing in
		/// <paramref name="Z"/> right now &mdash; the roll already reads only the zone the founder
		/// is standing in for the same reason the yard-trades lines do.</summary>
		public static string RollLine(Zone Z, string ResidentName)
		{
			GameObject resident = FindResidentByName(Z, ResidentName);
			if (resident == null)
			{
				return "";
			}
			GameObject home = HomeOf(Z, resident);
			if (home == null)
			{
				return (resident.GetIntProperty(UnhousedAnnouncedProperty) == 1) ? " {{r|(sleeps in the open)}}" : "";
			}
			KingdomRules.BuildEntry entry;
			TryGetBuiltEntry(home, out entry);
			List<string> needs = new List<string>(KingdomQol.ProfileOf(resident).Needs);
			string matched = KingdomLodgingRules.MatchedTag(needs, (entry == null) ? null : new List<string>(KingdomQol.OfferOf(entry.Key)));
			return " {{K|(" + KingdomLodgingRules.HomeSuffix((entry != null) ? entry.Name : null, matched) + ")}}";
		}

		/// <summary>The lodging line <c>kingdom:dump</c> appends for the zone the founder is
		/// standing in: how many of the residents present are housed, who is not, and how much of
		/// their grace (Addendum 4b) they have spent.</summary>
		public static string DumpLine(KingdomSystem System, Zone Z)
		{
			if (Z == null)
			{
				return "";
			}
			List<GameObject> residents = ResidentsIn(Z);
			if (residents.Count == 0)
			{
				return "";
			}
			int housed = 0;
			List<string> sleepingOpen = new List<string>();
			for (int i = 0; i < residents.Count; i++)
			{
				GameObject resident = residents[i];
				if (!string.IsNullOrEmpty(resident.GetStringProperty(HomePlotIdProperty)))
				{
					housed++;
					continue;
				}
				string name = NameOf(resident);
				string roll = RollNameOf(resident);
				int grace;
				if (System != null && roll != null && System.LodgingGrace.TryGetValue(roll, out grace))
				{
					name += " (grace " + grace + "/" + KingdomLodgingRules.GracePasses + ")";
				}
				sleepingOpen.Add(name);
			}
			string line = "\nLodging: " + housed + "/" + residents.Count + " housed";
			if (sleepingOpen.Count > 0)
			{
				line += "  sleeping in the open: " + string.Join(", ", sleepingOpen);
			}
			return line;
		}
	}
}
