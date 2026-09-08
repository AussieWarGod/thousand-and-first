#if TAF_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	// The delegate supplies named-field payloads; admission and normalization are real, not engine binary IO.
	public sealed class KingdomSubsidenceFrozenLoadTests
	{
		[TestCase("prepared", false)] [TestCase("roof-intent", false)]
		[TestCase("roof-intent", true)] [TestCase("roof-proved", false)]
		[TestCase("roof-proved", true)] [TestCase("quarantined", true)]
		public void ValidFrozenNamedLoadPreservesExactFieldsReferencesAndRoofTuple(string phase, bool standing)
		{
			KingdomCityBook source = Frozen(phase, standing);
			NamedPayload payload = new NamedPayload(source);
			KingdomCityBook loaded = new KingdomCityBook();
			loaded.ReadNamedState(() =>
			{
				ClassicAssert.AreEqual(0, loaded.SchemaVersion);
				ClassicAssert.IsNull(loaded.SubsidenceModel);
				ClassicAssert.IsTrue(loaded.SubsidenceReadFailed);
				payload.ReadInto(loaded);
			});
			ClassicAssert.IsFalse(loaded.SubsidenceReadFailed);
			ClassicAssert.IsTrue(loaded.HasValidSubsidenceStorage());
			ClassicAssert.IsTrue(loaded.TryReadExact(out KingdomCityState state, out _));
			ClassicAssert.AreEqual(2, state.ResidentCount);
			ClassicAssert.IsTrue(loaded.TryCaptureSubsidenceRoof(11, out KingdomCityBook.SubsidenceRoofRow roof));
			ClassicAssert.AreEqual(source.ResidentRoofStanding[0] == 1, roof.RoofStanding);
			ClassicAssert.AreEqual(source.ResidentRoofTicks[0], roof.Reached);
			ClassicAssert.AreEqual(source.ResidentRoofWarnedTicks[0], roof.Warned);
			payload.Unchanged(loaded);
			payload.Unchanged(source);
		}

		[TestCase(false)] [TestCase(true)]
		public void RepeatedLiveNormalizationPreservesHealthyFrozenAuthorityWithoutWholeBookPublication(bool standing)
		{
			KingdomCityBook city = Frozen("roof-intent", standing);
			NamedPayload payload = new NamedPayload(city);
			for (int repeat = 0; repeat < 3; repeat++)
			{
				city.Normalize();
				ClassicAssert.IsFalse(city.SubsidenceReadFailed);
				ClassicAssert.IsTrue(city.HasValidSubsidenceStorage());
				payload.Unchanged(city);
			}
		}

		[TestCase("null-column")] [TestCase("torn-column")] [TestCase("null-name")]
		[TestCase("cause-mismatch")] [TestCase("duplicate-id")] [TestCase("roof-flag")]
		[TestCase("off-ticks")] [TestCase("negative-clock")] [TestCase("foreign-owner")]
		[TestCase("old-schema")] [TestCase("future-schema")]
		[TestCase("missing-wire")] [TestCase("empty-wire")] [TestCase("malformed-wire")]
		[TestCase("truncated-wire")] [TestCase("future-wire")]
		public void MalformedFrozenNamedPayloadRefusesBeforeAnyNormalizationOrCarrierRepair(string corruption)
		{
			KingdomCityBook source = Frozen("roof-intent", false);
			Corrupt(source, corruption);
			NamedPayload payload = new NamedPayload(source);
			KingdomCityBook loaded = new KingdomCityBook();
			Assert.Throws<InvalidDataException>(() => loaded.ReadNamedState(() => payload.ReadInto(loaded)));
			ClassicAssert.IsTrue(loaded.SubsidenceReadFailed);
			ClassicAssert.IsFalse(loaded.HasValidSubsidenceStorage());
			payload.Unchanged(loaded);
			loaded.Normalize();
			ClassicAssert.IsTrue(loaded.SubsidenceReadFailed);
			payload.Unchanged(loaded);
			payload.Unchanged(source);
		}

		[TestCase("null-column")] [TestCase("torn-column")] [TestCase("null-name")]
		[TestCase("cause-mismatch")] [TestCase("duplicate-id")] [TestCase("roof-flag")]
		[TestCase("off-ticks")] [TestCase("negative-clock")] [TestCase("foreign-owner")]
		[TestCase("old-schema")] [TestCase("future-schema")]
		[TestCase("missing-wire")] [TestCase("empty-wire")] [TestCase("malformed-wire")]
		[TestCase("truncated-wire")] [TestCase("future-wire")]
		public void LiveMalformedFrozenCarrierLatchesFailureWithoutRepairingRawEvidence(string corruption)
		{
			KingdomCityBook city = Frozen("roof-intent", false);
			Corrupt(city, corruption);
			ClassicAssert.IsFalse(city.SubsidenceReadFailed);
			ClassicAssert.IsFalse(city.HasValidSubsidenceStorage());
			ClassicAssert.IsFalse(city.SubsidenceReadFailed, "observation alone must not replace the failure latch");
			NamedPayload payload = new NamedPayload(city);
			city.Normalize();
			ClassicAssert.IsTrue(city.SubsidenceReadFailed);
			ClassicAssert.IsFalse(city.HasValidSubsidenceStorage());
			payload.Unchanged(city);
			city.Normalize();
			ClassicAssert.IsTrue(city.SubsidenceReadFailed);
			payload.Unchanged(city);
		}

		[Test]
		public void ValidFrozenBytesCannotClearAnExistingFailedReadLatchThroughNormalize()
		{
			KingdomCityBook city = Frozen("roof-intent", true);
			city.SubsidenceReadFailed = true;
			NamedPayload payload = new NamedPayload(city);
			city.Normalize();
			ClassicAssert.IsTrue(city.SubsidenceReadFailed);
			ClassicAssert.IsFalse(city.HasValidSubsidenceStorage());
			payload.Unchanged(city);
		}

		[TestCase("ss1:new")] [TestCase("ss1:legacy")]
		public void ExistingFailedReadPreventsOrdinaryRepairEvenWhenTheSidecarItselfDecodes(string wire)
		{
			KingdomCityBook city = Ordinary(true);
			city.SubsidenceModel = wire;
			city.ResidentNames[0] = null;
			city.ResidentCauses[0] = (int)KingdomStandingCause.Founder;
			city.ResidentRoofWarnedTicks = null;
			city.SubsidenceReadFailed = true;
			NamedPayload payload = new NamedPayload(city);
			city.Normalize();
			ClassicAssert.IsTrue(city.SubsidenceReadFailed);
			ClassicAssert.IsFalse(city.HasValidSubsidenceStorage());
			payload.Unchanged(city);
		}

		[Test]
		public void InterruptedNamedReaderPreservesAssignedFrozenPayloadAndRefusesAuthority()
		{
			KingdomCityBook source = Frozen("roof-intent", true);
			NamedPayload payload = new NamedPayload(source);
			KingdomCityBook loaded = new KingdomCityBook();
			Assert.Throws<InvalidOperationException>(() => loaded.ReadNamedState(() =>
			{
				payload.ReadInto(loaded);
				throw new InvalidOperationException("frozen named-reader cut");
			}));
			ClassicAssert.IsTrue(loaded.SubsidenceReadFailed);
			loaded.Normalize();
			ClassicAssert.IsFalse(loaded.HasValidSubsidenceStorage());
			payload.Unchanged(loaded);
			payload.Unchanged(source);
		}

		[TestCase("ss1:new")] [TestCase("ss1:legacy")]
		public void ExplicitUnadmittedStorageStillPermitsOrdinaryNullColumnRepair(string wire)
		{
			KingdomCityBook city = Ordinary(true);
			city.SubsidenceModel = wire;
			List<int> ids = city.ResidentIds;
			city.ResidentRoofWarnedTicks = null;
			city.Normalize();
			ClassicAssert.AreEqual(wire, city.SubsidenceModel);
			ClassicAssert.IsFalse(city.SubsidenceReadFailed);
			ClassicAssert.IsTrue(city.HasValidSubsidenceStorage());
			ClassicAssert.AreSame(ids, city.ResidentIds);
			ClassicAssert.AreEqual(0, city.ResidentCount, "ordinary ragged rows retain the existing shortest-column policy");
			ClassicAssert.IsNotNull(city.ResidentRoofWarnedTicks);
			ClassicAssert.IsTrue(city.TryReadExact(out _, out _));
		}

		[TestCase("ss1:new")] [TestCase("ss1:legacy")]
		public void ExplicitUnadmittedStorageStillPermitsOrdinaryPresentationAndCauseRepair(string wire)
		{
			KingdomCityBook city = Ordinary(true);
			city.SubsidenceModel = wire;
			List<string> names = city.ResidentNames;
			List<int> causes = city.ResidentCauses;
			city.ResidentNames[0] = null;
			city.ResidentCauses[0] = (int)KingdomStandingCause.Founder;
			city.Normalize();
			ClassicAssert.AreSame(names, city.ResidentNames); ClassicAssert.AreSame(causes, city.ResidentCauses);
			ClassicAssert.AreEqual("", city.ResidentNames[0]);
			ClassicAssert.AreEqual((int)KingdomStandingCause.None, city.ResidentCauses[0]);
			ClassicAssert.AreEqual(2, city.ResidentCount);
			ClassicAssert.AreEqual(RungFixture.Due + 10, city.ResidentRoofTicks[0]);
			ClassicAssert.AreEqual(RungFixture.Due + 20, city.ResidentRoofWarnedTicks[0]);
			ClassicAssert.AreEqual(wire, city.SubsidenceModel);
			ClassicAssert.IsFalse(city.SubsidenceReadFailed);
			ClassicAssert.IsTrue(city.HasValidSubsidenceStorage());
			ClassicAssert.IsTrue(city.TryReadExact(out _, out _));
		}

		private static void Corrupt(KingdomCityBook city, string corruption)
		{
			switch (corruption)
			{
				case "null-column": city.ResidentRoofWarnedTicks = null; break;
				case "torn-column": city.ResidentRoofWarnedTicks.RemoveAt(1); break;
				case "null-name": city.ResidentNames[0] = null; break;
				case "cause-mismatch": city.ResidentCauses[0] = (int)KingdomStandingCause.Founder; break;
				case "duplicate-id": city.ResidentIds[1] = city.ResidentIds[0]; break;
				case "roof-flag": city.ResidentRoofStanding[0] = 2; break;
				case "off-ticks": city.ResidentRoofTicks[0] = 1; break;
				case "negative-clock": city.ProcessedThroughTick = -1; break;
				case "foreign-owner": city.SettlementId = KingdomIdentityRules.SettlementPrefix + new string('d', 64); break;
				case "old-schema": city.SchemaVersion = 3; break;
				case "future-schema": city.SchemaVersion = 5; break;
				case "missing-wire": city.SubsidenceModel = null; break;
				case "empty-wire": city.SubsidenceModel = ""; break;
				case "malformed-wire": city.SubsidenceModel = "not-a-subsidence-record"; break;
				case "truncated-wire": city.SubsidenceModel = city.SubsidenceModel.Substring(0, 30); break;
				case "future-wire": city.SubsidenceModel = "ss2:new"; break;
				default: Assert.Fail("Unknown fixture corruption: " + corruption); break;
			}
			if (corruption.EndsWith("-wire", StringComparison.Ordinal))
				city.ResidentRoofWarnedTicks = null;
		}

		private static KingdomCityBook Frozen(string phase, bool standing)
		{
			KingdomCityBook city = Ordinary(standing);
			KingdomSubsidenceStepBook book = RungFixture.Settling(false);
			KingdomSubsidenceRungWork work = RungFixture.Work(roofs: new[] { RungFixture.Roof(11, standing) });
			KingdomSubsidenceRungPlan plan = new KingdomSubsidenceRungPlan(book.Active.Id, book.RealmId,
				book.SettlementId, RungFixture.Zone, GrowthStage.City, GrowthStage.Town,
				book.Active.DueTick, RungFixture.Prepared, book.Active.Completed, new[] { work });
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryFreezeRungPlan(book, plan, out book));
			if (phase != "prepared")
			{
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungWear(book, 0, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungWear(book, 0, true, true, work.AfterWear, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryArmRungRoof(book, 0, 0, out book));
			}
			if (phase == "roof-proved")
			{
				city.ResidentRoofStanding[0] = 1;
				city.ResidentRoofTicks[0] = standing ? RungFixture.Due + 10 : RungFixture.Due;
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryProveRungRoof(book, 0, 0, true, true,
					city.ResidentRoofTicks[0], city.ResidentRoofWarnedTicks[0], out book));
			}
			if (phase == "quarantined") book = book.With(book.Active.Copy(
				phase: KingdomSubsidenceStepPhase.Quarantined, fault: "frozen-load-fixture"), book.Sequence);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire));
			city.SubsidenceModel = wire;
			ClassicAssert.IsTrue(city.HasValidSubsidenceStorage());
			return city;
		}

		private static KingdomCityBook Ordinary(bool standing)
		{
			List<KingdomResidentRow> rows = new List<KingdomResidentRow>();
			for (int id = 11; id <= 12; id++) rows.Add(new KingdomResidentRow(id, "Frozen load resident " + id,
				0, 0, 1, KingdomCityRules.StableId(RungFixture.ObjectId(0)), 0, 0, KingdomDayShape.Field,
				KingdomResidentStanding.Resident, KingdomStandingCause.None, RungFixture.Zone,
				new KingdomBrinkWindow(standing && id == 11, standing && id == 11 ? RungFixture.Due + 10 : 0,
					standing && id == 11 ? RungFixture.Due + 20 : 0), new KingdomBrinkWindow(false, 0, 0),
				null, 0, null, "fixture", ""));
			KingdomStocks stocks = new KingdomStocks(new KingdomStockPair(0, 0),
				new KingdomStockPair(0, 0), new KingdomStockPair(0, 0));
			ClassicAssert.IsTrue(KingdomCityState.TryCreate(KingdomCityRules.SchemaVersion, KingdomCityRules.RulesVersion,
				RungFixture.Settlement, 777, stocks, new KingdomZoneRow[0], new KingdomWorkRow[0],
				rows.ToArray(), new KingdomClockRow[0], out KingdomCityState state, out _));
			KingdomCityBook city = new KingdomCityBook();
			ClassicAssert.IsTrue(city.TryPublish(state, out _));
			ClassicAssert.IsTrue(city.TryReadExact(out _, out _));
			return city;
		}

		private sealed class NamedPayload
		{
			private readonly Dictionary<FieldInfo, object> fields = new Dictionary<FieldInfo, object>();
			private readonly Dictionary<FieldInfo, object[]> contents = new Dictionary<FieldInfo, object[]>();
			internal NamedPayload(KingdomCityBook city)
			{
				foreach (FieldInfo field in typeof(KingdomCityBook).GetFields(BindingFlags.Public | BindingFlags.Instance))
				{
					ClassicAssert.IsFalse(Attribute.IsDefined(field, typeof(NonSerializedAttribute)), field.Name);
					object value = field.GetValue(city);
					fields.Add(field, value);
					if (value is IList list)
					{
						object[] copy = new object[list.Count];
						list.CopyTo(copy, 0); contents.Add(field, copy);
					}
				}
			}
			internal void ReadInto(KingdomCityBook target)
			{
				foreach (KeyValuePair<FieldInfo, object> field in fields) field.Key.SetValue(target, field.Value);
			}
			internal void Unchanged(KingdomCityBook city)
			{
				foreach (KeyValuePair<FieldInfo, object> field in fields)
				{
					object actual = field.Key.GetValue(city);
					if (field.Key.FieldType.IsValueType || field.Key.FieldType == typeof(string))
						ClassicAssert.AreEqual(field.Value, actual, field.Key.Name);
					else ClassicAssert.AreSame(field.Value, actual, field.Key.Name + " carrier reference");
					if (contents.TryGetValue(field.Key, out object[] expected))
						CollectionAssert.AreEqual(expected, (IList)actual, field.Key.Name + " raw values");
				}
			}
		}
	}
}
#endif
