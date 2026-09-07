#if TAF_TESTS
using System;
using System.Security.Cryptography;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
			ClassicAssert.IsTrue(KingdomExpeditionRules.TryQuote(Source, Target, 1200L, out quote));
			ClassicAssert.GreaterOrEqual(quote.DurationDays, KingdomExpeditionRules.MinDurationDays);
			ClassicAssert.LessOrEqual(quote.DurationDays, KingdomExpeditionRules.MaxDurationDays);
			ClassicAssert.AreEqual(quote.DurationDays * KingdomExpeditionRules.WaterPerDay,
				quote.WaterDrams);
			ClassicAssert.AreEqual(quote.DurationDays * KingdomExpeditionRules.ProvisionsPerDay,
				quote.Provisions);
			ClassicAssert.AreEqual(1200L + quote.DurationDays * KingdomRules.TicksPerDay, quote.DueTick);
			ClassicAssert.IsFalse(KingdomExpeditionRules.TryQuote(Source, Source, 1200L, out quote));
			ClassicAssert.IsFalse(KingdomExpeditionRules.TryQuote(Source,
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
			ClassicAssert.IsTrue(KingdomExpeditionRules.TryDrawOutcome(seed, "taf:settlement:test", 7,
				10, out first, out firstScrap));
			ClassicAssert.IsTrue(KingdomExpeditionRules.TryDrawOutcome(seed, "taf:settlement:test", 7,
				10, out again, out againScrap));
			ClassicAssert.AreEqual(first, again);
			ClassicAssert.AreEqual(firstScrap, againScrap);
			ClassicAssert.IsTrue(first == KingdomExpeditionOutcome.PickedClean
				|| first == KingdomExpeditionOutcome.ModestFind
				|| first == KingdomExpeditionOutcome.RichFind);
			ClassicAssert.GreaterOrEqual(firstScrap, 0);
			ClassicAssert.LessOrEqual(firstScrap, 4);
		}

		[Test]
		public void DueBoundaryIsInclusiveAndTotal()
		{
			ClassicAssert.IsFalse(KingdomExpeditionRules.Due(99L, 100L));
			ClassicAssert.IsTrue(KingdomExpeditionRules.Due(100L, 100L));
			ClassicAssert.IsFalse(KingdomExpeditionRules.Due(-1L, 100L));
			ClassicAssert.IsFalse(KingdomExpeditionRules.Due(100L, 0L));
		}

		[Test]
		public void DebitProgressAcceptsEveryInjectedCutWithoutRechargingPastAfter()
		{
			for (int current = 10; current >= 4; current--)
			{
				int remaining;
				ClassicAssert.IsTrue(KingdomExpeditionRules.TryDebitProgress(10, 4, true,
					current, out remaining));
				ClassicAssert.AreEqual(current - 4, remaining);
			}
			int none;
			ClassicAssert.IsTrue(KingdomExpeditionRules.TryDebitProgress(3, 0, false, 0, out none));
			ClassicAssert.AreEqual(0, none);
			ClassicAssert.IsFalse(KingdomExpeditionRules.TryDebitProgress(10, 4, true, 11, out none));
			ClassicAssert.IsFalse(KingdomExpeditionRules.TryDebitProgress(10, 4, true, 3, out none));
			ClassicAssert.IsFalse(KingdomExpeditionRules.TryDebitProgress(10, 4, false, 0, out none));
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
			ClassicAssert.IsTrue(KingdomExpeditionDebitReceipt.TryCreate(17, Source, 12, 6,
				water, food, out receipt));
			string encoded;
			ClassicAssert.IsTrue(receipt.TryEncode(out encoded));
			ClassicAssert.LessOrEqual(encoded.Length, KingdomExpeditionDebitReceipt.MaxEncodedChars);
			KingdomExpeditionDebitReceipt cold;
			ClassicAssert.IsTrue(KingdomExpeditionDebitReceipt.TryDecode(encoded, out cold));
			ClassicAssert.AreEqual(17, cold.JobId);
			ClassicAssert.AreEqual(Source, cold.SourceZoneId);
			ClassicAssert.AreEqual(2, cold.WaterLegCount);
			ClassicAssert.AreEqual(2, cold.ProvisionLegCount);
			KingdomExpeditionWaterLeg secondWater;
			ClassicAssert.IsTrue(cold.TryWaterLeg(1, out secondWater));
			ClassicAssert.AreEqual(8, secondWater.BeforeVolume);
			ClassicAssert.AreEqual(2, secondWater.AfterVolume);
			KingdomExpeditionProvisionLeg secondFood;
			ClassicAssert.IsTrue(cold.TryProvisionLeg(1, out secondFood));
			ClassicAssert.AreEqual("food-b", secondFood.ItemId);
			ClassicAssert.IsFalse(KingdomExpeditionDebitReceipt.TryDecode(encoded + "AA", out cold));
		}

		[Test]
		public void DebitReceiptRejectsDuplicateIdentityAndWrongSums()
		{
			KingdomExpeditionDebitReceipt receipt;
			ClassicAssert.IsFalse(KingdomExpeditionDebitReceipt.TryCreate(1, Source, 4, 1,
				new[]
				{
					new KingdomExpeditionWaterLeg("same", 5, 3, 10),
					new KingdomExpeditionWaterLeg("same", 5, 3, 10)
				},
				new[] { new KingdomExpeditionProvisionLeg("larder", "food", 2, 1) },
				out receipt));
			ClassicAssert.IsFalse(KingdomExpeditionDebitReceipt.TryCreate(1, Source, 3, 1,
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
			ClassicAssert.IsTrue(KingdomJobTable.TryCreate(new[] { expedition }, out table, out fault));
			KingdomJobRegistry registry = new KingdomJobRegistry();
			ClassicAssert.IsTrue(registry.TryPublish(table, out fault));
			for (int pass = 0; pass < 2; pass++)
			{
				KingdomJobTable read;
				ClassicAssert.IsTrue(registry.TryRead(out read, out fault));
				KingdomJobRow row;
				ClassicAssert.IsTrue(read.TryGet(9, out row));
				ClassicAssert.AreEqual(KingdomJobKind.Expedition, row.Kind);
				ClassicAssert.AreEqual(42, row.SubjectId);
				ClassicAssert.AreEqual("Meyeh", row.SubjectName);
				ClassicAssert.AreEqual("the rust wells", row.TargetName);
				ClassicAssert.AreEqual(7200L, row.DueTick);
				ClassicAssert.AreEqual(18, row.WaterCost);
				ClassicAssert.AreEqual(6, row.ProvisionCost);
				ClassicAssert.AreEqual((int)KingdomExpeditionOutcome.RichFind, row.OutcomeCode);
				ClassicAssert.AreEqual((int)KingdomExpeditionPhase.Prepared, row.OriginCode);
				ClassicAssert.IsTrue(registry.TryPublish(read, out fault));
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
			ClassicAssert.IsTrue(KingdomJobTable.TryCreate(new[] { terminal }, out table, out fault));
			ClassicAssert.IsFalse(KingdomJobTable.TryCreate(new[]
			{
				terminal.WithOriginCode((int)KingdomExpeditionPhase.Dispatched)
			}, out table, out fault));
			ClassicAssert.IsFalse(KingdomJobTable.TryCreate(new[]
			{
				expedition.WithOriginCode((int)KingdomExpeditionPhase.ResolutionPrepared)
			}, out table, out fault));
			for (int outcome = (int)KingdomExpeditionOutcome.ResidentDiedOnGround;
				outcome <= (int)KingdomExpeditionOutcome.ResidentJoinedFounder; outcome++)
			{
				ClassicAssert.IsTrue(KingdomJobRules.ValidExpeditionOutcomeForPhase(
					(int)KingdomExpeditionPhase.ResolutionPrepared, outcome));
				ClassicAssert.IsFalse(KingdomJobRules.ValidExpeditionOutcomeForPhase(
					(int)KingdomExpeditionPhase.Dispatched, outcome));
			}

			ClassicAssert.IsTrue(KingdomJobTable.TryCreate(new[] { terminal }, out table, out fault));
			KingdomJobRegistry registry = new KingdomJobRegistry();
			ClassicAssert.IsTrue(registry.TryPublish(table, out fault));
			for (int pass = 0; pass < 2; pass++)
			{
				KingdomJobTable read;
				KingdomJobRow row;
				ClassicAssert.IsTrue(registry.TryRead(out read, out fault));
				ClassicAssert.IsTrue(read.TryGet(19, out row));
				ClassicAssert.AreEqual((int)KingdomExpeditionPhase.ResolutionPrepared,
					row.OriginCode);
				ClassicAssert.AreEqual(
					(int)KingdomExpeditionOutcome.ResidentMissingFromBoundGround,
					row.OutcomeCode);
				ClassicAssert.AreEqual(2400L, row.DueTick);
				ClassicAssert.AreEqual(provedGround, row.DestZoneId);
				ClassicAssert.IsTrue(registry.TryPublish(read, out fault));
			}
			byte[] missionPayload;
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryEncode(registry,
				KingdomRealmJobWireFixture.MissionVersion, out missionPayload));
			KingdomJobRegistry decoded;
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryDecode(missionPayload,
				KingdomRealmJobWireFixture.MissionVersion, out decoded));
			KingdomJobTable missionTable;
			KingdomJobRow missionRow;
			ClassicAssert.IsTrue(decoded.TryRead(out missionTable, out fault));
			ClassicAssert.IsTrue(missionTable.TryGet(19, out missionRow));
			ClassicAssert.AreEqual((int)KingdomExpeditionPhase.ResolutionPrepared,
				missionRow.OriginCode);

			byte[] payload;
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryEncode(registry,
				KingdomRealmJobWireFixture.CurrentVersion, out payload));
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryDecode(payload,
				KingdomRealmJobWireFixture.CurrentVersion, out decoded));
			KingdomJobTable decodedTable;
			KingdomJobRow decodedRow;
			ClassicAssert.IsTrue(decoded.TryRead(out decodedTable, out fault));
			ClassicAssert.IsTrue(decodedTable.TryGet(19, out decodedRow));
			ClassicAssert.AreEqual((int)KingdomExpeditionPhase.ResolutionPrepared,
				decodedRow.OriginCode);
			ClassicAssert.AreEqual(
				(int)KingdomExpeditionOutcome.ResidentMissingFromBoundGround,
				decodedRow.OutcomeCode);
			ClassicAssert.AreEqual(2400L, decodedRow.DueTick);
			ClassicAssert.AreEqual(provedGround, decodedRow.DestZoneId);
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
			ClassicAssert.IsFalse(KingdomJobTable.TryCreate(new[] { first, duplicate },
				out table, out fault));
			ClassicAssert.AreEqual(KingdomCityFault.DuplicateBinding, fault);
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
			ClassicAssert.AreEqual(1, legacy.Count);
			ClassicAssert.AreEqual(1, legacy.SubjectIds.Count);
			ClassicAssert.AreEqual(0, legacy.SubjectIds[0]);
			ClassicAssert.AreEqual("", legacy.TargetNames[0]);
			KingdomCityFault fault;
			KingdomJobTable first;
			ClassicAssert.IsTrue(legacy.TryRead(out first, out fault));
			ClassicAssert.IsTrue(legacy.TryPublish(first, out fault));
			KingdomJobTable second;
			ClassicAssert.IsTrue(legacy.TryRead(out second, out fault));
			KingdomJobRow row;
			ClassicAssert.IsTrue(second.TryGet(3, out row));
			ClassicAssert.AreEqual(KingdomJobKind.Delivery, row.Kind);
			ClassicAssert.AreEqual(0, row.SubjectId);
			ClassicAssert.AreEqual(0L, row.DueTick);
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
			ClassicAssert.IsTrue(KingdomJobTable.TryCreate(new[] { delivery }, out table, out fault));
			KingdomJobRegistry writer = new KingdomJobRegistry { JobCounter = 5 };
			ClassicAssert.IsTrue(writer.TryPublish(table, out fault));
			byte[] v2;
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryEncode(writer,
				KingdomRealmJobWireFixture.LegacyVersion, out v2));
			string digest;
			using (SHA256 sha = SHA256.Create())
				digest = BitConverter.ToString(sha.ComputeHash(v2)).Replace("-", "")
					.ToLowerInvariant();
			ClassicAssert.AreEqual(173, v2.Length);
			ClassicAssert.AreEqual(
				"b3f2b9622d024a6e33aedff82bdf36cef4a4c15158d9c02c1d9c7cfd0110f94b",
				digest);

			KingdomJobRegistry migrated;
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryDecode(v2,
				KingdomRealmJobWireFixture.LegacyVersion, out migrated));
			ClassicAssert.AreEqual(1, migrated.Count);
			ClassicAssert.AreEqual(0, migrated.SubjectIds[0]);
			ClassicAssert.AreEqual("", migrated.SubjectNames[0]);
			ClassicAssert.AreEqual(0, migrated.OutcomeCodes[0]);

			byte[] current;
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryEncode(migrated,
				KingdomRealmJobWireFixture.CurrentVersion, out current));
			KingdomJobRegistry coldOne;
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryDecode(current,
				KingdomRealmJobWireFixture.CurrentVersion, out coldOne));
			byte[] rewritten;
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryEncode(coldOne,
				KingdomRealmJobWireFixture.CurrentVersion, out rewritten));
			CollectionAssert.AreEqual(current, rewritten);
			KingdomJobRegistry coldTwo;
			ClassicAssert.IsTrue(KingdomRealmJobWireFixture.TryDecode(rewritten,
				KingdomRealmJobWireFixture.CurrentVersion, out coldTwo));
			KingdomJobRow roundTrip;
			KingdomJobTable roundTripTable;
			ClassicAssert.IsTrue(coldTwo.TryRead(out roundTripTable, out fault));
			ClassicAssert.IsTrue(roundTripTable.TryGet(5, out roundTrip));
			ClassicAssert.AreEqual(KingdomJobKind.Delivery, roundTrip.Kind);
			ClassicAssert.AreEqual(1, roundTrip.LegCount);
			byte[] truncated = new byte[v2.Length - 1];
			Array.Copy(v2, truncated, truncated.Length);
			ClassicAssert.IsFalse(KingdomRealmJobWireFixture.TryDecode(truncated,
				KingdomRealmJobWireFixture.LegacyVersion, out migrated));
		}
	}
}
#endif
