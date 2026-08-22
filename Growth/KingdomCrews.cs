using System.Collections.Generic;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of crew capability (<see cref="KingdomCrewRules"/> is the whole of
	/// the arithmetic, and is engine-free): the registry of what each design's <c>CrewNeeds</c>
	/// asks for, the reads that turn a real settler into a
	/// <see cref="KingdomCrewRules.SettlerCapability"/>, and the one entry point
	/// <c>KingdomGrowth.AssignWork</c> calls to crew a pass's works ablest-first.
	/// <para>
	/// <b>No state of its own beyond the registry.</b> Like <c>KingdomQol</c> beside it, the
	/// registry is keyed by building <c>Key</c>, cleared and refilled by the loader's single pass;
	/// nothing here is a per-city or realm-level field, and nothing accumulates across passes. A
	/// shortfall's "said once" flag lives on the work object itself
	/// (<see cref="ShortfallAnnouncedProperty"/>), the same idiom <c>r_KingdomPlot</c> already uses
	/// for its own once-only announcements, so a shortfall a founder fixes stops repeating without
	/// this file remembering anything about the pass that fixed it.
	/// </para>
	/// <para>
	/// <b>Derive before authoring.</b> <see cref="CapabilityOf"/> reads <c>Strength</c> and
	/// <c>Intelligence</c> straight off <c>GameObject.GetStatValue</c>, and asks
	/// <c>KingdomQol.TruthOf</c> &mdash; the vocabulary's own robot read &mdash; whether this
	/// settler is a robot, rather than testing <c>HasPart&lt;Robot&gt;()</c> a second time. A
	/// modded creature with real stats and no special part is therefore a correct crew member
	/// before its author has heard of this file.
	/// </para>
	/// </summary>
	public static class KingdomCrews
	{
		// Keyed by building Key like KingdomQol's own Declared registry (STANDARDS 6): a later
		// file re-using a key owns that design's whole CrewNeeds, and a re-declaration that names
		// no CrewNeeds at all correctly leaves it with none.
		private static readonly Dictionary<string, string> Declared = new Dictionary<string, string>();

		private static readonly Dictionary<string, List<KindAmount>> NeedsCache = new Dictionary<string, List<KindAmount>>();

		private static readonly List<KindAmount> EmptyNeeds = new List<KindAmount>();

		/// <summary>Forgets every registered <c>CrewNeeds</c>. Called by the registry loader
		/// before it re-reads the XML streams, beside <c>KingdomQol.ClearProvides</c>.</summary>
		public static void ClearCrewNeeds()
		{
			Declared.Clear();
			NeedsCache.Clear();
		}

		/// <summary>
		/// Registers one entry's <c>CrewNeeds</c> as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the merged raw attribute;
		/// null or blank registers "needs no particular hand", which is every design written
		/// before this attribute existed.
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="CrewNeeds">Raw <c>CrewNeeds</c> attribute: a <c>kind:amount</c> list in
		/// <see cref="KingdomCrewRules.TryParseCrewNeeds"/>'s language. A malformed value is
		/// logged and the design is left needing nothing, never refused outright &mdash; a
		/// third-party typo must not be able to take down a design the base catalogue relies on.
		/// </param>
		public static void RegisterCrewNeeds(string Key, string CrewNeeds)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			NeedsCache.Remove(Key);
			if (string.IsNullOrEmpty(CrewNeeds) || CrewNeeds.Trim().Length == 0)
			{
				Declared.Remove(Key);
				return;
			}
			if (!KingdomCrewRules.TryParseCrewNeeds(CrewNeeds, out var needs, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: building " + Key + " CrewNeeds \"" + CrewNeeds + "\" " + error + ".");
				return;
			}
			Declared[Key] = CrewNeeds;
			for (int i = 0; i < needs.Count; i++)
			{
				if (KingdomCrewRules.StatNameFor(needs[i].Kind) == null)
				{
					// A note, never a fault, exactly as KingdomQol logs an un-namespaced Provides
					// tag: a kind no stat answers to yet in THIS build may be read by a later wave
					// or another mod's own crew system.
					KingdomLog.Log("KingdomBuildings: building " + Key + " CrewNeeds names \"" + needs[i].Kind + "\", which no crew stat answers to yet.");
				}
			}
		}

		/// <summary>What one design was registered as needing, as written.</summary>
		public static string DeclaredCrewNeeds(string Key)
		{
			string declared;
			return (!string.IsNullOrEmpty(Key) && Declared.TryGetValue(Key, out declared)) ? declared : null;
		}

		/// <summary>The parsed <c>CrewNeeds</c> for one design. Never null; empty for a design
		/// that declares none.</summary>
		public static List<KindAmount> NeedsOf(string BuildingKey)
		{
			if (string.IsNullOrEmpty(BuildingKey))
			{
				return EmptyNeeds;
			}
			if (NeedsCache.TryGetValue(BuildingKey, out var cached))
			{
				return cached;
			}
			List<KindAmount> needs = EmptyNeeds;
			if (Declared.TryGetValue(BuildingKey, out var declared) && KingdomCrewRules.TryParseCrewNeeds(declared, out var parsed, out _))
			{
				needs = parsed;
			}
			NeedsCache[BuildingKey] = needs;
			return needs;
		}

		/// <summary>What a work standing on the ground needs, read off the design key it was
		/// raised under (<c>KingdomUpgrade.BuildKeyProperty</c>), the same read
		/// <c>KingdomQol.OfferOf(GameObject)</c> already performs. A work with no key on it needs
		/// nothing in particular.</summary>
		public static List<KindAmount> NeedsOf(GameObject Work)
		{
			if (Work == null)
			{
				return EmptyNeeds;
			}
			return NeedsOf(Work.GetStringProperty(KingdomUpgrade.BuildKeyProperty));
		}

		// --- Reading a settler ----------------------------------------------------------------

		/// <summary>What one real settler brings to a crew: <c>Strength</c> and
		/// <c>Intelligence</c> read straight off their stats, plus whether they are a robot
		/// (<c>KingdomQol.TruthOf</c>'s own read, reused rather than repeated).</summary>
		public static KingdomCrewRules.SettlerCapability CapabilityOf(GameObject Settler)
		{
			if (Settler == null)
			{
				return default(KingdomCrewRules.SettlerCapability);
			}
			int strength = Settler.GetStatValue("Strength");
			int intelligence = Settler.GetStatValue("Intelligence");
			bool tireless = KingdomQol.TruthOf(Settler).Robot;
			return new KingdomCrewRules.SettlerCapability(strength, intelligence, tireless);
		}

		/// <summary>The first <paramref name="Count"/> settlers of <paramref name="Settlers"/>,
		/// read into a capability pool in the same order. <paramref name="Count"/> is the
		/// headcount already left for works once the water detail is spent
		/// (<c>KingdomGrowth.AssignWork</c>'s own <c>forWorks</c>) &mdash; taking a prefix rather
		/// than the whole list keeps hands spent exactly once without this file having to know
		/// which specific settler carried water, which nothing in the mod tracks by identity.
		/// </summary>
		public static KingdomCrewRules.SettlerCapability[] CapabilitiesOf(IList<GameObject> Settlers, int Count)
		{
			int available = (Settlers != null) ? Settlers.Count : 0;
			int n = (Count < available) ? Count : available;
			if (n < 0)
			{
				n = 0;
			}
			KingdomCrewRules.SettlerCapability[] pool = new KingdomCrewRules.SettlerCapability[n];
			for (int i = 0; i < n; i++)
			{
				pool[i] = CapabilityOf(Settlers[i]);
			}
			return pool;
		}

		// --- The whole entry point AssignWork calls --------------------------------------------

		/// <summary>
		/// Crews a pass's works ablest-first: reads each work's headcount demand
		/// (<c>KingdomStaffNeeded</c>/<c>KingdomThresholdManning</c>, exactly as
		/// <c>KingdomRules.AssignCrew</c> already did) plus whichever <see cref="KingdomCrewRules.KnownKinds"/>
		/// its registered <c>CrewNeeds</c> names first with a positive threshold, then hands the
		/// whole priority-ordered list to <see cref="KingdomCrewRules.AssignCrew"/>.
		/// </summary>
		/// <param name="Works">Works in priority (placement) order, e.g. <c>Survey.Works</c>.</param>
		/// <param name="Pool">The capability pool for this pass, e.g. <see cref="CapabilitiesOf"/>
		/// already reduced by the water detail.</param>
		/// <returns>One outcome per work, same order, same length. Never null.</returns>
		public static KingdomCrewRules.CrewOutcome[] AssignWorks(IList<GameObject> Works, KingdomCrewRules.SettlerCapability[] Pool)
		{
			int n = (Works != null) ? Works.Count : 0;
			KingdomCrewRules.CrewDemand[] demands = new KingdomCrewRules.CrewDemand[n];
			for (int i = 0; i < n; i++)
			{
				GameObject work = Works[i];
				int headcount = work.GetIntProperty("KingdomStaffNeeded");
				bool threshold = work.GetIntProperty("KingdomThresholdManning") == 1;
				List<KindAmount> needs = NeedsOf(work);
				string kind = null;
				int capabilityThreshold = 0;
				for (int k = 0; k < KingdomCrewRules.KnownKinds.Length; k++)
				{
					int need = KingdomCrewRules.ThresholdOf(needs, KingdomCrewRules.KnownKinds[k]);
					if (need > 0)
					{
						kind = KingdomCrewRules.KnownKinds[k];
						capabilityThreshold = need;
						break;
					}
				}
				demands[i] = new KingdomCrewRules.CrewDemand(headcount, threshold, kind, capabilityThreshold);
			}
			return KingdomCrewRules.AssignCrew(Pool, demands);
		}

		// --- Announcing a shortfall once per work (STANDARDS 7b) -------------------------------

		public const string ShortfallAnnouncedProperty = "KingdomCrewShortfall";

		/// <summary>
		/// Says, once, that a work is running slow for want of a capability its crew does not
		/// bring, and stays quiet on every later pass that names the exact same shortfall. Call
		/// only when <see cref="KingdomCrewRules.CapabilityEffectiveness"/> actually reads under
		/// 100 for this work; call <see cref="ClearShortfall"/> the moment it does not, so a
		/// founder who answers the shortfall is told the ledger agrees.
		/// </summary>
		public static void AnnounceShortfall(GameObject Work, string DisplayName, string Kind, int Have, int Need)
		{
			if (Work == null)
			{
				return;
			}
			string tag = Kind + ":" + Have + ":" + Need;
			if (Work.GetStringProperty(ShortfallAnnouncedProperty) == tag)
			{
				return;
			}
			Work.SetStringProperty(ShortfallAnnouncedProperty, tag);
			MessageQueue.AddPlayerMessage("{{r|" + KingdomCrewRules.ShortfallLine(DisplayName, Kind, Have, Need) + "}}");
		}

		/// <summary>Clears a work's shortfall flag once its capability effectiveness reads 100
		/// again, so a shortfall that recurs later (a fresh <c>CrewNeeds</c> merge, a different
		/// crew drawn) is announced fresh rather than staying silent forever.</summary>
		public static void ClearShortfall(GameObject Work)
		{
			if (Work == null)
			{
				return;
			}
			if (!string.IsNullOrEmpty(Work.GetStringProperty(ShortfallAnnouncedProperty)))
			{
				Work.SetStringProperty(ShortfallAnnouncedProperty, null, RemoveIfNull: true);
			}
		}
	}
}
