#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The covenant owner of the joint civic view, and the register behind it.
	/// <para>
	/// One idea runs through every case: the archive is the evidence and everything the living
	/// world says is a projection beside it. A village that has come to resent the realm still
	/// sealed the covenant it sealed; a village that adores it never sealed one it did not. So the
	/// standing moves the report and never the verdict, and it never moves the source id at all.
	/// </para>
	/// </summary>
	[TestFixture]
	public sealed class KingdomVillageCovenantViewTests
	{
		private const string Realm = KingdomVillageCovenantTests.Realm;

		private static List<KingdomVillageCovenantProjection> Seen(
			KingdomVillageCovenantArchive archive, int standing, bool coherent = true,
			bool village = true)
		{
			List<KingdomVillageCovenantProjection> seen =
				new List<KingdomVillageCovenantProjection>();
			for (int i = 0; i < archive.Rows.Count; i++)
				seen.Add(new KingdomVillageCovenantProjection
				{
					ReceiptId = archive.Rows[i].ReceiptId,
					FactionCoherent = coherent,
					DeclaresVillage = village,
					CurrentStanding = standing
				});
			return seen;
		}

		private static KingdomJointCivicOwnerView Owner(KingdomVillageCovenantArchive archive,
			int standing, bool coherent = true, bool village = true)
		{
			return KingdomVillageCovenantView.Owner(KingdomVillageCovenantEvidence.Recorded,
				Realm, archive, Seen(archive, standing, coherent, village), null);
		}

		// ---- what a recorded covenant looks like -----------------------------------------

		[Test]
		public void ARecordedCovenantIsAValidOwnerNamedByItsOwnArchive()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			KingdomJointCivicOwnerView owner = Owner(archive, 600);
			Assert.AreEqual(KingdomJointOwnerState.Valid, owner.State, owner.Failure);
			Assert.AreEqual("covenant", owner.OwnerKey);
			Assert.AreEqual(KingdomVillageCovenantCodec.CurrentWireVersion, owner.SourceVersion);
			StringAssert.StartsWith(KingdomVillageCovenantView.ReceiptPrefix,
				owner.SourceReceiptId);
			StringAssert.Contains(KingdomVillageCovenantTests.Display, owner.Text);
			Assert.IsTrue(KingdomJointCivicViewRules.Valid(owner, "covenant"));
		}

		/// <summary>
		/// Standing changes the report and nothing else. If it moved the verdict, a village's mood
		/// could retroactively unmake a rite; if it moved the source id, the view's own name for
		/// this evidence would change every time somebody's feelings did.
		/// </summary>
		[Test]
		public void StandingIsReportedAsAProjectionAndNeverDecidesAnything()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			KingdomJointCivicOwnerView warm = Owner(archive, 900);
			KingdomJointCivicOwnerView cold = Owner(archive, -750);
			Assert.AreEqual(KingdomJointOwnerState.Valid, warm.State);
			Assert.AreEqual(KingdomJointOwnerState.Valid, cold.State,
				"a village that now resents the realm still sealed what it sealed");
			Assert.AreEqual(warm.SourceReceiptId, cold.SourceReceiptId,
				"the source id names the evidence, not today's mood");
			StringAssert.Contains("900", warm.Text);
			StringAssert.Contains("-750", cold.Text);
		}

		[Test]
		public void ACovenantWhoseVillageFailsItsNativeGateMakesTheOwnerInvalid()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			KingdomJointCivicOwnerView gone = Owner(archive, 600, coherent: false);
			Assert.AreEqual(KingdomJointOwnerState.Invalid, gone.State);
			StringAssert.Contains("native gate", gone.Failure);

			KingdomJointCivicOwnerView notAVillage = Owner(archive, 600, village: false);
			Assert.AreEqual(KingdomJointOwnerState.Invalid, notAVillage.State);
			StringAssert.Contains("native gate", notAVillage.Failure);
		}

		[Test]
		public void AnObservationAttachedToTheWrongCovenantIsRefused()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			List<KingdomVillageCovenantProjection> wrong = Seen(archive, 600);
			wrong[0].ReceiptId = KingdomVillageCovenantView.ReceiptPrefix + "nope";
			KingdomJointCivicOwnerView owner = KingdomVillageCovenantView.Owner(
				KingdomVillageCovenantEvidence.Recorded, Realm, archive, wrong, null);
			Assert.AreEqual(KingdomJointOwnerState.Invalid, owner.State);
			StringAssert.Contains("wrong receipt", owner.Failure);
		}

		[Test]
		public void RecordedEvidenceWithNoObservationsAtAllIsRefusedRatherThanAssumedFine()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			KingdomJointCivicOwnerView owner = KingdomVillageCovenantView.Owner(
				KingdomVillageCovenantEvidence.Recorded, Realm, archive, null, null);
			Assert.AreEqual(KingdomJointOwnerState.Invalid, owner.State);
			StringAssert.Contains("do not correspond", owner.Failure);
		}

		/// <summary>
		/// The owner is built from a whole archive that has passed its own rules, not from rows
		/// that each happen to pass on their own. An archive holding two covenants for one founding
		/// transaction would otherwise produce a confident aggregate name for a history that never
		/// happened.
		/// </summary>
		[Test]
		public void AnArchiveThatFailsItsOwnRulesCannotProduceAValidOwner()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			archive.Rows.Add(KingdomVillageCovenantTests.Row(display: "a second account"));
			archive.Rows.Sort((a, b) => string.CompareOrdinal(a.ReceiptId, b.ReceiptId));
			archive.Revision = archive.Rows.Count;
			KingdomJointCivicOwnerView owner = Owner(archive, 600);
			Assert.AreEqual(KingdomJointOwnerState.Invalid, owner.State);
			StringAssert.Contains("claim one founding transaction", owner.Failure);
		}

		[Test]
		public void AnArchiveBoundToAnotherRealmCannotProduceAValidOwner()
		{
			KingdomVillageCovenantArchive foreign =
				KingdomVillageCovenantTests.Bound(KingdomVillageCovenantTests.OtherRealm);
			Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(foreign,
				KingdomVillageCovenantTests.Row(realm: KingdomVillageCovenantTests.OtherRealm),
				KingdomVillageCovenantTests.OtherRealm, out foreign, out _, out _,
				out string appended), appended);
			KingdomJointCivicOwnerView owner = Owner(foreign, 600);
			Assert.AreEqual(KingdomJointOwnerState.Invalid, owner.State);
			StringAssert.Contains("not bound to this exact realm", owner.Failure);
		}

		// ---- every other answer, explicitly ----------------------------------------------

		/// <summary>
		/// An older save has no archive and must say so. This is the case the whole family exists
		/// for: the alternative is to read today's standing with some village, find it high, and
		/// conclude a covenant was once sealed &mdash; which is a guess about history dressed as a
		/// record of it.
		/// </summary>
		[Test]
		public void AnOlderSaveWithNoArchiveReportsExplicitAbsenceAndInfersNothing()
		{
			KingdomJointCivicOwnerView owner = KingdomVillageCovenantView.Owner(
				KingdomVillageCovenantEvidence.ArchiveAbsent, Realm, null, null, null);
			Assert.AreEqual(KingdomJointOwnerState.Absent, owner.State);
			Assert.AreEqual(0, owner.SourceVersion);
			Assert.IsEmpty(owner.SourceReceiptId);
			Assert.IsEmpty(owner.Text);
			StringAssert.Contains("No durable exact village-covenant owner", owner.Failure);
		}

		/// <summary>
		/// A realm that keeps an archive and has sealed nothing is a different answer from a save
		/// that has no archive at all. Both are "no covenant"; only one of them is a completed
		/// lookup, and collapsing them would hide a save that lost its records inside a save that
		/// never had any.
		/// </summary>
		[Test]
		public void AnArchiveWithNoCovenantsIsADifferentAnswerFromNoArchive()
		{
			KingdomJointCivicOwnerView none = KingdomVillageCovenantView.Owner(
				KingdomVillageCovenantEvidence.NoneRecorded, Realm, null, null, null);
			KingdomJointCivicOwnerView absent = KingdomVillageCovenantView.Owner(
				KingdomVillageCovenantEvidence.ArchiveAbsent, Realm, null, null, null);
			Assert.AreEqual(KingdomJointOwnerState.Absent, none.State);
			Assert.AreEqual(KingdomJointOwnerState.Absent, absent.State);
			Assert.AreNotEqual(none.Failure, absent.Failure);
			StringAssert.Contains("has recorded no covenant", none.Failure);
		}

		[TestCase(KingdomVillageCovenantEvidence.Future, "newer build")]
		[TestCase(KingdomVillageCovenantEvidence.Quarantined, "would not read")]
		[TestCase(KingdomVillageCovenantEvidence.WrongRealm, "another realm")]
		[TestCase(KingdomVillageCovenantEvidence.NativeInvalid, "native gate")]
		public void EveryRefusalIsExplicitAndFailsClosed(KingdomVillageCovenantEvidence evidence,
			string expected)
		{
			KingdomJointCivicOwnerView owner =
				KingdomVillageCovenantView.Owner(evidence, Realm, null, null, null);
			Assert.AreEqual(KingdomJointOwnerState.Invalid, owner.State);
			StringAssert.Contains(expected, owner.Failure);
			Assert.IsEmpty(owner.Text);
			Assert.IsEmpty(owner.SourceReceiptId);
		}

		[Test]
		public void AnEvidenceValueThisBuildDoesNotDefineFailsClosed()
		{
			KingdomJointCivicOwnerView owner = KingdomVillageCovenantView.Owner(
				(KingdomVillageCovenantEvidence)99, Realm, null, null, null);
			Assert.AreEqual(KingdomJointOwnerState.Invalid, owner.State);
			StringAssert.Contains("does not define", owner.Failure);
		}

		// ---- the four-owner fan-in, before and after the wire -----------------------------

		/// <summary>
		/// The covenant owner must say the same thing about an archive in memory and the same
		/// archive read back off a save. If it did not, a founder would be shown one history
		/// before a reload and another after it.
		/// </summary>
		[Test]
		public void TheFourOwnerViewSaysTheSameThingBeforeAndAfterTheArchiveGoesThroughItsWire()
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.With(
				KingdomVillageCovenantTests.Row(),
				KingdomVillageCovenantTests.Row("fedcba9876543210fedcba9876543210", "Kyakukya",
					"the people of Kyakukya"));
			Assert.IsTrue(KingdomVillageCovenantCodec.TryEncode(archive, out byte[] bytes,
				out string encode), encode);
			KingdomVillageCovenantArchive reloaded = KingdomVillageCovenantCodec.Decode(bytes);
			Assert.AreEqual(KingdomVillageCovenantState.Compatible, reloaded.State);

			KingdomJointCivicView before = Fanned(Owner(archive, 600));
			KingdomJointCivicView after = Fanned(Owner(reloaded, 600));
			Assert.AreEqual(KingdomJointOwnerState.Valid, before.Covenant.State);
			Assert.AreEqual(before.Covenant.SourceReceiptId, after.Covenant.SourceReceiptId);
			Assert.AreEqual(before.Covenant.Text, after.Covenant.Text);
			Assert.AreEqual(KingdomJointOwnerState.Valid, before.Creed.State);
			Assert.AreEqual(KingdomJointOwnerState.Invalid, before.Moot.State);
			Assert.AreEqual(KingdomJointOwnerState.Valid, before.Enclave.State);
		}

		private static KingdomJointCivicView Fanned(KingdomJointCivicOwnerView covenant)
		{
			KingdomJointCivicOwnerView creed = KingdomJointCivicViewAdapters.CreedDeclaration(
				"taf:realm:1", 5L, "Mechanimists", "The realm declared for the Mechanimists.");
			KingdomJointCivicOwnerView moot =
				KingdomJointCivicViewAdapters.Invalid("moot", "No coherent moot stands.");
			KingdomJointCivicOwnerView enclave = KingdomJointCivicViewAdapters.Enclave(
				new KingdomHostedArcologyAuthority
				{
					Phase = KingdomHostedAuthorityPhase.Active,
					RealmId = "taf:realm:1",
					SettlementId = "taf:settlement:1",
					ZoneId = "JoppaWorld.53.3.1.1.10",
					CarrierId = "raw-engine-carrier-id",
					ConstructionJobId = "raw-job-id",
					Fault = ""
				}, "Hosted lots are active.");
			Assert.IsTrue(KingdomJointCivicViewRules.TryBuild(creed, covenant, moot, enclave,
				out KingdomJointCivicView view, out string failure), failure);
			return view;
		}

		// ---- ground the realm actually holds ---------------------------------------------

		private const string Seat = "taf:settlement:v1:1111111111111111";
		private const string NonSeat = "taf:settlement:v1:2222222222222222";

		/// <summary>
		/// Ownership has to be unique before it can be evidence. Two settlements claiming one zone
		/// would otherwise let a topology fault decide which settlement an enclave belongs to, and
		/// neither claiming it is ground the realm has nothing to say about.
		/// </summary>
		[Test]
		public void GroundClaimedByTwoSettlementsOrByNoneProvesNothing()
		{
			Assert.IsFalse(KingdomJointCivicViewRules.TryProveOwnedGround(true, Seat, NonSeat,
				Seat, out string overlap));
			StringAssert.Contains("claimed by more than one settlement", overlap);

			Assert.IsFalse(KingdomJointCivicViewRules.TryProveOwnedGround(false, Seat, null,
				Seat, out string unowned));
			StringAssert.Contains("not ground the realm owns", unowned);
		}

		[Test]
		public void GroundOwnedByOneSettlementProvesOnlyThatSettlement()
		{
			Assert.IsTrue(KingdomJointCivicViewRules.TryProveOwnedGround(true, Seat, null, Seat,
				out string seated), seated);
			Assert.IsTrue(KingdomJointCivicViewRules.TryProveOwnedGround(false, Seat, NonSeat,
				NonSeat, out string outlying), outlying);

			Assert.IsFalse(KingdomJointCivicViewRules.TryProveOwnedGround(true, Seat, null,
				NonSeat, out string wrongSeat));
			StringAssert.Contains("other than the one that owns its ground", wrongSeat);
			Assert.IsFalse(KingdomJointCivicViewRules.TryProveOwnedGround(false, Seat, NonSeat,
				Seat, out string wrongOutlying));
			StringAssert.Contains("other than the one that owns its ground", wrongOutlying);
		}

		[Test]
		public void GroundWhoseOwningSettlementHasNoUsableIdentityProvesNothing()
		{
			Assert.IsFalse(KingdomJointCivicViewRules.TryProveOwnedGround(true, null, null, Seat,
				out string absent));
			StringAssert.Contains("has no identity", absent);
			Assert.IsFalse(KingdomJointCivicViewRules.TryProveOwnedGround(true, "settlement-3",
				null, "settlement-3", out string raw));
			StringAssert.Contains("not canonically named", raw);
		}

		// ---- the bounded summary and the register behind it ------------------------------

		[Test]
		public void AFullArchiveOfWideNamesStillProducesAReportInsideItsOwnBound()
		{
			KingdomVillageCovenantArchive archive = Crowded();
			KingdomJointCivicOwnerView owner = Owner(archive, 600);
			Assert.AreEqual(KingdomJointOwnerState.Valid, owner.State, owner.Failure);
			Assert.IsTrue(KingdomJointCivicViewRules.Report(owner.Text));
			StringAssert.Contains("48 village covenants stand on record", owner.Text);
		}

		/// <summary>
		/// The summary names a few covenants and counts the rest, and when even the few will not
		/// fit it stops naming and only counts. Both shapes are inside the owner's own bound; a
		/// report that truncated mid-sentence would be neither.
		/// </summary>
		[Test]
		public void TheSummaryNamesAFewAndCountsTheRestRatherThanTruncating()
		{
			KingdomVillageCovenantArchive many = Crowded("the people of ");
			string named = KingdomVillageCovenantView.Summary(many.Rows, Seen(many, 600));
			StringAssert.Contains("more, which the covenant register reads a page at a time",
				named);
			Assert.IsTrue(KingdomJointCivicViewRules.Report(named));

			KingdomVillageCovenantArchive wide = Crowded();
			string counted = KingdomVillageCovenantView.Summary(wide.Rows, Seen(wide, 600));
			StringAssert.Contains("48 village covenants stand on record", counted);
			StringAssert.DoesNotContain("sealed at standing", counted,
				"names that cannot fit are counted rather than cut off part-way");
			Assert.IsTrue(KingdomJointCivicViewRules.Report(counted));
		}

		[Test]
		public void TheRegisterHandsBackEveryCovenantAPageAtATime()
		{
			KingdomVillageCovenantArchive archive = Crowded();
			int seen = 0;
			int offset = 0;
			while (true)
			{
				Assert.IsTrue(KingdomVillageCovenantRegister.TryPage(archive, Realm, offset,
					out KingdomVillageCovenantRegister page, out string failure), failure);
				Assert.AreEqual(archive.Rows.Count, page.Total);
				Assert.AreEqual(offset, page.Offset);
				Assert.LessOrEqual(page.Count, KingdomVillageCovenantRegister.PageRows);
				for (int i = 0; i < page.Count; i++)
					Assert.AreEqual(archive.Rows[offset + i].ReceiptId, page.Row(i).ReceiptId);
				seen += page.Count;
				if (page.NextOffset >= archive.Rows.Count) break;
				offset = page.NextOffset;
			}
			Assert.AreEqual(archive.Rows.Count, seen);
		}

		[Test]
		public void TheRegisterHandsBackCopiesRatherThanTheArchivesOwnRows()
		{
			KingdomVillageCovenantArchive archive =
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row());
			Assert.IsTrue(KingdomVillageCovenantRegister.TryPage(archive, Realm, 0,
				out KingdomVillageCovenantRegister page, out string failure), failure);
			KingdomVillageCovenantReceipt row = page.Row(0);
			Assert.AreNotSame(archive.Rows[0], row);
			row.VillageDisplayName = "edited by a caller";
			Assert.AreEqual(KingdomVillageCovenantTests.Display,
				archive.Rows[0].VillageDisplayName);
			Assert.AreEqual(KingdomVillageCovenantTests.Display,
				page.Row(0).VillageDisplayName);

			// And the other direction: a page already handed out must not change because the
			// archive it was read from did.
			archive.Rows[0].VillageDisplayName = "edited in the archive";
			Assert.AreEqual(KingdomVillageCovenantTests.Display,
				page.Row(0).VillageDisplayName,
				"a page that aliased the archive would follow it");
		}

		[Test]
		public void TheRegisterRefusesEveryArchiveTheSummaryWouldRefuse()
		{
			Assert.IsFalse(KingdomVillageCovenantRegister.TryPage(
				KingdomVillageCovenantTests.Bound(KingdomVillageCovenantTests.OtherRealm), Realm,
				0, out _, out string foreign));
			StringAssert.Contains("not bound to this exact realm", foreign);

			byte[] bytes = KingdomVillageCovenantArchiveTests.Encoded();
			bytes[0] ^= 0x01;
			Assert.IsFalse(KingdomVillageCovenantRegister.TryPage(
				KingdomVillageCovenantCodec.Decode(bytes), Realm, 0, out _, out string broken));
			StringAssert.Contains("Quarantined", broken);

			Assert.IsFalse(KingdomVillageCovenantRegister.TryPage(
				KingdomVillageCovenantTests.With(KingdomVillageCovenantTests.Row()), Realm, 40,
				out _, out string past));
			StringAssert.Contains("was asked for row 40", past);
		}

		private static KingdomVillageCovenantArchive Crowded(string modest = null)
		{
			KingdomVillageCovenantArchive archive = KingdomVillageCovenantTests.Bound();
			// Every name at its own byte ceiling: 256 three-byte characters is 768 bytes, which is
			// exactly what the row was sized for. A caller wanting a readable summary passes a
			// short prefix instead.
			// Four characters short of the ceiling, so the index below still fits inside both the
			// character bound and the byte bound rather than tipping over either.
			string wide = modest ?? new string('\u4e00',
				KingdomVillageCovenantRules.MaxNameChars - 4);
			for (int i = 0; i < KingdomVillageCovenantArchive.MaxRows; i++)
			{
				string transaction = i.ToString("x2") + "0123456789abcdef0123456789abcd";
				Assert.IsTrue(KingdomVillageCovenantRules.TryAppend(archive,
					KingdomVillageCovenantTests.Row(transaction, wide + i, wide + "v" + i),
					Realm, out archive, out _, out _, out string failure), failure);
			}
			return archive;
		}
	}
}
#endif
