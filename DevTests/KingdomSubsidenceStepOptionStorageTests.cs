#if TAF_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomSubsidenceStepOptionStorageTests
	{
		private const long Anchor = 500;
		private const long Due = Anchor + KingdomSubsidenceStepRules.StepTicks;
		private const long Late = Due + 500;
		private static readonly string Realm = KingdomIdentityRules.RealmPrefix + new string('a', 64);
		private static readonly string Settlement = KingdomIdentityRules.SettlementPrefix + new string('b', 64);

		[TestCase("ss1:new")] [TestCase("ss1:legacy")]
		public void ExplicitUnadmittedRecordsKeepTheirOriginalWireAndNoOptionDefault(string wire)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook book));
			Assert.AreEqual("so1:none", book.OptionModel);
			Assert.AreEqual(KingdomSubsidenceBatchRules.None, book.BatchModel);
			Assert.AreEqual(wire, Wire(book));
			Assert.AreEqual(wire, Wire(book.With(null, 0)));
		}

		[TestCase(false)] [TestCase(true)]
		public void EmbeddedIntentAndExactPendingDepartureSurviveCurrentBookRoundTrip(bool pending)
		{
			KingdomSubsidenceStepBook original = Begin(2);
			if (pending) Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(original, Departure(), out original));
			string before = Wire(original);
			KingdomSubsidenceStepBook stored = WithIntent(original, out KingdomSubsidenceOptionIntent intent);
			Assert.AreNotSame(original, stored);
			Assert.AreSame(original.Active, stored.Active);
			Assert.AreEqual(KingdomSubsidenceStepRules.NoOption, original.OptionModel);
			Assert.AreEqual(before, Wire(original));
			KingdomSubsidenceStepBook restored = RoundTrip(stored);
			Assert.AreEqual(stored.OptionModel, restored.OptionModel);
			Assert.AreEqual(stored.RealmId, restored.RealmId); Assert.AreEqual(stored.SettlementId, restored.SettlementId);
			Assert.AreEqual(stored.Sequence, restored.Sequence); Assert.AreEqual(stored.LastRetiredTick, restored.LastRetiredTick);
			Assert.AreEqual(original.Active.Id, restored.Active.Id);
			Assert.AreEqual(original.Active.PendingDepartureId, restored.Active.PendingDepartureId);
			if (pending) Assert.IsTrue(restored.Active.PendingIdentity.Matches(Departure()));
			else Assert.IsNull(restored.Active.PendingIdentity);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryDecode(restored.OptionModel,
				out KingdomSubsidenceOptionIntent decoded));
			Assert.AreEqual(intent.PriorPresent, decoded.PriorPresent); Assert.AreEqual(intent.PriorWire, decoded.PriorWire);
			Assert.AreEqual(intent.NextWire, decoded.NextWire); Assert.AreEqual(intent.BeforeTick, decoded.BeforeTick);
			Assert.AreEqual(intent.Sequence, decoded.Sequence); Assert.AreEqual(intent.RetiredTick, decoded.RetiredTick);
			Assert.AreEqual(intent.StepId, decoded.StepId); Assert.AreEqual(intent.StepDueTick, decoded.StepDueTick);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(decoded, restored));
			using (BinaryReader reader = new BinaryReader(new MemoryStream(Bytes(Wire(restored))), Encoding.UTF8))
			{
				Assert.AreEqual(0x35535354, reader.ReadInt32());
				Assert.AreEqual(Realm, reader.ReadString()); Assert.AreEqual(Settlement, reader.ReadString());
				Assert.AreEqual(restored.Sequence, reader.ReadInt64()); Assert.AreEqual(restored.LastRetiredTick, reader.ReadInt64());
				Assert.AreEqual(stored.OptionModel, reader.ReadString(), "ss3 retains the intent slot after LastRetiredTick");
				Assert.AreEqual(KingdomSubsidenceBatchRules.None, reader.ReadString(), "ss3 then stores the batch slot");
				Assert.AreEqual(KingdomSubsidenceReportArchive.None, reader.ReadString(), "ss4 retains failed reports separately");
				Assert.AreEqual(KingdomSubsidenceAnnouncementCodec.None, reader.ReadString(), "ss5 stores announcement identity separately");
				Assert.AreEqual(1, reader.ReadByte());
			}
		}

		[TestCase(0, 2)] [TestCase(1, 2)] [TestCase(1, 1)]
		public void CancellationAndRetirementRetainIntentUntilSeparateOptionPublication(int completed, int quota)
		{
			KingdomSubsidenceStepBook book = Begin(quota);
			KingdomResidentDepartureOperation departure = Departure();
			if (completed != 0) Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			book = WithIntent(book, out KingdomSubsidenceOptionIntent intent);
			string option = book.OptionModel;
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Late, 2, out book));
			Assert.AreEqual(option, RoundTrip(book).OptionModel);
			if (completed != 0)
			{
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.City, out book));
				Assert.AreEqual(option, RoundTrip(book).OptionModel);
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			}
			long expected = completed == quota ? Due : Late;
			Assert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long checkpoint));
			Assert.AreEqual(expected, checkpoint);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, checkpoint, out book));
			book = RoundTrip(book);
			Assert.IsNull(book.Active); Assert.AreEqual(expected, book.LastRetiredTick);
			Assert.AreEqual(option, book.OptionModel);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, book));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, book, checkpoint, out long optionTick));
			Assert.AreEqual(Late, optionTick, "option anchoring is distinct from a fully earned due-tick receipt");
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, Late,
				Late + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, 2, out KingdomSubsidenceStepBook refused, 0, "water"));
			Assert.IsNull(refused);
			KingdomSubsidenceStepBook cleared = book.WithOption(KingdomSubsidenceStepRules.NoOption);
			Assert.AreEqual(option, book.OptionModel);
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(cleared, Late,
				Late + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, 2, out _, 0, "water"));
		}

		[Test]
		public void OpenIntentBlocksFirstStepEvenWhenThereIsNoActiveOrRetiredStep()
		{
			KingdomSubsidenceStepBook book = WithIntent(Admitted(), out _);
			Assert.IsNull(book.Active); Assert.AreEqual(0, book.Sequence);
			string before = Wire(book);
			Assert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, Anchor, Due, GrowthStage.City,
				1, out KingdomSubsidenceStepBook refused, 0, "water"));
			Assert.IsNull(refused); Assert.AreEqual(before, Wire(book));
		}

		[Test]
		public void CanonicalIntentForAnotherSequenceCannotBecomeThisBooksAuthority()
		{
			KingdomSubsidenceStepBook original = Begin(2);
			KingdomSubsidenceStepBook stored = WithIntent(original, out KingdomSubsidenceOptionIntent intent);
			KingdomSubsidenceOptionIntent foreign = new KingdomSubsidenceOptionIntent(intent.PriorPresent,
				intent.PriorWire, intent.NextWire, intent.BeforeTick, intent.Sequence + 1, intent.RetiredTick,
				intent.StepId, intent.StepDueTick);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(foreign, out string foreignWire));
			RefusesBook(original.WithOption(foreignWire));
			RefusesWire(Frame(original, false, foreignWire));
			Assert.IsTrue(KingdomSubsidenceStepRules.Valid(stored));
			Assert.AreEqual(KingdomSubsidenceStepRules.NoOption, original.OptionModel);
		}

		[TestCase(null)] [TestCase("")] [TestCase("broken")]
		[TestCase("so1:")] [TestCase("so2:none")] [TestCase("so1:none ")]
		public void StoredAbsentEmptyOrMalformedOptionNeverDefaultsToNoIntent(string option)
		{
			KingdomSubsidenceStepBook original = Admitted();
			RefusesBook(original.WithOption(option));
			if (option != null) RefusesWire(Frame(original, false, option));
			Assert.AreEqual(KingdomSubsidenceStepRules.NoOption, original.OptionModel);
		}

		[TestCase("admitted")] [TestCase("pending")] [TestCase("retired")]
		public void ExplicitLegacyAdmittedFramesUpgradeToCurrentWithoutInventingOptionAuthority(string state)
		{
			KingdomSubsidenceStepBook original = LegacyBook(state);
			string legacy = Frame(original, true, null);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(legacy, out KingdomSubsidenceStepBook decoded));
			Assert.AreEqual(KingdomSubsidenceStepRules.NoOption, decoded.OptionModel);
			Assert.AreEqual(KingdomSubsidenceBatchRules.None, decoded.BatchModel);
			Assert.AreEqual(original.Sequence, decoded.Sequence); Assert.AreEqual(original.LastRetiredTick, decoded.LastRetiredTick);
			if (state == "pending") Assert.IsTrue(decoded.Active.PendingIdentity.Matches(Departure()));
			else Assert.IsNull(decoded.Active);
			string upgraded = Wire(decoded);
			Assert.IsTrue(upgraded.StartsWith("ss5:", StringComparison.Ordinal));
			Assert.AreEqual(Wire(original), upgraded);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(
				Frame(original, false, KingdomSubsidenceStepRules.NoOption), out KingdomSubsidenceStepBook ss2));
			Assert.AreEqual(upgraded, Wire(ss2));
			Assert.AreEqual(legacy, Frame(decoded, true, null));
			Assert.AreNotEqual(legacy, upgraded);
			Assert.AreEqual(upgraded, Wire(RoundTrip(decoded)));
		}

		[TestCase("admitted")] [TestCase("pending")] [TestCase("retired")]
		[TestCase("partial-rung")] [TestCase("settling-rung")]
		public void HistoricalSs2BytesPreserveIntentCreditsAndOwedRungWithoutInventingBatch(string state)
		{
			KingdomSubsidenceStepBook original;
			bool rung = state == "partial-rung" || state == "settling-rung";
			if (rung)
			{
				original = Begin(state == "partial-rung" ? 2 : 1);
				Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(original, Departure(), out original));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCredit(original, Departure().OperationId,
					GrowthStage.City - 1, out original));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(original, Departure().OperationId, out original));
			}
			else original = LegacyBook(state);
			original = WithIntent(original, out _, state == "retired" ? Late : Anchor);
			// Historical writer has neither ss3 slot; migration input never uses the current encoder.
			string historical = Frame(original, false, original.OptionModel);
			Assert.IsTrue(historical.StartsWith("ss2:", StringComparison.Ordinal));
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(historical, out KingdomSubsidenceStepBook migrated));
			Assert.AreEqual(KingdomSubsidenceBatchRules.None, migrated.BatchModel);
			Assert.AreEqual(original.OptionModel, migrated.OptionModel);
			Assert.AreEqual(historical, Frame(migrated, false, migrated.OptionModel), "every historical field survives");
			if (rung)
			{
				Assert.AreEqual(KingdomSubsidenceBatchRules.PendingReport, migrated.Active.RungReportModel);
				Assert.AreEqual(KingdomSubsidenceStepRules.UnplannedRungs, migrated.Active.RungModel);
				Assert.AreEqual(1, migrated.Active.Completed);
				Assert.AreEqual(state == "partial-rung" ? KingdomSubsidenceStepPhase.Departing
					: KingdomSubsidenceStepPhase.Settling, migrated.Active.Phase);
				Assert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(migrated, Anchor, out _));
			}
			else if (migrated.Active != null)
			{
				Assert.IsTrue(migrated.Active.PendingIdentity.Matches(Departure()));
				Assert.AreEqual(KingdomSubsidenceBatchRules.NoReport, migrated.Active.RungReportModel);
			}
			string upgraded = Wire(migrated);
			Assert.IsTrue(upgraded.StartsWith("ss5:", StringComparison.Ordinal));
			Assert.AreEqual(Wire(original), upgraded);
			Assert.AreNotEqual(historical, upgraded);
			Assert.AreEqual(upgraded, Wire(RoundTrip(migrated)));
			Assert.AreEqual(historical, Frame(original, false, original.OptionModel));
		}

		[TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
		public void AllAdmittedFormatsRejectEveryByteTruncationAndNoncanonicalEnvelope(int version)
		{
			KingdomSubsidenceStepBook book = LegacyBook("pending");
			string wire = version == 5 ? Wire(book) : Frame(book, version == 1,
				KingdomSubsidenceStepRules.NoOption, version >= 3, version == 4);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out _));
			byte[] full = Bytes(wire);
			string prefix = "ss" + version + ":";
			for (int length = 0; length < full.Length; length++)
				RefusesWire(prefix + Convert.ToBase64String(full, 0, length));
			byte[] trailing = new byte[full.Length + 1];
			Array.Copy(full, trailing, full.Length);
			RefusesWire(prefix + Convert.ToBase64String(trailing));
			foreach (string other in new[] { "ss1:", "ss2:", "ss3:", "ss4:", "ss5:" })
				if (other != prefix) RefusesWire(other + wire.Substring(4));
			RefusesWire(wire.Insert(12, "\n"));
			RefusesWire(prefix + "!");
			full[0] ^= 1;
			RefusesWire(prefix + Convert.ToBase64String(full));
		}

		[TestCase("admitted")] [TestCase("pending")] [TestCase("retired")]
		public void HistoricalSs3HasNoFailureArchiveAndUpgradesWithoutLosingItsFields(string state)
		{
			KingdomSubsidenceStepBook prior = LegacyBook(state);
			string historical = Frame(prior, false, prior.OptionModel, true);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(historical, out KingdomSubsidenceStepBook next));
			Assert.AreEqual(KingdomSubsidenceReportArchive.None, next.FailureModel);
			Assert.AreEqual(historical, Frame(next, false, next.OptionModel, true));
			Assert.AreEqual(Wire(prior), Wire(next));
		}

		private static KingdomSubsidenceStepBook Admitted()
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			Assert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(fresh, Realm, Settlement, out KingdomSubsidenceStepBook book));
			return book;
		}
		private static KingdomSubsidenceStepBook Begin(int quota)
		{
			Assert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), Anchor, Due,
				GrowthStage.City, quota, out KingdomSubsidenceStepBook book, 0, "water"));
			return book;
		}
		private static KingdomResidentDepartureOperation Departure()
		{
			KingdomResidentDepartureOperation op = new KingdomResidentDepartureOperation
			{
				Version = KingdomResidentDepartureOperation.CurrentVersion, Revision = 1,
				Phase = (int)KingdomResidentDeparturePhase.Prepared, RealmId = Realm, SettlementId = Settlement,
				ResidentId = 7, BodyObjectId = "step-option-body", ZoneId = "JoppaWorld.12.24.1.1.10",
				ResidentName = "Step option fixture", PreparedTick = Due,
				OperationId = KingdomResidentDepartureRules.Id(Realm, Settlement, 7, "step-option-body", Due)
			};
			Assert.IsTrue(KingdomResidentDepartureRules.Valid(op)); return op;
		}
		private static KingdomSubsidenceStepBook WithIntent(KingdomSubsidenceStepBook book,
			out KingdomSubsidenceOptionIntent intent, long beforeTick = Anchor)
		{
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(
				new KingdomDurableKeyObservation { HasString = true, String = "v1|E|100|2" },
				false, 2, Late, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			Assert.IsTrue(decision.Valid);
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(book, beforeTick, snapshot, out intent));
			Assert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(intent, out string option));
			KingdomSubsidenceStepBook result = book.WithOption(option);
			Assert.IsTrue(KingdomSubsidenceStepRules.Valid(result)); return result;
		}
		private static KingdomSubsidenceStepBook LegacyBook(string state)
		{
			if (state == "admitted") return Admitted();
			KingdomSubsidenceStepBook book = Begin(2);
			if (state == "pending") Assert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, Departure(), out book));
			else
			{
				Assert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Late, 2, out book));
				Assert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, Late, out book));
			}
			return book;
		}
		private static string Wire(KingdomSubsidenceStepBook book)
		{
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire)); return wire;
		}
		private static KingdomSubsidenceStepBook RoundTrip(KingdomSubsidenceStepBook book)
		{
			string wire = Wire(book);
			Assert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook restored));
			Assert.AreEqual(wire, Wire(restored)); return restored;
		}
		private static byte[] Bytes(string wire) => Convert.FromBase64String(wire.Substring(4));
		private static void RefusesBook(KingdomSubsidenceStepBook book)
		{
			Assert.IsFalse(KingdomSubsidenceStepRules.Valid(book));
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book, out string wire)); Assert.IsNull(wire);
		}
		private static void RefusesWire(string wire)
		{
			Assert.IsFalse(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook book), wire);
			Assert.IsNull(book);
		}

		// Independently framed historical ss1–ss4; none includes the ss5 announcement slot.
		private static string Frame(KingdomSubsidenceStepBook book, bool legacy, string option,
			bool batchFormat = false, bool failureFormat = false)
		{
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
			{
				writer.Write(legacy ? 0x31535354 : failureFormat ? 0x34535354 : batchFormat ? 0x33535354 : 0x32535354);
				writer.Write(book.RealmId); writer.Write(book.SettlementId);
				writer.Write(book.Sequence); writer.Write(book.LastRetiredTick);
				if (!legacy) writer.Write(option);
				if (batchFormat) writer.Write(book.BatchModel);
				if (failureFormat) writer.Write(book.FailureModel);
				writer.Write((byte)(book.Active == null ? 0 : 1));
				KingdomSubsidenceStepOperation op = book.Active;
				if (op != null)
				{
					writer.Write(op.Id); writer.Write(op.AnchorTick); writer.Write(op.DueTick);
					writer.Write((byte)op.FromStage); writer.Write((byte)op.ReachedStage);
					writer.Write(op.Quota); writer.Write(op.Completed); writer.Write((byte)op.Phase);
					writer.Write(op.PendingDepartureId); writer.Write((byte)(op.PendingCredited ? 1 : 0));
					writer.Write((byte)(op.CancelRequested ? 1 : 0)); writer.Write(op.CancelTick); writer.Write(op.CancelToken);
					writer.Write(op.RungModel); writer.Write(op.Fault); writer.Write(op.CreditedDepartureIds);
					writer.Write(op.StorageCapacity); writer.Write(op.BindingSupport); writer.Write(op.LastActivityTick);
					if (op.PendingIdentity != null)
					{
						writer.Write(op.PendingIdentity.ResidentId); writer.Write(op.PendingIdentity.BodyObjectId);
						writer.Write(op.PendingIdentity.ZoneId); writer.Write(op.PendingIdentity.PreparedTick);
					}
					if (batchFormat) writer.Write(op.RungReportModel);
				}
				writer.Flush(); return (legacy ? "ss1:" : failureFormat ? "ss4:" : batchFormat ? "ss3:" : "ss2:") + Convert.ToBase64String(stream.ToArray());
			}
		}
	}
}
#endif
