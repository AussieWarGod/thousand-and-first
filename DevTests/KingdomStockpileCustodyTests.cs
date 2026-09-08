#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Custody of a bundle a delivery made, driven against handlers that carry it off. The two
	/// defects these exist for are both physical: a delivery that could not prove where its bundle
	/// went used to CREATE the units again in the next store or on the ground, so the same
	/// material stood in the world twice; and a bundle a stack-count handler had already moved
	/// into somebody else's inventory used to be OBLITERATED on the strength of a failed stamp
	/// proof, destroying goods the delivery did not own.
	/// <para>
	/// These are not room arithmetic. Each case runs the real fill loop against a store whose
	/// engine callbacks relocate, fill, or destroy underneath it, and asserts what physically
	/// exists afterwards.
	/// </para>
	/// </summary>
	public class KingdomStockpileCustodyTests
	{
		/// <summary>A bundle the fill made. <c>Holder</c> is null while it belongs to nobody;
		/// "store" once it is standing in the destination; anything else is somebody else's
		/// inventory, which is exactly the case the delivery may not resolve by force.</summary>
		private sealed class FakeBundle
		{
			internal int Count = 1;

			internal bool Alive = true;

			internal string Holder;
		}

		/// <summary>A destination whose callbacks run where the engine's would.</summary>
		private sealed class FakeStore : IKingdomDepositHost
		{
			internal int Capacity = 8;

			internal int Held;

			internal bool Stackable = true;

			internal bool Announced;

			internal readonly List<FakeBundle> Created = new List<FakeBundle>();

			internal readonly List<FakeBundle> Discarded = new List<FakeBundle>();

			internal int Sayings;

			/// <summary>Stands in for <c>StackCountChangedEvent</c>: it runs inside the stamp.
			/// </summary>
			internal Action<FakeStore, FakeBundle> OnStamp;

			/// <summary>Stands in for the inventory handlers the insertion runs. Returns whatever
			/// the insertion accepted, which is null when it accepted nothing.</summary>
			internal Func<FakeStore, FakeBundle, object> OnInsert;

			public int RoomNow()
			{
				int room = Capacity - Held;
				return (room > 0) ? room : 0;
			}

			public int HeldNow()
			{
				return Held;
			}

			public object Create()
			{
				FakeBundle bundle = new FakeBundle();
				Created.Add(bundle);
				return bundle;
			}

			public bool Stacks(object Bundle)
			{
				return Stackable;
			}

			public void Stamp(object Bundle, int Count)
			{
				FakeBundle bundle = (FakeBundle)Bundle;
				bundle.Count = Count;
				if (OnStamp != null) OnStamp(this, bundle);
			}

			public int CountOf(object Bundle)
			{
				return ((FakeBundle)Bundle).Count;
			}

			public bool Alive(object Bundle)
			{
				return ((FakeBundle)Bundle).Alive;
			}

			public bool Ownerless(object Bundle)
			{
				FakeBundle bundle = (FakeBundle)Bundle;
				return bundle.Alive && bundle.Holder == null;
			}

			public void Discard(object Bundle)
			{
				FakeBundle bundle = (FakeBundle)Bundle;
				bundle.Alive = false;
				Discarded.Add(bundle);
			}

			public object Insert(object Bundle)
			{
				FakeBundle bundle = (FakeBundle)Bundle;
				if (OnInsert != null) return OnInsert(this, bundle);
				bundle.Holder = "store";
				Held += bundle.Count;
				return bundle;
			}

			public bool Landed(object Bundle, object Accepted, int Batch)
			{
				FakeBundle bundle = (FakeBundle)Bundle;
				return ReferenceEquals(Accepted, Bundle) && bundle.Alive
					&& bundle.Count == Batch && bundle.Holder == "store";
			}

			public bool CustodyAnnounced
			{
				get { return Announced; }
				set { Announced = value; }
			}

			public void AnnounceUncertainCustody()
			{
				Sayings++;
			}

			/// <summary>Units physically standing anywhere at all, so a duplication shows up as a
			/// number the delivery never earned.</summary>
			internal int UnitsInTheWorld()
			{
				int units = 0;
				for (int i = 0; i < Created.Count; i++)
				{
					if (Created[i].Alive) units += Created[i].Count;
				}
				return units;
			}
		}

		/// <summary>Defect 2. A stack-count handler carries the bundle into another inventory and
		/// fills the store, so the stamp proof fails on a bundle that is now somebody else's. The
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
			ClassicAssert.AreEqual(0, outcome.Placed, "nothing was proved into the store");
			ClassicAssert.AreEqual(0, store.Discarded.Count,
				"a body held by somebody else must never be obliterated");
			ClassicAssert.IsTrue(store.Created[0].Alive);
			ClassicAssert.AreEqual("the porter's pack", store.Created[0].Holder);
			ClassicAssert.AreEqual(1, store.Created.Count,
				"no replacement may be minted for a bundle that still exists");
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>The narrower case the same seam must still handle: the stamp filled the store
		/// but nobody took the bundle, so it reached nobody, is withdrawn, and the delivery walks
		/// on to the next store without a word.</summary>
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

		/// <summary>Defect 1. An insertion handler moves the bundle into another inventory. The
		/// old salvage preserved the body and returned zero, leaving the units still to deliver,
		/// and the caller then created them again somewhere else: the same four units stood in
		/// the world twice.</summary>
		[Test]
		public void AnInsertionHandlerThatCarriesTheBundleOffNeverMintsAReplacement()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) =>
			{
				bundle.Holder = "a passing merchant";
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(0, outcome.Placed, "nothing unproved may be credited");
			ClassicAssert.AreEqual(1, store.Created.Count);
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.IsTrue(store.Created[0].Alive);
			ClassicAssert.AreEqual(4, store.UnitsInTheWorld(),
				"exactly the one bundle exists; the delivery owed four and made four");
			ClassicAssert.AreEqual(0, store.Held);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>A bundle the insertion refused outright reached nobody, so it is withdrawn and
		/// the units stay to deliver. This is the path that must NOT be turned into a refusal, or
		/// an ordinary full store would stop a whole load.</summary>
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
		/// what the store itself gained, and no more. A gain covering the batch settles it.
		/// </summary>
		[Test]
		public void AVanishedBundleWhoseGainCoversTheBatchIsCountedOnceAndSettles()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) =>
			{
				bundle.Alive = false;
				host.Held += 4;
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(4, outcome.Placed);
			ClassicAssert.AreEqual(0, store.Sayings);
		}

		/// <summary>And a bundle that vanished leaving LESS behind took the remainder somewhere
		/// this delivery cannot read. What the store gained is credited; the rest is neither
		/// credited nor made again.</summary>
		[Test]
		public void AVanishedBundleThatLeftLessBehindCreditsTheGainAndThenStops()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) =>
			{
				bundle.Alive = false;
				host.Held += 2;
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(2, outcome.Placed, "only the store's own gain is credited");
			ClassicAssert.AreEqual(1, store.Created.Count);
			ClassicAssert.AreEqual(1, store.Sayings);
		}

		/// <summary>An ordinary delivery lands proved, is credited its whole batch, and takes back
		/// an uncertainty said about this store earlier (STANDARDS 7b).</summary>
		[Test]
		public void AProvedLandingIsCreditedInFullAndTakesBackAnEarlierUncertainty()
		{
			FakeStore store = new FakeStore { Capacity = 8, Announced = true };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(4, outcome.Placed);
			ClassicAssert.AreEqual(4, store.Held);
			ClassicAssert.IsFalse(store.Announced, "room to land takes the saying back");
			ClassicAssert.AreEqual(0, store.Sayings);
		}

		/// <summary>Said once, not once a delivery. Two loads into the same uncertain store leave
		/// the founder with one line.</summary>
		[Test]
		public void TheUncertaintyIsSaidOnceAcrossRepeatedDeliveries()
		{
			FakeStore store = new FakeStore { Capacity = 8 };
			store.OnInsert = (host, bundle) =>
			{
				bundle.Holder = "a passing merchant";
				return null;
			};

			KingdomDepositOutcome first = KingdomDepositEngine.Fill(store, 4, 4);
			KingdomDepositOutcome second = KingdomDepositEngine.Fill(store, 4, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, first.Custody);
			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, second.Custody);
			ClassicAssert.AreEqual(1, store.Sayings);
			ClassicAssert.IsTrue(store.Announced);
		}

		/// <summary>Accounting is exact across several bundles: what one store took plus what is
		/// still to deliver is what the delivery started with, and every bundle made is either
		/// standing in the store or withdrawn.</summary>
		[Test]
		public void AnUnstackedFillPlacesExactlyItsRoomAndLeavesTheRestToDeliver()
		{
			FakeStore store = new FakeStore { Capacity = 3, Stackable = false };

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 3, 5);

			ClassicAssert.AreEqual(KingdomDepositCustody.Settled, outcome.Custody);
			ClassicAssert.AreEqual(3, outcome.Placed);
			ClassicAssert.AreEqual(2, 5 - outcome.Placed, "two units are still to deliver");
			ClassicAssert.AreEqual(3, store.Held);
			ClassicAssert.AreEqual(3, store.Created.Count);
			ClassicAssert.AreEqual(0, store.Discarded.Count);
		}

		/// <summary>A handler that takes the SECOND bundle leaves the first one's units credited
		/// exactly once and stops there. The first bundle is in the store, the second is standing
		/// where the handler put it, and nothing was made a third time.</summary>
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
					return bundle;
				}
				bundle.Holder = "a passing merchant";
				return null;
			};

			KingdomDepositOutcome outcome = KingdomDepositEngine.Fill(store, 5, 4);

			ClassicAssert.AreEqual(KingdomDepositCustody.Unproved, outcome.Custody);
			ClassicAssert.AreEqual(1, outcome.Placed);
			ClassicAssert.AreEqual(1, store.Held);
			ClassicAssert.AreEqual(2, store.Created.Count, "no third bundle was ever made");
			ClassicAssert.AreEqual(0, store.Discarded.Count);
			ClassicAssert.AreEqual(2, store.UnitsInTheWorld(),
				"one unit stored and one carried off; the delivery owed four and made two");
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

		/// <summary>The seam itself: an engine-free interface with exactly the operations the law
		/// needs, so nothing above it can reach past the port and read a GameObject.</summary>
		[Test]
		public void TheDepositSeamKeepsItsExactShape()
		{
			Type host = typeof(IKingdomDepositHost);
			ClassicAssert.IsTrue(host.IsNotPublic && host.IsInterface);
			ClassicAssert.AreEqual("CustodyAnnounced",
				string.Join(",", Array.ConvertAll(host.GetProperties(),
					property => property.Name)));
			ClassicAssert.AreEqual("RoomNow,HeldNow,Create,Stacks,Stamp,CountOf,Alive,Ownerless,"
				+ "Discard,Insert,Landed,AnnounceUncertainCustody",
				string.Join(",", Array.ConvertAll(Array.FindAll(host.GetMethods(),
					method => !method.IsSpecialName), method => method.Name)));
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
