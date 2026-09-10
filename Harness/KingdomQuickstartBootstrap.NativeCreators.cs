using System;
using System.Collections.Generic;
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
			Context.Case("creator-founders-cohort", () => NativeFoundersCohort(Context));
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
				KingdomQuickstartFoundersDisposition.Pending,
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
			var before = new NativeWaterGround(Context);
			using (var fault = new KingdomQuickstartCaskFault(Context, receipt))
			{
				GameObject refused = CreateWater(Context.Game, Context.Zone, receipt, out string refusal);
				Context.Check(refused == null && refusal == "The starter water was not exactly 24 physical drams in its dedicated casks.",
					"actual water creator must refuse its post-placement capacity fault");
				fault.Check(1, 1);
				NativeAbsent(Context, fault.First);
				before.Check(null);
				Context.Check(!GrantQuarantined(Context.Game), "proved creator rollback must release all quarantine tables");
				GameObject water = NativeTrackFresh(Context,
					CreateWater(Context.Game, Context.Zone, receipt, out string failure));
				fault.Check(2, 2);
				Context.Check(!ReferenceEquals(water, fault.First) && ReferenceEquals(water, fault.Second),
					"retry must return exactly the second witnessed factory original");
				Context.Check(VerifyWaterGrant(Context.Zone, water, receipt, true, out failure), failure);
				Context.Check(water.GetPart<LiquidVolume>().Volume == 24
					&& water.GetPart<LiquidVolume>().MaxVolume == 64, "retry must retain 24 drams and shipped capacity64");
				before.Check(water);
				Context.Check(ReferenceEquals(water, CreateWater(Context.Game, Context.Zone, receipt,
					out failure)), "unpublished water recovery must reuse the exact object");
				fault.Check(2, 2);
				Context.Check(VerifyWaterGrant(Context.Zone, water, receipt, true, out failure), failure);
				Context.Check(ExactGrantMarker(water, receipt, KingdomQuickstartPhase.WaterStocked),
					"recovery must leave exactly one water marker without another mint or placement");
				before.Check(water);
				Context.Check(!GrantQuarantined(Context.Game), "successful water grant must not quarantine");
			}
		}

		private sealed class NativeWaterGround
		{
			private readonly KingdomNativeRegressionContext Context;
			private readonly Cell.ObjectRack[] Lists;
			private readonly Cell[] Cells;
			private readonly GameObject[][] Rows;
			private readonly List<NativeStockBody> Bodies = new List<NativeStockBody>();
			internal NativeWaterGround(KingdomNativeRegressionContext context)
			{
				Context = context;
				context.Check(context.Zone.Width == 80 && context.Zone.Height == 25, "water fixture zone dimensions differ");
				Lists = new Cell.ObjectRack[2000]; Rows = new GameObject[2000][]; Cells = new Cell[2000];
				var seen = new List<GameObject>();
				for (int x = 0; x < 80; x++) for (int y = 0; y < 25; y++)
				{
					int index = x * 25 + y;
					Cells[index] = context.Zone.GetCell(x, y); Lists[index] = Cells[index].Objects;
					Rows[index] = Lists[index].ToArray();
					context.Check(Rows[index].Length <= 512 && seen.Count <= 20000, "water fixture observation exceeds bounds");
					foreach (GameObject body in Rows[index])
					{
						context.Check(seen.Count < 20000 && GameObject.Validate(body) && !ContainsExact(seen, body),
							"baseline cell custody is not unique or exceeds bounds");
						seen.Add(body); Bodies.Add(new NativeStockBody(body));
					}
				}
			}
			internal void Check(GameObject Added)
			{
				for (int x = 0; x < 80; x++) for (int y = 0; y < 25; y++)
				{
					int index = x * 25 + y;
					Cell.ObjectRack list = Context.Zone.GetCell(x, y).Objects;
					bool extra = Added != null && x == KingdomQuickstartRules.WaterCellX && y == KingdomQuickstartRules.WaterCellY;
					Context.Check(ReferenceEquals(Context.Zone.GetCell(x, y), Cells[index])
						&& ReferenceEquals(list, Lists[index]) && list.Count == Rows[index].Length + (extra ? 1 : 0),
						"water creator changed unrelated cell custody or retained a refused allocation");
					for (int i = 0; i < Rows[index].Length; i++)
						Context.Check(ReferenceEquals(list[i], Rows[index][i]), "water creator replaced or moved a baseline body");
					if (extra) Context.Check(ReferenceEquals(list[list.Count - 1], Added), "retry is not the sole added water body");
				}
				foreach (NativeStockBody body in Bodies)
					Context.Check(body.Exact(null, 0), "water creator changed captured baseline identity, stock or custody");
			}
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

		/// <summary>
		/// The reversible half of the founding cohort, against the engine: four bodies raised in
		/// one scope, every one of them placed on its own reserved cell wearing its own indexed
		/// reservation, and every one of them resolving back from the identity the fence would
		/// publish. Their gear is tracked body-first so the context's reverse cleanup takes the
		/// gear off before the body, which is the same order the scope's own rollback uses.
		/// </summary>
		private static void NativeFoundersCohort(KingdomNativeRegressionContext Context)
		{
			KingdomQuickstartReceipt receipt = NativeReceipt(Context, KingdomQuickstartPhase.AdvisorResolved);
			Context.Check(KingdomQuickstartRules.TryAdvance(receipt, KingdomQuickstartPhase.AdvisorResolved,
				"", KingdomQuickstartAdvisorDisposition.Omitted, out KingdomQuickstartReceipt resolved)
				&& KingdomQuickstartRules.TryAdvance(resolved, KingdomQuickstartPhase.Complete, "",
					KingdomQuickstartAdvisorDisposition.Unresolved, out receipt),
				"fixture receipt must reach Complete owing a cohort");
			Context.Check(receipt.FoundersDisposition == KingdomQuickstartFoundersDisposition.Pending
				&& !KingdomQuickstartRules.IsTerminal(receipt), "a Complete receipt owing founders is not finished");
			Context.Check(TryStageFounderBodies(Context.Game, Context.Zone, receipt,
				out GameObject[] cohort, out string failure), failure);
			for (int i = 0; i < cohort.Length; i++)
			{
				Context.Track(cohort[i]);
				if (cohort[i].Inventory != null)
					foreach (GameObject carried in cohort[i].Inventory.Objects) Context.Track(carried);
				if (cohort[i].Body != null)
					foreach (GameObject worn in cohort[i].Body.GetEquippedObjects()) Context.Track(worn);
			}
			Context.Check(VerifyFounderPlacement(Context.Zone, cohort, receipt, out failure), failure);
			string[] ids = new string[KingdomQuickstartRules.FounderCount];
			for (int i = 0; i < ids.Length; i++) ids[i] = cohort[i].IDIfAssigned;
			Context.Check(KingdomQuickstartRules.TryRestateFounders(receipt,
				KingdomQuickstartFoundersDisposition.Seeding, ids, out KingdomQuickstartReceipt seeding),
				"the fence must name the exact four before any irreversible write");
			for (int i = 0; i < ids.Length; i++)
				Context.Check(ReferenceEquals(Context.Zone.FindObjectByID(ids[i]), cohort[i])
					&& FounderIsExact(Context.Zone, cohort[i], seeding, i),
					"a named founder must resolve back to the exact body it named");
			Context.Check(!GrantQuarantined(Context.Game),
				"a whole cohort must leave no quarantine behind");
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
