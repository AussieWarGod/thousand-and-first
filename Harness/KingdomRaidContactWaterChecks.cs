using System;
using System.Collections.Generic;
using System.Text;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	// Selector receipts are read-only reservations. Only the production contact drains water.
	internal static class KingdomRaidContactWaterChecks
	{
		private static GameObject Other;
		private static readonly List<KingdomWaterDebit> Receipts = new List<KingdomWaterDebit>();
		internal static void Prepare(KingdomRaidLaunchNativeFixture fixture,
			Func<Cell, bool> clear, StringBuilder evidence)
		{
			Require(Other == null && Receipts.Count == 0, "water selector evidence already retained");
			Require(KingdomSurvey.ActiveFor(fixture.Zone) == null, "selector requires no pre-existing survey scope");
			Cell destination = null;
			for (int x = fixture.Zone.Width - 2; x > 1 && destination == null; x--)
				for (int y = fixture.Zone.Height - 2; y > 1; y--)
				{
					Cell candidate = fixture.Zone.GetCell(x, y);
					if (clear(candidate)) { destination = candidate; break; }
				}
			Require(destination != null, "no empty cell for retained second store; no clearing authorized");
			int allocations = 0;
			GameObject created = GameObject.Create(KingdomRaidLaunchNativeFixture.StoreBlueprint,
				BeforeObjectCreated: body => { allocations++; Other = body; });
			Require(allocations == 1 && ReferenceEquals(created, Other) && GameObject.Validate(Other)
				&& Other.Blueprint == KingdomRaidLaunchNativeFixture.StoreBlueprint && Other.Count == 1
				&& Other.CurrentCell == null && Other.InInventory == null && Other.Equipped == null,
				"second store factory did not return its exact unowned allocation");
			LiquidVolume other = Other.GetPart<LiquidVolume>();
			Require(other != null && other.MaxVolume == 1920 && other.Volume == 0
				&& KingdomLiquids.CanReceiveFreshWater(other), "second store has no exact empty finite vessel");
			Other.SetIntProperty("KingdomStores", 1);
			Require(clear(destination), "second store destination changed before placement");
			Require(ReferenceEquals(destination.AddObject(Other, NoStack: true), Other)
				&& ReferenceEquals(Other.CurrentCell, destination), "second store lost exact placement");
			Require(KingdomLiquids.Fill(other, "water", 96) == 96 && other.Volume == 96
				&& KingdomLiquids.HasFreshWater(other), "second store fill did not prove96drams");
			Require(!string.IsNullOrEmpty(Other.ID), "second store identity unavailable");
			LiquidVolume named = fixture.Store.GetPart<LiquidVolume>();
			var namedBody = new KingdomRaidContactBody(fixture.Store);
			var otherBody = new KingdomRaidContactBody(Other);
			KingdomSurvey survey = KingdomSurvey.Take(fixture.Zone, fixture.System);
			Require(survey.Stores.Count == 2 && ReferenceEquals(survey.Stores[0], named)
				&& ReferenceEquals(survey.Stores[1], other) && survey.StoredWater == 336,
				"selector must exercise exact nonfirst store in genuine two-store survey");
			int stored = survey.StoredWater, space = survey.StorageSpace, capacity = survey.StorageCapacity;
			using (survey.BindPass())
			{
				Require(ReferenceEquals(KingdomSurvey.ActiveFor(fixture.Zone), survey), "selector survey did not bind");
				Refused(KingdomWaterDebit.ReserveExactStore(survey, null, 1), "null selector");
				Refused(KingdomWaterDebit.ReserveExactStore(survey, named, 241), "selected volume insufficient despite aggregate336");
				KingdomWaterDebit exact = KingdomWaterDebit.ReserveExactStore(survey, other, 1);
				Receipts.Add(exact);
				Require(exact != null && exact.State == KingdomWaterDebitState.Reserved
					&& exact.TryDescribe(out var legs) && legs.Length == 1
					&& ReferenceEquals(legs[0].Owner, Other) && legs[0].BeforeVolume == 96
					&& legs[0].AfterVolume == 95 && legs[0].MaxVolume == 1920,
					"nonfirst selector did not freeze exactly one named physical vessel");
				var copy = new KingdomSurvey();
				copy.Stores.Add(named); copy.Stores.Add(other);
				copy.StoredWater = stored; copy.StorageSpace = space; copy.StorageCapacity = capacity;
				Refused(copy.ReserveExactWater(1), "anonymous survey with exact live parts and counters");
				Require(survey.StoredWater == stored && survey.StorageSpace == space && survey.StorageCapacity == capacity,
					"read-only selector reservations changed active aggregate accounting");
				namedBody.Exact(namedBody.OriginalCell); otherBody.Exact(otherBody.OriginalCell);
			}
			Require(KingdomSurvey.ActiveFor(fixture.Zone) == null, "selector survey scope leaked into real contact");
			evidence.Append("\nwater-selector stores=2 aggregate=336 nonfirst=96 exact-leg=96->95 reserved-only=true")
				.Append(" null=refused insufficient-exact=refused anonymous=refused bodies=unchanged scope=disposed");
		}
		private static void Refused(KingdomWaterDebit receipt, string label)
		{
			Receipts.Add(receipt);
			Require(receipt != null && receipt.State == KingdomWaterDebitState.Failed
				&& receipt.Spent == 0 && receipt.Lost == 0 && receipt.MeasurementExact
				&& receipt.VesselCount == 0, label + " did not refuse without a physical debit");
		}
		private static void Require(bool condition, string failure)
		{
			KingdomRaidContactNativeProvider.Require(condition, failure);
		}
	}
}
