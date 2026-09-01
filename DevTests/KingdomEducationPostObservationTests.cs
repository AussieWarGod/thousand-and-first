#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomEducationPostObservationTests
	{
		private static KingdomEducationPostObservationRow Row(int WorkId = 17,
			string Root = "root-a", string Designation = "designation-a")
		{
			return new KingdomEducationPostObservationRow { WorkId = WorkId, RootId = Root,
				DesignationIdentity = Designation, DesignationRevision = "revision-a",
				ZoneId = "zone-a", AnchorX = 4, AnchorY = 5, Blueprint = "School House" };
		}

		[Test]
		public void CanonicalPayloadSortsAndRoundTripsEveryExactField()
		{
			KingdomEducationPostObservationRow second = Row(29, "root-b", "designation-b");
			second.DesignationRevision = "revision-b"; second.AnchorX = 8;
			Assert.That(KingdomEducationPostObservationRules.TryEncode(
				new List<KingdomEducationPostObservationRow> { second, Row() },
				out string payload), Is.True);
			Assert.That(KingdomEducationPostObservationRules.TryEncode(
				new List<KingdomEducationPostObservationRow> { Row(), second },
				out string reordered), Is.True);
			Assert.That(reordered, Is.EqualTo(payload));
			Assert.That(KingdomEducationPostObservationRules.TryDecode(payload,
				out List<KingdomEducationPostObservationRow> rows), Is.True);
			Assert.Multiple(() => {
				Assert.That(rows.Count, Is.EqualTo(2));
				Assert.That(rows[0].WorkId, Is.EqualTo(17));
				Assert.That(rows[0].RootId, Is.EqualTo("root-a"));
				Assert.That(rows[0].DesignationIdentity, Is.EqualTo("designation-a"));
				Assert.That(rows[0].DesignationRevision, Is.EqualTo("revision-a"));
				Assert.That(rows[0].ZoneId, Is.EqualTo("zone-a"));
				Assert.That(rows[0].AnchorX, Is.EqualTo(4));
				Assert.That(rows[0].AnchorY, Is.EqualTo(5));
				Assert.That(rows[0].Blueprint, Is.EqualTo("School House"));
			});
		}

		[Test]
		public void EmptyObservationIsCanonicalAuthoritativeZero()
		{
			Assert.That(KingdomEducationPostObservationRules.TryEncode(
				new List<KingdomEducationPostObservationRow>(), out string payload), Is.True);
			Assert.That(KingdomEducationPostObservationRules.TryDecode(payload,
				out List<KingdomEducationPostObservationRow> rows), Is.True);
			Assert.That(rows, Is.Empty);
		}

		[Test]
		public void DuplicateWorkRootOrDesignationFailsClosed()
		{
			KingdomEducationPostObservationRow a = Row();
			KingdomEducationPostObservationRow sameWork = Row(17, "root-b", "designation-b");
			KingdomEducationPostObservationRow sameRoot = Row(18, "root-a", "designation-b");
			KingdomEducationPostObservationRow sameDesignation = Row(18, "root-b", "designation-a");
			foreach (KingdomEducationPostObservationRow duplicate in
				new[] { sameWork, sameRoot, sameDesignation })
				Assert.That(KingdomEducationPostObservationRules.TryEncode(
					new List<KingdomEducationPostObservationRow> { a, duplicate }, out _), Is.False);
		}

		[Test]
		public void MalformedAndOverBoundRowsFailClosed()
		{
			foreach (Action<KingdomEducationPostObservationRow> mutate in new Action<KingdomEducationPostObservationRow>[] {
				r => r.WorkId = 0, r => r.RootId = "", r => r.DesignationIdentity = " designation",
				r => r.DesignationRevision = null, r => r.ZoneId = "zone-a ", r => r.AnchorX = -1,
				r => r.AnchorY = short.MaxValue + 1,
				r => r.Blueprint = new string('x',
					KingdomEducationPostObservationRules.MaxBlueprintChars + 1) })
			{
				KingdomEducationPostObservationRow row = Row(); mutate(row);
				Assert.That(KingdomEducationPostObservationRules.TryEncode(
					new List<KingdomEducationPostObservationRow> { row }, out _), Is.False);
			}
			List<KingdomEducationPostObservationRow> tooMany = new List<KingdomEducationPostObservationRow>();
			for (int i = 0; i <= KingdomEducationPostObservationRules.MaxRows; i++)
				tooMany.Add(Row(i + 1, "root-" + i, "designation-" + i));
			Assert.That(KingdomEducationPostObservationRules.TryEncode(tooMany, out _), Is.False);
		}

		[Test]
		public void ExactLookupRejectsEveryWorkTupleMismatch()
		{
			KingdomEducationPostObservationRules.TryEncode(
				new List<KingdomEducationPostObservationRow> { Row() }, out string payload);
			Assert.That(KingdomEducationPostObservationRules.TryFindExact(payload,
				17, "zone-a", 4, 5, "School House", out var exact), Is.True);
			Assert.That(exact.RootId, Is.EqualTo("root-a"));
			Assert.That(exact.DesignationIdentity, Is.EqualTo("designation-a"));
			foreach (object[] wrong in new[] { new object[] { 18, "zone-a", 4, 5, "School House" },
				new object[] { 17, "zone-b", 4, 5, "School House" },
				new object[] { 17, "zone-a", 3, 5, "School House" },
				new object[] { 17, "zone-a", 4, 6, "School House" },
				new object[] { 17, "zone-a", 4, 5, "Other House" } })
				Assert.That(KingdomEducationPostObservationRules.TryFindExact(payload,
					(int)wrong[0], (string)wrong[1], (int)wrong[2], (int)wrong[3],
					(string)wrong[4], out _), Is.False);
		}

		[Test]
		public void EnvelopeRejectsWrongRawPurposeBindingFutureAndTamper()
		{
			KingdomEducationPostObservationRules.TryEncode(
				new List<KingdomEducationPostObservationRow> { Row() }, out string payload);
			Assert.That(KingdomZoneObservationRules.TryCreate(
				KingdomEducationPostObservationRules.Purpose, "realm-a", "settlement-a",
				"zone-a", "owner-a", KingdomEducationPostObservationRules.SourceRevision,
				19L, payload, out KingdomZoneObservationReceipt receipt), Is.True);
			KingdomZoneObservationCodec.TryEncode(receipt, out string wire);
			Assert.That(KingdomZoneObservationRules.TryReadExact(wire,
				KingdomEducationPostObservationRules.Purpose, "realm-a", "settlement-a",
				"zone-a", "owner-a", KingdomEducationPostObservationRules.SourceRevision,
				19L, out _), Is.True);
			Assert.That(KingdomZoneObservationRules.TryReadExact(1,
				KingdomEducationPostObservationRules.Purpose, "realm-a", "settlement-a",
				"zone-a", "owner-a", KingdomEducationPostObservationRules.SourceRevision,
				19L, out _), Is.False);
			Assert.That(KingdomZoneObservationRules.TryReadExact(wire, "taf.reach", "realm-a",
				"settlement-a", "zone-a", "owner-a",
				KingdomEducationPostObservationRules.SourceRevision, 19L, out _), Is.False);
			Assert.That(KingdomZoneObservationRules.TryReadExact(wire,
				KingdomEducationPostObservationRules.Purpose, "realm-b", "settlement-a",
				"zone-a", "owner-a", KingdomEducationPostObservationRules.SourceRevision,
				19L, out _), Is.False);
			foreach (string[] wrong in new[] {
				new[] { "settlement-b", "zone-a", "owner-a",
					KingdomEducationPostObservationRules.SourceRevision },
				new[] { "settlement-a", "zone-b", "owner-a",
					KingdomEducationPostObservationRules.SourceRevision },
				new[] { "settlement-a", "zone-a", "owner-b",
					KingdomEducationPostObservationRules.SourceRevision },
				new[] { "settlement-a", "zone-a", "owner-a", "other-revision" } })
				Assert.That(KingdomZoneObservationRules.TryReadExact(wire,
					KingdomEducationPostObservationRules.Purpose, "realm-a", wrong[0],
					wrong[1], wrong[2], wrong[3], 19L, out _), Is.False);
			Assert.That(KingdomZoneObservationRules.TryReadExact(wire,
				KingdomEducationPostObservationRules.Purpose, "realm-a", "settlement-a",
				"zone-a", "owner-a", KingdomEducationPostObservationRules.SourceRevision,
				18L, out _), Is.False);
			KingdomEducationPostObservationRow changed = Row(); changed.DesignationRevision = "other";
			KingdomEducationPostObservationRules.TryEncode(
				new List<KingdomEducationPostObservationRow> { changed }, out receipt.Payload);
			Assert.That(KingdomZoneObservationRules.Valid(receipt), Is.False);
			Assert.That(KingdomEducationPostObservationRules.TryDecode(payload + " ", out _), Is.False);
		}
	}
}
#endif
