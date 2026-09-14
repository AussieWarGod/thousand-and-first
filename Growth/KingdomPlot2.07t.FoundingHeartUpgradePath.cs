using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static bool FoundingUpgradePathHasSubject(Zone Z, string Origin, string Subject)
		{
			if (Origin == Subject) return true;
			if (!ReadFoundingUpgradePath(Z, Origin, out var completed, out var pending)) return false;
			foreach (var edge in completed)
				if (edge.SubjectId == Subject) return true;
			return pending != null && pending.SubjectId == Subject;
		}

		private static bool ReadFoundingUpgradePath(Zone Z, string Origin,
			out List<KingdomConstructionJob> Completed, out KingdomConstructionJob Pending)
		{
			Completed = null;
			Pending = null;
			var system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || Z == null || !system.ClaimedZones.Contains(Z.ZoneID)
				|| !KingdomFoundingHeartTerminalRules.TryDecode(
					Z.GetZoneProperty(FoundingHeartTerminalProperty, null), out var terminal)
				|| terminal.FinalId != Origin
				|| !KingdomConstruction.TryRead(out var jobs, out _)) return false;
			return KingdomFoundingHeartUpgradePathRules.TryRead(jobs, Origin, terminal.PredecessorId,
				KingdomConstruction.OwnerOf(system), Z.ZoneID, out Completed, out Pending,
				OriginRung: KingdomPlotRules.HeartRungOf(terminal.BuildKey));
		}

		private static bool TryImprovementSuccessorOf(Zone Z, string RetiredId,
			out KingdomConstructionJob Job, out GameObject Successor, out int Named,
			out int LiveOutputs)
		{
			Job = null;
			Successor = null;
			Named = LiveOutputs = 0;
			if (!ReadFoundingUpgradePath(Z, RetiredId, out var completed, out _)
				|| completed.Count == 0) return false;
			Named = completed.Count;
			foreach (var edge in completed)
				if (!ExactFoundingHeartLiveAbsence(edge.SubjectId)) return false;
			Job = completed[completed.Count - 1];
			if (KingdomConstruction.FindGlobalLiveId(Job.OutputId, out Successor)
				!= KingdomPhysicalLookupState.Exact || !GameObject.Validate(Successor)
				|| Successor.CurrentZone != Z || Successor.CurrentCell != Z.GetCell(Job.X, Job.Y)
				|| KingdomUpgrade.DesignKeyOf(Successor) != Job.TargetKey) return false;
			LiveOutputs = 1;
			return true;
		}

		private static bool HasChainedImprovementReceipt(Zone Z, GameObject Successor,
			KingdomConstructionJob Completed)
		{
			if (!GameObject.Validate(Successor) || Completed == null
				|| !KingdomFoundingHeartTerminalRules.TryDecode(
					Z.GetZoneProperty(FoundingHeartTerminalProperty, null), out var terminal)
				|| !ReadFoundingUpgradePath(Z, terminal.FinalId, out var path, out var pending)
				|| path.Count == 0 || path[path.Count - 1].Id != Completed.Id
				|| !KingdomFoundingHeartUpgradePathRules.OutputReceiptMatches(Completed, pending,
					Successor.IDIfAssigned, Successor.GetStringProperty(KingdomConstruction.ReceiptProperty)))
				return false;
			if (KingdomConstruction.HasReceipt(Successor, Completed)) return true;
			var working = Successor.GetPart<r_KingdomImprovement>();
			return pending != null && working != null && working.Working
				&& working.SuccessorKey == pending.TargetKey;
		}
	}
}
