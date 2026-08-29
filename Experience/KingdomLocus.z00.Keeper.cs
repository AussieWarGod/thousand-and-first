using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLocus
	{
		/// <summary>Reconciles the one civic work and the exact resident posted to it. This pass
		/// owns no guest clock: plain travellers may wait or recover without suppressing it.</summary>
		private static void RunKeeperPass(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			long TimeTicks)
		{
			List<GameObject> benches = FindBenches(Z, Survey);
			int locusWorkId = KingdomLocusRules.SelectLocusWork(
				System?.City?.WorkIds, System?.City?.WorkDesignKeys, BenchBlueprint);
			GameObject bench = FindBench(benches, locusWorkId, out bool ambiguous);
			if (!GameObject.Validate(bench))
			{
				ConfigureAmbient(benches, null, null, System, Z, TimeTicks, Enabled: false);
				DemoteKeepers(Survey, null);
				KingdomLocusRules.KeeperServiceState state = ambiguous || locusWorkId == 0
					? KingdomLocusRules.KeeperServiceState.AuthorityUnknown
					: KingdomLocusRules.KeeperServiceState.OtherGround;
				for (int i = 0; i < benches.Count; i++)
					SetBenchDescription(benches[i], KingdomLocusRules.BenchDescription(state, null));
				return;
			}

			// v1 benches predate the catalogue's staffed-locus declaration. Adopt only an exact
			// TAF-built gathering bench whose staff field is absent; a nonzero divergent value is
			// evidence and remains untouched. The refreshed survey makes it a work immediately,
			// while ordinary assignment owns who is posted on the next settlement pass.
			if (bench.GetIntProperty("KingdomStaffNeeded") == 0)
			{
				bench.SetIntProperty("KingdomStaffNeeded", 1);
				bench.SetIntProperty("KingdomThresholdManning", 0, RemoveIfZero: true);
				Survey.ObserveChanged(bench);
			}
			if (!Enabled)
			{
				ConfigureAmbient(benches, null, null, System, Z, TimeTicks, Enabled: false);
				DemoteKeepers(Survey, null);
				SetBenchDescription(bench, KingdomLocusRules.BenchDescription(
					KingdomLocusRules.KeeperServiceState.Disabled, null));
				DescribeOtherBenches(benches, bench);
				return;
			}

			int workId = locusWorkId;
			bool staffed = bench.GetIntProperty("KingdomStaffNeeded") == 1
				&& bench.GetIntProperty("KingdomStaffed") == 1 && workId != 0;
			List<string> candidates = KeeperCandidates(Survey, workId);
			string current = FirstMarkedCandidate(Survey, candidates);
			string selected = staffed ? KingdomLocusRules.SelectKeeper(candidates, current) : null;
			GameObject keeper = FindSettler(Survey, selected);
			if (!staffed)
			{
				ConfigureAmbient(benches, null, null, System, Z, TimeTicks, Enabled: false);
				DemoteKeepers(Survey, null);
				SetBenchDescription(bench, KingdomLocusRules.BenchDescription(
					KingdomLocusRules.KeeperServiceState.Unstaffed, null));
				DescribeOtherBenches(benches, bench);
				return;
			}
			if (!GameObject.Validate(keeper))
			{
				ConfigureAmbient(benches, null, null, System, Z, TimeTicks, Enabled: false);
				DemoteKeepers(Survey, null);
				SetBenchDescription(bench, KingdomLocusRules.BenchDescription(
					KingdomLocusRules.KeeperServiceState.KeeperMissing, null));
				DescribeOtherBenches(benches, bench);
				return;
			}

			DemoteKeepers(Survey, keeper);
			UpdateKeeperConversation(System, keeper, TimeTicks);
			SetBenchDescription(bench, KingdomLocusRules.BenchDescription(
				KingdomLocusRules.KeeperServiceState.Ready, keeper.ShortDisplayName));
			DescribeOtherBenches(benches, bench);

			bool ambient = KingdomExperienceRuntime.TryObserveConfiguredOptions(System,
				TimeTicks, out string _)
				&& KingdomExperienceRules.CanEmit(System.Experience,
					KingdomExperienceOptionKind.AmbientUse, TimeTicks)
				&& !Options.DisableAllIdleTileAnimations
				&& !Options.DisableTextAnimationEffects;
			ConfigureAmbient(benches, bench, keeper, System, Z, TimeTicks, ambient);
		}

		private static List<GameObject> FindBenches(Zone Z, KingdomSurvey Survey)
		{
			List<GameObject> benches = new List<GameObject>();
			for (int i = 0; Survey != null && i < Survey.Objects.Count; i++)
			{
				GameObject item = Survey.Objects[i];
				if (GameObject.Validate(item) && item.CurrentCell != null
					&& ReferenceEquals(item.CurrentZone, Z) && item.Blueprint == BenchBlueprint
					&& item.GetIntProperty("KingdomBuilt") == 1) benches.Add(item);
			}
			benches.Sort(delegate(GameObject A, GameObject B)
			{
				return string.CompareOrdinal(A?.IDIfAssigned ?? "", B?.IDIfAssigned ?? "");
			});
			return benches;
		}

		private static GameObject FindBench(List<GameObject> Benches, int WorkId,
			out bool Ambiguous)
		{
			Ambiguous = false;
			GameObject found = null;
			for (int i = 0; Benches != null && WorkId != 0 && i < Benches.Count; i++)
			{
				if (KingdomCityRules.StableId(Benches[i].IDIfAssigned) != WorkId) continue;
				if (found != null) { Ambiguous = true; return null; }
				found = Benches[i];
			}
			return found;
		}

		private static List<string> KeeperCandidates(KingdomSurvey Survey, int WorkId)
		{
			List<string> ids = new List<string>();
			for (int i = 0; Survey != null && WorkId != 0 && i < Survey.Settlers.Count; i++)
			{
				GameObject settler = Survey.Settlers[i];
				if (GameObject.Validate(settler) && settler.IsAlive && settler.Brain != null
					&& KingdomResidents.IdOf(settler) > 0
					&& !KingdomPhysicalHappenings.IsStaged(settler)
					&& KingdomStations.PostOf(settler) == WorkId
					&& !string.IsNullOrEmpty(settler.IDIfAssigned)) ids.Add(settler.IDIfAssigned);
			}
			ids.Sort(StringComparer.Ordinal);
			return ids;
		}

		private static string FirstMarkedCandidate(KingdomSurvey Survey, List<string> Candidates)
		{
			for (int i = 0; i < Candidates.Count; i++)
			{
				GameObject body = FindSettler(Survey, Candidates[i]);
				if (body != null && body.GetIntProperty("KingdomKeeper") == 1) return Candidates[i];
			}
			return null;
		}

		private static GameObject FindSettler(KingdomSurvey Survey, string ID)
		{
			if (string.IsNullOrEmpty(ID)) return null;
			for (int i = 0; Survey != null && i < Survey.Settlers.Count; i++)
				if (Survey.Settlers[i].IDIfAssigned == ID) return Survey.Settlers[i];
			return null;
		}
	}
}
