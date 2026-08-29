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
	public static partial class KingdomReach
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

	}
}
