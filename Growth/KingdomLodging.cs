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
	public static partial class KingdomLodging
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
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (Survey == null) return;
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
					WriteObservationString(writer, Newcomer == null ? null : Newcomer.IDIfAssigned);
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
						WriteObservationString(writer, home.IDIfAssigned);
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
					WriteObservationString(writer, home.IDIfAssigned);
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
				WriteObservationString(writer, Newcomer == null ? null : Newcomer.IDIfAssigned);
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
	}
}
