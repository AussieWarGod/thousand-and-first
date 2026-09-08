#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomQuickstartTests
	{
		private static readonly string[] StateKeys =
		{
			KingdomQuickstartRules.ProfileState,
			KingdomQuickstartRules.ReceiptState,
			KingdomQuickstartRules.QuarantineState,
			KingdomQuickstartRules.WorldReservationState
		};

		[Test]
		public void ProfilesAreExactReviewedGround()
		{
			Assert.That(KingdomQuickstartRules.ProfileCount, Is.EqualTo(3));
			AssertProfile("marsh", "TAFQuickstartMarsh", "JoppaWorld.8.22.1.1.10",
				"Reedwake", "TerrainSaltmarsh", 8, 22);
			AssertProfile("canyon", "TAFQuickstartCanyon", "JoppaWorld.14.17.1.1.10",
				"Riftside", "TerrainDesertCanyon", 14, 17);
			AssertProfile("dunes", "TAFQuickstartDunes", "JoppaWorld.6.17.1.1.10",
				"Saltwake", "TerrainSaltdunes", 6, 17);
			Assert.That(KingdomQuickstartRules.TryProfile("Marsh", out _), Is.False);
			Assert.That(KingdomQuickstartRules.TryProfileForLocation("Joppa", out _), Is.False);
		}

		[TestCase("quickstart-boot marsh yes", "marsh", true)]
		[TestCase("quickstart-boot marsh no", "marsh", false)]
		[TestCase("quickstart-boot canyon yes", "canyon", true)]
		[TestCase("quickstart-boot canyon no", "canyon", false)]
		[TestCase("quickstart-boot dunes yes", "dunes", true)]
		[TestCase("quickstart-boot dunes no", "dunes", false)]
		public void BootRequestParsesSixExactNativeTestSelections(string Command, string Profile, bool Advisor)
		{
			Assert.That(Harness.KingdomQuickstartBootRequest.TryParse(new[] { Command }, out var request), Is.True);
			Assert.That(request.ProfileKey, Is.EqualTo(Profile));
			Assert.That(request.Advisor, Is.EqualTo(Advisor));
			Assert.That(request.Command, Is.EqualTo(Command));
			Assert.That(request.Save, Is.False);
		}

		[TestCase("quickstart-save marsh yes", "marsh", true)]
		[TestCase("quickstart-save marsh no", "marsh", false)]
		[TestCase("quickstart-save canyon yes", "canyon", true)]
		[TestCase("quickstart-save canyon no", "canyon", false)]
		[TestCase("quickstart-save dunes yes", "dunes", true)]
		[TestCase("quickstart-save dunes no", "dunes", false)]
		public void SaveRequestPreservesExactSelectionAndDoesNotChangeBootOnly(string Command, string Profile, bool Advisor)
		{
			Assert.That(Harness.KingdomQuickstartBootRequest.TryParse(new[] { Command }, out var request), Is.True);
			Assert.That(request.ProfileKey, Is.EqualTo(Profile));
			Assert.That(request.Advisor, Is.EqualTo(Advisor));
			Assert.That(request.Command, Is.EqualTo(Command));
			Assert.That(request.Save, Is.True);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("quickstart-boot")]
		[TestCase("quickstart-boot marsh")]
		[TestCase("quickstart-boot Marsh yes")]
		[TestCase("quickstart-boot marsh Yes")]
		[TestCase("quickstart-boot marsh 1")]
		[TestCase("quickstart-boot unknown no")]
		[TestCase("quickstart-boot  marsh yes")]
		[TestCase("quickstart-boot\tmarsh yes")]
		[TestCase("quickstart-boot marsh yes\n")]
		[TestCase("quickstart-boot marsh yes status")]
		[TestCase("status quickstart-boot marsh yes")]
		[TestCase("quickstart-check")]
		[TestCase("quickstart-save")]
		[TestCase("quickstart-save marsh yes status")]
		[TestCase("quickstart-save marsh 1")]
		public void BootRequestRefusesNoncanonicalCommands(string Command)
		{
			Assert.That(Harness.KingdomQuickstartBootRequest.TryParse(new[] { Command }, out var request), Is.False);
			Assert.That(request, Is.Null);
		}

		[Test]
		public void BootRequestRefusesMissingMixedAndOversizedScripts()
		{
			Assert.That(Harness.KingdomQuickstartBootRequest.TryParse(null, out _), Is.False);
			Assert.That(Harness.KingdomQuickstartBootRequest.TryParse(new string[0], out _), Is.False);
			Assert.That(Harness.KingdomQuickstartBootRequest.TryParse(
				new[] { "quickstart-boot marsh yes", "status" }, out _), Is.False);
			Assert.That(Harness.KingdomQuickstartBootRequest.TryParse(new[] { new string('x', 97) }, out _), Is.False);
		}

		[Test]
		public void PreparedGroundIsApronSupplyApproachHeartIngressAndTheShelterLot()
		{
			int endpoints = 0;
			int shelterCells = 0;
			for (int y = -1; y <= 25; y++)
				for (int x = -1; x <= 80; x++)
				{
					bool camp = (x >= 37 && x <= 44 && y >= 10 && y <= 15)
						|| (x >= 27 && x <= 30 && y >= 9 && y <= 17)
						|| (x >= 29 && x <= 37 && y >= 11 && y <= 13);
					bool endpoint = (x == 40 || x == 41) && y == 16;
					bool shelter = x >= 21 && x <= 26 && y >= 9 && y <= 12;
					ClassicAssert.AreEqual(camp || endpoint || shelter,
						KingdomQuickstartRules.RequiresPreparedGround(x, y), x + "," + y);
					if (endpoint && !camp) endpoints++;
					if (shelter && !camp && !endpoint) shelterCells++;
				}
			ClassicAssert.AreEqual(2, endpoints);
			ClassicAssert.AreEqual(24, shelterCells);
			ClassicAssert.AreEqual(21, KingdomQuickstartRules.ShelterX1);
			ClassicAssert.AreEqual(9, KingdomQuickstartRules.ShelterY1);
			ClassicAssert.AreEqual(26, KingdomQuickstartRules.ShelterX2);
			ClassicAssert.AreEqual(12, KingdomQuickstartRules.ShelterY2);
			// One Small plot (6x4), west of the supply column, clear of every reserved role cell,
			// of the founder's start cell, and of the heart's extreme survey (which begins at 31).
			ClassicAssert.AreEqual(6, KingdomQuickstartRules.ShelterX2 - KingdomQuickstartRules.ShelterX1 + 1);
			ClassicAssert.AreEqual(4, KingdomQuickstartRules.ShelterY2 - KingdomQuickstartRules.ShelterY1 + 1);
			foreach (int roleY in new[] { 10, 12, 14, 16 })
				ClassicAssert.IsFalse(KingdomQuickstartRules.ShelterX1 <= 28
					&& 28 <= KingdomQuickstartRules.ShelterX2
					&& KingdomQuickstartRules.ShelterY1 <= roleY
					&& roleY <= KingdomQuickstartRules.ShelterY2, "role cell 28," + roleY);
			ClassicAssert.IsFalse(KingdomQuickstartRules.ShelterX1 <= KingdomQuickstartRules.StartCellX
				&& KingdomQuickstartRules.StartCellX <= KingdomQuickstartRules.ShelterX2
				&& KingdomQuickstartRules.ShelterY1 <= KingdomQuickstartRules.StartCellY
				&& KingdomQuickstartRules.StartCellY <= KingdomQuickstartRules.ShelterY2);
			Assert.That(KingdomQuickstartRules.ShelterX2, Is.LessThan(31));
			foreach (int outside in new[] { int.MinValue, int.MaxValue })
			{
				ClassicAssert.IsFalse(KingdomQuickstartRules.RequiresPreparedGround(outside, 12));
				ClassicAssert.IsFalse(KingdomQuickstartRules.RequiresPreparedGround(40, outside));
			}
		}

		[Test]
		public void ShelterIsStakedOnTheCalendarAfterFoundingAndNeverWearsTheGrantMarker()
		{
			string shelter = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Shelter.cs");
			StringAssert.Contains("KingdomPlots.Stake(", shelter);
			StringAssert.Contains("KingdomData.TryGetBuilding(KingdomQuickstartRules.ShelterBuildKey", shelter);
			StringAssert.Contains("KingdomPlots.TryGetSpec(entry.Key, out spec)", shelter);
			StringAssert.Contains("new KingdomPlots.GroundGrid(Zone)", shelter);
			StringAssert.Contains("KingdomPlotRules.IsUnderground(Zone.Z)", shelter);
			StringAssert.Contains("KingdomQuickstartRules.ShelterMarkerProperty", shelter);
			// The grant recovery scan reads every object in the zone and refuses any wearing the
			// grant marker with a value it did not mint, so the shelter must never carry it.
			StringAssert.DoesNotContain("GrantMarkerProperty", shelter);
			// Nothing here invents completion or drives the plot clock.
			StringAssert.DoesNotContain("KingdomPlots.Advance(", shelter);
			StringAssert.DoesNotContain("SetIntProperty(\"KingdomBuilt\"", shelter);
			StringAssert.DoesNotContain("PlotWorkSchemaProperty", shelter);
			StringAssert.DoesNotContain("Popup", shelter);

			string bootstrap = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.cs");
			int founded = bootstrap.IndexOf("if (!VerifyFounded(system, zone, profile, out Failure)) return false;",
				StringComparison.Ordinal);
			int stake = bootstrap.IndexOf("TryStakeShelter(system, zone, out Failure)",
				StringComparison.Ordinal);
			int advance = bootstrap.IndexOf("KingdomQuickstartPhase.Founded, crop",
				StringComparison.Ordinal);
			Assert.That(founded, Is.GreaterThanOrEqualTo(0));
			Assert.That(stake, Is.GreaterThan(founded));
			Assert.That(advance, Is.GreaterThan(stake));
			ClassicAssert.AreEqual(stake, bootstrap.LastIndexOf("TryStakeShelter(system, zone, out Failure)",
				StringComparison.Ordinal));
		}

		[Test]
		public void ReceiptIsExactMonotoneAndTamperEvident()
		{
			Assert.That(KingdomQuickstartRules.TryCreateReceipt("marsh",
				"JoppaWorld.8.22.1.1.10", out KingdomQuickstartReceipt receipt), Is.True);
			Assert.That(KingdomQuickstartRules.TryAdvance(receipt,
				KingdomQuickstartPhase.WaterStocked, "early", 0, out _), Is.False);
			receipt = Advance(receipt, KingdomQuickstartPhase.Founded, "Watervine");
			receipt = Advance(receipt, KingdomQuickstartPhase.WaterStocked, "water-id");
			receipt = Advance(receipt, KingdomQuickstartPhase.FoodStocked, "larder-id");
			receipt = Advance(receipt, KingdomQuickstartPhase.MaterialsStocked, "stock-id");
			receipt = Advance(receipt, KingdomQuickstartPhase.AdvisorResolved, "advisor-id",
				KingdomQuickstartAdvisorDisposition.Included);
			receipt = Advance(receipt, KingdomQuickstartPhase.Complete, "");
			string wire = KingdomQuickstartRules.Encode(receipt);
			Assert.That(KingdomQuickstartRules.TryDecode(wire, out KingdomQuickstartReceipt decoded),
				Is.True);
			Assert.That(decoded.Phase, Is.EqualTo(KingdomQuickstartPhase.Complete));
			Assert.That(decoded.AdvisorObjectId, Is.EqualTo("advisor-id"));
			char replacement = wire[wire.Length - 1] == '0' ? '1' : '0';
			Assert.That(KingdomQuickstartRules.TryDecode(
				wire.Substring(0, wire.Length - 1) + replacement, out _), Is.False);
		}

		[Test]
		public void GrantMarkerIsStableAcrossPublicationAndBoundToRoleAndGround()
		{
			KingdomQuickstartRules.TryCreateReceipt("marsh", "JoppaWorld.8.22.1.1.10",
				out KingdomQuickstartReceipt receipt);
			receipt = Advance(receipt, KingdomQuickstartPhase.Founded, "Watervine");
			string water = KingdomQuickstartRules.GrantMarker(receipt,
				KingdomQuickstartPhase.WaterStocked);
			string food = KingdomQuickstartRules.GrantMarker(receipt,
				KingdomQuickstartPhase.FoodStocked);
			receipt = Advance(receipt, KingdomQuickstartPhase.WaterStocked, "water-id");
			Assert.That(KingdomQuickstartRules.GrantMarker(receipt,
				KingdomQuickstartPhase.WaterStocked), Is.EqualTo(water));
			Assert.That(food, Is.Not.EqualTo(water));

			KingdomQuickstartRules.TryCreateReceipt("canyon", "JoppaWorld.14.17.1.1.10",
				out KingdomQuickstartReceipt other);
			other = Advance(other, KingdomQuickstartPhase.Founded, "Watervine");
			Assert.That(KingdomQuickstartRules.GrantMarker(other,
				KingdomQuickstartPhase.WaterStocked), Is.Not.EqualTo(water));
			Assert.That(KingdomQuickstartRules.GrantMarker(receipt,
				KingdomQuickstartPhase.Founded), Is.Null);
		}

		[Test]
		public void EveryPhysicalMutationCutHasOneIdempotentRecoveryAction()
		{
			KingdomQuickstartPhase[] targets =
			{
				KingdomQuickstartPhase.WaterStocked,
				KingdomQuickstartPhase.FoodStocked,
				KingdomQuickstartPhase.MaterialsStocked,
				KingdomQuickstartPhase.AdvisorResolved
			};
			for (int i = 0; i < targets.Length; i++)
			{
				KingdomQuickstartPhase target = targets[i];
				KingdomQuickstartPhase before = (KingdomQuickstartPhase)((int)target - 1);
				Assert.That(KingdomQuickstartRules.RecoveryAction(before, target,
					KingdomQuickstartGrantObservation.Absent), Is.EqualTo(
					KingdomQuickstartRecoveryAction.PreparePlaceAndPublish), target + " cut 0");
				Assert.That(KingdomQuickstartRules.RecoveryAction(before, target,
					KingdomQuickstartGrantObservation.ExactPlaced), Is.EqualTo(
					KingdomQuickstartRecoveryAction.PublishExisting), target + " cut 1");
				Assert.That(KingdomQuickstartRules.RecoveryAction(target, target,
					KingdomQuickstartGrantObservation.ExactPlaced), Is.EqualTo(
					KingdomQuickstartRecoveryAction.VerifyPublished), target + " cut 2");
				Assert.That(KingdomQuickstartRules.RecoveryAction(before, target,
					KingdomQuickstartGrantObservation.ForeignOrMalformed), Is.EqualTo(
					KingdomQuickstartRecoveryAction.Refuse), target + " foreign");
				Assert.That(KingdomQuickstartRules.RecoveryAction(target, target,
					KingdomQuickstartGrantObservation.Absent), Is.EqualTo(
					KingdomQuickstartRecoveryAction.Refuse), target + " published but absent");
			}
			Assert.That(KingdomQuickstartRules.RecoveryAction(
				KingdomQuickstartPhase.Founded, KingdomQuickstartPhase.MaterialsStocked,
				KingdomQuickstartGrantObservation.Absent), Is.EqualTo(
				KingdomQuickstartRecoveryAction.Refuse));
		}

		[Test]
		public void OmittedAdvisorIsDurableAndCannotAcquireAnIdentity()
		{
			KingdomQuickstartRules.TryCreateReceipt("dunes", "JoppaWorld.6.17.1.1.10",
				out KingdomQuickstartReceipt receipt);
			receipt = Advance(receipt, KingdomQuickstartPhase.Founded, "Yuckwheat");
			receipt = Advance(receipt, KingdomQuickstartPhase.WaterStocked, "water");
			receipt = Advance(receipt, KingdomQuickstartPhase.FoodStocked, "food");
			receipt = Advance(receipt, KingdomQuickstartPhase.MaterialsStocked, "materials");
			Assert.That(KingdomQuickstartRules.TryAdvance(receipt,
				KingdomQuickstartPhase.AdvisorResolved, "forbidden-id",
				KingdomQuickstartAdvisorDisposition.Omitted, out _), Is.False);
			receipt = Advance(receipt, KingdomQuickstartPhase.AdvisorResolved, "",
				KingdomQuickstartAdvisorDisposition.Omitted);
			receipt = Advance(receipt, KingdomQuickstartPhase.Complete, "");
			Assert.That(KingdomQuickstartRules.TryDecode(
				KingdomQuickstartRules.Encode(receipt), out KingdomQuickstartReceipt decoded),
				Is.True);
			Assert.That(decoded.AdvisorDisposition,
				Is.EqualTo(KingdomQuickstartAdvisorDisposition.Omitted));
			Assert.That(decoded.AdvisorObjectId, Is.Empty);
		}

		[Test]
		public void ReservationProofIsProfileBoundAndTamperEvident()
		{
			KingdomQuickstartRules.TryProfile("canyon", out KingdomQuickstartProfile canyon);
			KingdomQuickstartRules.TryProfile("marsh", out KingdomQuickstartProfile marsh);
			string wire = KingdomQuickstartRules.WorldReservation(canyon);
			Assert.That(KingdomQuickstartRules.WorldReservationMatches(wire, canyon), Is.True);
			Assert.That(KingdomQuickstartRules.WorldReservationMatches(wire, marsh), Is.False);
			Assert.That(KingdomQuickstartRules.WorldReservationMatches(wire + "x", canyon),
				Is.False);
		}

		[Test]
		public void EmbarkContractIsSeparateCompleteAndGrantFree()
		{
			XDocument xml = XDocument.Parse(TestMain.ReadRepositoryText(
				"RuntimeData/EmbarkModules.xml"));
			XElement mode = xml.Descendants("mode").Single(e =>
				(string)e.Attribute("ID") == KingdomQuickstartRules.ModeId);
			Assert.That((string)mode.Attribute("Title"), Is.EqualTo("Kingdom Quickstart"));
			Assert.That(mode.Elements("stringgamestate").Single(e =>
				(string)e.Attribute("Name") == "GameMode").Attribute("Value").Value,
				Is.EqualTo(KingdomQuickstartRules.ModeId));
			Assert.That(mode.Elements("boolgamestate").Single(e =>
				(string)e.Attribute("Name") == "r_TAF_KingdomMode").Attribute("Value").Value,
				Is.EqualTo("true"));
			Assert.That(mode.Elements("gamesystem").Single().Attribute("Class").Value,
				Is.EqualTo("ThousandAndFirst.KingdomSuccession"));

			XElement[] locations = xml.Descendants("location").Where(e =>
				(string)e.Attribute("Set") == KingdomQuickstartRules.LocationSet).ToArray();
			Assert.That(locations, Has.Length.EqualTo(3));
			HashSet<string> expectedPositions = new HashSet<string>(
				Enumerable.Range(0, 3).SelectMany(y => Enumerable.Range(0, 5)
					.Select(x => x.ToString() + y.ToString())), StringComparer.Ordinal);
			foreach (XElement location in locations)
			{
				Assert.That((string)location.Attribute("ExcludeFromDaily"), Is.EqualTo("Yes"));
				Assert.That(location.Elements("item"), Is.Empty);
				Assert.That(location.Elements("skill"), Is.Empty);
				Assert.That(location.Elements("reputation"), Is.Empty);
				XElement state = location.Elements("stringgamestate").Single();
				Assert.That((string)state.Attribute("Name"),
					Is.EqualTo(KingdomQuickstartRules.ProfileState));
				CollectionAssert.AreEquivalent(expectedPositions,
					location.Elements("grid").Select(e => (string)e.Attribute("Position")).ToArray());
			}
			Assert.That(xml.Root.Elements("module").Count(e =>
				(string)e.Attribute("Class") ==
				"ThousandAndFirst.KingdomQuickstartEmbarkModule"), Is.EqualTo(1));
		}

		[Test]
		public void RuntimeUsesNormalAuthorityPhysicalStoresAndBenefitFreeAdvisor()
		{
			string bootstrap = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.cs")
				+ TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Stock.cs")
				+ TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Materials.cs")
				+ TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.StockVerification.cs")
				+ TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Advisor.cs")
				+ TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Recovery.cs")
				+ TestMain.ReadRepositoryText(
					"World/KingdomQuickstartBootstrap.Verification.cs");
			StringAssert.Contains("KingdomFoundingTransaction.TryFoundFirstWithoutWater", bootstrap);
			StringAssert.Contains("KingdomLiquids.Fill", bootstrap);
			StringAssert.Contains("KingdomOrdinaryFoodAuthority.IsEdible", bootstrap);
			StringAssert.Contains("KingdomMaterials.BlueprintFor", bootstrap);
			StringAssert.Contains("KingdomMaterials.TryOrdinaryMaterialOf", bootstrap);
			StringAssert.Contains("TryPrepareGrant", bootstrap);
			StringAssert.Contains("TryPlaceGrant", bootstrap);
			StringAssert.Contains("GrantMarkerProperty", bootstrap);
			StringAssert.Contains("KingdomCitizen\") != 0", bootstrap);
			StringAssert.Contains("KingdomStaffNeeded\") != 0", bootstrap);
			StringAssert.Contains("KingdomDefence\") != 0", bootstrap);
			StringAssert.Contains("brain.Passive = true", bootstrap);
			StringAssert.Contains("brain.Mobile = false", bootstrap);
			StringAssert.Contains("advisor.RequirePart<NoXPGain>()", bootstrap);
			StringAssert.DoesNotContain("SetIntProperty(\"KingdomBuilt\", 1", bootstrap);
			string camp = TestMain.ReadRepositoryText("World/KingdomQuickstartCampBuilder.cs");
			StringAssert.Contains("KingdomPlots.ReadObject", camp);
			StringAssert.Contains("SystemLongDistanceMoveTo", camp);
			StringAssert.Contains("if (!Required(x, y)) continue", camp);
			StringAssert.Contains("return KingdomQuickstartRules.RequiresPreparedGround(X, Y);", camp);
			StringAssert.Contains("if (Required(x, y)) continue;", camp);
			StringAssert.DoesNotContain("ClearAll", camp);
		}

		[Test]
		public void SourceContractUsesPlacedFounderReadinessBeforeReceiptPublication()
		{
			string source = TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.cs");
			StringAssert.Contains("GameObject founder = The.Player;", source);
			int readiness = source.IndexOf("KingdomQuickstartCampBuilder.ReadyForFounder(zone, founder, out string groundFailure)",
				StringComparison.Ordinal);
			Assert.That(readiness, Is.GreaterThanOrEqualTo(0));
			Assert.That(readiness, Is.LessThan(source.IndexOf(
				"KingdomQuickstartRules.TryCreateReceipt", StringComparison.Ordinal)));
			StringAssert.DoesNotContain("KingdomQuickstartCampBuilder.Ready(zone)", source);
			string camp = TestMain.ReadRepositoryText("World/KingdomQuickstartCampBuilder.cs");
			StringAssert.Contains("return Ready(Z);", camp);
			StringAssert.Contains("ReferenceEquals(item, Founder)", camp);
			StringAssert.Contains("out string Failure", camp);
			StringAssert.Contains("object fails engine validation", camp);
			StringAssert.Contains("Object?.Blueprint", camp);
			StringAssert.Contains("Object?.Physics != null", camp);
			StringAssert.Contains("Object.IsInGraveyard()", camp);
			StringAssert.Contains("+ groundFailure", source);
		}

		[Test]
		public void SourceContractPreparesCompletedZoneBeforePlayerPlacement()
		{
			string source = TestMain.ReadRepositoryText("World/KingdomQuickstartEmbarkModule.cs");
			StringAssert.DoesNotContain("AddZonePostBuilder", source);
			StringAssert.Contains("BOOTEVENT_BOOTSTARTINGLOCATION", source);
			StringAssert.Contains("BindCampBuilder(game, info, element as GlobalLocation)", source);
			StringAssert.Contains("BOOTEVENT_AFTERBOOTPLAYEROBJECT", source);
			StringAssert.Contains("PrepareCamp(game, info, element as GameObject)", source);
			int resolve = source.IndexOf("attempt.Manager.GetZone(attempt.Profile.ZoneId)", StringComparison.Ordinal);
			int prepare = source.IndexOf("new KingdomQuickstartCampBuilder().BuildZone(zone)", StringComparison.Ordinal);
			Assert.That(resolve, Is.GreaterThanOrEqualTo(0));
			Assert.That(prepare, Is.GreaterThan(resolve));
			ClassicAssert.AreEqual(prepare, source.LastIndexOf("new KingdomQuickstartCampBuilder().BuildZone(zone)", StringComparison.Ordinal));
			int reachability = source.IndexOf("zone.BuildReachableMap(", StringComparison.Ordinal);
			Assert.That(reachability, Is.GreaterThan(prepare));
			Assert.That(source.IndexOf("attempt.Prepared = true;", StringComparison.Ordinal), Is.GreaterThan(reachability));
			StringAssert.Contains("KingdomQuickstartRules.StartCellY, bClearFirst: false)", source);
			StringAssert.Contains("!KingdomQuickstartCampBuilder.Ready(zone)", source);
			StringAssert.DoesNotContain("ReachableMap[", source);
			StringAssert.Contains("Zone != null && Zone.Built", source);
			StringAssert.Contains("ReferenceEquals(cached, Zone)", source);
			StringAssert.Contains("Attempt.Player.Body == null && The.Player == null", source);
			StringAssert.Contains("attempt.Consumed = true;", source);
			StringAssert.Contains("if (!EnterBootstrap(game, info, out failure)", source);
			StringAssert.DoesNotContain("SetStringGameState", source);
		}

		[Test]
		public void SourceContractRequiresExactFoundedHeartWithoutInventingCompletedRung()
		{
			string source = TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Verification.cs");
			StringAssert.Contains("KingdomPlots.HasExactFoundedHeart", source);
			StringAssert.DoesNotContain("KingdomPlots.HeartRung(Zone) < 1", source);
			StringAssert.DoesNotContain("SetZoneProperty", source);
			StringAssert.DoesNotContain("RecoverFoundingHeart", source);
			string seal = TestMain.ReadRepositoryText("Growth/KingdomPlot2.07h.FoundingHeartSeal.cs");
			StringAssert.Contains("TryReadFoundingHeartWorkAuthority(Z, works, out _, RawDisplayName: true)", seal);
			string stake = TestMain.ReadRepositoryText("Growth/KingdomPlot2.07f.FoundingHeartStakeTruth.cs");
			StringAssert.Contains("RawDisplayName ? Works.Render?.DisplayName : Works.DisplayName", stake);
		}

		[Test]
		public void PlacementPrecedesIdentityPublicationAndLifecycleResumesIncompleteReceipts()
		{
			string bootstrap = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.cs");
			string stock = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Stock.cs")
				+ TestMain.ReadRepositoryText("World/KingdomQuickstartBootstrap.Materials.cs");
			string recovery = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Recovery.cs");
			string lifecycle = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartLifecycle.cs");
			string embark = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartEmbarkModule.cs");

			Assert.That(stock.IndexOf("TryPrepareGrant(water", StringComparison.Ordinal),
				Is.LessThan(stock.IndexOf("TryPlaceGrant(Zone, water",
					StringComparison.Ordinal)));
			Assert.That(stock.IndexOf("TryPrepareGrant(larder", StringComparison.Ordinal),
				Is.LessThan(stock.IndexOf("TryPlaceGrant(Zone, larder",
					StringComparison.Ordinal)));
			Assert.That(stock.IndexOf("TryPrepareGrant(stockpile", StringComparison.Ordinal),
				Is.LessThan(stock.IndexOf("TryPlaceGrant(Zone, stockpile",
					StringComparison.Ordinal)));
			StringAssert.Contains("Grant.CurrentCell != null", recovery);
			StringAssert.Contains("PublishExisting", stock);
			StringAssert.Contains("water.IDIfAssigned", bootstrap);
			StringAssert.DoesNotContain("ObliterateExact(water)", bootstrap);
			StringAssert.DoesNotContain("ObliterateExact(larder)", bootstrap);
			StringAssert.DoesNotContain("ObliterateExact(stockpile)", bootstrap);
			StringAssert.Contains("KingdomQuickstartLifecycle : IPlayerSystem", lifecycle);
			StringAssert.Contains("AfterGameLoadedEvent.ID", lifecycle);
			StringAssert.Contains("ZoneActivatedEvent.ID", lifecycle);
			StringAssert.Contains("EndTurnEvent.ID", lifecycle);
			StringAssert.Contains("receipt.Phase == KingdomQuickstartPhase.Complete", lifecycle);
			StringAssert.Contains("RequireSystem<KingdomQuickstartLifecycle>()", embark);
		}

		[Test]
		public void CompletedReceiptChecksIdentityNotConsumableOpeningQuantities()
		{
			string verification = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.Verification.cs");
			string stock = TestMain.ReadRepositoryText(
				"World/KingdomQuickstartBootstrap.StockVerification.cs");
			StringAssert.Contains("Receipt, false, out Failure", verification);
			Assert.That(Count(stock, "if (!InitialQuantity) return true;"), Is.EqualTo(2));
			StringAssert.Contains("if (!InitialQuantity) return true;", stock);
			StringAssert.Contains("(InitialQuantity && (volume.Volume", stock);
			StringAssert.DoesNotContain("VerifyComplete(system, zone, receipt, out Failure)\n"
				+ "\t\t\t\t||", verification);
		}

		[Test]
		public void RemovalPreservesReceiptsAndReinstallDetectsTheirFootprint()
		{
			foreach (string key in StateKeys)
			{
				Assert.That(KingdomRemovalCoverage.IsOwnedGlobalState(key), Is.True, key);
				Assert.That(KingdomRemovalCoverage.GlobalDisposition(key),
					Is.EqualTo(KingdomRemovalGlobalDisposition.Preserve), key);
			}
			string removal = TestMain.ReadRepositoryText(
				"Core/KingdomRemovalProjectionRuntime.Global.cs");
			StringAssert.Contains("disposition == KingdomRemovalGlobalDisposition.Preserve",
				removal);
			string reinstall = TestMain.ReadRepositoryText(
				"Core/KingdomSaveSystemRosterRuntime.FirstInstall.cs");
			StringAssert.Contains("KingdomRemovalCoverage.IsOwnedGlobalState(key)", reinstall);
		}

		[Test]
		public void InstalledQudGroundAndCarrierBlueprintsMatchTheContract()
		{
			string root = LocateBase();
			XDocument map = XDocument.Load(Path.Combine(root, "QudWorldMap.rpm"));
			AssertTerrain(map, 8, 22, "TerrainSaltmarsh");
			AssertTerrain(map, 14, 17, "TerrainDesertCanyon");
			AssertTerrain(map, 6, 17, "TerrainSaltdunes");
			Assert.That(File.ReadAllText(Path.Combine(root, "ObjectBlueprints", "Furniture.xml")),
				Does.Contain("<object Name=\"Chest\""));
			Assert.That(File.ReadAllText(Path.Combine(root, "ObjectBlueprints", "Creatures.xml")),
				Does.Contain("<object Name=\"NPC\""));
			XDocument mod = XDocument.Parse(TestMain.ReadRepositoryText(
				"RuntimeData/ObjectBlueprints.xml"));
			XElement casks = mod.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == "r_KingdomCaskRack");
			Assert.That(casks.Descendants("part").Any(e =>
				(string)e.Attribute("Name") == "LiquidVolume"), Is.True);
			Assert.That(casks.Descendants("part").Any(e =>
				(string)e.Attribute("Name") == "LiquidProducer"), Is.False);
			XElement larder = mod.Descendants("object").Single(e =>
				(string)e.Attribute("Name") == "r_KingdomLarder");
			Assert.That((string)larder.Attribute("Inherits"), Is.EqualTo("Chest"));
		}

		#if !TAF_CONSTRUCTION_INPUT_PORTABLE
		[Test]
		public void QuickstartCannotCompeteWithInheritanceOffer()
		{
			Assert.That(KingdomInheritanceStateRules.ShouldOffer(
				KingdomQuickstartRules.ModeId, false), Is.False);
			Assert.That(KingdomInheritanceStateRules.ShouldOffer("Classic", false), Is.True);
		}
		#endif

		private static KingdomQuickstartReceipt Advance(KingdomQuickstartReceipt current,
			KingdomQuickstartPhase next, string value,
			KingdomQuickstartAdvisorDisposition advisor =
				KingdomQuickstartAdvisorDisposition.Unresolved)
		{
			Assert.That(KingdomQuickstartRules.TryAdvance(current, next, value, advisor,
				out KingdomQuickstartReceipt advanced), Is.True, next.ToString());
			return advanced;
		}

		private static int Count(string text, string fragment)
		{
			return text.Split(new[] { fragment }, StringSplitOptions.None).Length - 1;
		}

		private static void AssertProfile(string key, string location, string zone,
			string city, string terrain, int x, int y)
		{
			Assert.That(KingdomQuickstartRules.TryProfile(key,
				out KingdomQuickstartProfile profile), Is.True, key);
			Assert.That(profile.LocationId, Is.EqualTo(location));
			Assert.That(profile.ZoneId, Is.EqualTo(zone));
			Assert.That(profile.CityName, Is.EqualTo(city));
			Assert.That(profile.TerrainFamily, Is.EqualTo(terrain));
			Assert.That(profile.WorldX, Is.EqualTo(x));
			Assert.That(profile.WorldY, Is.EqualTo(y));
			Assert.That(KingdomQuickstartRules.TryProfileForLocation(location, out var byLocation),
				Is.True);
			Assert.That(byLocation, Is.SameAs(profile));
		}

		private static void AssertTerrain(XDocument map, int x, int y, string expected)
		{
			XElement cell = map.Root.Elements("cell").Single(e =>
				(int)e.Attribute("X") == x && (int)e.Attribute("Y") == y);
			Assert.That((string)cell.Element("object").Attribute("Name"),
				Is.EqualTo(expected));
		}

		private static string LocateBase()
		{
			string supplied = Environment.GetEnvironmentVariable("TAF_QUD_BASE");
			if (!string.IsNullOrWhiteSpace(supplied)
				&& File.Exists(Path.Combine(supplied, "QudWorldMap.rpm"))) return supplied;
			if (supplied != null)
				throw new InvalidOperationException("TAF_QUD_BASE lacks QudWorldMap.rpm: " + supplied);
			foreach (string candidate in new[]
			{
				@"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ_Data\StreamingAssets\Base",
				"/mnt/f/SteamLibrary/steamapps/common/Caves of Qud/CoQ_Data/StreamingAssets/Base"
			}) if (File.Exists(Path.Combine(candidate, "QudWorldMap.rpm"))) return candidate;
			Assert.Ignore("Quickstart native test requires TAF_QUD_BASE or the configured Qud base.");
			return null;
		}
	}
}
#endif
