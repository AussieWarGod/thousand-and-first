using System;
using System.Threading;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.ZoneBuilders;

namespace ThousandAndFirst
{
	/// <summary>
	/// Physical, monotone bootstrap for Kingdom Quickstart. Every phase is measured before its
	/// receipt advances; a malformed or disagreeing receipt stops without replacement grants.
	/// </summary>
	public static partial class KingdomQuickstartBootstrap
	{
		private static int Active;

		public static bool Run(XRLGame Game, out string Failure)
		{
			Failure = "";
			if (Interlocked.CompareExchange(ref Active, 1, 0) != 0)
			{
				Failure = "A nested quickstart callback was refused.";
				return false;
			}
			try
			{
				bool completedNow;
				int shelterLots;
				int founders;
				if (!RunCore(Game, out completedNow, out shelterLots, out founders,
					out Failure)) return false;
				if (completedNow)
					Popup.Show("{{W|Your kingdom stands.}} The founder's casks hold "
						+ KingdomQuickstartRules.StarterWaterDrams + " drams of water, the larder "
						+ "holds " + KingdomQuickstartRules.StarterFoodServings
						+ " meals, and the materials chest holds only what you can see. None of "
						+ "these stores produces replacements."
						+ (shelterLots > 0
							? " " + shelterLots + " tent-row lot"
								+ (shelterLots == 1 ? " is" : "s are") + " staked west of them,"
								+ " three beds to a row once it stands. Raising runs over the"
								+ " first days, not by nightfall, and only at the day boundaries"
								+ " you spend on this claimed ground."
							: "")
						+ (founders > 0 ? FoundersArrived(founders) : ""));
				else if (founders > 0)
					// The cohort was raised on a later wake, after the completion notice had already been
					// given. It is still the once-only arrival of four named citizens, and the founder is
					// told about it exactly where it happened.
					Popup.Show("{{W|Your founding party has arrived.}}" + FoundersArrived(founders));
				return true;
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst quickstart bootstrap", ex);
				Failure = "The bootstrap failed closed (" + ex.GetType().Name + ": "
					+ ex.Message + ").";
				return false;
			}
			finally
			{
				Volatile.Write(ref Active, 0);
			}
		}

		/// <summary>The arrival sentence, said in exactly one wording wherever it is said.</summary>
		private static string FoundersArrived(int Founders)
		{
			return " " + Founders + " founding citizens stand on the approach,"
				+ " already on your roll and able to work. Until a row stands"
				+ " they sleep rough, and the charter will say so.";
		}

		private static bool RunCore(XRLGame Game, out bool CompletedNow,
			out int ShelterLots, out int Founders, out string Failure)
		{
			CompletedNow = false;
			ShelterLots = 0;
			Founders = 0;
			Failure = "";
			if (GrantQuarantined(Game))
			{
				Failure = "Quickstart grant custody is quarantined; replacement grants are forbidden.";
				return false;
			}
			KingdomQuickstartProfile profile = null;
			Zone zone = The.ZoneManager?.ActiveZone;
			GameObject founder = The.Player;
			Cell playerCell = founder?.CurrentCell;
			string raw = Game?.GetStringGameState(KingdomQuickstartRules.ReceiptState, null);
			bool continuation = KingdomQuickstartRules.TryDecode(raw,
				out KingdomQuickstartReceipt observedReceipt)
				&& observedReceipt.Phase >= KingdomQuickstartPhase.Founded;
			if (Game == null || !KingdomQuickstartRules.IsMode(Game.gameMode)
				|| !Game.GetBooleanGameState("r_TAF_KingdomMode")
				|| !KingdomMaster.ConfiguredEnabled
				|| !KingdomQuickstartRules.TryProfile(Game.GetStringGameState(
					KingdomQuickstartRules.ProfileState, null), out profile)
				|| !KingdomQuickstartRules.WorldReservationMatches(Game.GetStringGameState(
					KingdomQuickstartRules.WorldReservationState, null), profile)
				|| zone == null || !string.Equals(zone.ZoneID, profile.ZoneId,
					StringComparison.Ordinal)
				|| playerCell == null || playerCell.ParentZone != zone
				|| ((!continuation
					|| !string.Equals(observedReceipt.ProfileKey, profile.Key,
						StringComparison.Ordinal)
					|| !string.Equals(observedReceipt.ZoneId, zone.ZoneID,
						StringComparison.Ordinal))
					&& (playerCell.X != KingdomQuickstartRules.StartCellX
						|| playerCell.Y != KingdomQuickstartRules.StartCellY)))
			{
				Failure = "The selected profile, reserved ground, master option, and placed player did not agree.";
				return false;
			}
			if (KingdomInheritanceState.Instance != null
				&& KingdomInheritanceState.Instance.Phase != KingdomInheritancePhase.Empty)
			{
				Failure = "A legacy inheritance offer was already active; quickstart will not compete with it.";
				return false;
			}

			KingdomSystem system = Game.RequireSystem<KingdomSystem>();
			KingdomQuickstartReceipt receipt;
			if (string.IsNullOrEmpty(raw))
			{
				if (system.Founded)
				{
					Failure = "A realm existed before the quickstart receipt reserved its first step.";
					return false;
				}
				if (!KingdomQuickstartCampBuilder.ReadyForFounder(zone, founder, out string groundFailure))
				{
					Failure = "The bounded heart apron or supply path was not safely prepared: " + groundFailure + ".";
					return false;
				}
				// The founders option is read ONCE, here, before the receipt is ever published,
				// and frozen for the life of the world. An option-off world is stamped Omitted,
				// which encodes on the old wire and is terminal at Complete on every later wake:
				// it can never be re-attempted, and turning the option on later cannot seed it.
				if (!KingdomQuickstartRules.TryCreateReceipt(profile.Key, zone.ZoneID,
					Options.GetOption(KingdomQuickstartRules.FoundersOption, "Yes") != "No"
						? KingdomQuickstartFoundersDisposition.Pending
						: KingdomQuickstartFoundersDisposition.Omitted,
					out receipt) || !Publish(Game, receipt, out Failure)) return false;
			}
			else if (!KingdomQuickstartRules.TryDecode(raw, out receipt)
				|| !string.Equals(receipt.ProfileKey, profile.Key, StringComparison.Ordinal)
				|| !string.Equals(receipt.ZoneId, zone.ZoneID, StringComparison.Ordinal))
			{
				Failure = "The quickstart receipt was malformed or belonged to different ground.";
				return false;
			}

			// A receipt at Complete or above has every store it will ever get. What is left is the
			// founding cohort, which may be owed, half-seeded, done, refused or faulted; the one
			// terminal predicate decides which of those still wants a wake.
			if (receipt.Phase >= KingdomQuickstartPhase.Complete)
			{
				if (!VerifyComplete(system, zone, receipt, out Failure)) return false;
				// A refused cohort never fails the bootstrap: every store is already standing, and
				// RunFounders says what went wrong itself, once.
				RunFounders(Game, system, zone, ref receipt, out Founders);
				return true;
			}

			if (receipt.Phase == KingdomQuickstartPhase.Reserved)
			{
				if (!system.Founded)
				{
					Faction faction;
					if (!KingdomFoundingTransaction.TryFoundFirstWithoutWater(profile.CityName,
						zone, out faction, out Failure))
					{
						Failure = "Normal founding authority refused the quickstart: " + Failure;
						return false;
					}
				}
				if (!VerifyFounded(system, zone, profile, out Failure)) return false;
				// Two tent-row lots, staked on the founded ground before the receipt moves: without
				// a standing roof nobody joins, and nothing commissioned rises while nobody has.
				// Only a receipt minted by this version carries that obligation. A receipt written
				// before the lots existed resumes here on ground the older prepared-ground mask
				// never bared, where the authored-ground preflight may lawfully refuse; its water,
				// larder, materials and advisor must not be lost to a roof it was never promised.
				if (receipt.ShelterObligation
					&& !TryStakeShelter(system, zone, out Failure)) return false;
				string crop = KingdomData.CropForStyle(system.Style);
				if (string.IsNullOrEmpty(crop)
					|| GameObjectFactory.Factory.GetBlueprintIfExists(crop) == null)
				{
					Failure = "The founded style had no physical food blueprint.";
					return false;
				}
				if (!Advance(Game, ref receipt, KingdomQuickstartPhase.Founded, crop,
					KingdomQuickstartAdvisorDisposition.Unresolved, out Failure)) return false;
			}

			if (!VerifyFounded(system, zone, profile, out Failure)
				|| !string.Equals(receipt.FoodBlueprint,
					KingdomData.CropForStyle(system.Style), StringComparison.Ordinal))
			{
				Failure = string.IsNullOrEmpty(Failure)
					? "The founded realm no longer matched its frozen quickstart food."
					: Failure;
				return false;
			}

			if (receipt.Phase == KingdomQuickstartPhase.Founded)
			{
				GameObject water = CreateWater(Game, zone, receipt, out Failure);
				if (water == null) return false;
				if (!VerifyWaterGrant(zone, water, receipt, true, out Failure)) return false;
				if (!Advance(Game, ref receipt, KingdomQuickstartPhase.WaterStocked,
					water.IDIfAssigned, KingdomQuickstartAdvisorDisposition.Unresolved,
					out Failure)) return false;
			}
			GameObject receiptedWater = zone.FindObjectByID(receipt.WaterObjectId);
			if (!VerifyWaterGrant(zone, receiptedWater, receipt, false,
				out Failure)) return false;

			if (receipt.Phase == KingdomQuickstartPhase.WaterStocked)
			{
				GameObject larder = CreateLarder(Game, zone, receipt, out Failure);
				if (larder == null) return false;
				if (!VerifyLarderGrant(zone, larder, receipt, true, out Failure)) return false;
				if (!Advance(Game, ref receipt, KingdomQuickstartPhase.FoodStocked,
					larder.IDIfAssigned, KingdomQuickstartAdvisorDisposition.Unresolved,
					out Failure)) return false;
			}
			GameObject receiptedLarder = zone.FindObjectByID(receipt.LarderObjectId);
			if (!VerifyLarderGrant(zone, receiptedLarder, receipt, false,
				out Failure)) return false;

			if (receipt.Phase == KingdomQuickstartPhase.FoodStocked)
			{
				GameObject stockpile = CreateMaterials(Game, zone, receipt, out Failure);
				if (stockpile == null) return false;
				if (!VerifyMaterialsGrant(zone, stockpile, receipt, true,
					out Failure)) return false;
				if (!Advance(Game, ref receipt, KingdomQuickstartPhase.MaterialsStocked,
					stockpile.IDIfAssigned, KingdomQuickstartAdvisorDisposition.Unresolved,
					out Failure)) return false;
			}
			GameObject receiptedMaterials = zone.FindObjectByID(receipt.StockpileObjectId);
			if (!VerifyMaterialsGrant(zone, receiptedMaterials, receipt, false,
				out Failure)) return false;

			if (receipt.Phase == KingdomQuickstartPhase.MaterialsStocked)
			{
				if (!TryResolveAdvisor(Game, zone, profile, receipt, out GameObject advisor,
					out KingdomQuickstartAdvisorDisposition disposition,
					out Failure)) return false;
				if (!Advance(Game, ref receipt, KingdomQuickstartPhase.AdvisorResolved,
					disposition == KingdomQuickstartAdvisorDisposition.Included
						? advisor.IDIfAssigned : "", disposition, out Failure)) return false;
			}
			if (receipt.AdvisorDisposition == KingdomQuickstartAdvisorDisposition.Included
				&& !VerifyAdvisor(zone, zone.FindObjectByID(receipt.AdvisorObjectId),
					receipt, out Failure)) return false;

			if (receipt.Phase == KingdomQuickstartPhase.AdvisorResolved)
				if (!Advance(Game, ref receipt, KingdomQuickstartPhase.Complete, "",
					KingdomQuickstartAdvisorDisposition.Unresolved, out Failure)) return false;
			if (!VerifyComplete(system, zone, receipt, out Failure)) return false;
			RunFounders(Game, system, zone, ref receipt, out Founders);
			CompletedNow = true;
			// Read the ground, not the branch that ran. A save cut past the Reserved phase resumes
			// straight through to Complete without ever staking a lot, so the completion notice may
			// only name the tent rows a claim is actually standing on here.
			ShelterLots = ShelterLotsClaimed(zone);
			return true;
		}

	}
}
