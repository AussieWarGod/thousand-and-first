#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using ThousandAndFirst.Harness;

namespace ThousandAndFirst.Tests
{
	// Synthetic codec evidence only; opaque step/rung wires do not prove a native save cut.
	public sealed class KingdomSubsidenceRungSaveSnapshotTests
	{
		private const string GameId = "01234567-89ab-cdef-0123-456789abcdef";
		private const string ZoneId = "JoppaWorld.8.22.1.1.10";
		private const string StepWire = "ss5:opaque-native-owner-must-validate";
		private const string RungWire = "sr2:opaque-native-owner-must-validate";
		private const string Prefix = "taf-rung-save-v2:";
		private static readonly string[] TextFields = { "GameId", "ZoneId", "StepWire", "RungWire",
			"StepId", "TellingDigest", "ObjectId", "Blueprint", "PlotId", "DesignStamp", "IncidentId",
			"LastCompletedIncidentId", "IncidentLine", "BodyObjectId", "RoofZoneId",
			"SurvivorObject", "AbsentObject" };
		private static readonly string[] NullableTextFields = { "DesignStamp", "IncidentId",
			"LastCompletedIncidentId", "IncidentLine" };

		[TestCase(false)] [TestCase(true)]
		public void EveryScalarWorkReceiptRoofFieldAndAllFiftyPairsRoundTripExactly(bool flag)
		{
			Fixture fixture = new Fixture { Work = Work(quarantined: flag), Roof = Roof(roofStanding: flag) };
			KingdomSubsidenceRungSaveSnapshot expected = fixture.Snapshot();
			string wire = Wire(expected);
			Assert.IsTrue(wire.StartsWith(Prefix, StringComparison.Ordinal));
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.MatchesPrefix(wire));
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(wire, out KingdomSubsidenceRungSaveSnapshot actual));
			Assert.AreEqual(expected.GameId, actual.GameId); Assert.AreEqual(expected.ZoneId, actual.ZoneId);
			Assert.AreEqual(expected.StepWire, actual.StepWire); Assert.AreEqual(expected.RungWire, actual.RungWire);
			Assert.AreEqual(expected.StepId, actual.StepId); Assert.AreEqual(expected.TellingDigest, actual.TellingDigest);
			Assert.AreEqual(expected.Now, actual.Now); Assert.AreEqual(expected.AnchorTick, actual.AnchorTick);
			Assert.AreEqual(expected.DueTick, actual.DueTick); Assert.AreEqual(expected.Sequence, actual.Sequence);
			Assert.AreEqual(expected.LastSubsidenceTick, actual.LastSubsidenceTick);
			Assert.AreEqual(expected.LedgerDepartures, actual.LedgerDepartures);
			Assert.AreEqual(expected.Population, actual.Population); Assert.AreEqual(expected.Stage, actual.Stage);
			Assert.AreEqual(expected.ChronicleCount, actual.ChronicleCount); Assert.AreEqual(expected.OutsiderCount, actual.OutsiderCount);
			AssertWork(expected.Work, actual.Work); AssertRoof(expected.Roof, actual.Roof);
			Assert.AreEqual(35, actual.ResidentIds.Count); Assert.AreEqual(15, actual.AbsentResidentIds.Count);
			CollectionAssert.AreEqual(expected.ResidentIds, actual.ResidentIds);
			CollectionAssert.AreEqual(expected.ObjectIds, actual.ObjectIds);
			CollectionAssert.AreEqual(expected.AbsentResidentIds, actual.AbsentResidentIds);
			CollectionAssert.AreEqual(expected.AbsentObjectIds, actual.AbsentObjectIds);
			Assert.AreEqual(wire, Wire(actual));
		}

		[Test]
		public void CurrentEnvelopeBindsVersionTwoToSs5WithoutChangingTheRungSchema()
		{
			string wire = Wire(new Fixture().Snapshot()); byte[] bytes = Bytes(wire);
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.MatchesCurrentPrefix(wire));
			Assert.AreEqual(0x52535401, ReadInt(bytes, 0)); Assert.AreEqual(2, ReadInt(bytes, 4));
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(wire, out var snapshot));
			StringAssert.StartsWith("ss5:", snapshot.StepWire); StringAssert.StartsWith("sr2:", snapshot.RungWire);
		}

		[Test]
		public void HistoricalV1Ss4FixtureRetainsExactRoundtripButCannotClaimTheCurrentNativeVariant()
		{
			// Explicit old-format fixture, not a historical native save or installed-mod compatibility proof.
			byte[] bytes = Bytes(Wire(new Fixture().Snapshot()));
			bytes = ReplaceText(bytes, Offsets(bytes)["StepWire"], Encoding.UTF8.GetBytes("ss4:" + StepWire.Substring(4)));
			PutInteger(bytes, 4, 1, 4); string historical = "taf-rung-save-v1:" + Convert.ToBase64String(bytes);
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.MatchesPrefix(historical));
			Assert.IsFalse(KingdomSubsidenceRungSaveSnapshotCodec.MatchesCurrentPrefix(historical));
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(historical, out var snapshot));
			Assert.AreEqual("ss4:" + StepWire.Substring(4), snapshot.StepWire);
			Assert.AreEqual(historical, Wire(snapshot)); Assert.AreEqual("sr2:opaque-native-owner-must-validate", snapshot.RungWire);
		}

		[TestCase("prefix")] [TestCase("version")] [TestCase("step")]
		public void CurrentAndHistoricalEnvelopeVersionAndNestedStepCannotBeMixed(string field)
		{
			byte[] bytes = Bytes(Wire(new Fixture().Snapshot())); string prefix = Prefix;
			if (field == "prefix") prefix = "taf-rung-save-v1:";
			if (field == "version") PutInteger(bytes, 4, 1, 4);
			if (field == "step") bytes = ReplaceText(bytes, Offsets(bytes)["StepWire"], Encoding.UTF8.GetBytes("ss4:foreign"));
			RefusesWire(prefix + Convert.ToBase64String(bytes));
		}

		[Test]
		public void ConstructorCopiesAllFourArraysAndExposedCollectionsCannotChangeTheWitness()
		{
			Fixture fixture = new Fixture(); KingdomSubsidenceRungSaveSnapshot value = fixture.Snapshot();
			string before = Wire(value);
			fixture.ResidentIds[0] = 101; fixture.ObjectIds[0] = "changed-survivor";
			fixture.AbsentResidentIds[0] = 102; fixture.AbsentObjectIds[0] = "changed-absent";
			Assert.AreEqual(1, value.ResidentIds[0]); Assert.AreEqual("body-1", value.ObjectIds[0]);
			Assert.AreEqual(36, value.AbsentResidentIds[0]); Assert.AreEqual("body-36", value.AbsentObjectIds[0]);
			Assert.Throws<NotSupportedException>(() => ((IList<int>)value.ResidentIds)[0] = 101);
			Assert.Throws<NotSupportedException>(() => ((IList<string>)value.ObjectIds)[0] = "changed-survivor");
			Assert.Throws<NotSupportedException>(() => ((IList<int>)value.AbsentResidentIds)[0] = 102);
			Assert.Throws<NotSupportedException>(() => ((IList<string>)value.AbsentObjectIds)[0] = "changed-absent");
			Assert.AreEqual(before, Wire(value));
		}

		[Test]
		public void OptionalNullTextAndEmptyPlotRemainDistinctWithoutDefaulting()
		{
			KingdomSubsidenceRungSaveSnapshot value = new Fixture { Work = Work(plotId: "", designStamp: null,
				incidentId: null, lastCompletedIncidentId: null, incidentLine: null) }.Snapshot();
			string wire = Wire(value); byte[] bytes = Bytes(wire); Dictionary<string, int> offsets = Offsets(bytes);
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(wire, out KingdomSubsidenceRungSaveSnapshot read));
			Assert.AreEqual("", read.Work.PlotId); Assert.IsNull(read.Work.DesignStamp); Assert.IsNull(read.Work.IncidentId);
			Assert.IsNull(read.Work.LastCompletedIncidentId); Assert.IsNull(read.Work.IncidentLine);
			Assert.AreEqual(0, ReadInt(bytes, offsets["PlotId"]));
			foreach (string field in NullableTextFields) Assert.AreEqual(-1, ReadInt(bytes, offsets[field]), field);
			Assert.AreEqual(wire, Wire(read));
			foreach (string field in NullableTextFields) Refuses(TextField(field, ""));
			Refuses(TextField("PlotId", null));
		}

		[TestCase("ResidentIds")] [TestCase("ObjectIds")]
		[TestCase("AbsentResidentIds")] [TestCase("AbsentObjectIds")]
		public void NullArraysStayNullAndRefuseRatherThanInventingPeople(string field)
		{
			Fixture fixture = new Fixture();
			if (field == "ResidentIds") fixture.ResidentIds = null;
			if (field == "ObjectIds") fixture.ObjectIds = null;
			if (field == "AbsentResidentIds") fixture.AbsentResidentIds = null;
			if (field == "AbsentObjectIds") fixture.AbsentObjectIds = null;
			KingdomSubsidenceRungSaveSnapshot value = fixture.Snapshot();
			if (field == "ResidentIds") Assert.IsNull(value.ResidentIds);
			if (field == "ObjectIds") Assert.IsNull(value.ObjectIds);
			if (field == "AbsentResidentIds") Assert.IsNull(value.AbsentResidentIds);
			if (field == "AbsentObjectIds") Assert.IsNull(value.AbsentObjectIds);
			Refuses(value);
		}

		[Test]
		public void NullSnapshotWorkAndRoofRefuse()
		{
			Refuses(null); Refuses(new Fixture { Work = null }.Snapshot()); Refuses(new Fixture { Roof = null }.Snapshot());
		}

		[TestCase(null)] [TestCase("")] [TestCase("01234567-89AB-CDEF-0123-456789ABCDEF")]
		[TestCase("0123456789abcdef0123456789abcdef")] [TestCase("{01234567-89ab-cdef-0123-456789abcdef}")]
		public void GameIdRequiresCanonicalLowercaseGuidD(string gameId) { Refuses(new Fixture { Game = gameId }.Snapshot()); }

		[TestCase(null)] [TestCase("")] [TestCase("abcdef")] [TestCase("ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcd")]
		public void TellingDigestRequiresExactlySixtyFourLowercaseHexCharacters(string digest)
		{
			Refuses(new Fixture { TellingDigest = digest }.Snapshot());
			Refuses(new Fixture { TellingDigest = new string('g', 64) }.Snapshot());
		}

		[Test]
		public void RequiredTextRejectsNullAndEmpty()
		{
			foreach (string field in TextFields)
			{
				if (Array.IndexOf(NullableTextFields, field) >= 0 || field == "PlotId") continue;
				Refuses(TextField(field, null)); Refuses(TextField(field, ""));
			}
		}

		[TestCase("StepWire", "ss3:old")] [TestCase("StepWire", "SS5:wrong-case")]
		[TestCase("StepWire", "opaque")] [TestCase("RungWire", "sr1:old")]
		[TestCase("RungWire", "SR2:wrong-case")] [TestCase("RungWire", "opaque")]
		public void OpaqueNestedWiresStillRequireTheirExactVariantPrefix(string field, string value) { Refuses(TextField(field, value)); }

		[TestCase("ZoneId", 128)] [TestCase("StepWire", 131072)] [TestCase("RungWire", 65536)]
		[TestCase("StepId", 512)] [TestCase("ObjectId", 512)] [TestCase("Blueprint", 256)]
		[TestCase("PlotId", 512)] [TestCase("DesignStamp", 512)] [TestCase("IncidentId", 512)]
		[TestCase("LastCompletedIncidentId", 512)] [TestCase("IncidentLine", 65536)]
		[TestCase("RoofZoneId", 128)] [TestCase("SurvivorObject", 512)] [TestCase("AbsentObject", 512)]
		public void TextBoundsAcceptExactLimitAndRejectOneMoreWithoutTruncation(string field, int maximum)
		{
			string prefix = field == "StepWire" ? "ss5:" : field == "RungWire" ? "sr2:" : "";
			KingdomSubsidenceRungSaveSnapshot value = TextField(field, prefix + new string('x', maximum - prefix.Length));
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(Wire(value), out _));
			Refuses(TextField(field, prefix + new string('x', maximum - prefix.Length + 1)));
		}

		[Test]
		public void RoofBodyTextBoundKeepsTheMatchingSurvivorPairExact()
		{
			foreach (int length in new[] { 512, 513 })
			{
				string body = new string('x', length); Fixture fixture = new Fixture { Roof = Roof(bodyObjectId: body) };
				fixture.ObjectIds[0] = body;
				if (length == 512) Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(Wire(fixture.Snapshot()), out _));
				else Refuses(fixture.Snapshot());
			}
		}

		[TestCase(0)] [TestCase(10)] [TestCase(127)] [TestCase(0x85)] [TestCase(0xD800)] [TestCase(0xDC00)]
		public void EveryTextPositionRejectsControlsAndUnpairedUtf16(int codeUnit)
		{
			string invalid = "before" + new string((char)codeUnit, 1) + "after";
			foreach (string field in TextFields)
				Refuses(TextField(field, (field == "StepWire" ? "ss5:" : field == "RungWire" ? "sr2:" : "") + invalid));
		}

		[Test]
		public void PairedUnicodeAndOrdinalObjectIdentitySurviveWithoutNormalization()
		{
			Fixture fixture = new Fixture { Step = "ss5:opaque-\U0001F9EA", Rung = "sr2:opaque-\u0800" };
			fixture.ObjectIds[1] = "BODY-1"; fixture.ObjectIds[2] = "body-\U0001F9EA";
			fixture.AbsentObjectIds[0] = "body-e\u0301"; fixture.AbsentObjectIds[1] = "body-\u00E9";
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(Wire(fixture.Snapshot()), out KingdomSubsidenceRungSaveSnapshot read));
			Assert.AreEqual(fixture.Step, read.StepWire); Assert.AreEqual(fixture.Rung, read.RungWire);
			CollectionAssert.AreEqual(fixture.ObjectIds, read.ObjectIds); CollectionAssert.AreEqual(fixture.AbsentObjectIds, read.AbsentObjectIds);
		}

		[TestCase("Stage", 5)] [TestCase("IncidentPhase", 9)] [TestCase("IncidentCause", 5)]
		[TestCase("LastCause", 5)] [TestCase("IncidentMessageState", 6)] [TestCase("Standing", 4)]
		public void UndefinedEnumValuesRefuseInModelsAndForgedFrames(string field, int firstUndefined)
		{
			foreach (int invalid in new[] { -1, firstUndefined, 255, int.MaxValue })
			{
				Refuses(NumberField(field, invalid)); RefusesIntegerFrame(field, invalid);
			}
		}

		[TestCase("Now", -1L)] [TestCase("AnchorTick", -1L)] [TestCase("DueTick", -1L)]
		[TestCase("Sequence", 0L)] [TestCase("Sequence", -1L)] [TestCase("LastSubsidenceTick", -1L)]
		[TestCase("LedgerDepartures", 14L)] [TestCase("Population", 34L)] [TestCase("Population", 36L)]
		[TestCase("ChronicleCount", -1L)] [TestCase("ChronicleCount", 65537L)]
		[TestCase("OutsiderCount", -1L)] [TestCase("OutsiderCount", 65537L)]
		[TestCase("X", -1L)] [TestCase("X", 1048577L)] [TestCase("Y", -1L)] [TestCase("Y", 1048577L)]
		[TestCase("PartCopies", 0L)] [TestCase("PartCopies", 2L)]
		[TestCase("BeforeWear", -1L)] [TestCase("BeforeWear", 1001L)]
		[TestCase("BeforeWear", 2147483647L)]
		[TestCase("AfterWear", 10L)] [TestCase("AfterWear", 1001L)]
		[TestCase("AfterWear", 2147483647L)]
		[TestCase("IncidentBeforeWear", -1L)] [TestCase("IncidentBeforeWear", 1001L)]
		[TestCase("IncidentBeforeWear", 2147483647L)]
		[TestCase("IncidentAfterWear", -1L)] [TestCase("IncidentAfterWear", 1001L)]
		[TestCase("IncidentAfterWear", 2147483647L)]
		[TestCase("Wear", -1L)] [TestCase("Wear", 1001L)]
		[TestCase("Wear", 2147483647L)]
		[TestCase("Reached", -1L)] [TestCase("Warned", -1L)]
		public void InvalidNumericBoundsRefuseWithoutClamping(string field, long value)
		{
			Refuses(NumberField(field, value));
			byte[] bytes = Bytes(Wire(new Fixture().Snapshot()));
			bool wide = field == "Now" || field == "AnchorTick" || field == "DueTick" || field == "Sequence"
				|| field == "LastSubsidenceTick" || field == "Reached" || field == "Warned";
			PutInteger(bytes, Offsets(bytes)[field], value, wide ? 8 : 4); RefusesWire(Envelope(bytes));
		}

		[Test]
		public void ReversedAnchorAndDueRefuseButEqualTicksAndUnwarnedZeroRemainExact()
		{
			Refuses(new Fixture { AnchorTick = 60002, DueTick = 60001 }.Snapshot());
			KingdomSubsidenceRungSaveSnapshot value = new Fixture { AnchorTick = 60001, DueTick = 60001,
				Roof = Roof(reached: 60001, warned: 0) }.Snapshot();
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(Wire(value), out KingdomSubsidenceRungSaveSnapshot read));
			Assert.AreEqual(60001, read.AnchorTick); Assert.AreEqual(0, read.Roof.Warned);
		}

		[TestCase(0)] [TestCase(34)] [TestCase(36)]
		public void SurvivorArraysRequireExactlyThirtyFiveEntriesEach(int count)
		{
			Refuses(new Fixture { ResidentIds = Ids(1, count) }.Snapshot());
			Refuses(new Fixture { ObjectIds = Objects(1, count) }.Snapshot());
		}

		[TestCase(0)] [TestCase(14)] [TestCase(16)]
		public void AbsentArraysRequireExactlyFifteenEntriesEach(int count)
		{
			Refuses(new Fixture { AbsentResidentIds = Ids(36, count) }.Snapshot());
			Refuses(new Fixture { AbsentObjectIds = Objects(36, count) }.Snapshot());
		}

		[TestCase(false, 0)] [TestCase(false, -1)] [TestCase(false, 3)] [TestCase(false, 36)]
		[TestCase(true, 0)] [TestCase(true, -1)] [TestCase(true, 37)] [TestCase(true, 1)]
		public void ResidentIdsArePositiveUniqueAndDisjointAcrossBothSets(bool absent, int id)
		{
			Fixture fixture = new Fixture(); (absent ? fixture.AbsentResidentIds : fixture.ResidentIds)[absent ? 0 : 1] = id;
			Refuses(fixture.Snapshot());
		}

		[TestCase(false, "body-3")] [TestCase(false, "body-36")]
		[TestCase(true, "body-37")] [TestCase(true, "body-1")]
		public void ObjectIdsAreUniqueAndDisjointAcrossBothSets(bool absent, string id)
		{
			Fixture fixture = new Fixture(); (absent ? fixture.AbsentObjectIds : fixture.ObjectIds)[absent ? 0 : 1] = id;
			Refuses(fixture.Snapshot());
		}

		[TestCase("body-1")] [TestCase("body-36")]
		public void WorkObjectCannotBorrowASurvivingOrAbsentBodyIdentity(string id) { Refuses(new Fixture { Work = Work(objectId: id) }.Snapshot()); }

		[TestCase(1, "body-2")] [TestCase(2, "body-1")]
		[TestCase(36, "body-36")] [TestCase(1, "body-36")] [TestCase(36, "body-1")]
		[TestCase(36, "body-37")] [TestCase(51, "body-1")] [TestCase(1, "body-51")]
		[TestCase(0, "body-1")] [TestCase(-1, "body-1")]
		public void RoofRequiresAnExactSurvivingPairNeverAbsentCrossedOrUnknownIdentities(int resident, string body)
		{
			Refuses(new Fixture { Roof = Roof(residentId: resident, bodyObjectId: body) }.Snapshot());
			byte[] bytes = Bytes(Wire(new Fixture().Snapshot())); Dictionary<string, int> offsets = Offsets(bytes);
			PutInteger(bytes, offsets["ResidentId"], resident, 4);
			RefusesWire(Envelope(ReplaceText(bytes, offsets["BodyObjectId"], Encoding.UTF8.GetBytes(body))));
		}

		[TestCase(1)] [TestCase(17)] [TestCase(35)]
		public void AnyExactSurvivingRoofPairCanRoundTrip(int resident)
		{
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(
				Wire(new Fixture { Roof = Roof(residentId: resident, bodyObjectId: "body-" + resident) }.Snapshot()), out _));
		}

		[TestCase(null)] [TestCase("")] [TestCase("taf-rung-save-v1:")]
		[TestCase("taf-rung-save-v2:AAAA")] [TestCase("TAF-RUNG-SAVE-V1:AAAA")]
		[TestCase("ssv1:AAAA")] [TestCase("taf-rung-save-v1:!not-base64")]
		public void MissingUnknownAndMalformedEnvelopesRefuse(string wire) { RefusesWire(wire); }

		[TestCase(null, false)] [TestCase("", false)] [TestCase("ssv1:AAAA", false)]
		[TestCase("taf-rung-save-v2:", true)] [TestCase("TAF-RUNG-SAVE-V1:", false)]
		[TestCase("taf-rung-save-v3:", false)]
		[TestCase("taf-rung-save-v1:", true)] [TestCase("taf-rung-save-v1:!broken", true)]
		public void PrefixClassificationRecognizesTheClaimIndependentlyOfPayloadValidity(string wire, bool claimed)
		{
			Assert.AreEqual(claimed, KingdomSubsidenceRungSaveSnapshotCodec.MatchesPrefix(wire));
		}

		[Test]
		public void OversizedClaimStillMatchesThisVariantButDecodeRefuses()
		{
			string wire = Prefix + new string('A', KingdomSubsidenceRungSaveSnapshotCodec.MaxWireChars);
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.MatchesPrefix(wire)); RefusesWire(wire);
		}

		[Test]
		public void EveryTruncatedByteBoundaryAndTrailingBytesRefuse()
		{
			byte[] bytes = Bytes(Wire(new Fixture().Snapshot()));
			for (int length = 0; length < bytes.Length; length++)
			{
				byte[] truncated = new byte[length]; Array.Copy(bytes, truncated, length); RefusesWire(Envelope(truncated));
			}
			byte[] trailing = new byte[bytes.Length + 1]; Array.Copy(bytes, trailing, bytes.Length); RefusesWire(Envelope(trailing));
		}

		[TestCase(0)] [TestCase(4)]
		public void WrongMagicOrVersionRefuses(int offset)
		{
			byte[] bytes = Bytes(Wire(new Fixture().Snapshot())); bytes[offset] ^= 1; RefusesWire(Envelope(bytes));
		}

		[Test]
		public void NoncanonicalBase64WhitespaceAndUnusedPaddingBitsRefuse()
		{
			string wire = Wire(new Fixture().Snapshot()); RefusesWire(wire.Insert(Prefix.Length + 4, "\n"));
			for (int extra = 1; extra <= 3 && !wire.EndsWith("=", StringComparison.Ordinal); extra++)
				wire = Wire(new Fixture { Step = StepWire + new string('x', extra) }.Snapshot());
			Assert.IsTrue(wire.EndsWith("=", StringComparison.Ordinal));
			const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
			int at = wire.Length - (wire.EndsWith("==", StringComparison.Ordinal) ? 3 : 2);
			char[] chars = wire.ToCharArray(); chars[at] = alphabet[alphabet.IndexOf(chars[at]) + 1];
			CollectionAssert.AreEqual(Bytes(wire), Bytes(new string(chars)), "padding mutation must preserve decoded bytes");
			RefusesWire(new string(chars));
		}

		[TestCase(-2)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
		public void EveryTextFrameRejectsInvalidOrUnboundedByteLengths(int length)
		{
			byte[] original = Bytes(Wire(new Fixture().Snapshot())); Dictionary<string, int> offsets = Offsets(original);
			foreach (string field in TextFields)
			{
				byte[] bytes = (byte[])original.Clone(); PutInteger(bytes, offsets[field], length, 4); RefusesWire(Envelope(bytes));
			}
		}

		[Test]
		public void RequiredTextFramesRejectNullAndEmptyAndLengthsBeyondRemainingBytes()
		{
			byte[] original = Bytes(Wire(new Fixture().Snapshot())); Dictionary<string, int> offsets = Offsets(original);
			foreach (string field in TextFields)
			{
				byte[] bytes = (byte[])original.Clone(); PutInteger(bytes, offsets[field], bytes.Length, 4); RefusesWire(Envelope(bytes));
				if (Array.IndexOf(NullableTextFields, field) >= 0 || field == "PlotId") continue;
				RefusesWire(Envelope(ReplaceText(original, offsets[field], null)));
				RefusesWire(Envelope(ReplaceText(original, offsets[field], new byte[0])));
			}
		}

		[TestCase("ff")] [TestCase("c0af")] [TestCase("eda080")] [TestCase("f4908080")]
		[TestCase("e282")] [TestCase("80")]
		public void StrictUtf8RejectsMalformedBytesInsideEveryTextPosition(string hex)
		{
			byte[] invalid = new byte[hex.Length / 2];
			for (int i = 0; i < invalid.Length; i++) invalid[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
			byte[] bytes = Bytes(Wire(new Fixture().Snapshot())); Dictionary<string, int> offsets = Offsets(bytes);
			foreach (string field in TextFields) RefusesWire(Envelope(ReplaceText(bytes, offsets[field], invalid)));
		}

		[TestCase("SurvivorCount", 34)] [TestCase("SurvivorCount", 36)]
		[TestCase("AbsentCount", 14)] [TestCase("AbsentCount", 16)]
		[TestCase("SurvivorCount", -1)] [TestCase("AbsentCount", int.MaxValue)]
		public void ForgedPairCountsRefuseBeforeReadingUnboundedArrays(string field, int count) { RefusesIntegerFrame(field, count); }

		[TestCase("SurvivorId", 0)] [TestCase("SurvivorId", 1)] [TestCase("SurvivorId", 36)]
		[TestCase("AbsentId", 0)] [TestCase("AbsentId", 37)] [TestCase("AbsentId", 1)]
		public void ForgedResidentRowsCannotBypassSetValidation(string field, int id) { RefusesIntegerFrame(field, id); }

		[TestCase("Quarantined")] [TestCase("RoofStanding")]
		public void BooleanFramesMustUseExactlyZeroOrOne(string field)
		{
			byte[] original = Bytes(Wire(new Fixture().Snapshot())); int offset = Offsets(original)[field];
			foreach (byte invalid in new byte[] { 2, 255 })
			{
				byte[] bytes = (byte[])original.Clone(); bytes[offset] = invalid; RefusesWire(Envelope(bytes));
			}
		}

		[Test]
		public void Utf8ByteBudgetCanRejectAnOtherwiseBoundedModelWithoutPartialWire()
		{
			Fixture fixture = new Fixture { Step = "ss5:" + new string('\u0800', KingdomSubsidenceRungSaveSnapshotCodec.MaxStepWireChars - 4) };
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.Valid(fixture.Snapshot()));
			Assert.IsFalse(KingdomSubsidenceRungSaveSnapshotCodec.TryEncode(fixture.Snapshot(), out string wire)); Assert.IsNull(wire);
		}

		private sealed class Fixture
		{
			internal string Game = GameId, Zone = ZoneId, Step = StepWire, Rung = RungWire, StepId = "step-7";
			internal string TellingDigest = new string('a', 64);
			internal long Now = 90001, AnchorTick = 1001, DueTick = 60001, Sequence = 7, LastSubsidenceTick = 1111;
			internal int LedgerDepartures = 17, Population = 35, Stage = 3, ChronicleCount = 23, OutsiderCount = 29;
			internal KingdomSubsidenceRungSaveWork Work = KingdomSubsidenceRungSaveSnapshotTests.Work();
			internal KingdomSubsidenceRungSaveRoof Roof = KingdomSubsidenceRungSaveSnapshotTests.Roof();
			internal int[] ResidentIds = Ids(1, 35), AbsentResidentIds = Ids(36, 15);
			internal string[] ObjectIds = Objects(1, 35), AbsentObjectIds = Objects(36, 15);
			internal KingdomSubsidenceRungSaveSnapshot Snapshot()
			{
				return new KingdomSubsidenceRungSaveSnapshot(Game, Zone, Step, Rung, StepId, TellingDigest,
					Now, AnchorTick, DueTick, Sequence, LastSubsidenceTick, LedgerDepartures, Population, Stage,
					ChronicleCount, OutsiderCount, Work, Roof, ResidentIds, ObjectIds, AbsentResidentIds, AbsentObjectIds);
			}
		}

		private static KingdomSubsidenceRungSaveWork Work(string objectId = "work-1", string blueprint = "r_test_house",
			string plotId = "plot-1", string designStamp = "design-1", int x = 17, int y = 23, int partCopies = 1,
			int beforeWear = 11, int afterWear = 29, int incidentPhase = 0, string incidentId = "step-7",
			int incidentCause = 4, int incidentBeforeWear = 13, int incidentAfterWear = 31, int wear = 29,
			int lastCause = 2, string lastCompletedIncidentId = "step-6", string incidentLine = "a roof remembers its lost rung",
			int incidentMessageState = 3, bool quarantined = false)
		{
			return new KingdomSubsidenceRungSaveWork(objectId, blueprint, plotId, designStamp, x, y, partCopies,
				beforeWear, afterWear, incidentPhase, incidentId, incidentCause, incidentBeforeWear, incidentAfterWear,
				wear, lastCause, lastCompletedIncidentId, incidentLine, incidentMessageState, quarantined);
		}
		private static KingdomSubsidenceRungSaveRoof Roof(int residentId = 1, string bodyObjectId = "body-1",
			int standing = 0, string zoneId = ZoneId, bool roofStanding = true, long reached = 60001, long warned = 60002)
		{
			return new KingdomSubsidenceRungSaveRoof(residentId, bodyObjectId, -1234567, standing, zoneId, roofStanding, reached, warned);
		}
		private static KingdomSubsidenceRungSaveSnapshot TextField(string field, string text)
		{
			Fixture f = new Fixture();
			switch (field)
			{
			case "GameId": f.Game = text; break;
			case "ZoneId": f.Zone = text; break;
			case "StepWire": f.Step = text; break;
			case "RungWire": f.Rung = text; break;
			case "StepId": f.StepId = text; break;
			case "TellingDigest": f.TellingDigest = text; break;
			case "ObjectId": f.Work = Work(objectId: text); break;
			case "Blueprint": f.Work = Work(blueprint: text); break;
			case "PlotId": f.Work = Work(plotId: text); break;
			case "DesignStamp": f.Work = Work(designStamp: text); break;
			case "IncidentId": f.Work = Work(incidentId: text); break;
			case "LastCompletedIncidentId": f.Work = Work(lastCompletedIncidentId: text); break;
			case "IncidentLine": f.Work = Work(incidentLine: text); break;
			case "BodyObjectId": f.Roof = Roof(bodyObjectId: text); break;
			case "RoofZoneId": f.Roof = Roof(zoneId: text); break;
			case "SurvivorObject": f.ObjectIds[1] = text; break;
			case "AbsentObject": f.AbsentObjectIds[0] = text; break;
			default: throw new ArgumentException(field);
			}
			return f.Snapshot();
		}
		private static KingdomSubsidenceRungSaveSnapshot NumberField(string field, long value)
		{
			Fixture f = new Fixture(); int number = (int)value;
			switch (field)
			{
			case "Now": f.Now = value; break;
			case "AnchorTick": f.AnchorTick = value; break;
			case "DueTick": f.DueTick = value; break;
			case "Sequence": f.Sequence = value; break;
			case "LastSubsidenceTick": f.LastSubsidenceTick = value; break;
			case "LedgerDepartures": f.LedgerDepartures = number; break;
			case "Population": f.Population = number; break;
			case "Stage": f.Stage = number; break;
			case "ChronicleCount": f.ChronicleCount = number; break;
			case "OutsiderCount": f.OutsiderCount = number; break;
			case "X": f.Work = Work(x: number); break;
			case "Y": f.Work = Work(y: number); break;
			case "PartCopies": f.Work = Work(partCopies: number); break;
			case "BeforeWear": f.Work = Work(beforeWear: number); break;
			case "AfterWear": f.Work = Work(afterWear: number); break;
			case "IncidentPhase": f.Work = Work(incidentPhase: number); break;
			case "IncidentCause": f.Work = Work(incidentCause: number); break;
			case "IncidentBeforeWear": f.Work = Work(incidentBeforeWear: number); break;
			case "IncidentAfterWear": f.Work = Work(incidentAfterWear: number); break;
			case "Wear": f.Work = Work(wear: number); break;
			case "LastCause": f.Work = Work(lastCause: number); break;
			case "IncidentMessageState": f.Work = Work(incidentMessageState: number); break;
			case "Standing": f.Roof = Roof(standing: number); break;
			case "Reached": f.Roof = Roof(reached: value); break;
			case "Warned": f.Roof = Roof(warned: value); break;
			default: throw new ArgumentException(field);
			}
			return f.Snapshot();
		}
		private static void AssertWork(KingdomSubsidenceRungSaveWork expected, KingdomSubsidenceRungSaveWork actual)
		{
			Assert.AreEqual(expected.ObjectId, actual.ObjectId); Assert.AreEqual(expected.Blueprint, actual.Blueprint);
			Assert.AreEqual(expected.PlotId, actual.PlotId); Assert.AreEqual(expected.DesignStamp, actual.DesignStamp);
			Assert.AreEqual(expected.X, actual.X); Assert.AreEqual(expected.Y, actual.Y); Assert.AreEqual(expected.PartCopies, actual.PartCopies);
			Assert.AreEqual(expected.BeforeWear, actual.BeforeWear); Assert.AreEqual(expected.AfterWear, actual.AfterWear);
			Assert.AreEqual(expected.IncidentPhase, actual.IncidentPhase); Assert.AreEqual(expected.IncidentId, actual.IncidentId);
			Assert.AreEqual(expected.IncidentCause, actual.IncidentCause); Assert.AreEqual(expected.IncidentBeforeWear, actual.IncidentBeforeWear);
			Assert.AreEqual(expected.IncidentAfterWear, actual.IncidentAfterWear); Assert.AreEqual(expected.Wear, actual.Wear);
			Assert.AreEqual(expected.LastCause, actual.LastCause); Assert.AreEqual(expected.LastCompletedIncidentId, actual.LastCompletedIncidentId);
			Assert.AreEqual(expected.IncidentLine, actual.IncidentLine); Assert.AreEqual(expected.IncidentMessageState, actual.IncidentMessageState);
			Assert.AreEqual(expected.Quarantined, actual.Quarantined);
		}
		private static void AssertRoof(KingdomSubsidenceRungSaveRoof expected, KingdomSubsidenceRungSaveRoof actual)
		{
			Assert.AreEqual(expected.ResidentId, actual.ResidentId); Assert.AreEqual(expected.BodyObjectId, actual.BodyObjectId);
			Assert.AreEqual(expected.Carrier.ResidentId, actual.Carrier.ResidentId); Assert.AreEqual(expected.Carrier.ObjectId, actual.Carrier.ObjectId);
			Assert.AreEqual(expected.Carrier.Key, actual.Carrier.Key);
			Assert.AreEqual(expected.HomeWorkId, actual.HomeWorkId); Assert.AreEqual(expected.Standing, actual.Standing);
			Assert.AreEqual(expected.ZoneId, actual.ZoneId); Assert.AreEqual(expected.RoofStanding, actual.RoofStanding);
			Assert.AreEqual(expected.Reached, actual.Reached); Assert.AreEqual(expected.Warned, actual.Warned);
		}
		private static int[] Ids(int first, int count)
		{
			int[] values = new int[count]; for (int i = 0; i < count; i++) values[i] = first + i; return values;
		}
		private static string[] Objects(int first, int count)
		{
			string[] values = new string[count]; for (int i = 0; i < count; i++) values[i] = "body-" + (first + i); return values;
		}
		private static string Wire(KingdomSubsidenceRungSaveSnapshot value)
		{
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.Valid(value));
			Assert.IsTrue(KingdomSubsidenceRungSaveSnapshotCodec.TryEncode(value, out string wire)); return wire;
		}
		private static void Refuses(KingdomSubsidenceRungSaveSnapshot value)
		{
			Assert.IsFalse(KingdomSubsidenceRungSaveSnapshotCodec.Valid(value));
			Assert.IsFalse(KingdomSubsidenceRungSaveSnapshotCodec.TryEncode(value, out string wire)); Assert.IsNull(wire);
		}
		private static void RefusesWire(string wire)
		{
			Assert.IsFalse(KingdomSubsidenceRungSaveSnapshotCodec.TryDecode(wire, out KingdomSubsidenceRungSaveSnapshot value)); Assert.IsNull(value);
		}
		private static void RefusesIntegerFrame(string field, int value)
		{
			byte[] bytes = Bytes(Wire(new Fixture().Snapshot())); PutInteger(bytes, Offsets(bytes)[field], value, 4); RefusesWire(Envelope(bytes));
		}
		private static byte[] Bytes(string wire) { return Convert.FromBase64String(wire.Substring(Prefix.Length)); }
		private static string Envelope(byte[] bytes) { return Prefix + Convert.ToBase64String(bytes); }
		private static void PutInteger(byte[] bytes, int offset, long value, int size)
		{
			for (int i = 0; i < size; i++) bytes[offset + i] = (byte)(value >> (8 * i));
		}
		private static int ReadInt(byte[] bytes, int offset)
		{
			return bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24;
		}
		private static byte[] ReplaceText(byte[] bytes, int offset, byte[] replacement)
		{
			int previous = Math.Max(0, ReadInt(bytes, offset)), length = replacement == null ? 0 : replacement.Length;
			byte[] result = new byte[bytes.Length - previous + length]; Array.Copy(bytes, result, offset);
			PutInteger(result, offset, replacement == null ? -1 : length, 4);
			if (replacement != null) Array.Copy(replacement, 0, result, offset + 4, length);
			Array.Copy(bytes, offset + 4 + previous, result, offset + 4 + length, bytes.Length - offset - 4 - previous);
			return result;
		}
		private static Dictionary<string, int> Offsets(byte[] bytes)
		{
			Dictionary<string, int> offsets = new Dictionary<string, int>();
			using (MemoryStream stream = new MemoryStream(bytes))
			using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
			{
				stream.Position = 8;
				foreach (string field in new[] { "GameId", "ZoneId", "StepWire", "RungWire", "StepId", "TellingDigest" }) TextOffset(reader, offsets, field);
				foreach (string field in new[] { "Now", "AnchorTick", "DueTick", "Sequence", "LastSubsidenceTick" }) FixedOffset(stream, offsets, field, 8);
				foreach (string field in new[] { "LedgerDepartures", "Population", "Stage", "ChronicleCount", "OutsiderCount" }) FixedOffset(stream, offsets, field, 4);
				foreach (string field in new[] { "ObjectId", "Blueprint", "PlotId", "DesignStamp" }) TextOffset(reader, offsets, field);
				foreach (string field in new[] { "X", "Y", "PartCopies", "BeforeWear", "AfterWear", "IncidentPhase" }) FixedOffset(stream, offsets, field, 4);
				TextOffset(reader, offsets, "IncidentId");
				foreach (string field in new[] { "IncidentCause", "IncidentBeforeWear", "IncidentAfterWear", "Wear", "LastCause" }) FixedOffset(stream, offsets, field, 4);
				TextOffset(reader, offsets, "LastCompletedIncidentId"); TextOffset(reader, offsets, "IncidentLine");
				FixedOffset(stream, offsets, "IncidentMessageState", 4); FixedOffset(stream, offsets, "Quarantined", 1);
				FixedOffset(stream, offsets, "ResidentId", 4); TextOffset(reader, offsets, "BodyObjectId");
				FixedOffset(stream, offsets, "HomeWorkId", 4); FixedOffset(stream, offsets, "Standing", 4);
				TextOffset(reader, offsets, "RoofZoneId"); FixedOffset(stream, offsets, "RoofStanding", 1);
				FixedOffset(stream, offsets, "Reached", 8); FixedOffset(stream, offsets, "Warned", 8);
				BodyOffsets(reader, offsets, "Survivor", 35, 1); BodyOffsets(reader, offsets, "Absent", 15, 0);
				Assert.AreEqual(stream.Length, stream.Position, "fixture layout must account for every byte");
			}
			return offsets;
		}
		private static void TextOffset(BinaryReader reader, Dictionary<string, int> offsets, string field)
		{
			offsets.Add(field, (int)reader.BaseStream.Position); int length = reader.ReadInt32();
			if (length > 0) reader.BaseStream.Position += length;
		}
		private static void FixedOffset(Stream stream, Dictionary<string, int> offsets, string field, int size)
		{
			offsets.Add(field, (int)stream.Position); stream.Position += size;
		}
		private static void BodyOffsets(BinaryReader reader, Dictionary<string, int> offsets, string group, int count, int selected)
		{
			offsets.Add(group + "Count", (int)reader.BaseStream.Position); Assert.AreEqual(count, reader.ReadInt32());
			for (int i = 0; i < count; i++)
			{
				if (i == selected) offsets.Add(group + "Id", (int)reader.BaseStream.Position);
				reader.ReadInt32();
				if (i == selected) TextOffset(reader, offsets, group + "Object");
				else { int length = reader.ReadInt32(); reader.BaseStream.Position += length; }
			}
		}
	}
}
#endif
