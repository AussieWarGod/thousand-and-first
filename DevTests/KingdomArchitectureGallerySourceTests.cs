#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomArchitectureGallerySourceTests
	{
		private static string Read()
		{
			return TestMain.ReadRepositoryText(Path.Combine("Debug",
				"KingdomArchitectureGalleryWishes.cs"));
		}

		[Test]
		public void GalleryEnumeratesEveryExactVariantAndCardinalPose()
		{
			string source = Read();
			StringAssert.Contains("KingdomArchitecture.InspectMappings()", source);
			StringAssert.Contains("mapping.VariantKeys", source);
			StringAssert.Contains("facing < 4", source);
			AssertOrdered(source, "KingdomArchitecture.TryResolveVariant(Case.Mapping.BuildKey",
				"KingdomArchitectureRules.TryEncodeSnapshot(snapshot",
				"KingdomArchitectureRules.TrySnapshotHash(snapshot",
				"KingdomArchitectureIntent.Create(snapshot");
		}

		[Test]
		public void GalleryUsesTheProductionLayeredStamperAndFinalOwnerCopy()
		{
			string source = Read();
			AssertOrdered(source, "KingdomArchitectureRuntime.TryFreeze(works, intent",
				"KingdomArchitectureStamper.TryInitializeOwner(works, intent",
				"ArchitectureLayer.Ground", "ArchitectureLayer.Structure",
				"ArchitectureLayer.Object", "KingdomArchitectureStamper.TryVerifyComplete(works",
				"KingdomArchitectureStamper.TryCopyFrozenOwner(works, final",
				"KingdomArchitectureStamper.TryVerifyComplete(final");
			StringAssert.Contains("Case.Mapping.BuildingBlueprint", source);
		}

		[Test]
		public void GalleryRefusesLiveGroundAndProtectsExactCleanup()
		{
			string source = Read();
			StringAssert.Contains("KingdomPlots.ReadGround(cell, out blocker)", source);
			StringAssert.Contains("KingdomPlotRules.GroundKind.Bare", source);
			StringAssert.Contains("ConnectionCells(Zone)", source);
			StringAssert.Contains("cell.HasOpenLiquidVolume()", source);
			StringAssert.Contains("KingdomConstruction.HasActiveAt(system, Zone, cell)", source);
			AssertOrdered(source, "KingdomArchitectureStamper.TryVerifyComplete(Owner, Zone",
				"components.Count != snapshot.Placements.Count", "FrozenContents(Owner)",
				"FrozenContents(components[i])", "components[i].Destroy", "Owner.Destroy");
			StringAssert.Contains("AppendInventory(Item, \"<root>\"", source);
			StringAssert.Contains("child.Count", source);
			StringAssert.Contains("ParentKey + \"\\t\" + id", source);
			StringAssert.Contains("Depth > 64", source);
			StringAssert.Contains("!Seen.Add(Parent)", source);
			StringAssert.Contains("liquid.ComponentLiquids", source);
			StringAssert.Contains("liquid.MaxVolume", source);
			StringAssert.Contains("liquid.Flags", source);
			StringAssert.Contains("GalleryLiquidProperty, LiquidHash(Item)", source);
		}

		[Test]
		public void GalleryRequiresVersionedScreenshotVerdictBeforeCleanup()
		{
			string source = Read();
			StringAssert.Contains("Mod " + '"' + " + ModVersion", source);
			StringAssert.Contains("XRLGame.CoreVersion", source);
			StringAssert.Contains("Snapshot ", source);
			StringAssert.Contains("kingdom:archverdict", source);
			StringAssert.Contains("verdict != \"pass\" && verdict != \"fail\"", source);
			AssertOrdered(source, "string.IsNullOrEmpty(owner.GetStringProperty(GalleryVerdictProperty))",
				"TryClearExact(owner, zone");
			StringAssert.Contains("[TAF architecture-gallery]", source);
			StringAssert.Contains("deliberately bypasses stock debit", source);
			StringAssert.Contains("eligibility are deliberately bypassed", source);
			StringAssert.Contains("economy=bypassed eligibility=not-asserted", source);
		}

		[Test]
		public void HeartGalleryNeverBorrowsTheRealFoundingRelic()
		{
			string source = Read();
			AssertOrdered(source, "Snapshot.Placements[i].ExistingAuthority",
				"item.GetIntProperty(KingdomPlots.HeartRelicProperty) == 1",
				"require an isolated test zone", "GameObject.Create(existing.Blueprint)",
				"Synthetic.SetIntProperty(KingdomPlots.HeartRelicProperty, 1)");
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int at = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int next = Source.IndexOf(Terms[i], at + 1, StringComparison.Ordinal);
				Assert.Greater(next, at, "missing or out-of-order source term: " + Terms[i]);
				at = next;
			}
		}
	}
}
#endif
