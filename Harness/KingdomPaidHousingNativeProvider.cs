using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Real Quickstart homes and paid conversion; only supplemental stock and catalogue drift are synthetic.</summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomPaidHousingNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string PayVerb = "paid-housing-pay";
		internal const string CompleteVerb = "paid-housing-complete";
		internal const string StateKey = "r_TAF_PaidHousingNative_v1";
		private static readonly string[] Script = {
			"quickstart-lifecycle marsh yes", "lifecycle-open", "advance 7200", "advance 9600",
			"lifecycle-grown", PayVerb, "advance 7200", CompleteVerb, "lifecycle-save", "stagedigest" };
		internal static XRLGame Owner;
		internal static string JobId;
		internal static KingdomArchitectureIntent Before, After;
		internal static KingdomSocketTransition Paid;
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { PayVerb, CompleteVerb };
		internal static bool ClaimsScript() => KingdomScenarioScript.TryRead(out IList<string> script, out _)
			&& script.Contains(PayVerb);
		internal static void RequireScript()
		{
			Require(KingdomScenarioScript.TryRead(out IList<string> script, out _)
				&& script.Count == Script.Length, "paid housing script absent or changed");
			for (int i = 0; i < Script.Length; i++) Require(script[i] == Script[i], "paid housing script differs at " + i);
		}
		public string RunScenarioVerb(string verb, string argument, out bool ok)
		{
			ok = false;
			try
			{
				RequireScript();
				Require(string.IsNullOrEmpty(argument) && The.Game != null && The.Player?.CurrentZone != null
					&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending, "paid housing context differs");
				string result = verb == PayVerb ? Pay() : verb == CompleteVerb
					? KingdomPaidHousingWitness.Complete() : throw new InvalidOperationException("unknown paid housing verb");
				ok = true; return result;
			}
			catch (Exception error)
			{
				KingdomPaidHousingCatalogue.Restore();
				return "paid housing failed: " + KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
		private static string Pay()
		{
			Require(Owner == null && JobId == null, "conversion was already commissioned");
			Owner = The.Game;
			KingdomPaidHousingWitness.RequireAbsentWitness();
			var zone = The.Player.CurrentZone;
			var system = Owner.GetSystem<KingdomSystem>();
			Require(system != null && system.Founded, "settlement absent");
			string failure;
			GameObject work = null;
			foreach (var item in zone.GetObjects())
				if (KingdomUpgrade.IsFunctionallyBuilt(item) && KingdomUpgrade.DesignKeyOf(item) == "tentrow"
					&& (work == null || string.CompareOrdinal(item.IDIfAssigned, work.IDIfAssigned) < 0)) work = item;
			Require(work != null, "no actually completed Quickstart tent row");
			Require(KingdomArchitectureRuntime.TryRead(work, out Before, out failure), failure);
			Require(Before.LotSize == ArchitectureLotSize.Medium && Before.BuildKey == "tentrow"
				&& KingdomSocketTransitions.TryGet("tentrow", "hutyard", "housing", Before.LotSize, out Paid),
				"expected medium housing declaration absent");
			Require(Paid.WaterDrams == 7 && Paid.WorkTicks == 1800, "baseline conversion quote changed");
			Require(KingdomData.TryGetBuilding(Paid.ToBuildKey, out var entry), "successor design absent");
			TransferCarriedWater(zone);
			Supply(zone, Paid);
			Require(KingdomSurvey.TryBindLocalOperation(zone, system, out var scope, out failure), failure);
			using (scope)
			{
				Require(KingdomUpgrade.TryPreparePlanChange(system, zone, work, entry, Paid,
					out var assessment, out var prepared, out failure), "conversion preflight: " + failure);
				After = prepared.Architecture;
				KingdomPaidHousingWitness.ExpectedClearFloor(After);
				KingdomPaidHousingWitness.Capture(work, zone);
				var stock = KingdomMaterials.Stock(zone).Tally.Copy();
				int water = KingdomGrowth.CountStoredWater(zone);
				Require(KingdomConstruction.TryRead(out var jobsBefore, out failure), failure);
				Require(KingdomUpgrade.BeginPreparedPlanChange(system, zone, work, assessment, prepared, out failure), failure);
				JobId = work.GetStringProperty(KingdomConstruction.ReceiptProperty);
				Require(KingdomConstruction.TryFind(JobId, out var job) && job != null
					&& job.SubjectId == work.IDIfAssigned && job.Route == KingdomConstructionRoute.Improvement
					&& job.TargetKey == Paid.ToBuildKey && KingdomConstruction.Owns(system, zone, job), "paid job identity differs");
				foreach (var prior in jobsBefore) Require(prior.Id != JobId, "commission reused an existing job");
				Require(KingdomQuickstartBuildClaims.CleanFirstPayment(job.Claims, Paid.WaterDrams,
					new KingdomMaterialDebitCost(Paid.Materials)), "conversion claims differ from paid quote");
				var remaining = KingdomMaterials.Stock(zone).Tally;
				foreach (KingdomMaterial material in Enum.GetValues(typeof(KingdomMaterial)))
					Require(stock.Get(material) - remaining.Get(material) == Paid.Materials.Get(material),
						"physical material debit differs: " + material);
				Require(water - KingdomGrowth.CountStoredWater(zone) == Paid.WaterDrams, "physical water debit differs");
				KingdomPaidHousingCatalogue.Change(work, Before, After, Paid);
			}
			Require(!KingdomSurvey.HasBoundPass, "conversion leaked its local survey");
			return "paid-housing job=" + JobId + "; water=7; materials=exact; old-current-refused=true"
				+ "; retained-authorized=true; current-water=8; actual-home=true; supplied-materials=true";
		}
		private static void Supply(Zone zone, KingdomSocketTransition price)
		{
			Require(KingdomQuickstartLifecycleSteps.TryStockpile(Owner, zone, out var store, out string failure), failure);
			var available = KingdomMaterials.Stock(zone).Tally;
			foreach (KingdomMaterial material in Enum.GetValues(typeof(KingdomMaterial)))
				for (int i = available.Get(material); i < price.Materials.Get(material); i++)
				{
					var unit = GameObject.Create(KingdomMaterials.BlueprintFor(material));
					Require(unit != null && ReferenceEquals(store.Inventory.AddObject(unit, null, Silent: true, NoStack: true), unit)
						&& unit.InInventory == store, "supplemental material custody failed");
				}
			Require(KingdomGrowth.CountStoredWater(zone) >= price.WaterDrams, "settlement cannot fund conversion water");
		}
		private static void TransferCarriedWater(Zone zone)
		{
			const int drams = 32;
			Require(KingdomQuickstartRules.TryDecode(Owner.GetStringGameState(KingdomQuickstartRules.ReceiptState),
				out var receipt), "water transfer lacks founder receipt");
			Require(KingdomConstruction.FindExactId(zone, receipt.WaterObjectId, out var cask) == KingdomPhysicalLookupState.Exact,
				"receipted water cask absent or ambiguous");
			var water = cask.GetPart<LiquidVolume>();
			Require(cask.Blueprint == "r_KingdomCaskRack" && cask.GetIntProperty("KingdomStores") == 1
				&& water != null && KingdomLiquids.HasFreshWater(water) && water.MaxVolume - water.Volume >= drams,
				"existing fresh-water cask lacks transfer space");
			var player = The.Player;
			GameObject donor = null;
			foreach (var item in player.Inventory.Objects)
				if (GameObject.Validate(item) && item.InInventory == player && item.GetPart<LiquidVolume>() is LiquidVolume held
					&& KingdomLiquids.HasFreshWater(held) && held.Volume >= drams) { donor = item; break; }
			Require(donor != null, "founder lacks 32 carried drams for the extended wait");
			int moves = KingdomGuestSaveSupply.Walk(player, cask.CurrentCell);
			Require(player.CurrentZone == zone && Math.Max(Math.Abs(player.CurrentCell.X - cask.CurrentCell.X),
				Math.Abs(player.CurrentCell.Y - cask.CurrentCell.Y)) == 1, "founder is not beside the receipted cask");
			var liquid = donor.GetPart<LiquidVolume>();
			int before = water.Volume, carried = liquid.Volume, stores = KingdomGrowth.CountStoredWater(zone);
			water.MixWith(liquid, PouredFrom: donor, Amount: drams);
			Require(water.Volume == before + drams && liquid.Volume == carried - drams && donor.InInventory == player
				&& KingdomGrowth.CountStoredWater(zone) == stores + drams && KingdomLiquids.HasFreshWater(water)
				&& ReferenceEquals(cask.GetPart<LiquidVolume>(), water) && ReferenceEquals(donor.GetPart<LiquidVolume>(), liquid),
				"carried-water transfer did not conserve exact volumes and owners");
			Require(KingdomScenarioJournal.Append("paid-housing-water", true, "source=carried; drams=32; conserved=true; moves=" + moves
				+ "; donor=" + donor.IDIfAssigned + "; cask=" + cask.IDIfAssigned) == null, "water transfer journal unavailable");
		}
		internal static void Require(bool value, string failure)
		{
			if (!value) throw new InvalidOperationException(failure ?? "paid housing invariant failed");
		}
	}
}
