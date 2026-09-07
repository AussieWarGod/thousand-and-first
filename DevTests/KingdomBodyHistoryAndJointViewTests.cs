#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomBodyHistoryAndJointViewTests
	{
		private const string Realm = "taf:realm:v1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
		private const string OtherRealm = "taf:realm:v1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

		[Test]
		public void ExactProcedureHistoryIsBoundedAtomicAndSaveSafe()
		{
			KingdomBodyHistoryBook book = new KingdomBodyHistoryBook();
			for (int i = 0; i < KingdomBodyHistoryRules.MaxRows; i++)
			{
				ClassicAssert.IsTrue(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(book,
					book.Revision, Evidence(i), out _, out string failure), failure);
			}
			KingdomBodyHistoryEnvelope envelope = new KingdomBodyHistoryEnvelope();
			ClassicAssert.IsTrue(envelope.TryBindEmptyIdentity(Realm, out string bindFailure), bindFailure);
			envelope.Book = book;
			byte[] bytes = KingdomBodyHistoryCodec.Encode(envelope);
			ClassicAssert.LessOrEqual(bytes.Length, KingdomBodyHistoryCodec.MaxEnvelopeBytes);
			ClassicAssert.AreEqual(32946, KingdomBodyHistoryCodec.MaxEnvelopeBytes);
			KingdomBodyHistoryEnvelope loaded = KingdomBodyHistoryCodec.Decode(bytes);
			ClassicAssert.IsTrue(loaded.IdentityBound); ClassicAssert.AreEqual(Realm, loaded.RealmId);
			CollectionAssert.AreEqual(bytes, KingdomBodyHistoryCodec.Encode(loaded));
			long revision = book.Revision;
			ClassicAssert.IsFalse(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(book,
				book.Revision, Evidence(99), out _, out _));
			ClassicAssert.AreEqual(revision, book.Revision);
			ClassicAssert.AreEqual(KingdomBodyHistoryRules.MaxRows, book.Rows.Count);
		}

		[Test]
		public void BodyHistoryV1OnlyMigratesEmptyAndRealmMismatchQuarantines()
		{
			KingdomBodyHistoryBook book = new KingdomBodyHistoryBook();
			ClassicAssert.IsTrue(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(book, 0,
				Evidence(1), out _, out string failure), failure);
			KingdomBodyHistoryEnvelope current = new KingdomBodyHistoryEnvelope();
			ClassicAssert.IsTrue(current.TryBindEmptyIdentity(Realm, out failure), failure);
			current.Book = book;
			byte[] v2 = KingdomBodyHistoryCodec.Encode(current);
			ClassicAssert.AreEqual(2, BitConverter.ToInt32(v2, 4));
			ClassicAssert.IsTrue(KingdomBodyHistoryStore.ReadForRealm(v2, OtherRealm,
				out failure).Quarantined); StringAssert.Contains("mismatch", failure);
			KingdomBodyHistoryEnvelope copy = current.Copy();
			ClassicAssert.AreNotSame(current.Book, copy.Book); ClassicAssert.AreEqual(Realm, copy.RealmId);

			byte[] populatedV1 = LegacyFromV2(v2); byte[] exact = (byte[])populatedV1.Clone();
			KingdomBodyHistoryEnvelope legacy = KingdomBodyHistoryCodec.Decode(populatedV1);
			ClassicAssert.IsFalse(legacy.IdentityBound);
			ClassicAssert.IsFalse(legacy.TryBindEmptyIdentity(Realm, out failure));
			ClassicAssert.IsTrue(KingdomBodyHistoryStore.ReadForRealm(populatedV1, Realm,
				out failure).Quarantined); CollectionAssert.AreEqual(exact, populatedV1);

			KingdomBodyHistoryEnvelope empty = new KingdomBodyHistoryEnvelope();
			ClassicAssert.IsTrue(empty.TryBindEmptyIdentity(Realm, out failure), failure);
			KingdomBodyHistoryEnvelope migrated = KingdomBodyHistoryStore.ReadForRealm(
				LegacyFromV2(KingdomBodyHistoryCodec.Encode(empty)), Realm, out failure);
			ClassicAssert.IsNull(failure); ClassicAssert.IsTrue(migrated.IdentityBound);
			ClassicAssert.AreEqual(Realm, migrated.RealmId);
		}

		[Test]
		public void OneExactProcedureOwnerCannotMintDifferentHistory()
		{
			KingdomBodyHistoryBook book = new KingdomBodyHistoryBook();
			KingdomWitnessedBodyEventEvidence evidence = Evidence(1);
			ClassicAssert.IsTrue(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(book, 0,
				evidence, out KingdomBodyHistoryReceipt first, out string failure), failure);
			ClassicAssert.IsTrue(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(book, 0,
				evidence, out KingdomBodyHistoryReceipt replay, out failure), failure);
			ClassicAssert.AreEqual(first.ReceiptId, replay.ReceiptId);
			ClassicAssert.AreEqual(1, book.Rows.Count);
			evidence.BodyPartFact = "a different alleged scar";
			ClassicAssert.IsFalse(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(book,
				book.Revision, evidence, out _, out _));
			ClassicAssert.AreEqual(1, book.Rows.Count);
		}

		[Test]
		public void AnatomyViewRequiresExactBodyDigestAndReflectsProsthesisFacts()
		{
			List<KingdomLiveAnatomyPart> parts = new List<KingdomLiveAnatomyPart>
			{
				new KingdomLiveAnatomyPart
				{
					NativeOrderIndex = 0,
					NativePath = "0/0",
					BodyPartId = 0,
					Type = "Hand",
					OrdinalName = "left hand",
					Category = 1,
					Extrinsic = true,
					CyberneticsBlueprint = "Cybernetic Arm"
				}
			};
			KingdomLiveAnatomySnapshot snapshot = new KingdomLiveAnatomySnapshot
			{
				ResidentIdentity = "taf:resident:1",
				BodyObjectId = "taf:object:body:1",
				OrderedParts = parts,
				ObservedTick = 10
			};
			snapshot.BodyIdentityDigest = KingdomBodyHistoryRules.AnatomyDigest(
				snapshot.ResidentIdentity, snapshot.BodyObjectId, parts);
			ClassicAssert.IsTrue(KingdomBodyHistoryRules.TryView(snapshot, out string view,
				out string failure), failure);
			StringAssert.Contains("left hand", view);
			StringAssert.Contains("Cybernetic Arm", view);
			snapshot.BodyObjectId = "taf:object:clone";
			ClassicAssert.IsFalse(KingdomBodyHistoryRules.TryView(snapshot, out _, out _));
		}

		[Test]
		public void BodyHistoryMalformedQuarantinesAndFutureIsOpaque()
		{
			KingdomBodyHistoryEnvelope future = new KingdomBodyHistoryEnvelope
			{
				OpaqueFutureVersion = KingdomBodyHistoryCodec.CurrentWireVersion + 1,
				OpaqueFuturePayload = new byte[] { 1, 2 }
			};
			byte[] bytes = KingdomBodyHistoryCodec.Encode(future);
			CollectionAssert.AreEqual(bytes, KingdomBodyHistoryCodec.Encode(
				KingdomBodyHistoryCodec.Decode(bytes)));
			bytes[12] ^= 1;
			KingdomBodyHistoryEnvelope quarantined =
				KingdomBodyHistoryStore.ReadOrEmpty(bytes, out string failure);
			ClassicAssert.IsTrue(quarantined.Quarantined);
			ClassicAssert.IsNotNull(failure);
		}

		[Test]
		public void HistoryPresentationIsChronologicalAndNamesCurrentVersusFormerForm()
		{
			KingdomWitnessedBodyEventEvidence current = Evidence(0);
			KingdomBodyHistoryBook book = new KingdomBodyHistoryBook();
			ClassicAssert.IsTrue(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(book,
				book.Revision, Evidence(2), out _, out string failure), failure);
			ClassicAssert.IsTrue(KingdomBodyHistoryRules.TryRecordWitnessedProcedure(book,
				book.Revision, current, out _, out failure), failure);
			List<KingdomLiveAnatomyPart> parts = new List<KingdomLiveAnatomyPart>
			{
				new KingdomLiveAnatomyPart { NativeOrderIndex = 0, NativePath = "0",
					Type = "Hand", OrdinalName = "left hand", Category = 1 }
			};
			KingdomLiveAnatomySnapshot anatomy = new KingdomLiveAnatomySnapshot
			{
				ResidentIdentity = current.ResidentIdentity,
				BodyObjectId = current.BodyObjectId, ObservedTick = 99, OrderedParts = parts
			};
			anatomy.BodyIdentityDigest = KingdomBodyHistoryRules.AnatomyDigest(
				anatomy.ResidentIdentity, anatomy.BodyObjectId, parts);
			ClassicAssert.IsTrue(KingdomBodyHistoryViewRules.TryCompose(anatomy, book,
				out string view, out failure), failure);
			ClassicAssert.Less(view.IndexOf("At tick 20", StringComparison.Ordinal),
				view.IndexOf("At tick 22", StringComparison.Ordinal));
			StringAssert.Contains("[current form]", view);
			StringAssert.Contains("[former form]", view);
		}

		[Test]
		public void JointViewPreservesIndependentOwnersAndCopiesInputs()
		{
			KingdomJointCivicOwnerView creed =
				KingdomJointCivicViewAdapters.CreedDeclaration("taf:realm:1", 5,
					"Mechanimists", "The realm declared for the Mechanimists.");
			KingdomJointCivicOwnerView covenant =
				KingdomJointCivicViewAdapters.CovenantMissing();
			KingdomJointCivicOwnerView moot = KingdomJointCivicViewAdapters.Invalid(
				"moot", "Invalid moot.");
			KingdomHostedArcologyAuthority authority = new KingdomHostedArcologyAuthority
			{
				Phase = KingdomHostedAuthorityPhase.Active,
				RealmId = "taf:realm:1",
				SettlementId = "taf:settlement:1",
				ZoneId = "JoppaWorld.53.3.1.1.10",
				CarrierId = "raw-engine-carrier-id",
				ConstructionJobId = "raw-job-id",
				Fault = ""
			};
			KingdomJointCivicOwnerView enclave =
				KingdomJointCivicViewAdapters.Enclave(authority, "Hosted lots are active.");
			ClassicAssert.IsTrue(KingdomJointCivicViewRules.TryBuild(creed, covenant, moot,
				enclave, out KingdomJointCivicView view, out string failure), failure);
			ClassicAssert.AreEqual(KingdomJointOwnerState.Absent, view.Covenant.State);
			ClassicAssert.AreEqual(KingdomJointOwnerState.Invalid, view.Moot.State);
			StringAssert.StartsWith("taf:hosted-enclave:v1:",
				view.Enclave.SourceReceiptId);
			ClassicAssert.IsFalse(view.Enclave.SourceReceiptId.Contains(authority.CarrierId));
			creed.Text = "changed";
			ClassicAssert.AreEqual("The realm declared for the Mechanimists.", view.Creed.Text);
		}

		private static KingdomWitnessedBodyEventEvidence Evidence(int Index)
		{
			string value = Index.ToString(System.Globalization.CultureInfo.InvariantCulture);
			return new KingdomWitnessedBodyEventEvidence
			{
				OwnerKind = KingdomBodyHistoryRules.CompletedLabProcedureKind,
				OwnerReceiptId = KingdomBodyHistoryRules.CompletedLabProcedureReceiptId(
					"game", "taf:realm:1", "5", "building", "patient", "job-" + value,
					"grafted-hand", "fingerprint", "0123456789abcdef0123456789abcdef",
					value, "0"),
				ResidentIdentity = "taf:resident:" + value,
				BodyObjectId = "taf:object:body:" + value,
				ProcedureKey = "grafted-hand",
				BodyPartFact = "hand " + value,
				WitnessedTick = 20 + Index
			};
		}

		private static byte[] LegacyFromV2(byte[] Current)
		{
			int payloadLength = BitConverter.ToInt32(Current, 8);
			int realmLength = BitConverter.ToInt32(Current, 12);
			int identityLength = 4 + realmLength + 1;
			byte[] book = new byte[payloadLength - identityLength];
			Buffer.BlockCopy(Current, 12 + identityLength, book, 0, book.Length);
			using (MemoryStream frame = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(frame))
			{
				writer.Write(0x35424654); writer.Write(1); writer.Write(book.Length);
				writer.Write(book); writer.Flush(); byte[] authenticated = frame.ToArray();
				using (SHA256 sha = SHA256.Create()) writer.Write(sha.ComputeHash(authenticated));
				writer.Flush(); return frame.ToArray();
			}
		}
	}
}
#endif
