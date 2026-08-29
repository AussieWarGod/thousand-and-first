#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomPurposeAnchorBindingSourceTests
	{
		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		private static string Between(string Source, string Start, string End)
		{
			int first = Source.IndexOf(Start, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, Start);
			int last = Source.IndexOf(End, first + Start.Length, StringComparison.Ordinal);
			Assert.Greater(last, first, End);
			return Source.Substring(first, last - first);
		}

		private static int Count(string Source, string Needle)
		{
			int count = 0;
			for (int at = 0; (at = Source.IndexOf(Needle, at,
				StringComparison.Ordinal)) >= 0; at += Needle.Length) count++;
			return count;
		}

		[Test]
		public void CurrentRootsBindExactAuthoredRolesAndLegacySortingIsIsolated()
		{
			string helper = Read("Growth/KingdomPurposePortfolio.PairingHelpers.cs");
			string authored = Between(helper, "private static bool TryAuthoredPurposeStores(",
				"private static bool TryLegacyPurposeStores(");
			StringAssert.Contains("TryExactLayoutOwner(Root, Zone", authored);
			StringAssert.Contains("PurposeRoleCount(snapshot, \"purpose:input\")", authored);
			StringAssert.Contains("PurposeRoleCount(snapshot, \"purpose:output\")", authored);
			StringAssert.Contains("inputs == 0 && outputs == 0", authored);
			StringAssert.Contains("inputs != 1 || outputs != 1", authored);
			StringAssert.Contains("\"purpose:input\", out Input", authored);
			StringAssert.Contains("\"purpose:output\", out Output", authored);
			StringAssert.Contains("ExactPurposeStore(stock, Input)", authored);
			StringAssert.DoesNotContain("Sort(", authored);

			string legacy = Between(helper, "private static bool TryLegacyPurposeStores(",
				"/// <summary>Re-proves a disclosed legacy binding");
			StringAssert.Contains("stores.Sort((a, b) => string.CompareOrdinal", legacy);
			StringAssert.Contains("lowest exact identity is input and the next is output", legacy);
			string frozen = Between(helper, "private static bool TryFrozenPurposeStores(",
				"private static bool ExactPurposeStore(");
			StringAssert.DoesNotContain("Sort(", frozen);
			StringAssert.Contains("FindExactKnown(Zone, InputId", frozen);
			StringAssert.Contains("FindExactKnown(Zone, OutputId", frozen);
		}

		[Test]
		public void AnchorLookupPinsRootLotRectPoseComponentAndRoleInsteadOfIdOrder()
		{
			string source = Read("Growth/KingdomArchitectureStamper.AnchoredLookup.cs");
			StringAssert.Contains("KingdomConstruction.FindExactId(Z, Owner.IDIfAssigned", source);
			StringAssert.Contains("ReferenceEquals(exactOwner, Owner)", source);
			StringAssert.Contains("Intent.MainWorldX, Intent.MainWorldY", source);
			StringAssert.Contains("KingdomPlots.PlotIdProperty) != Lot", source);
			StringAssert.Contains("AnchorRoleOf(placement.StatefulAnchor) != Role", source);
			StringAssert.Contains("LastIndexOf('@')", source);
			StringAssert.Contains("matches != 1", source);
			StringAssert.Contains("TryExactOutput(Owner, Z, intent, lot, anchored", source);
			StringAssert.Contains("intent.Rect.Contains(", source);
			StringAssert.DoesNotContain("Sort(", source);
		}

		[Test]
		public void AdoptionIsSoleAuthorizedTransitionAndNeverMutatesStock()
		{
			string all = KingdomPurposeLogicalSource.Read();
			string endpoint = Read("Growth/KingdomPurposePortfolio.SecondEndpoint.cs");
			string control = Read("Growth/KingdomPurposePortfolio.OperationControl.cs");
			string interaction = Read("Growth/KingdomPurposePortfolio.Interaction.cs");
			Assert.AreEqual(2, Count(all, "TryPrepareSecondEndpoint("),
				"one declaration and one authorized transition caller only");
			StringAssert.DoesNotContain("TryPrepareSecondEndpoint(", interaction);
			StringAssert.DoesNotContain("TryPrepareSecondEndpoint(",
				Read("Growth/KingdomPurposePortfolio.Open.cs"));
			StringAssert.DoesNotContain("TryPrepareSecondEndpoint(",
				Read("Growth/KingdomPurposePortfolio.RuntimeRegistry.cs"));
			int consent = interaction.IndexOf("Popup.ShowYesNo(prompt)",
				StringComparison.Ordinal);
			int start = interaction.IndexOf("TryStartPortfolioOperation(Work, Pair",
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(consent, 0);
			Assert.GreaterOrEqual(start, 0);
			Assert.Less(consent, start);
			StringAssert.Contains("Pair?.Phase != KingdomPurposePairPhase.SecondPending", endpoint);
			StringAssert.Contains("!string.IsNullOrEmpty(Pair.SecondWorkId)", endpoint);
			StringAssert.Contains("KingdomConstructionPhase.Complete", endpoint);
			StringAssert.Contains("KingdomPhysicalPhase.EffectsSettled", endpoint);
			StringAssert.Contains("string.IsNullOrEmpty(job.InputReceiptHash)", endpoint);
			StringAssert.Contains("FullyFundedExact(job)", endpoint);
			StringAssert.Contains("InputId = input.IDIfAssigned", endpoint);
			StringAssert.Contains("OutputId = output.IDIfAssigned", endpoint);
			StringAssert.Contains("inputReceipt.TxPhase != KingdomConstructionInputTxPhase.Committed",
				endpoint);
			StringAssert.Contains("inputReceipt.RequiresObject(Pair.Operation.OutputCargoId)",
				endpoint);
			StringAssert.Contains("cargoState == KingdomPhysicalLookupState.Ambiguous", endpoint);
			StringAssert.Contains("cargoState == KingdomPhysicalLookupState.Exact && !graveyard",
				endpoint);
			foreach (string mutation in new string[]
			{
				"AddObject(", "RemoveObject(", "Obliterate(", "Destroy("
			}) StringAssert.DoesNotContain(mutation, endpoint);
			int preflight = control.IndexOf("TryPortfolioOperationPreflight(operation",
				StringComparison.Ordinal);
			int retire = control.IndexOf("TryRetireCreditedPurposeCargo(Pair.Operation)",
				StringComparison.Ordinal);
			int publish = control.IndexOf("TryPublishPortfolioPair(Pair, next",
				StringComparison.Ordinal);
			Assert.IsTrue(preflight >= 0 && retire > preflight && publish > retire);
			StringAssert.Contains("if (adoptsSecondEndpoint)", control);
			StringAssert.Contains("next.SecondInputStoreId = newSecondInput", control);
			StringAssert.Contains("next.SecondOutputStoreId = newSecondOutput", control);
			StringAssert.Contains("next.RouteDigest = newRouteDigest", control);
		}

		[Test]
		public void EveryPortfolioMapHasOneDistinctRolePairInAllFourWorldPoses()
		{
			XDocument document = XDocument.Parse(Read(
				"Architecture/KingdomArchitectures-PurposePortfolio.xml"));
			List<XElement> maps = document.Root.Elements("map").ToList();
			Assert.AreEqual(12, maps.Count);
			int poseCases = 0;
			foreach (XElement map in maps)
			{
				int width = (int)map.Attribute("Width");
				int height = (int)map.Attribute("Height");
				List<string> rows = map.Elements("row")
					.Select(row => (string)row.Attribute("Cells")).ToList();
				Assert.AreEqual(height, rows.Count, (string)map.Attribute("Key"));
				(char Glyph, int X, int Y) input = RoleCell(map, rows, "purpose:input");
				(char Glyph, int X, int Y) output = RoleCell(map, rows, "purpose:output");
				Assert.AreNotEqual(input.Glyph, output.Glyph, (string)map.Attribute("Key"));
				foreach (ArchitectureFacing facing in Enum.GetValues(
					typeof(ArchitectureFacing)))
				{
					Assert.IsTrue(KingdomArchitectureRules.TryWorldDimensions(width, height,
						facing, out int worldWidth, out int worldHeight));
					Assert.IsTrue(KingdomArchitectureRules.TryToWorld(17, 23, width, height,
						facing, input.X, input.Y, out int inputX, out int inputY));
					Assert.IsTrue(KingdomArchitectureRules.TryToWorld(17, 23, width, height,
						facing, output.X, output.Y, out int outputX, out int outputY));
					Assert.IsTrue(inputX >= 17 && inputX < 17 + worldWidth
						&& inputY >= 23 && inputY < 23 + worldHeight);
					Assert.IsTrue(outputX >= 17 && outputX < 17 + worldWidth
						&& outputY >= 23 && outputY < 23 + worldHeight);
					Assert.IsTrue(inputX != outputX || inputY != outputY,
						(string)map.Attribute("Key") + "/" + facing);
					poseCases++;
				}
			}
			Assert.AreEqual(48, poseCases);
		}

		[Test]
		public void BodyPurposeMapsExplicitlyRemainZeroAnchorLegacy()
		{
			XDocument document = XDocument.Parse(Read(
				"Architecture/KingdomArchitectures-DeepEndgame.xml"));
			string[] keys = new string[]
			{
				"deepend-chimerictheatre-xl0", "deepend-becomingannexe-xl0",
				"deepend-becomingannexe-truekin-xl0"
			};
			for (int i = 0; i < keys.Length; i++)
			{
				XElement map = document.Root.Elements("map").Single(m =>
					(string)m.Attribute("Key") == keys[i]);
				Assert.AreEqual(0, map.Elements("glyph").Count(g =>
					((string)g.Attribute("Anchors") ?? "").Split(',').Any(a =>
						a == "purpose:input" || a == "purpose:output")), keys[i]);
			}
		}

		private static (char Glyph, int X, int Y) RoleCell(XElement Map,
			List<string> Rows, string Role)
		{
			List<XElement> glyphs = Map.Elements("glyph").Where(g =>
				((string)g.Attribute("Anchors") ?? "").Split(',').Contains(Role)).ToList();
			Assert.AreEqual(1, glyphs.Count, (string)Map.Attribute("Key") + "/" + Role);
			Assert.AreEqual("yes", (string)glyphs[0].Attribute("Stateful"));
			Assert.IsNotEmpty((string)glyphs[0].Attribute("Object"));
			char glyph = ((string)glyphs[0].Attribute("Char"))[0];
			List<(int X, int Y)> cells = new List<(int X, int Y)>();
			for (int y = 0; y < Rows.Count; y++)
				for (int x = 0; x < Rows[y].Length; x++)
					if (Rows[y][x] == glyph) cells.Add((x, y));
			Assert.AreEqual(1, cells.Count, (string)Map.Attribute("Key") + "/" + Role);
			return (glyph, cells[0].X, cells[0].Y);
		}
	}
}
#endif
