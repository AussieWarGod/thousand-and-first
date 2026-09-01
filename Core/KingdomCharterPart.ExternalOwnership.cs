using System;
using ThousandAndFirst.Api;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		private bool ExternalOwnershipActionAllowed(KingdomSystem System,
			KingdomCharterAction Action, out string Failure)
		{
			Failure = null;
			Zone site = ParentObject?.CurrentZone;
			if (site == null || System == null ||
				!System.ClaimedZones.Contains(site.ZoneID) || IsOwnershipReport(Action))
				return true;
			KingdomExternalOwnershipBindingRuntime.FinishPublishedClaimStage(site);
			return KingdomExternalOwnershipBindingRuntime.CanOperate(site, out Failure);
		}

		private static bool IsOwnershipReport(KingdomCharterAction Action)
		{
			switch (Action)
			{
			case KingdomCharterAction.Status:
			case KingdomCharterAction.Homecoming:
			case KingdomCharterAction.ChronicleAndDynasty:
			case KingdomCharterAction.OutsiderChronicle:
			case KingdomCharterAction.Standings:
			case KingdomCharterAction.SettlerRoll:
			case KingdomCharterAction.CityBook:
			case KingdomCharterAction.TechMap:
			case KingdomCharterAction.CityAsks:
			case KingdomCharterAction.BodyHistory:
			case KingdomCharterAction.GuestFeastRecord:
			case KingdomCharterAction.CivicCommitments:
			case KingdomCharterAction.InspectBuildingBenefits:
			case KingdomCharterAction.TrafficRecords:
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// Opens D9's four-owner reading from exact objects already present in the founder's
		/// current zone. Remote or ambiguous ground remains visibly unavailable.
		/// </summary>
		private void OpenCivicCommitments(KingdomSystem System)
		{
			Zone zone = ParentObject?.CurrentZone;
			if (System == null || !System.Founded || zone == null
				|| !System.OwnedZone(zone.ZoneID))
			{
				Popup.Show("Civic commitments can only be read on exact loaded realm ground.");
				return;
			}
			GameObject moot = CurrentMoot(System, zone);
			GameObject enclave = CurrentEnclave(System, zone);
			if (!KingdomJointCivicViewRuntime.TryRead(System, zone, moot, enclave,
				out KingdomJointCivicView view, out string failure))
			{
				Popup.Show("Civic commitments are unavailable. Nothing changed.\n\n"
					+ KingdomPresentation.Rich(failure));
				return;
			}
			Popup.Show("{{W|Civic commitments}}\n\n"
				+ CivicOwner("Creed", view.Creed) + "\n\n"
				+ CivicOwner("Village covenants", view.Covenant) + "\n\n"
				+ CivicOwner("Assenting moot", view.Moot) + "\n\n"
				+ CivicOwner("Hosted enclave", view.Enclave));
		}

		private static GameObject CurrentMoot(KingdomSystem System, Zone Zone)
		{
			string settlementId = System.SettlementIdForOwnedZone(Zone.ZoneID);
			if (string.IsNullOrEmpty(settlementId)
				|| !System.TryFindSettlement(settlementId, out bool seated,
					out KingdomSettlement settlement)) return null;
			Simulation.City.KingdomCityBook book = seated ? System.City : settlement?.City;
			KingdomAssentingMootReceipt row = book?.AssentingMoot;
			if (row == null || !string.Equals(row.ZoneId, Zone.ZoneID,
				StringComparison.Ordinal) || string.IsNullOrEmpty(row.BuildingObjectId)) return null;
			return Zone.FindObjectByID(row.BuildingObjectId);
		}

		private static GameObject CurrentEnclave(KingdomSystem System, Zone Zone)
		{
			if (!KingdomHostedArcology.TryReadAuthorityIdentityForJointView(System,
				out KingdomHostedArcologyAuthority authority, out _, out _)) return null;
			if (authority == null || authority.Phase != KingdomHostedAuthorityPhase.Active
				|| !string.Equals(authority.ZoneId, Zone.ZoneID, StringComparison.Ordinal)
				|| string.IsNullOrEmpty(authority.CarrierId)) return null;
			return Zone.FindObjectByID(authority.CarrierId);
		}

		private static string CivicOwner(string Label, KingdomJointCivicOwnerView Owner)
		{
			if (Owner == null) return "{{r|" + Label + ": malformed view}}";
			if (Owner.State == KingdomJointOwnerState.Valid)
				return "{{G|" + Label + "}}\n" + Owner.Text;
			string color = Owner.State == KingdomJointOwnerState.Absent ? "K" : "r";
			return "{{" + color + "|" + Label + ": " + Owner.Failure + "}}";
		}

		private static bool ExternalClaimPublicationObserved(KingdomSystem System,
			Zone Site)
		{
			if (System == null || Site == null) return false;
			Faction realm = Factions.GetIfExists(System.KingdomFactionName);
			return System.ClaimedZones.Contains(Site.ZoneID)
				|| Site.GetZoneProperty("faction", null) == System.KingdomFactionName
				|| Site.HasZoneProperty(
					KingdomFoundingTransaction.ClaimChronicleEventProperty)
				|| (realm != null && realm.HolyPlaces.Contains(Site.ZoneID));
		}

		private static bool ResumeExternalClaimIfNeeded(KingdomSystem System, Zone Site)
		{
			if (!KingdomExternalOwnershipBindingRuntime.HasStage(Site)
				|| KingdomFoundingTransaction.HasSiteReservation(Site)) return false;
			string expected = KingdomExternalOwnershipBindingRuntime.ClaimAuthority(
				System.KingdomFactionName, Site.ZoneID);
			if (!KingdomExternalOwnershipBindingRuntime.TryReadStage(Site,
					out string authority, out string encoded))
			{
				encoded = Site.GetZoneProperty(
					KingdomExternalOwnershipBindingRuntime.StageProperty, null);
				string repairFailure = null;
				if (!KingdomExternalOwnershipRules.TryDecode(encoded, out _)
					|| !KingdomExternalOwnershipBindingRuntime.TryStage(
						Site, expected, encoded, out repairFailure))
				{
					Popup.Show("The external-owner claim receipt is malformed and was left unchanged. "
						+ repairFailure);
					return true;
				}
				authority = expected;
			}
			if (authority != expected) return false;
			if (!KingdomExternalOwnershipBindingRuntime.RevalidateStage(
					Site, authority, encoded, out string failure)
				|| !KingdomExternalOwnershipBindingRuntime.TryCommit(
					Site, authority, encoded, out failure))
			{
				Popup.Show("The pending claim remains paused: " + failure);
				return true;
			}
			bool claimed = false;
			try
			{
				claimed = KingdomFounding.ClaimZone(Site);
			}
			catch (Exception ex)
			{
				failure = ex.Message;
				KingdomLog.Log("external claim recovery remains pending: " + ex.Message);
			}
			if (!claimed)
			{
				bool partial = ExternalClaimPublicationObserved(System, Site);
				if (!partial) KingdomExternalOwnershipBindingRuntime.RollbackStage(
					Site, authority, encoded, PublicationObserved: false);
				Popup.Show(partial ? "The claim remains partly published: " + failure
					: "The uncommitted claim was rolled back: " + failure);
				return true;
			}
			if (!KingdomExternalOwnershipBindingRuntime.FinishStage(
				Site, authority, encoded))
			{
				Popup.Show("The claim stands; its external-owner cleanup receipt remains.");
				return true;
			}
			KingdomGovernanceScope.Commit("claim ground recovery");
			Popup.Show("{{G|The interrupted claim and its exact external-owner binding now stand.}}");
			return true;
		}
	}
}
