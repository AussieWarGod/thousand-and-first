using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Setup-only helpers for <see cref="KingdomDepositOverflowNativeChecks"/>: real bodies of the
	/// delivered blueprint, placed through the production placement APIs, carrying counts the
	/// harness assigns directly. No assertion about the deposit law lives here.
	/// <para>
	/// SYNTHETIC COUNTS, DISCLOSED. <c>Stacker.StackCount</c> is a plain <c>int</c> field with no
	/// ceiling (<c>XRL/World/Parts/Stacker.cs:11</c>, written by
	/// <c>Writer.Write(StackCount)</c> and read back by <c>Reader.ReadInt32()</c> at
	/// <c>:104-112</c>), so the engine PERMITS these counts; nothing here claims ordinary play
	/// produces one. Every fixture body also carries <c>NeverStack</c>, which both
	/// <c>CheckCellForStacking</c> and <c>CheckInventoryForStacking</c> honour
	/// (<c>Stacker.cs:278</c>, <c>:286</c>, <c>:327</c>, <c>:339</c>), so two separate bodies stay
	/// two separate bodies and the census really does sum two rows.
	/// </para>
	/// </summary>
	internal static partial class KingdomDepositOverflowNativeChecks
	{
		private sealed partial class Frame
		{
			/// <summary>
			/// The store this suite delivers into, made and DEDICATED by the fixture through the
			/// production check-in (<c>KingdomMaterials.DedicateStockpile</c>) rather than stamped
			/// into place. The camp's own founding transaction on this branch dedicates no
			/// container of its own, and #111 must not acquire a dependency on another ticket's
			/// heart stockpile to have somewhere to deliver; so the ground is built here, through
			/// the same call a founder's dedicate action makes, and disclosed as synthetic.
			/// </summary>
			private void DedicateStore()
			{
				GameObject chest = GameObject.Create("Chest");
				Require(GameObject.Validate(chest) && chest.Inventory != null,
					"the container blueprint produced nothing that holds things");
				string chestId = RequireAssignedIdentity(chest, "the synthetic store");
				Cell seat = ReserveCell();
				Require(ReferenceEquals(seat.AddObject(chest, NoStack: true), chest),
					"native placement substituted the synthetic store");
				string failure;
				Require(KingdomMaterials.DedicateStockpile(System, Zone, chest, out failure),
					failure ?? "the production check-in refused the synthetic store");
				Require(KingdomMaterials.IsStockpile(chest),
					"the production check-in left the synthetic store undedicated");
				bool indexed = false;
				foreach (GameObject store in KingdomMaterials.Stock(Zone).Stockpiles)
					if (ReferenceEquals(store, chest)) indexed = true;
				Require(indexed,
					"the settlement's own stock reading does not see the synthetic store");
				Require(KingdomSurvey.StockCapacityOf(chest) > 0,
					"the synthetic store declares no capacity to deliver into");
				RequireIdentityHeld(chest, chestId, "the synthetic store");
				Container = chest;
			}

			/// <summary>One real body of the delivered blueprint, standing in a cell, carrying an
			/// exact raw count and refusing every merge.</summary>
			private Body PlaceInCell(Cell Cell, int RawCount)
			{
				string id;
				GameObject item = Make(RawCount, out id);
				Require(ReferenceEquals(Cell.AddObject(item, NoStack: true), item),
					"native placement substituted a fixture stack in the cell");
				RequireIdentityHeld(item, id, "a ground fixture stack");
				Require(ReferenceEquals(item.CurrentCell, Cell),
					"the fixture stack did not come to rest in the cell it was placed in");
				Require(KingdomMaterials.RawPhysicalCountOf(item) == RawCount,
					"the fixture stack's raw count did not survive placement");
				return Body.OnGround(item, Cell);
			}

			/// <summary>The same body, standing in the dedicated store's own inventory.</summary>
			private Body PlaceInStore(int RawCount)
			{
				string id;
				GameObject item = Make(RawCount, out id);
				Container.Inventory.AddObject(item, null, true, NoStack: true);
				RequireIdentityHeld(item, id, "a stored fixture stack");
				Require(ReferenceEquals(item.Physics?.InInventory, Container)
					&& item.CurrentCell == null,
					"the fixture stack is not standing in the store's own custody");
				Require(KingdomMaterials.RawPhysicalCountOf(item) == RawCount,
					"the fixture stack's raw count did not survive the inventory add");
				return Body.InStore(item, Container);
			}

			/// <summary>A body of the delivered blueprint carrying an exact raw count. The count
			/// is written through the engine's own <c>StackCount</c> setter, which dispatches
			/// <c>StackCountChangedEvent</c> exactly as a stamp does; the read back beside it is
			/// the raw field, so a body the engine refused to hold at this count fails here rather
			/// than later.</summary>
			private GameObject Make(int RawCount, out string Id)
			{
				GameObject item = GameObject.Create(Blueprint);
				Require(GameObject.Validate(item), Blueprint + " produced no object");
				Stacker stacker = item.GetPart<Stacker>();
				Require(stacker != null, Blueprint + " carries no Stacker to hold a count");
				item.SetIntProperty("NeverStack", 1);
				Require(item.HasPropertyOrTag("NeverStack"),
					"the fixture stack did not take the NeverStack mark");
				stacker.StackCount = RawCount;
				Require(stacker.StackCount == RawCount,
					"the engine refused to hold the fixture count " + RawCount);
				Require(item.Blueprint == Blueprint,
					"the fixture body is not the blueprint this delivery is making");
				Id = RequireAssignedIdentity(item, "a fixture stack");
				return item;
			}

			/// <summary>
			/// SYNTHETIC IDENTITY ALLOCATION, DISCLOSED. A freshly created object carries no
			/// engine id until something asks for one. <c>GameObject.IDIfAssigned</c>
			/// (<c>XRL/World/GameObject.cs:424-429</c>) is the pure
			/// <c>GetStringProperty("id")</c> read; <c>GameObject.ID</c> (<c>:436-452</c>) reads
			/// the same property and, when it is null, derives it from <c>BaseID</c> and writes it
			/// back ONCE with <c>SetStringProperty("id", ...)</c>, where <c>BaseID</c>
			/// (<c>:400-415</c>) lazily takes <c>++game.GameObjectIDSequence</c>. So exactly one
			/// <c>ID</c> read allocates, and every later <c>IDIfAssigned</c> returns that same
			/// string.
			/// <para>
			/// Ordinary play allocates as a side effect of the many things that ask; a fixture
			/// that only creates, counts and places never asks, so its own bodies would stand
			/// there unidentified and admission would rightly refuse them. This helper is the ONE
			/// allocating read per own body, taken while the body is still being BUILT and never
			/// from an observation path. It proves the allocation rather than assuming it: the id
			/// comes back non-empty, the plain read agrees, and no other fixture body has ever
			/// been given the same one.
			/// </para>
			/// </summary>
			/// <returns>The allocated id, to be re-proved after the body is placed.</returns>
			private string RequireAssignedIdentity(GameObject Item, string What)
			{
				Require(GameObject.Validate(Item),
					What + " does not exist and cannot be given an identity");
				string id = Item.ID;
				Require(!string.IsNullOrEmpty(id) && Item.IDIfAssigned == id,
					"the engine allocated no readable identity for " + What);
				Require(!Identities.Contains(id),
					What + " was allocated an identity another fixture body already carries");
				Identities.Add(id);
				IdentitiesAllocated++;
				return id;
			}

			/// <summary>The same identity, after the body has been placed. Placement runs the
			/// engine's own entry handlers, and a body that came back carrying a different id is
			/// not the body this fixture made.</summary>
			private void RequireIdentityHeld(GameObject Item, string Id, string What)
			{
				Require(GameObject.Validate(Item) && Item.IDIfAssigned == Id,
					What + " did not keep the identity it was allocated across its placement");
			}

			/// <summary>Rows of the delivered blueprint standing in the store right now, counted by
			/// body and never by units, so a parcel that was inserted shows up as a new row.
			/// </summary>
			private int StoreRows()
			{
				int rows = 0;
				foreach (GameObject item in Container.Inventory.Objects)
					if (GameObject.Validate(item) && item.Blueprint == Blueprint) rows++;
				return rows;
			}

			/// <summary>The same count for a cell.</summary>
			private int GroundRows(Cell Cell)
			{
				int rows = 0;
				foreach (GameObject item in Cell.Objects)
					if (GameObject.Validate(item) && item.Blueprint == Blueprint) rows++;
				return rows;
			}
		}
	}
}
