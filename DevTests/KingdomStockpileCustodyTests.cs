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
			ClassicAssert.AreEqual("RoomNow,MaterialHeldNow,Create,Stacks,Stamp,CountOf,Alive,"
				+ "HeldByNobody,Discard,Insert,Landed,AnnounceUncertainCustody",
				string.Join(",", Array.ConvertAll(Array.FindAll(host.GetMethods(),
					method => !method.IsSpecialName), method => method.Name)));
			ClassicAssert.AreEqual(typeof(bool),
				host.GetMethod("Discard").ReturnType);
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
