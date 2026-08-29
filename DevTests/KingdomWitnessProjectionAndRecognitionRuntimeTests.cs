#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Tests;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomWitnessProjectionAndRecognitionRuntimeTests
	{
		[Test]
		public void RecognitionPreparationCopiesAuthorityBeforeMutation()
		{
			KingdomArtifactRecognitionBook current = new KingdomArtifactRecognitionBook();
			KingdomArtifactSnapshot snapshot = Artifact();
			byte[] before = KingdomArtifactRecognitionCodec.Encode(current);
			Assert.IsTrue(KingdomArtifactRecognitionSelectionRuntime.TryPrepareRecognition(
				current, 0L, snapshot, KingdomArtifactRecognitionKind.Remark, 7, "Eshkind",
				20L, out KingdomArtifactRecognitionBook candidate,
				out KingdomArtifactRecognitionReceipt receipt, out string failure), failure);
			CollectionAssert.AreEqual(before, KingdomArtifactRecognitionCodec.Encode(current));
			Assert.AreEqual(0L, current.Revision);
			Assert.AreEqual(1L, candidate.Revision);
			Assert.AreEqual(receipt.RecognitionId, candidate.Rows[0].RecognitionId);
			Assert.AreNotSame(current, candidate);
			Assert.AreNotSame(snapshot, receipt.Source);
		}

		[Test]
		public void RecognitionPreparationFailureLeavesAuthorityUntouched()
		{
			KingdomArtifactRecognitionBook current = new KingdomArtifactRecognitionBook();
			byte[] before = KingdomArtifactRecognitionCodec.Encode(current);
			Assert.IsFalse(KingdomArtifactRecognitionSelectionRuntime.TryPrepareRecognition(
				current, 1L, Artifact(), KingdomArtifactRecognitionKind.Inscription, 0, null,
				20L, out KingdomArtifactRecognitionBook candidate,
				out KingdomArtifactRecognitionReceipt receipt, out _));
			Assert.IsNull(candidate);
			Assert.IsNull(receipt);
			CollectionAssert.AreEqual(before, KingdomArtifactRecognitionCodec.Encode(current));
		}

		[Test]
		public void WitnessCarrierIdentityCannotBeReusedAfterTeardown()
		{
			KingdomWitnessWorkBook book = new KingdomWitnessWorkBook();
			KingdomWitnessWorkSource source = new KingdomWitnessWorkSource
			{
				EventId = "taf:event:closed:1",
				SettlementId = "taf:settlement:seat",
				EventKind = KingdomWitnessWorkRules.RaisingAdapterKind,
				EventText = "the west cistern was sealed",
				ClosedTick = 10L,
				MakerResidentId = 7,
				MakerName = "Eshkind"
			};
			source.SnapshotDigest = KingdomWitnessWorkRules.SnapshotDigest(source);
			Assert.IsTrue(KingdomWitnessWorkRules.TryCapture(book, 0L, source,
				out KingdomWitnessWorkReceipt receipt, out string failure), failure);
			Assert.IsTrue(KingdomWitnessWorkRules.TryPrepareCarrier(book, book.Revision,
				receipt.WorkId, "taf:object:surface-1", "taf:zone:seat",
				"taf:construction:surface-1", 4, 5, 11L,
				out failure), failure);
			Assert.IsFalse(KingdomWitnessWorkProjectionRuntime.TryRequireUnclaimed(book,
				"taf:object:surface-1", out _));
			Assert.IsTrue(KingdomWitnessWorkProjectionRuntime.TryRequireUnclaimed(book,
				"taf:object:surface-2", out failure), failure);
			Assert.IsTrue(KingdomWitnessWorkRules.TryReconcileCarrier(book, book.Revision,
				receipt.WorkId, false, true, 12L, out failure), failure);
			Assert.IsFalse(KingdomWitnessWorkProjectionRuntime.TryRequireUnclaimed(book,
				"taf:object:surface-1", out _));
		}

		[Test]
		public void WitnessPartOwnsOnlyPresentationAndZeroValueAnswers()
		{
			string source = Read("Experience/r_KingdomWitnessWorkProjection.cs");
			StringAssert.Contains("GetShortDescriptionEvent.ID", source);
			StringAssert.Contains("GetShortDescriptionEvent.cs 47-68", source);
			StringAssert.Contains("GameObject.cs", source);
			StringAssert.Contains("565-592", source);
			StringAssert.Contains("GetIntrinsicValueEvent.ID", source);
			StringAssert.Contains("GetExtrinsicValueEvent.ID", source);
			Assert.AreEqual(2, Occurrences(source, "E.Value = 0.0"));
			StringAssert.Contains("Registrar.Register(\"CanBeTaken\")", source);
			StringAssert.Contains("HandleEvent(CanBeReplicatedEvent E)", source);
			StringAssert.Contains("ShapeMatchesParent() ? false : base.HandleEvent(E)", source);
			StringAssert.Contains("E?.Postfix != null && ShapeMatchesParent()", source);
			StringAssert.Contains("E.ID == \"CanBeTaken\" && ShapeMatchesParent()", source);
			Assert.AreEqual(2, Occurrences(source,
				"E != null && ShapeMatchesParent()) E.Value = 0.0"));
			StringAssert.Contains("ShapeMatchesParent() ? false : base.CanGenerateStacked()", source);
			StringAssert.Contains("FieldsAuthenticated()", source);
			StringAssert.Contains("ProjectionProof", source);
			Assert.IsFalse(source.Contains("ordinary object properties apply"),
				"an unauthenticated marker must be wholly inert, including look text");
			StringAssert.Contains("FinalizeCopy", source);
			StringAssert.Contains("ParentObject?.RemovePart(this)", source);
			Assert.IsFalse(source.Contains("Description.Short"));
			Assert.IsFalse(source.Contains("DisplayName ="));
			Assert.IsFalse(source.Contains("Physics.Owner ="));
			Assert.IsFalse(source.Contains("SetStringProperty"));
			Assert.IsFalse(source.Contains("Journal"));
		}

		[Test]
		public void WitnessRuntimeRequiresIndependentBuiltExactSurfaceAndReadback()
		{
			string source = Read("Experience/KingdomWitnessWorkProjectionRuntime.cs");
			source += Read("Experience/KingdomWitnessWorkProjectionRuntime.Readback.cs");
			StringAssert.Contains("GameObject.cs 424-451", source);
			StringAssert.Contains("517-532", source);
			StringAssert.Contains("8945-8952", source);
			StringAssert.Contains("Physics.cs 136-146", source);
			StringAssert.Contains("GetIntProperty(\"KingdomBuilt\") != 1", source);
			StringAssert.Contains("r_KingdomCairn", source);
			StringAssert.Contains("r_KingdomGraveGrove", source);
			StringAssert.Contains("r_KingdomNicheTomb", source);
			StringAssert.Contains("Survey.Cairns", source);
			StringAssert.Contains("KingdomRemembranceRuntime.MemorialForProperty", source);
			StringAssert.Contains("r_KingdomRemembranceProjection", source);
			StringAssert.Contains("r_KingdomOfficeProjection", source);
			StringAssert.Contains("Carrier.Inventory != null", source);
			StringAssert.Contains("PhysicalPhenomena.xml 20-25", source);
			StringAssert.Contains("commerce.Value == 0.0 || commerce.Value == 0.01", source);
			StringAssert.Contains("Carrier.ValueEach != 0.0", source);
			StringAssert.Contains("KingdomWitnessCarrierObservation.Missing", source);
			StringAssert.Contains("KingdomWitnessCarrierObservation.Ambiguous", source);
			StringAssert.Contains("KingdomWitnessCarrierObservation.Diverged", source);
			StringAssert.Contains("TryDetach", source);
			StringAssert.Contains("if (marker == null) return true", source);
			StringAssert.Contains("Marker.FieldsAuthenticated()", source);
			Assert.IsFalse(source.Contains("GameObjectFactory"));
			Assert.IsFalse(source.Contains("AddObject"));
			Assert.IsFalse(source.Contains("Journal"));
			Assert.IsFalse(source.Contains("KingdomExperience"));
		}

		[Test]
		public void ExplicitRecognitionReadsOnlyBoundedGroundAndPreservesCustody()
		{
			string source = Read("Experience/KingdomArtifactRecognitionSelectionRuntime.cs");
			source += Read("Experience/KingdomArtifactRecognitionSelectionRuntime.Prepare.cs");
			StringAssert.Contains("Cell.cs", source);
			StringAssert.Contains("4854-4857", source);
			StringAssert.Contains("7443-7462", source);
			StringAssert.Contains("origin.GetLocalAdjacentCells()", source);
			StringAssert.Contains("cells[c].GetObjects()", source);
			StringAssert.Contains("MaxNearbyChoices = 64", source);
			StringAssert.Contains("Selected.Holder", source);
			StringAssert.Contains("Selected.Physics.Owner", source);
			StringAssert.Contains("TrySnapshotExplicit(Selected", source);
			StringAssert.Contains("KingdomArtifactRecognitionCodec.Decode", source);
			Assert.IsFalse(source.Contains("KingdomProperty"));
			Assert.IsFalse(source.Contains("OwnedByPlayer"));
			Assert.IsFalse(source.Contains("IsTakeable"));
			Assert.IsFalse(source.Contains("Owner ="));
			Assert.IsFalse(source.Contains("AddObject"));
			Assert.IsFalse(source.Contains("RemoveObject"));
			Assert.IsFalse(source.Contains("TakeObject"));
			Assert.IsFalse(source.Contains("Journal"));
		}

		private static KingdomArtifactSnapshot Artifact()
		{
			KingdomArtifactSnapshot result = new KingdomArtifactSnapshot
			{
				ObjectId = "taf:object:artifact-1",
				Blueprint = "Fullerite Long Sword",
				DisplayName = "folded fullerite sword",
				OwnerId = "taf:owner:player",
				LocationId = "taf:zone:JoppaWorld.11.22.1.1.10:4:5",
				DeedId = "taf:deed:reef",
				DeedText = "the crossing of the rusted reef",
				ObservedTick = 10L
			};
			result.SnapshotDigest = KingdomArtifactRecognitionRules.SnapshotDigest(result);
			return result;
		}

		private static string Read(string Path)
		{
			return TestMain.ReadRepositoryText(Path);
		}

		private static int Occurrences(string Text, string Needle)
		{
			int count = 0;
			int at = 0;
			while ((at = Text.IndexOf(Needle, at, System.StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += Needle.Length;
			}
			return count;
		}
	}
}
#endif
