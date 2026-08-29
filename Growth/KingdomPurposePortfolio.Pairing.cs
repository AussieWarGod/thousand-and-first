using System;
using System.Collections.Generic;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		private static void OfferPair(KingdomSystem System, GameObject Work,
			KingdomPurposeKind FirstKind, KingdomPurposePairReceipt Dormant)
		{
			if (!KingdomMaster.NewWorkAllowed(System))
			{
				Popup.Show("Kingdom work is paused. Standing purposeful works keep their exact custody, but no new purpose pair is frozen until the realm resumes.");
				return;
			}
			Zone zone = Work.CurrentZone;
			string failure = null;
			string workId = Work.IDIfAssigned;
			if (zone == null || !TrySettlementIdentity(System, zone.ZoneID, out string firstCity)
				|| !TryStandingPurpose(zone, out KingdomPurposeKind localKind,
					out GameObject localWork, out failure)
				|| string.IsNullOrEmpty(workId) || localKind != FirstKind
				|| localWork?.IDIfAssigned != workId
				|| !FindLocalConnection(System, zone, out KingdomPurposeConnection connection,
					out failure)
				|| !TrySettlementIdentity(System, connection.DestinationZone.ZoneID,
					out string secondCity))
			{
				Popup.Show(failure ?? "The reciprocal city and mirror route cannot be proved.");
				return;
			}
			GameObject firstInput;
			GameObject firstOutput;
			GameObject secondInput;
			GameObject secondOutput;
			bool firstLegacy;
			bool secondLegacy;
			if (!TryPurposeStores(zone, localWork, out firstInput, out firstOutput,
				out firstLegacy, out failure))
			{
				Popup.Show(failure ?? "The first purpose stores could not be bound.");
				return;
			}
			if (!TryStandingPurpose(connection.DestinationZone,
				out KingdomPurposeKind standingSecond, out GameObject secondWork, out failure))
			{
				Popup.Show(failure);
				return;
			}
			if (standingSecond != KingdomPurposeKind.None && Dormant == null)
			{
				Popup.Show("The other city already has a standing purpose. A first epoch must freeze its second commission before bootstrap; only a dissolved pair may reactivate two standing shells.");
				return;
			}
			if (!TryPurposeStores(connection.DestinationZone, secondWork,
				out secondInput, out secondOutput, out secondLegacy, out failure))
			{
				Popup.Show(failure ?? "The reciprocal purpose stores could not be bound.");
				return;
			}
			List<KingdomPurposeKind> choices = PairChoices(FirstKind, standingSecond,
				Dormant != null);
			if (choices.Count == 0)
			{
				Popup.Show("The other city's standing purpose is not a lawful neighbour of {{C|"
					+ KingdomPurposePortfolioRules.PurposeName(FirstKind)
					+ "}}. Its only lawful partners are " + PartnerList(FirstKind) + ".");
				return;
			}
			string[] options = new string[choices.Count];
			for (int i = 0; i < choices.Count; i++)
				options[i] = KingdomPurposePortfolioRules.PurposeName(choices[i]);
			int picked = Popup.PickOption(Title: "Freeze a reciprocal purpose pair",
				Intro: standingSecond == KingdomPurposeKind.None
					? "Choose the one purpose the other city will raise. Only cycle neighbours are offered; no cargo or stock is touched by this choice."
					: "Reactivate the compatible standing shell under a new epoch. Old cargo remains invalid; no cargo or stock is touched by this choice.",
				Options: options, AllowEscape: true);
			if (picked < 0) return;
			KingdomPurposeKind secondKind = choices[picked];
			if (!KingdomPurposePortfolioRules.TryRecipe(FirstKind, secondKind, out var outgoing)
				|| !KingdomPurposePortfolioRules.TryRecipe(secondKind, FirstKind, out var incoming)) return;
			if (Dormant != null && Dormant.Epoch == long.MaxValue)
			{
				Popup.Show("The dissolved purpose-pair epoch is exhausted. Nothing changed.");
				return;
			}
			long epoch = Dormant == null ? 1L : Dormant.Epoch + 1L;
			string firstInputId = firstInput.IDIfAssigned;
			string firstOutputId = firstOutput.IDIfAssigned;
			string secondInputId = secondInput.IDIfAssigned;
			string secondOutputId = secondOutput.IDIfAssigned;
			string secondWorkId = secondWork?.IDIfAssigned;
			if (string.IsNullOrEmpty(firstInputId) || string.IsNullOrEmpty(firstOutputId)
				|| string.IsNullOrEmpty(secondInputId) || string.IsNullOrEmpty(secondOutputId)
				|| (standingSecond != KingdomPurposeKind.None
					&& string.IsNullOrEmpty(secondWorkId)))
			{
				Popup.Show("The purpose pair lacks exact assigned physical identity. Nothing changed.");
				return;
			}
			if (!KingdomPurposePortfolioRules.TryRouteDigest(System.RealmId, firstCity,
				secondCity, connection.SourceKey, connection.DestinationKey, zone.ZoneID,
				connection.DestinationZone.ZoneID, firstInputId, firstOutputId,
				secondInputId, secondOutputId, out string routeDigest))
			{
				Popup.Show("The purpose route and its exact stores could not be authenticated. Nothing changed.");
				return;
			}
			string pairId = "purpose-" + PurposeDigest(System.RealmId,
				epoch.ToString(), workId, secondCity, ((int)secondKind).ToString()).Substring(0, 32);
			if (!KingdomPurposePortfolioRules.TryCreatePair(pairId, System.RealmId, epoch,
				FirstKind, secondKind, firstCity, secondCity, workId,
				secondWorkId, zone.ZoneID,
				connection.DestinationZone.ZoneID, firstInputId, firstOutputId,
				secondInputId, secondOutputId, connection.SourceKey,
				connection.DestinationKey, routeDigest, out var fresh, out _))
			{
				Popup.Show("The exact pair receipt could not be frozen. Nothing changed.");
				return;
			}
			string prompt = "Freeze epoch {{C|" + epoch + "}} between {{C|"
				+ KingdomPurposePortfolioRules.PurposeName(FirstKind) + "}} and {{C|"
				+ KingdomPurposePortfolioRules.PurposeName(secondKind) + "}}?\n\n"
				+ "Bootstrap: " + outgoing.WaterDrams + " drams, "
				+ ParseClaim(outgoing.MaterialClaim).Materials.Describe()
				+ (outgoing.FoodServings > 0 ? ", " + outgoing.FoodServings + " food" : "")
				+ " → {{C|" + outgoing.CargoKey + "}}."
				+ CarriageLine("Provision carried", outgoing, secondKind)
				+ PurposeEffectDisclosure(FirstKind)
				+ "\nReturn: " + incoming.WaterDrams + " drams, "
				+ ParseClaim(incoming.MaterialClaim).Materials.Describe()
				+ (incoming.FoodServings > 0 ? ", " + incoming.FoodServings + " food" : "")
				+ " → {{C|" + incoming.CargoKey + "}}."
				+ CarriageLine("Provision carried", incoming, FirstKind)
				+ PurposeEffectDisclosure(secondKind)
				+ DeclaredEffect(FirstKind) + DeclaredEffect(secondKind)
				+ "\nStore binding, first: " + PurposeStoreBinding(firstLegacy) + "."
				+ "\nStore binding, reciprocal: " + PurposeStoreBinding(secondLegacy) + "."
				+ "\n\nNo operation runs in the background, and cargo has no visit deadline.";
			if (Popup.ShowYesNo(prompt) != DialogResult.Yes) return;
			bool published = Dormant == null
				? TryPublishPortfolioPair(null, fresh, out failure)
				: TryReplaceDormantPair(Dormant, fresh, out failure);
			if (!published) Popup.Show(failure);
			else KingdomGovernanceScope.Commit("freeze purpose pair");
		}

		private static KingdomMaterialDebitCost ParseClaim(string Claim)
		{
			KingdomMaterialDebitCost.TryParseClaim(Claim, out var parsed);
			return parsed ?? new KingdomMaterialDebitCost();
		}
}
}
