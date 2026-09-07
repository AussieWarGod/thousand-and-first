using System;
using ThousandAndFirst.Harness;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		internal static void NativeQuickstartChecks(KingdomNativeRegressionContext Context)
		{
			Context.Case("creator-water-recovery", () => NativeWaterCreator(Context));
			Context.Case("creator-larder-recovery", () => NativeLarderCreator(Context));
			Context.Case("creator-materials-recovery", () => NativeMaterialsCreator(Context));
			Context.Case("creator-advisor-recovery", () => NativeAdvisorCreator(Context));
			Context.Case("creator-foreign-obstruction", () => NativeForeignObstruction(Context));
			NativeQuickstartFaults(Context);
		}

		internal static bool NativeQuickstartCleanup(GameObject Object)
		{
			return RemoveFreshGrantObject(Object);
		}

		private static KingdomQuickstartReceipt NativeReceipt(KingdomNativeRegressionContext Context,
			KingdomQuickstartPhase Target)
		{
			Context.Check(KingdomQuickstartRules.TryCreateReceipt("marsh", Context.Zone.ZoneID,
				out KingdomQuickstartReceipt receipt), "fixture zone must match the marsh profile");
			for (int phase = (int)KingdomQuickstartPhase.Founded; phase < (int)Target; phase++)
			{
				string value = phase == (int)KingdomQuickstartPhase.Founded
					? "Vinewafer" : "native-fixture-" + phase;
				Context.Check(KingdomQuickstartRules.TryAdvance(receipt, (KingdomQuickstartPhase)phase,
					value, KingdomQuickstartAdvisorDisposition.Unresolved,
					out KingdomQuickstartReceipt next), "fixture receipt must advance lawfully");
				receipt = next;
			}
			return receipt;
		}

		private static GameObject NativeTrackFresh(KingdomNativeRegressionContext Context,
			GameObject Grant)
		{
			Context.Check(GameObject.Validate(Grant), "production creator must return a live fresh grant");
			Context.Track(Grant);
			// Called only on the first successful creator return in this case's empty role cell.
			if (Grant.Inventory != null)
				foreach (GameObject child in Grant.Inventory.Objects) Context.Track(child);
			return Grant;
		}

		private static void NativeWaterCreator(KingdomNativeRegressionContext Context)
		{
			KingdomQuickstartReceipt receipt = NativeReceipt(Context, KingdomQuickstartPhase.WaterStocked);
			GameObject water = NativeTrackFresh(Context,
				CreateWater(Context.Game, Context.Zone, receipt, out string failure));
			Context.Check(VerifyWaterGrant(Context.Zone, water, receipt, true, out failure), failure);
			Context.Check(water.GetPart<LiquidVolume>().Volume == 24, "water grant must contain 24 drams");
			Context.Check(ReferenceEquals(water, CreateWater(Context.Game, Context.Zone, receipt,
				out failure)), "unpublished water recovery must reuse the exact object");
			Context.Check(ExactGrantMarker(water, receipt, KingdomQuickstartPhase.WaterStocked),
				"recovery must leave exactly one water marker");
			Context.Check(!GrantQuarantined(Context.Game), "successful water grant must not quarantine");
		}

		private static void NativeLarderCreator(KingdomNativeRegressionContext Context)
		{
			KingdomQuickstartReceipt receipt = NativeReceipt(Context, KingdomQuickstartPhase.FoodStocked);
			GameObject larder = NativeTrackFresh(Context,
				CreateLarder(Context.Game, Context.Zone, receipt, out string failure));
			Context.Check(VerifyLarderGrant(Context.Zone, larder, receipt, true, out failure), failure);
			Context.Check(larder.Inventory.Objects.Count == 12, "larder must hold 12 unstacked meals");
			Context.Check(ReferenceEquals(larder, CreateLarder(Context.Game, Context.Zone, receipt,
				out failure)), "unpublished larder recovery must reuse the exact object");
			Context.Check(larder.Inventory.Objects.Count == 12
				&& ExactGrantMarker(larder, receipt, KingdomQuickstartPhase.FoodStocked),
				"larder recovery must not mint a second container or additional meals");
			NativeLarderChildAlias(Context, larder, receipt);
		}

		private static void NativeMaterialsCreator(KingdomNativeRegressionContext Context)
		{
			KingdomQuickstartReceipt receipt = NativeReceipt(Context, KingdomQuickstartPhase.MaterialsStocked);
			GameObject stockpile = NativeTrackFresh(Context,
				CreateMaterials(Context.Game, Context.Zone, receipt, out string failure));
			Context.Check(VerifyMaterialsGrant(Context.Zone, stockpile, receipt, true, out failure), failure);
			Context.Check(stockpile.Inventory.Objects.Count == 3, "materials must occupy three exact stacks");
			Context.Check(ReferenceEquals(stockpile, CreateMaterials(Context.Game, Context.Zone, receipt,
				out failure)), "unpublished material recovery must reuse the exact object");
			Context.Check(VerifyMaterialsGrant(Context.Zone, stockpile, receipt, true, out failure), failure);
			NativeMaterialChildAlias(Context, stockpile, receipt);
		}

		private static void NativeAdvisorCreator(KingdomNativeRegressionContext Context)
		{
			KingdomQuickstartReceipt receipt = NativeReceipt(Context, KingdomQuickstartPhase.AdvisorResolved);
			Context.Check(KingdomQuickstartRules.TryProfile("marsh", out KingdomQuickstartProfile profile),
				"marsh advisor profile must exist");
			GameObject advisor = NativeTrackFresh(Context,
				CreateAdvisor(Context.Game, Context.Zone, profile, receipt, out string failure));
			Context.Check(VerifyAdvisor(Context.Zone, advisor, receipt, out failure), failure);
			Context.Check(advisor.GetIntProperty("NoLoot") == 1 && advisor.Inventory.Objects.Count == 0,
				"advisor factory must suppress inherited RandomLoot before creation events");
			Context.Check(TryResolveAdvisor(Context.Game, Context.Zone, profile, receipt,
				out GameObject recovered, out KingdomQuickstartAdvisorDisposition disposition,
				out failure) && ReferenceEquals(advisor, recovered)
				&& disposition == KingdomQuickstartAdvisorDisposition.Included,
				"unpublished advisor recovery must reuse the exact NPC");
		}

		private static void NativeForeignObstruction(KingdomNativeRegressionContext Context)
		{
			KingdomQuickstartReceipt receipt = NativeReceipt(Context, KingdomQuickstartPhase.WaterStocked);
			GameObject foreign = Context.Track(GameObject.Create("Chest"));
			Cell cell = Context.Zone.GetCell(KingdomQuickstartRules.WaterCellX, KingdomQuickstartRules.WaterCellY);
			cell.AddObject(foreign, NoStack: true, Silent: true);
			int before = Context.Zone.GetObjects().Count;
			Context.Check(CreateWater(Context.Game, Context.Zone, receipt, out _) == null,
				"production creator must refuse the foreign role obstruction");
			Context.Check(GameObject.Validate(foreign) && foreign.CurrentCell == cell
				&& Context.Zone.GetObjects().Count == before && !GrantQuarantined(Context.Game),
				"pre-allocation refusal must preserve the obstruction and physical object count");
		}

		private const string NativeFaultMessage = "taf-native-quickstart-injected-fault";

		private static void NativeExpectedThrow(KingdomNativeRegressionContext Context, Action Action)
		{
			bool caught = false;
			try { Action(); }
			catch (InvalidOperationException ex)
			{
				if (ex.Message != NativeFaultMessage) throw;
				caught = true;
			}
			Context.Check(caught, "the injected adapter callback must throw at its declared cut");
		}

		private static void NativeAbsent(KingdomNativeRegressionContext Context, GameObject Object)
		{
			Context.Check(Object != null && !GameObject.Validate(Object), "fresh object must be invalidated");
			Context.Check(Object.CurrentCell == null && Object.InInventory == null && Object.Equipped == null
				&& !ContainsExact(Context.Zone.GetObjects(), Object), "fresh object must leave physical custody");
		}

		private static void NativeAssertFence(KingdomNativeRegressionContext Context)
		{
			string key = KingdomQuickstartRules.QuarantineState;
			string token = Context.Game.GetStringGameState(key, null);
			Context.Check(!string.IsNullOrEmpty(token) && GrantQuarantined(Context.Game),
				"unproved cleanup must retain its exact durable quarantine token");
			try
			{
				int allocations = 0;
				Context.Check(!TryCreateFreshGrant(Context.Game, scope =>
				{
					allocations++;
					return scope.Create(() => Context.Track(GameObject.Create("Chest")));
				}, item => true, out _) && allocations == 0, "quarantine must refuse before any retry allocation");
			}
			finally
			{
				if (string.Equals(Context.Game.GetStringGameState(key, null), token, StringComparison.Ordinal))
					Context.Game.StringGameState.Remove(key);
			}
		}
	}
}
