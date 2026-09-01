using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		public static bool TryPrepareImprovement(KingdomSystem System, Zone Z, GameObject Work,
			Assessment A, out PreparedImprovement Prepared, out string Failure)
		{
			Prepared = null;
			if (!TryPrepareImprovementPayload(System, Z, Work, A, out string payload,
				out KingdomArchitectureIntent architecture, out ArchitectureLayoutDelta delta,
				out bool legacy, out Failure)) return false;
			string sourceKey = A.Key;
			string successorKey = A.SuccessorKey;
			if (A.Transition != null)
			{
				if (!KingdomArchitectureRuntime.TryRead(Work,
					out KingdomArchitectureIntent before, out Failure)
					|| !TryCurrentTransition(before, A, out KingdomSocketTransition current,
						out Failure)) return false;
				sourceKey = current.FromBuildKey;
				successorKey = current.ToBuildKey;
			}
			Prepared = new PreparedImprovement
			{
				WorkId = Work?.ID, SourceKey = sourceKey, SuccessorKey = successorKey,
				Payload = payload, Legacy = legacy, Architecture = architecture, Delta = delta
			};
			return true;
		}

		private static bool TryPrepareImprovementPayload(KingdomSystem System, Zone Z,
			GameObject Work, Assessment A, out string Payload,
			out KingdomArchitectureIntent Architecture, out ArchitectureLayoutDelta Delta,
			out bool Legacy, out string Failure)
		{
			Payload = A.Key;
			Architecture = null;
			Delta = null;
			Legacy = true;
			Failure = null;
			bool marker = Work != null
				&& (Work.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
					|| Work.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty));
			if (!marker) return true; // save-era plot or single-cell work retains its legacy path.
			KingdomArchitectureIntent before;
			if (!KingdomArchitectureRuntime.TryRead(Work, out before, out Failure)) return false;
			if (!KingdomArchitectureRules.IsLatestSnapshotEncoding(before.EncodedSnapshot))
			{
				if (!KingdomArchitectureRules.IsManagedSnapshotEncoding(before.EncodedSnapshot))
					return true; // a1/a2 retain their older read-only compatibility lane.
				Failure = "This save-era authored plot cannot invent the building/yard scope "
					+ "needed for a new renovation. Strike it and commission the successor fresh.";
				return false;
			}
			Legacy = false;
			ArchitectureLayoutSnapshot ignoredBefore;
			string lot;
			if (!KingdomArchitectureStamper.TryReadOwner(Work, out before, out ignoredBefore,
				out lot, out Failure) || Work.GetIntProperty(
					KingdomArchitectureStamper.NextLayerProperty) != 3)
				return false;
			KingdomSocketTransition transition = null;
			if (A.Transition != null
				&& !TryCurrentTransition(before, A, out transition, out Failure)) return false;
			KingdomArchitectureIntent successor;
			if (transition == null)
			{
				if (!KingdomArchitectureRuntime.TryPrepareSuccessorForUpgrade(System, Z, Work,
					before, A.SuccessorKey, out successor, out Failure)) return false;
			}
			else if (!KingdomArchitectureRuntime.TryPreparePlanTransition(System, Z, before,
				A.SuccessorKey, transition, out successor, out Failure)) return false;
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(transition == null
				? KingdomMaterials.UpgradeCostFor(A.Key) : transition.Materials);
			if (transition == null)
			{
				if (!KingdomArchitectureStamper.TryPreflightUpgrade(System, Z, Work, successor,
					claim, out Delta, out Failure)) return false;
			}
			else if (!KingdomArchitectureStamper.TryPreflightPlanTransition(System, Z, Work,
				successor, transition, claim, out Delta, out Failure)) return false;
			Architecture = successor;
			return KingdomPlots.TryEncodePlotPayload(successor.Rect, null, Architecture,
				out Payload, out Failure);
		}

		private static bool TryReprovePreparedImprovement(KingdomSystem System, Zone Z,
			GameObject Work, Assessment A, PreparedImprovement Prepared,
			out string Payload, out string Failure)
		{
			Payload = null;
			Failure = null;
			if (Prepared == null || !GameObject.Validate(Work) || Prepared.WorkId != Work.ID
				|| Prepared.SourceKey != A.Key || Prepared.SuccessorKey != A.SuccessorKey
				|| string.IsNullOrEmpty(Prepared.Payload))
			{
				Failure = "The previewed improvement no longer names this exact work.";
				return false;
			}
			if (Prepared.Legacy)
			{
				if (A.Transition != null)
				{
					Failure = "A same-set plan change cannot use a legacy prepared payload.";
					return false;
				}
				Payload = Prepared.Payload;
				return true;
			}
			KingdomPlotRules.PlotRect rect;
			string skin;
			bool legacy;
			KingdomArchitectureIntent architecture;
			if (!KingdomPlots.TryDecodePlotPayload(Prepared.Payload, out rect, out skin,
				out architecture, out legacy, out Failure) || legacy || architecture == null
				|| Prepared.Architecture == null
				|| architecture.EncodedSnapshot != Prepared.Architecture.EncodedSnapshot
				|| architecture.SnapshotHash != Prepared.Architecture.SnapshotHash
				|| architecture.MainWorldX != Prepared.Architecture.MainWorldX
				|| architecture.MainWorldY != Prepared.Architecture.MainWorldY)
			{
				if (Failure == null) Failure = "The previewed successor receipt changed before consent.";
				return false;
			}
			KingdomSocketTransition transition = null;
			if (A.Transition != null)
			{
				if (!KingdomArchitectureRuntime.TryRead(Work,
					out KingdomArchitectureIntent before, out Failure)
					|| !TryCurrentTransition(before, A, out transition, out Failure)) return false;
			}
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(transition == null
				? KingdomMaterials.UpgradeCostFor(A.Key) : transition.Materials);
			ArchitectureLayoutDelta delta;
			bool proved = transition == null
				? KingdomArchitectureStamper.TryPreflightUpgrade(System, Z, Work, architecture,
					claim, out delta, out Failure)
				: KingdomArchitectureStamper.TryPreflightPlanTransition(System, Z, Work,
					architecture, transition, claim, out delta, out Failure);
			if (!proved) return false;
			Prepared.Delta = delta;
			Payload = Prepared.Payload;
			return true;
		}

		private static bool TryCurrentTransition(KingdomArchitectureIntent Before, Assessment A,
			out KingdomSocketTransition Current, out string Failure)
		{
			Current = null;
			Failure = null;
			if (Before == null || A.Transition == null || A.Successor == null
				|| Before.BuildKey != A.Key || A.Successor.Key != A.SuccessorKey
				|| !KingdomSocketTransitions.TryResolveCurrent(A.Transition, Before.BuildKey,
					A.SuccessorKey, Before.LotType, Before.LotSize, out Current)
				|| A.Key != Current.FromBuildKey || A.SuccessorKey != Current.ToBuildKey
				|| A.CostDrams != Current.WaterDrams || A.BuildTicks != Current.WorkTicks
				|| A.CrewNeeded != Math.Max(1, A.Successor.Staff))
			{
				Current = null;
				Failure = "The same-set declaration is forged, stale, or changed since preview.";
				return false;
			}
			return true;
		}

		/// <summary>Starts one founder-ordered explicit same-set plan change.</summary>
	}
}
