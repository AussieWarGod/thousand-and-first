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
			/// <summary>One real body of the delivered blueprint, standing in a cell, carrying an
			/// exact raw count and refusing every merge.</summary>
			private Body PlaceInCell(Cell Cell, int RawCount)
			{
				GameObject item = Make(RawCount);
				Require(ReferenceEquals(Cell.AddObject(item, NoStack: true), item),
					"native placement substituted a fixture stack in the cell");
				Require(ReferenceEquals(item.CurrentCell, Cell),
					"the fixture stack did not come to rest in the cell it was placed in");
				Require(KingdomMaterials.RawPhysicalCountOf(item) == RawCount,
					"the fixture stack's raw count did not survive placement");
				return new Body(item);
			}

			/// <summary>The same body, standing in the dedicated store's own inventory.</summary>
			private Body PlaceInStore(int RawCount)
			{
				GameObject item = Make(RawCount);
				Container.Inventory.AddObject(item, null, true, NoStack: true);
				Require(ReferenceEquals(item.Physics?.InInventory, Container)
					&& item.CurrentCell == null,
					"the fixture stack is not standing in the store's own custody");
				Require(KingdomMaterials.RawPhysicalCountOf(item) == RawCount,
					"the fixture stack's raw count did not survive the inventory add");
				return new Body(item);
			}

			/// <summary>A body of the delivered blueprint carrying an exact raw count. The count
			/// is written through the engine's own <c>StackCount</c> setter, which dispatches
			/// <c>StackCountChangedEvent</c> exactly as a stamp does; the read back beside it is
			/// the raw field, so a body the engine refused to hold at this count fails here rather
			/// than later.</summary>
			private GameObject Make(int RawCount)
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
				return item;
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
