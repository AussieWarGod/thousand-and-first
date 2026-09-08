#if TAF_TESTS
using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook book));
			ClassicAssert.AreEqual("so1:none", book.OptionModel);
			ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.None, book.BatchModel);
			ClassicAssert.AreEqual(wire, Wire(book));
			ClassicAssert.AreEqual(wire, Wire(book.With(null, 0)));
		}

		[TestCase(false)] [TestCase(true)]
		public void EmbeddedIntentAndExactPendingDepartureSurviveCurrentBookRoundTrip(bool pending)
		{
			KingdomSubsidenceStepBook original = Begin(2);
			if (pending) ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(original, Departure(), out original));
			string before = Wire(original);
			KingdomSubsidenceStepBook stored = WithIntent(original, out KingdomSubsidenceOptionIntent intent);
			ClassicAssert.AreNotSame(original, stored);
			ClassicAssert.AreSame(original.Active, stored.Active);
			ClassicAssert.AreEqual(KingdomSubsidenceStepRules.NoOption, original.OptionModel);
			ClassicAssert.AreEqual(before, Wire(original));
			KingdomSubsidenceStepBook restored = RoundTrip(stored);
			ClassicAssert.AreEqual(stored.OptionModel, restored.OptionModel);
			ClassicAssert.AreEqual(stored.RealmId, restored.RealmId); ClassicAssert.AreEqual(stored.SettlementId, restored.SettlementId);
			ClassicAssert.AreEqual(stored.Sequence, restored.Sequence); ClassicAssert.AreEqual(stored.LastRetiredTick, restored.LastRetiredTick);
			ClassicAssert.AreEqual(original.Active.Id, restored.Active.Id);
			ClassicAssert.AreEqual(original.Active.PendingDepartureId, restored.Active.PendingDepartureId);
			if (pending) ClassicAssert.IsTrue(restored.Active.PendingIdentity.Matches(Departure()));
			else ClassicAssert.IsNull(restored.Active.PendingIdentity);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryDecode(restored.OptionModel,
				out KingdomSubsidenceOptionIntent decoded));
			ClassicAssert.AreEqual(intent.PriorPresent, decoded.PriorPresent); ClassicAssert.AreEqual(intent.PriorWire, decoded.PriorWire);
			ClassicAssert.AreEqual(intent.NextWire, decoded.NextWire); ClassicAssert.AreEqual(intent.BeforeTick, decoded.BeforeTick);
			ClassicAssert.AreEqual(intent.Sequence, decoded.Sequence); ClassicAssert.AreEqual(intent.RetiredTick, decoded.RetiredTick);
			ClassicAssert.AreEqual(intent.StepId, decoded.StepId); ClassicAssert.AreEqual(intent.StepDueTick, decoded.StepDueTick);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(decoded, restored));
			using (BinaryReader reader = new BinaryReader(new MemoryStream(Bytes(Wire(restored))), Encoding.UTF8))
			{
				ClassicAssert.AreEqual(0x35535354, reader.ReadInt32());
				ClassicAssert.AreEqual(Realm, reader.ReadString()); ClassicAssert.AreEqual(Settlement, reader.ReadString());
				ClassicAssert.AreEqual(restored.Sequence, reader.ReadInt64()); ClassicAssert.AreEqual(restored.LastRetiredTick, reader.ReadInt64());
				ClassicAssert.AreEqual(stored.OptionModel, reader.ReadString(), "ss3 retains the intent slot after LastRetiredTick");
				ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.None, reader.ReadString(), "ss3 then stores the batch slot");
				ClassicAssert.AreEqual(KingdomSubsidenceReportArchive.None, reader.ReadString(), "ss4 retains failed reports separately");
				ClassicAssert.AreEqual(KingdomSubsidenceAnnouncementCodec.None, reader.ReadString(), "ss5 stores announcement identity separately");
				ClassicAssert.AreEqual(1, reader.ReadByte());
			}
		}

		[TestCase(0, 2)] [TestCase(1, 2)] [TestCase(1, 1)]
		public void CancellationAndRetirementRetainIntentUntilSeparateOptionPublication(int completed, int quota)
		{
			KingdomSubsidenceStepBook book = Begin(quota);
			KingdomResidentDepartureOperation departure = Departure();
			if (completed != 0) ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, departure, out book));
			book = WithIntent(book, out KingdomSubsidenceOptionIntent intent);
			string option = book.OptionModel;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Late, 2, out book));
			ClassicAssert.AreEqual(option, RoundTrip(book).OptionModel);
			if (completed != 0)
			{
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(book, departure.OperationId, GrowthStage.City, out book));
				ClassicAssert.AreEqual(option, RoundTrip(book).OptionModel);
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(book, departure.OperationId, out book));
			}
			long expected = completed == quota ? Due : Late;
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCheckpoint(book, Anchor, out long checkpoint));
			ClassicAssert.AreEqual(expected, checkpoint);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, checkpoint, out book));
			book = RoundTrip(book);
			ClassicAssert.IsNull(book.Active); ClassicAssert.AreEqual(expected, book.LastRetiredTick);
			ClassicAssert.AreEqual(option, book.OptionModel);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.Matches(intent, book));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryCheckpoint(intent, book, checkpoint, out long optionTick));
			ClassicAssert.AreEqual(Late, optionTick, "option anchoring is distinct from a fully earned due-tick receipt");
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, Late,
				Late + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, 2, out KingdomSubsidenceStepBook refused, 0, "water"));
			ClassicAssert.IsNull(refused);
			KingdomSubsidenceStepBook cleared = book.WithOption(KingdomSubsidenceStepRules.NoOption);
			ClassicAssert.AreEqual(option, book.OptionModel);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(cleared, Late,
				Late + KingdomSubsidenceStepRules.StepTicks, GrowthStage.City, 2, out _, 0, "water"));
		}

		[Test]
		public void OpenIntentBlocksFirstStepEvenWhenThereIsNoActiveOrRetiredStep()
		{
			KingdomSubsidenceStepBook book = WithIntent(Admitted(), out _);
			ClassicAssert.IsNull(book.Active); ClassicAssert.AreEqual(0, book.Sequence);
			string before = Wire(book);
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryBegin(book, Anchor, Due, GrowthStage.City,
				1, out KingdomSubsidenceStepBook refused, 0, "water"));
			ClassicAssert.IsNull(refused); ClassicAssert.AreEqual(before, Wire(book));
		}

		[Test]
		public void CanonicalIntentForAnotherSequenceCannotBecomeThisBooksAuthority()
		{
			KingdomSubsidenceStepBook original = Begin(2);
			KingdomSubsidenceStepBook stored = WithIntent(original, out KingdomSubsidenceOptionIntent intent);
			KingdomSubsidenceOptionIntent foreign = new KingdomSubsidenceOptionIntent(intent.PriorPresent,
				intent.PriorWire, intent.NextWire, intent.BeforeTick, intent.Sequence + 1, intent.RetiredTick,
				intent.StepId, intent.StepDueTick);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(foreign, out string foreignWire));
			RefusesBook(original.WithOption(foreignWire));
			RefusesWire(Frame(original, false, foreignWire));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.Valid(stored));
			ClassicAssert.AreEqual(KingdomSubsidenceStepRules.NoOption, original.OptionModel);
		}

		[TestCase(null)] [TestCase("")] [TestCase("broken")]
		[TestCase("so1:")] [TestCase("so2:none")] [TestCase("so1:none ")]
		public void StoredAbsentEmptyOrMalformedOptionNeverDefaultsToNoIntent(string option)
		{
			KingdomSubsidenceStepBook original = Admitted();
			RefusesBook(original.WithOption(option));
			if (option != null) RefusesWire(Frame(original, false, option));
			ClassicAssert.AreEqual(KingdomSubsidenceStepRules.NoOption, original.OptionModel);
		}

		[TestCase("admitted")] [TestCase("pending")] [TestCase("retired")]
		public void ExplicitLegacyAdmittedFramesUpgradeToCurrentWithoutInventingOptionAuthority(string state)
		{
			KingdomSubsidenceStepBook original = LegacyBook(state);
			string legacy = Frame(original, true, null);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(legacy, out KingdomSubsidenceStepBook decoded));
			ClassicAssert.AreEqual(KingdomSubsidenceStepRules.NoOption, decoded.OptionModel);
			ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.None, decoded.BatchModel);
			ClassicAssert.AreEqual(original.Sequence, decoded.Sequence); ClassicAssert.AreEqual(original.LastRetiredTick, decoded.LastRetiredTick);
			if (state == "pending") ClassicAssert.IsTrue(decoded.Active.PendingIdentity.Matches(Departure()));
			else ClassicAssert.IsNull(decoded.Active);
			string upgraded = Wire(decoded);
			ClassicAssert.IsTrue(upgraded.StartsWith("ss5:", StringComparison.Ordinal));
			ClassicAssert.AreEqual(Wire(original), upgraded);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(
				Frame(original, false, KingdomSubsidenceStepRules.NoOption), out KingdomSubsidenceStepBook ss2));
			ClassicAssert.AreEqual(upgraded, Wire(ss2));
			ClassicAssert.AreEqual(legacy, Frame(decoded, true, null));
			ClassicAssert.AreNotEqual(legacy, upgraded);
			ClassicAssert.AreEqual(upgraded, Wire(RoundTrip(decoded)));
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
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(original, Departure(), out original));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCredit(original, Departure().OperationId,
					GrowthStage.City - 1, out original));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryReleaseRetired(original, Departure().OperationId, out original));
			}
			else original = LegacyBook(state);
			original = WithIntent(original, out _, state == "retired" ? Late : Anchor);
			// Historical writer has neither ss3 slot; migration input never uses the current encoder.
			string historical = Frame(original, false, original.OptionModel);
			ClassicAssert.IsTrue(historical.StartsWith("ss2:", StringComparison.Ordinal));
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(historical, out KingdomSubsidenceStepBook migrated));
			ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.None, migrated.BatchModel);
			ClassicAssert.AreEqual(original.OptionModel, migrated.OptionModel);
			ClassicAssert.AreEqual(historical, Frame(migrated, false, migrated.OptionModel), "every historical field survives");
			if (rung)
			{
				ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.PendingReport, migrated.Active.RungReportModel);
				ClassicAssert.AreEqual(KingdomSubsidenceStepRules.UnplannedRungs, migrated.Active.RungModel);
				ClassicAssert.AreEqual(1, migrated.Active.Completed);
				ClassicAssert.AreEqual(state == "partial-rung" ? KingdomSubsidenceStepPhase.Departing
					: KingdomSubsidenceStepPhase.Settling, migrated.Active.Phase);
				ClassicAssert.IsFalse(KingdomSubsidenceStepRules.TryCheckpoint(migrated, Anchor, out _));
			}
			else if (migrated.Active != null)
			{
				ClassicAssert.IsTrue(migrated.Active.PendingIdentity.Matches(Departure()));
				ClassicAssert.AreEqual(KingdomSubsidenceBatchRules.NoReport, migrated.Active.RungReportModel);
			}
			string upgraded = Wire(migrated);
			ClassicAssert.IsTrue(upgraded.StartsWith("ss5:", StringComparison.Ordinal));
			ClassicAssert.AreEqual(Wire(original), upgraded);
			ClassicAssert.AreNotEqual(historical, upgraded);
			ClassicAssert.AreEqual(upgraded, Wire(RoundTrip(migrated)));
			ClassicAssert.AreEqual(historical, Frame(original, false, original.OptionModel));
		}

		[TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
		public void AllAdmittedFormatsRejectEveryByteTruncationAndNoncanonicalEnvelope(int version)
		{
			KingdomSubsidenceStepBook book = LegacyBook("pending");
			string wire = version == 5 ? Wire(book) : Frame(book, version == 1,
				KingdomSubsidenceStepRules.NoOption, version >= 3, version == 4);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out _));
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
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(historical, out KingdomSubsidenceStepBook next));
			ClassicAssert.AreEqual(KingdomSubsidenceReportArchive.None, next.FailureModel);
			ClassicAssert.AreEqual(historical, Frame(next, false, next.OptionModel, true));
			ClassicAssert.AreEqual(Wire(prior), Wire(next));
		}

		private static KingdomSubsidenceStepBook Admitted()
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode("ss1:new", out KingdomSubsidenceStepBook fresh));
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAdmit(fresh, Realm, Settlement, out KingdomSubsidenceStepBook book));
			return book;
		}
		private static KingdomSubsidenceStepBook Begin(int quota)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryBegin(Admitted(), Anchor, Due,
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
			ClassicAssert.IsTrue(KingdomResidentDepartureRules.Valid(op)); return op;
		}
		private static KingdomSubsidenceStepBook WithIntent(KingdomSubsidenceStepBook book,
			out KingdomSubsidenceOptionIntent intent, long beforeTick = Anchor)
		{
			KingdomElapsedOptionDecision decision = KingdomSubsidenceOptionRules.Observe(
				new KingdomDurableKeyObservation { HasString = true, String = "v1|E|100|2" },
				false, 2, Late, out KingdomSubsidenceOptionRules.Snapshot snapshot);
			ClassicAssert.IsTrue(decision.Valid);
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryPrepare(book, beforeTick, snapshot, out intent));
			ClassicAssert.IsTrue(KingdomSubsidenceOptionIntentRules.TryEncode(intent, out string option));
			KingdomSubsidenceStepBook result = book.WithOption(option);
			ClassicAssert.IsTrue(KingdomSubsidenceStepRules.Valid(result)); return result;
		}
		private static KingdomSubsidenceStepBook LegacyBook(string state)
		{
			if (state == "admitted") return Admitted();
			KingdomSubsidenceStepBook book = Begin(2);
			if (state == "pending") ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryAssociate(book, Departure(), out book));
			else
			{
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryCancel(book, Late, 2, out book));
				ClassicAssert.IsTrue(KingdomSubsidenceStepRules.TryRetire(book, Late, out book));
			}
			return book;
		}
		private static string Wire(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryEncode(book, out string wire)); return wire;
		}
		private static KingdomSubsidenceStepBook RoundTrip(KingdomSubsidenceStepBook book)
		{
			string wire = Wire(book);
			ClassicAssert.IsTrue(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook restored));
			ClassicAssert.AreEqual(wire, Wire(restored)); return restored;
		}
		private static byte[] Bytes(string wire) => Convert.FromBase64String(wire.Substring(4));
		private static void RefusesBook(KingdomSubsidenceStepBook book)
		{
			ClassicAssert.IsFalse(KingdomSubsidenceStepRules.Valid(book));
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryEncode(book, out string wire)); ClassicAssert.IsNull(wire);
		}
		private static void RefusesWire(string wire)
		{
			ClassicAssert.IsFalse(KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook book), wire);
			ClassicAssert.IsNull(book);
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
