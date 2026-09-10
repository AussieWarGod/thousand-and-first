#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Wiring tripwires only. The persona supplies the actual native evidence; these assert that
	/// the seam is registered, that it drives the REAL adapters through the REAL engine rather
	/// than any substitute, that every case proves its bodies unchanged, and that the synthetic
	/// setup is disclosed in every report line it can produce.
	/// </summary>
	public class KingdomDepositOverflowNativeSourceTests
	{
		private const string Provider = "Harness/KingdomDepositOverflowNativeProvider.cs";
		private const string Checks = "Harness/KingdomDepositOverflowNativeChecks.cs";
		private const string Fixture = "Harness/KingdomDepositOverflowNativeFixture.cs";
		private const string Cases = "Harness/KingdomDepositOverflowNativeCases.cs";
		private const string Persona = "Tools/personas/deposit-overflow-native-check.persona";

		private static string Read(string Path) { return TestMain.ReadRepositoryText(Path); }

		private static int Occurrences(string Source, string Token)
		{
			int found = 0;
			for (int i = Source.IndexOf(Token, System.StringComparison.Ordinal); i >= 0;
				i = Source.IndexOf(Token, i + Token.Length, System.StringComparison.Ordinal))
				found++;
			return found;
		}

		[Test]
		public void ProviderIsRegisteredWithBothVerbsAndTheSealedScript()
		{
			string provider = Read(Provider);
			Assert.That(provider, Does.Contain("[KingdomScenarioVerbProvider]"));
			Assert.That(provider, Does.Contain(
				"internal const string SetupVerb = \"deposit-overflow-setup\";"));
			Assert.That(provider, Does.Contain(
				"internal const string CheckVerb = \"deposit-overflow-check\";"));
			Assert.That(provider, Does.Contain(
				"KingdomDepositOverflowNativeChecks.Run(Verb, game, zone,"));
			Assert.That(provider, Does.Contain("\"stagedigest\", SetupVerb, CheckVerb, CheckVerb,"));
		}

		/// <summary>The whole point of this seam: the REAL hosts, through the REAL engine. A fake
		/// host or a hand-rolled census here would prove nothing the engine-free suites do not
		/// already prove.</summary>
		[Test]
		public void EveryCaseDrivesTheRealAdaptersThroughTheRealEngine()
		{
			string cases = Read(Cases);
			foreach (string token in new[] {
				"KingdomMaterials.StockForExactContainer(Zone, Container)",
				".Put(KingdomMaterial.Brush, 3, null, out custody)",
				".Put(KingdomMaterial.Brush, 1, null, out custody)",
				"KingdomDepositEngine.Fill(",
				"new KingdomMaterials.GroundSpillHost(Zone, PlainGround, Blueprint, 3)",
				"new KingdomMaterials.GroundSpillHost(Zone, BoundaryGround, Blueprint, 2)",
				"new KingdomMaterials.GroundSpillHost(Zone, OverflowGround, Blueprint, 1)" })
				Assert.That(cases, Does.Contain(token), token);
			Assert.That(cases, Does.Not.Contain("FakeStore"));
			Assert.That(cases, Does.Not.Contain("IKingdomDepositHost host ="));
		}

		/// <summary>Ordinary delivery and the representability boundary both still credit, and the
		/// boundary body is proved untouched. The bound is int.MaxValue itself, so exactly that
		/// total must still read.</summary>
		[Test]
		public void TheOrdinaryParcelCreditsAndTheBoundaryStackStillReadsAndStillCredits()
		{
			string cases = Read(Cases);
			foreach (string token in new[] {
				"custody == KingdomDepositCustody.Settled",
				"heldAfter == heldBefore + 3",
				"groundAfter == groundBefore + 3",
				"Boundary = PlaceInCell(BoundaryGround, int.MaxValue)",
				"held == int.MaxValue",
				"outcome.Custody == KingdomDepositCustody.Settled && outcome.Placed == 2",
				"Boundary.RequireUnchanged(\"the int.MaxValue stack\")" })
				Assert.That(cases, Does.Contain(token), token);
		}

		/// <summary>Both hosts refuse a total past int.MaxValue, credit nothing, and leave every
		/// standing body byte-for-byte where it was. Ground's room is a declared bound and must
		/// still read, so the hold reading carries that path alone.</summary>
		[Test]
		public void BothHostsRefuseATotalPastIntMaxValueAndDisturbNothing()
		{
			string cases = Read(Cases);
			foreach (string token in new[] {
				"StoreStacks = new[] { first, PlaceInStore(1200000000) }",
				"counted == baseline + 1200000000",
				"!KingdomMaterials.TryDepositRawRoomNow(Container, out unread)",
				"!KingdomMaterials.TryDepositMaterialHeldNow(Container, Blueprint,",
				"custody == KingdomDepositCustody.Unproved",
				"StoreRows() == rowsBefore",
				"StoreStacks[i].RequireUnchanged(",
				"GroundStacks = new[] { first, PlaceInCell(OverflowGround, 1200000000) }",
				"counted == 1200000000",
				".TryRawRoomNow(out bound) && bound == 1",
				"!KingdomMaterials.TryGroundMaterialHeldNow(OverflowGround, Blueprint,",
				"outcome.Custody == KingdomDepositCustody.Unproved && outcome.Placed == 0",
				"GroundRows(OverflowGround) == rowsBefore",
				"GroundStacks[i].RequireUnchanged(" })
				Assert.That(cases, Does.Contain(token), token);
			// BOTH refused readings must be proved to hand back nothing -- the store's room read
			// and the cell's hold read -- so dropping either one is caught here rather than by
			// the other one's surviving assertion.
			Assert.That(Occurrences(cases, "unread == 0"), Is.EqualTo(2),
				"each refused reading must be proved to hand back nothing");
		}

		/// <summary>A body is proved by identity, raw count and custody, not by a tally.</summary>
		[Test]
		public void AnUnchangedBodyIsProvedByIdentityRawCountAndCustody()
		{
			string checks = Read(Checks);
			// Zone, cell AND holder, all three, always. The same coordinates in another zone are
			// different ground, and a body carried out of a chest into a cell at the chest's own
			// coordinates would read as unmoved if only whichever field happened to be set were
			// recorded.
			foreach (string token in new[] { "Item.IDIfAssigned == Id",
				"Item.Blueprint == Blueprint",
				"KingdomMaterials.RawPhysicalCountOf(Item) == RawCount",
				"ZoneOf(Item) == ZoneId", "CellOf(Item) == CellKey",
				"HolderOf(Item) == HolderId", "GameObject.Validate(Item)" })
				Assert.That(checks, Does.Contain(token), token);
			Assert.That(checks, Does.Contain("Zone own = Item.CurrentZone;"),
				"a body's place must be bound to the zone it is really in");
		}

		/// <summary>Three ground cases need three DISTINCT cells, reserved before any of them is
		/// used. Every candidate is still empty at reservation time, so a plain "first bare cell"
		/// search hands back the same cell every call &mdash; which is exactly the defect this
		/// asserts against.</summary>
		[Test]
		public void TheThreeGroundCasesReserveThreeDistinctCellsBeforeAnyOfThemIsUsed()
		{
			string checks = Read(Checks);
			foreach (string token in new[] {
				"PlainGround = ReserveCell();", "BoundaryGround = ReserveCell();",
				"OverflowGround = ReserveCell();",
				"KingdomDepositOverflowReservation.AllDistinct(Reserved)",
				"Reserved.Count == 3",
				"!ReferenceEquals(PlainGround, BoundaryGround)",
				"!ReferenceEquals(PlainGround, OverflowGround)",
				"!ReferenceEquals(BoundaryGround, OverflowGround)",
				"while (cell != null && Reserved.Contains(KeyOf(cell))) cell = NextBare(cell);",
				"KingdomDepositOverflowReservation.TryReserve(Reserved," })
				Assert.That(checks, Does.Contain(token), token);
		}

		/// <summary>The store is the fixture's own, made and dedicated through the production
		/// check-in rather than stamped, and #111 takes no dependency on another ticket's heart
		/// stockpile to have somewhere to deliver.</summary>
		[Test]
		public void TheStoreIsRealDedicatedThroughTheProductionCheckInAndDisclosedAsSynthetic()
		{
			string fixture = Read(Fixture);
			foreach (string token in new[] { "GameObject.Create(\"Chest\")",
				"KingdomMaterials.DedicateStockpile(System, Zone, chest, out failure)",
				"KingdomMaterials.IsStockpile(chest)",
				"KingdomMaterials.Stock(Zone).Stockpiles",
				"KingdomSurvey.StockCapacityOf(chest) > 0" })
				Assert.That(fixture, Does.Contain(token), token);
			Assert.That(fixture, Does.Not.Contain("SetIntProperty(KingdomMaterials.StockpileProperty"),
				"a store is dedicated through the check-in, never stamped into place");
			Assert.That(Read(Checks), Does.Contain("synthetic store="),
				"the journal must disclose that the store is the fixture's own");
		}

		/// <summary>Case 3 lands on top of whatever case 1 delivered, so it measures its baseline
		/// instead of asserting a bare 2,400,000,000; and the advisory number it records is a
		/// ROOM, never the wrapped hold.</summary>
		[Test]
		public void CaseThreeMeasuresItsBaselineAndNeverConflatesRoomWithTheWrappedHold()
		{
			string cases = Read(Cases);
			foreach (string token in new[] { "out baseline)",
				"counted == baseline + 1200000000",
				"int advisoryRoom = KingdomMaterials.StockpileRoom(Container);",
				"advisoryRoom >= 1",
				"baseline held=", "advisory ROOM (not the wrapped hold)=" })
				Assert.That(cases, Does.Contain(token), token);
			Assert.That(cases, Does.Not.Contain("totalling 2,400,000,000"),
				"the store total is a measured baseline plus the fixture stacks, not a bare total");
		}

		/// <summary>The fixture's counts are assigned, and its bodies refuse every merge, so two
		/// rows really are two rows and the census really does sum them. Eligibility is proved by
		/// the census itself counting the first stack, never by asking a classifier.</summary>
		[Test]
		public void FixtureBodiesCarryAssignedCountsAndRefuseEveryMerge()
		{
			string fixture = Read(Fixture);
			foreach (string token in new[] { "item.SetIntProperty(\"NeverStack\", 1)",
				"item.HasPropertyOrTag(\"NeverStack\")", "stacker.StackCount = RawCount",
				"stacker.StackCount == RawCount", "Cell.AddObject(item, NoStack: true)",
				"Container.Inventory.AddObject(item, null, true, NoStack: true)",
				"item.Blueprint == Blueprint" })
				Assert.That(fixture, Does.Contain(token), token);
		}

		/// <summary>Every report line and the persona itself disclose the synthetic setup, and
		/// neither claims anything the run does not exercise.</summary>
		[Test]
		public void PersonaBracketsExactPhasesAndDisclosesEverySyntheticInput()
		{
			string persona = Read(Persona);
			Assert.That(persona, Does.Contain("REQUEST=founding-first-city"));
			Assert.That(persona, Does.Contain(
				"VERBS=deposit-overflow-setup,deposit-overflow-check"));
			Assert.That(persona, Does.Contain("SCRIPT=stagedigest;deposit-overflow-setup;"
				+ "deposit-overflow-check;deposit-overflow-check;deposit-overflow-check;"
				+ "deposit-overflow-check;stagedigest"));
			Assert.That(persona, Does.Contain("EXPECT=stagedigest:OK~founded=false,"
				+ "deposit-overflow-setup:OK~native-deposit-overflow phase=1,"
				+ "deposit-overflow-check:OK~native-deposit-overflow phase=2,"
				+ "deposit-overflow-check:OK~native-deposit-overflow phase=3,"
				+ "deposit-overflow-check:OK~native-deposit-overflow phase=4,"
				+ "deposit-overflow-check:OK~native-deposit-overflow cases=4 passed=4 failed=0,"
				+ "stagedigest:OK~founded=true,COMPLETE"));
			Assert.That(persona, Does.Contain("nothing"));
			string checks = Read(Checks);
			Assert.That(checks, Does.Contain("synthetic-camp=true; synthetic-stacks=true; "
				+ "synthetic-neverstack=true"));
			Assert.That(checks, Does.Contain("ordinary-reachability=untested; charter=untested; "
				+ "save-load=untested"));
		}
	}
}
#endif
