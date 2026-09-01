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
				if (!RunCore(Game, out completedNow, out Failure)) return false;
				if (completedNow)
					Popup.Show("{{W|Your kingdom stands.}} The founder's casks hold "
						+ KingdomQuickstartRules.StarterWaterDrams + " drams of water, the larder "
						+ "holds " + KingdomQuickstartRules.StarterFoodServings
						+ " meals, and the materials chest holds only what you can see. None of "
						+ "these stores produces replacements.");
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

		private static bool RunCore(XRLGame Game, out bool CompletedNow,
			out string Failure)
		{
			CompletedNow = false;
			Failure = "";
			KingdomQuickstartProfile profile = null;
			Zone zone = The.ZoneManager?.ActiveZone;
			Cell playerCell = The.Player?.CurrentCell;
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
				if (!KingdomQuickstartCampBuilder.Ready(zone))
				{
					Failure = "The bounded heart apron or supply path was not safely prepared.";
					return false;
				}
				if (!KingdomQuickstartRules.TryCreateReceipt(profile.Key, zone.ZoneID,
					out receipt) || !Publish(Game, receipt, out Failure)) return false;
			}
			else if (!KingdomQuickstartRules.TryDecode(raw, out receipt)
				|| !string.Equals(receipt.ProfileKey, profile.Key, StringComparison.Ordinal)
				|| !string.Equals(receipt.ZoneId, zone.ZoneID, StringComparison.Ordinal))
			{
				Failure = "The quickstart receipt was malformed or belonged to different ground.";
				return false;
			}

			if (receipt.Phase == KingdomQuickstartPhase.Complete)
				return VerifyComplete(system, zone, receipt, out Failure);

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
				GameObject water = CreateWater(zone, receipt, out Failure);
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
				GameObject larder = CreateLarder(zone, receipt, out Failure);
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
				GameObject stockpile = CreateMaterials(zone, receipt, out Failure);
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
				if (!TryResolveAdvisor(zone, profile, receipt, out GameObject advisor,
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
			CompletedNow = true;
			return true;
		}

		private static bool Publish(XRLGame Game, KingdomQuickstartReceipt Receipt,
			out string Failure)
		{
			Failure = "";
			string encoded = KingdomQuickstartRules.Encode(Receipt);
			if (Game == null || encoded == null)
			{
				Failure = "The quickstart receipt could not be encoded.";
				return false;
			}
			Game.SetStringGameState(KingdomQuickstartRules.ReceiptState, encoded);
			string observed = Game.GetStringGameState(KingdomQuickstartRules.ReceiptState,
				null);
			KingdomQuickstartReceipt read;
			if (!string.Equals(observed, encoded, StringComparison.Ordinal)
				|| !KingdomQuickstartRules.TryDecode(observed, out read)
				|| !string.Equals(KingdomQuickstartRules.Encode(read), encoded,
					StringComparison.Ordinal))
			{
				Failure = "The quickstart receipt did not publish exactly.";
				return false;
			}
			return true;
		}

		private static bool Advance(XRLGame Game, ref KingdomQuickstartReceipt Receipt,
			KingdomQuickstartPhase Next, string Value,
			KingdomQuickstartAdvisorDisposition Advisor, out string Failure)
		{
			Failure = "";
			KingdomQuickstartReceipt advanced;
			if (!KingdomQuickstartRules.TryAdvance(Receipt, Next, Value, Advisor,
				out advanced) || !Publish(Game, advanced, out Failure))
			{
				if (string.IsNullOrEmpty(Failure))
					Failure = "The quickstart receipt refused a non-monotone phase.";
				return false;
			}
			Receipt = advanced;
			return true;
		}

	}
}
