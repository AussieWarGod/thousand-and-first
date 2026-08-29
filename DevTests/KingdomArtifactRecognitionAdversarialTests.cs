#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Every way D6 is asked to change something and must not: refused authorities, the wrong
	/// realm, a settler who left, an original that is gone, and a copy that quietly loses a field.
	/// </summary>
	[TestFixture]
	public sealed class KingdomArtifactRecognitionAdversarialTests
	{
		private const string Realm = KingdomArtifactRecognitionServiceTests.Realm;
		private const string OtherRealm = KingdomArtifactRecognitionServiceTests.OtherRealm;

		private static KingdomArtifactSnapshot Artifact(string Id = "artifact-1")
		{
			return KingdomArtifactRecognitionServiceTests.Artifact(Id);
		}

		private static byte[] Envelope(byte[] ArtifactsPayload)
		{
			return KingdomCivicMemoryCodec.Encode(KingdomCivicMemoryState.Of(
				new List<KingdomCivicMemorySection>
				{
					new KingdomCivicMemorySection(
						KingdomCivicMemoryLimits.SectionCivicArtifacts, ArtifactsPayload)
				}, 0L));
		}

		private static KingdomCivicMemoryAuthority Adopted(byte[] ArtifactsPayload)
		{
			KingdomCivicMemoryAuthority authority = new KingdomCivicMemoryAuthority(
				KingdomArtifactRecognitionServiceTests.Families());
			authority.AdoptSaved(Envelope(ArtifactsPayload));
			return authority;
		}

		/// <summary>Bytes the family refuses are evidence. D6 reads nothing and writes nothing.</summary>
		[Test]
		public void QuarantinedArtifactSectionIsNeverWrittenOver()
		{
			KingdomCivicMemoryAuthority authority = Adopted(new byte[] { 9, 9, 9, 9 });
			Assert.IsTrue(authority.Quarantined);
			Assert.IsTrue(authority.Latch.Tripped);
			long before = authority.Revision;
			string reason = authority.ReadOnlyReason;
			Assert.IsFalse(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out KingdomCivicArtifactsEnvelope held,
				out string failure));
			Assert.IsNull(lease);
			Assert.IsNull(held);
			StringAssert.Contains("read-only", failure);
			Assert.AreEqual(before, authority.Revision);
			Assert.IsTrue(authority.Quarantined, "the refused bytes remain as evidence");
			Assert.AreEqual(reason, authority.ReadOnlyReason);
			Assert.Throws<System.IO.InvalidDataException>(() => authority.Encode(),
				"a quarantined authority must refuse to be written back at all");
		}

		/// <summary>A newer build's authority is carried whole, never edited by this one.</summary>
		[Test]
		public void FutureArtifactAuthorityIsCarriedAndRefusesEveryRecognition()
		{
			KingdomCivicArtifactsEnvelope future = new KingdomCivicArtifactsEnvelope
			{
				OpaqueFutureVersion = KingdomCivicArtifactsCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 4, 5, 6, 7 }
			};
			Assert.IsTrue(KingdomCivicArtifactsStore.TryWrite(future, out byte[] payload,
				out string writeFailure), writeFailure);
			KingdomCivicMemoryAuthority authority = Adopted(payload);
			Assert.IsFalse(authority.Quarantined, "a newer build is lawful, not corrupt");
			byte[] before = authority.Encode();
			Assert.IsFalse(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out _, out _, out string failure));
			StringAssert.Contains("newer", failure);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>Another realm's authority is another realm's. It is never adopted or amended.</summary>
		[Test]
		public void ArtifactAuthorityBoundToAnotherRealmIsRefused()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Authority();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, OtherRealm,
				out KingdomCivicMemorySectionLease lease, out _, out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				OtherRealm, Artifact(), KingdomArtifactRecognitionServiceTests.Kind, 0, null, 40L,
				out _, out _, out failure), failure);
			byte[] before = authority.Encode();
			Assert.IsFalse(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out _, out _, out failure));
			StringAssert.Contains("realm", failure);
			CollectionAssert.AreEqual(before, authority.Encode());
		}

		/// <summary>
		/// The interpretation seam is public, so it answers for itself rather than leaning on the
		/// section reader that happens to guard it today. A newer build's bytes and another realm's
		/// bytes are each refused by name, with nothing bound and nothing carried forward.
		/// </summary>
		[Test]
		public void InterpretingForeignOrNewerBytesRefusesThemByName()
		{
			KingdomCivicArtifactsEnvelope future = new KingdomCivicArtifactsEnvelope
			{
				OpaqueFutureVersion = KingdomCivicArtifactsCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 4, 5, 6, 7 }
			};
			Assert.IsTrue(KingdomCivicArtifactsStore.TryWrite(future, out byte[] newer,
				out string failure), failure);
			Assert.IsFalse(KingdomArtifactRecognitionLease.TryInterpret(newer, Realm,
				out KingdomCivicArtifactsEnvelope carried, out failure));
			StringAssert.Contains("newer build", failure);
			Assert.IsNull(carried);

			KingdomCivicArtifactsEnvelope theirs = new KingdomCivicArtifactsEnvelope
			{
				RealmId = OtherRealm,
				IdentityBound = true
			};
			Assert.IsTrue(KingdomCivicArtifactsStore.TryWrite(theirs, out byte[] foreign,
				out failure), failure);
			Assert.IsFalse(KingdomArtifactRecognitionLease.TryInterpret(foreign, Realm,
				out KingdomCivicArtifactsEnvelope borrowed, out failure));
			StringAssert.Contains("belongs to another realm", failure);
			Assert.IsNull(borrowed);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryInterpret(foreign, OtherRealm,
				out KingdomCivicArtifactsEnvelope owned, out failure), failure);
			Assert.AreEqual(OtherRealm, owned.RealmId);
		}

		/// <summary>
		/// A lease naming a different section cannot be used to write recognitions. Without this,
		/// artifact bytes would be committed into somebody else's section entirely.
		/// </summary>
		[Test]
		public void LeaseForAnotherSectionCannotCarryARecognition()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Authority();
			Assert.IsTrue(authority.TryReadSection(
				KingdomCivicMemoryLimits.SectionCivicPractice,
				out KingdomCivicMemorySectionLease wrong, out string failure), failure);
			byte[] before = authority.Encode();
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, wrong,
				Realm, Artifact(), KingdomArtifactRecognitionServiceTests.Kind, 0, null, 40L,
				out KingdomArtifactRecognitionReceipt refused, out _, out failure));
			StringAssert.Contains("artifact section", failure);
			Assert.IsNull(refused);
			CollectionAssert.AreEqual(before, authority.Encode());
			Assert.IsNull(authority.Read().Section(
				KingdomCivicMemoryLimits.SectionCivicPractice),
				"no artifact bytes may land in another family's section");
		}

		/// <summary>A lease from another authority cannot be used to write into this one.</summary>
		[Test]
		public void LeaseFromAnotherAuthorityCannotCommitHere()
		{
			KingdomCivicMemoryAuthority mine = KingdomArtifactRecognitionServiceTests.Authority();
			KingdomCivicMemoryAuthority theirs = KingdomArtifactRecognitionServiceTests.Authority();
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(theirs, Realm,
				out KingdomCivicMemorySectionLease foreign, out _, out string failure), failure);
			byte[] before = mine.Encode();
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryCommitPlanned(mine, foreign, Realm,
				Artifact(), KingdomArtifactRecognitionServiceTests.Kind, 0, null, 40L,
				out _, out _, out failure));
			StringAssert.Contains("another authority", failure);
			CollectionAssert.AreEqual(before, mine.Encode());
		}

		/// <summary>
		/// The committed receipt is the only recognition authority, and the subject is the object
		/// itself. Once the city has written about a thing, what later becomes of that thing can
		/// neither rewrite the row nor earn a second one.
		/// </summary>
		[TestCase(0, TestName = "the original was moved")]
		[TestCase(1, TestName = "the original changed hands")]
		[TestCase(2, TestName = "another form was asked for")]
		public void LaterChangeToTheOriginalNeitherRewritesNorDuplicatesTheRow(int Change)
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out string id);
			byte[] before = authority.Encode();
			// Every changed reading is also a LATER reading, so tolerating a later observation
			// tick cannot be mistaken for tolerating a changed fact.
			KingdomArtifactSnapshot after = Change == 0
				? KingdomArtifactRecognitionServiceTests.Artifact("artifact-1",
					"folded fullerite sword", "taf:owner:player",
					"taf:zone:JoppaWorld.11.22.1.1.10:9:9", 9000L)
				: Change == 1
					? KingdomArtifactRecognitionServiceTests.Artifact("artifact-1",
						"folded fullerite sword", "taf:owner:settlement",
						"taf:zone:JoppaWorld.11.22.1.1.10:4:5", 9000L)
					: KingdomArtifactRecognitionServiceTests.Artifact("artifact-1",
						"folded fullerite sword", "taf:owner:player",
						"taf:zone:JoppaWorld.11.22.1.1.10:4:5", 9000L);
			KingdomArtifactRecognitionKind kind = Change == 2
				? KingdomArtifactRecognitionKind.Remark
				: KingdomArtifactRecognitionServiceTests.Kind;
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out _, out string failure), failure);
			Assert.IsFalse(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, after, kind, 7, "Eshkind", 9000L,
				out KingdomArtifactRecognitionReceipt refused, out _, out failure));
			StringAssert.Contains("already recognized", failure);
			Assert.IsNull(refused);
			CollectionAssert.AreEqual(before, authority.Encode());
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBack(authority, Realm,
				out KingdomCivicArtifactsEnvelope held, out failure), failure);
			Assert.AreEqual(1, held.Recognitions.Rows.Count, "no second row may appear");
			Assert.AreEqual(id, held.Recognitions.Rows[0].RecognitionId);
			Assert.AreEqual("taf:zone:JoppaWorld.11.22.1.1.10:4:5",
				held.Recognitions.Rows[0].Source.LocationId, "the first row stays immutable");
		}

		/// <summary>
		/// A genuinely different object is a different subject, even when it wears the same name.
		/// Identity, not appearance, is what the city keeps its rows by.
		/// </summary>
		[Test]
		public void ASecondObjectWithTheSameNameIsLawfullyItsOwnSubject()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out string id);
			KingdomArtifactSnapshot twin = KingdomArtifactRecognitionServiceTests.Artifact(
				"artifact-2", "folded fullerite sword");
			Assert.AreEqual("folded fullerite sword", twin.DisplayName);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadAuthority(authority, Realm,
				out KingdomCivicMemorySectionLease lease, out _, out string failure), failure);
			Assert.IsTrue(KingdomArtifactRecognitionCommit.TryCommitPlanned(authority, lease,
				Realm, twin, KingdomArtifactRecognitionServiceTests.Kind, 7, "Eshkind", 41L,
				out KingdomArtifactRecognitionReceipt second,
				out KingdomArtifactRecognitionOutcome outcome, out failure), failure);
			Assert.AreEqual(KingdomArtifactRecognitionOutcome.Recorded, outcome);
			Assert.AreNotEqual(id, second.RecognitionId);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBack(authority, Realm,
				out KingdomCivicArtifactsEnvelope held, out failure), failure);
			Assert.AreEqual(2, held.Recognitions.Rows.Count);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBackRow(authority, Realm, id,
				out KingdomArtifactRecognitionReceipt first, out failure), failure);
			Assert.AreEqual("taf:object:artifact-1", first.Source.ObjectId);
		}

		/// <summary>The whole save writes and reads back with every row intact.</summary>
		[Test]
		public void CivicMemoryRoundTripKeepsEveryRecognitionRow()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out string id);
			byte[] saved = authority.Encode();
			KingdomCivicMemoryAuthority reloaded = new KingdomCivicMemoryAuthority(
				KingdomArtifactRecognitionServiceTests.Families());
			reloaded.AdoptSaved(saved);
			Assert.IsFalse(reloaded.Quarantined, reloaded.ReadOnlyReason);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBackRow(reloaded, Realm, id,
				out KingdomArtifactRecognitionReceipt kept, out string failure), failure);
			Assert.AreEqual(7, kept.AttributedResidentId);
			Assert.AreEqual("Eshkind", kept.AttributionName);
			Assert.AreEqual(0, kept.CommerceValue);
			Assert.IsFalse(kept.CustodyClaimed);
		}

		/// <summary>
		/// What a caller is handed is a copy. Editing it, however violently, cannot reach the save.
		/// </summary>
		[Test]
		public void ReadRowsAreCopiesAndCannotBeEditedIntoTheAuthority()
		{
			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out string id);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBack(authority, Realm,
				out KingdomCivicArtifactsEnvelope held, out string failure), failure);
			List<KingdomArtifactRecognitionReceipt> rows =
				KingdomArtifactRecognitionRegister.Rows(held);
			Assert.AreEqual(1, rows.Count);
			rows[0].Text = "the city praises the founder beyond all measure";
			rows[0].CommerceValue = 9999;
			rows[0].CustodyClaimed = true;
			rows.Clear();
			held.Recognitions.Rows.Clear();
			held.RealmId = OtherRealm;
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBackRow(authority, Realm, id,
				out KingdomArtifactRecognitionReceipt kept, out failure), failure);
			Assert.AreEqual(0, kept.CommerceValue);
			Assert.IsFalse(kept.CustodyClaimed);
			StringAssert.DoesNotContain("beyond all measure", kept.Text);
		}

		/// <summary>
		/// The copy law, by reflection rather than by inspection. Every declared field of every
		/// persisted D6 shape must survive the crossing that <c>Copy</c> and the codec make, and
		/// the counts are pinned so a field added later cannot vanish from a copy in silence.
		/// </summary>
		[Test]
		public void EveryDeclaredFieldSurvivesCopyAndTheCountsArePinned()
		{
			Assert.AreEqual(9, Fields(typeof(KingdomArtifactSnapshot)).Length,
				"a new snapshot field must be taught to the codec and this count");
			Assert.AreEqual(10, Fields(typeof(KingdomArtifactRecognitionReceipt)).Length,
				"a new receipt field must be taught to the codec and this count");
			Assert.AreEqual(2, Fields(typeof(KingdomArtifactRecognitionBook)).Length);
			Assert.AreEqual(8, Fields(typeof(KingdomCivicArtifactsEnvelope)).Length,
				"a new artifacts-envelope field must be taught to Copy and this count");

			KingdomCivicMemoryAuthority authority =
				KingdomArtifactRecognitionServiceTests.Recorded(out string id);
			Assert.IsTrue(KingdomArtifactRecognitionLease.TryReadBackRow(authority, Realm, id,
				out KingdomArtifactRecognitionReceipt kept, out string failure), failure);
			KingdomCivicArtifactsEnvelope source = new KingdomCivicArtifactsEnvelope
			{
				RealmId = Realm,
				IdentityBound = true
			};
			source.Recognitions.Rows.Add(kept);
			source.Recognitions.Revision = 1L;
			KingdomCivicArtifactsEnvelope copied = KingdomCivicArtifactsStore.Copy(source);
			Assert.AreNotSame(source.Recognitions, copied.Recognitions);
			AssertFieldsEqual(typeof(KingdomArtifactRecognitionReceipt), kept,
				copied.Recognitions.Rows[0]);
			AssertFieldsEqual(typeof(KingdomArtifactSnapshot), kept.Source,
				copied.Recognitions.Rows[0].Source);
		}

		private static FieldInfo[] Fields(Type Shape)
		{
			return Shape.GetFields(BindingFlags.Public | BindingFlags.NonPublic
				| BindingFlags.Instance | BindingFlags.DeclaredOnly);
		}

		private static void AssertFieldsEqual(Type Shape, object Left, object Right)
		{
			Assert.AreNotSame(Left, Right, Shape.Name + " must be copied, not shared");
			FieldInfo[] fields = Fields(Shape);
			Assert.Greater(fields.Length, 0);
			for (int i = 0; i < fields.Length; i++)
			{
				object left = fields[i].GetValue(Left);
				object right = fields[i].GetValue(Right);
				if (fields[i].FieldType == typeof(KingdomArtifactSnapshot)) continue;
				Assert.AreEqual(left, right, Shape.Name + "." + fields[i].Name
					+ " did not survive the copy");
			}
		}
	}
}
#endif
