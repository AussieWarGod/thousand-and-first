#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Source and value contracts for the heart's fifth rung (issue #160). These prove what a pin
	/// can prove -- that the seam drives nothing, that the arcology's extra gates are actually
	/// asked, that the composite bill is priced as production prices it, and that the sealed
	/// five-rung script is a strict extension of the accepted four-rung one. They are NOT native
	/// evidence: the persona camp-heart-rung5-native-check owes a real run.
	/// </summary>
	public class KingdomCampHeartRung5NativeSourceTests
	{
		private const string Script = "Harness/KingdomCampHeartChainScript.cs";
		private const string Chain = "Harness/KingdomCampHeartChain.cs";
		private const string Capital = "Harness/KingdomCampHeartChainCapital.cs";
		private const string HighCraft = "Harness/KingdomCampHeartChainHighCraft.cs";
		private const string HighCraftFactory = "Harness/KingdomCampHeartChainHighCraftFactory.cs";
		private const string Rung5 = "Harness/KingdomCampHeartChainRung5.cs";
		private const string Payment = "Harness/KingdomCampHeartChainPayment.cs";
		private const string Support = "Harness/KingdomCampHeartChainSupport.cs";
		private const string Crown = "Harness/KingdomCampHeartChainCrown.cs";
		private const string Input = "Harness/KingdomCampHeartChainInput.cs";
		private const string Observation = "Harness/KingdomCampHeartChainObservation.cs";
		private const string Trace = "Harness/KingdomCampHeartChainTrace.cs";
		private const string Handover = "Harness/KingdomCampHeartChainHandoverOccupancy.cs";
		private const string Persona = "Tools/personas/camp-heart-rung5-native-check.persona";
		private const string ChainPersona = "Tools/personas/camp-heart-chain.persona";

		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		private static string[] Steps(string persona)
		{
			foreach (string line in Read(persona).Split('\n'))
				if (line.StartsWith("SCRIPT="))
					return line.Substring("SCRIPT=".Length).Trim().Split(';');
			return new string[0];
		}

		[Test]
		public void FiveRungScriptIsAStrictExtensionOfTheAcceptedFourRungScript()
		{
			string[] four = Steps(ChainPersona), five = Steps(Persona);
			Assert.That(four, Is.Not.Empty);
			Assert.That(five.Length, Is.GreaterThan(four.Length));
			Assert.That(five.Take(four.Length), Is.EqualTo(four));
			Assert.That(KingdomCampHeartChainScript.Matches(four), Is.True);
			Assert.That(KingdomCampHeartChainScript.Matches(five), Is.False);
			Assert.That(KingdomCampHeartChainScript.MatchesRung5(five), Is.True);
			Assert.That(KingdomCampHeartChainScript.MatchesRung5(four), Is.False);
			Assert.That(KingdomCampHeartChainScript.SealedTargetRung(four), Is.EqualTo(4));
			Assert.That(KingdomCampHeartChainScript.SealedTargetRung(five), Is.EqualTo(5));
			Assert.That(KingdomCampHeartChainScript.SealedTargetRung(null), Is.EqualTo(0));
			// The four-rung form keeps admitting the camp prefix, and neither form is a save form.
			Assert.That(KingdomCampHeartScript.Matches(five), Is.True);
			Assert.That(KingdomCampHeartScript.Matches(five, true), Is.False);
		}

		[Test]
		public void EveryEditedOrMissingStepBreaksTheFiveRungSeal()
		{
			string[] five = Steps(Persona);
			for (int i = 0; i < five.Length; i++)
			{
				var changed = (string[])five.Clone();
				changed[i] += " ";
				Assert.That(KingdomCampHeartChainScript.MatchesRung5(changed), Is.False);
				Assert.That(KingdomCampHeartChainScript.SealedTargetRung(changed), Is.EqualTo(0));
				Assert.That(KingdomCampHeartChainScript.MatchesRung5(
					five.Where((_, at) => at != i).ToArray()), Is.False);
			}
			Assert.That(KingdomCampHeartChainScript.MatchesRung5(
				five.Concat(new[] { "camp-heart-save" }).ToArray()), Is.False);
		}

		[Test]
		public void TheFifthLegRequestsAFiniteDisclosedTurnBudget()
		{
			string[] five = Steps(Persona);
			var waits = five.Where(x => x.StartsWith("advance ")).Select(x => int.Parse(x.Substring(8)));
			Assert.That(waits.Sum(), Is.EqualTo(60000));
			Assert.That(waits.All(x => x <= 10000), Is.True);
			Assert.That(five.Count(x => x == "camp-heart-chain-capital"), Is.EqualTo(1));
			Assert.That(five.Count(x => x == "camp-heart-chain-supply"), Is.EqualTo(3));
		}

		[Test]
		public void TheCapitalSeedAsksEveryGateTheArcologyAddsAndWritesNoCrown()
		{
			string capital = Read(Capital), crown = Read(Crown);
			foreach (string read in new[] {
				"KingdomZoning.Learn(System, KingdomZoningRules.KindNode, ArclightNode)",
				"KingdomZoning.Tech(System) == TechLevel.Arclight",
				"KingdomFounding.ClaimZone(neighbour)",
				"KingdomFounding.ZonesAdjacent(Zone.ZoneID, neighbour.ZoneID)",
				"System.ClaimedZones.Count >= ArcologyZones",
				"KingdomCrown.Enabled",
				"KingdomCrown.CrownedOn(System, Zone.ZoneID)",
				"KingdomHostedArcology.CanReserveAt(System, Zone.ZoneID, out string failure)",
				"verdict.Verdict == ZoningVerdict.Permitted",
				"KingdomPlots.Stake(System, Ground, rect, Entry, Spec, grid," })
				Assert.That(capital, Does.Contain(read), read);
			// The crown is READ, never written, and the seam drives no improvement and no turns.
			foreach (string source in new[] { capital, crown })
				foreach (string forbidden in new[] { "RegisterStateKey", "KingdomCrown.TakeUp",
					"KingdomUpgrade.Begin", "KingdomArchitectureStamper.TryApplyUpgrade",
					"SetStringGameState", "KingdomHostedArcology.TryReserve" })
					Assert.That(source, Does.Not.Contain(forbidden), forbidden);
			Assert.That(capital, Does.Contain("the capital seed advanced the real clock"));
		}

		/// <summary>Review of #264: the crown resolves from the city book plus the ACTIVE zone's
		/// survey only, and the hall stands on a neighbour the seed never enters in zero turns, so
		/// the seed must make that zone's own production read before any crown gate - and the gate
		/// must ask the book before it asks whether the ground is crowned.</summary>
		[Test]
		public void TheCrownHallEntersTheBookByItsZonesOwnReadBeforeAnyCrownGate()
		{
			string capital = Read(Capital), crown = Read(Crown);
			int seed = capital.IndexOf("private void SeedChainCapital()", StringComparison.Ordinal);
			int last = -1;
			foreach (string step in new[] { "TeachArclightCraft();", "TeachArclightNode();",
				"ClaimChainTerritory();", "RaiseChainCrownHall();", "PublishChainCrownZone();",
				"ChainCapitalReport = DescribeChainCapital();", "RequireChainCapitalGates();" })
			{
				int at = capital.IndexOf(step, seed, StringComparison.Ordinal);
				Assert.That(at, Is.GreaterThan(last), step);
				last = at;
			}
			int publish = crown.IndexOf("private void PublishChainCrownZone()", StringComparison.Ordinal);
			last = publish;
			foreach (string step in new[] { "ChainCrownedBefore = KingdomCrown.CrownedOn(System, Zone.ZoneID);",
				"ChainCrownBookBefore = ChainCrownBookHolds();", "KingdomCity.OnSuspending(System, ground);",
				"KingdomCrown.ClearCache();", "ChainCrownBookAfter = ChainCrownBookHolds();" })
			{
				int at = crown.IndexOf(step, publish, StringComparison.Ordinal);
				Assert.That(at, Is.GreaterThan(last), step);
				last = at;
			}
			Assert.That(crown, Does.Contain("KingdomCampHeartChainRules.CrownBookHolds(book.WorkIds,"));
			Assert.That(crown, Does.Contain("!ReferenceEquals(ground, Zone)"));
			int gates = capital.IndexOf("private void RequireChainCapitalGates()", StringComparison.Ordinal);
			int book = capital.IndexOf("Require(ChainCrownBookAfter,", gates, StringComparison.Ordinal);
			Assert.That(book, Is.GreaterThan(gates));
			Assert.That(capital.IndexOf("Require(KingdomCrown.CrownedOn(System, Zone.ZoneID),", gates,
				StringComparison.Ordinal), Is.GreaterThan(book));
			foreach (string field in new[] { "\"; crowned-before=\"", "\"; book-before=\"", "\"; book-after=\"",
				"\"; synthetic-book-read=true; crown-cache-cleared=true\"" })
				Assert.That(capital, Does.Contain(field), field);
		}

		/// <summary>Review of #264: the read the seed makes is production's own path, and every fact
		/// the fix relies on is pinned where production states it - the suspend handler calls
		/// OnSuspending, which checks out only a claimed zone from a fresh survey; check-out
		/// rebuilds that zone's work rows (blueprint column, other zones' rows kept) and publishes
		/// the book; the crown reads the seat's book and surveys only the active zone; and the
		/// crown's answer is cached per tick, which is why the seed clears it.</summary>
		[Test]
		public void TheCrownBookReadIsProductionsOwnSuspendTimeCheckOut()
		{
			string checkOut = Read("Simulation/City/KingdomCity.z02.CheckOut.cs");
			int suspending = checkOut.IndexOf("public static void OnSuspending(KingdomSystem System, Zone Z)",
				StringComparison.Ordinal);
			Assert.That(suspending, Is.GreaterThan(0));
			Assert.That(checkOut.IndexOf("!System.ClaimedZones.Contains(Z.ZoneID)", suspending, StringComparison.Ordinal),
				Is.GreaterThan(suspending));
			Assert.That(checkOut, Does.Contain(
				"CheckOut(System, Z, KingdomSurvey.Take(Z, System), (The.Game != null) ? The.Game.TimeTicks : 0L);"));
			int works = checkOut.IndexOf("written = ReadWorks(written, Z, Survey);", StringComparison.Ordinal);
			Assert.That(works, Is.GreaterThan(0));
			Assert.That(checkOut.IndexOf("Publish(System, written);", works, StringComparison.Ordinal),
				Is.GreaterThan(works));
			string read = Read("Simulation/City/KingdomCity.z09.WorksAndAudit.cs");
			Assert.That(read, Does.Contain("!string.Equals(row.ZoneId, Z.ZoneID, StringComparison.Ordinal)"));
			Assert.That(read, Does.Contain("work.Blueprint ?? \"\","));
			Assert.That(Read("Core/KingdomSystem.z20.Events.cs"), Does.Contain(
				"Simulation.City.KingdomCity.OnSuspending(this, E.Zone);"));
			string discovery = Read("Growth/KingdomCrownDiscovery.cs");
			Assert.That(discovery, Does.Contain("AddIfKeeping(found, System.SeatName, System.City, blueprint);"));
			Assert.That(discovery, Does.Contain("KingdomSurvey.ActiveFor(Active)"));
			string crown = Read("Growth/KingdomCrown.cs");
			Assert.That(crown, Does.Contain("if (CacheTick == now && string.Equals(CacheZone, here))"));
			Assert.That(crown, Does.Contain("internal static void ClearCache()"));
		}

		/// <summary>Review of #264: the five-rung form refuses an impossible territory at chain
		/// setup from what is known WITHOUT building a zone, never after the 1->4 prefix.</summary>
		[Test]
		public void TheFiveRungFormReadsItsTerritoryAtSetupWithoutBuildingAZone()
		{
			string crown = Read(Crown), chain = Read(Chain);
			int setup = chain.IndexOf("if (Verb == KingdomCampHeartChainScript.Setup)", StringComparison.Ordinal);
			int preflight = chain.IndexOf("if (ChainFinalRung == 5) PreflightChainTerritory();", setup,
				StringComparison.Ordinal);
			Assert.That(preflight, Is.GreaterThan(setup));
			Assert.That(chain.IndexOf("SeedChainSupport();", setup, StringComparison.Ordinal),
				Is.GreaterThan(preflight));
			foreach (string read in new[] { "Zone.GetZoneIDFromDirection(direction)",
				"KingdomFounding.ZonesAdjacent(Zone.ZoneID, id)", "System.FindNonSeatSettlementByZone(id)",
				"System.ExiledRealmHolds(id)", "The.ZoneManager.GetZoneProperty(id, \"faction\")",
				"taf-camp-rung5-territory-infeasible" })
				Assert.That(crown, Does.Contain(read), read);
			// GetZoneFromDirection builds the zone; the preflight must never call it.
			Assert.That(crown, Does.Not.Contain("GetZoneFromDirection("));
			Assert.That(crown, Does.Not.Contain("ClaimZone("));
		}

		/// <summary>Review of #264: a neighbour the city already holds is still crown-hall ground
		/// (after the seed's own claims, and without building any zone for siting alone), and the
		/// founder check names a lost cell instead of dereferencing it.</summary>
		[Test]
		public void AlreadyHeldNeighboursStaySitingGroundAndTheFounderCheckIsNullSafe()
		{
			string capital = Read(Capital);
			Assert.That(capital, Does.Contain(
				"{ ChainClaimNotes.Add(direction + \":already-held\"); ChainHeld.Add(neighbour); continue; }"));
			Assert.That(capital, Does.Contain("var grounds = new List<Zone>(ChainClaimed);"));
			Assert.That(capital, Does.Contain("grounds.AddRange(ChainHeld);"));
			Assert.That(capital, Does.Contain("foreach (Zone ground in grounds)"));
			Assert.That(capital, Does.Contain("(player.CurrentCell != null"));
			Assert.That(capital, Does.Contain("ReferenceEquals(The.ZoneManager?.ActiveZone, Zone) && player != null"));
		}

		[Test]
		public void TheArcologyBillIsPricedAsACompositeAndNothingBelowItIs()
		{
			string payment = Read(Payment);
			Assert.That(payment, Does.Contain("Target == 5 ? KingdomMaterials.BitCostFor(ChainTo) : null"));
			Assert.That(payment, Does.Contain("Target == 5 ? KingdomMaterials.ExoticCostFor(ChainTo) : null"));
			Assert.That(payment, Does.Contain("if (Target == 5) SupplyChainHighCraft();"));
			Assert.That(payment, Does.Contain("Target == 4 ? 121 : 133"));
			Assert.That(payment, Does.Contain("ChainWater = Target == 3 ? 28 : Target == 4 ? 50 : 94;"));
			Assert.That(payment, Does.Contain("ChainTarget == 4 ? \"512\" : \"1024\""));
		}

		/// <summary>Review of #264: the paid check compares the job against the ONE cost the supply
		/// step built - composite at the fifth rung - and never against the plain tally; whole bit
		/// bodies are judged by the fall in production's own bit tally. The predicates themselves
		/// execute in KingdomQuickstartBuildClaimsTests.</summary>
		[Test]
		public void ThePaidCheckJudgesTheOneCompositeCostTheSupplyStepBuilt()
		{
			string payment = Read(Payment), craft = Read(HighCraft);
			foreach (string pin in new[] {
				"ChainSupplyCost = new KingdomMaterialDebitCost(tally,",
				"ChainSupplyClaim = ChainSupplyCost.ToClaimString();",
				"return KingdomQuickstartBuildClaims.CleanFirstPayment(Claims, ChainWater, ChainSupplyCost);",
				"&& KingdomQuickstartBuildClaims.CleanFirstCompositePayment(Claims, ChainWater, ChainSupplyCost, lost);",
				"if (!stock.InputLeaseAuthorityExact) return false;",
				"var after = stock.Bits;",
				"!ChainPaidExactly(found.Claims)" })
				Assert.That(payment, Does.Contain(pin), pin);
			Assert.That(payment, Does.Not.Contain("new KingdomMaterialDebitCost(KingdomMaterials.UpgradeCostFor(ChainFrom))"));
			int mint = craft.IndexOf("KingdomCampHeartHighCraftRules.Fill(bits,", StringComparison.Ordinal);
			int before = craft.IndexOf("ChainBitsBefore = stock.Bits.Copy();", StringComparison.Ordinal);
			Assert.That(before, Is.GreaterThan(mint));
			Assert.That(craft.IndexOf("\"camp-heart-chain-exotics\"", StringComparison.Ordinal),
				Is.GreaterThan(before));
		}

		/// <summary>Review of #264: the post-payment resident probe arms with the exact design the
		/// paid job's handover carries, so the arcology's renovation handover is watched as
		/// "arcology" and not as the great court. The keys execute in KingdomCampHeartChainRulesTests.</summary>
		[Test]
		public void TheHandoverProbeArmsWithThePaidRungsOwnSuccessorKey()
		{
			string payment = Read(Payment), probe = Read(Handover);
			Assert.That(payment, Does.Contain("ChainFrom = KingdomCampHeartChainRules.PredecessorKey(Target);"));
			Assert.That(payment, Does.Contain("ChainTo = KingdomCampHeartChainRules.SuccessorKey(Target);"));
			Assert.That(payment, Does.Contain(
				"KingdomCampHeartChainHandoverOccupancy.Arm(FixtureResidents[0], ChainJobId, ChainTo, ChainTarget >= 4);"));
			Assert.That(probe, Does.Contain("Key == Successor"));
			Assert.That(probe, Does.Contain("Successor = SuccessorKey;"));
			foreach (string literal in new[] { "\"heartmoot\"", "\"heartcourt\"", "\"arcology\"" })
				Assert.That(probe, Does.Not.Contain(literal), literal);
			Assert.That(KingdomCampHeartChainRules.SuccessorKey(5), Is.EqualTo("arcology"));
		}

		[Test]
		public void HighCraftIsClassifiedByProductionAndNeverByAHandWrittenTable()
		{
			string craft = Read(HighCraft), factory = Read(HighCraftFactory);
			foreach (string reader in new[] {
				"KingdomMaterials.TryExoticOf(unit, out var read)",
				"KingdomMaterialRules.CoversExotics(stock.Exotics, exotics)",
				"KingdomMaterialRules.CoversBits(stock.Bits, bits)",
				"string refused = KingdomCampHeartHighCraftRules.Fill(bits, ReadChainBits, ResolveChainBit," })
				Assert.That(craft, Does.Contain(reader), reader);
			foreach (string table in new[] {
				"XRL.World.Parts.TinkerItem.GetBitCostFor(Candidate.Key)",
				"new KingdomCampHeartHighCraftRules.Vocabulary(KingdomMaterials.MaterialTag,",
				"KingdomMaterials.ExoticTag, HighCraftScrapTag, KingdomMaterials.MaterialBlueprints, exotics);",
				"foreach (string[] row in KingdomMaterials.ExoticBlueprints) exotics.AddRange(row);",
				"internal const string HighCraftScrapTag = \"SemanticScrap\";" })
				Assert.That(factory, Does.Contain(table), table);
			// The scrap tag is production's own literal, read beside its own material tag.
			Assert.That(Read("Growth/KingdomMaterials.03.StockClassification.cs"),
				Does.Contain("Object.HasTag(\"SemanticScrap\")"));
			Assert.That(craft + factory, Does.Not.Contain("SetIntProperty(\"KingdomStockpile\""));
		}

		/// <summary>Fix rounds of #264: the bit mint's decisions execute in
		/// KingdomCampHeartHighCraftRulesTests, KingdomCampHeartHighCraftFillTests and, against the
		/// installed corpus, KingdomCampHeartHighCraftCorpusTests. This pins only the native wiring:
		/// every created body is read by production's and the engine's own readers before anything
		/// stores it, a refused body is discarded, the stock is production's own reading with its
		/// routed-input authority, and a body production did not count leaves the store.</summary>
		[Test]
		public void TheBitMintJudgesEachBodyWithProductionsReadersBeforeStoringIt()
		{
			string craft = Read(HighCraft);
			int offer = craft.IndexOf("private string OfferChainBit(string Key, int Tier, out KingdomBitTally Unit)",
				StringComparison.Ordinal);
			int judged = craft.IndexOf("string refusal = KingdomCampHeartHighCraftRules.BodyRefusal(reading, Tier);",
				offer, StringComparison.Ordinal);
			int stored = craft.IndexOf("StoreHighCraft(body);", offer, StringComparison.Ordinal);
			Assert.That(offer, Is.GreaterThan(0));
			foreach (string reader in new[] { "GameObject body = GameObject.Create(Key);",
				"Exact = body.Blueprint == Key && body.Count == 1 && body.CurrentCell == null",
				"Takeable = body.IsTakeable(),", "Important = body.IsImportant(),",
				"Empty = KingdomOrdinaryCustody.TryProveEmpty(body, out _),", "Natural = body.IsNatural(),",
				"Creature = body.IsCreature,", "AlwaysStack = body.HasTag(\"AlwaysStack\"),",
				"Material = KingdomMaterials.TryOrdinaryMaterialOf(body, out _),",
				"Exotic = KingdomMaterials.TryExoticOf(body, out _),", "Unit = KingdomMaterials.UnitBits(body)" })
			{
				int at = craft.IndexOf(reader, offer, StringComparison.Ordinal);
				Assert.That(at, Is.GreaterThan(offer), reader);
				Assert.That(at, Is.LessThan(judged), reader + " is read before the body is judged");
			}
			Assert.That(stored, Is.GreaterThan(judged));
			Assert.That(craft.IndexOf("DiscardHighCraft(body);", judged, StringComparison.Ordinal),
				Is.LessThan(stored), "a refused body is discarded, never stored");
			foreach (string pin in new[] { "var stock = KingdomMaterials.Stock(Zone);",
				"return stock.InputLeaseAuthorityExact ? stock.Bits.Copy() : null;",
				"Require(!ChainStore.Inventory.Objects.Contains(body),", "Body.Obliterate(null, Silent: true);",
				".Append(\"; skipped=\").Append(KingdomScenarioRules.Bounded(string.Join(\",\", book.Notes)))",
				".Append(\"; bit-bodies=\")" })
				Assert.That(craft, Does.Contain(pin), pin);
			// The production readers the body reading mirrors, where production states them.
			string authority = Read("Growth/KingdomConstructionInputLeaseAuthority.cs");
			foreach (string fact in new[] { "&& KingdomOrdinaryCustody.TryProveEmpty(item, out _)",
				"&& !item.IsImportant() && item.Equipped == null && item.IsTakeable();" })
				Assert.That(authority, Does.Contain(fact), fact);
			Assert.That(Read("Growth/KingdomConstruction.InputObservationRegistry.cs"),
				Does.Contain("|| !item.IsTakeable() || item.HasTag(\"AlwaysStack\")"));
		}

		/// <summary>Fix round of #264: the five-rung form resolves every tier of the arcology's
		/// bits from declared data at chain setup and creates nothing, so an unsourced tier refuses
		/// before the 1->4 prefix rather than after it. The resolution itself executes against the
		/// installed corpus in KingdomCampHeartHighCraftCorpusTests.</summary>
		[Test]
		public void TheFiveRungFormResolvesItsBitTiersAtSetupWithoutCreatingABody()
		{
			string chain = Read(Chain), factory = Read(HighCraftFactory);
			int setup = chain.IndexOf("if (Verb == KingdomCampHeartChainScript.Setup)", StringComparison.Ordinal);
			int preflight = chain.IndexOf("if (ChainFinalRung == 5) PreflightChainHighCraft();", setup,
				StringComparison.Ordinal);
			Assert.That(preflight, Is.GreaterThan(setup));
			Assert.That(chain.IndexOf("SeedChainSupport();", setup, StringComparison.Ordinal), Is.GreaterThan(preflight));
			Assert.That(chain, Does.Contain("+ (ChainHighCraftPreflight == null ? \"\" : ChainHighCraftPreflight + \"; \")"));
			int method = factory.IndexOf("private void PreflightChainHighCraft()", StringComparison.Ordinal);
			Assert.That(method, Is.GreaterThan(0));
			string body = factory.Substring(method);
			foreach (string read in new[] { "KingdomCampHeartChainRules.SuccessorKey(5)",
				"KingdomMaterials.BitCostFor(arcology)", "vocabulary, KingdomCampHeartHighCraftRules.DeclaredWorth);",
				"taf-camp-rung5-highcraft-infeasible", "GameObjectFactory.Factory.Blueprints.ContainsKey(IngotBlueprint)" })
				Assert.That(body, Does.Contain(read), read);
			Assert.That(body, Does.Not.Contain("RealisedBitWorth"), "the preflight asks the engine for no realised cost");
			Assert.That(factory, Does.Not.Contain("GameObject.Create("), "the factory shard creates nothing");
		}

		[Test]
		public void TheStandingCheckProvesARenovationAndARetiredCourt()
		{
			string rung5 = Read(Rung5);
			foreach (string proof in new[] {
				"KingdomPlots.HeartRung(Zone) == 5",
				"KingdomUpgrade.DesignKeyOf(standing) == KingdomHostedArcology.ArcologyKey",
				"taf-camp-rung5-footprint-grew",
				"taf-camp-rung5-lot-grew",
				"KingdomArchitectureStamper.TryVerifyComplete(standing, Zone,",
				"XRL.World.Parts.r_KingdomScaffold.HasRemovalProof(Standing, job.SubjectId)",
				"KingdomPlots.TryChainedWorkSuccessor(Zone,",
				"KingdomConstruction.FindGlobalLiveId(ChainHeartId, out var court)",
				"row.Phase == KingdomHostedAuthorityPhase.Active",
				"taf-camp-rung5-hosted-lot-started" })
				Assert.That(rung5, Does.Contain(proof), proof);
			// Absence is consulted LAST: a court merely gone must still refuse as unproved.
			Assert.That(rung5.IndexOf("HasRemovalProof"),
				Is.LessThan(rung5.IndexOf("FindGlobalLiveId")));
			Assert.That(rung5.IndexOf("TryChainedWorkSuccessor"),
				Is.LessThan(rung5.IndexOf("FindGlobalLiveId")));
			// The renovate delta strikes the rostrum, the benches and both founder statues by
			// design, so no rung-4 fixture may be asserted by identity except the two protected
			// placements the tier keeps.
			foreach (string stale in new[] { "r_KingdomGreatCourtRostrum", "r_KingdomFounderStatue",
				"r_KingdomCivicTorchpost", "r_KingdomFixtureBenchTimber" })
				Assert.That(rung5, Does.Not.Contain(stale), stale);
		}

		/// <summary>Review of #264: every sealed form's setup seeds foundry exactly as the accepted
		/// four-rung run did, so the 1->2->3->4 prefix keeps its inputs, and only the capital seed
		/// raises the craft to arclight, after rung four.</summary>
		[Test]
		public void ThePrefixSeedsFoundryAndOnlyTheCapitalSeedRaisesArclight()
		{
			string support = Read(Support), crown = Read(Crown), chain = Read(Chain);
			Assert.That(support, Does.Contain(
				"for (int i = 0; i < KingdomZoningRules.PointsForLevel(TechLevel.Foundry); i++)"));
			Assert.That(support, Does.Contain(
				"Require(KingdomZoning.Tech(System) == TechLevel.Foundry, \"synthetic lessons did not reach foundry craft\");"));
			Assert.That(support, Does.Not.Contain("Arclight"));
			Assert.That(support, Does.Not.Contain("ChainFinalRung"));
			Assert.That(crown, Does.Contain("for (int i = KingdomZoningRules.PointsForLevel(TechLevel.Foundry);"));
			Assert.That(crown, Does.Contain("i < KingdomZoningRules.PointsForLevel(TechLevel.Arclight); i++)"));
			foreach (string source in new[] { support, crown })
				Assert.That(source, Does.Contain("KingdomZoning.Learn(System, \"disk\", \"paid-heart-chain-fixture-\" + i)"));
			Assert.That(chain, Does.Contain("\"; craft=\" + KingdomZoning.Tech(System)"));
			Assert.That(chain, Does.Contain(
				"ChainFinalRung = KingdomCampHeartChainScript.SealedTargetRung(sealedScript);"));
			Assert.That(chain, Does.Contain("Require(ChainPhase == 8 && ChainFinalRung == 5,"));
			Assert.That(chain, Does.Contain("Require(ChainPhase == 12 && ChainFinalRung == 5,"));
			// The four-rung persona's terminal line is unchanged.
			Assert.That(chain, Does.Contain("\"paid-heart-chain complete; paid-rungs=1->2->3->4;"));
			Assert.That(Read(Payment), Does.Contain(
				"Require(Target <= ChainFinalRung, \"the sealed script does not drive this target rung\");"));
		}

		/// <summary>The lessons' arithmetic, executed: nine setup lessons are foundry exactly, all
		/// five capital lessons are needed for arclight, and the node key adds no craft.</summary>
		[Test]
		public void NineLessonsAreFoundryAndFourteenAreArclightWhileTheNodeAddsNothing()
		{
			var roster = new List<string>();
			int foundry = KingdomZoningRules.PointsForLevel(TechLevel.Foundry);
			int arclight = KingdomZoningRules.PointsForLevel(TechLevel.Arclight);
			for (int i = 0; i < foundry; i++)
				roster.Add(KingdomZoningRules.ComposeKey("disk", "paid-heart-chain-fixture-" + i));
			Assert.That(KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(roster)),
				Is.EqualTo(TechLevel.Foundry));
			for (int i = foundry; i < arclight; i++)
			{
				Assert.That(KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(roster)),
					Is.EqualTo(TechLevel.Foundry), "lesson " + i);
				roster.Add(KingdomZoningRules.ComposeKey("disk", "paid-heart-chain-fixture-" + i));
			}
			int points = KingdomZoningRules.TechPoints(roster);
			Assert.That(KingdomZoningRules.LevelForPoints(points), Is.EqualTo(TechLevel.Arclight));
			// The capital seed's node key (Harness/KingdomCampHeartChainCapital.cs ArclightNode).
			roster.Add(KingdomZoningRules.ComposeKey(KingdomZoningRules.KindNode, "arclight"));
			Assert.That(KingdomZoningRules.TechPoints(roster), Is.EqualTo(points));
		}

		/// <summary>Review of #264: three gates keyed on the four-rung form switched the paid
		/// chain's input isolation, its journal row, its trace and the founder's commission walk
		/// OFF for the sealed five-rung script. No harness shard but the script's own may ask the
		/// four-rung form; every chain consumer asks the sealed rung.</summary>
		[Test]
		public void NoHarnessShardSwitchesPaidChainBehaviourOnTheFourRungFormAlone()
		{
			var offenders = new List<string>();
			int scanned = 0;
			foreach (string path in Directory.EnumerateFiles(Path.Combine(TestMain.RepositoryRoot, "Harness"),
				"*.cs", SearchOption.AllDirectories))
			{
				scanned++;
				if (Path.GetFileName(path) == "KingdomCampHeartChainScript.cs") continue;
				if (File.ReadAllText(path).Contains("KingdomCampHeartChainScript.Matches("))
					offenders.Add(Path.GetFileName(path));
			}
			Assert.That(scanned, Is.GreaterThan(100), "the harness tree was not found");
			Assert.That(offenders, Is.Empty, "four-rung-only chain gates: " + string.Join(", ", offenders));
			Assert.That(Read(Input), Does.Contain(
				"&& Harness.KingdomCampHeartChainScript.SealedTargetRung(script) > 0;"));
			Assert.That(Read(Input), Does.Contain(
				"if (Harness.KingdomCampHeartChainScript.SealedTargetRung(Verbs) == 0) return true;"));
			Assert.That(Read(Observation), Does.Contain(
				"ChainCommission = KingdomCampHeartChainScript.SealedTargetRung(script) > 0;"));
			// The trace, the retry fault and the handover probe are all switched by input ownership.
			Assert.That(Read(Trace), Does.Contain("KingdomScenarioAutoRunner.ChainInputOwner() != null"));
			Assert.That(KingdomCampHeartChainScript.SealedTargetRung(Steps(Persona)), Is.EqualTo(5));
		}

		[Test]
		public void ThePersonaDisclosesItsSyntheticSeedAndItsOwedRun()
		{
			string persona = Read(Persona);
			foreach (string line in new[] { "node:arclight", "FOUR claimed zones", "THE CROWN",
				"COMPOSITE bill", "SAME-FOOTPRINT RENOVATION", "BUDGET, DISCLOSED AND MEASURED",
				"no save/load", "# Physical input isolated for this exact dedicated game, through owned shutdown.",
				"KingdomCity.OnSuspending", "five more disk lessons", "WITHOUT building any zone",
				"The bit mint re-reads production's own stock before every body",
				"does not count by exactly its worth is taken back out" })
				Assert.That(persona, Does.Contain(line), line);
			Assert.That(persona, Does.Contain("LOG_FORBID=[\"construction: founding heart recovery requires inspection\""));
			Assert.That(persona, Does.Contain("TIMEOUT=7000"));
		}
	}
}
#endif
