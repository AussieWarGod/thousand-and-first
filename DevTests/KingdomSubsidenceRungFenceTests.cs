#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomSubsidenceRungFenceTests
	{
		[Test]
		public void FrozenExactObligationsBlockWhileForeignFullIdsAndResidentsRemainFree()
		{
			KingdomSubsidenceStepBook book = Frozen();
			string wire = Wire(book), objectId = RungFixture.ObjectId(0);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire, objectId));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire, RungFixture.ObjectId(1)));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksWork(wire, objectId + "-foreign"));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksRoof(wire, 11));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksRoof(wire, 12));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksRoof(wire, 13));
			ClassicAssert.AreEqual(wire, Wire(book));
		}

		[Test]
		public void WearProofAloneDoesNotReleaseItsPendingRoofsAndEachProofReleasesOnlyItsObligation()
		{
			KingdomSubsidenceStepBook book = Frozen();
			string work = RungFixture.ObjectId(0), later = RungFixture.ObjectId(1);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(Wire(book), work));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan plan));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true,
				plan.Works[0].AfterWear, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(Wire(book), work));
			for (int i = 0; i < 2; i++)
			{
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungRoof(book, 0, i, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksRoof(Wire(book), 11 + i));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungRoof(book, 0, i, true, true,
					RungFixture.Due, KingdomBrinkRules.Unwarned, out book));
				string wire = Wire(book);
				ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksRoof(wire, 11 + i));
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire, work));
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire, later));
			}
			book = Release(book, 0);
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksWork(Wire(book), work));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksProjection(Wire(book)));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 1, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out plan));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 1, true, true,
				plan.Works[1].AfterWear, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(Wire(book), later));
			book = Release(book, 1);
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksWork(Wire(book), later));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksProjection(Wire(book)));
		}

		private static KingdomSubsidenceStepBook Release(KingdomSubsidenceStepBook book, int index)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan plan));
			KingdomSubsidenceRungWork row = plan.Works[index];
			KingdomSubsidenceWearReceipt receipt = new KingdomSubsidenceWearReceipt(
				(int)KingdomWearIncidentPhase.Mutated, plan.StepId, (int)KingdomWearRules.WearCause.Subsidence,
				row.BeforeWear, row.AfterWear, row.AfterWear, 0, null, "prior line", 0);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungRelease(book, index, true, receipt, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out plan));
			row = plan.Works[index];
			for (int cut = 0; cut <= 4; cut++)
			{
				KingdomSubsidenceWearReceipt observed = KingdomSubsidenceReleaseRules.AfterWrite(
					row.ReleaseBefore, row.ReleaseAfter, cut);
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(Wire(book), row.ObjectId));
				ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksProjection(Wire(book)));
				if (cut != 4) ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryProveRungRelease(
					book, index, true, observed, out _));
			}
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungRelease(book, index, true, row.ReleaseAfter, out book));
			return book;
		}

		[TestCase("ss1:new")]
		[TestCase("ss1:legacy")]
		public void ExplicitUnadmittedRecordsDoNotInventObligations(string wire)
		{
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksWork(wire, RungFixture.ObjectId(0)));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksRoof(wire, 11));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksWork(wire, null));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksRoof(wire, 0));
		}

		[Test]
		public void AdmittedIdleAndUnplannedStepHaveNoFrozenTargetsToFence()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(fresh, RungFixture.Realm,
				RungFixture.Settlement, out KingdomSubsidenceStepBook admitted));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(admitted,
				RungFixture.Due - KingdomSubsidenceStepRules.StepTicks, RungFixture.Due,
				GrowthStage.City, 5, out KingdomSubsidenceStepBook noRungs, 0, "water"));
			ClassicAssert.AreEqual(KingdomSubsidenceStepRules.NoRungs, noRungs.Active.RungModel);
			foreach (KingdomSubsidenceStepBook book in new[] { admitted, noRungs, RungFixture.Settling(false) })
			{
				ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksWork(Wire(book), RungFixture.ObjectId(0)));
				ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksRoof(Wire(book), 11));
			}
		}

		[Test]
		public void QuarantineKeepsFrozenUnfinishedTargetsFenced()
		{
			KingdomSubsidenceStepBook book = Frozen();
			book = book.With(book.Active.Copy(phase: KingdomSubsidenceStepPhase.Quarantined,
				fault: "fixture unresolved effect"), book.Sequence);
			string wire = Wire(book);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire, RungFixture.ObjectId(0)));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksRoof(wire, 11));
		}

		[TestCase(null)] [TestCase("")] [TestCase("ss1:")] [TestCase("ss1:garbage")]
		[TestCase("ss2:new")] [TestCase("ss1:new ")] [TestCase("sr1:none")]
		public void MalformedStepStorageRefusesAllWritersWithoutFallback(string wire)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire, "foreign-work"));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksRoof(wire, 99));
			KingdomCityBook city = Carrier();
			city.SubsidenceModel = wire;
			List<long> ticks = city.ResidentRoofTicks;
			ClassicAssert.IsFalse(city.TryWriteBrink(11, BrinkKind.Roof, true, 99, 100, null, 0));
			ClassicAssert.AreSame(ticks, city.ResidentRoofTicks);
			ClassicAssert.AreEqual(0, city.ResidentRoofTicks[0]);
			ClassicAssert.AreEqual(wire, city.SubsidenceModel);
		}

		[Test]
		public void TornNestedPlanDoesNotBecomeUnplannedOrFresh()
		{
			string wire = Wire(Frozen());
			byte[] bytes = Convert.FromBase64String(wire.Substring(4));
			byte[] marker = Encoding.UTF8.GetBytes("sr2:");
			int found = -1;
			for (int i = 0; i <= bytes.Length - marker.Length; i++)
				if (bytes[i] == marker[0] && bytes[i + 1] == marker[1]
					&& bytes[i + 2] == marker[2] && bytes[i + 3] == marker[3]) { found = i; break; }
			ClassicAssert.GreaterOrEqual(found, 0);
			bytes[found + 2] = (byte)'9';
			string torn = wire.Substring(0, 4) + Convert.ToBase64String(bytes);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(torn, "foreign"));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksRoof(torn, 99));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire.Substring(0, wire.Length - 3), "foreign"));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksRoof(wire.Substring(0, wire.Length - 3), 99));
		}

		[Test]
		public void ActivePlanRefusesMissingObjectAndNonpositiveResidentIdentity()
		{
			string wire = Wire(Frozen());
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire, null));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire, ""));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksRoof(wire, 0));
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksRoof(wire, -1));
		}

		[Test]
		public void RealNumericWorkHashCollisionDoesNotFenceTheUnrelatedFullObjectId()
		{
			FindCollision(out string selected, out string foreign);
			ClassicAssert.AreNotEqual(selected, foreign);
			ClassicAssert.AreEqual(KingdomCityRules.StableId(selected), KingdomCityRules.StableId(foreign));
			KingdomSubsidenceStepBook book = Frozen(RungFixture.ForObject(selected));
			string wire = Wire(book);
			ClassicAssert.IsTrue(KingdomSubsidenceRungRules.BlocksWork(wire, selected));
			ClassicAssert.IsFalse(KingdomSubsidenceRungRules.BlocksWork(wire, foreign));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan plan));
			ClassicAssert.AreEqual(selected, plan.Works[0].ObjectId);
			// Sharing the numeric model hash is not the caller's exactAuthority proof.
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, false, true,
				plan.Works[0].AfterWear, out _));
		}

		[Test]
		public void CarrierRefusesFencedRoofBeforeRepairingRaggedOrMalformedRawColumns()
		{
			KingdomCityBook city = Carrier();
			city.SubsidenceModel = Wire(Frozen());
			city.ResidentNames = null;
			city.ResidentCauses[0] = (int)KingdomStandingCause.Founder;
			city.ResidentRoofTicks[0] = -7;
			List<int> ids = city.ResidentIds, causes = city.ResidentCauses;
			List<long> ticks = city.ResidentRoofTicks;
			string before = city.SubsidenceModel;
			ClassicAssert.IsFalse(city.TryWriteBrink(11, BrinkKind.Roof, true, 99, 100, null, 0));
			ClassicAssert.IsNull(city.ResidentNames);
			ClassicAssert.AreSame(ids, city.ResidentIds); ClassicAssert.AreSame(causes, city.ResidentCauses);
			ClassicAssert.AreSame(ticks, city.ResidentRoofTicks);
			ClassicAssert.AreEqual((int)KingdomStandingCause.Founder, causes[0]);
			ClassicAssert.AreEqual(-7, ticks[0]);
			ClassicAssert.AreEqual(before, city.SubsidenceModel);
		}

		[TestCase("failed-read")] [TestCase("old-schema")] [TestCase("future-schema")]
		[TestCase("current-missing-model")] [TestCase("current-empty-model")] [TestCase("current-torn-model")]
		public void CarrierStorageAdmissionFailureRefusesBeforeNormalizationOrRoofWrite(string failure)
		{
			KingdomCityBook city = Carrier();
			ClassicAssert.AreEqual(KingdomCityRules.SchemaVersion, city.SchemaVersion);
			ClassicAssert.IsTrue(city.HasValidSubsidenceStorage());
			if (failure == "failed-read") city.SubsidenceReadFailed = true;
			if (failure == "old-schema") city.SchemaVersion = KingdomCityRules.SchemaVersion - 1;
			if (failure == "future-schema") city.SchemaVersion = KingdomCityRules.SchemaVersion + 1;
			if (failure == "current-missing-model") city.SubsidenceModel = null;
			if (failure == "current-empty-model") city.SubsidenceModel = "";
			if (failure == "current-torn-model") city.SubsidenceModel = Wire(Frozen()).Substring(0, 30);
			string wire = city.SubsidenceModel;
			int schema = city.SchemaVersion;
			bool readFailed = city.SubsidenceReadFailed;
			city.ResidentNames = null;
			List<long> ticks = city.ResidentRoofTicks, warned = city.ResidentRoofWarnedTicks;
			List<int> standing = city.ResidentRoofStanding;
			ClassicAssert.IsFalse(city.HasValidSubsidenceStorage());
			ClassicAssert.IsFalse(city.TryWriteBrink(11, BrinkKind.Roof, true, 99, 100, null, 0));
			ClassicAssert.IsNull(city.ResidentNames);
			ClassicAssert.AreSame(ticks, city.ResidentRoofTicks);
			ClassicAssert.AreSame(warned, city.ResidentRoofWarnedTicks);
			ClassicAssert.AreSame(standing, city.ResidentRoofStanding);
			ClassicAssert.AreEqual(0, ticks[0]); ClassicAssert.AreEqual(0, warned[0]); ClassicAssert.AreEqual(0, standing[0]);
			ClassicAssert.AreEqual(schema, city.SchemaVersion);
			ClassicAssert.AreEqual(wire, city.SubsidenceModel);
			ClassicAssert.AreEqual(readFailed, city.SubsidenceReadFailed);
		}

		[TestCase("ss1:new")] [TestCase("ss1:legacy")]
		public void CurrentSchemaUnadmittedCarrierStillAcceptsOrdinaryRoofWrites(string wire)
		{
			KingdomCityBook city = Carrier();
			city.SubsidenceModel = wire;
			ClassicAssert.IsTrue(city.HasValidSubsidenceStorage());
			ClassicAssert.IsTrue(city.TryWriteBrink(11, BrinkKind.Roof, true, 99, 100, null, 0));
			ClassicAssert.AreEqual(1, city.ResidentRoofStanding[0]);
			ClassicAssert.AreEqual(99, city.ResidentRoofTicks[0]);
			ClassicAssert.AreEqual(100, city.ResidentRoofWarnedTicks[0]);
			ClassicAssert.AreEqual(wire, city.SubsidenceModel);
		}

		[Test]
		public void CarrierStillAllowsCreedAndUnrelatedRoofThenReleasesOnlyProvedRoof()
		{
			KingdomSubsidenceStepBook book = Frozen();
			KingdomCityBook city = Carrier();
			city.SubsidenceModel = Wire(book);
			ClassicAssert.IsTrue(city.TryWriteBrink(11, BrinkKind.Creed, true, 99, 100, "Joppa", 1));
			ClassicAssert.IsTrue(city.TryWriteBrink(13, BrinkKind.Roof, true, 99, 100, null, 0));
			ClassicAssert.IsFalse(city.TryWriteBrink(11, BrinkKind.Roof, true, 99, 100, null, 0));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReadRungPlan(book, out KingdomSubsidenceRungPlan plan));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, plan.Works[0].AfterWear, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungRoof(book, 0, 0, out book));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungRoof(book, 0, 0, true, true, RungFixture.Due, 0, out book));
			city.SubsidenceModel = Wire(book);
			ClassicAssert.IsTrue(city.TryWriteBrink(11, BrinkKind.Roof, true, 99, 100, null, 0));
			ClassicAssert.IsFalse(city.TryWriteBrink(12, BrinkKind.Roof, true, 99, 100, null, 0));
			ClassicAssert.AreEqual(99, city.ResidentRoofTicks[0]);
			ClassicAssert.AreEqual(0, city.ResidentRoofTicks[1]);
			ClassicAssert.AreEqual(99, city.ResidentRoofTicks[2]);
		}

		private static KingdomSubsidenceStepBook Frozen(params KingdomSubsidenceRungWork[] works)
		{
			KingdomSubsidenceStepBook book = RungFixture.Settling(false);
			if (works.Length == 0) works = new[] { RungFixture.Work(0,
				roofs: new[] { RungFixture.Roof(11), RungFixture.Roof(12) }), RungFixture.Work(1) };
			KingdomSubsidenceRungPlan plan = new KingdomSubsidenceRungPlan(book.Active.Id,
				book.RealmId, book.SettlementId, RungFixture.Zone, GrowthStage.City, GrowthStage.Town,
				book.Active.DueTick, RungFixture.Prepared, book.Active.Completed, works);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
			return book;
		}

		private static string Wire(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook decoded));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(decoded, out string repeated));
			ClassicAssert.AreEqual(wire, repeated);
			return wire;
		}

		private static KingdomCityBook Carrier()
		{
			List<KingdomResidentRow> residents = new List<KingdomResidentRow>();
			for (int id = 11; id <= 13; id++) residents.Add(new KingdomResidentRow(id, "Fixture resident",
				0, 0, 1, KingdomCityRules.StableId(RungFixture.ObjectId(0)), 0, 0, KingdomDayShape.Field,
				KingdomResidentStanding.Resident, KingdomStandingCause.None, RungFixture.Zone,
				new KingdomBrinkWindow(false, 0, 0), new KingdomBrinkWindow(false, 0, 0), null, 0, null,
				"fixture", ""));
			KingdomStocks stocks = new KingdomStocks(new KingdomStockPair(0, 0),
				new KingdomStockPair(0, 0), new KingdomStockPair(0, 0));
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				RungFixture.Settlement, 0, stocks, new KingdomZoneRow[0], new KingdomWorkRow[0],
				residents.ToArray(), new KingdomClockRow[0], out KingdomCityState state, out _));
			KingdomCityBook book = new KingdomCityBook();
			ClassicAssert.IsTrue(book.TryPublish(state, out _));
			return book;
		}

		private static void FindCollision(out string selected, out string foreign)
		{
			Dictionary<int, string> seen = new Dictionary<int, string>();
			for (int i = 0; i < 1000000; i++)
			{
				string id = "rung-fence-collision-" + i.ToString(CultureInfo.InvariantCulture);
				int hash = KingdomCityRules.StableId(id);
				if (hash != 0 && seen.TryGetValue(hash, out string prior))
				{
					if (KingdomSubsidenceRules.RollRuin(RungFixture.Settlement, prior, (ulong)RungFixture.Due, GrowthStage.City))
					{ selected = prior; foreign = id; return; }
					if (KingdomSubsidenceRules.RollRuin(RungFixture.Settlement, id, (ulong)RungFixture.Due, GrowthStage.City))
					{ selected = id; foreign = prior; return; }
				}
				else seen[hash] = id;
			}
			throw new InvalidOperationException("No selected deterministic 31-bit hash collision found.");
		}
	}
}
#endif
