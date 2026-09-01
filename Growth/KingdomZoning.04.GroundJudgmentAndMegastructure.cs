using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomZoning
	{
		/// <summary>
		/// The district on a piece of the realm's ground: whatever the founder designated, or
		/// null for ground never designated. Unclaimed and unknown zones read as undistricted,
		/// which is the permissive answer.
		/// </summary>
		public static string DistrictOf(KingdomSystem System, string ZoneID)
		{
			if (System == null || ZoneID == null || System.ZoneDistricts == null)
			{
				return null;
			}
			return System.ZoneDistricts.TryGetValue(ZoneID, out string district) ? district : null;
		}

		private static ZoningJudgement JudgeAt(KingdomSystem System, string District, KingdomRules.BuildEntry Entry, bool Underground)
		{
			if (!Enabled || System == null || !System.Founded || Entry == null)
			{
				return ZoningJudgement.Allowed;
			}
			ZoningJudgement covenant = KingdomZoningRules.JudgeCovenant(
				new CovenantGate(Entry.CovenantFaction, Entry.CovenantMinStanding),
				string.IsNullOrEmpty(Entry.CovenantFaction) ? 0 :
				System.GetRegardForRealm(Entry.CovenantFaction));
			if (!covenant.Permitted)
			{
				return covenant;
			}
			int claimed = (System.ClaimedZones != null) ? System.ClaimedZones.Count : 0;
			// The capital's two lanes are read here rather than inside the rules, because both need
			// the realm's books and the rules class has no engine. Both are cheap on this path for
			// the same reason KeptMegastructure is: the crown's answer is cached by ground and tick,
			// and the outpost lane asks nothing at all of a design nobody declared an outpost.
			KingdomSatelliteVerdict satellite = KingdomSatellite.JudgeActiveGround(System, Entry.Key, out string satelliteDetail);
			return KingdomZoningRules.Judge(GateFor(Entry.Key), District, Entry.Category, claimed, Roster(System),
				Underground, WantsSky(Entry), BuilderRollOf(System), KingdomZoningRules.StratumOfGround(Underground),
				Entry.Key, KeptMegastructure(System),
				KingdomCrown.CrownedOnActiveGround(System), KingdomCrown.CapitalName(System),
				satellite, satelliteDetail);
		}

		// Retained only as loader-reset compatibility for older source integrations. The answer is no
		// longer read from this cache: two commissions can publish on the same game tick, so a tick
		// cache could authorize both body works before either had appeared in the city book.
		private static string KeptCacheZone;

		private static long KeptCacheTick = -1L;

		private static string KeptCacheValue;

		/// <summary>
		/// The registry key of the megastructure this city already keeps, or null when it keeps none
		/// &mdash; and null, deliberately, when nothing could tell.
		/// <para>
		/// <b>Two sources, and they are not equals.</b> The city book is the RECORD: its work rows
		/// cover every zone the city holds, including the ones nobody has stood in for a season, and
		/// a cardinality rule that only saw loaded ground would let a founder raise a second great
		/// work simply by walking away from the first. The loaded zone is the FRESHNESS PATCH: the
		/// book's work rows for a zone are rebuilt at that zone's own settlement pass
		/// (<c>KingdomCity.ReadWorks</c>), so a theatre finished since the last pass is standing in
		/// the world and not yet written down. Where the two disagree it is always in that one
		/// direction, and the patch closes it.
		/// </para>
		/// <para>
		/// <b>Derivation only &mdash; nothing here is stored.</b> A serialized "this city's purpose"
		/// field would be a second record of a thing the book already knows, and the two would drift
		/// the first time a great work was demolished.
		/// </para>
		/// <para>
		/// The book stores each work's BLUEPRINT (<c>KingdomCity.ReadWorks</c> writes
		/// <c>work.Blueprint</c> into the design-key column), so each stored value is resolved
		/// against both the registry's keys and its blueprints. Reading the raw column rather than
		/// the frozen model is deliberate and is the one place in this file that does: <c>TryRead</c>
		/// allocates a whole city &mdash; zones, works, residents &mdash; and this is a single-column
		/// scan on a hot menu path.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Null yields null, which permits.</param>
		public static string KeptMegastructure(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return null;
			}
			// Before the cache is trusted, not after: this is what runs ClearGates for a freshly
			// loaded game, and a cache read that happened first could hand a second game in the same
			// session the first one's answer on a shared tick and zone.
			KingdomData.EnsureBuildings();
			Zone active = The.ZoneManager?.ActiveZone;
			string here = (active != null) ? active.ZoneID : "";
			long now = (The.Game != null) ? The.Game.TimeTicks : 0L;
			// Gathered once and searched against, rather than walking the whole catalogue for every
			// stored work: a city's book can carry forty work rows and the catalogue eighty designs,
			// and the megastructures among them are — by the rule this enforces — almost always one.
			List<string> keys = new List<string>();
			List<string> blueprints = new List<string>();
			List<KingdomRules.BuildEntry> entries = KingdomData.Buildings;
			for (int i = 0; i < entries.Count; i++)
			{
				if (GateFor(entries[i].Key).Megastructure)
				{
					keys.Add(entries[i].Key);
					blueprints.Add(entries[i].Blueprint ?? "");
				}
			}
			string kept = null;
			if (keys.Count > 0)
			{
				Simulation.City.KingdomCityBook book = System.City;
				if (book != null && book.WorkDesignKeys != null)
				{
					for (int i = 0; i < book.WorkDesignKeys.Count && kept == null; i++)
					{
						kept = MegastructureKeyOf(book.WorkDesignKeys[i], keys, blueprints);
					}
				}
				if (kept == null && active != null)
				{
					KingdomSurvey survey = KingdomSurvey.ActiveFor(active)
						?? KingdomSurvey.Take(active);
					for (int i = 0; i < survey.Built.Count; i++)
					{
						GameObject work = survey.Built[i];
						if (!KingdomUpgrade.IsFunctionallyBuilt(work))
						{
							continue;
						}
						kept = MegastructureKeyOf(KingdomUpgrade.DesignKeyOf(work), keys, blueprints);
						if (kept != null)
						{
							break;
						}
					}
				}
				// A published plot job spends the slot immediately. Waiting for the works object or the
				// next city-book pass leaves a same-tick window in which the capital can fund both the
				// theatre and annexe. Only physical plot routes count: a cargo consignment names its
				// destination purpose but belongs to the producer city and must not spend that city's slot.
				if (kept == null && KingdomConstruction.TryRead(
					out List<KingdomConstructionJob> jobs, out _))
				{
					string owner = KingdomConstruction.OwnerOf(System);
					for (int i = 0; i < jobs.Count && kept == null; i++)
					{
						KingdomConstructionJob job = jobs[i];
						if (job.OwnerKey != owner || System.ClaimedZones == null
							|| !System.ClaimedZones.Contains(job.ZoneId)
							|| KingdomConstructionRules.IsTerminal(job.Phase)
							|| (job.Route != KingdomConstructionRoute.PlotCommission
								&& job.Route != KingdomConstructionRoute.PlotPlan)) continue;
						kept = MegastructureKeyOf(job.TargetKey, keys, blueprints);
					}
				}
			}
			KeptCacheZone = here;
			KeptCacheTick = now;
			KeptCacheValue = kept;
			return kept;
		}

		/// <summary>
		/// The registry key a stored work-row value names, if that design is a megastructure.
		/// Matched against the registry's KEYS first and its BLUEPRINTS second, because the book's
		/// column carries a blueprint (<c>KingdomCity.ReadWorks</c>) while the loaded-zone read
		/// carries a key (<c>KingdomUpgrade.DesignKeyOf</c>), and a rule that read only one of the
		/// two would be right about half its callers.
		/// </summary>
		private static string MegastructureKeyOf(string Stored, List<string> Keys, List<string> Blueprints)
		{
			if (string.IsNullOrEmpty(Stored))
			{
				return null;
			}
			for (int i = 0; i < Keys.Count; i++)
			{
				if (string.Equals(Keys[i], Stored) || string.Equals(Blueprints[i], Stored))
				{
					return Keys[i];
				}
			}
			return null;
		}

		// The weather half of the depth gate is the design's own Sky flag, which lives on the plot
		// spec rather than on the build entry. A design the plot registry never registered wants no
		// weather by definition, so it is never refused for the want of it. Read in one place
		// because the refusal has to ask the same question the judgement did, and two lookups that
		// could ever disagree would put a sentence in front of the founder that was true of neither.
		private static bool WantsSky(KingdomRules.BuildEntry Entry)
		{
			KingdomPlotRules.PlotSpec spec;
			return Entry != null && KingdomPlots.TryGetSpec(Entry.Key, out spec) && spec != null && spec.RequiresSky;
		}

	}
}
