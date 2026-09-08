#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomQuickstartGrantScopeTests
	{
		private sealed class Item
		{
			internal string Name;
			internal bool Alive = true;
			internal string Cell;
			internal Item Holder;
			internal readonly List<Item> Contents = new List<Item>();
			public override bool Equals(object Other) { return (Other as Item)?.Name == Name; }
			public override int GetHashCode() { return Name == null ? 0 : Name.GetHashCode(); }
		}

		private sealed class PhysicalWorld
		{
			internal readonly List<Item> Objects = new List<Item>();
			internal readonly List<string> RemovalOrder = new List<string>();
			internal readonly List<bool> RemovalFences = new List<bool>();
			internal bool Fenced;
			internal string RefusedRemoval;
			internal string ThrowRemoval;
			internal bool ThrowAfterRemoval;
			internal int FactoryCalls;

			internal Item Create(string Name)
			{
				FactoryCalls++;
				Item item = new Item { Name = Name };
				Objects.Add(item);
				return item;
			}

			internal void Insert(Item Holder, Item Child)
			{
				if (Child.Holder != null) Child.Holder.Contents.RemoveAll(i => ReferenceEquals(i, Child));
				Child.Cell = null;
				Child.Holder = Holder;
				Holder.Contents.Add(Child);
			}

			internal bool Remove(Item Item)
			{
				RemovalOrder.Add(Item.Name);
				RemovalFences.Add(Fenced);
				if (Item.Name == RefusedRemoval || Item.Contents.Count != 0) return false;
				if (Item.Name == ThrowRemoval && !ThrowAfterRemoval) throw new Exception("before removal");
				if (Item.Holder != null) Item.Holder.Contents.RemoveAll(i => ReferenceEquals(i, Item));
				Item.Holder = null;
				Item.Cell = null;
				Item.Alive = false;
				if (Item.Name == ThrowRemoval) throw new Exception("after removal");
				return true;
			}

			internal bool Execute(Func<KingdomQuickstartGrantScope<Item>, Item> Prepare,
				Func<Item, bool> Verify, out Item Grant)
			{
				return KingdomQuickstartGrantScope<Item>.TryExecute(Prepare, Verify, Remove,
					() => Fenced, () => Fenced = true, () => Fenced = false, out Grant);
			}

			internal Item Prepared(KingdomQuickstartGrantScope<Item> Scope)
			{
				Item root = Scope.Create(() => Create("root"));
				Item child = Scope.Create(() => Create("child"));
				Insert(root, child);
				root.Cell = "role";
				return root;
			}
		}

		[TestCase("water")]
		[TestCase("larder")]
		[TestCase("materials")]
		[TestCase("advisor")]
		public void PostPlacementRefusalRemovesEveryFreshObjectBeforeRetry(string Role)
		{
			PhysicalWorld world = new PhysicalWorld();
			Assert.That(world.Execute(scope =>
			{
				Item root = world.Prepared(scope);
				root.Name = Role;
				return root;
			}, root => false, out Item refused), Is.False);
			Assert.That(refused, Is.Null);
			Assert.That(world.Objects.All(i => !i.Alive && i.Holder == null && i.Cell == null), Is.True);
			Assert.That(world.RemovalOrder, Is.EqualTo(new[] { "child", Role }));
			Assert.That(world.RemovalFences.All(value => value), Is.True);
			Assert.That(world.Fenced, Is.False);
			Assert.That(world.Execute(world.Prepared, root => true, out Item retry), Is.True);
			Assert.That(world.Objects.Count(i => i.Alive), Is.EqualTo(2));
			Assert.That(retry.Contents.Count, Is.EqualTo(1));
		}

		[TestCase("before child insertion")]
		[TestCase("after child insertion")]
		[TestCase("before root placement")]
		[TestCase("after root placement")]
		[TestCase("verification")]
		public void MutationExceptionsCleanTrackedObjects(string Cut)
		{
			PhysicalWorld world = new PhysicalWorld();
			Assert.Throws<InvalidOperationException>(() => world.Execute(scope =>
			{
				Item root = scope.Create(() => world.Create("root"));
				Item child = scope.Create(() => world.Create("child"));
				if (Cut == "before child insertion") throw new InvalidOperationException(Cut);
				world.Insert(root, child);
				if (Cut == "after child insertion" || Cut == "before root placement")
					throw new InvalidOperationException(Cut);
				root.Cell = "role";
				if (Cut == "after root placement") throw new InvalidOperationException(Cut);
				return root;
			}, root => { throw new InvalidOperationException(Cut); }, out _));
			Assert.That(world.Objects.Any(i => i.Alive), Is.False);
			Assert.That(world.RemovalOrder, Is.EqualTo(new[] { "child", "root" }));
			Assert.That(world.Fenced, Is.False);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void RejectedOrMovedChildAndForeignReturnKeepExactOwnership(bool Moved)
		{
			PhysicalWorld world = new PhysicalWorld();
			Item foreign = world.Create("foreign holder");
			Assert.That(world.Execute(scope =>
			{
				scope.Create(() => world.Create("root"));
				Item child = scope.Create(() => world.Create("child"));
				if (Moved) world.Insert(foreign, child);
				// Inventory callback returned a foreign reference, never a factory allocation.
				return foreign;
			}, item => true, out Item result), Is.False);
			Assert.That(result, Is.Null);
			Assert.That(foreign.Alive, Is.True);
			Assert.That(foreign.Contents, Is.Empty);
			Assert.That(world.Objects.Count(i => i.Alive), Is.EqualTo(1));
			Assert.That(world.Fenced, Is.False);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void MovedRootIsRemovedEvenWhenPlacementCallbackThrows(bool Throw)
		{
			PhysicalWorld world = new PhysicalWorld();
			Func<KingdomQuickstartGrantScope<Item>, Item> place = scope =>
			{
				Item root = world.Prepared(scope);
				root.Cell = "another zone";
				if (Throw) throw new InvalidOperationException("moved");
				return root;
			};
			if (Throw) Assert.Throws<InvalidOperationException>(() =>
				world.Execute(place, root => root.Cell == "role", out _));
			else Assert.That(world.Execute(place, root => root.Cell == "role", out _), Is.False);
			Assert.That(world.Objects.Any(i => i.Alive || i.Cell != null), Is.False);
		}

		[Test]
		public void ForeignContentsPreventRootDestructionAndFenceReplacement()
		{
			PhysicalWorld world = new PhysicalWorld();
			Item foreign = world.Create("foreign");
			Assert.That(world.Execute(scope =>
			{
				Item root = world.Prepared(scope);
				world.Insert(root, foreign);
				return root;
			}, root => false, out _), Is.False);
			Assert.That(foreign.Alive, Is.True);
			Assert.That(foreign.Holder.Alive, Is.True);
			Assert.That(world.Objects.Single(i => i.Name == "child").Alive, Is.False);
			Assert.That(world.Fenced, Is.True);
			int before = world.FactoryCalls;
			Assert.That(world.Execute(world.Prepared, root => true, out _), Is.False);
			Assert.That(world.FactoryCalls, Is.EqualTo(before));
		}

		[TestCase("refuse")]
		[TestCase("throw before")]
		[TestCase("throw after")]
		public void UnprovedCleanupKeepsFenceAndStillAttemptsRemainingAllocations(string Failure)
		{
			PhysicalWorld world = new PhysicalWorld();
			if (Failure == "refuse") world.RefusedRemoval = "child";
			else { world.ThrowRemoval = "child"; world.ThrowAfterRemoval = Failure == "throw after"; }
			Assert.That(world.Execute(world.Prepared, root => false, out _), Is.False);
			Assert.That(world.RemovalOrder, Is.EqualTo(new[] { "child", "root" }));
			Assert.That(world.RemovalFences.All(value => value), Is.True);
			Assert.That(world.Fenced, Is.True);
			int before = world.FactoryCalls;
			Assert.That(world.Execute(world.Prepared, root => true, out _), Is.False);
			Assert.That(world.FactoryCalls, Is.EqualTo(before));
		}

		[Test]
		public void FactoryThrowWithoutReturnedIdentityLeavesFenceAfterKnownObjectsCleaned()
		{
			PhysicalWorld world = new PhysicalWorld();
			Assert.Throws<InvalidOperationException>(() => world.Execute(scope =>
			{
				scope.Create(() => world.Create("root"));
				return scope.Create(() =>
				{
					world.Create("unknown allocation");
					throw new InvalidOperationException("factory interrupted");
				});
			}, root => true, out _));
			Assert.That(world.Objects.Single(i => i.Name == "root").Alive, Is.False);
			Assert.That(world.Fenced, Is.True);
			int before = world.FactoryCalls;
			Assert.That(world.Execute(world.Prepared, root => true, out _), Is.False);
			Assert.That(world.FactoryCalls, Is.EqualTo(before));
		}

		[Test]
		public void VerifiedGrantRemainsForReceiptRecoveryWithoutAnotherFactoryCall()
		{
			PhysicalWorld world = new PhysicalWorld();
			Assert.That(world.Execute(world.Prepared, root => true, out Item grant), Is.True);
			Assert.That(world.RemovalOrder, Is.Empty);
			Assert.That(world.Fenced, Is.False);
			Assert.That(KingdomQuickstartRules.RecoveryAction(KingdomQuickstartPhase.Founded,
				KingdomQuickstartPhase.WaterStocked, KingdomQuickstartGrantObservation.ExactPlaced),
				Is.EqualTo(KingdomQuickstartRecoveryAction.PublishExisting));
			// Receipt publication is separate from fresh allocation; recovery keeps the same object.
			Assert.That(world.Objects.Single(i => i.Alive && i.Cell == "role"), Is.SameAs(grant));
			Assert.That(world.FactoryCalls, Is.EqualTo(2));
		}

		[Test]
		public void NullPreparationResultCleansMalformedFactoryAllocation()
		{
			PhysicalWorld world = new PhysicalWorld();
			Assert.That(world.Execute(scope =>
			{
				scope.Create(() => world.Create("malformed root"));
				return null;
			}, root => true, out Item grant), Is.False);
			Assert.That(grant, Is.Null);
			Assert.That(world.Objects.Single().Alive, Is.False);
			Assert.That(world.Fenced, Is.False);
		}

		[Test]
		public void AllocationTrackingAndForeignReturnUseReferenceIdentity()
		{
			PhysicalWorld world = new PhysicalWorld();
			Item foreign = world.Create("same value");
			Assert.That(world.Execute(scope =>
			{
				scope.Create(() => world.Create("same value"));
				scope.Create(() => world.Create("same value"));
				return foreign;
			}, root => true, out _), Is.False);
			Assert.That(world.RemovalOrder.Count, Is.EqualTo(2));
			Assert.That(world.Objects.Count(i => i.Alive), Is.EqualTo(1));
			Assert.That(foreign.Alive, Is.True);
		}

		[Test]
		public void ForeignQuarantineAppearingDuringVerificationIsPreserved()
		{
			PhysicalWorld world = new PhysicalWorld();
			string state = null;
			Assert.That(KingdomQuickstartGrantScope<Item>.TryExecute(world.Prepared,
				root => { state = "foreign"; return true; }, world.Remove, () => state != null,
				() => { if (state == null) state = "owned token"; },
				() => { if (state == "owned token") state = null; }, out _), Is.False);
			Assert.That(state, Is.EqualTo("foreign"));
			Assert.That(world.Objects.Any(i => i.Alive), Is.False);
		}

		[Test]
		public void RuntimeWiresEveryFreshCreatorAndPreservesEveryQuarantineStorageType()
		{
			string creators = TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Stock.cs")
				+ TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Materials.cs")
				+ TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Advisor.cs");
			Assert.That(creators.Split(new[] { "bool created = TryCreateFreshGrant(" },
				StringSplitOptions.None).Length - 1, Is.EqualTo(4));
			StringAssert.DoesNotContain("Obliterate(", creators);
			StringAssert.Contains("BeforeObjectCreated: obj => obj.SetIntProperty(\"NoLoot\", 1)", creators);
			string adapter = TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Allocations.cs");
			int destroy = adapter.IndexOf("Object.Obliterate(", StringComparison.Ordinal);
			int firstContentsCheck = adapter.IndexOf("HasUnprovedGrantContents(Object)",
				StringComparison.Ordinal);
			int lastContentsCheck = adapter.LastIndexOf("HasUnprovedGrantContents(Object)",
				StringComparison.Ordinal);
			Assert.That(firstContentsCheck, Is.GreaterThan(0));
			Assert.That(destroy, Is.GreaterThan(firstContentsCheck));
			Assert.That(lastContentsCheck, Is.GreaterThan(destroy),
				"destruction callbacks cannot clear the fence after adding unproved contents");
			foreach (string storage in new[] { "String", "Int", "Int64", "Boolean", "Object" })
				StringAssert.Contains("Game." + storage + "GameState?.ContainsKey(key)", adapter);
			StringAssert.Contains("Game.StringGameState.Remove(KingdomQuickstartRules.QuarantineState)", adapter);
			Assert.That(KingdomRemovalCoverage.GlobalDisposition(KingdomQuickstartRules.QuarantineState),
				Is.EqualTo(KingdomRemovalGlobalDisposition.Preserve));
		}
	}
}
#endif
