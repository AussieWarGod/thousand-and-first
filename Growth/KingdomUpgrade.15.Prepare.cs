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
			Prepared = new PreparedImprovement
			{
				WorkId = Work?.ID, SourceKey = A.Key, SuccessorKey = A.SuccessorKey,
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
			if (!KingdomArchitectureRules.IsCurrentSnapshotEncoding(before.EncodedSnapshot))
				return true; // a1 remains read-only legacy compatibility.
			Legacy = false;
			ArchitectureLayoutSnapshot ignoredBefore;
			string lot;
			if (!KingdomArchitectureStamper.TryReadOwner(Work, out before, out ignoredBefore,
				out lot, out Failure) || Work.GetIntProperty(
					KingdomArchitectureStamper.NextLayerProperty) != 3)
				return false;
			KingdomArchitectureIntent successor;
			if (A.Transition == null)
			{
				if (!KingdomArchitectureRuntime.TryPrepareSuccessor(System, Z, before,
					A.SuccessorKey, out successor, out Failure)) return false;
			}
			else if (!KingdomArchitectureRuntime.TryPreparePlanTransition(System, Z, before,
				A.SuccessorKey, A.Transition, out successor, out Failure)) return false;
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(A.Transition == null
				? KingdomMaterials.UpgradeCostFor(A.Key) : A.Transition.Materials);
			if (A.Transition == null)
			{
				if (!KingdomArchitectureStamper.TryPreflightUpgrade(System, Z, Work, successor,
					claim, out Delta, out Failure)) return false;
			}
			else if (!KingdomArchitectureStamper.TryPreflightPlanTransition(System, Z, Work,
				successor, A.Transition, claim, out Delta, out Failure)) return false;
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
			KingdomMaterialDebitCost claim = new KingdomMaterialDebitCost(A.Transition == null
				? KingdomMaterials.UpgradeCostFor(A.Key) : A.Transition.Materials);
			ArchitectureLayoutDelta delta;
			bool proved = A.Transition == null
				? KingdomArchitectureStamper.TryPreflightUpgrade(System, Z, Work, architecture,
					claim, out delta, out Failure)
				: KingdomArchitectureStamper.TryPreflightPlanTransition(System, Z, Work,
					architecture, A.Transition, claim, out delta, out Failure);
			if (!proved) return false;
			Prepared.Delta = delta;
			Payload = Prepared.Payload;
			return true;
		}

		/// <summary>Starts one founder-ordered explicit same-set plan change.</summary>
	}
}
