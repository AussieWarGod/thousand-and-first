#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomSocketTransitionRulesTests
	{
		[Test]
		public void LogicalAuthorityPreservesEnginePartAndNestedDeclarationOrder()
		{
			string source = KingdomSocketLogicalSource.Read();
			ClassicAssert.AreEqual(15, KingdomSocketLogicalSource.FileCount);
			AssertOrdered(source,
				"[Serializable]",
				"public class r_KingdomSocket : IPart",
				"public string LastDesignKey;",
				"public static partial class KingdomSocket",
				"internal static void RetryConstruction(",
				"internal static void InspectConstruction(",
				"private static KingdomPhysicalLookupState FindSocketResult(",
				"private static void ContinueSocketBuild(",
				"private static bool RemoveSocketPredecessor(",
				"private static bool HasBlockingReceipt(",
				"private struct ConvertContext",
				"private sealed class PreparedConvert",
				"private static bool Validate(",
				"public static bool AssessConvert(",
				"private static bool TryPrepareConvert(",
				"public static bool ExecuteConvert(",
				"private static bool ExecutePreparedConvert(",
				"private static bool ProjectConvertOrder(",
				"internal static bool ResumeStrikeSuccessor(",
				"private static bool HasStrikePlotParts(",
				"public static bool OnCleared(",
				"internal static bool TryReadSocketLot(",
				"internal static bool TryStampSocketLot(",
				"private static bool TrySweepLegacyPlotParts(",
				"private sealed class PreparedSocketBuild",
				"public static bool BuildOnSocket(",
				"private static bool ExecuteSocketBuild(",
				"public static bool Redress(",
				"public static void OpenConvert(",
				"public static void OpenRedress(");
			ClassicAssert.IsFalse(source.Contains("partial class r_KingdomSocket"));
			ClassicAssert.IsFalse(source.Contains("private static void LeaveSocket("));
		}

		[Test]
		public void TypedSocketUsesNamedSchemaLastPropertiesAndNoNewPartFields()
		{
			string source = KingdomSocketLogicalSource.Read();
			string part = Between(source, "public class r_KingdomSocket : IPart",
				"public static partial class KingdomSocket");
			StringAssert.Contains("public string LastDesignKey;", part);
			StringAssert.DoesNotContain("public string LotType;", part);
			StringAssert.DoesNotContain("public ArchitectureLotSize LotSize;", part);
			AssertOrdered(source,
				"SocketLotSchemaProperty = \"r_TAF_SocketLotSchema\"",
				"SocketLotTypeProperty = \"r_TAF_SocketLotType\"",
				"SocketLotSizeProperty = \"r_TAF_SocketLotSize\"",
				"SocketLotFacingProperty = \"r_TAF_SocketLotFacing\"");
			string stamp = Between(source, "internal static bool TryStampSocketLot(",
				"internal static bool SocketLotMatches(");
			AssertOrdered(stamp, "RemoveIntProperty(SocketLotSchemaProperty)",
				"SetStringProperty(SocketLotTypeProperty, Intent.LotType)",
				"SetIntProperty(SocketLotSizeProperty, (int)Intent.LotSize)",
				"SetIntProperty(SocketLotFacingProperty, (int)Intent.Facing)",
				"SetIntProperty(SocketLotSchemaProperty, SocketLotSchema)",
				"TryReadSocketLot(Marker");
		}

		[Test]
		public void StrikeFreezesTypedPoseAndOnlyLiveSuccessorStampsIt()
		{
			string materials = TestMain.ReadRepositoryText(
				"Growth/KingdomMaterials.08.StrikeOrdering.cs");
			AssertOrdered(materials, "KingdomArchitectureRuntime.TryRead(Building, out authored",
				"KingdomPlots.TryReadRect(Building",
				"intent.HasTypedLot = true", "intent.LotType = authored.LotType",
				"intent.LotSize = authored.LotSize", "intent.Facing = authored.Facing",
				"TryEncodeStrikeIntent(intent");
			string socket = KingdomSocketLogicalSource.Read();
			string successor = Between(socket, "internal static bool ResumeStrikeSuccessor(",
				"private static bool HasStrikePlotParts(");
			AssertOrdered(successor, "GameObject.Create(SocketBlueprint)",
				"part.LastDesignKey = Intent.BuildKey", "KingdomPlots.StampRect(marker, rect)",
				"TryStampSocketLot(marker, Intent", "KingdomConstruction.UpdateOutput(ref Job");
			ClassicAssert.AreEqual(1, Count(socket, "GameObject.Create(SocketBlueprint)"));
		}

		[Test]
		public void RebuildDisclosesAndPreservesExactTypeSizeFacingBeforeDebit()
		{
			string source = KingdomSocketLogicalSource.Read();
			string menu = Between(source, "public static void OpenConvert(",
				"public static void OpenRedress(");
			StringAssert.Contains("SocketLotLabel(sockets[i])", menu);
			AssertOrdered(menu, "TryReadSocketLot(target, out socketType",
				"TryClassifySetChange(socketType, socketSize",
				"candidate.Facing != socketFacing", "TryPrepareSocketBuild(System, zone, target");
			string prepare = Between(source, "private static bool TryPrepareSocketBuild(",
				"private static bool TrySocketBuildLabour(");
			AssertOrdered(prepare, "TryReadSocketLot(Marker, out string frozenType",
				"TryClassifySetChange(frozenType, frozenSize",
				"TryPreparePlotPayload(System, Z, rect, entry.Key, lotType",
				"SocketAcceptsArchitecture(Marker, architecture");
			string execute = Between(source, "private static bool ExecuteSocketBuild(",
				"public static bool Redress(");
			AssertOrdered(execute, "Marker.IDIfAssigned != Prepared.MarkerId",
				"Marker.CurrentCell != Z.GetCell(Prepared.Rect.CenterX",
				"TryDecodePlotPayload(payload, out var promisedRect",
				"promisedArchitecture.EncodedSnapshot != architecture.EncodedSnapshot",
				"SocketAcceptsArchitecture(Marker, promisedArchitecture",
				"ReserveExactWater(entry.CostDrams)");
		}

		[Test]
		public void ParseFreezesDirectionalTypedDelta()
		{
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryParse("shed-to-post", "toolshed",
				"chargingpost", " CRAFT ", "M", "renovate", "12", "scrap:2", "900",
				out KingdomSocketTransition transition, out string failure), failure);
			ClassicAssert.AreEqual("toolshed", transition.FromBuildKey);
			ClassicAssert.AreEqual("chargingpost", transition.ToBuildKey);
			ClassicAssert.AreEqual("craft", transition.LotType);
			ClassicAssert.AreEqual(ArchitectureLotSize.Medium, transition.LotSize);
			ClassicAssert.AreEqual(ArchitectureTransitionMode.Renovate, transition.Mode);
			ClassicAssert.AreEqual(12, transition.WaterDrams);
			ClassicAssert.AreEqual(900L, transition.WorkTicks);
			ClassicAssert.AreEqual(2, transition.Materials.Get(KingdomMaterial.Scrap));
		}

		[Test]
		public void TransitionModeVocabularyIsClosedAndReplacementIsExplicit()
		{
			string[] keys = { "none", "additive", "additive-expand", "renovate",
				"renovate-expand", "replacement" };
			ArchitectureTransitionMode[] modes =
			{
				ArchitectureTransitionMode.None, ArchitectureTransitionMode.Additive,
				ArchitectureTransitionMode.AdditiveExpand,
				ArchitectureTransitionMode.Renovate,
				ArchitectureTransitionMode.RenovateExpand,
				ArchitectureTransitionMode.Replacement
			};
			for (int i = 0; i < keys.Length; i++)
			{
				ClassicAssert.IsTrue(KingdomArchitectureTransitionRules.TryParseMode(keys[i],
					out ArchitectureTransitionMode parsed));
				ClassicAssert.AreEqual(modes[i], parsed);
				ClassicAssert.AreEqual(keys[i], KingdomArchitectureTransitionRules.ModeKey(parsed));
			}
			ClassicAssert.IsFalse(KingdomArchitectureTransitionRules.TryParseMode("expand", out _));
			ClassicAssert.IsFalse(KingdomArchitectureTransitionRules.ValidTierMode(0,
				ArchitectureTransitionMode.Renovate));
			ClassicAssert.IsTrue(KingdomArchitectureTransitionRules.ValidTierMode(0,
				ArchitectureTransitionMode.None));
			ClassicAssert.IsFalse(KingdomArchitectureTransitionRules.ValidTierMode(1,
				ArchitectureTransitionMode.None));

			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryParse("replace", "a", "b",
				"craft", "S", "replacement", "1", "scrap:1", "1",
				out KingdomSocketTransition replacement, out string failure), failure);
			ClassicAssert.AreEqual(ArchitectureTransitionMode.Replacement, replacement.Mode);
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.TryParse("none", "a", "b",
				"craft", "S", "none", "1", "scrap:1", "1", out _, out failure));
		}

		[Test]
		public void RouteIdentityIsDirectional()
		{
			string forward = KingdomSocketTransitionRules.IndexKey("a", "b", "craft",
				ArchitectureLotSize.Small);
			string reverse = KingdomSocketTransitionRules.IndexKey("b", "a", "craft",
				ArchitectureLotSize.Small);
			ClassicAssert.AreNotEqual(forward, reverse);
		}

		[Test]
		public void FixedLotAuthoritySeparatesOrdinaryPreflightAndDurableRetry()
		{
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.AuthorizesFixedLotTransition(
				SamePlan: true, SameBinding: true, SameType: true, SameSize: true,
				SameRect: true, SameFacing: true, SameMainRoot: true, ExactLotIdentity: true,
				AllowPlanChange: false, DurableRouteAuthority: false),
				"ordinary upgrades stay inside their frozen plan and binding");
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.AuthorizesFixedLotTransition(
				SamePlan: false, SameBinding: false, SameType: true, SameSize: true,
				SameRect: true, SameFacing: true, SameMainRoot: true, ExactLotIdentity: true,
				AllowPlanChange: false, DurableRouteAuthority: false));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.AuthorizesFixedLotTransition(
				SamePlan: true, SameBinding: false, SameType: true, SameSize: true,
				SameRect: true, SameFacing: true, SameMainRoot: true, ExactLotIdentity: true,
				AllowPlanChange: false, DurableRouteAuthority: false));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.AuthorizesFixedLotTransition(
				SamePlan: false, SameBinding: true, SameType: true, SameSize: true,
				SameRect: true, SameFacing: true, SameMainRoot: true, ExactLotIdentity: true,
				AllowPlanChange: false, DurableRouteAuthority: false));
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.AuthorizesFixedLotTransition(
				SamePlan: false, SameBinding: false, SameType: true, SameSize: true,
				SameRect: true, SameFacing: true, SameMainRoot: true, ExactLotIdentity: true,
				AllowPlanChange: true, DurableRouteAuthority: false),
				"only the declaration-owning preflight may use transient authority");
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.AuthorizesFixedLotTransition(
				SamePlan: false, SameBinding: false, SameType: true, SameSize: true,
				SameRect: true, SameFacing: true, SameMainRoot: true, ExactLotIdentity: true,
				AllowPlanChange: false, DurableRouteAuthority: true),
				"paid retry must rebind the exact durable route receipt");
		}

		[Test]
		public void FixedLotAuthorityRefusesEveryPhysicalIdentityDrift()
		{
			string[] names = { "type", "size", "rectangle", "facing", "main root", "lot id" };
			for (int changed = 0; changed < names.Length; changed++)
			{
				bool[] exact = { true, true, true, true, true, true };
				exact[changed] = false;
				ClassicAssert.IsFalse(KingdomSocketTransitionRules.AuthorizesFixedLotTransition(
					SamePlan: false, SameBinding: false, SameType: exact[0],
					SameSize: exact[1], SameRect: exact[2], SameFacing: exact[3],
					SameMainRoot: exact[4], ExactLotIdentity: exact[5],
					AllowPlanChange: true, DurableRouteAuthority: true), names[changed]);
			}
		}

		[Test]
		public void RouteMatchRefusesWrongDeclarationTypeOrSize()
		{
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryParse("tent-to-hut-s", "tent",
				"hut", "housing", "S", "renovate", "4", "timber:4,mud:2", "1350",
				out KingdomSocketTransition route, out string failure), failure);
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.MatchesRoute(route, "tent", "hut",
				"housing", ArchitectureLotSize.Small));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.MatchesRoute(route, "tentrow", "hut",
				"housing", ArchitectureLotSize.Small));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.MatchesRoute(route, "tent", "mudhut",
				"housing", ArchitectureLotSize.Small));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.MatchesRoute(route, "tent", "hut",
				"craft", ArchitectureLotSize.Small));
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.MatchesRoute(route, "tent", "hut",
				"housing", ArchitectureLotSize.Medium));
		}

		[Test]
		public void ExactDeclarationAuthorityCoversEveryFieldAndDeepSnapshotsDetach()
		{
			Type declarationType = typeof(KingdomSocketTransition);
			ClassicAssert.AreEqual(0, declarationType.GetFields(System.Reflection.BindingFlags.Public
				| System.Reflection.BindingFlags.Instance).Length);
			string[] immutable = { "Key", "FromBuildKey", "ToBuildKey", "LotType", "LotSize", "Mode",
				"WaterDrams", "Materials", "WorkTicks" };
			for (int i = 0; i < immutable.Length; i++)
			{
				System.Reflection.PropertyInfo property = declarationType.GetProperty(immutable[i]);
				ClassicAssert.IsNotNull(property, immutable[i]);
				ClassicAssert.IsNull(property.GetSetMethod(), immutable[i] + " exposes a public setter");
			}
			KingdomSocketTransition original = ParsedRoute();
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryDeclarationDigest(original,
				out string originalDigest));
			string[] names = { "key", "from", "to", "type", "size", "mode", "water", "materials", "ticks" };
			KingdomSocketTransition[] changed =
			{
				ParsedRoute(Key: "other-key"),
				ParsedRoute(From: "tentrow"),
				ParsedRoute(To: "mudhut"),
				ParsedRoute(Type: "craft"),
				ParsedRoute(Size: "M"),
				ParsedRoute(Mode: "additive"),
				ParsedRoute(Water: "5"),
				ParsedRoute(Materials: "timber:99,mud:2"),
				ParsedRoute(Ticks: "1351")
			};
			for (int i = 0; i < changed.Length; i++)
			{
				ClassicAssert.IsFalse(KingdomSocketTransitionRules.MatchesRoute(original, changed[i]),
					names[i]);
				ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryDeclarationDigest(changed[i],
					out string changedDigest), names[i]);
				ClassicAssert.AreNotEqual(originalDigest, changedDigest, names[i]);
			}
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TrySnapshot(original,
				out KingdomSocketTransition snapshot));
			KingdomMaterialTally exposed = snapshot.Materials;
			exposed.Set(KingdomMaterial.Timber, 99);
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.MatchesRoute(original, snapshot));
			ClassicAssert.AreEqual(4, original.Materials.Get(KingdomMaterial.Timber),
				"mutating a detached preview must not alter registry authority");
			ClassicAssert.AreEqual(4, snapshot.Materials.Get(KingdomMaterial.Timber),
				"a declaration never exposes its internal material snapshot");
		}

		[Test]
		public void ReceiptRefusesEverySchemaLastPublicationCut()
		{
			KingdomSocketTransitionReceiptShape receipt = ReceiptValuesOnly();
			ClassicAssert.IsFalse(ReceiptAuthorizes(receipt, out _), "cut 0: schema invalidated");
			for (int cut = 0; cut < 5; cut++)
			{
				SetPublishedString(ref receipt, cut);
				ClassicAssert.IsFalse(ReceiptAuthorizes(receipt, out _), "cut " + (cut + 1));
			}
			receipt.SchemaHasInt = true;
			receipt.Schema = KingdomSocketTransitionRules.ReceiptSchema;
			ClassicAssert.IsTrue(ReceiptAuthorizes(receipt, out bool legacy));
			ClassicAssert.IsFalse(legacy);
		}

		[Test]
		public void ReceiptRefusesEveryMissingDualOrWrongPropertyType()
		{
			for (int fault = 0; fault < 18; fault++)
			{
				KingdomSocketTransitionReceiptShape receipt = CurrentReceipt();
				ApplyShapeFault(ref receipt, fault);
				ClassicAssert.IsFalse(ReceiptAuthorizes(receipt, out _), "shape fault " + fault);
			}
			KingdomSocketTransitionReceiptShape unknown = CurrentReceipt();
			unknown.Schema = 99;
			ClassicAssert.IsFalse(ReceiptAuthorizes(unknown, out _), "unknown schema");
		}

		[Test]
		public void ReceiptRefusesEveryForgedBoundValue()
		{
			for (int field = 0; field < 5; field++)
			{
				KingdomSocketTransitionReceiptShape receipt = CurrentReceipt();
				ApplyValueForgery(ref receipt, field);
				ClassicAssert.IsFalse(ReceiptAuthorizes(receipt, out _), "forged field " + field);
			}
		}

		[Test]
		public void ExactLegacyReceiptIsAdoptableButNoHybridShapeIs()
		{
			KingdomSocketTransitionReceiptShape legacyReceipt = CurrentReceipt();
			legacyReceipt.Schema = KingdomSocketTransitionRules.LegacyReceiptSchema;
			legacyReceipt.DeclarationHasString = false;
			legacyReceipt.DeclarationDigest = null;
			ClassicAssert.IsTrue(ReceiptAuthorizes(legacyReceipt, out bool legacy));
			ClassicAssert.IsTrue(legacy);

			legacyReceipt.DeclarationHasString = true;
			legacyReceipt.DeclarationDigest = ExpectedDeclarationDigest;
			ClassicAssert.IsFalse(ReceiptAuthorizes(legacyReceipt, out _),
				"schema 1 may not expose a schema 2 declaration field");
		}

		[TestCase("a", "a", "craft", "S", "1", "scrap:1", "1")]
		[TestCase("a", "b", "craft", "bogus", "1", "scrap:1", "1")]
		[TestCase("a", "b", "craft", "S", "-1", "scrap:1", "1")]
		[TestCase("a", "b", "craft", "S", "1", "scrap:1", "0")]
		[TestCase("a", "b", "craft", "S", "1", "unknown:1", "1")]
		public void MalformedOrSelfRouteRefuses(string from, string to, string type,
			string size, string water, string materials, string ticks)
		{
			ClassicAssert.IsFalse(KingdomSocketTransitionRules.TryParse("route", from, to, type,
				size, "renovate", water, materials, ticks, out _, out string failure));
			ClassicAssert.IsFalse(string.IsNullOrEmpty(failure));
		}

		[Test]
		public void UndeclaredRefusalNamesBothEndpointsAndRemedy()
		{
			string refusal = KingdomSocketTransitionRules.RefuseUndeclared("tool shed",
				"charging post");
			StringAssert.Contains("tool shed", refusal);
			StringAssert.Contains("charging post", refusal);
			StringAssert.Contains("explicit transition", refusal);
		}

		[Test]
		public void PlanQuoteUsesOnlyDeclaredDeltaAndNoStrike()
		{
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryParse("quote", "a", "b", "craft",
				"S", "renovate", "7", "scrap:3", "450", out KingdomSocketTransition transition,
				out string failure), failure);
			KingdomSocketRules.ConversionQuote quote = KingdomSocketRules.AssessPlanChange(
				transition);
			ClassicAssert.AreEqual(0, quote.StrikeEffort);
			ClassicAssert.AreEqual(0, quote.Salvage.Total());
			ClassicAssert.AreEqual(7, quote.NewDrams);
			ClassicAssert.AreEqual(3, quote.NetMaterials.Get(KingdomMaterial.Scrap));
			ClassicAssert.AreEqual(450L, quote.WorkTicks);
		}

		[Test]
		public void ShippedEarlyHousingRoutesCoverEveryExactSizeAndStayCheaper()
		{
			XmlDocument buildings = new XmlDocument();
			buildings.LoadXml(TestMain.ReadRepositoryText("KingdomBuildings.xml"));
			Dictionary<string, XmlElement> byKey = new Dictionary<string, XmlElement>(
				StringComparer.Ordinal);
			foreach (XmlElement building in buildings.SelectNodes("//building"))
				byKey[building.GetAttribute("Key")] = building;

			XmlDocument routes = new XmlDocument();
			routes.Load(Path.Combine(TestMain.RepositoryRoot, "Architecture",
				"KingdomArchitectureTransitions.xml"));
			XmlNodeList declared = routes.SelectNodes("/KingdomArchitectureTransitions/transition");
			ClassicAssert.AreEqual(24, declared.Count);
			HashSet<string> mappings = new HashSet<string>(StringComparer.Ordinal);
			foreach (string file in Directory.GetFiles(Path.Combine(TestMain.RepositoryRoot,
				"Architecture"), "KingdomArchitectures*.xml"))
			{
				XmlDocument architecture = new XmlDocument();
				architecture.Load(file);
				foreach (XmlElement binding in architecture.SelectNodes(
					"/KingdomArchitectures/plan/binding"))
					foreach (XmlElement tier in binding.SelectNodes("tier"))
						mappings.Add(tier.GetAttribute("BuildKey") + ":"
							+ binding.GetAttribute("Type") + ":" + binding.GetAttribute("Size"));
			}
			string[] sizes = { "S", "M", "L", "XL" };
			string[,] pairs =
			{
				{ "tent", "hut" }, { "tentrow", "hutyard" },
				{ "tent", "mudhut" }, { "tentrow", "mudhutcourt" },
				{ "tent", "blockhut" }, { "tentrow", "blockyard" }
			};
			HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
			foreach (XmlElement route in declared)
			{
				string from = route.GetAttribute("From");
				string to = route.GetAttribute("To");
				string identity = from + ">" + to + ":" + route.GetAttribute("Size");
				ClassicAssert.IsTrue(identities.Add(identity), identity);
				ClassicAssert.IsFalse(identities.Contains(to + ">" + from + ":"
					+ route.GetAttribute("Size")), "undeclared reverse became present: " + identity);
				XmlElement target = byKey[to];
				string mappingSuffix = ":" + route.GetAttribute("Type") + ":"
					+ route.GetAttribute("Size");
				ClassicAssert.IsTrue(mappings.Contains(from + mappingSuffix), identity + " source mapping");
				ClassicAssert.IsTrue(mappings.Contains(to + mappingSuffix), identity + " target mapping");
				ClassicAssert.Less(int.Parse(route.GetAttribute("Water")),
					int.Parse(target.GetAttribute("Cost")), identity + " water");
				ClassicAssert.Less(MaterialTotal(route.GetAttribute("Materials")),
					MaterialTotal(target.GetAttribute("Materials")), identity + " materials");
				ClassicAssert.Less(long.Parse(route.GetAttribute("Ticks")),
					long.Parse(target.GetAttribute("Ticks")), identity + " labour");
			}
			for (int pair = 0; pair < pairs.GetLength(0); pair++)
				for (int size = 0; size < sizes.Length; size++)
					ClassicAssert.IsTrue(identities.Contains(pairs[pair, 0] + ">" + pairs[pair, 1]
						+ ":" + sizes[size]), "missing exact route");
			ClassicAssert.AreEqual("all,!common,!eater", byKey["hut"].GetAttribute("Styles"));
			ClassicAssert.AreEqual("common", byKey["mudhut"].GetAttribute("Styles"));
			ClassicAssert.AreEqual("eater", byKey["blockhut"].GetAttribute("Styles"));
			AssertEveryDeclaredTargetVariantRetainsSourceStatefulFixtures(declared);
		}

		[Test]
		public void AllTwentyFourDeclaredRoutesExerciseCrossPlanAuthorizationLaw()
		{
			ArchitectureCorpus corpus = KingdomArchitectureCorpusFixture.Load();
			XmlDocument routes = new XmlDocument();
			routes.Load(Path.Combine(TestMain.RepositoryRoot, "Architecture",
				"KingdomArchitectureTransitions.xml"));
			XmlNodeList declared = routes.SelectNodes(
				"/KingdomArchitectureTransitions/transition");
			ClassicAssert.AreEqual(24, declared.Count);
			int exercised = 0;
			foreach (XmlElement route in declared)
			{
				ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryParse(route.GetAttribute("Key"),
					route.GetAttribute("From"), route.GetAttribute("To"),
					route.GetAttribute("Type"), route.GetAttribute("Size"),
					route.GetAttribute("Mode"),
					route.GetAttribute("Water"), route.GetAttribute("Materials"),
					route.GetAttribute("Ticks"), out KingdomSocketTransition parsed,
					out string failure), failure);
				List<ArchitectureCorpusCase> sources = CorpusCases(corpus, parsed.FromBuildKey,
					parsed.LotType, parsed.LotSize);
				List<ArchitectureCorpusCase> targets = CorpusCases(corpus, parsed.ToBuildKey,
					parsed.LotType, parsed.LotSize);
				ClassicAssert.IsNotEmpty(sources, parsed.Key + " source");
				ClassicAssert.IsNotEmpty(targets, parsed.Key + " target");
				for (int i = 0; i < sources.Count; i++)
					for (int j = 0; j < targets.Count; j++)
					{
						ClassicAssert.AreNotEqual(sources[i].PlanKey, targets[j].PlanKey,
							parsed.Key + " must exercise the cross-plan path");
						ClassicAssert.IsFalse(KingdomSocketTransitionRules.AuthorizesFixedLotTransition(
							SamePlan: false,
							SameBinding: sources[i].Binding.Key == targets[j].Binding.Key,
							SameType: true, SameSize: true, SameRect: true, SameFacing: true,
							SameMainRoot: true, ExactLotIdentity: true,
							AllowPlanChange: false, DurableRouteAuthority: false), parsed.Key);
						ClassicAssert.IsTrue(KingdomSocketTransitionRules.AuthorizesFixedLotTransition(
							SamePlan: false,
							SameBinding: sources[i].Binding.Key == targets[j].Binding.Key,
							SameType: true, SameSize: true, SameRect: true, SameFacing: true,
							SameMainRoot: true, ExactLotIdentity: true,
							AllowPlanChange: true, DurableRouteAuthority: false), parsed.Key);
					}
				exercised++;
			}
			ClassicAssert.AreEqual(24, exercised);
		}

		[Test]
		public void TransitionUiPreparesOneSnapshotBeforeConfirmationAndDebit()
		{
			string socket = KingdomSocketLogicalSource.Read();
			StringAssert.Contains("TryPrepareConvert(System, zone, target, chosen.Key, skinKey", socket);
			StringAssert.Contains("KingdomArchitecturePreview.TryRenderTransition(conversion.Architecture", socket);
			StringAssert.Contains("Popup.PickOption(Title: \"Preview exact change:", socket);
			StringAssert.Contains("ExecutePreparedConvert(System, zone, target, conversion", socket);
			StringAssert.Contains("[change: \" + transition.WaterDrams", socket);
			StringAssert.Contains("!KingdomSocketTransitions.TryGet(currentKey, entry.Key", socket);
			ClassicAssert.Less(socket.IndexOf("TryPrepareConvert(System, zone, target, chosen.Key, skinKey",
				StringComparison.Ordinal), socket.IndexOf("Popup.PickOption(Title: \"Preview exact change:",
				StringComparison.Ordinal));
			ClassicAssert.Less(socket.IndexOf("Popup.PickOption(Title: \"Preview exact change:",
				StringComparison.Ordinal), socket.IndexOf("ExecutePreparedConvert(System, zone, target, conversion",
				StringComparison.Ordinal));
		}

		[Test]
		public void DurableReceiptInvalidatesOldCommitAndPublishesExactSchemaLastShape()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomSocketTransitions.cs");
			StringAssert.Contains("ReceiptSchemaProperty = \"r_TAF_SocketTransitionSchema\"", source);
			StringAssert.Contains("ReceiptKeyProperty = \"r_TAF_SocketTransitionKey\"", source);
			StringAssert.Contains("ReceiptDeclarationProperty = \"r_TAF_SocketTransitionDeclaration\"",
				source);
			StringAssert.Contains("ReceiptBeforeHashProperty = \"r_TAF_SocketTransitionBefore\"", source);
			StringAssert.Contains("ReceiptAfterHashProperty = \"r_TAF_SocketTransitionAfter\"", source);
			StringAssert.Contains("ReceiptJobProperty = \"r_TAF_SocketTransitionJob\"", source);
			string bind = Between(source, "internal static bool BindReceipt",
				"internal static bool Authorizes");
			AssertOrdered(bind,
				"Owner.RemoveIntProperty(ReceiptSchemaProperty)",
				"Owner.RemoveStringProperty(ReceiptSchemaProperty)",
				"RemoveIntPayloadTypes(Owner)",
				"Owner.SetStringProperty(ReceiptKeyProperty",
				"Owner.SetStringProperty(ReceiptDeclarationProperty",
				"Owner.SetStringProperty(ReceiptBeforeHashProperty",
				"Owner.SetStringProperty(ReceiptAfterHashProperty",
				"Owner.SetStringProperty(ReceiptJobProperty",
				"Owner.SetIntProperty(ReceiptSchemaProperty");
			StringAssert.Contains("KingdomSocketTransitionRules.ReceiptAuthorizes", bind);
			StringAssert.Contains("KingdomSocketTransitionRules.ReceiptSchema", bind);
			StringAssert.Contains("TryAdoptLegacyReceipt", source);
			AssertOrdered(Between(source, "private static bool TryAdoptLegacyReceipt(",
				"private static KingdomSocketTransitionReceiptShape ReadReceiptShape("),
				"Owner.RemoveIntProperty(ReceiptSchemaProperty)",
				"Owner.RemoveStringProperty(ReceiptSchemaProperty)",
				"Owner.SetStringProperty(ReceiptDeclarationProperty",
				"Owner.SetIntProperty(ReceiptSchemaProperty");
			StringAssert.Contains("internal static bool ClearReceipt(GameObject Owner, "
				+ "KingdomConstructionJob Job", source);
			StringAssert.DoesNotContain("internal static void ClearReceipt(GameObject Owner)", source);
			string clear = Between(source, "internal static bool ClearReceipt(",
				"private static bool TryAdoptLegacyReceipt(");
			AssertOrdered(clear, "TryResolveCurrent(Transition",
				"KingdomSocketTransitionRules.ReceiptAuthorizes",
				"TryInvalidateReceipt(Owner)", "RemovePayload(Owner)");
		}

		[Test]
		public void RegistryPreviewCommitAndRetryAllReResolveCurrentExactDeclaration()
		{
			string registry = TestMain.ReadRepositoryText("Growth/KingdomSocketTransitions.cs");
			StringAssert.Contains("KingdomSocketTransitionRules.TrySnapshot(registered", registry);
			string resolve = Between(registry, "internal static bool TryResolveCurrent(",
				"private static void EnsureLoaded(");
			StringAssert.Contains("KingdomSocketTransitionRules.MatchesRoute(Supplied, declared)",
				resolve);

			string plan = TestMain.ReadRepositoryText("Growth/KingdomUpgrade.16.PlanChange.cs");
			AssertOrdered(Between(plan, "public static bool TryPreparePlanChange(",
				"public static bool BeginPreparedPlanChange("),
				"KingdomArchitectureRuntime.TryRead(Work",
				"KingdomSocketTransitions.TryResolveCurrent(Transition",
				"survey.StoredWater < declared.WaterDrams",
				"Transition = declared");
			AssertOrdered(Between(plan, "public static bool BeginPreparedPlanChange(",
				"private static bool FounderMarksWouldFit("),
				"TryCurrentTransition(standing, Assessment",
				"KingdomMaterials.CanPayTransition(Z, declared.Materials",
				"current.Transition = declared",
				"BeginPrepared(System, Z, Work, current");

			string prepare = TestMain.ReadRepositoryText("Growth/KingdomUpgrade.15.Prepare.cs");
			StringAssert.Contains("private static bool TryCurrentTransition(", prepare);
			StringAssert.Contains("KingdomSocketTransitions.TryResolveCurrent(A.Transition", prepare);
			StringAssert.Contains("The same-set declaration is forged, stale, or changed since preview.",
				prepare);
			string preflight = TestMain.ReadRepositoryText(
				"Growth/KingdomArchitectureStamper.UpgradePreflight.cs");
			StringAssert.Contains("KingdomSocketTransitions.TryResolveCurrent(Transition", preflight);
			StringAssert.Contains("ExactTransitionClaim(PaidClaim, declared.Materials)", preflight);

			string begin = TestMain.ReadRepositoryText("Growth/KingdomUpgrade.14.Begin.cs");
			AssertOrdered(begin, "TryCurrentTransition(transitionBefore, A, out transition",
				"Survey.ReserveExactWater(A.CostDrams)",
				"KingdomSocketTransitions.BindReceipt(Work, job",
				"KingdomSocketTransitions.ClearReceipt(Work, job");
		}

		private const string ReceiptKey = "tent-to-hut-s";
		private const string ReceiptJob = "job-1";
		private static readonly string ExpectedDeclarationDigest = new string('b', 64);
		private static readonly string ExpectedBeforeHash = new string('c', 64);
		private static readonly string ExpectedAfterHash = new string('d', 64);

		private static KingdomSocketTransition ParsedRoute(string Key = ReceiptKey,
			string From = "tent", string To = "hut", string Type = "housing",
			string Size = "S", string Mode = "renovate", string Water = "4", string Materials = "timber:4,mud:2",
			string Ticks = "1350")
		{
			ClassicAssert.IsTrue(KingdomSocketTransitionRules.TryParse(Key, From, To, Type, Size,
				Mode, Water, Materials, Ticks,
				out KingdomSocketTransition route, out string failure), failure);
			return route;
		}

		private static KingdomSocketTransitionReceiptShape ReceiptValuesOnly()
		{
			return new KingdomSocketTransitionReceiptShape
			{
				Key = ReceiptKey,
				DeclarationDigest = ExpectedDeclarationDigest,
				BeforeHash = ExpectedBeforeHash,
				AfterHash = ExpectedAfterHash,
				JobId = ReceiptJob
			};
		}

		private static KingdomSocketTransitionReceiptShape CurrentReceipt()
		{
			KingdomSocketTransitionReceiptShape receipt = ReceiptValuesOnly();
			for (int i = 0; i < 5; i++) SetPublishedString(ref receipt, i);
			receipt.SchemaHasInt = true;
			receipt.Schema = KingdomSocketTransitionRules.ReceiptSchema;
			return receipt;
		}

		private static bool ReceiptAuthorizes(KingdomSocketTransitionReceiptShape Receipt,
			out bool Legacy)
		{
			return KingdomSocketTransitionRules.ReceiptAuthorizes(Receipt, ReceiptKey,
				ExpectedDeclarationDigest, ExpectedBeforeHash, ExpectedAfterHash,
				ReceiptJob, out Legacy);
		}

		private static void SetPublishedString(ref KingdomSocketTransitionReceiptShape Receipt,
			int Field)
		{
			switch (Field)
			{
			case 0: Receipt.KeyHasString = true; break;
			case 1: Receipt.DeclarationHasString = true; break;
			case 2: Receipt.BeforeHasString = true; break;
			case 3: Receipt.AfterHasString = true; break;
			case 4: Receipt.JobHasString = true; break;
			default: Assert.Fail("unknown receipt field"); break;
			}
		}

		private static void ApplyShapeFault(ref KingdomSocketTransitionReceiptShape Receipt,
			int Fault)
		{
			switch (Fault)
			{
			case 0: Receipt.SchemaHasInt = false; break;
			case 1: Receipt.SchemaHasString = true; break;
			case 2: Receipt.SchemaHasInt = false; Receipt.SchemaHasString = true; break;
			case 3: Receipt.KeyHasString = false; break;
			case 4: Receipt.KeyHasInt = true; break;
			case 5: Receipt.KeyHasString = false; Receipt.KeyHasInt = true; break;
			case 6: Receipt.DeclarationHasString = false; break;
			case 7: Receipt.DeclarationHasInt = true; break;
			case 8:
				Receipt.DeclarationHasString = false; Receipt.DeclarationHasInt = true; break;
			case 9: Receipt.BeforeHasString = false; break;
			case 10: Receipt.BeforeHasInt = true; break;
			case 11: Receipt.BeforeHasString = false; Receipt.BeforeHasInt = true; break;
			case 12: Receipt.AfterHasString = false; break;
			case 13: Receipt.AfterHasInt = true; break;
			case 14: Receipt.AfterHasString = false; Receipt.AfterHasInt = true; break;
			case 15: Receipt.JobHasString = false; break;
			case 16: Receipt.JobHasInt = true; break;
			case 17: Receipt.JobHasString = false; Receipt.JobHasInt = true; break;
			default: Assert.Fail("unknown shape fault"); break;
			}
		}

		private static void ApplyValueForgery(ref KingdomSocketTransitionReceiptShape Receipt,
			int Field)
		{
			switch (Field)
			{
			case 0: Receipt.Key = "forged-key"; break;
			case 1: Receipt.DeclarationDigest = new string('e', 64); break;
			case 2: Receipt.BeforeHash = new string('e', 64); break;
			case 3: Receipt.AfterHash = new string('e', 64); break;
			case 4: Receipt.JobId = "forged-job"; break;
			default: Assert.Fail("unknown value field"); break;
			}
		}

		private static int MaterialTotal(string Text)
		{
			int total = 0;
			foreach (string term in Text.Split(','))
				total += int.Parse(term.Substring(term.IndexOf(':') + 1));
			return total;
		}

		private static List<ArchitectureCorpusCase> CorpusCases(ArchitectureCorpus Corpus,
			string BuildKey, string LotType, ArchitectureLotSize LotSize)
		{
			List<ArchitectureCorpusCase> result = new List<ArchitectureCorpusCase>();
			for (int i = 0; i < Corpus.Cases.Count; i++)
			{
				ArchitectureCorpusCase item = Corpus.Cases[i];
				if (item.Tier.BuildKey == BuildKey && item.Binding.TypeKey == LotType &&
					item.Binding.Size == LotSize) result.Add(item);
			}
			return result;
		}

		private sealed class LayoutFixture
		{
			public string Name;
			public string Map;
			public string Palette;
		}

		private static void AssertEveryDeclaredTargetVariantRetainsSourceStatefulFixtures(
			XmlNodeList Declared)
		{
			Dictionary<string, XmlElement> maps = new Dictionary<string, XmlElement>(
				StringComparer.Ordinal);
			Dictionary<string, XmlElement> palettes = new Dictionary<string, XmlElement>(
				StringComparer.Ordinal);
			Dictionary<string, List<LayoutFixture>> layouts =
				new Dictionary<string, List<LayoutFixture>>(StringComparer.Ordinal);
			foreach (string file in Directory.GetFiles(Path.Combine(TestMain.RepositoryRoot,
				"Architecture"), "KingdomArchitectures*.xml"))
			{
				XmlDocument architecture = new XmlDocument();
				architecture.Load(file);
				foreach (XmlElement map in architecture.SelectNodes(
					"/KingdomArchitectures/map")) maps[map.GetAttribute("Key")] = map;
				foreach (XmlElement palette in architecture.SelectNodes(
					"/KingdomArchitectures/palette"))
					palettes[palette.GetAttribute("Key")] = palette;
				foreach (XmlElement binding in architecture.SelectNodes(
					"/KingdomArchitectures/plan/binding"))
				{
					foreach (XmlElement tier in binding.SelectNodes("tier"))
					{
						string mapping = tier.GetAttribute("BuildKey") + ":"
							+ binding.GetAttribute("Type") + ":" + binding.GetAttribute("Size");
						if (!layouts.TryGetValue(mapping, out List<LayoutFixture> choices))
						{
							choices = new List<LayoutFixture>();
							layouts.Add(mapping, choices);
						}
						foreach (XmlElement variant in tier.SelectNodes("variant"))
						{
							choices.Add(new LayoutFixture
							{
								Name = variant.GetAttribute("Key"),
								Map = string.IsNullOrEmpty(variant.GetAttribute("Map"))
									? tier.GetAttribute("Map") : variant.GetAttribute("Map"),
								Palette = string.IsNullOrEmpty(variant.GetAttribute("Palette"))
									? tier.GetAttribute("Palette") : variant.GetAttribute("Palette")
							});
						}
					}
				}
			}
			foreach (XmlElement route in Declared)
			{
				string suffix = ":" + route.GetAttribute("Type") + ":"
					+ route.GetAttribute("Size");
				string sourceKey = route.GetAttribute("From") + suffix;
				string targetKey = route.GetAttribute("To") + suffix;
				ClassicAssert.IsTrue(layouts.TryGetValue(sourceKey, out List<LayoutFixture> sources),
					sourceKey);
				ClassicAssert.IsTrue(layouts.TryGetValue(targetKey, out List<LayoutFixture> targets),
					targetKey);
				foreach (LayoutFixture source in sources)
				{
					HashSet<string> retained = StatefulFixtureSignatures(source, maps, palettes);
					string sourceMain = MainCoordinate(source, maps);
					foreach (LayoutFixture target in targets)
					{
						string context = route.GetAttribute("Key") + " target variant " + target.Name;
						CollectionAssert.IsSubsetOf(retained,
							StatefulFixtureSignatures(target, maps, palettes), context);
						ClassicAssert.AreEqual(sourceMain, MainCoordinate(target, maps), context + " main");
					}
				}
			}
		}

		private static HashSet<string> StatefulFixtureSignatures(LayoutFixture Layout,
			Dictionary<string, XmlElement> Maps, Dictionary<string, XmlElement> Palettes)
		{
			XmlElement map = Maps[Layout.Map];
			XmlElement palette = Palettes[Layout.Palette];
			Dictionary<char, XmlElement> glyphs = new Dictionary<char, XmlElement>();
			foreach (XmlElement glyph in map.SelectNodes("glyph"))
				glyphs[glyph.GetAttribute("Char")[0]] = glyph;
			HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
			int y = 0;
			foreach (XmlElement row in map.SelectNodes("row"))
			{
				string cells = row.GetAttribute("Cells");
				for (int x = 0; x < cells.Length; x++)
				{
					if (!glyphs.TryGetValue(cells[x], out XmlElement glyph)
						|| glyph.GetAttribute("Stateful") != "yes"
						|| glyph.GetAttribute("Object") == "$building") continue;
					string slotKey = glyph.GetAttribute("Object").Substring(1);
					XmlElement slot = (XmlElement)palette.SelectSingleNode(
						"slot[@Key='" + slotKey + "']");
					string anchor = glyph.GetAttribute("Anchors");
					result.Add(anchor + "|" + x + "|" + y + "|"
						+ slot.GetAttribute("Blueprint") + "|" + slot.GetAttribute("Material")
						+ "|" + slot.GetAttribute("MinTech") + "|" + slot.GetAttribute("Knowledge")
						+ "|" + slot.GetAttribute("Power") + "|" + slot.GetAttribute("Natural"));
				}
				y++;
			}
			return result;
		}

		private static string MainCoordinate(LayoutFixture Layout,
			Dictionary<string, XmlElement> Maps)
		{
			XmlElement map = Maps[Layout.Map];
			char mainGlyph = '\0';
			foreach (XmlElement glyph in map.SelectNodes("glyph"))
				if (glyph.GetAttribute("Object") == "$building")
					mainGlyph = glyph.GetAttribute("Char")[0];
			int y = 0;
			foreach (XmlElement row in map.SelectNodes("row"))
			{
				int x = row.GetAttribute("Cells").IndexOf(mainGlyph);
				if (x >= 0) return x + "|" + y;
				y++;
			}
			return null;
		}

		private static void AssertOrdered(string Source, params string[] Needles)
		{
			int previous = -1;
			for (int i = 0; i < Needles.Length; i++)
			{
				int next = Source.IndexOf(Needles[i], previous + 1, StringComparison.Ordinal);
				ClassicAssert.Greater(next, previous, "missing or out of order: " + Needles[i]);
				previous = next;
			}
		}

		private static string Between(string Source, string Start, string End)
		{
			int first = Source.IndexOf(Start, StringComparison.Ordinal);
			ClassicAssert.GreaterOrEqual(first, 0, "missing source boundary: " + Start);
			int last = Source.IndexOf(End, first + Start.Length, StringComparison.Ordinal);
			ClassicAssert.Greater(last, first, "missing source boundary: " + End);
			return Source.Substring(first, last - first);
		}

		private static int Count(string Source, string Term)
		{
			int count = 0;
			for (int offset = 0; (offset = Source.IndexOf(Term, offset,
				StringComparison.Ordinal)) >= 0; offset += Term.Length) count++;
			return count;
		}
	}
}
#endif
