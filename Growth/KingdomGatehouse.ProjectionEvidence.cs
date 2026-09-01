using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGatehouse
	{
		private const int MaxProjectionScanObjects = 16384;

		private static bool TryDriveProjectionSlot(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part,
			int Index, out string Failure)
		{
			Failure = null;
			if (Z == null || Plan == null
				|| (Plan.ReceiptVersion != 1 && Part == null)
				|| !KingdomGatehouseRules.TrySatellite(Plan, Index, out KingdomGatehouseCell spec))
				return FailSlot("The frozen gatehouse topology is incomplete.", out Failure);
			if (Plan.ReceiptVersion == 1)
				return TryDriveLegacyProjectionSlot(Root, RootCell, Job, Scaffold, Z,
					Plan, PlanReceipt, Part, Index, spec, out Failure);
			string expectedId = KingdomGatehouseProjectionRules.StableSatelliteId(
				Root.IDIfAssigned, PlanReceipt, Index);
			string idKey = SatelliteIdProperty(Index);
			string stateKey = SatelliteStateProperty(Index);
			if (string.IsNullOrEmpty(expectedId) || Root.HasIntProperty(idKey)
				|| Root.HasStringProperty(stateKey))
				return FailSlot("A gatehouse satellite receipt has the wrong value type.", out Failure);

			for (int transition = 0; transition < 5; transition++)
			{
				string id = Root.GetStringProperty(idKey);
				int rawState = Root.GetIntProperty(stateKey);
				if (rawState < 0 || rawState > (int)KingdomGatehouseSlotState.Contested
					|| (!string.IsNullOrEmpty(id) && id != expectedId))
					return ContestSlot(Root, Index,
						"A gatehouse satellite identity or phase is noncanonical.", out Failure);
				KingdomGatehouseSlotState state = (KingdomGatehouseSlotState)rawState;
				if (!TryProjectionEvidence(Root, Z, Plan, Part, Index, expectedId,
					out KingdomGatehouseSlotEvidence evidence, out GameObject item,
					out Failure)) return ContestSlot(Root, Index, Failure, out Failure);
				KingdomGatehouseSlotAction action = KingdomGatehouseProjectionRules.Resolve(
					Index, state, !string.IsNullOrEmpty(id), evidence);
				switch (action)
				{
				case KingdomGatehouseSlotAction.PublishIdentity:
					Root.SetStringProperty(idKey, expectedId);
					if (Root.GetStringProperty(idKey) != expectedId)
						return FailSlot("A gatehouse satellite identity did not persist.", out Failure);
					continue;
				case KingdomGatehouseSlotAction.PublishPending:
					Root.SetIntProperty(stateKey, (int)KingdomGatehouseSlotState.Pending);
					if (Root.GetIntProperty(stateKey) != (int)KingdomGatehouseSlotState.Pending)
						return FailSlot("A gatehouse satellite pending phase did not persist.", out Failure);
					continue;
				case KingdomGatehouseSlotAction.Create:
					if (!TryCreateStagedSatellite(Root, RootCell, Job, Scaffold, Z,
						Plan, PlanReceipt, Part, Index, spec, expectedId, out Failure))
						return false;
					continue;
				case KingdomGatehouseSlotAction.Place:
					return TryPlaceStagedSatellite(Root, RootCell, Job, Scaffold, Z,
						Plan, PlanReceipt, Part, Index, spec, expectedId, item, out Failure);
				case KingdomGatehouseSlotAction.Settle:
					return SettleProjectionSlot(Root, Part, Index, item, out Failure);
				case KingdomGatehouseSlotAction.Verify:
					if (ProjectionCustody(Part, Index) != null
						&& !ReferenceEquals(ProjectionCustody(Part, Index), item))
						return ContestSlot(Root, Index,
							"A settled gatehouse slot retains foreign callback custody.", out Failure);
					return SetProjectionCustody(Part, Index, null)
						|| FailSlot("Settled gatehouse callback custody did not clear.", out Failure);
				default:
					return ContestSlot(Root, Index,
						"A gatehouse satellite is absent, duplicated, foreign, or contested.", out Failure);
				}
			}
			return FailSlot("A gatehouse satellite transition did not converge.", out Failure);
		}

		private static bool TryDriveLegacyProjectionSlot(GameObject Root, Cell RootCell,
			KingdomConstructionJob Job, GameObject Scaffold, Zone Z,
			KingdomGatehousePlan Plan, string PlanReceipt, IPart Part, int Index,
			KingdomGatehouseCell Spec, out string Failure)
		{
			Failure = null;
			string idKey = SatelliteIdProperty(Index);
			string stateKey = SatelliteStateProperty(Index);
			if (Plan?.ReceiptVersion != 1 || Root.HasIntProperty(idKey)
				|| Root.HasStringProperty(stateKey))
				return FailSlot("A legacy gatehouse satellite receipt has the wrong value type.",
					out Failure);
			for (int transition = 0; transition < 4; transition++)
			{
				string id = Root.GetStringProperty(idKey);
				int rawState = Root.GetIntProperty(stateKey);
				if (rawState < 0 || rawState > (int)KingdomGatehouseSlotState.Contested
					|| (!string.IsNullOrEmpty(id)
						&& !KingdomGatehouseProjectionRules.ExactStoredSatelliteId(false,
							Root.IDIfAssigned, PlanReceipt, Index, id)))
					return ContestSlot(Root, Index,
						"A legacy gatehouse satellite identity or phase is malformed.", out Failure);
				KingdomGatehouseSlotState state = (KingdomGatehouseSlotState)rawState;
				if (state == KingdomGatehouseSlotState.Empty && string.IsNullOrEmpty(id))
				{
					GameObject staged = ProjectionCustody(Part, Index);
					if (staged != null)
					{
						if (!TryAdoptUnpublishedLegacyCustody(Root, RootCell, Job,
							Scaffold, Z, Plan, PlanReceipt, Part, Index, Spec, staged,
							out Failure)) return false;
						continue;
					}
					if (Part == null || KingdomGatehouseProjectionRules.
						ResolveLegacyPublicationCut(Index, state, false, false,
							false, false, false, false, false,
							KingdomGatehouseSlotEvidence.Absent)
							!= KingdomGatehouseLegacyPublicationAction.Create
						|| !TryCreateLegacyStagedSatellite(Root, RootCell,
						Job, Scaffold, Z, Plan, PlanReceipt, Part, Index, Spec, out Failure))
						return false;
					continue;
				}
				if (string.IsNullOrEmpty(id)
					|| !TryProjectionEvidence(Root, Z, Plan, Part, Index, id,
						out KingdomGatehouseSlotEvidence evidence, out GameObject item,
						out Failure))
					return ContestSlot(Root, Index, Failure
						?? "A legacy gatehouse satellite lacks exact stored identity.", out Failure);
				switch (state)
				{
				case KingdomGatehouseSlotState.Empty:
					if (Part == null || KingdomGatehouseProjectionRules.
						ResolveLegacyPublicationCut(Index, state, true, true,
							true, true, true, true, true,
							evidence) != KingdomGatehouseLegacyPublicationAction.PublishPending)
						return ContestSlot(Root, Index,
							"Legacy identity publication lacks its staged body.", out Failure);
					Root.SetIntProperty(stateKey, (int)KingdomGatehouseSlotState.Pending);
					if (Root.GetIntProperty(stateKey)
						!= (int)KingdomGatehouseSlotState.Pending)
						return FailSlot("A legacy pending phase did not persist.", out Failure);
					continue;
				case KingdomGatehouseSlotState.Pending:
					if (evidence == KingdomGatehouseSlotEvidence.Staged && Part != null)
						return TryPlaceStagedSatellite(Root, RootCell, Job, Scaffold, Z,
							Plan, PlanReceipt, Part, Index, Spec, id, item, out Failure);
					if (evidence == KingdomGatehouseSlotEvidence.ExactPlacement && Part != null)
						return SettleProjectionSlot(Root, Part, Index, item, out Failure);
					return ContestSlot(Root, Index,
						"A legacy pending satellite is absent, foreign, or duplicated.", out Failure);
				case KingdomGatehouseSlotState.Settled:
					if (evidence != KingdomGatehouseSlotEvidence.ExactPlacement)
						return ContestSlot(Root, Index,
							"A settled legacy satellite lost exact physical evidence.", out Failure);
					GameObject custody = ProjectionCustody(Part, Index);
					if (custody != null && !ReferenceEquals(custody, item))
						return ContestSlot(Root, Index,
							"A settled legacy slot retains foreign callback custody.", out Failure);
					return Part == null || SetProjectionCustody(Part, Index, null)
						|| FailSlot("Settled legacy callback custody did not clear.", out Failure);
				default:
					return ContestSlot(Root, Index,
						"A legacy gatehouse satellite is contested.", out Failure);
				}
			}
			return FailSlot("A legacy gatehouse satellite transition did not converge.",
				out Failure);
		}

	}
}
