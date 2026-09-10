using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The four cases themselves, each driving the REAL adapters through the REAL
	/// <c>KingdomDepositEngine.Fill</c>. The stockpile cases go through the whole production
	/// delivery (<c>KingdomMaterials.MaterialStock.Put</c>, which builds the real
	/// <c>StockpileDepositHost</c>); the ground cases build the real <c>GroundSpillHost</c> and
	/// hand it to the same engine, which is the shortest path that still runs no substitute.
	/// </summary>
	internal static partial class KingdomDepositOverflowNativeChecks
	{
		private sealed partial class Frame
		{
			/// <summary>Case 1. An ordinary delivery of three units into the dedicated store, and
			/// three onto bare ground. Both are credited, and both are proved by the RAW census
			/// either side of the delivery rather than by what the call returned.</summary>
			private void OrdinaryParcel()
			{
				int room, heldBefore, heldAfter;
				Require(KingdomMaterials.TryDepositRawRoomNow(Container, out room) && room >= 3,
					"the camp store has no readable room for an ordinary parcel");
				Require(KingdomMaterials.TryDepositMaterialHeldNow(Container, Blueprint,
					out heldBefore), "the camp store's hold is not readable before an ordinary parcel");
				int rowsBefore = StoreRows();
				KingdomDepositCustody custody;
				int spilled = KingdomMaterials.StockForExactContainer(Zone, Container)
					.Put(KingdomMaterial.Brush, 3, null, out custody);
				Require(custody == KingdomDepositCustody.Settled,
					"an ordinary parcel into a readable store did not settle");
				Require(spilled == 0, "an ordinary parcel into a store with room spilled");
				Require(KingdomMaterials.TryDepositMaterialHeldNow(Container, Blueprint,
					out heldAfter), "the camp store's hold is not readable after an ordinary parcel");
				Require(heldAfter == heldBefore + 3,
					"the store's raw hold did not rise by exactly the parcel");
				Require(StoreRows() >= rowsBefore,
					"an ordinary parcel removed a row that was already standing");
				int groundBefore, groundAfter;
				Require(KingdomMaterials.TryGroundMaterialHeldNow(PlainGround, Blueprint,
					out groundBefore), "the bare cell's hold is not readable");
				KingdomDepositOutcome spill = KingdomDepositEngine.Fill(
					new KingdomMaterials.GroundSpillHost(Zone, PlainGround, Blueprint, 3), 3, 3);
				Require(spill.Custody == KingdomDepositCustody.Settled && spill.Placed == 3,
					"an ordinary spill onto bare ground did not settle for its whole batch");
				Require(KingdomMaterials.TryGroundMaterialHeldNow(PlainGround, Blueprint,
					out groundAfter), "the bare cell's hold is not readable after the spill");
				Require(groundAfter == groundBefore + 3,
					"the cell's raw hold did not rise by exactly the spill");
				Evidence.Append("\ncase1 ordinary: store held ").Append(heldBefore).Append("->")
					.Append(heldAfter).Append("; ground held ").Append(groundBefore).Append("->")
					.Append(groundAfter).Append("; spilled=").Append(spilled);
			}

			/// <summary>Case 2. A real held stack at EXACTLY <c>int.MaxValue</c>. The bound is
			/// representability itself, so this total still reads, and a parcel landing beside it
			/// is still credited on its own exact-body proof. The huge body is untouched.
			/// </summary>
			private void RepresentableBoundary()
			{
				Boundary = PlaceInCell(BoundaryGround, int.MaxValue);
				int held;
				Require(KingdomMaterials.TryGroundMaterialHeldNow(BoundaryGround, Blueprint,
					out held), "a hold of exactly int.MaxValue must still read");
				Require(held == int.MaxValue,
					"the boundary cell did not read back exactly int.MaxValue");
				KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(
					new KingdomMaterials.GroundSpillHost(Zone, BoundaryGround, Blueprint, 2), 2, 2);
				Require(outcome.Custody == KingdomDepositCustody.Settled && outcome.Placed == 2,
					"a delivery beside a representable hold was refused");
				Boundary.RequireUnchanged("the int.MaxValue stack");
				Evidence.Append("\ncase2 boundary: held=").Append(held).Append("; placed=")
					.Append(outcome.Placed).Append("; body=").Append(Boundary.Evidence);
			}

			/// <summary>
			/// Case 3. Two real bodies of 1,200,000,000 added to the dedicated store, ON TOP OF
			/// whatever case 1 already delivered into it. The total is therefore the measured
			/// baseline plus 2,400,000,000, never a bare 2,400,000,000, and the journal records
			/// the baseline it actually read rather than asserting a number it did not measure.
			/// <para>
			/// The checked room census refuses. The ORDINARY advisory reading beside it still
			/// reports room, which is why the delivery reaches the store at all and is the open
			/// follow-up on this issue. That advisory number is a ROOM &mdash; capacity less an
			/// unchecked hold &mdash; and is NOT the wrapped hold itself; the two are recorded
			/// separately and never equated. The delivery grants no credit and leaves both bodies
			/// exactly as they stood.
			/// </para>
			/// </summary>
			private void StockpileOverflow()
			{
				// What the store already holds of this material, measured rather than assumed:
				// case 1 delivered into this same store, so the fixture stacks land on top of a
				// baseline and the totals below are that baseline plus what is added.
				int baseline;
				Require(KingdomMaterials.TryDepositMaterialHeldNow(Container, Blueprint,
					out baseline), "the store's hold is not readable before the fixture stacks");
				// One stack first, and the census is proved to COUNT it before a second is placed.
				// That is the eligibility proof this fixture needs: not that a classifier says the
				// body is material, but that the very reading under test already saw it.
				Body first = PlaceInStore(1200000000);
				int counted;
				Require(KingdomMaterials.TryDepositMaterialHeldNow(Container, Blueprint,
					out counted) && counted == baseline + 1200000000,
					"the raw census did not count the first fixture stack");
				StoreStacks = new[] { first, PlaceInStore(1200000000) };
				int rowsBefore = StoreRows();
				int unread;
				Require(!KingdomMaterials.TryDepositRawRoomNow(Container, out unread),
					"a store holding more than int.MaxValue must have no room reading");
				Require(unread == 0, "a refused room reading must hand back nothing");
				Require(!KingdomMaterials.TryDepositMaterialHeldNow(Container, Blueprint,
					out unread), "a hold past int.MaxValue must have no reading either");
				// The advisory ROOM the delivery walks on. It is capacity less an UNCHECKED hold,
				// so it is a room reading and not the wrapped hold; the wrapped hold is not read
				// here at all, and the two are never equated.
				int advisoryRoom = KingdomMaterials.StockpileRoom(Container);
				Require(advisoryRoom >= 1, "the advisory reading already refuses this store, so "
					+ "the checked census would never be reached and the case proves nothing");
				KingdomDepositCustody custody;
				int spilled = KingdomMaterials.StockForExactContainer(Zone, Container)
					.Put(KingdomMaterial.Brush, 1, null, out custody);
				Require(custody == KingdomDepositCustody.Unproved,
					"a delivery into an unreadable store must refuse");
				Require(spilled == 0, "a refused delivery must spill nothing");
				Require(StoreRows() == rowsBefore,
					"a refused delivery left a body standing in the store");
				for (int i = 0; i < StoreStacks.Length; i++)
					StoreStacks[i].RequireUnchanged("store stack " + i);
				Evidence.Append("\ncase3 store overflow: baseline held=").Append(baseline)
					.Append("; after one fixture stack=").Append(counted)
					.Append("; then +1200000000 more, so the true hold is baseline+2400000000")
					.Append("; advisory ROOM (not the wrapped hold)=").Append(advisoryRoom)
					.Append("; custody=").Append(custody).Append("; rows=").Append(rowsBefore)
					.Append("; bodies=").Append(StoreStacks[0].Evidence).Append(',')
					.Append(StoreStacks[1].Evidence);
			}

			/// <summary>Case 4. The same total in a cell. Open ground declares a bound rather than
			/// counting a capacity, so its room reading cannot fail and the hold reading carries
			/// the refusal alone. Nothing is credited, and the two bodies are untouched.</summary>
			private void GroundOverflow()
			{
				Body first = PlaceInCell(OverflowGround, 1200000000);
				int counted;
				Require(KingdomMaterials.TryGroundMaterialHeldNow(OverflowGround, Blueprint,
					out counted) && counted == 1200000000,
					"the raw census did not count the first fixture stack in the cell");
				GroundStacks = new[] { first, PlaceInCell(OverflowGround, 1200000000) };
				int rowsBefore = GroundRows(OverflowGround);
				int bound, unread;
				Require(new KingdomMaterials.GroundSpillHost(Zone, OverflowGround, Blueprint, 1)
					.TryRawRoomNow(out bound) && bound == 1,
					"open ground's declared bound must still read");
				Require(!KingdomMaterials.TryGroundMaterialHeldNow(OverflowGround, Blueprint,
					out unread), "a cell holding more than int.MaxValue must have no reading");
				Require(unread == 0, "a refused hold reading must hand back nothing");
				KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(
					new KingdomMaterials.GroundSpillHost(Zone, OverflowGround, Blueprint, 1), 1, 1);
				Require(outcome.Custody == KingdomDepositCustody.Unproved && outcome.Placed == 0,
					"a spill onto an unreadable cell must refuse and credit nothing");
				Require(GroundRows(OverflowGround) == rowsBefore,
					"a refused spill left a body standing in the cell");
				for (int i = 0; i < GroundStacks.Length; i++)
					GroundStacks[i].RequireUnchanged("ground stack " + i);
				Evidence.Append("\ncase4 ground overflow: bound=").Append(bound)
					.Append("; counted one=").Append(counted)
					.Append("; custody=").Append(outcome.Custody).Append("; placed=")
					.Append(outcome.Placed).Append("; rows=").Append(rowsBefore)
					.Append("; bodies=").Append(GroundStacks[0].Evidence).Append(',')
					.Append(GroundStacks[1].Evidence);
			}
		}
	}
}
