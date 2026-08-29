using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		internal sealed class ManningPass
		{
			internal readonly List<ManningEntry> Entries = new List<ManningEntry>();
			internal readonly List<KingdomCrewRules.CrewReservation> Reservations =
				new List<KingdomCrewRules.CrewReservation>();
		}

		internal sealed class ManningEntry
		{
			internal r_KingdomNotice Data;
			internal int WorkIndex;
			internal int SettlerIndex;
			internal GameObject Work;
			internal GameObject Settler;
		}

		internal static ManningPass PrepareManningPass(KingdomSystem System, KingdomSurvey Survey,
			IList<GameObject> Available, int WorkHands)
		{
			ManningPass pass = new ManningPass();
			if (System == null || Survey == null) return pass;
			List<r_KingdomNotice> active = ActiveManning(Survey);
			QuarantineDuplicateManning(active);
			long now = The.Game.TimeTicks;
			KingdomElapsedOptionDecision option = ObserveManningOption(System, now);
			for (int i = 0; i < active.Count; i++)
			{
				r_KingdomNotice data = active[i];
				if (data.LifecycleQuarantined) continue;
				if (!ApplyManningOption(data, now, option))
				{
					if (data.LifecycleQuarantined) TellQuarantine(System, data);
					continue;
				}
				int workIndex = FindWorkIndex(Survey.Works, data.ManningWorkId);
				int settlerIndex = FindResidentIndex(Available, WorkHands, data.WorkerResidentId);
				GameObject work = workIndex >= 0 ? Survey.Works[workIndex] : null;
				GameObject settler = settlerIndex >= 0 ? Available[settlerIndex] : null;
				int workEpoch = Simulation.City.KingdomStations.AvailabilityEpochOf(work);
				int residentEpoch = Simulation.City.KingdomStations.AvailabilityEpochOf(settler);
				if ((workIndex >= 0 && workEpoch < 0) || (settlerIndex >= 0 && residentEpoch < 0))
				{
					Quarantine(data, "A manning endpoint's availability epoch is malformed.");
					TellQuarantine(System, data);
					continue;
				}
				bool exact = workIndex >= 0 && settlerIndex >= 0
					&& work.GetIntProperty("KingdomStaffNeeded") > 0
					&& workEpoch == data.ManningWorkEpoch
					&& residentEpoch == data.ManningResidentEpoch;
				long served;
				if (!KingdomBountyManningRules.TryAccrue(data.ManningServedTicks,
					data.ManningCheckpointTick, now, data.ManningAssigned, exact, out served))
				{
					data.ManningAssigned = false;
					data.DueTick = 0L;
					Quarantine(data, "The manning service clock regressed; its old checkpoint was preserved.");
					TellQuarantine(System, data);
					continue;
				}
				data.ManningServedTicks = served;
				data.ManningCheckpointTick = now;
				data.ManningAssigned = false;
				data.DueTick = 0L;
				if (KingdomBountyManningRules.RemainingTicks(data.ManningServedTicks) <= 0L)
					continue;
				if (workIndex < 0 || Survey.Works[workIndex].GetIntProperty("KingdomStaffNeeded") <= 0)
				{
					Announce(System, data, BountyBlock.ManningTargetLost);
					continue;
				}
				if (settlerIndex < 0)
				{
					Announce(System, data, ResidentPresent(Available, data.WorkerResidentId)
						? BountyBlock.NoFreeHands : BountyBlock.ManningWorkerAbsent);
					continue;
				}
				ManningEntry entry = new ManningEntry
				{
					Data = data,
					WorkIndex = workIndex,
					SettlerIndex = settlerIndex,
					Work = work,
					Settler = settler
				};
				pass.Entries.Add(entry);
				pass.Reservations.Add(new KingdomCrewRules.CrewReservation(
					settlerIndex, workIndex));
			}
			return pass;
		}

		internal static void PublishManningPass(KingdomSystem System, ManningPass Pass,
			KingdomCrewRules.CrewOutcome[] Outcomes, bool ReservationsValid)
		{
			if (Pass == null) return;
			long now = The.Game.TimeTicks;
			for (int i = 0; i < Pass.Entries.Count; i++)
			{
				ManningEntry entry = Pass.Entries[i];
				if (entry.Data.LifecycleQuarantined) continue;
				int workEpoch = Simulation.City.KingdomStations.AvailabilityEpochOf(entry.Work);
				int residentEpoch = Simulation.City.KingdomStations.AvailabilityEpochOf(entry.Settler);
				bool endpointExact = workEpoch >= 0 && residentEpoch >= 0
					&& GameObject.Validate(entry.Work) && GameObject.Validate(entry.Settler)
					&& entry.Work.IDIfAssigned == entry.Data.ManningWorkId
					&& Simulation.City.KingdomResidents.IdOf(entry.Settler)
						== entry.Data.WorkerResidentId
					&& entry.Work.GetIntProperty("KingdomStaffNeeded") > 0;
				if (!endpointExact)
				{
					Quarantine(entry.Data,
						"A manning endpoint changed while its ordinary crew reservation was publishing.");
					TellQuarantine(System, entry.Data);
					continue;
				}
				bool assigned = now > 0L && ReservationsValid && Outcomes != null
					&& entry.WorkIndex >= 0 && entry.WorkIndex < Outcomes.Length
					&& Contains(Outcomes[entry.WorkIndex].SettlerIndices, entry.SettlerIndex)
					&& Simulation.City.KingdomStations.PostOf(entry.Settler)
						== Simulation.City.KingdomCityRules.StableId(entry.Work.IDIfAssigned);
				entry.Data.ManningAssigned = assigned;
				entry.Data.ManningCheckpointTick = now;
				entry.Data.ManningResidentEpoch = residentEpoch;
				entry.Data.ManningWorkEpoch = workEpoch;
				entry.Data.DueTick = KingdomBountyManningRules.ForecastDueTick(now,
					entry.Data.ManningServedTicks, assigned);
				Announce(System, entry.Data,
					assigned ? BountyBlock.None : BountyBlock.NoFreeHands);
			}
		}

		internal static void RefuseManningPass(KingdomSystem System, ManningPass Pass)
		{
			if (Pass == null) return;
			for (int i = 0; i < Pass.Entries.Count; i++)
			{
				Quarantine(Pass.Entries[i].Data,
					"The exact manning reservations contradicted one another; no partial crew draw was used.");
				TellQuarantine(System, Pass.Entries[i].Data);
			}
		}

		private static List<r_KingdomNotice> ActiveManning(KingdomSurvey Survey)
		{
			List<r_KingdomNotice> found = new List<r_KingdomNotice>();
			for (int i = 0; i < Survey.Notices.Count; i++)
			{
				r_KingdomNotice data = Survey.Notices[i].GetPart<r_KingdomNotice>();
				if (data == null || data.TaskCode != (int)BountyTask.Manning || data.Done
					|| string.IsNullOrEmpty(data.WorkerName)) continue;
				if (data.ManningVersion != 1 || data.WorkerResidentId <= 0
					|| string.IsNullOrEmpty(data.ManningWorkId))
					Quarantine(data, "A claimed manning notice lacks its exact resident or work identity.");
				found.Add(data);
			}
			return found;
		}

		private static void QuarantineDuplicateManning(List<r_KingdomNotice> Active)
		{
			for (int i = 0; i < Active.Count; i++)
				for (int j = i + 1; j < Active.Count; j++)
					if (Active[i].WorkerResidentId == Active[j].WorkerResidentId
						|| string.Equals(Active[i].ManningWorkId, Active[j].ManningWorkId,
							StringComparison.Ordinal))
					{
						Quarantine(Active[i], "Two manning contracts claim the same resident or work.");
						Quarantine(Active[j], "Two manning contracts claim the same resident or work.");
					}
		}

		internal static List<string> ReaderRoster(KingdomSystem System, KingdomSurvey Survey,
			BountyTask Task, out List<int> ResidentIds)
		{
			ResidentIds = new List<int>();
			List<string> names = new List<string>();
			if (Task != BountyTask.Manning)
			{
				List<Simulation.City.KingdomResidentRow> rows =
					Simulation.City.KingdomResidents.RollRows(System);
				for (int i = 0; i < rows.Count; i++)
				{
					names.Add(rows[i].Name);
					ResidentIds.Add(rows[i].ResidentId);
				}
				return names;
			}
			List<GameObject> available = KingdomCrews.AvailableSettlers(System, Survey);
			int count = KingdomCrews.WorkHandCount(System, available);
			HashSet<int> contracted = ContractedResidents(Survey);
			List<Simulation.City.KingdomResidentRow> labour =
				Simulation.City.KingdomResidents.RollRows(System, true);
			Dictionary<int, string> byId = new Dictionary<int, string>();
			for (int i = 0; i < labour.Count; i++) byId[labour[i].ResidentId] = labour[i].Name;
			for (int i = 0; i < count; i++)
			{
				int id = Simulation.City.KingdomResidents.IdOf(available[i]);
				if (id <= 0 || contracted.Contains(id) || !byId.TryGetValue(id, out string name))
					continue;
				names.Add(name);
				ResidentIds.Add(id);
			}
			return names;
		}

		internal static int ReaderResidentId(IList<int> Ids, IList<string> Names,
			int Index, string Name)
		{
			return Ids != null && Names != null && Index >= 0 && Index < Ids.Count
				&& Index < Names.Count && string.Equals(Names[Index], Name, StringComparison.Ordinal)
				? Ids[Index] : 0;
		}

		private static HashSet<int> ContractedResidents(KingdomSurvey Survey)
		{
			HashSet<int> found = new HashSet<int>();
			if (Survey == null) return found;
			List<r_KingdomNotice> active = ActiveManning(Survey);
			for (int i = 0; i < active.Count; i++)
				if (!active[i].LifecycleQuarantined && active[i].WorkerResidentId > 0)
					found.Add(active[i].WorkerResidentId);
			return found;
		}

		private static int FindWorkIndex(IList<GameObject> Works, string WorkId)
		{
			for (int i = 0; Works != null && i < Works.Count; i++)
				if (GameObject.Validate(Works[i]) && Works[i].IDIfAssigned == WorkId) return i;
			return -1;
		}

		private static int FindResidentIndex(IList<GameObject> Available, int Count, int ResidentId)
		{
			int limit = Available != null && Count < Available.Count ? Count : Available?.Count ?? 0;
			for (int i = 0; i < limit; i++)
				if (Simulation.City.KingdomResidents.IdOf(Available[i]) == ResidentId) return i;
			return -1;
		}

		private static bool ResidentPresent(IList<GameObject> Available, int ResidentId)
		{
			return FindResidentIndex(Available, Available?.Count ?? 0, ResidentId) >= 0;
		}

		private static bool Contains(int[] Values, int Wanted)
		{
			for (int i = 0; Values != null && i < Values.Length; i++)
				if (Values[i] == Wanted) return true;
			return false;
		}
	}
}
