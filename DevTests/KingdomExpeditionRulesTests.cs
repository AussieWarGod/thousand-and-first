#if TAF_TESTS
using System;
using System.Security.Cryptography;
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomExpeditionRulesTests
	{
		private const string Source = "JoppaWorld.10.10.1.1.10";
		private const string Target = "JoppaWorld.12.11.2.0.12";

		[Test]
		public void QuoteIsExactBoundedAndUsesWorldTicks()
		{
			KingdomExpeditionQuote quote;
			Assert.IsTrue(KingdomExpeditionRules.TryQuote(Source, Target, 1200L, out quote));
			Assert.GreaterOrEqual(quote.DurationDays, KingdomExpeditionRules.MinDurationDays);
			Assert.LessOrEqual(quote.DurationDays, KingdomExpeditionRules.MaxDurationDays);
			Assert.AreEqual(quote.DurationDays * KingdomExpeditionRules.WaterPerDay,
				quote.WaterDrams);
			Assert.AreEqual(quote.DurationDays * KingdomExpeditionRules.ProvisionsPerDay,
				quote.Provisions);
			Assert.AreEqual(1200L + quote.DurationDays * KingdomRules.TicksPerDay, quote.DueTick);
			Assert.IsFalse(KingdomExpeditionRules.TryQuote(Source, Source, 1200L, out quote));
			Assert.IsFalse(KingdomExpeditionRules.TryQuote(Source,
				"AnotherWorld.12.11.2.0.12", 1200L, out quote));
		}

		[Test]
		public void OutcomeIsCounterAddressedAndNeverKillsOffscreen()
		{
			KernelSeed128 seed = new KernelSeed128(17UL, 29UL);
			KingdomExpeditionOutcome first;
			KingdomExpeditionOutcome again;
			int firstScrap;
			int againScrap;
			Assert.IsTrue(KingdomExpeditionRules.TryDrawOutcome(seed, "taf:settlement:test", 7,
				10, out first, out firstScrap));
			Assert.IsTrue(KingdomExpeditionRules.TryDrawOutcome(seed, "taf:settlement:test", 7,
				10, out again, out againScrap));
			Assert.AreEqual(first, again);
			Assert.AreEqual(firstScrap, againScrap);
			Assert.IsTrue(first == KingdomExpeditionOutcome.PickedClean
				|| first == KingdomExpeditionOutcome.ModestFind
				|| first == KingdomExpeditionOutcome.RichFind);
			Assert.GreaterOrEqual(firstScrap, 0);
			Assert.LessOrEqual(firstScrap, 4);
		}

		[Test]
		public void DueBoundaryIsInclusiveAndTotal()
		{
			Assert.IsFalse(KingdomExpeditionRules.Due(99L, 100L));
			Assert.IsTrue(KingdomExpeditionRules.Due(100L, 100L));
			Assert.IsFalse(KingdomExpeditionRules.Due(-1L, 100L));
			Assert.IsFalse(KingdomExpeditionRules.Due(100L, 0L));
		}

		[Test]
		public void DebitProgressAcceptsEveryInjectedCutWithoutRechargingPastAfter()
		{
			for (int current = 10; current >= 4; current--)
			{
				int remaining;
				Assert.IsTrue(KingdomExpeditionRules.TryDebitProgress(10, 4, true,
					current, out remaining));
				Assert.AreEqual(current - 4, remaining);
			}
			int none;
			Assert.IsTrue(KingdomExpeditionRules.TryDebitProgress(3, 0, false, 0, out none));
			Assert.AreEqual(0, none);
			Assert.IsFalse(KingdomExpeditionRules.TryDebitProgress(10, 4, true, 11, out none));
			Assert.IsFalse(KingdomExpeditionRules.TryDebitProgress(10, 4, true, 3, out none));
			Assert.IsFalse(KingdomExpeditionRules.TryDebitProgress(10, 4, false, 0, out none));
		}

		[Test]
		public void BoundedDebitReceiptSurvivesAttachAndEveryPartialLegCut()
		{
			KingdomExpeditionWaterLeg[] water =
			{
				new KingdomExpeditionWaterLeg("water-a", 10, 4, 20),
				new KingdomExpeditionWaterLeg("water-b", 8, 2, 20)
			};
			KingdomExpeditionProvisionLeg[] food =
			{
				new KingdomExpeditionProvisionLeg("larder-a", "food-a", 7, 2),
				new KingdomExpeditionProvisionLeg("larder-a", "food-b", 5, 4)
			};
			KingdomExpeditionDebitReceipt receipt;
			Assert.IsTrue(KingdomExpeditionDebitReceipt.TryCreate(17, Source, 12, 6,
				water, food, out receipt));
			string encoded;
			Assert.IsTrue(receipt.TryEncode(out encoded));
			Assert.LessOrEqual(encoded.Length, KingdomExpeditionDebitReceipt.MaxEncodedChars);
			KingdomExpeditionDebitReceipt cold;
			Assert.IsTrue(KingdomExpeditionDebitReceipt.TryDecode(encoded, out cold));
			Assert.AreEqual(17, cold.JobId);
			Assert.AreEqual(Source, cold.SourceZoneId);
			Assert.AreEqual(2, cold.WaterLegCount);
			Assert.AreEqual(2, cold.ProvisionLegCount);
			KingdomExpeditionWaterLeg secondWater;
			Assert.IsTrue(cold.TryWaterLeg(1, out secondWater));
			Assert.AreEqual(8, secondWater.BeforeVolume);
			Assert.AreEqual(2, secondWater.AfterVolume);
			KingdomExpeditionProvisionLeg secondFood;
			Assert.IsTrue(cold.TryProvisionLeg(1, out secondFood));
			Assert.AreEqual("food-b", secondFood.ItemId);
			Assert.IsFalse(KingdomExpeditionDebitReceipt.TryDecode(encoded + "AA", out cold));
		}

		[Test]
		public void DebitReceiptRejectsDuplicateIdentityAndWrongSums()
		{
			KingdomExpeditionDebitReceipt receipt;
			Assert.IsFalse(KingdomExpeditionDebitReceipt.TryCreate(1, Source, 4, 1,
				new[]
				{
					new KingdomExpeditionWaterLeg("same", 5, 3, 10),
					new KingdomExpeditionWaterLeg("same", 5, 3, 10)
				},
				new[] { new KingdomExpeditionProvisionLeg("larder", "food", 2, 1) },
				out receipt));
			Assert.IsFalse(KingdomExpeditionDebitReceipt.TryCreate(1, Source, 3, 1,
				new[] { new KingdomExpeditionWaterLeg("water", 5, 3, 10) },
				new[] { new KingdomExpeditionProvisionLeg("larder", "food", 2, 1) },
				out receipt));
		}

		[Test]
		public void ExpeditionPayloadSurvivesPublishAndTwoColdReads()
		{
			KingdomJobRow expedition = new KingdomJobRow(9, KingdomJobKind.Expedition,
				KingdomStockKind.Materials, 4, Source, Target, 1200L, 1,
				KingdomJobStatus.Open, (int)KingdomExpeditionPhase.Prepared, 0,
				new KingdomLeg[0], 0, 42, "Meyeh",
				"the rust wells", 7200L, 18, 6,
				(int)KingdomExpeditionOutcome.RichFind);
			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobTable.TryCreate(new[] { expedition }, out table, out fault));
			KingdomJobRegistry registry = new KingdomJobRegistry();
			Assert.IsTrue(registry.TryPublish(table, out fault));
			for (int pass = 0; pass < 2; pass++)
			{
				KingdomJobTable read;
				Assert.IsTrue(registry.TryRead(out read, out fault));
				KingdomJobRow row;
				Assert.IsTrue(read.TryGet(9, out row));
				Assert.AreEqual(KingdomJobKind.Expedition, row.Kind);
				Assert.AreEqual(42, row.SubjectId);
				Assert.AreEqual("Meyeh", row.SubjectName);
				Assert.AreEqual("the rust wells", row.TargetName);
				Assert.AreEqual(7200L, row.DueTick);
				Assert.AreEqual(18, row.WaterCost);
				Assert.AreEqual(6, row.ProvisionCost);
				Assert.AreEqual((int)KingdomExpeditionOutcome.RichFind, row.OutcomeCode);
				Assert.AreEqual((int)KingdomExpeditionPhase.Prepared, row.OriginCode);
				Assert.IsTrue(registry.TryPublish(read, out fault));
			}
			AssertTerminalResolutionReceiptIsPhaseBoundAndSurvivesColdReads();
		}

		private static void AssertTerminalResolutionReceiptIsPhaseBoundAndSurvivesColdReads()
		{
			const string provedGround = "JoppaWorld.11.10.1.0.11";
			KingdomJobRow expedition = new KingdomJobRow(19, KingdomJobKind.Expedition,
				KingdomStockKind.Materials, 0, Source, Target, 1200L, 1,
				KingdomJobStatus.Open, (int)KingdomExpeditionPhase.Dispatched, 0,
				new KingdomLeg[0], 0, 52, "Nehin", "the rust wells", 7200L, 18, 6,
				(int)KingdomExpeditionOutcome.PickedClean);
			KingdomJobRow terminal = expedition.WithExpeditionResolution(
				(int)KingdomExpeditionOutcome.ResidentMissingFromBoundGround, 2400L,
				provedGround, KingdomExpeditionDeedDisposition.NotApplicable, null, null,
				null);
			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobTable.TryCreate(new[] { terminal }, out table, out fault));
			Assert.IsFalse(KingdomJobTable.TryCreate(new[]
			{
				terminal.WithOriginCode((int)KingdomExpeditionPhase.Dispatched)
			}, out table, out fault));
			Assert.IsFalse(KingdomJobTable.TryCreate(new[]
			{
				expedition.WithOriginCode((int)KingdomExpeditionPhase.ResolutionPrepared)
			}, out table, out fault));
			for (int outcome = (int)KingdomExpeditionOutcome.ResidentDiedOnGround;
				outcome <= (int)KingdomExpeditionOutcome.ResidentJoinedFounder; outcome++)
			{
				Assert.IsTrue(KingdomJobRules.ValidExpeditionOutcomeForPhase(
					(int)KingdomExpeditionPhase.ResolutionPrepared, outcome));
				Assert.IsFalse(KingdomJobRules.ValidExpeditionOutcomeForPhase(
					(int)KingdomExpeditionPhase.Dispatched, outcome));
			}

			Assert.IsTrue(KingdomJobTable.TryCreate(new[] { terminal }, out table, out fault));
			KingdomJobRegistry registry = new KingdomJobRegistry();
			Assert.IsTrue(registry.TryPublish(table, out fault));
			for (int pass = 0; pass < 2; pass++)
			{
				KingdomJobTable read;
				KingdomJobRow row;
				Assert.IsTrue(registry.TryRead(out read, out fault));
				Assert.IsTrue(read.TryGet(19, out row));
				Assert.AreEqual((int)KingdomExpeditionPhase.ResolutionPrepared,
					row.OriginCode);
				Assert.AreEqual(
					(int)KingdomExpeditionOutcome.ResidentMissingFromBoundGround,
					row.OutcomeCode);
				Assert.AreEqual(2400L, row.DueTick);
				Assert.AreEqual(provedGround, row.DestZoneId);
				Assert.IsTrue(registry.TryPublish(read, out fault));
			}
			byte[] missionPayload;
			Assert.IsTrue(KingdomRealmJobWireFixture.TryEncode(registry,
				KingdomRealmJobWireFixture.MissionVersion, out missionPayload));
			KingdomJobRegistry decoded;
			Assert.IsTrue(KingdomRealmJobWireFixture.TryDecode(missionPayload,
				KingdomRealmJobWireFixture.MissionVersion, out decoded));
			KingdomJobTable missionTable;
			KingdomJobRow missionRow;
			Assert.IsTrue(decoded.TryRead(out missionTable, out fault));
			Assert.IsTrue(missionTable.TryGet(19, out missionRow));
			Assert.AreEqual((int)KingdomExpeditionPhase.ResolutionPrepared,
				missionRow.OriginCode);

			byte[] payload;
			Assert.IsTrue(KingdomRealmJobWireFixture.TryEncode(registry,
				KingdomRealmJobWireFixture.CurrentVersion, out payload));
			Assert.IsTrue(KingdomRealmJobWireFixture.TryDecode(payload,
				KingdomRealmJobWireFixture.CurrentVersion, out decoded));
			KingdomJobTable decodedTable;
			KingdomJobRow decodedRow;
			Assert.IsTrue(decoded.TryRead(out decodedTable, out fault));
			Assert.IsTrue(decodedTable.TryGet(19, out decodedRow));
			Assert.AreEqual((int)KingdomExpeditionPhase.ResolutionPrepared,
				decodedRow.OriginCode);
			Assert.AreEqual(
				(int)KingdomExpeditionOutcome.ResidentMissingFromBoundGround,
				decodedRow.OutcomeCode);
			Assert.AreEqual(2400L, decodedRow.DueTick);
			Assert.AreEqual(provedGround, decodedRow.DestZoneId);
		}

		[Test]
		public void RealmTableRejectsSecondExpeditionForExactResident()
		{
			KingdomJobRow first = new KingdomJobRow(1, KingdomJobKind.Expedition,
				KingdomStockKind.Materials, 0, Source, Target, 1L, 1,
				KingdomJobStatus.Open, (int)KingdomExpeditionPhase.Prepared, 0,
				new KingdomLeg[0], 0, 42, "Meyeh", "rust", 100L, 9, 3,
				(int)KingdomExpeditionOutcome.PickedClean);
			KingdomJobRow duplicate = new KingdomJobRow(2, KingdomJobKind.Expedition,
				KingdomStockKind.Materials, 1, Source, Target, 2L, 1,
				KingdomJobStatus.Open, (int)KingdomExpeditionPhase.Prepared, 0,
				new KingdomLeg[0], 0, 42, "Meyeh", "rust", 101L, 9, 3,
				(int)KingdomExpeditionOutcome.ModestFind);
			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomJobTable.TryCreate(new[] { first, duplicate },
				out table, out fault));
			Assert.AreEqual(KingdomCityFault.DuplicateBinding, fault);
		}

		[Test]
		public void FrozenLegacyDeliveryEnvelopePadsThenRewritesCanonically()
		{
			KingdomJobRegistry legacy = new KingdomJobRegistry();
			legacy.JobCounter = 3;
			legacy.JobIds.Add(3); legacy.Kinds.Add((int)KingdomJobKind.Delivery);
			legacy.Cargos.Add((int)KingdomStockKind.Food); legacy.CargoAmounts.Add(2);
			legacy.SourceZoneIds.Add(Source); legacy.DestZoneIds.Add(Target);
			legacy.StartTicks.Add(1200L); legacy.WalkTicksPerCell.Add(1);
			legacy.Statuses.Add((int)KingdomJobStatus.Open); legacy.OriginCodes.Add(1);
			legacy.DepositLegIndexes.Add(0); legacy.LegCounts.Add(0);
			legacy.Normalize();
			Assert.AreEqual(1, legacy.Count);
			Assert.AreEqual(1, legacy.SubjectIds.Count);
			Assert.AreEqual(0, legacy.SubjectIds[0]);
			Assert.AreEqual("", legacy.TargetNames[0]);
			KingdomCityFault fault;
			KingdomJobTable first;
			Assert.IsTrue(legacy.TryRead(out first, out fault));
			Assert.IsTrue(legacy.TryPublish(first, out fault));
			KingdomJobTable second;
			Assert.IsTrue(legacy.TryRead(out second, out fault));
			KingdomJobRow row;
			Assert.IsTrue(second.TryGet(3, out row));
			Assert.AreEqual(KingdomJobKind.Delivery, row.Kind);
			Assert.AreEqual(0, row.SubjectId);
			Assert.AreEqual(0L, row.DueTick);
		}

		[Test]
		public void FrozenRealmV2JobWireRewritesCurrentAndSurvivesSecondColdRead()
		{
			KingdomLeg leg = new KingdomLeg(Target, 1, 2, 7, 8, 12, 100L, 112L);
			KingdomJobRow delivery = new KingdomJobRow(5, KingdomJobKind.Delivery,
				KingdomStockKind.Food, 2, Source, Target, 100L, 1,
				KingdomJobStatus.Open, 3, 0, new[] { leg }, 1);
			KingdomJobTable table;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomJobTable.TryCreate(new[] { delivery }, out table, out fault));
			KingdomJobRegistry writer = new KingdomJobRegistry { JobCounter = 5 };
			Assert.IsTrue(writer.TryPublish(table, out fault));
			byte[] v2;
			Assert.IsTrue(KingdomRealmJobWireFixture.TryEncode(writer,
				KingdomRealmJobWireFixture.LegacyVersion, out v2));
			string digest;
			using (SHA256 sha = SHA256.Create())
				digest = BitConverter.ToString(sha.ComputeHash(v2)).Replace("-", "")
					.ToLowerInvariant();
			Assert.AreEqual(173, v2.Length);
			Assert.AreEqual(
				"b3f2b9622d024a6e33aedff82bdf36cef4a4c15158d9c02c1d9c7cfd0110f94b",
				digest);

			KingdomJobRegistry migrated;
			Assert.IsTrue(KingdomRealmJobWireFixture.TryDecode(v2,
				KingdomRealmJobWireFixture.LegacyVersion, out migrated));
			Assert.AreEqual(1, migrated.Count);
			Assert.AreEqual(0, migrated.SubjectIds[0]);
			Assert.AreEqual("", migrated.SubjectNames[0]);
			Assert.AreEqual(0, migrated.OutcomeCodes[0]);

			byte[] current;
			Assert.IsTrue(KingdomRealmJobWireFixture.TryEncode(migrated,
				KingdomRealmJobWireFixture.CurrentVersion, out current));
			KingdomJobRegistry coldOne;
			Assert.IsTrue(KingdomRealmJobWireFixture.TryDecode(current,
				KingdomRealmJobWireFixture.CurrentVersion, out coldOne));
			byte[] rewritten;
			Assert.IsTrue(KingdomRealmJobWireFixture.TryEncode(coldOne,
				KingdomRealmJobWireFixture.CurrentVersion, out rewritten));
			CollectionAssert.AreEqual(current, rewritten);
			KingdomJobRegistry coldTwo;
			Assert.IsTrue(KingdomRealmJobWireFixture.TryDecode(rewritten,
				KingdomRealmJobWireFixture.CurrentVersion, out coldTwo));
			KingdomJobRow roundTrip;
			KingdomJobTable roundTripTable;
			Assert.IsTrue(coldTwo.TryRead(out roundTripTable, out fault));
			Assert.IsTrue(roundTripTable.TryGet(5, out roundTrip));
			Assert.AreEqual(KingdomJobKind.Delivery, roundTrip.Kind);
			Assert.AreEqual(1, roundTrip.LegCount);
			byte[] truncated = new byte[v2.Length - 1];
			Array.Copy(v2, truncated, truncated.Length);
			Assert.IsFalse(KingdomRealmJobWireFixture.TryDecode(truncated,
				KingdomRealmJobWireFixture.LegacyVersion, out migrated));
		}
	}
}
#endif
