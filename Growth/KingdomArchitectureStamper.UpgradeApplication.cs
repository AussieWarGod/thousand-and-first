using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureStamper
	{
		/// <summary>
		/// Reconstructs and proves a paid successor solely from the standing owner and frozen
		/// successor receipt. No current architecture catalogue, material table, or building entry is
		/// consulted. Used by projection/retry after the no-spend preflight has crossed debit.
		/// </summary>
		public static bool TryValidateFrozenUpgrade(GameObject Owner, Zone Z,
			KingdomArchitectureIntent Successor, out ArchitectureLayoutDelta Delta,
			out string Failure)
		{
			Delta = null;
			Failure = null;
			KingdomArchitectureIntent beforeIntent;
			ArchitectureLayoutSnapshot before;
			ArchitectureLayoutSnapshot after;
			string lot;
			if (Z == null) return Fail("frozen authored upgrade has no exact zone", out Failure);
			if (!TryUpgradeBase(Owner, Z, Successor, out beforeIntent, out before,
				out after, out Delta, out lot, out Failure)) return false;
			if (Owner.CurrentZone != Z || Owner.CurrentCell != Z.GetCell(beforeIntent.MainWorldX,
				beforeIntent.MainWorldY) || Owner.GetIntProperty(NextLayerProperty) != 3)
				return Fail("frozen authored predecessor is not complete on its exact main cell",
					out Failure);
			return true;
		}

		/// <summary>
		/// Applies one frozen same-lot delta without consulting current catalogues. The predecessor
		/// remains the durable controller until every exact removal, retained retag, and added layer
		/// proves itself on the already-rooted successor behavior object.
		/// </summary>
		public static bool TryApplyUpgrade(GameObject Owner, GameObject Target, Zone Z,
			KingdomArchitectureIntent Successor, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Owner) || !GameObject.Validate(Target) || Z == null
				|| Owner.CurrentZone != Z || Target.CurrentZone != Z
				|| Target.CurrentCell != Z.GetCell(Successor == null ? -1 : Successor.MainWorldX,
					Successor == null ? -1 : Successor.MainWorldY))
				return Fail("authored upgrade endpoints do not stand on the frozen main cell", out Failure);
			KingdomArchitectureIntent beforeIntent;
			ArchitectureLayoutSnapshot before;
			ArchitectureLayoutSnapshot after;
			ArchitectureLayoutDelta delta;
			string lot;
			if (!TryUpgradeBase(Owner, Z, Successor, out beforeIntent, out before, out after,
				out delta, out lot, out Failure)) return false;

			bool marked = Owner.HasIntProperty(UpgradeSchemaProperty)
				|| Owner.HasStringProperty(UpgradeSchemaProperty);
			if (!marked)
			{
				if (!TryVerifyComplete(Owner, Z, out Failure)) return false;
				for (int i = 0; i < delta.Removed.Count; i++)
				{
					GameObject exact;
					if (!TryExactOutput(Owner, Z, beforeIntent, lot, delta.Removed[i],
						out exact, out Failure)
						|| !TryRemovableComponent(exact, delta.Removed[i], out Failure)) return false;
				}
				if (!TryBeginUpgradeReceipt(Owner, Target, Successor, lot, delta, out Failure))
					return false;
			}
			else if (!TryReadUpgradeReceipt(Owner, Target, Successor, lot, out Failure))
				return false;

			int phase = Owner.GetIntProperty(UpgradePhaseProperty);
			if (phase == 0)
			{
				Target.SetStringProperty(KingdomPlots.PlotIdProperty, lot);
				KingdomArchitectureIntent targetIntent;
				ArchitectureLayoutSnapshot targetSnapshot;
				string targetLot;
				if (!KingdomArchitectureStamper.TryReadOwner(Target, out targetIntent,
					out targetSnapshot, out targetLot, out _))
				{
					if (!KingdomArchitectureRuntime.TryFreeze(Target, Successor, out Failure)
						|| !TryInitializeOwner(Target, Successor, lot, out Failure))
						return UpgradeFail(Owner, Failure, out Failure);
				}
				else if (targetLot != lot || targetIntent.SnapshotHash != Successor.SnapshotHash)
					return UpgradeFail(Owner, "successor already carries another layout receipt",
						out Failure);
				Owner.SetIntProperty(UpgradePhaseProperty, 1);
				phase = 1;
			}
			if (!ExactSuccessorOwner(Target, Successor, lot, out Failure))
				return UpgradeFail(Owner, Failure, out Failure);

			if (phase == 1)
			{
				for (int i = 0; i < delta.Removed.Count; i++)
					if (!TryRemoveUpgradeSlot(Owner, Z, beforeIntent, lot, delta.Removed[i],
						out Failure)) return UpgradeFail(Owner, Failure, out Failure);
				Owner.SetIntProperty(UpgradePhaseProperty, 2);
				phase = 2;
			}
			if (phase == 2)
			{
				for (int i = 0; i < delta.Retained.Count; i++)
					if (!TryCarryUpgradeSlot(Owner, Target, Z, beforeIntent, Successor, lot,
						delta.Retained[i], delta.RetainedAfter[i], out Failure))
						return UpgradeFail(Owner, Failure, out Failure);
				Owner.SetIntProperty(UpgradePhaseProperty, 3);
				phase = 3;
			}
			if (phase == 3)
			{
				if (!TryStageLayer(Target, Z, ArchitectureLayer.Ground, out Failure)
					|| !TryStageLayer(Target, Z, ArchitectureLayer.Structure, out Failure)
					|| !TryStageLayer(Target, Z, ArchitectureLayer.Object, out Failure)
					|| !TryVerifyComplete(Target, Z, out Failure))
					return UpgradeFail(Owner, Failure, out Failure);
				Owner.SetIntProperty(UpgradePhaseProperty, 4);
				phase = 4;
			}
			if (phase != 4 || !TryVerifyComplete(Target, Z, out Failure))
				return UpgradeFail(Owner, Failure ?? "authored upgrade phase is malformed", out Failure);
			return true;
		}
	}
}
