#if TAF_TESTS
using System;
using System.IO;
using System.Xml;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomHostedArcologySourceTests
	{
		private static string Read(params string[] Parts)
		{
			return TestMain.ReadRepositoryText(Path.Combine(Parts));
		}

		[Test]
		public void CatalogueMakesOneFinalHeartShellAndPrivateHostedLots()
		{
			string catalogue = Read("RuntimeData", "KingdomBuildings.xml");
			XmlDocument doc = new XmlDocument(); doc.LoadXml(catalogue);
			XmlElement court = (XmlElement)doc.SelectSingleNode("//building[@Key='heartcourt']");
			XmlElement shell = (XmlElement)doc.SelectSingleNode("//building[@Key='arcology']");
			XmlElement ward = (XmlElement)doc.SelectSingleNode("//building[@Key='arcologyward']");
			XmlElement terrace = (XmlElement)doc.SelectSingleNode("//building[@Key='arcologyterrace']");
			Assert.AreEqual("arcology", court.GetAttribute("UpgradesTo"));
			Assert.AreEqual("yes", shell.GetAttribute("Capital"));
			Assert.AreEqual("yes", shell.GetAttribute("Megastructure"));
			Assert.AreEqual("arcology", ward.GetAttribute("Strata"));
			Assert.AreEqual("arcology", terrace.GetAttribute("Strata"));
			Assert.AreEqual("food:14", terrace.GetAttribute("Carries"));
			StringAssert.DoesNotContain("HostedProducer", catalogue);
			StringAssert.DoesNotContain("surface", ward.GetAttribute("Strata"));
			StringAssert.DoesNotContain("surface", terrace.GetAttribute("Strata"));
		}

		[Test]
		public void RootOwnsOneNativeSchemaAndNoNestedInteriors()
		{
			XmlDocument doc = new XmlDocument(); doc.LoadXml(Read("RuntimeData", "ObjectBlueprints.xml"));
			XmlNode root = doc.SelectSingleNode("//object[@Name='r_KingdomArcology']");
			Assert.IsNotNull(root.SelectSingleNode("part[@Name='r_KingdomArcology']"));
			Assert.IsNotNull(root.SelectSingleNode(
				"part[@Name='Interior'][@Cell='TAFArcology'][@X='1'][@Y='1'][@Z='10']"));
			Assert.IsNotNull(root.SelectSingleNode("part[@Name='NoDamage']"));
			Assert.IsNull(root.SelectSingleNode("part[@Name='Bed']"));
			XmlNode ward = doc.SelectSingleNode("//object[@Name='r_KingdomArcologyWard']");
			XmlNode terrace = doc.SelectSingleNode("//object[@Name='r_KingdomArcologyTerrace']");
			Assert.IsNull(ward.SelectSingleNode("part[@Name='Bed']"));
			Assert.AreEqual("Furniture", terrace.Attributes["Inherits"].Value);
			Assert.IsNull(terrace.SelectSingleNode("tag[@Name='r_KingdomCropRows']"));
			XmlNode growbed = doc.SelectSingleNode("//object[@Name='r_KingdomArcologyGrowbed']");
			Assert.IsNull(growbed.SelectSingleNode("part[@Name='r_KingdomPlot']"));
			Assert.IsNull(growbed.SelectSingleNode("tag[@Name='r_KingdomCropRows']"));
			Assert.AreEqual("2", growbed.SelectSingleNode(
				"tag[@Name='r_TAF_HostedCropRows']").Attributes["Value"].Value);
			Assert.AreEqual(1, doc.SelectNodes("//part[@Name='Interior'][@Cell='TAFArcology']").Count);
			Assert.IsNull(doc.SelectSingleNode("//object[@Name='r_KingdomArcologyWardLift']"));
			Assert.IsNull(doc.SelectSingleNode("//object[@Name='r_KingdomArcologyTerraceLift']"));
			Assert.IsNotNull(doc.SelectSingleNode(
				"//object[@Name='r_KingdomArcologyStairsUp']/part[@Name='StairsUp'][@ConnectionObject='r_KingdomArcologyStairsDown']"));
			Assert.IsNotNull(doc.SelectSingleNode(
				"//object[@Name='r_KingdomArcologyStairsDown']/part[@Name='StairsDown'][@ConnectionObject='r_KingdomArcologyStairsUp']"));
			Assert.IsNotNull(doc.SelectSingleNode(
				"//object[@Name='r_KingdomArcologyExit']/part[@Name='InteriorPortal']"));
		}

		[Test]
		public void InteriorWorldOwnsOneThreeByThreeByThreeSchema()
		{
			XmlDocument doc = new XmlDocument(); doc.LoadXml(Read("RuntimeData", "Worlds.xml"));
			Assert.AreEqual(1, doc.SelectNodes("/worlds/world[@Name='Interior']").Count);
			XmlNode cell = doc.SelectSingleNode("//cell[@Name='TAFArcology']");
			Assert.IsNotNull(cell);
			Assert.AreEqual(1, doc.SelectNodes("//cell[starts-with(@Name,'TAFArcology')]").Count);
			Assert.AreEqual(3, cell.SelectNodes("zone[@x='0-2'][@y='0-2']").Count);
			Assert.AreEqual(1, cell.SelectNodes("zone[@Level='9']").Count);
			Assert.AreEqual(1, cell.SelectNodes("zone[@Level='10']").Count);
			Assert.AreEqual(1, cell.SelectNodes("zone[@Level='11']").Count);
			Assert.AreEqual(3, cell.SelectNodes("zone[@DisableForcedConnections='Yes']").Count);
			Assert.AreEqual(3, doc.SelectNodes(
				"//builder[@Class='KingdomHostedArcologyBuilder']").Count);
		}

		[Test]
		public void BuilderUsesCoordinateAuthorityAndDecorationMintsNoEconomy()
		{
			string builder = Read("World", "KingdomHostedArcologyBuilder.cs");
			StringAssert.Contains("interior.Schema != KingdomHostedArcologyTopology.Schema", builder);
			StringAssert.Contains("HasHorizontalNeighbour", builder);
			StringAssert.Contains("r_KingdomArcologyStairsUp", builder);
			StringAssert.Contains("r_KingdomArcologyStairsDown", builder);
			StringAssert.Contains("IsSurfaceExit", builder);
			StringAssert.Contains("StableRole", builder);
			XmlDocument doc = new XmlDocument(); doc.LoadXml(Read("RuntimeData", "ObjectBlueprints.xml"));
			string[] names = new string[] { "r_KingdomArcologyCeramicBed",
				"r_KingdomArcologySpectrumLamp", "r_KingdomArcologySeedCase",
				"r_KingdomArcologyCondenserShell", "r_KingdomArcologyColdRange",
				"r_KingdomArcologyDormantBunk", "r_KingdomArcologyServiceCabinet",
				"r_KingdomArcologyScrubBank", "r_KingdomArcologyRepairStand" };
			for (int i = 0; i < names.Length; i++)
			{
				XmlNode item = doc.SelectSingleNode("//object[@Name='" + names[i] + "']");
				Assert.IsNotNull(item, names[i]);
				Assert.AreEqual("Furniture", item.Attributes["Inherits"].Value, names[i]);
				Assert.IsNull(item.SelectSingleNode("part[@Name='Inventory' or @Name='LiquidVolume' or @Name='Bed' or @Name='PowerSwitch' or @Name='ElectricalPowerTransmission']"), names[i]);
				Assert.IsNull(item.SelectSingleNode("tag[@Name='r_KingdomCropRows' or @Name='r_TAF_HostedCropRows']"), names[i]);
			}
		}

		[Test]
		public void HostedTerraceFoodIsBackedByExactlyTwentyEightPhysicalRows()
		{
			string programme = Read("World", "KingdomHostedArcologyProgrammeBuilder.cs");
			int growbeds = System.Text.RegularExpressions.Regex.Matches(programme,
				"F\\([^)]*\\\"r_KingdomArcologyGrowbed\\\"").Count;
			Assert.AreEqual(14, growbeds);
			XmlDocument blueprints = new XmlDocument();
			blueprints.LoadXml(Read("RuntimeData", "ObjectBlueprints.xml"));
			int rows = int.Parse(blueprints.SelectSingleNode(
				"//object[@Name='r_KingdomArcologyGrowbed']/tag[@Name='r_TAF_HostedCropRows']")
				.Attributes["Value"].Value);
			Assert.AreEqual(2, rows);
			Assert.AreEqual(14, growbeds * rows
				* KingdomCropRules.YieldPerRow / KingdomCropRules.CropDays);
			string rules = Read("Growth", "KingdomHostedArcologyRules.cs");
			System.Text.RegularExpressions.Match definition =
				System.Text.RegularExpressions.Regex.Match(rules,
					"RegisterBuiltInPaidLot\\(new\\s+KingdomHostedLotDefinition\\s*\\{"
					+ "(?=[^}]*\\bKey\\s*=\\s*\\\"arcologyterrace\\\")([^}]*)\\}");
			Assert.IsTrue(definition.Success);
			StringAssert.Contains("Supports = \"food:14\"", definition.Groups[1].Value);
			StringAssert.Contains("PhysicalProducerBlueprint = \"r_KingdomArcologyGrowbed\"",
				definition.Groups[1].Value);
			StringAssert.Contains("PhysicalProducerCount = 14", definition.Groups[1].Value);
			XmlDocument catalogue = new XmlDocument();
			catalogue.LoadXml(Read("RuntimeData", "KingdomBuildings.xml"));
			Assert.AreEqual("food:14", catalogue.SelectSingleNode(
				"//building[@Key='arcologyterrace']").Attributes["Carries"].Value);
			Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(programme,
				"TerraceLotKey\\s*&&\\s*Programme\\s*==\\s*"
				+ "KingdomArcologyProgramme\\.HydroponicTerrace\\s*\\?\\s*Terrace"));
			string slate = Read("Growth", "KingdomHostedArcologySlateRules.cs");
			StringAssert.Contains("row.Supports != definition.Supports", slate);
			StringAssert.Contains("row.RequiresWater != definition.RequiresWater", slate);
			StringAssert.Contains("TryPaidFixtures(string LotKey", programme);
			string visual = Read("Growth", "KingdomHostedArcology.Visual.cs");
			StringAssert.Contains("producers != Definition.PhysicalProducerCount", visual);
		}

		[Test]
		public void PaidManifestIsClosedAndRealizationPreflightsBeforeMutation()
		{
			string rules = Read("Growth", "KingdomHostedArcologyRules.cs");
			StringAssert.Contains("public static bool RegisterReadOnlyHostedLot", rules);
			StringAssert.DoesNotContain("public static bool RegisterHostedLot", rules);
			StringAssert.Contains("paid hosted-lot registration is closed in v1", rules);
			StringAssert.Contains("private static void RegisterBuiltInPaidLot", rules);
			string visual = Read("Growth", "KingdomHostedArcology.Visual.cs");
			AssertBefore(visual, "if (!TryPreflight(", "TryPlacePrepared(");
			AssertBefore(visual, "!cell.IsEmptyOfSolid()", "GameObject.Create(");
			AssertBefore(visual, "GameObject.Create(", ".AddObject(row.Output");
			StringAssert.Contains("DiscardPrepared(Prepared)", visual);
			StringAssert.Contains("ProvesExactFixtures", visual);
		}

		[Test]
		public void ProgrammePortfolioHasNineFabricsThreeMaterialStrataAndRealLight()
		{
			string programme = Read("World", "KingdomHostedArcologyProgrammeBuilder.cs");
			int programmes = System.Text.RegularExpressions.Regex.Matches(programme,
				"case KingdomArcologyProgramme\\.[A-Za-z]+: return Set\\(").Count;
			Assert.AreEqual(27, programmes);
			for (int i = 0; i < 9; i++)
				StringAssert.Contains("case " + i + ": return", programme);
			Assert.AreEqual(6, System.Text.RegularExpressions.Regex.Matches(programme,
				"\\\"Techlight1\\\",\\\"light:").Count);
			StringAssert.Contains("TAFArcologyPlanSignature", programme);
			StringAssert.Contains("FoamcreteFloor", programme);
			StringAssert.Contains("GreyMarbleFloor", programme);
			StringAssert.Contains("SmallHexFloor", programme);
			StringAssert.Contains("BuildArchetype", programme);
			StringAssert.Contains("TryPaidFixtures(string LotKey", programme);
		}

		[Test]
		public void DesignatedZoneAnchorsRealizeOnceThenFailClosed()
		{
			string visual = Read("Growth", "KingdomHostedArcology.Visual.cs");
			StringAssert.Contains("IsHostedLotZone(Anchor.LotKey", visual);
			AssertBefore(visual, "!KingdomHostedArcology.IsOperationalPure(shell)",
				"IsHostedLotZone(Anchor.LotKey");
			StringAssert.Contains("if (Anchor.FixturesRealized)", visual);
			StringAssert.Contains("it was not respawned", visual);
			StringAssert.Contains("duplicated, mistyped, or displaced", visual);
			StringAssert.Contains("anchor is displaced or ambiguous", visual);
			StringAssert.Contains("return Quarantine(root, failure)", visual);
			StringAssert.Contains("KingdomHostedArcologyProgrammeBuilder.TryPaidFixtures", visual);
			StringAssert.Contains("Anchor.FixturesRealized = true", visual);
			string anchor = Read("Growth", "r_KingdomArcologyZoneAnchor.cs");
			StringAssert.Contains("public bool FixturesRealized", anchor);
			StringAssert.DoesNotContain("KingdomHostedArcologyVisual.Reconcile", anchor);
			string driver = Read("Growth", "KingdomHostedArcology.InteriorDriver.cs");
			StringAssert.Contains("KingdomHostedArcologyVisual.Reconcile", driver);
			string builder = Read("World", "KingdomHostedArcologyBuilder.cs");
			StringAssert.DoesNotContain("KingdomHostedArcologyVisual.Reconcile", builder);
			string events = Read("Core", "KingdomSystem.z20.Events.cs");
			Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(events,
				"KingdomHostedArcology\\.ReconcileActiveInterior").Count);
			string runtime = Read("Growth", "KingdomHostedArcology.Runtime.cs");
			StringAssert.Contains("interior.Schema != KingdomHostedArcologyTopology.Schema", runtime);
			StringAssert.Contains("KingdomHostedArcologyTopology.InBounds", runtime);
			StringAssert.Contains("TryLoadedInteriorRoot(interior", runtime);
		}

		[Test]
		public void AuthorityAndConstructionFailClosedAtExactCarrierBoundaries()
		{
			string authority = Read("Growth", "KingdomHostedArcology.Authority.cs");
			string begin = Read("Growth", "KingdomUpgrade.14.Begin.cs");
			string handover = Read("Growth", "KingdomUpgrade.20.HandOver.cs");
			string construction = Read("Growth", "KingdomHostedArcology.Construction.cs");
			StringAssert.Contains("AdvanceLaborAfterMasterEdge", construction);
			StringAssert.Contains("System.MasterOptionTick", construction);
			string zoning = Read("Growth", "KingdomZoning.02.OffersAndJudgment.cs");
			string strike = Read("Growth", "KingdomMaterials.08.StrikeOrdering.cs");
			StringAssert.Contains("AuthoritySlotKeys", authority);
			StringAssert.Contains("AuthoritySlotForWrite", authority);
			StringAssert.DoesNotContain("AuthorityPrefix + System.RealmId", authority);
			StringAssert.Contains("KingdomCrown.CrownedOn(System, ZoneId)", authority);
			StringAssert.Contains("another exact carrier", authority);
			StringAssert.Contains("|| !system.ClaimedZones.Contains(zone.ZoneID)) return true;",
				authority);
			StringAssert.Contains("TryReserve(System, Z, Work", begin);
			StringAssert.Contains("BitCostFor(A.SuccessorKey)", begin);
			StringAssert.Contains("BindAuthority(ownerSystem", handover);
			StringAssert.Contains("CanReserveAt(System, ZoneID", zoning);
			StringAssert.Contains("Building.GetPart<r_KingdomArcology>()", strike);
			StringAssert.Contains("cannot be struck", strike);
			StringAssert.Contains("TryDecodeLot(Job.PhysicalReceipt", construction);
			StringAssert.Contains("physical.StaffingBasis = 0", construction);
		}

		[Test]
		public void ReadOnlyProviderReceivesLoadedHostAndExposesNoResearchMutation()
		{
			string source = Read("Growth", "KingdomHostedArcology.Interaction.cs");
			StringAssert.Contains("KingdomHostedReadOnlyEligibility(KingdomSystem System,",
				source);
			StringAssert.Contains("Zone HostZone, GameObject HostRoot, out string Refusal", source);
			StringAssert.Contains("provider.Eligibility(system, shell.CurrentZone, shell", source);
			StringAssert.Contains("provider.View(system)", source);
			StringAssert.Contains("Zone provedZone = shell.CurrentZone", source);
			StringAssert.Contains("changed while the view was drawn", source);
			StringAssert.DoesNotContain("Learn(", source);
			StringAssert.DoesNotContain("Queue", source);
			StringAssert.DoesNotContain("Budget", source);
		}

		[Test]
		public void RuntimeNeverLoadsRemoteZonesOrSimulatesHostedActors()
		{
			string runtime = Read("Growth", "KingdomHostedArcology.Runtime.cs");
			StringAssert.Contains("int need = 0", runtime);
			StringAssert.Contains("need = 4", runtime);
			StringAssert.Contains("&& IsOperationalPure(work)", runtime);
			StringAssert.Contains("TryReceiptSlate(root, out rows, out failure)", runtime);
			StringAssert.Contains("Quarantine(root, failure)", runtime);
			string authority = Read("Growth", "KingdomHostedArcology.Authority.cs");
			StringAssert.Contains("!string.IsNullOrEmpty(hosted.QuarantineReason)", authority);
			string source = Read("Growth", "KingdomHostedArcology.Authority.cs")
				+ Read("Growth", "KingdomHostedArcology.Construction.cs")
				+ runtime
				+ Read("Growth", "KingdomHostedArcology.Visual.cs")
				+ Read("Growth", "KingdomHostedArcology.LoadedRoot.cs")
				+ Read("Growth", "KingdomHostedArcology.Designation.cs")
				+ Read("Growth", "KingdomHostedArcology.Observation.cs")
				+ Read("Growth", "KingdomHostedArcology.Departure.cs")
				+ Read("Growth", "KingdomHostedArcology.DepartureStore.cs")
				+ Read("Growth", "KingdomHostedArcology.InteriorDriver.cs")
				+ Read("Growth", "KingdomHostedArcology.CityProjection.cs")
				+ Read("Growth", "KingdomHostedArcology.Quarantine.cs");
			StringAssert.DoesNotContain("ZoneManager.GetZone", source);
			StringAssert.DoesNotContain("GetZone(", source);
			StringAssert.DoesNotContain("DirectMove", source);
			StringAssert.DoesNotContain("IsCreature", source);
			StringAssert.DoesNotContain("BaseHumanoid", source);
		}

		[Test]
		public void InteriorRootLookupIsBoundedCanonicalAndNeverThawsItsExterior()
		{
			string source = Read("Growth", "KingdomHostedArcology.LoadedRoot.cs");
			StringAssert.Contains("manager.ActiveZone", source);
			StringAssert.Contains("manager.CachedZones", source);
			StringAssert.Contains("cached.Count > MaxLoadedRootZones", source);
			StringAssert.Contains("objects.Count > MaxLoadedRootObjects", source);
			StringAssert.Contains("(long)Inspected + objects.Count", source);
			StringAssert.Contains("pair.Key != pair.Value.ZoneID", source);
			StringAssert.Contains("cached.TryGetValue(pair.Value.ZoneID, out held)", source);
			StringAssert.Contains("ReferenceEquals(held, pair.Value)", source);
			StringAssert.Contains("Interior.Location.ZoneID != Z.ZoneID", source);
			StringAssert.Contains("Interior.Location.CellX != cell.X", source);
			StringAssert.Contains("Interior.Location.CellY != cell.Y", source);
			StringAssert.Contains("Root != null && !ReferenceEquals(Root, candidate)", source);
			StringAssert.Contains("count > 1", source);
			StringAssert.DoesNotContain("Interior.ParentObject", source);
			StringAssert.DoesNotContain("GetZone(", source);
			StringAssert.DoesNotContain("FindObjectByID", source);
		}

		[Test]
		public void FinalObservationUsesActiveAttendedExactInteriorEvidence()
		{
			string designation = Read("Growth", "KingdomHostedArcology.Designation.cs");
			StringAssert.Contains("[KingdomDesignationProvider]", designation);
			StringAssert.Contains("RootId = Context.AnchorObject.IDIfAssigned", designation);
			StringAssert.Contains("Context.Zone.Width * Context.Zone.Height", designation);
			StringAssert.Contains("KingdomDesignationRules.MaxCellsPerDesignation", designation);
			StringAssert.Contains("KingdomBenefitCellUse.Interior", designation);
			StringAssert.Contains("TryInteriorZoneIdentity(shell, lotKey, Z.ZoneID", designation);
			StringAssert.Contains("interior.Instance != shell.IDIfAssigned", designation);
			StringAssert.Contains("part.X != KingdomHostedArcologyTopology.EntryX", designation);
			StringAssert.Contains("part.Y != KingdomHostedArcologyTopology.EntryY", designation);
			StringAssert.Contains("part.Z != KingdomHostedArcologyTopology.EntryZ", designation);
			StringAssert.Contains("string entry = part.ZoneID", designation);
			StringAssert.Contains("receipt.RootId != shell.IDIfAssigned", designation);
			StringAssert.Contains("TryExactAnchor", designation);

			string observation = Read("Growth", "KingdomHostedArcology.Observation.cs");
			string departure = Read("Growth", "KingdomHostedArcology.Departure.cs");
			string witness = departure + observation;
			StringAssert.DoesNotContain("ActiveZone", witness);
			StringAssert.Contains("context.Anchor.Attended", departure);
			AssertBefore(departure, "context.Anchor.Attended = false",
				"TryLiveContext(Z, true");
			AssertBefore(departure, "TryLiveContext(Z, true", "KingdomBenefitIndex.TryBuild");
			StringAssert.DoesNotContain("KingdomHostedArcologyVisual.Reconcile", departure);
			StringAssert.Contains("KingdomSurvey.TakeCustodyOnly(Z)", departure);
			StringAssert.Contains("survey.BindPass()", departure);
			StringAssert.DoesNotContain("KingdomSurvey.Take(Z", departure);
			StringAssert.Contains("FinalizeDesignationIdentity(designation)", departure);
			StringAssert.Contains("\"ext:\" + Designation.ProviderId.ToLowerInvariant()",
				departure);
			StringAssert.Contains("AmountForRoot(\n\t\t\t\t\tcontext.AnchorObject.IDIfAssigned",
				departure);
			StringAssert.Contains("ObserveTerraceFood(context)", departure);
			StringAssert.Contains("item.IsBroken()", observation);
			StringAssert.Contains("r_TAF_HostedCropRows", observation);
		}

		[Test]
		public void DeactivationZerosThenTrueSuspensionPublishesFinalBoundedSnapshot()
		{
			string events = Read("Core", "KingdomSystem.z20.Events.cs");
			string register = Read("Core", "KingdomSystem.z19.PersistenceAndCallbacks.cs");
			string departure = Read("Growth", "KingdomHostedArcology.Departure.cs");
			int suspend = events.IndexOf("HandleEvent(SuspendingEvent E)",
				StringComparison.Ordinal);
			int observe = events.IndexOf("KingdomHostedArcology.OnSuspending(this, E.Zone)",
				StringComparison.Ordinal);
			int master = events.IndexOf("KingdomMaster.ObserveAutomaticWake(this, game.TimeTicks)",
				suspend, StringComparison.Ordinal);
			Assert.GreaterOrEqual(suspend, 0); Assert.Greater(observe, suspend);
			Assert.Greater(master, observe,
				"final physical observation must not preserve stale fixtures while work is paused");
			Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(events,
				"KingdomHostedArcology\\.OnSuspending").Count);
			Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(events,
				"KingdomHostedArcology\\.OnDeactivated").Count);
			StringAssert.Contains("Registrar.Register(ZoneDeactivatedEvent.ID)", register);
			StringAssert.Contains("Registrar.Register(SuspendingEvent.ID)", register);
			StringAssert.Contains("InvalidateDeparture(System, Z)", departure);
			StringAssert.Contains("ObserveSuspension(System, Z)", departure);
			StringAssert.Contains("awaiting final suspension observation", departure);
			StringAssert.Contains("PersistDepartureFault(System, envelope, corrupt", departure);
			string store = Read("Growth", "KingdomHostedArcology.DepartureStore.cs");
			AssertBefore(store, "FenceExistingProjection(slot, authority, lot",
				"Interior.Instance != authority.CarrierId");
			StringAssert.Contains("snapshot is absent or names another authority", store);

			string root = Read("Growth", "r_KingdomArcology.cs");
			StringAssert.Contains("public List<string> LotObservations", root);
			StringAssert.Contains("LotReceipts = new List<string>();\n\t\t\tLotObservations = new List<string>();",
				root);
			StringAssert.Contains("MaxEncodedObservationChars", root);
			StringAssert.Contains("TryReadObservations(LotObservations", root);
			string receipt = Read("Growth", "KingdomHostedArcologyReceipt.cs");
			StringAssert.Contains("TAF-HOSTED-OBSERVATION-V1", receipt);
			StringAssert.Contains("ObservedTick", receipt);
			StringAssert.Contains("return (KingdomHostedObservation)MemberwiseClone()", receipt);
			string slate = Read("Growth", "KingdomHostedArcologySlateRules.cs");
			StringAssert.Contains("duplicate observations", slate);
			StringAssert.Contains("ReceiptRevision", slate);
			StringAssert.Contains("dated in the future", slate);
		}

		[Test]
		public void ExteriorConsumesDatedPhysicalOutputWithoutCatalogueSupply()
		{
			string subsidence = Read("Growth", "KingdomSubsidence.cs");
			string scoped = Read("Growth", "KingdomSubsidence.ScopeAndSightings.cs");
			string projection = Read("Growth", "KingdomObservedBenefitProjection.cs");
			string reach = Read("Growth", "KingdomReach.GroundCharacter.cs");
			StringAssert.Contains("TryTerracePhysicalFood(work,\n\t\t\t\t\t\tSurvey.StoredWater > 0",
				subsidence);
			StringAssert.Contains("KingdomObservedBenefitProjection.TryCarries(work, reading",
				subsidence);
			StringAssert.Contains("KingdomObservedBenefitProjection.TryCarries(work, reading",
				scoped);
			StringAssert.Contains("KingdomObservedBenefitProjection.TryCarries(Root, Reading",
				reach);
			StringAssert.Contains("KingdomHostedArcology.TryWardPhysical(", projection);
			StringAssert.DoesNotContain("TryWardPhysical", subsidence + scoped + reach);
			StringAssert.DoesNotContain("HostedCarries", subsidence + scoped
				+ Read("Growth", "KingdomHostedArcology.Runtime.cs"));

			string observation = Read("Growth", "KingdomHostedArcology.Observation.cs");
			StringAssert.Contains("physical output unobserved", observation);
			StringAssert.Contains("observed this visit", observation);
			StringAssert.Contains("credited only while fresh-water flow reaches it", observation);
			StringAssert.DoesNotContain("observation.Food = benefits", observation);
			StringAssert.DoesNotContain("observation.Roof = Cap", observation);
			StringAssert.DoesNotContain("observation.Luxury = Cap", observation);
		}

		[Test]
		public void EveryHostedProductionShardStaysBelowThreeHundredLines()
		{
			string[] files = new string[] {
				"Growth/KingdomHostedLotDefinition.cs",
				"Growth/KingdomHostedArcologyRules.cs",
				"Growth/KingdomHostedArcologySlateRules.cs",
				"Growth/KingdomHostedArcologyTopology.cs",
				"Growth/KingdomHostedArcologyReceipt.cs",
				"Growth/KingdomHostedArcology.Authority.cs",
				"Growth/KingdomHostedArcology.Construction.cs",
				"Growth/KingdomHostedArcology.Interaction.cs",
				"Growth/KingdomHostedArcology.Designation.cs",
				"Growth/KingdomHostedArcology.Departure.cs",
				"Growth/KingdomHostedArcology.DepartureStore.cs",
				"Growth/KingdomHostedArcology.InteriorDriver.cs",
				"Growth/KingdomHostedArcology.CityProjection.cs",
				"Growth/KingdomHostedArcology.Quarantine.cs",
				"Growth/KingdomHostedArcology.LoadedRoot.cs",
				"Growth/KingdomHostedArcology.Observation.cs",
				"Growth/KingdomHostedArcology.Runtime.cs",
				"Growth/KingdomHostedArcology.Visual.cs",
				"Growth/KingdomHostedDepartureState.cs",
				"Growth/r_KingdomArcology.cs",
				"Growth/r_KingdomArcologyZoneAnchor.cs",
				"World/KingdomHostedArcologyBuilder.cs",
				"World/KingdomHostedArcologyProgrammeBuilder.cs"
			};
			for (int i = 0; i < files.Length; i++)
			{
				int lines = Read(files[i].Split('/')).Split('\n').Length;
				Assert.Less(lines, 300, files[i]);
			}
		}

		[Test]
		public void AuthorityWritersAreOrderedAndPassiveReadersStayPure()
		{
			string authority = Read("Growth", "KingdomHostedArcology.Authority.cs");
			string construction = Read("Growth", "KingdomHostedArcology.Construction.cs");
			string events = Read("Core", "KingdomSystem.z20.Events.cs");
			string root = Read("Growth", "r_KingdomArcology.cs");
			Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(events,
				"KingdomHostedArcology\\.ReconcileRoot").Count);
			Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(construction,
				"ReconcileRoot\\(shell").Count);
			StringAssert.DoesNotContain("ReconcileRoot", root);
			StringAssert.DoesNotContain("KingdomHostedArcology.Quarantine", root);
			int pure = authority.IndexOf("internal static bool IsOperationalPure",
				StringComparison.Ordinal);
			int next = authority.IndexOf("internal static bool TryReconciliationRoot",
				pure, StringComparison.Ordinal);
			Assert.GreaterOrEqual(pure, 0); Assert.Greater(next, pure);
			string body = authority.Substring(pure, next - pure);
			StringAssert.Contains("GetSystem<KingdomSystem>()", body);
			StringAssert.DoesNotContain("RequireSystem", body);
			StringAssert.DoesNotContain("ReconcileRoot", body);
			StringAssert.DoesNotContain("WriteExact", body);
		}

		private static void AssertBefore(string Source, string First, string Second)
		{
			int first = Source.IndexOf(First, StringComparison.Ordinal);
			int second = Source.IndexOf(Second, StringComparison.Ordinal);
			Assert.GreaterOrEqual(first, 0, First);
			Assert.GreaterOrEqual(second, 0, Second);
			Assert.Less(first, second, First + " before " + Second);
		}
	}
}
#endif
