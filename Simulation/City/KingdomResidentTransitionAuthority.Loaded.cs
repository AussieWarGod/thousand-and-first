using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentTransitionAuthority
	{
		private static bool TryProjectLoadedClaims(KingdomSystem System, GameObject Body,
			int ResidentId, KingdomResidentDestructionAuthorization Authorization,
			ref KingdomResidentTransitionClaim Claims, out bool ExactLabAuthorization)
		{
			ExactLabAuthorization = false;
			if (!KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> loaded))
				return false;
			string objectId = Body.IDIfAssigned;
			int labSources = 0, exactMarkerSources = 0, terminalMarkerSources = 0;
			bool authorizedLab = false;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (item == null) continue;
				ProjectLoadedMarketClaim(item, objectId, ResidentId, ref Claims);
				if (!ProjectLoadedStasisClaim(item, objectId, ref Claims)) return false;
				ProjectLoadedBountyClaim(System, item, ResidentId, ref Claims);
				ProjectLoadedKeeperClaim(item, objectId, ResidentId, ref Claims);
				ProjectLoadedLabClaim(System, item, Body, objectId, ResidentId,
					Authorization, ref Claims, ref labSources, ref exactMarkerSources,
					ref terminalMarkerSources, ref authorizedLab);
			}
			string labEvent = Body.GetStringProperty(KingdomLabCivicRuntime.RefusalEventProperty);
			string labOwner = Body.GetStringProperty(KingdomLabCivicRuntime.RefusalOwnerProperty);
			string labDigest = Body.GetStringProperty(KingdomLabCivicRuntime.RefusalDigestProperty);
			bool anyMarker = !string.IsNullOrEmpty(labEvent)
				|| !string.IsNullOrEmpty(labOwner) || !string.IsNullOrEmpty(labDigest);
			bool wholeMarker = !string.IsNullOrEmpty(labEvent)
				&& !string.IsNullOrEmpty(labOwner) && !string.IsNullOrEmpty(labDigest);
			if (anyMarker && terminalMarkerSources != 1)
				Claims |= KingdomResidentTransitionClaim.LabRefusalDeparture;
			if (labSources > 1 || anyMarker && (!wholeMarker
				|| exactMarkerSources + terminalMarkerSources != 1))
				Claims |= KingdomResidentTransitionClaim.AuthorityUnproved;
			ExactLabAuthorization = authorizedLab && labSources == 1
				&& exactMarkerSources == 1;
			if (Authorization.Kind != KingdomResidentDestructionAuthorizationKind.None
				&& !ExactLabAuthorization) return false;
			return true;
		}

		private static void ProjectLoadedLabClaim(KingdomSystem System, GameObject Item,
			GameObject Body, string ObjectId, int ResidentId,
			KingdomResidentDestructionAuthorization Authorization,
			ref KingdomResidentTransitionClaim Claims, ref int Sources,
			ref int ExactMarkerSources, ref int TerminalMarkerSources, ref bool Authorized)
		{
			r_KingdomLabCivicFriction part = Item.GetPart<r_KingdomLabCivicFriction>();
			KingdomLabCivicReceipt receipt = part?.RefusalDeparture;
			if (receipt == null || receipt.Kind == KingdomLabCivicKind.None) return;
			bool target = receipt.SubjectResidentId == ResidentId
				|| string.Equals(receipt.SubjectObjectId, ObjectId, StringComparison.Ordinal);
			if (!target) return;
			bool valid = KingdomLabCivicRules.Valid(receipt, out string _);
			bool marker = valid && string.Equals(Body.GetStringProperty(
				KingdomLabCivicRuntime.RefusalEventProperty), receipt.EventId,
				StringComparison.Ordinal) && string.Equals(Body.GetStringProperty(
				KingdomLabCivicRuntime.RefusalOwnerProperty), receipt.OwnerObjectId,
				StringComparison.Ordinal) && string.Equals(Body.GetStringProperty(
				KingdomLabCivicRuntime.RefusalDigestProperty), receipt.CauseDigest,
				StringComparison.Ordinal);
			if (valid && receipt.Phase == KingdomLabCivicPhase.Closed)
			{
				if (marker) TerminalMarkerSources++;
				return;
			}
			Claims |= KingdomResidentTransitionClaim.LabRefusalDeparture;
			if (!valid || receipt.Kind != KingdomLabCivicKind.RefusalDeparture
				|| receipt.Phase != KingdomLabCivicPhase.Active
					&& receipt.Phase != KingdomLabCivicPhase.Quarantined
				|| receipt.SubjectResidentId != ResidentId
				|| !string.Equals(receipt.SubjectObjectId, ObjectId,
					StringComparison.Ordinal)
				|| !string.Equals(receipt.SubjectName,
					Body.GetStringProperty("KingdomName"), StringComparison.Ordinal))
			{
				Claims |= KingdomResidentTransitionClaim.AuthorityUnproved;
				return;
			}
			Sources++;
			if (!marker) return;
			ExactMarkerSources++;
			if (receipt.Phase == KingdomLabCivicPhase.Active
				&& KingdomLabCivicRuntime.ReadOnlyDepartureAuthorizationMatches(System,
					Item, Body, receipt, Authorization)) Authorized = true;
		}

		private static void ProjectLoadedMarketClaim(GameObject Item, string ObjectId,
			int ResidentId, ref KingdomResidentTransitionClaim Claims)
		{
			r_KingdomLegendaryMarketProjection target =
				Item.GetPart<r_KingdomLegendaryMarketProjection>();
			if (target?.HandoffPrepared == 1
				&& (target.BodyObjectId == ObjectId || target.PriorBodyObjectId == ObjectId
					|| target.HandoffResidentId == ResidentId
					|| target.PriorResidentId == ResidentId))
				Claims |= KingdomResidentTransitionClaim.PreparedMarketHandoff;
			r_KingdomMarketHandoffSourceProjection source =
				Item.GetPart<r_KingdomMarketHandoffSourceProjection>();
			if (source != null && (source.SourceBodyObjectId == ObjectId
				|| source.TargetBodyObjectId == ObjectId
				|| source.SourceResidentId == ResidentId
				|| source.TargetResidentId == ResidentId))
				Claims |= KingdomResidentTransitionClaim.PreparedMarketHandoff;
		}

		private static bool ProjectLoadedStasisClaim(GameObject Item, string ObjectId,
			ref KingdomResidentTransitionClaim Claims)
		{
			r_KingdomStasisVault vault = Item.GetPart<r_KingdomStasisVault>();
			if (vault == null) return true;
			if (vault.Slots == null || vault.Slots.Count > KingdomStasisVaultRules.MaxSlots)
				return false;
			bool[] slots = new bool[KingdomStasisVaultRules.MaxSlots];
			for (int i = 0; i < vault.Slots.Count; i++)
			{
				KingdomStasisCustodyReceipt receipt = vault.Slots[i];
				if (!KingdomStasisVaultRules.Validate(receipt, out string _)
					|| slots[receipt.Slot]) return false;
				slots[receipt.Slot] = true;
				if (receipt.Phase != KingdomStasisCustodyPhase.Released
					&& string.Equals(receipt.BodyObjectId, ObjectId,
						StringComparison.Ordinal))
				{
					Claims |= KingdomResidentTransitionClaim.StasisCustody;
					break;
				}
			}
			return true;
		}

		private static void ProjectLoadedBountyClaim(KingdomSystem System, GameObject Item,
			int ResidentId, ref KingdomResidentTransitionClaim Claims)
		{
			r_KingdomNotice notice = Item.GetPart<r_KingdomNotice>();
			if (notice == null || Item.CurrentZone == null
				|| !System.OwnedZone(Item.CurrentZone.ZoneID)
				|| notice.TaskCode != (int)BountyTask.Manning || notice.Done
				|| string.IsNullOrEmpty(notice.WorkerName)
				|| notice.WorkerResidentId != ResidentId) return;
			Claims |= KingdomResidentTransitionClaim.BountyManning;
		}

		private static void ProjectLoadedKeeperClaim(GameObject Item, string ObjectId,
			int ResidentId, ref KingdomResidentTransitionClaim Claims)
		{
			r_KingdomLocusAmbient ambient = Item.GetPart<r_KingdomLocusAmbient>();
			if (ambient != null && ambient.AuthorityEnabled
				&& (ambient.KeeperResidentId == ResidentId
					|| string.Equals(ambient.KeeperObjectId, ObjectId,
						StringComparison.Ordinal)))
				Claims |= KingdomResidentTransitionClaim.Keeper;
		}
	}
}
