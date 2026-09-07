#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomDelveLinkSourceTests
	{
		private static string Link()
		{
			return KingdomDelveLinkLogicalSource.Read();
		}

		private static string Plot()
		{
			return KingdomPlot2LogicalSource.Read();
		}

		private static string Materials()
		{
			return KingdomMaterialsLogicalSource.Read();
		}

		private static string Delve()
		{
			return TestMain.ReadRepositoryText(Path.Combine("Growth", "KingdomDelve.cs"));
		}

		[Test]
		public void AuthoredHeadHasOneOwnedDownAndNoCosmeticSameMapPair()
		{
			string xml = TestMain.ReadRepositoryText(Path.Combine("Architecture",
				"KingdomArchitectures-DeepEndgame.xml"));
			StringAssert.Contains("Blueprint=\"r_KingdomDelveDown\"", xml);
			ClassicAssert.AreEqual(2, Count(xml, "Anchors=\"travel:down\""));
			ClassicAssert.AreEqual(0, Count(xml, "Anchors=\"travel:up\""));
			ClassicAssert.AreEqual(0, Count(xml, "Blueprint=\"StairsDown\""));
			ClassicAssert.AreEqual(0, Count(xml, "Blueprint=\"StairsUp\""));

			string blueprints = TestMain.ReadRepositoryText("ObjectBlueprints.xml");
			StringAssert.Contains("Name=\"r_KingdomDelveDown\" Inherits=\"StairsDown\"", blueprints);
			StringAssert.Contains("ConnectionObject=\"r_KingdomDelveUp\"", blueprints);
			StringAssert.Contains("Name=\"r_KingdomDelveUp\" Inherits=\"StairsUp\"", blueprints);
			StringAssert.Contains("ConnectionObject=\"r_KingdomDelveDown\"", blueprints);
		}

		[Test]
		public void NoSpendPreflightRequiresClaimAndBuiltFootBeforeAnyLoadOrMutation()
		{
			string preflight = Between(Link(), "public static bool TryPreflight(",
				"public static bool TrySettle(");
			AssertOrdered(preflight,
				"TryDerive(Architecture, Head",
				"System.ClaimedZones.Contains(derived.FootZoneId)",
				"The.ZoneManager.IsZoneBuilt(derived.FootZoneId)",
				"The.ZoneManager.GetZone(derived.FootZoneId)",
				"TrySafeFoot(System, foot, derived",
				"EmptyConnectionCell(derived.HeadZoneId",
				"EmptyConnectionCell(derived.FootZoneId");
			ClassicAssert.IsFalse(preflight.Contains("GameObject.Create"));
			ClassicAssert.IsFalse(preflight.Contains("AddObject"));
			ClassicAssert.IsFalse(preflight.Contains("SetIntProperty"));
			ClassicAssert.IsFalse(preflight.Contains("SetStringProperty"));

			string prepare = Between(Plot(),
				"internal static bool TryPreparePlotPayload(KingdomSystem System, Zone Z,\n\t\t\tKingdomPlotRules.PlotRect Rect, string BuildKey, string LotType, string SkinKey,",
				"internal static bool TryEncodePlotPayload(");
			AssertOrdered(prepare, "KingdomArchitectureRuntime.TryPrepare(System, Z",
				"KingdomArchitectureStamper.TryPreflight(System, Z, prepared, claim",
				"KingdomDelveLink.TryPreflight(System, Z, prepared",
				"TryEncodePlotPayload(Rect, SkinKey, prepared");
			ClassicAssert.IsFalse(prepare.Contains("Reserve"));
			ClassicAssert.IsFalse(prepare.Contains("TryDebit"));
		}

		[Test]
		public void DurableSettlementUsesFrozenRootAndSchemaLastPhaseReceipts()
		{
			string source = Link();
			string settle = Between(source, "public static bool TrySettle(",
				"public static bool TryPreflightStrike(");
			StringAssert.Contains("KingdomArchitectureStamper.TryReadOwner(Owner", settle);
			StringAssert.Contains("KingdomArchitectureRuntime.TryDecode(Architecture", source);
			ClassicAssert.IsFalse(settle.Contains("KingdomArchitecture.TryResolve"));
			ClassicAssert.IsFalse(settle.Contains("KingdomArchitecture.TryGetMapping"));
			ClassicAssert.IsFalse(settle.Contains("KingdomData"));

			string initialize = Between(source, "private static bool TryInitializeRoot(",
				"private static bool TryReadRoot(");
			int schema = initialize.IndexOf(
				"Owner.SetIntProperty(SchemaProperty, LinkSchema);", StringComparison.Ordinal);
			ClassicAssert.Greater(schema, initialize.IndexOf("Owner.SetIntProperty(PhaseProperty, 0)",
				StringComparison.Ordinal));
			ClassicAssert.AreEqual(schema, initialize.LastIndexOf("Owner.Set", StringComparison.Ordinal));

			string stamp = Between(source, "private static void StampEndpoint(",
				"private static bool ExactEndpoint(");
			ClassicAssert.AreEqual(stamp.IndexOf(
				"Endpoint.SetIntProperty(EndpointSchemaProperty, EndpointSchema);",
				StringComparison.Ordinal), stamp.LastIndexOf("Endpoint.Set", StringComparison.Ordinal));

			string foot = Between(source, "private static bool TrySettleFootEndpoint(",
				"private static void StampEndpoint(");
			AssertOrdered(foot, "FindEndpointByToken(Foot", "TrySafeFoot(null, Foot",
				"GameObject.Create(UpBlueprint)", "StampEndpoint(endpoint, Derived, FootRole)",
				"AddObject(endpoint", "FindEndpointByToken(Foot",
				"string endpointId = created ? endpoint.ID : endpoint.IDIfAssigned",
				"Owner.SetStringProperty(FootEndpointProperty, endpointId)",
				"Owner.SetIntProperty(PhaseProperty, 2)");
		}

		[Test]
		public void ReachReprovesBothPhysicalEndpointsWithoutGeneratingZones()
		{
			string physical = Between(Link(), "public static bool PhysicalLinkStands(",
				"public static bool HasPhysicalState(");
			AssertOrdered(physical,
				"The.ZoneManager.IsZoneBuilt(receipt.HeadZoneId)",
				"The.ZoneManager.IsZoneBuilt(receipt.FootZoneId)",
				"The.ZoneManager.GetZone(receipt.HeadZoneId)",
				"The.ZoneManager.GetZone(receipt.FootZoneId)",
				"FindExactEndpoint(head, receipt.RootId",
				"FindExactEndpoint(head, receipt.HeadEndpointId",
				"FindExactEndpoint(foot, receipt.FootEndpointId",
				"CountExactConnection(receipt.FootZoneId",
				"CountExactConnection(receipt.HeadZoneId");
			ClassicAssert.IsFalse(physical.Contains("GenerateZone"));
			ClassicAssert.IsFalse(physical.Contains("ZoneBuilders"));
			StringAssert.Contains("ExactString(root, ReceiptProperty, encoded)", physical);
			StringAssert.Contains("CountEndpointAt(head.GetCell(receipt.X, receipt.Y), receipt.Token, null) == 1", physical);
			StringAssert.DoesNotContain("KingdomSurvey.Take", physical);
			StringAssert.Contains("CountPartAt(foot.GetCell(receipt.X, receipt.Y), \"StairsUp\") == 1",
				physical);
			StringAssert.Contains("IsPassable(null, false)", physical);
			StringAssert.Contains("HasOpenLiquidVolume()", physical);

			string stands = Between(Delve(), "public static bool ShaftStands(",
				"public static void RecordShaft(");
			AssertOrdered(stands, "KingdomDelveLink.HasPhysicalState(ZoneId)",
				"KingdomDelveLink.PhysicalLinkStands(ZoneId)",
				"GetIntGameState(ShaftState + ZoneId)");
			StringAssert.Contains("Old saves remain readable", stands);
		}

		[Test]
		public void FinalAndStrikeHooksRespectPhysicalCommitBoundaries()
		{
			string plot = Plot();
			string finish = Between(plot, "private static bool Finish(r_KingdomPlotWorks Works,",
				"private static bool FinishPlotEffects(");
			AssertOrdered(finish, "KingdomArchitectureStamper.TryVerifyComplete(parent",
				"KingdomArchitectureStamper.TryCopyFrozenOwner(parent, building",
				"r_KingdomScaffold.HasRemovalProof(building, predecessorId)",
				"KingdomDelveLink.TrySettle(building, Z",
				"KingdomConstruction.Complete(ref construction)");

			string materials = Materials();
			int architecture = materials.IndexOf(
				"KingdomArchitectureStamper.TryPreflightStrike(Building, Z", StringComparison.Ordinal);
			int link = materials.IndexOf("KingdomDelveLink.TryPreflightStrike(Building, Z",
				architecture, StringComparison.Ordinal);
			int intent = materials.IndexOf("KingdomStrikeIntent intent =", link,
				StringComparison.Ordinal);
			ClassicAssert.Greater(link, architecture);
			ClassicAssert.Greater(intent, link);

			string continueStrike = Between(materials, "private static void ContinueStrike(",
				"private static void RemoveStrikePlotPart(");
			AssertOrdered(continueStrike, "RemoveStrikePlotPart(Z, intent",
				"KingdomDelveLink.TryFinishStrike(Building, Z",
				"RemoveStrikePredecessor(Z, Building");

			string remove = Between(Link(), "public static bool TryFinishStrike(",
				"public static bool TryReadPhysicalReceipt(");
			AssertOrdered(remove, "TryManagedStrikeLane(Owner, Head", "TryStrikeBase(Owner, Head");
			AssertOrdered(remove, "footEndpoint.Destroy(null, Silent: true)",
				"Head.RemoveZoneConnection(\"d\"",
				"foot.RemoveZoneConnection(\"u\"",
				"EmptyConnectionCell(derived.HeadZoneId",
				"SetStringGameState(LinkState + derived.HeadZoneId, Tombstone)");

			string managed = Between(Link(), "private static bool TryManagedStrikeLane(",
				"private static bool TryStrikeBase(");
			AssertOrdered(managed, "HasAnyRootField(Owner)",
				"KingdomUpgrade.BuildKeyProperty", "KingdomDelveRules.IsDelve(buildKey)",
				"ReadState(Head.ZoneID)", "KingdomArchitectureRuntime.SchemaProperty",
				"KingdomArchitectureRuntime.TryRead(Owner",
				"KingdomArchitectureRules.IsManagedSnapshotEncoding(architecture.EncodedSnapshot)");
			StringAssert.Contains("Explicit read-only legacy architecture", managed);
		}

		[Test]
		public void PublicDocsNamePhysicalPersistenceReachLossAndThirdPartyBoundary()
		{
			string api = TestMain.ReadRepositoryText(Path.Combine("docs", "API.md"));
			StringAssert.Contains("`KingdomDelveLink` is the engine-coupled proof", api);
			StringAssert.Contains("schema-last phase writes", api);
			StringAssert.Contains("Missing,", api);
			StringAssert.Contains("moved, corrupt, duplicated, or obstructed evidence", api);
			StringAssert.Contains("consulted only when no new physical-link state exists", api);

			string modding = TestMain.ReadRepositoryText("MODDING.md");
			StringAssert.Contains("### Physical delve architecture", modding);
			StringAssert.Contains("`r_KingdomDelveDown`", modding);
			StringAssert.Contains("Do not place `r_KingdomDelveUp`, raw `StairsDown`/`StairsUp`",
				modding);
			StringAssert.Contains("write `r_TAF_DelveLink*` or `r_TAF_DelveEndpoint*`",
				modding);
			StringAssert.Contains("foreign objects are a refusal, never cleanup targets", modding);
		}

		[Test]
		public void DelveCompletionNamesItsExactSettlementRatherThanTheSeatCursor()
		{
			string plot = Plot();
			ClassicAssert.GreaterOrEqual(Count(plot,
				"TryExactSettlementName(System, Z, out string settlementName)"), 2);
			StringAssert.Contains("SettlementIdForOwnedZone(Z?.ZoneID)", plot);
			StringAssert.Contains("System.TryFindSettlement(id", plot);
			StringAssert.DoesNotContain("ShaftOpens(KingdomPresentation.Rich(system.SeatName))",
				plot);
			StringAssert.DoesNotContain("ShaftOpens(KingdomPresentation.Rich(System.SeatName))",
				plot);
		}

		private static int Count(string Source, string Term)
		{
			int count = 0;
			int at = 0;
			while ((at = Source.IndexOf(Term, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += Term.Length;
			}
			return count;
		}

		private static string Between(string Source, string Start, string End)
		{
			int start = Source.IndexOf(Start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(start, 0, "missing source boundary: " + Start);
			int end = Source.IndexOf(End, start + Start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(end, start, "missing source boundary: " + End);
			return Source.Substring(start, end - start);
		}

		private static void AssertOrdered(string Source, params string[] Terms)
		{
			int previous = -1;
			for (int i = 0; i < Terms.Length; i++)
			{
				int found = Source.IndexOf(Terms[i], previous + 1, StringComparison.Ordinal);
				ClassicAssert.Greater(found, previous, "missing/out-of-order source term: " + Terms[i]);
				previous = found;
			}
		}
	}
}
#endif
