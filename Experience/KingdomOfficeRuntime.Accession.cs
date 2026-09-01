using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomOfficeRuntime
	{
		/// <summary>Closes an exact office before accession erases its resident identity. Re-entry
		/// recognizes the prepared vacancy after cuts in projection cleanup or stock-mark release.</summary>
		internal static bool TryObserveAccessionLoss(KingdomSystem System, GameObject Body,
			out string Failure)
		{
			Failure = null;
			if (System == null || !GameObject.Validate(Body))
				{ Failure = "accession office cleanup lacks its exact body or realm"; return false; }
			string objectId = Body.IDIfAssigned;
			int bodyResidentId = KingdomResidents.IdOf(Body);
			KingdomCivicOfficeReceipt receipt = null;
			for (int i = 0; System.Experience != null
				&& i < System.Experience.Offices.Count; i++)
			{
				KingdomCivicOfficeReceipt row = System.Experience.Offices[i];
				if (row == null || row.HolderObjectId != objectId
					|| (row.Phase != KingdomCivicOfficePhase.Held
						&& row.Phase != KingdomCivicOfficePhase.AppointmentPrepared
						&& row.Phase != KingdomCivicOfficePhase.VacancyPrepared)) continue;
				if (bodyResidentId > 0 && row.HolderResidentId != bodyResidentId)
					{ Failure = "accession office receipt has divergent resident identity"; return false; }
				if (receipt != null)
					{ Failure = "accession body is claimed by more than one office"; return false; }
				receipt = row;
			}
			r_KingdomOfficeProjection office = Body.GetPart<r_KingdomOfficeProjection>();
			r_KingdomLegendaryMarketProjection legend =
				Body.GetPart<r_KingdomLegendaryMarketProjection>();
			bool stock = false;
			for (int i = 0; Body.Inventory != null && i < Body.Inventory.Objects.Count; i++)
				if (KingdomMarketStockProtection.HasProjection(Body.Inventory.Objects[i]))
					{ stock = true; break; }
			KingdomMarketAccessionAuthority authority =
				KingdomShopStockRules.ClassifyAccessionAuthority(legend != null,
					office != null, receipt != null, stock);
			if (authority == KingdomMarketAccessionAuthority.RefusedCompetingOwners
				|| authority == KingdomMarketAccessionAuthority.RefusedOfficeWithoutReceipt
				|| authority == KingdomMarketAccessionAuthority.RefusedOrphanedStock)
			{
				Failure = "accession market authority is competing, unreceipted, or orphaned";
				return false;
			}
			if (authority == KingdomMarketAccessionAuthority.Legendary
				&& !KingdomMarketStockCustody.TryRetireAccedingLegendary(System, Body,
					out bool _, out Failure)) return false;
			if (authority == KingdomMarketAccessionAuthority.Legendary
				|| authority == KingdomMarketAccessionAuthority.None) return true;
			if (System.Experience == null)
				{ Failure = "accession office cleanup lacks its civic ledger"; return false; }
			if (receipt == null)
				{ Failure = "accession body lacks its exact office receipt"; return false; }
			if (receipt.Phase != KingdomCivicOfficePhase.VacancyPrepared)
			{
				if (!KingdomExperienceRules.TryPrepareOfficeVacancy(System.Experience,
					System.Experience.Revision, receipt.SettlementId, receipt.HolderResidentId,
					KingdomCivicOfficeVacancyCause.AuthorityLost, Now(), out Failure)) return false;
				if (!KingdomExperienceRules.TryGetOffice(System.Experience, receipt.SettlementId,
					out receipt, out Failure)) return false;
			}
			if (receipt == null || receipt.Phase != KingdomCivicOfficePhase.VacancyPrepared
				|| receipt.HolderObjectId != objectId || receipt.HolderResidentId <= 0
				|| receipt.VacancyCause == KingdomCivicOfficeVacancyCause.Death)
				{ Failure = "accession office vacancy lacks exact frozen authority"; return false; }

			r_KingdomOfficeProjection marker = office;
			if (marker != null)
			{
				if (!CleanupProjection(System, receipt, Body, out Failure)) return false;
			}
			else
			{
				if (Body.HasIntProperty("Merchant") || Body.HasIntProperty("VillageMerchant")
					|| Body.HasIntProperty("InventoryTier")
					|| receipt.OwnsRole && HasRole(Body.GetPart<SocialRoles>(), RoleFor(receipt)))
				{
					Failure = "accession office cleanup residue is divergent"; return false;
				}
			}
			if (!KingdomMarketStockCustody.TryRetireAccedingHolder(System,
				receipt.SettlementId, Body, out Failure)) return false;
			if (!KingdomExperienceRules.TryCompleteOfficeVacancy(System.Experience,
				System.Experience.Revision, receipt.SettlementId, receipt.Generation,
				out Failure)) return false;
			KingdomExperienceRules.TryGetOffice(System.Experience, receipt.SettlementId,
				out KingdomCivicOfficeReceipt vacant, out string _);
			ProjectCompatibility(System, vacant); TellVacant(System, vacant);
			return true;
		}
	}
}
