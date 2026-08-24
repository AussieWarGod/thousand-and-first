using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

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
	/// declared <c>Provides</c> plus what its roof gives <em>on the ground it stands on</em>
	/// &mdash; the zone goes with the key at every call here, because an open plot in the deep
	/// offers shade and not sky. This file reads no tag off an object itself: there is one
	/// vocabulary and one place it is assembled.
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
	/// <b>Feelings scale with closeness (Addendum 4c).</b> How much of a quarrel a roof will hold
	/// is a property of the roof. This file derives every home's closeness rung from what the
	/// registry already declares &mdash; the beds in its <c>Carries</c> against the ground its tier
	/// stands on, from <c>KingdomPlots</c> &mdash; and lets a design's own <c>Closeness</c>
	/// attribute override that arithmetic where it reads the ground wrong. Nothing about closeness
	/// is serialized: it is registry data, recomputed from the merged catalogue every load, so a
	/// rebalance moves it for a house raised a year ago and a save carries none of it.
	/// </para>
	/// <para>
	/// <b>Housing binds (Addendum 4b), through the brink.</b> A settler joins only if a home
	/// exists they would take (<see cref="WouldTakeArrival"/>, called by the arrival loop). A
	/// settler who loses every acceptable home has reached an irreversible line, so the loss is
	/// RECORDED with the tick it happened (<see cref="KingdomBrink"/>) and nothing accrues past
	/// it; the word is PUSHED to the founder wherever they are, once, naming the arrest; the
	/// founder then has <c>KingdomLodgingRules.GraceDays</c> of WORLD TIME; and if that runs out
	/// with them still unroofed they leave through <c>KingdomGrowth.Emigrate</c>, the machinery
	/// the settlement already has, whether or not anybody was there to see it (Addendum 10(a)).
	/// The going is dated to the day the window actually ran out, not to the homecoming that
	/// found it. Nothing is a meter and nothing decays; the record lives on the settler themselves
	/// and is lifted &mdash; and unsaid &mdash; the moment somebody is housed.
	/// </para>
	/// <para>
	/// <b>Condemnation.</b> A house worn past
	/// <c>KingdomLodgingRules.CondemnedWearPercent</c> stops counting as a roof
	/// (<see cref="IsCondemned"/>). It is not cleared, unbuilt or moved &mdash; the protection
	/// law &mdash; and every point of the damage is mendable, so the founder arrests a
	/// condemnation by putting the roof back on. This is what gives a subsidence's ruin a real
	/// housing consequence, and it is why <see cref="RecordCondemnedRoofBrink"/> exists: when a
	/// slide wrecks an OCCUPIED home at a breakpoint days back, the people under it lost their
	/// roof on that day, and the brink is recorded with that day's tick so the announcement
	/// quotes the honest elapsed rather than the moment somebody finally walked in.
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

		// --- Addendum 4c: the closeness registry ------------------------------------------

		// A design's declared Closeness override, keyed by building Key exactly as the plot spec,
		// the zoning gate and the upgrade chain are (STANDARDS 6): a later file re-using a key owns
		// that design's rung, and re-declaring it WITHOUT the attribute correctly returns the design
		// to the derivation. Nothing here is serialized -- closeness is registry data, recomputed
		// from the merged catalogue on every load, so a save carries none of it and a rebalance
		// moves it for a house raised a year ago.
		private static readonly Dictionary<string, KingdomLodgingRules.Closeness> Declared = new Dictionary<string, KingdomLodgingRules.Closeness>();

		/// <summary>Forgets every declared <c>Closeness</c>. Called by the registry loader before it
		/// re-reads the XML streams.</summary>
		public static void ClearCloseness()
		{
			Declared.Clear();
		}

		/// <summary>
		/// Registers one entry's <c>Closeness</c> override as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the merged raw attribute;
		/// null or blank registers "measure me", which is every design content to be derived.
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="Closeness">Raw <c>Closeness</c> attribute: <c>Packed</c>, <c>Close</c>,
		/// <c>Roomed</c> or <c>Private</c>. A word this build does not know is logged and the design
		/// falls back to the derivation &mdash; hostile-input discipline, STANDARDS 9: a malformed
		/// attribute disables itself and never takes a design out of the catalogue.</param>
		public static void RegisterCloseness(string Key, string Closeness)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			Declared.Remove(Key);
			if (string.IsNullOrEmpty(Closeness) || Closeness.Trim().Length == 0)
			{
				return;
			}
			KingdomLodgingRules.Closeness quarters;
			if (!KingdomLodgingRules.TryParseCloseness(Closeness, out quarters))
			{
				KingdomLog.Log("KingdomBuildings: building " + Key + " declares Closeness=\"" + Closeness
					+ "\", which is none of " + string.Join(", ", KingdomLodgingRules.ClosenessNames)
					+ ". Measuring its beds against its footprint instead.");
				return;
			}
			Declared[Key] = quarters;
		}

		/// <summary>
		/// The kingdom's one attended pass over one claimed zone's lodging: keeps every valid
		/// standing assignment untouched, clears any pointing at a home that is no longer there,
		/// assigns everyone left over, and spends one pass of grace on everyone it could not
		/// house. Call once per zone activation, after <c>KingdomPlot.OnSettlementPass</c> so a
		/// building finished raising this very pass is already a candidate.
		/// </summary>
		public static void OnSettlementPass(KingdomSystem System, Zone Z)
		{
			Settle(System, Z, RunBrink: true);
		}

		/// <summary>
		/// Addendum 4b's arrival gate, and the whole of it: whether some home standing here would
		/// take this newcomer &mdash; meets their Needs, has a bed free, and holds nobody either
		/// of them refuses. Assignment-level, not a bed tally, because a settlement with ten empty
		/// beds and no charging post genuinely has no room for a robot.
		/// Reads standing assignments without changing them. The ordinary settlement pass owns
		/// assignment, brink, and announcement writes; admission is a pure observation until its
		/// lodging intent has published.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Z">The zone the arrival would walk into.</param>
		/// <param name="Newcomer">The settler themselves, created but not yet placed.</param>
		/// <param name="Reason">Why nobody would take them, for the founder's line.</param>
		public static bool WouldTakeArrival(KingdomSystem System, Zone Z, GameObject Newcomer, out KingdomLodgingRules.UnhousedReason Reason)
		{
			string ignored;
			return ObservePreparedArrival(System, Z, Newcomer, out Reason, out ignored);
		}

		/// <summary>Pure arrival decision over already-refreshed assignments. The returned hash
		/// freezes every semantic input consumed by the gate; this method writes no game state.</summary>
		internal static bool ObservePreparedArrival(KingdomSystem System, Zone Z,
			GameObject Newcomer, out KingdomLodgingRules.UnhousedReason Reason,
			out string ObservationHash)
		{
			return ObservePreparedArrival(System, Z, Newcomer, null, out Reason,
				out ObservationHash);
		}

		/// <summary>Pure arrival decision using an already-frozen creed when the newcomer has not
		/// yet published that creed onto their ordinary resident property.</summary>
		internal static bool ObservePreparedArrival(KingdomSystem System, Zone Z,
			GameObject Newcomer, string PlannedCreed,
			out KingdomLodgingRules.UnhousedReason Reason, out string ObservationHash)
		{
			Reason = KingdomLodgingRules.UnhousedReason.Housed;
			ObservationHash = null;
			string creed = PlannedCreed ?? ((Newcomer == null) ? null
				: Newcomer.GetStringProperty(KingdomCreed.CreedProperty));
			if (!Enabled || System == null || Z == null || !System.Founded)
			{
				ObservationHash = ArrivalObservationHash(delegate(BinaryWriter writer)
				{
					WriteObservationString(writer, "bypassed");
					WriteObservationString(writer, Z == null ? null : Z.ZoneID);
					WriteObservationString(writer, Newcomer == null ? null : Newcomer.ID);
					WriteObservationString(writer, creed);
				});
				return true;
			}
			Dictionary<string, List<GameObject>> occupancy = ProjectedOccupancy(Z);
			QolProfile profile = KingdomQol.ProfileOf(Newcomer);
			List<string> needs = new List<string>(profile.Needs);
			List<string> refuses = new List<string>(profile.Refuses);
			List<string> selfTags = SelfTagsOf(profile);
			List<GameObject> homes = HousingIn(Z);
			List<KingdomLodgingRules.ArrivalHome> offers = new List<KingdomLodgingRules.ArrivalHome>();
			List<string> homeEvidence = new List<string>();
			bool anyCondemned = false;
			for (int i = 0; i < homes.Count; i++)
			{
				GameObject home = homes[i];
				string plotId = home.GetStringProperty(KingdomPlots.PlotIdProperty);
				KingdomRules.BuildEntry entry;
				if (string.IsNullOrEmpty(plotId) || !TryGetBuiltEntry(home, out entry))
				{
					continue;
				}
				int capacity = RoofCapacity(entry);
				if (capacity <= 0)
				{
					continue;
				}
				// Counted rather than merely skipped: a settlement whose every roof has fallen in
				// must be told to MEND, not to commission housing it already built.
				bool condemned = IsCondemned(home);
				if (condemned)
				{
					anyCondemned = true;
					homeEvidence.Add(ArrivalObservationHash(delegate(BinaryWriter writer)
					{
						WriteObservationString(writer, home.ID);
						WriteObservationString(writer, home.Blueprint);
						WriteObservationString(writer, plotId);
						WriteObservationString(writer, entry.Key);
						writer.Write(capacity); writer.Write(true);
					}));
					continue;
				}
				List<GameObject> occupants;
				occupancy.TryGetValue(plotId, out occupants);
				List<string> occupantEvidence;
				KingdomLodgingRules.Closeness quarters = KingdomFaith.EducatedCloseness(
					Z, QuartersOf(entry), home);
				bool conflict = ObserveOccupantConflicts(refuses, selfTags, creed,
					occupants, quarters, out occupantEvidence);
				List<string> provides = new List<string>(KingdomQol.OfferOf(entry.Key, Z));
				offers.Add(new KingdomLodgingRules.ArrivalHome(
					provides, capacity, (occupants == null) ? 0 : occupants.Count, conflict));
				provides.Sort(StringComparer.Ordinal);
				occupantEvidence.Sort(StringComparer.Ordinal);
				homeEvidence.Add(ArrivalObservationHash(delegate(BinaryWriter writer)
				{
					WriteObservationString(writer, home.ID);
					WriteObservationString(writer, home.Blueprint);
					WriteObservationString(writer, plotId);
					WriteObservationString(writer, entry.Key);
					writer.Write(capacity); writer.Write(false); writer.Write((int)quarters);
					WriteObservationList(writer, provides);
					WriteObservationList(writer, occupantEvidence);
					writer.Write(conflict);
				}));
			}
			bool joined = KingdomLodgingRules.AnyWouldTake(offers, needs, out Reason,
				anyCondemned);
			KingdomLodgingRules.UnhousedReason frozenReason = Reason;
			needs.Sort(StringComparer.Ordinal); refuses.Sort(StringComparer.Ordinal);
			selfTags.Sort(StringComparer.Ordinal); homeEvidence.Sort(StringComparer.Ordinal);
			ObservationHash = ArrivalObservationHash(delegate(BinaryWriter writer)
			{
				WriteObservationString(writer, "prepared-arrival");
				WriteObservationString(writer, Z.ZoneID);
				WriteObservationString(writer, Newcomer == null ? null : Newcomer.ID);
				WriteObservationString(writer, Newcomer == null ? null : Newcomer.Blueprint);
				WriteObservationString(writer, creed);
				WriteObservationList(writer, needs); WriteObservationList(writer, refuses);
				WriteObservationList(writer, selfTags); WriteObservationList(writer, homeEvidence);
				writer.Write(anyCondemned); writer.Write(joined); writer.Write((int)frozenReason);
			});
			return joined;
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

		/// <summary>
		/// The closeness rung of the home this resident sleeps in &mdash; the design's declared
		/// <c>Closeness</c>, or the arithmetic on its beds against the ground its tier stands on.
		/// For a caller that needs to know how much of a difference a roof will hold: Addendum 5's
		/// osmosis scales on exactly this ladder, and it is the same ladder the cohabitation gate
		/// already reads, so the roof that will house a grudge and the roof that will cross one can
		/// never disagree.
		/// </summary>
		/// <param name="Z">The zone. Null reads as no home.</param>
		/// <param name="Resident">The resident. Null, and a resident with no assigned home, both
		/// read as <see cref="KingdomLodgingRules.Closeness.Packed"/> &mdash; the tightest rung,
		/// which is also the rung that converts nobody, so a missing answer can never accelerate
		/// anything.</param>
		public static KingdomLodgingRules.Closeness QuartersOf(Zone Z, GameObject Resident)
		{
			GameObject home = HomeOf(Z, Resident);
			KingdomRules.BuildEntry entry;
			return (home != null && TryGetBuiltEntry(home, out entry)) ? QuartersOf(entry) : KingdomLodgingRules.Closeness.Packed;
		}

		// --- The pass itself -------------------------------------------------------------

		// Returns the occupancy map it settled on, or null when the module has nothing to do here.
		// RunBrink is false for the arrival gate, which asks the same question without charging
		// anybody a pass of the grace Addendum 4b gives them.
		private static Dictionary<string, List<GameObject>> Settle(KingdomSystem System, Zone Z, bool RunBrink)
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
					// Whoever they were living beside, they are not living beside them now, so
					// the cohabitation clock osmosis reads restarts here rather than crediting a
					// household that has stopped existing.
					KingdomConversion.ForgetCohabitation(resident);
				}
				unassigned.Add(resident);
			}
			for (int i = 0; i < unassigned.Count; i++)
			{
				AssignOne(System, Z, unassigned[i], homes, occupancy, RunBrink);
			}
			return occupancy;
		}

		private static void AssignOne(KingdomSystem System, Zone Z, GameObject Resident, List<GameObject> Homes, Dictionary<string, List<GameObject>> Occupancy, bool RunBrink)
		{
			GameObject winningHome;
			KingdomLodgingRules.UnhousedReason reason;
			KingdomLodgingRules.Closeness roomiestRefused;
			List<string> needs;
			string winningPlotId = ChooseHome(Z, Resident, Homes, Occupancy,
				out winningHome, out reason, out roomiestRefused, out needs);
			string residentName = NameOf(Resident);
			if (winningPlotId == null)
			{
				AnnounceUnhoused(System, Resident, residentName, reason, roomiestRefused);
				if (RunBrink)
				{
					RunRoofBrink(System, Z, Resident, RollNameOf(Resident));
				}
				return;
			}
			Resident.SetStringProperty(HomePlotIdProperty, winningPlotId);
			// A new roof is a new household, so the cohabitation clock starts from tonight. They
			// do not inherit the days they spent under somebody else's roof, or outside.
			KingdomConversion.ForgetCohabitation(Resident);
			bool wasUnhoused = Resident.GetIntProperty(UnhousedAnnouncedProperty) == 1;
			if (wasUnhoused)
			{
				Resident.SetIntProperty(UnhousedAnnouncedProperty, 0);
			}
			// Housed is housed: the window a settler was spending is not banked, halved or
			// remembered. The brink is lifted outright, and if they lose their home again they get
			// the whole of it back, because the founder is being asked to act on THIS loss. Rule 2
			// -- the pressure is a fact re-derived every pass, so taking it off takes it off -- and
			// the unsaying is owed as loudly as the warning was.
			bool wasWarned = KingdomBrink.Of(Resident, BrinkKind.Roof).Warned;
			if (KingdomBrink.Lift(Resident, BrinkKind.Roof) && wasWarned)
			{
				// Only what was actually said is unsaid. A brink pre-recorded at a condemnation
				// the founder has not been told about yet has no warning to withdraw, and
				// withdrawing one they never heard is noise in the one lane that must not have any.
				KingdomBrink.Unsay(System, BrinkKind.Roof, residentName, KingdomWord.StandsIn(Z), System.SeatName);
			}
			AddOccupant(Occupancy, winningPlotId, Resident);
			if (wasUnhoused)
			{
				KingdomRules.BuildEntry winEntry;
				TryGetBuiltEntry(winningHome, out winEntry);
				string matched = KingdomLodgingRules.MatchedTag(needs, (winEntry == null) ? null : new List<string>(KingdomQol.OfferOf(winEntry.Key, Z)));
				string line = residentName + " found shelter: " + KingdomLodgingRules.HomeSuffix((winEntry != null) ? winEntry.Name : null, matched) + ".";
				KingdomChronicle.Record(System, line);
			}
		}

		/// <summary>Shared pure chooser for the real settlement pass and admission's projected
		/// occupancy. All mutation remains in <see cref="AssignOne"/>.</summary>
		private static string ChooseHome(Zone Z, GameObject Resident, List<GameObject> Homes,
			Dictionary<string, List<GameObject>> Occupancy, out GameObject WinningHome,
			out KingdomLodgingRules.UnhousedReason Reason,
			out KingdomLodgingRules.Closeness RoomiestRefused, out List<string> Needs)
		{
			WinningHome = null;
			Reason = KingdomLodgingRules.UnhousedReason.Housed;
			RoomiestRefused = KingdomLodgingRules.Closeness.Packed;
			QolProfile profile = KingdomQol.ProfileOf(Resident);
			Needs = new List<string>(profile.Needs);
			List<string> refuses = new List<string>(profile.Refuses);
			List<string> selfTags = SelfTagsOf(profile);
			string creed = Resident.GetStringProperty(KingdomCreed.CreedProperty);
			bool anyRoofAtAll = Homes.Count > 0;
			bool anyStanding = false;
			bool anyMeetsNeeds = false;
			bool anyHasCapacity = false;
			bool anyWithoutRefusal = false;
			List<KingdomLodgingRules.LodgingCandidate> eligible =
				new List<KingdomLodgingRules.LodgingCandidate>();
			List<GameObject> eligibleHomes = new List<GameObject>();
			for (int i = 0; i < Homes.Count; i++)
			{
				GameObject home = Homes[i];
				string plotId = home.GetStringProperty(KingdomPlots.PlotIdProperty);
				KingdomRules.BuildEntry entry;
				if (string.IsNullOrEmpty(plotId) || !TryGetBuiltEntry(home, out entry)) continue;
				int capacity = RoofCapacity(entry);
				if (capacity <= 0 || IsCondemned(home)) continue;
				anyStanding = true;
				List<string> provides = new List<string>(KingdomQol.OfferOf(entry.Key, Z));
				if (!KingdomLodgingRules.MeetsNeeds(Needs, provides)) continue;
				anyMeetsNeeds = true;
				List<GameObject> occupants;
				Occupancy.TryGetValue(plotId, out occupants);
				int occupantCount = occupants == null ? 0 : occupants.Count;
				if (!KingdomLodgingRules.HasFreeBed(capacity, occupantCount)) continue;
				anyHasCapacity = true;
				KingdomLodgingRules.Closeness quarters = KingdomFaith.EducatedCloseness(
					Z, QuartersOf(entry), home);
				if (occupants != null && AnyOccupantConflicts(refuses, selfTags, creed,
					occupants, quarters))
				{
					RoomiestRefused = KingdomLodgingRules.Roomier(RoomiestRefused, quarters);
					continue;
				}
				anyWithoutRefusal = true;
				eligible.Add(new KingdomLodgingRules.LodgingCandidate(
					plotId, capacity, occupantCount));
				eligibleHomes.Add(home);
			}
			int chosen = KingdomLodgingRules.ChooseIndex(eligible);
			if (chosen < 0)
			{
				Reason = KingdomLodgingRules.Diagnose(anyRoofAtAll, anyMeetsNeeds,
					anyHasCapacity, anyWithoutRefusal, anyStanding);
				return null;
			}
			WinningHome = eligibleHomes[chosen];
			return eligible[chosen].PlotId;
		}

		// --- Addendum 4b: the brink, the window, and the leaving ---------------------------

		// The roof instance of the shared brink (KingdomBrinkRules). Losing every acceptable home
		// is the irreversible line: it is RECORDED with the tick it happened and then nothing
		// accrues, so a founder away a thousand days and a founder away ten find a settler
		// standing in exactly the same place -- that half of the doctrine did not move.
		//
		// What moved (Addendum 10(a)) is everything after the record. The word is PUSHED the
		// moment the loss is seen, wherever the founder is, and it names the arrest. From that
		// delivery the settler has KingdomLodgingRules.GraceDays of WORLD TIME, not two visits. If
		// that time runs out with them still unroofed they go, attended or not, and the leaving is
		// dated to the tick the window actually ran out on rather than to the pass that found it.
		// Nothing here fires unwarned: an unwarned brink has no deadline at all, so the pass that
		// discovers a loss can only ever say so.
		private static void RunRoofBrink(KingdomSystem System, Zone Z, GameObject Resident, string ResidentName)
		{
			if (string.IsNullOrEmpty(ResidentName))
			{
				// Somebody the roll does not carry: a founding citizen, or a person the settlement
				// never named. The brink names its subject, so an unnamed resident simply never
				// enters it and never leaves for want of a roof. Staying is the safe answer to a
				// question the registers cannot record, and it is the one taken here.
				return;
			}
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			// Recorded at the tick the roof was lost. Usually that is this pass; when a slide
			// condemned the house days back it is that breakpoint's own tick, pre-recorded by
			// RecordCondemnedRoofBrink, and the announcement quotes the honest elapsed either way.
			KingdomBrink.Record(Resident, BrinkKind.Roof, now, null, 0);
			bool here = KingdomWord.StandsIn(Z);
			if (KingdomBrink.MarkWarned(Resident, BrinkKind.Roof, now))
			{
				// The day the word goes out is never the day they go: the window starts here, and
				// the whole of it is still in front of the founder.
				KingdomBrink.Announce(System, BrinkKind.Roof, ResidentName, null,
					KingdomBrink.Of(Resident, BrinkKind.Roof), now, here, System.SeatName, null);
				return;
			}
			BrinkRecord brink = KingdomBrink.Of(Resident, BrinkKind.Roof);
			if (!KingdomBrinkRules.WindowSpent(BrinkKind.Roof, brink.WarnedTick, now))
			{
				return;
			}
			long went = KingdomBrinkRules.ExpiryTick(BrinkKind.Roof, brink.WarnedTick);
			string leaving = KingdomLodgingRules.LeavingLine(ResidentName, KingdomBrinkRules.DaysStood(brink.ReachedTick, went))
				+ KingdomBrinkRules.FiredClause(KingdomBrinkRules.DaysStood(went, now));
			if (KingdomGrowth.Emigrate(System, Z, null, Resident, KingdomLodgingRules.DepartureCause))
			{
				KingdomWord.Aftermath(System, System.SeatName, here, leaving);
				KingdomBrink.Lift(Resident, BrinkKind.Roof);
				return;
			}
			// The settlement would not let them go &mdash; they are the last of the loyal core, or
			// the emigration machinery could not take them. The window stays spent and is tried
			// again on the next resolve rather than being reset, so nothing is lost and nobody is
			// told they are going by a settlement that then kept them.
		}

		// The per-city LodgingGrace map this file used to keep is RETIRED. A settler's window now
		// lives on the settler (KingdomBrink), which fixes two things at once: two settlers of the
		// same name in two cities no longer share one entry, and a departed settler's window
		// cannot be inherited by a later settler of the same name, because it walks out of the
		// settlement inside them. Nothing needs pruning, so nothing prunes. The field itself is
		// KingdomSystem's and KingdomSettlement's to remove.

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

		/// <summary>Purely projects the ordinary settlement pass: standing assignments keep
		/// their beds, then every unassigned or stale-home resident is seated in normal resident
		/// order. No property, brink, Chronicle, ledger, or cohabitation state is changed.</summary>
		private static Dictionary<string, List<GameObject>> ProjectedOccupancy(Zone Z)
		{
			Dictionary<string, List<GameObject>> result =
				new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);
			HashSet<string> standing = new HashSet<string>(StringComparer.Ordinal);
			List<GameObject> homes = HousingIn(Z);
			for (int i = 0; i < homes.Count; i++)
			{
				string plot = homes[i].GetStringProperty(KingdomPlots.PlotIdProperty);
				if (!string.IsNullOrEmpty(plot)) standing.Add(plot);
			}
			List<GameObject> residents = ResidentsIn(Z);
			List<GameObject> unassigned = new List<GameObject>();
			for (int i = 0; i < residents.Count; i++)
			{
				string plot = residents[i].GetStringProperty(HomePlotIdProperty);
				if (standing.Contains(plot)) AddOccupant(result, plot, residents[i]);
				else unassigned.Add(residents[i]);
			}
			for (int i = 0; i < unassigned.Count; i++)
			{
				GameObject ignoredHome;
				KingdomLodgingRules.UnhousedReason ignoredReason;
				KingdomLodgingRules.Closeness ignoredRefusal;
				List<string> ignoredNeeds;
				string plot = ChooseHome(Z, unassigned[i], homes, result, out ignoredHome,
					out ignoredReason, out ignoredRefusal, out ignoredNeeds);
				if (plot != null) AddOccupant(result, plot, unassigned[i]);
			}
			return result;
		}

		private static bool ObserveOccupantConflicts(List<string> Refuses,
			List<string> SelfTags, string Creed, List<GameObject> Occupants,
			KingdomLodgingRules.Closeness Quarters, out List<string> Evidence)
		{
			Evidence = new List<string>();
			bool any = false;
			if (Occupants == null) return false;
			for (int i = 0; i < Occupants.Count; i++)
			{
				GameObject occupant = Occupants[i];
				string occupantCreed = occupant.GetStringProperty(KingdomCreed.CreedProperty);
				int hostility = KingdomCreed.HostilityBetween(Creed, occupantCreed);
				QolProfile profile = KingdomQol.ProfileOf(occupant);
				List<string> needs = new List<string>(profile.Needs);
				List<string> prefers = new List<string>(profile.Prefers);
				List<string> refuses = new List<string>(profile.Refuses);
				List<string> selfTags = SelfTagsOf(profile);
				bool conflict = KingdomLodgingRules.Conflicts(Refuses, SelfTags,
					refuses, selfTags, hostility, Quarters);
				any |= conflict;
				needs.Sort(StringComparer.Ordinal); prefers.Sort(StringComparer.Ordinal);
				refuses.Sort(StringComparer.Ordinal); selfTags.Sort(StringComparer.Ordinal);
				Evidence.Add(ArrivalObservationHash(delegate(BinaryWriter writer)
				{
					WriteObservationString(writer, occupant.ID);
					WriteObservationString(writer, occupant.Blueprint);
					WriteObservationString(writer, occupantCreed);
					WriteObservationList(writer, needs); WriteObservationList(writer, prefers);
					WriteObservationList(writer, refuses); WriteObservationList(writer, selfTags);
					writer.Write(hostility); writer.Write((int)Quarters); writer.Write(conflict);
				}));
			}
			return any;
		}

		private static bool AnyOccupantConflicts(List<string> Refuses, List<string> SelfTags, string Creed, List<GameObject> Occupants, KingdomLodgingRules.Closeness Quarters)
		{
			for (int i = 0; i < Occupants.Count; i++)
			{
				GameObject occupant = Occupants[i];
				string occupantCreed = occupant.GetStringProperty(KingdomCreed.CreedProperty);
				// Addendum 4c: which creed feelings break a household is a question about the
				// household's own quarters, so the raw engine feeling is handed straight down and
				// the ladder in KingdomLodgingRules decides. The single floor that used to be
				// applied here -- only the flat -100 fault lines, never the standing -50 -- is
				// still exactly what a home at Closeness.Private asks, and is now the top rung of
				// four rather than the rule for every roof in the settlement.
				int hostility = KingdomCreed.HostilityBetween(Creed, occupantCreed);
				QolProfile theirs = KingdomQol.ProfileOf(occupant);
				List<string> occupantSelfTags = SelfTagsOf(theirs);
				List<string> occupantRefuses = new List<string>(theirs.Refuses);
				if (KingdomLodgingRules.Conflicts(Refuses, SelfTags, occupantRefuses, occupantSelfTags, hostility, Quarters))
				{
					return true;
				}
			}
			return false;
		}

		private static string ArrivalObservationHash(Action<BinaryWriter> Write)
		{
			if (Write == null) return null;
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream,
				new UTF8Encoding(false, true), true))
			{
				WriteObservationString(writer, "taf:lodging-arrival-observation:v1");
				Write(writer); writer.Flush();
				using (SHA256 sha = SHA256.Create())
				{
					byte[] digest = sha.ComputeHash(stream.ToArray());
					StringBuilder text = new StringBuilder(64);
					for (int i = 0; i < digest.Length; i++)
						text.Append(digest[i].ToString("x2", CultureInfo.InvariantCulture));
					return text.ToString();
				}
			}
		}

		private static void WriteObservationString(BinaryWriter Writer, string Value)
		{
			if (Value == null) { Writer.Write(-1); return; }
			byte[] bytes = new UTF8Encoding(false, true).GetBytes(Value);
			Writer.Write(bytes.Length); Writer.Write(bytes);
		}

		private static void WriteObservationList(BinaryWriter Writer, List<string> Values)
		{
			Writer.Write(Values == null ? -1 : Values.Count);
			if (Values != null) for (int i = 0; i < Values.Count; i++)
				WriteObservationString(Writer, Values[i]);
		}

		private static void AnnounceUnhoused(KingdomSystem System, GameObject Resident, string ResidentName, KingdomLodgingRules.UnhousedReason Reason, KingdomLodgingRules.Closeness RoomiestRefused)
		{
			if (Resident.GetIntProperty(UnhousedAnnouncedProperty) == 1)
			{
				return;
			}
			Resident.SetIntProperty(UnhousedAnnouncedProperty, 1);
			// Addendum 4c names the quarters, so a founder hearing this once (7b) hears what to
			// build rather than only that somebody is outside.
			string line = KingdomLodgingRules.UnhousedLine(ResidentName, Reason, RoomiestRefused);
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

		/// <summary>
		/// Whether this standing home has been worn past the point of being a roof
		/// (<see cref="KingdomLodgingRules.CondemnedWearPercent"/>). A home with no wear part
		/// has never been damaged and is sound.
		/// <para>
		/// The building is not touched, moved or unbuilt &mdash; the protection law forbids it
		/// and there is nothing to forbid here anyway. It simply stops being counted as somewhere
		/// to live until somebody mends it, which is a thing the founder can do with materials
		/// and hands on any pass.
		/// </para>
		/// </summary>
		public static bool IsCondemned(GameObject Home)
		{
			if (!GameObject.Validate(Home))
			{
				return false;
			}
			r_KingdomWear wear = Home.GetPart<r_KingdomWear>();
			return wear != null && KingdomLodgingRules.IsCondemned(wear.Wear);
		}

		/// <summary>
		/// The residents this home currently holds, by their stored assignment. For the caller
		/// that has just condemned a roof and owes the people under it a dated record of losing
		/// it &mdash; <c>KingdomSubsidence</c>'s ruin is the one that does.
		/// </summary>
		/// <param name="Z">The zone. Null holds nobody.</param>
		/// <param name="Home">The home. One with no plot id holds nobody, because an assignment
		/// is stored as a plot id and nothing else.</param>
		public static List<GameObject> ResidentsOf(Zone Z, GameObject Home)
		{
			List<GameObject> list = new List<GameObject>();
			string plotId = GameObject.Validate(Home) ? Home.GetStringProperty(KingdomPlots.PlotIdProperty) : null;
			if (Z == null || string.IsNullOrEmpty(plotId))
			{
				return list;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomCitizen") == 1 && item.GetStringProperty(HomePlotIdProperty) == plotId)
				{
					list.Add(item);
				}
			}
			return list;
		}

		/// <summary>
		/// Records the roof brink for everyone living under a home that has just been condemned,
		/// at the tick it actually happened rather than the pass that notices.
		/// <para>
		/// This is the honest-elapsed half of the brink, and the reason it is worth the call:
		/// <see cref="RunRoofBrink"/> records at the pass that finds the loss, which is
		/// right when the loss happened at that pass. A subsidence ruins a home at a breakpoint
		/// days or seasons back, and the settler has been sleeping in the open ever since. Record
		/// is idempotent, so the earliest honest tick is the one that stands and a second caller
		/// cannot redate it; nothing is warned and no window starts here, because the window is
		/// anchored at the founder's WARNING and this call has nobody to warn.
		/// </para>
		/// <para>
		/// Recorded only for an OCCUPIED home that actually crossed the line. A ruined shed
		/// nobody sleeps in, and a home worn but still livable, both record nothing.
		/// </para>
		/// </summary>
		/// <param name="Z">The zone the home stands in.</param>
		/// <param name="Home">The home that has just crossed into condemnation.</param>
		/// <param name="AtTick">The tick it crossed &mdash; the ruining breakpoint's own tick.</param>
		/// <returns>How many residents this recorded for.</returns>
		public static int RecordCondemnedRoofBrink(Zone Z, GameObject Home, long AtTick)
		{
			if (!Enabled || !IsCondemned(Home))
			{
				return 0;
			}
			List<GameObject> residents = ResidentsOf(Z, Home);
			int recorded = 0;
			for (int i = 0; i < residents.Count; i++)
			{
				// Unnamed residents never enter the brink, exactly as RunRoofBrink has it:
				// the brink names its subject, and staying is the safe answer to a question the
				// registers cannot record.
				if (string.IsNullOrEmpty(RollNameOf(residents[i])))
				{
					continue;
				}
				if (KingdomBrink.Record(residents[i], BrinkKind.Roof, AtTick, null, 0))
				{
					recorded++;
				}
			}
			return recorded;
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

		// The rung this design's own arithmetic puts it on, or the one its author declared. The
		// footprint is the ground the TIER stands on -- KingdomPlotRules.TryFootprint answers with
		// the whole plot for a tier that declares no footprint of its own, which is exactly right:
		// the stone house fills its plot and the tent does not. A design with no plot spec at all is
		// a single-cell work with a bunk in it, and reads Packed, which is what one cell is.
		private static KingdomLodgingRules.Closeness QuartersOf(KingdomRules.BuildEntry Entry)
		{
			if (Entry == null)
			{
				return KingdomLodgingRules.Closeness.Packed;
			}
			KingdomLodgingRules.Closeness declared;
			if (Declared.TryGetValue(Entry.Key, out declared))
			{
				return declared;
			}
			KingdomPlotRules.PlotSpec spec;
			int width;
			int height;
			int cells = (KingdomPlots.TryGetSpec(Entry.Key, out spec) && KingdomPlotRules.TryFootprint(spec, out width, out height))
				? (width * height)
				: 0;
			return KingdomLodgingRules.ClosenessFromDensity(cells, RoofCapacity(Entry));
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
			string matched = KingdomLodgingRules.MatchedTag(needs, (entry == null) ? null : new List<string>(KingdomQol.OfferOf(entry.Key, Z)));
			return " {{K|(" + KingdomLodgingRules.HomeSuffix((entry != null) ? entry.Name : null, matched) + ")}}";
		}

		/// <summary>The lodging line <c>kingdom:dump</c> appends for the zone the founder is
		/// standing in: how many of the residents present are housed, who is not, and how much of
		/// their brink window (Addendum 4b) they have spent, with how long they have actually been
		/// without a roof.</summary>
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
				BrinkRecord brink = KingdomBrink.Of(resident, BrinkKind.Roof);
				if (brink.Stands)
				{
					long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
					name += " (brink " + KingdomBrinkRules.DaysLeft(BrinkKind.Roof, brink.WarnedTick, now)
						+ "/" + KingdomLodgingRules.GraceDays + "d left"
						+ (brink.Warned ? "" : ", unwarned")
						+ ", stood " + KingdomBrinkRules.DaysStood(brink.ReachedTick, now) + "d)";
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
