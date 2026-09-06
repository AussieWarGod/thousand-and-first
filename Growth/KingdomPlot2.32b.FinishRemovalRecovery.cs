using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPlots
	{
		private static void RecoverPendingPlotRemoval(KingdomSystem System, Zone Z,
			GameObject Final, ref KingdomConstructionJob Job)
		{
			if (Job == null || Job.PhysicalPhase != KingdomPhysicalPhase.FinalRemovalPending)
				return;
			string predecessorId = Job.PhysicalItemId;
			if (!KingdomConstruction.Owns(System, Z, Job) || !KingdomConstruction.IsCurrent(Job)
				|| !GameObject.Validate(Final) || Final.IDIfAssigned != Job.OutputId
				|| !ExactPlotFinalRootCustody(Job.OutputId, Final)
				|| Job.PhysicalDestinationId != Job.OutputId
				|| string.IsNullOrEmpty(predecessorId) || predecessorId == Job.OutputId
				|| !KingdomConstruction.HasReceipt(Final, Job)
				|| !KingdomConstruction.PaidBuildMatches(Final, Job))
			{
				KingdomConstruction.Quarantine(ref Job,
					"Pending plot removal lost its authenticated final endpoint.");
				return;
			}
			if (!XRL.World.Parts.r_KingdomScaffold.HasRemovalProof(Final, predecessorId)
				|| KingdomConstruction.FindExactId(Z, predecessorId, out _)
					!= KingdomPhysicalLookupState.Absent
				|| !ExactPlotRemovalTombstone(predecessorId, null, Job))
			{
				KingdomConstruction.Quarantine(ref Job,
					"Pending plot removal lacks a durable exact callback tombstone.");
				return;
			}
			if (!KingdomConstruction.UpdatePhysical(ref Job,
					KingdomPhysicalPhase.FinalRemoved, Job.PhysicalIndex, Job.PhysicalAmount,
					Job.PhysicalSpilled, predecessorId, Job.OutputId, Job.PhysicalReceipt))
			{
				KingdomConstruction.Quarantine(ref Job,
					"Absent plot predecessor could not settle its removal receipt.");
				return;
			}
			FinishPlotEffects(System, Z, Final, ref Job);
		}

		private static bool ExactGraveyardTombstone(string Id, GameObject Expected)
		{
			return ExactGraveyardTombstone(Id, Expected, out _);
		}

		private static bool ExactPlotRemovalTombstone(string Id, GameObject Expected,
			KingdomConstructionJob Job)
		{
			if (!ExactGraveyardTombstone(Id, Expected, out GameObject tombstone)
				|| tombstone == null || GameObject.Validate(tombstone)) return false;
			// Native Destroy promises retained graveyard parts, but invalid-object access remains
			// guarded. Any engine/version disagreement fails closed instead of guessing retirement.
			try
			{
				XRL.World.Parts.r_KingdomPlotWorks works =
					tombstone.GetPart<XRL.World.Parts.r_KingdomPlotWorks>();
				if (works == null) return false;
				if (Job == null) return true;
				return Job.SubjectId == Id && Job.PhysicalItemId == Id
					&& tombstone.GetStringProperty(KingdomConstruction.ReceiptProperty) == Job.Id
					&& works.DesignKey == Job.TargetKey;
			}
			catch
			{
				KingdomLog.Log("plot removal: native retained graveyard parts are unreadable");
				return false;
			}
		}

		private static bool ExactLegacyPlotRemovalTombstone(string Id, string BuildKey)
		{
			if (!ExactGraveyardTombstone(Id, null, out GameObject tombstone)
				|| tombstone == null || GameObject.Validate(tombstone)) return false;
			try
			{
				XRL.World.Parts.r_KingdomPlotWorks works =
					tombstone.GetPart<XRL.World.Parts.r_KingdomPlotWorks>();
				return works != null && works.DesignKey == BuildKey;
			}
			catch
			{
				KingdomLog.Log("plot removal: legacy graveyard parts are unreadable");
				return false;
			}
		}

		private static bool ExactGraveyardTombstone(string Id, GameObject Expected,
			out GameObject Tombstone)
		{
			return FindGraveyardTombstone(Id, out Tombstone) == KingdomPhysicalLookupState.Exact
				&& (Expected == null || object.ReferenceEquals(Expected, Tombstone));
		}

		private static KingdomPhysicalLookupState FindGraveyardTombstone(string Id,
			out GameObject Tombstone)
		{
			Tombstone = null;
			if (string.IsNullOrEmpty(Id) || !TryLoadedPlotTombstones(out List<GameObject> rows))
				return KingdomPhysicalLookupState.Ambiguous;
			int count = 0;
			try
			{
				for (int i = 0; i < rows.Count; i++)
				{
					GameObject item = rows[i];
					if (item == null) continue;
					if (!TryReadGraveyardId(item, out string itemId))
					{ Tombstone = null; return KingdomPhysicalLookupState.Ambiguous; }
					if (itemId == Id) { count++; Tombstone = item; }
				}
			}
			catch
			{
				KingdomLog.Log("plot removal: native graveyard identity is unreadable");
				Tombstone = null;
				return KingdomPhysicalLookupState.Ambiguous;
			}
			if (count == 1) return KingdomPhysicalLookupState.Exact;
			Tombstone = null;
			return count == 0 ? KingdomPhysicalLookupState.Absent : KingdomPhysicalLookupState.Ambiguous;
		}

		private static bool TryReadGraveyardId(GameObject Item, out string Id)
		{
			Id = null;
			if (Item == null) return false;
			try { Id = Item.IDIfAssigned; return true; }
			catch
			{
				KingdomLog.Log("plot removal: native graveyard identity is unreadable");
				return false;
			}
		}
	}
}
