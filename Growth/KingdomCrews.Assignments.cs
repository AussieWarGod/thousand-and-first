using System;
using System.Collections.Generic;
using ThousandAndFirst.Api;
using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
	{
	public static partial class KingdomCrews
	{
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
		public static KingdomCrewRules.CrewOutcome[] AssignWorks(IList<GameObject> Works,
			KingdomCrewRules.SettlerCapability[] Pool, IList<GameObject> Settlers = null)
		{
			KingdomCrewRules.CrewDemand[] demands = DemandsOf(Works);
			int[,] extensionAffinities = ExtensionAffinities(demands, Settlers,
				Pool == null ? 0 : Pool.Length);
			return KingdomCrewRules.AssignCrew(Pool, demands, extensionAffinities);
		}

		/// <summary>Reserved form of <see cref="AssignWorks"/>. Invalid or duplicate exact
		/// reservations publish no partial result and return false.</summary>
		public static bool TryAssignWorks(IList<GameObject> Works,
			KingdomCrewRules.SettlerCapability[] Pool, IList<GameObject> Settlers,
			IList<KingdomCrewRules.CrewReservation> Reservations,
			out KingdomCrewRules.CrewOutcome[] Outcomes)
		{
			KingdomCrewRules.CrewDemand[] demands = DemandsOf(Works);
			int[,] extensionAffinities = ExtensionAffinities(demands, Settlers,
				Pool == null ? 0 : Pool.Length);
			return KingdomCrewRules.TryAssignCrewReserved(Pool, demands, extensionAffinities,
				Reservations, out Outcomes);
		}

		private static KingdomCrewRules.CrewDemand[] DemandsOf(IList<GameObject> Works)
		{
			int n = Works != null ? Works.Count : 0;
			KingdomCrewRules.CrewDemand[] demands = new KingdomCrewRules.CrewDemand[n];
			for (int i = 0; i < n; i++)
			{
				GameObject work = Works[i];
				List<KindAmount> needs = NeedsOf(work);
				string kind = null;
				int threshold = 0;
				for (int k = 0; k < KingdomCrewRules.KnownKinds.Length; k++)
				{
					int wanted = KingdomCrewRules.ThresholdOf(needs,
						KingdomCrewRules.KnownKinds[k]);
					if (wanted <= 0) continue;
					kind = KingdomCrewRules.KnownKinds[k];
					threshold = wanted;
					break;
				}
				string workKind = null;
				string buildKey = work.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
				if (KingdomData.TryGetBuilding(buildKey, out KingdomRules.BuildEntry entry))
					workKind = entry.Category;
				demands[i] = new KingdomCrewRules.CrewDemand(
					work.GetIntProperty("KingdomStaffNeeded"),
					work.GetIntProperty("KingdomThresholdManning") == 1,
					kind, threshold, workKind);
			}
			return demands;
		}

		/// <summary>Draws one temporary raising gang through the same capability, identity, and
		/// extension-affinity lane as a finished work. Construction differs only in headcount: it
		/// wants the bounded settlement gang rather than the finished design's running staff.</summary>
		internal static KingdomCrewRules.CrewOutcome AssignRaising(GameObject Work,
			IList<GameObject> Settlers, int WantedHands)
		{
			int count = Settlers == null ? 0 : Settlers.Count;
			KingdomCrewRules.SettlerCapability[] pool = CapabilitiesOf(Settlers, count);
			List<KindAmount> needs = NeedsOf(Work);
			string kind = null;
			int capabilityThreshold = 0;
			for (int k = 0; k < KingdomCrewRules.KnownKinds.Length; k++)
			{
				int need = KingdomCrewRules.ThresholdOf(needs,
					KingdomCrewRules.KnownKinds[k]);
				if (need <= 0) continue;
				kind = KingdomCrewRules.KnownKinds[k];
				capabilityThreshold = need;
				break;
			}
			string workKind = null;
			string buildKey = GameObject.Validate(Work)
				? Work.GetStringProperty(KingdomUpgrade.BuildKeyProperty) : null;
			if (KingdomData.TryGetBuilding(buildKey, out KingdomRules.BuildEntry entry))
				workKind = entry.Category;
			KingdomCrewRules.CrewDemand[] demand = new KingdomCrewRules.CrewDemand[1]
			{
				new KingdomCrewRules.CrewDemand(WantedHands, false, kind,
					capabilityThreshold, workKind)
			};
			int[,] extensionAffinities = ExtensionAffinities(demand, Settlers, pool.Length);
			return KingdomCrewRules.AssignCrew(pool, demand, extensionAffinities)[0];
		}

		/// <summary>Freezes every third-party affinity before pure assignment begins. One source is
		/// asked once per (person, canonical work kind) in this pass, even when several works share a
		/// category; no mutable body crosses the extension seam.</summary>
		private static int[,] ExtensionAffinities(KingdomCrewRules.CrewDemand[] Demands,
			IList<GameObject> Settlers, int PoolCount)
		{
			if (Demands == null || Demands.Length == 0 || Settlers == null || PoolCount <= 0)
			{
				return null;
			}
			int count = PoolCount < Settlers.Count ? PoolCount : Settlers.Count;
			if (count <= 0)
			{
				return null;
			}
			int[,] result = new int[Demands.Length, PoolCount];
			Dictionary<string, int> cache = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < Demands.Length; i++)
			{
				string workKind = KingdomApiRules.IdentityWorkKind(Demands[i].WorkKind);
				if (string.IsNullOrEmpty(workKind)) continue;
				for (int j = 0; j < count; j++)
				{
					GameObject settler = Settlers[j];
					if (!GameObject.Validate(settler)) continue;
					string cacheKey = j.ToString(System.Globalization.CultureInfo.InvariantCulture)
						+ "|" + workKind;
					if (!cache.TryGetValue(cacheKey, out int affinity))
					{
						affinity = KingdomExtensions.IdentityAffinity(
							KingdomIdentity.Read(settler), workKind);
						cache[cacheKey] = affinity;
					}
					result[i, j] = affinity;
				}
			}
			return result;
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
