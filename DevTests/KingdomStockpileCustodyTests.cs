#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Custody of a bundle a delivery made, driven against handlers that carry it off, veto its
	/// destruction, or merge it away. The defects these exist for are physical: material created
	/// twice because a delivery could not read where its bundle went, and material destroyed
	/// because a delivery assumed it still owned a body somebody else was holding.
	/// <para>
	/// These are not room arithmetic. Each case runs the real fill loop against a destination
	/// whose engine callbacks fire where the engine's do, and asserts what physically exists
	/// afterwards. <see cref="FakeStore"/> is the destination.
	/// </para>
	/// </summary>
	public class KingdomStockpileCustodyTests
	{
		// --- Custody before every mutation -----------------------------------------------------

		/// <summary>A creation handler that takes the bundle stops the delivery even though the
		/// store has room for all of it. The fence is a CUSTODY proof, not a room proof: inserting
		/// a body takes it out of whoever is holding it, so a delivery that cannot prove it holds
		/// the body may not insert, stamp, or destroy it.</summary>
		[Test]
		public void ACreationHandlerThatTakesTheBundleStopsTheDeliveryWithRoomToSpare()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnCreate = FakeStore.CarryOff;

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(1, store.Created.Count, "no replacement may be minted");
			ClassicAssert.AreEqual(0, store.Discarded.Count, "a foreign body is never destroyed");
			ClassicAssert.IsTrue(store.Created[0].Alive);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>The creation fence holds for a bundle that carries one unit too. A
		/// non-stacking material never reaches the stamp, so the fence in front of the INSERTION
		/// is the only one it ever crosses.</summary>
		[TestCase(true)]
		[TestCase(false)]
		public void ACreationHandlerThatTakesTheBundleIsRefusedStackedOrNot(bool stackable)
		{
			FakeStore store = new FakeStore { Capacity = 64, Stackable = stackable };
			store.OnCreate = FakeStore.CarryOff;

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(1, store.Created.Count);
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.AreEqual(1, store.Created[0].Count,
				"a foreign-held bundle is never stamped");
		}

		/// <summary>Equipment and implantation are custody the engine keeps apart from an
		/// inventory, and both CLEAR the inventory and the cell. A bundle a creature is wearing
		/// must not read as belonging to nobody.</summary>
		[Test]
		public void ABundleTakenIntoEquipmentCustodyIsNeitherDestroyedNorReplaced()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnCreate = (host, bundle) => { bundle.Holder = "worn by a settler"; };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.IsTrue(store.Created[0].Alive);
		}

		/// <summary>A handler that releases the destination's dedication mid-insertion leaves the
		/// bundle physically in the container. Physical membership is not eligibility: crediting
		/// the settlement's stock for material standing in something it no longer counts is the
		/// same lie by another route.</summary>
		[Test]
		public void LosingTheDestinationsDedicationIsNotALanding()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnInsert = (host, bundle) =>
			{
				bundle.Holder = "store";
				host.Held += bundle.Count;
				host.MaterialHeld += bundle.Count;
				host.Dedicated = false;
				return bundle;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed, "an ineligible destination earns no credit");
			ClassicAssert.AreEqual(1, store.Created.Count, "and no replacement is minted");
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>A handler that changes custody and THEN throws is not a retry: nothing is made
		/// again, the body it took is left alone, and the delivery reports uncertainty.</summary>
		[Test]
		public void AHandlerThatMovesTheBundleAndThenThrowsMintsNothing()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnStamp = (host, bundle) =>
			{
				FakeStore.CarryOff(host, bundle);
				throw new InvalidOperationException("a handler threw inside the stamp");
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(1, store.Created.Count, "no retry, no second bundle");
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.IsTrue(store.Created[0].Alive);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>The same fence after the stamp, again with room to spare. A count handler that
		/// relocates the bundle without filling the store used to pass the count-and-room proof
		/// outright.</summary>
		[Test]
		public void ACountHandlerThatRelocatesWithRoomToSpareStillRefuses()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnStamp = (host, bundle) =>
			{
				bundle.Holder = "the porter's pack";
				host.Held = 1;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.AreEqual(4, store.Created[0].Count, "the stamp itself is not undone");
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>A stack-count handler that carries the bundle off AND fills the store: the
		/// body must survive, the delivery must stop, and nothing may be made again.</summary>
		[Test]
		public void AStampHandlerThatCarriesTheBundleOffLeavesItStandingAndStopsTheDelivery()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnStamp = (host, bundle) =>
			{
				bundle.Holder = "the porter's pack";
				host.Held = host.Capacity;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.IsTrue(outcome.Refused);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Discarded.Count,
				"a body held by somebody else must never be obliterated");
			ClassicAssert.AreEqual(1, store.Created.Count);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>A bundle that stopped existing BEFORE the insertion is not a bundle this
		/// delivery destroyed: a creation or count handler may have merged its units into a stack
		/// elsewhere and retired the body. Nothing distinguishes the two, so it refuses rather
		/// than leaving the units to be made again.</summary>
		[Test]
		public void ABundleThatDisappearedBeforeTheInsertionIsNeverTreatedAsWithdrawn()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnStamp = (host, bundle) => { bundle.Alive = false; };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(1, store.Created.Count);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		// --- Reading is a callback too -----------------------------------------------------------

		/// <summary>A room census walks the destination's objects and asks each one its count, so
		/// it is a callback too. One that relocates the bundle must stop the delivery before the
		/// insertion, which would otherwise take the body out of whoever is holding it.</summary>
		[Test]
		public void ARoomCensusThatRelocatesTheBundleStopsBeforeTheInsertion()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnRoomRead = FakeStore.CarryOff;

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Insertions, "nothing is handed to the destination");
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.IsTrue(store.Created[0].Alive);
		}

		/// <summary>The gain census immediately before and after the insertion is RAW, so there
		/// is no callback there to relocate anything: the reading that pays the delivery cannot
		/// be the reading that moves its bundle.</summary>
		[Test]
		public void TheGainCensusIsRawAndCannotRelocateTheBundleItPaysFor()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnMaterialRead = FakeStore.CarryOff;

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(4, outcome.Placed);
			ClassicAssert.AreEqual(0, store.MaterialReads,
				"the ordinary census was never taken, so its handler never ran");
			ClassicAssert.AreEqual("store", store.Created[0].Holder);
		}

		/// <summary>
		/// (a) An ordinary room census may change the PENDING BATCH, not only the holder. A
		/// handler inside it that stamps the bundle up to nine leaves custody untouched, so a
		/// custody-only re-proof would insert nine units into a store paid for four. The final
		/// proofs are raw and are taken after it, so the mismatch is caught and the bundle is
		/// withdrawn instead.
		/// </summary>
		[Test]
		public void AnAdvisoryCensusThatChangesTheCountCannotSmuggleAnOverlargeBatchIn()
		{
			FakeStore store = new FakeStore { Capacity = 64, Stackable = false };
			store.OnRoomRead = (host, bundle) => { bundle.RawCount = 9; };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(0, store.Insertions,
				"nine units are never handed to a store asked for one");
			ClassicAssert.AreEqual(0, store.MaterialHeld);
			ClassicAssert.AreEqual(1, store.Created.Count, "and nothing is made again for it");
			ClassicAssert.AreEqual(1, store.Discarded.Count);
			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
		}

		/// <summary>And the same census filling the store to its last unit is judged on the RAW
		/// room read afterwards, not on the number it returned.</summary>
		[Test]
		public void TheBatchIsJudgedOnTheRawRoomReadTakenAfterTheAdvisoryOne()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnRoomRead = (host, bundle) => { host.Held = host.Capacity; };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(0, store.Insertions);
			ClassicAssert.AreEqual(1, store.Discarded.Count);
			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.Greater(store.RawRoomReads, 0, "the raw reading is the one that decides");
		}

		/// <summary>
		/// (b) and (c) The delivery never takes an EVENTFUL count or census at all. The ordinary
		/// count read repairs a nonpositive count and dispatches for it &mdash; on a body a
		/// previous callback may already have handed to somebody else &mdash; and an ordinary
		/// census can move row N-1 after row N-1's units are already in the total. Neither is
		/// reachable: the port declares no such reading, and these counters prove the production
		/// loop never asks the fake for one.
		/// </summary>
		[Test]
		public void TheDeliveryNeverTakesAnEventfulCountOrCensus()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.Residents.Add(new FakeBundle { RawCount = 3, Holder = "store" });
			store.Residents.Add(new FakeBundle { RawCount = 0, Holder = "store" });
			store.OnCountRead = FakeStore.CarryOff;
			store.OnRowRead = (host, resident) => { host.Residents[0].Holder = "a passing thief"; };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(4, outcome.Placed);
			ClassicAssert.AreEqual(0, store.CountReads, "no ordinary count read was ever taken");
			ClassicAssert.AreEqual(0, store.MaterialReads, "no ordinary census was ever taken");
			ClassicAssert.Greater(store.RawCountReads, 0);
			ClassicAssert.Greater(store.RawMaterialReads, 0);
			// The rows are exactly as they were: nothing repaired a count and nothing moved.
			ClassicAssert.AreEqual("store", store.Residents[0].Holder);
			ClassicAssert.AreEqual(0, store.Residents[1].RawCount,
				"a broken count reads as one and is never written back");
		}

		/// <summary>
		/// A census fallback is not a proof. A resident whose raw count is nonpositive OCCUPIES
		/// one place, exactly as the repair would make it, and the field is never written back;
		/// but the same body read as a bundle to be inserted comes back AS IT IS, so a proof
		/// built on it can fail. Reading the two the same way is what would let a malformed body
		/// pass a batch-of-one proof.
		/// </summary>
		[Test]
		public void ACensusFallbackIsNotAProofOfAnInsertableCount()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			FakeBundle broken = new FakeBundle { RawCount = 0, Holder = "store" };
			store.Residents.Add(broken);

			ClassicAssert.AreEqual(1, store.RawMaterialHeldNow(),
				"a broken resident is still one thing lying in the chest");
			ClassicAssert.AreEqual(0, broken.RawCount, "and the field is not written back");
			ClassicAssert.AreEqual(0, store.RawCountOf(broken),
				"but a body offered for insertion is proved on the field as it stands");
			ClassicAssert.AreEqual(63, store.RawRoomNow(), "and it takes up its place");
		}

		/// <summary>
		/// A malformed original is refused BEFORE the insertion. The engine's own stacking adds
		/// the incoming body's actual count to the stack it merges into, so a body carrying zero
		/// delivers nothing and one carrying minus one takes a unit OUT of what was already lying
		/// there &mdash; and the delivery would only notice afterwards, with the damage done.
		/// </summary>
		[TestCase(0)]
		[TestCase(-1)]
		public void AMalformedSingletonIsRefusedBeforeItCanBeMergedAway(int raw)
		{
			FakeStore store = new FakeStore { Capacity = 64, MergesOnEntry = true };
			FakeBundle resident = new FakeBundle { RawCount = 5, Holder = "store" };
			store.Residents.Add(resident);
			store.OnCreate = (host, bundle) => { bundle.RawCount = raw; };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 1, 1);

			ClassicAssert.AreEqual(0, store.Insertions, "nothing malformed reaches the store");
			ClassicAssert.AreEqual(5, resident.RawCount,
				"and the stack already lying there is untouched");
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(1, store.Discarded.Count);
			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
		}

		/// <summary>The control: a well-formed bundle merging into a compatible stack really does
		/// deliver, and is paid out of what the store gained.</summary>
		[Test]
		public void AWellFormedBundleThatMergesIsPaidOutOfTheStoresGain()
		{
			FakeStore store = new FakeStore
			{
				Capacity = 64, Stackable = false, MergesOnEntry = true
			};
			FakeBundle resident = new FakeBundle { RawCount = 5, Holder = "store" };
			store.Residents.Add(resident);

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 1, 1);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(1, outcome.Placed);
			ClassicAssert.AreEqual(6, resident.RawCount);
		}

		/// <summary>
		/// A bits-only resident is classified by the ORDINARY census by asking it its count, which
		/// repairs a nonpositive one and dispatches for it &mdash; from inside the walk, where the
		/// handler can raise a row the walk has already counted and leave the total describing a
		/// store that is really full. The raw census asks only what one of a thing is worth, so
		/// that handler never runs and the room proof describes the store as it stands.
		/// </summary>
		[Test]
		public void ABitsOnlyResidentCannotRearmACountRepairInsideTheRoomProof()
		{
			FakeStore store = new FakeStore { Capacity = 4 };
			FakeBundle rowA = new FakeBundle { RawCount = 1, Holder = "store" };
			FakeBundle rowB = new FakeBundle { RawCount = 1, Holder = "store", BitsOnly = true };
			store.Residents.Add(rowA);
			store.Residents.Add(rowB);
			store.OnStamp = (host, bundle) => { rowB.RawCount = 0; };
			store.OnRowRead = (host, resident) => { rowA.RawCount = 3; };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 2, 2);

			ClassicAssert.AreEqual(0, store.MaterialReads,
				"the ordinary census was never taken, so its repair never ran");
			ClassicAssert.AreEqual(0, store.RowHookRuns,
				"and no row handler ran from inside a room proof");
			ClassicAssert.AreEqual(1, rowA.RawCount, "no counted row was raised behind the walk");
			ClassicAssert.AreEqual(0, rowB.RawCount, "the broken row was not written back either");
			// The broken row still takes the one place it takes, so two units is exactly the room
			// there was, and the store ends at its stated size rather than over it.
			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(2, outcome.Placed);
			ClassicAssert.AreEqual(0, store.RawRoomNow(), "the store is exactly full, never over");
		}

		/// <summary>Losing the dedication takes the gain with it: a destination the settlement no
		/// longer counts holds nothing it can be paid for, so a vanished bundle earns nothing and
		/// the delivery stops.</summary>
		[Test]
		public void AVanishedBundleEarnsNothingFromADestinationThatLostItsDedication()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.OnInsert = (host, bundle) =>
			{
				bundle.Alive = false;
				host.MaterialHeld += 4;
				host.Dedicated = false;
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed,
				"a store the settlement no longer counts cannot pay for a landing");
			ClassicAssert.AreEqual(1, store.Created.Count);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		// --- Every batch proves its count --------------------------------------------------------

		/// <summary>
		/// A batch of ONE is proved like any other. A creation handler can leave an exclusively
		/// held stack of two standing where the delivery only ever wanted one; inserting it would
		/// put two units into a destination paid for one, and on open ground an ordinary merge
		/// would retire the body and clamp the gain back to one, settling a delivery that actually
		/// placed two.
		/// </summary>
		[Test]
		public void ASingletonBatchProvesItsCountBeforeAnythingIsInserted()
		{
			FakeStore store = new FakeStore { Capacity = 1 };
			store.OnCreate = (host, bundle) => { bundle.Count = 2; };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 1, 1);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody,
				"the bundle was ours and was withdrawn cleanly");
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Insertions,
				"a stack of two is never handed to a destination paid for one");
			ClassicAssert.AreEqual(1, store.Discarded.Count);
			ClassicAssert.AreEqual(0, store.MaterialHeld);
			ClassicAssert.AreEqual(0, store.Sayings);
		}

		/// <summary>The same proof when the miscount cannot be withdrawn: a vetoed destruction of
		/// a two-unit body leaves it standing, so the delivery refuses rather than walking on and
		/// making the units again.</summary>
		[Test]
		public void ASingletonMiscountThatCannotBeWithdrawnRefuses()
		{
			FakeStore store = new FakeStore { Capacity = 1 };
			store.OnCreate = (host, bundle) => { bundle.Count = 2; };
			store.OnDiscard = (host, bundle) => false;

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 1, 1);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Insertions);
			ClassicAssert.AreEqual(2, store.UnitsInTheWorld());
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		// --- Withdrawal is vetoable ------------------------------------------------------------

		/// <summary>Destruction can be refused. A store with no room left withdraws the bundle it
		/// just made; if that withdrawal is vetoed the body is still standing, so the delivery
		/// stops rather than walking on and making the units again elsewhere.</summary>
		[Test]
		public void AVetoedWithdrawalOnTheNoRoomPathRefusesInsteadOfWalkingOn()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnCreate = (host, bundle) => { host.Held = host.Capacity; };
			store.OnDiscard = (host, bundle) => false;

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.IsTrue(store.Created[0].Alive, "a vetoed destruction leaves the body");
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>The same veto on the stamp path, by a handler that MOVES the body before it
		/// refuses. Counting that as a withdrawal would leave real material in somebody's pack
		/// and mint its replacement.</summary>
		[Test]
		public void AVetoThatMovesTheBodyOnTheStampPathAlsoRefuses()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnStamp = (host, bundle) => { host.Held = host.Capacity; };
			store.OnDiscard = (host, bundle) =>
			{
				bundle.Holder = "a passing merchant";
				return false;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(4, store.UnitsInTheWorld());
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>And the same veto after an insertion that reached nobody.</summary>
		[Test]
		public void AVetoedWithdrawalAfterAnUnprovedInsertionRefuses()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) => null;
			store.OnDiscard = (host, bundle) => false;

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>The narrow case that must still walk on: the stamp filled the store, nobody
		/// took the bundle, and the withdrawal was allowed. The units stay to deliver and no word
		/// is said, or an ordinary full store would stop a whole load.</summary>
		[Test]
		public void AStampThatOnlyFilledTheStoreWithdrawsTheOwnerlessBundleAndSettles()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnStamp = (host, bundle) => { host.Held = host.Capacity; };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(1, store.Discarded.Count);
			ClassicAssert.IsFalse(store.Created[0].Alive);
			ClassicAssert.AreEqual(0, store.Sayings, "an accounted-for bundle says nothing");
		}

		// --- The insertion ----------------------------------------------------------------------

		/// <summary>An insertion handler moves the bundle into another inventory. The old salvage
		/// preserved the body and returned zero, leaving the units still to deliver, and the
		/// caller then created them again somewhere else: the same four units stood in the world
		/// twice.</summary>
		[Test]
		public void AnInsertionHandlerThatCarriesTheBundleOffNeverMintsAReplacement()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) =>
			{
				FakeStore.CarryOff(host, bundle);
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed, "nothing unproved may be credited");
			ClassicAssert.AreEqual(1, store.Created.Count);
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.AreEqual(4, store.UnitsInTheWorld(),
				"exactly the one bundle exists; the delivery owed four and made four");
			ClassicAssert.AreEqual(0, store.MaterialHeld);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>A bundle the insertion refused outright reached nobody, so it is withdrawn and
		/// the units stay to deliver.</summary>
		[Test]
		public void ABundleThatReachedNobodyIsWithdrawnAndTheDeliveryWalksOn()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) => null;

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.IsFalse(outcome.Refused);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(1, store.Discarded.Count);
			ClassicAssert.AreEqual(0, store.UnitsInTheWorld());
			ClassicAssert.AreEqual(0, store.Sayings);
		}

		/// <summary>A bundle that vanished into a stack already standing in this store delivered
		/// what the store itself gained OF THAT MATERIAL, and no more.</summary>
		[Test]
		public void AVanishedBundleWhoseGainCoversTheBatchIsCountedOnceAndSettles()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) =>
			{
				bundle.Alive = false;
				host.Held += 4;
				host.MaterialHeld += 4;
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(4, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Sayings);
		}

		/// <summary>A bundle that vanished leaving LESS behind took the remainder somewhere this
		/// delivery cannot read. What the store gained is credited; the rest is neither credited
		/// nor made again.</summary>
		[Test]
		public void AVanishedBundleThatLeftLessBehindCreditsTheGainAndThenStops()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) =>
			{
				bundle.Alive = false;
				host.Held += 2;
				host.MaterialHeld += 2;
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(2, outcome.Placed, "only the store's own gain is credited");
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>The gain that pays for a vanished bundle is a gain IN THAT MATERIAL. A handler
		/// that retires the timber and drops an equal count of stone leaves the store just as
		/// full, and used to be read as a full delivery of timber.</summary>
		[Test]
		public void UnrelatedMaterialArrivingCannotCreditAVanishedBundle()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) =>
			{
				bundle.Alive = false;
				host.Held += 4;
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed,
				"occupancy is not provenance; no timber arrived");
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		// --- Accounting, announcement, and faults -----------------------------------------------

		/// <summary>An ordinary delivery lands proved, is credited its whole batch, and takes back
		/// an uncertainty said about this store earlier (STANDARDS 7b).</summary>
		[Test]
		public void AProvedLandingIsCreditedInFullAndTakesBackAnEarlierUncertainty()
		{
			FakeStore store = new FakeStore { Capacity = 8, Announced = true };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(4, outcome.Placed);
			ClassicAssert.AreEqual(4, store.MaterialHeld);
			ClassicAssert.IsFalse(store.Announced, "a proved landing takes the saying back");
			ClassicAssert.AreEqual(0, store.Sayings);
		}

		/// <summary>Said once, not once a delivery.</summary>
		[Test]
		public void TheUncertaintyIsSaidOnceAcrossRepeatedDeliveries()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) =>
			{
				FakeStore.CarryOff(host, bundle);
				return null;
			};

			KingdomDepositOutcome first = KingdomDepositEngine.Fill(store, 4, 4);
			KingdomDepositOutcome second = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, first.Custody);
			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, second.Custody);
			ClassicAssert.AreEqual(1, store.Sayings);
			ClassicAssert.IsTrue(store.Announced);
		}

		/// <summary>A destination that can no longer carry the saying &mdash; a store a handler
		/// destroyed &mdash; must still not repeat itself while the delivery lasts. The seam
		/// remembers it itself when the store cannot.</summary>
		[Test]
		public void AnUnrememberableStoreIsStillSaidOnlyOnce()
		{
			FakeStore store = new FakeStore { Capacity = 8, CanRemember = false };
			store.OnInsert = (host, bundle) =>
			{
				FakeStore.CarryOff(host, bundle);
				return null;
			};

			KingdomDepositEngine.Fill(store, 4, 4);
			KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(1, store.Sayings);
			ClassicAssert.IsFalse(store.Announced, "the destroyed store recorded nothing");
		}

		/// <summary>A handler that THROWS inside an insertion does not unwind past the delivery.
		/// What earlier bundles proved into the store is still credited, and the rest is uncertain
		/// rather than lost: unwinding would discard a proved landing and orphan the live bundle
		/// without a word.</summary>
		[Test]
		public void AThrowingHandlerKeepsWhatWasProvedAndRefusesTheRest()
		{
			FakeStore store = new FakeStore
			{
				Capacity = 8, Stackable = false, ThrowOnInsertAfter = 1
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 5, 3);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(1, outcome.Placed, "the first bundle really did land");
			ClassicAssert.AreEqual(1, store.MaterialHeld);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>
		/// A diagnostic that throws must not cost the delivery its accounting. Naming a store
		/// reaches display handlers, so the saying itself can throw &mdash; and it is said from
		/// inside the fault path, where an escape would unwind past the caller and take the units
		/// this fill had already PROVED with it.
		/// </summary>
		[Test]
		public void AThrowingAnnouncementNeverCostsTheDeliveryItsProvedUnits()
		{
			FakeStore store = new FakeStore
			{
				Capacity = 8, Stackable = false, ThrowOnInsertAfter = 1, ThrowOnAnnounce = true
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 5, 3);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(1, outcome.Placed, "the first bundle really did land");
			ClassicAssert.AreEqual(1, store.MaterialHeld);
			ClassicAssert.AreEqual(1, store.Sayings);
			ClassicAssert.IsTrue(store.Announced,
				"the once flag is set before the saying, so a throwing handler cannot repeat it");
		}

		/// <summary>And a throwing saying is still said only once across deliveries.</summary>
		[Test]
		public void AThrowingAnnouncementStillSaysItOnlyOnce()
		{
			FakeStore store = new FakeStore { Capacity = 8, ThrowOnAnnounce = true };
			store.OnInsert = (host, bundle) =>
			{
				FakeStore.CarryOff(host, bundle);
				return null;
			};

			KingdomDepositOutcome first = KingdomDepositEngine.Fill(store, 4, 4);
			KingdomDepositOutcome second = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, first.Custody);
			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, second.Custody);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>Accounting is exact across several bundles: what one store took plus what is
		/// still to deliver is what the delivery started with.</summary>
		[Test]
		public void AnUnstackedFillPlacesExactlyItsRoomAndLeavesTheRestToDeliver()
		{
			FakeStore store = new FakeStore { Capacity = 3, Stackable = false };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 3, 5);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(3, outcome.Placed);
			ClassicAssert.AreEqual(2, 5 - outcome.Placed, "two units are still to deliver");
			ClassicAssert.AreEqual(3, store.MaterialHeld);
			ClassicAssert.AreEqual(3, store.Created.Count);
			ClassicAssert.AreEqual(0, store.Discarded.Count);
		}

		/// <summary>A handler that takes the SECOND bundle leaves the first one's units credited
		/// exactly once and stops there.</summary>
		[Test]
		public void AHandlerThatTakesTheSecondBundleKeepsTheFirstAndStopsThere()
		{
			FakeStore store = new FakeStore { Capacity = 5, Stackable = false };
			store.OnInsert = (host, bundle) =>
			{
				if (host.Created.Count < 2)
				{
					bundle.Holder = "store";
					host.Held += bundle.Count;
					host.MaterialHeld += bundle.Count;
					return bundle;
				}
				FakeStore.CarryOff(host, bundle);
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 5, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(1, outcome.Placed);
			ClassicAssert.AreEqual(1, store.MaterialHeld);
			ClassicAssert.AreEqual(2, store.Created.Count, "no third bundle was ever made");
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.AreEqual(2, store.UnitsInTheWorld(),
				"one unit stored and one carried off; the delivery owed four and made two");
		}

		/// <summary>Partial progress does not take back an uncertainty. A bundle that lands proved
		/// clears the saying only when the whole fill settles; a fill that ends unproved leaves it
		/// standing, and does not say it a second time either.</summary>
		[Test]
		public void ProvedProgressInsideARefusedFillNeverClearsTheSaying()
		{
			FakeStore store = new FakeStore { Capacity = 5, Stackable = false, Announced = true };
			store.OnInsert = (host, bundle) =>
			{
				if (host.Created.Count < 2)
				{
					bundle.Holder = "store";
					host.Held += bundle.Count;
					host.MaterialHeld += bundle.Count;
					return bundle;
				}
				FakeStore.CarryOff(host, bundle);
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 5, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(1, outcome.Placed);
			ClassicAssert.IsTrue(store.Announced, "an unsettled fill takes nothing back");
			ClassicAssert.AreEqual(0, store.Sayings, "and does not repeat what was already said");
		}

		/// <summary>A fill asked for nothing, or into no room, does nothing at all and says
		/// nothing.</summary>
		[TestCase(4, 0)]
		[TestCase(0, 4)]
		[TestCase(-3, 4)]
		public void AFillWithNothingToDoTouchesNothing(int room, int units)
		{
			FakeStore store = new FakeStore { Capacity = 8, Announced = true };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, room, units);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Created.Count);
			ClassicAssert.IsTrue(store.Announced, "nothing landed, so nothing is taken back");
		}

		// --- Totals that are not representable as an int ---------------------------------------
		//
		// A stack's count is the engine's own plain int field with no ceiling on it
		// (Stacker._StackCount, read back by Reader.ReadInt32 and merged by unchecked int
		// addition), so two honest stacks can total more than int.MaxValue. The censuses total in
		// a long and refuse exactly when that total is not representable as an int; an unchecked
		// sum instead comes back large and NEGATIVE, and a delivery believes it twice over: as
		// room, where minus two thousand million reopens a full chest, and as a gain, where two
		// wrapped readings agree mod 2^32 and pay for a landing nobody can see.

		/// <summary>
		/// A store whose contents do not total to a representable int has NO room reading, and the
		/// batch is refused before anything is inserted. Two stacks of 1,200,000,000 total
		/// 2,400,000,000; summed unchecked that is &minus;1,894,967,296, which would leave a
		/// capacity-64 chest reporting 1,894,967,360 places free and admit the delivery past its
		/// stated size. The parcel is already made, stamped and proved ownerless, so it is put
		/// back rather than abandoned.
		/// </summary>
		[Test]
		public void AStoreWhoseContentsExceedIntMaxValueHaveNoRoomAtAll()
		{
			FakeStore store = new FakeStore { Capacity = 64 };
			store.Residents.Add(new FakeBundle { RawCount = 1200000000, Holder = "store" });
			store.Residents.Add(new FakeBundle { RawCount = 1200000000, Holder = "store" });

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 64, 1);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed, "no room reading, so no batch is judged");
			ClassicAssert.AreEqual(0, store.Insertions, "and nothing reached the store");
			ClassicAssert.AreEqual(1, store.Created.Count, "no replacement is minted");
			ClassicAssert.AreEqual(1, store.DiscardCalls, "the made parcel is put back, exactly once");
			ClassicAssert.AreEqual(1, store.Discarded.Count);
			ClassicAssert.IsFalse(store.Created[0].Alive, "nothing is left standing in nobody's hands");
			ClassicAssert.AreEqual(1, store.Sayings, "and the founder is told once");
		}

		/// <summary>
		/// The same on open ground, where room is a declared bound and never a census: the cell's
		/// hold in the material is the ONLY reading on that path whose total can exceed
		/// <c>int.MaxValue</c>, so the material check carries it alone. Room is one, and the
		/// delivery still stops. The pre-insert reading gates EVERY landing on this parcel, the
		/// exact-body one included, because it is taken before the insertion runs.
		/// </summary>
		[Test]
		public void AGroundCellWhoseHoldExceedsIntMaxValueRefusesBeforeTheInsertion()
		{
			FakeStore ground = new FakeStore { Bound = 1, MergesOnEntry = true };
			FakeBundle first = new FakeBundle { RawCount = 1200000000, Holder = "store" };
			FakeBundle second = new FakeBundle { RawCount = 1200000000, Holder = "store" };
			ground.Residents.Add(first);
			ground.Residents.Add(second);

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(ground, 1, 1);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(1, ground.RawRoomNow(), "the bound is a whole number and holds");
			ClassicAssert.AreEqual(0, outcome.Placed);
			ClassicAssert.AreEqual(0, ground.Insertions, "nothing was handed to the cell");
			ClassicAssert.AreEqual(1, ground.DiscardCalls, "the made parcel is put back");
			ClassicAssert.IsFalse(ground.Created[0].Alive);
			ClassicAssert.AreEqual(1200000000, first.RawCount, "and what was lying there is untouched");
			ClassicAssert.AreEqual(1200000000, second.RawCount);
			ClassicAssert.AreEqual(1, ground.Sayings);
		}

		/// <summary>
		/// This is the AGGREGATE path, and only the aggregate path: the parcel stopped existing
		/// inside the destination, so the only evidence for it is the difference between the hold
		/// before and the hold after. A hold that was representable going in and is not coming out
		/// is no such evidence, so nothing is credited for THIS parcel &mdash; a parcel proved
		/// exact-body is credited on its own proof and never reaches here. The parcel is left
		/// exactly where it is: this delivery no longer owns it, and destroying or withdrawing it
		/// would obliterate goods the destination is now holding.
		/// </summary>
		[Test]
		public void AHoldThatExceedsIntMaxValueDuringAnInsertionIsNotAGain()
		{
			FakeStore store = new FakeStore { Capacity = int.MaxValue };
			store.Residents.Add(new FakeBundle { RawCount = 2147483000, Holder = "store" });
			store.Residents.Add(new FakeBundle { RawCount = 600, Holder = "store" });
			store.OnInsert = (host, bundle) =>
			{
				// The parcel goes in and stops existing, and a handler drops a hundred more units
				// of the same material beside it: 2,147,483,700 units, which is not a whole count.
				bundle.Alive = false;
				host.Residents.Add(new FakeBundle { RawCount = 100, Holder = "store" });
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 47, 1);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			// Unchecked, the two readings agree mod 2^32 and their difference is a tidy hundred,
			// which would pay this delivery its whole batch for a landing it never read.
			ClassicAssert.AreEqual(0, outcome.Placed,
				"a hold that is not representable is not evidence of a gain");
			ClassicAssert.AreEqual(1, store.Insertions);
			ClassicAssert.AreEqual(0, store.DiscardCalls,
				"a parcel already inside its destination is not this delivery's to destroy");
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>
		/// The wrapped-delta case, kept as a NEGATIVE. Stacks of 2,147,483,000 and 600 hold
		/// 2,147,483,600 &mdash; representable &mdash; and a batch of 100 merging in takes the cell
		/// to 2,147,483,700, which is not. Read unchecked, the two numbers differ by exactly the
		/// batch and the delivery is paid in full. Agreement mod 2^32 is a coincidence, never a
		/// proof of a landing, and the checked reading refuses it.
		/// </summary>
		[Test]
		public void AGainThatOnlyAgreesModuloTheWordSizeIsNotAProofOfALanding()
		{
			FakeStore ground = new FakeStore { Bound = 100, MergesOnEntry = true };
			FakeBundle stack = new FakeBundle { RawCount = 2147483000, Holder = "store" };
			ground.Residents.Add(stack);
			ground.Residents.Add(new FakeBundle { RawCount = 600, Holder = "store" });

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(ground, 100, 100);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed, "no credit is minted from a wrapped pair");
			ClassicAssert.AreEqual(2147483100, stack.RawCount,
				"the units really did merge, and are left standing where they merged");
			// The parcel's fate is UNPROVEN, not known-undelivered. Nothing here schedules a
			// replacement and nothing may mint one: the delivery stops, the caller holds its load,
			// and any further attempt is a fresh decision made against a fresh reading.
			ClassicAssert.AreEqual(1, ground.Created.Count, "and no replacement is minted");
			ClassicAssert.AreEqual(0, ground.DiscardCalls, "and nothing standing there is destroyed");
			ClassicAssert.AreEqual(1, ground.Sayings);
		}

		/// <summary>The bound is representability itself and nothing is invented: a hold of exactly
		/// <c>int.MaxValue</c> reads back, and an ordinary exact-body landing on top of it credits
		/// as it always did. A ceiling below that would refuse a legitimately large modded
		/// store.</summary>
		[Test]
		public void AHoldOfExactlyIntMaxValueStillReadsAndStillCredits()
		{
			FakeStore ground = new FakeStore { Bound = 1 };
			ground.Residents.Add(new FakeBundle { RawCount = int.MaxValue, Holder = "store" });

			ClassicAssert.AreEqual(int.MaxValue, ground.RawMaterialHeldNow());

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(ground, 1, 1);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(1, outcome.Placed);
			ClassicAssert.AreEqual(0, ground.Sayings);
		}

		/// <summary>
		/// A refusal keeps what was already PROVED. The first parcel lands exact-body &mdash; the
		/// same object, alive, carrying its stamped count, standing in this destination &mdash;
		/// which is credited on its own proof and needs no before/after aggregate at all. Only
		/// then does the destination's hold stop being representable, on each of the three
		/// readings in turn. Every case stops the delivery and says so once, and none of them
		/// takes back the unit already standing in the store.
		/// </summary>
		[TestCase("room")]
		[TestCase("material-before")]
		[TestCase("material-after")]
		public void RootReviewOverflowKeepsEarlierProvedCredit(string failure)
		{
			FakeStore store = new FakeStore { Capacity = 64, Stackable = false };
			if (failure != "room") store.Bound = 2;
			Action overflow = () =>
			{
				store.Residents.Add(new FakeBundle { RawCount = 1200000000, Holder = "store" });
				store.Residents.Add(new FakeBundle { RawCount = 1200000000, Holder = "store" });
			};
			store.OnInsert = (host, bundle) =>
			{
				if (host.Insertions == 1)
				{
					bundle.Holder = "store";
					host.Held++;
					host.MaterialHeld++;
					if (failure != "material-after") overflow();
					return bundle;
				}
				bundle.Alive = false;
				overflow();
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 2, 2);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(1, outcome.Placed, "earlier exact-body credit must survive");
			ClassicAssert.AreEqual(2, store.Created.Count, "no replacement after refusal");
			ClassicAssert.AreEqual(failure == "material-after" ? 2 : 1, store.Insertions);
			ClassicAssert.AreEqual(failure == "material-after" ? 0 : 1, store.DiscardCalls);
			ClassicAssert.IsTrue(store.Created[0].Alive);
			ClassicAssert.AreEqual("store", store.Created[0].Holder);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		// --- The seam itself ---------------------------------------------------------------------

		/// <summary>An engine-free interface with exactly the operations the law needs, so nothing
		/// above it can reach past the port and read a GameObject.</summary>
		[Test]
		public void TheDepositSeamKeepsItsExactShape()
		{
			Type host = typeof(IKingdomDepositHost);
			ClassicAssert.IsTrue(host.IsNotPublic && host.IsInterface);
			ClassicAssert.AreEqual("CustodyAnnounced",
				string.Join(",", Array.ConvertAll(host.GetProperties(),
					property => property.Name)));
			ClassicAssert.AreEqual("RoomNow,TryRawRoomNow,TryRawMaterialHeldNow,Create,Stacks,"
				+ "Stamp,RawCountOf,Alive,HeldByNobody,Discard,Insert,Landed,"
				+ "AnnounceUncertainCustody",
				string.Join(",", Array.ConvertAll(Array.FindAll(host.GetMethods(),
					method => !method.IsSpecialName), method => method.Name)));
			ClassicAssert.AreEqual(typeof(bool),
				host.GetMethod("Discard").ReturnType);
			// There is exactly ONE ordinary reading on the seam, and it is advice about room.
			// Every other reading a delivery takes is raw, so no proof can dispatch.
			ClassicAssert.IsNull(host.GetMethod("CountOf"),
				"an ordinary count read repairs and dispatches; the seam offers none");
			ClassicAssert.IsNull(host.GetMethod("MaterialHeldNow"),
				"the gain census must be raw; the seam offers no ordinary one");
			// Both raw readings SUM raw stack counts, which are the engine's own unbounded int
			// fields, so both can fail to be a whole number. The seam offers no reading that
			// answers with a number either way: a caller cannot forget to ask whether it read.
			ClassicAssert.IsNull(host.GetMethod("RawRoomNow"),
				"a room reading that cannot fail would hand back a wrapped sum as room");
			ClassicAssert.IsNull(host.GetMethod("RawMaterialHeldNow"),
				"a gain reading that cannot fail would hand back a wrapped sum as evidence");
			ClassicAssert.AreEqual(typeof(bool), host.GetMethod("TryRawRoomNow").ReturnType);
			ClassicAssert.AreEqual(typeof(bool), host.GetMethod("TryRawMaterialHeldNow").ReturnType);
			ClassicAssert.AreEqual(typeof(int),
				Enum.GetUnderlyingType(typeof(KingdomDepositCustody)));
			ClassicAssert.AreEqual("0:Settled,1:Unproved",
				string.Join(",", Array.ConvertAll((KingdomDepositCustody[])Enum.GetValues(
					typeof(KingdomDepositCustody)),
					value => ((int)value) + ":" + value)));
			Type engine = typeof(KingdomDepositEngine);
			ClassicAssert.IsTrue(engine.IsNotPublic && engine.IsAbstract && engine.IsSealed);
			Type outcome = typeof(KingdomDepositOutcome);
			ClassicAssert.IsTrue(outcome.IsNotPublic && outcome.IsSealed);
			ClassicAssert.AreEqual(0, new KingdomDepositOutcome(-5,
				KingdomDepositCustody.Settled).Placed);
		}

		/// <summary>The law lives where no engine type can reach it, and the seam is the only way
		/// down to one.</summary>
		[Test]
		public void TheDepositLawImportsNoEngineType()
		{
			foreach (string relative in new[]
			{
				"Core/KingdomDepositEngine.cs", "Core/IKingdomDepositHost.cs"
			})
			{
				string source = TestMain.ReadRepositoryText(relative);
				ClassicAssert.IsFalse(source.Contains("using XRL"), relative + " imports XRL");
				ClassicAssert.IsFalse(source.Contains("GameObject"),
					relative + " names a GameObject");
			}
		}
	}
}
#endif
