using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomCrewRules
	{
		/// <summary>One caller-owned, exact person-to-work reservation for a single crew pass.</summary>
		public readonly struct CrewReservation
		{
			public readonly int SettlerIndex;
			public readonly int DemandIndex;

			public CrewReservation(int SettlerIndex, int DemandIndex)
			{
				this.SettlerIndex = SettlerIndex;
				this.DemandIndex = DemandIndex;
			}
		}

		/// <summary>
		/// Performs the ordinary deterministic draw while pinning exact residents to exact works.
		/// Every reservation is validated before any result is published. Reserved residents are
		/// unavailable to every other demand, including earlier priority rows. A threshold demand
		/// still receives all its requested hands or none; a reservation never weakens that law.
		/// </summary>
		public static bool TryAssignCrewReserved(SettlerCapability[] Pool, CrewDemand[] Demands,
			int[,] ExtensionAffinities, IList<CrewReservation> Reservations,
			out CrewOutcome[] Outcomes)
		{
			if (Reservations == null || Reservations.Count == 0)
			{
				Outcomes = AssignCrew(Pool, Demands, ExtensionAffinities);
				return true;
			}
			SettlerCapability[] pool = Pool ?? EmptyPool;
			if (Demands == null || Demands.Length == 0)
			{
				Outcomes = new CrewOutcome[0];
				return false;
			}
			bool[] reservedSettler = new bool[pool.Length];
			int[] reservedForDemand = new int[Demands.Length];
			for (int i = 0; i < Reservations.Count; i++)
			{
				CrewReservation row = Reservations[i];
				if (row.SettlerIndex < 0 || row.SettlerIndex >= pool.Length
					|| row.DemandIndex < 0 || row.DemandIndex >= Demands.Length
					|| reservedSettler[row.SettlerIndex])
				{
					Outcomes = EmptyOutcomes(Demands);
					return false;
				}
				int need = Demands[row.DemandIndex].Headcount;
				if (need <= 0 || reservedForDemand[row.DemandIndex] >= need)
				{
					Outcomes = EmptyOutcomes(Demands);
					return false;
				}
				reservedSettler[row.SettlerIndex] = true;
				reservedForDemand[row.DemandIndex]++;
			}

			bool[] taken = new bool[pool.Length];
			Outcomes = new CrewOutcome[Demands.Length];
			for (int demandIndex = 0; demandIndex < Demands.Length; demandIndex++)
			{
				CrewDemand demand = Demands[demandIndex];
				int need = demand.Headcount > 0 ? demand.Headcount : 0;
				List<int> forced = ForcedIndices(Reservations, demandIndex);
				if (need == 0)
				{
					Outcomes[demandIndex] = EmptyOutcome(demand);
					continue;
				}
				bool[] blocked = (bool[])taken.Clone();
				for (int i = 0; i < reservedSettler.Length; i++)
					if (reservedSettler[i]) blocked[i] = true;
				int[] ranked = RankCandidates(pool, blocked, demand.CapabilityKind,
					demand.WorkKind, ExtensionAffinities, demandIndex);
				int possible = forced.Count + ranked.Length;
				if (demand.Threshold && possible < need)
				{
					Outcomes[demandIndex] = EmptyOutcome(demand);
					continue;
				}
				int give = need < possible ? need : possible;
				int[] chosen = new int[give];
				int cursor = 0;
				for (int i = 0; i < forced.Count && cursor < give; i++)
					chosen[cursor++] = forced[i];
				for (int i = 0; i < ranked.Length && cursor < give; i++)
					chosen[cursor++] = ranked[i];
				for (int i = 0; i < chosen.Length; i++) taken[chosen[i]] = true;
				Outcomes[demandIndex] = OutcomeOf(pool, demand, chosen,
					ExtensionAffinities, demandIndex);
			}
			return true;
		}

		private static List<int> ForcedIndices(IList<CrewReservation> Rows, int DemandIndex)
		{
			List<int> found = new List<int>();
			for (int i = 0; i < Rows.Count; i++)
				if (Rows[i].DemandIndex == DemandIndex) found.Add(Rows[i].SettlerIndex);
			return found;
		}

		private static CrewOutcome OutcomeOf(SettlerCapability[] Pool, CrewDemand Demand,
			int[] Chosen, int[,] ExtensionAffinities, int DemandIndex)
		{
			int best = 0;
			int affinity = 0;
			for (int i = 0; i < Chosen.Length; i++)
			{
				int at = Chosen[i];
				int value = Pool[at].ValueOf(Demand.CapabilityKind);
				if (value > best) best = value;
				affinity += KingdomIdentityAffinityRules.Compose(Pool[at].Affinity(Demand.WorkKind),
					ExtensionAffinityOf(ExtensionAffinities, DemandIndex, at));
			}
			return new CrewOutcome(Chosen.Length, Demand.CapabilityKind,
				Demand.CapabilityThreshold, Demand.CapabilityKind != null ? best : 0, Chosen,
				Chosen.Length > 0 ? affinity / Chosen.Length
					: KingdomIdentityAffinityRules.NeutralPercent, Demand.WorkKind);
		}

		private static CrewOutcome EmptyOutcome(CrewDemand Demand)
		{
			return new CrewOutcome(0, Demand.CapabilityKind, Demand.CapabilityThreshold, 0,
				EmptyIndices, KingdomIdentityAffinityRules.NeutralPercent, Demand.WorkKind);
		}

		private static CrewOutcome[] EmptyOutcomes(CrewDemand[] Demands)
		{
			CrewOutcome[] result = new CrewOutcome[Demands.Length];
			for (int i = 0; i < result.Length; i++) result[i] = EmptyOutcome(Demands[i]);
			return result;
		}
	}
}
