#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Tests;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomBodyHistoryDurabilitySourceTests
	{
		[Test]
		public void CommissionFreezesExactRulerLifeBeforeAnyReservation()
		{
			string source = Read("Growth/KingdomLab.Commission.cs");
			int freeze = source.IndexOf("TryFreezeRulerLife", System.StringComparison.Ordinal);
			Assert.That(freeze, Is.GreaterThan(0));
			Assert.That(freeze, Is.LessThan(source.IndexOf("TryReserveExactWater",
				System.StringComparison.Ordinal)));
			Assert.That(freeze, Is.LessThan(source.IndexOf("ReserveBits(",
				System.StringComparison.Ordinal)));
			StringAssert.Contains("RulerSuccessionOrdinal = rulerLife.SuccessionOrdinal", source);
			StringAssert.Contains("RulerLifeId = rulerLife.RulerLifeId", source);
			string job = Read("Growth/r_KingdomLabJob.cs");
			StringAssert.Contains("Writer.WriteNamedFields(this, typeof(r_KingdomLabJob))", job);
			StringAssert.Contains("Reader.ReadNamedFields(this, typeof(r_KingdomLabJob))", job);
			StringAssert.Contains("ValidIdentity(RealmId", job);
			StringAssert.Contains("NormalizeBodyHistory", job);
			string contract = Read("Growth/r_KingdomLabJob.BodyHistory.cs");
			StringAssert.Contains("BodyHistoryPartFact", contract);
			StringAssert.Contains("LegacyPhysicalOnly", contract);
			StringAssert.Contains("TryResolveLoaded", contract);
		}

		[Test]
		public void RulerLifeReadIsCurrentLoadedAndNeverSynthesizesKingdomOrdinal()
		{
			string source = Read("Core/KingdomBodyHistoryRulerLifeRuntime.cs");
			StringAssert.Contains("ReferenceEquals(The.Player, Actor)", source);
			StringAssert.Contains("Actor.CurrentZone == null", source);
			StringAssert.Contains("Actor.CurrentCell == null", source);
			StringAssert.Contains("IDIfAssigned", source);
			StringAssert.Contains("kingdomMode && succession == null", source);
			StringAssert.Contains("TryReadStableRulerOrdinal", source);
			StringAssert.Contains("PendingDeathToken", source);
			StringAssert.Contains("InterregnumPhase.RiteDue", source);
			Assert.That(source, Does.Not.Contain("RequireSystem"));
			Assert.That(source, Does.Not.Contain("ZoneManager"));
			Assert.That(source, Does.Not.Contain("FindByID"));
		}

		[Test]
		public void FrozenRulerLifeIsPartOfCanonicalLabAuthority()
		{
			string entry = Read("Growth/KingdomLabRegistryEntry.cs");
			StringAssert.Contains("RulerSuccessionOrdinal", entry);
			StringAssert.Contains("RulerLifeId", entry);
			string registry = Read("Growth/KingdomLabRules.RegistryIdentityWire.cs");
			StringAssert.Contains("lines[0] == \"v2\"", registry);
			StringAssert.Contains("RulerSuccessionOrdinal = ordinal", registry);
			StringAssert.Contains("RulerLifeId = life", registry);
			string codec = Read("Growth/KingdomLabRules.RegistryCodec.cs");
			StringAssert.Contains("HasBoundRulerLife", codec);
			StringAssert.Contains("Encode(row.RulerLifeId)", codec);
			string contracts = Read("Growth/KingdomLabRules.Contracts.cs");
			StringAssert.Contains("Entry.RulerSuccessionOrdinal == Expected.RulerSuccessionOrdinal",
				contracts);
			StringAssert.Contains("Entry.RulerLifeId, Expected.RulerLifeId", contracts);
		}

		[Test]
		public void ExactWitnessAndFreshC18ReadbackPrecedeEveryCompletePurge()
		{
			string evidence = Read("Growth/KingdomLab.BodyHistoryEvidence.cs");
			StringAssert.Contains("ReferenceEquals(The.Player, Actor)", evidence);
			StringAssert.Contains("ReferenceEquals(Actor.CurrentZone, building.CurrentZone)",
				evidence);
			StringAssert.Contains("rulerLife.SuccessionOrdinal != Job.RulerSuccessionOrdinal",
				evidence);
			StringAssert.Contains("ExactLiveBodyPart", evidence);
			StringAssert.Contains("EffectNonces", evidence);
			StringAssert.Contains("BodyPartFact = Job.BodyHistoryPartFact", evidence);
			StringAssert.Contains("Job.BodyHistoryEffectNonce = effectNonce", evidence);
			StringAssert.Contains("Job.BodyHistoryOwnerReceiptId = owner", evidence);
			StringAssert.Contains("CompletedBodyHistoryOwner(Job, effectNonce)", evidence);

			string commit = Read("Growth/KingdomLab.BodyHistoryCommit.cs");
			Assert.That(Count(commit, "TryReadSection("), Is.EqualTo(2));
			StringAssert.Contains("TryCommitSection(lease", commit);
			StringAssert.Contains("ContainsExact(readback.Payload()", commit);
			string application = Read("Growth/KingdomLab.Application.cs");
			int delivery = application.IndexOf("SettleCompletedBodyHistory",
				System.StringComparison.Ordinal);
			Assert.That(delivery, Is.GreaterThan(0));
			Assert.That(delivery, Is.LessThan(application.IndexOf(
				"FinalizeApplicationProjection(Actor, Job, KingdomLabRegistryStatus.Complete)",
				System.StringComparison.Ordinal)));
			string deliverySource = Read("Growth/KingdomLab.BodyHistoryDelivery.cs");
			StringAssert.Contains("TryFreezeCompletedBodyHistoryWitness", deliverySource);
			StringAssert.Contains("TryCommitCompletedBodyHistory", deliverySource);
			Assert.That(deliverySource.IndexOf("TryFreezeCompletedBodyHistoryWitness",
				System.StringComparison.Ordinal), Is.LessThan(deliverySource.IndexOf(
				"TryCommitCompletedBodyHistory", System.StringComparison.Ordinal)));
			string retirement = Read("Core/KingdomRealmRetirementAuthority.Inspection.cs");
			StringAssert.Contains("rows[i].Status == KingdomLabRegistryStatus.Active",
				retirement);
			StringAssert.Contains("rows[i].Status == KingdomLabRegistryStatus.Quarantined",
				retirement);
			string purge = Read("Growth/KingdomLab.cs");
			int history = purge.IndexOf("SettleCompletedBodyHistory",
				System.StringComparison.Ordinal);
			Assert.That(history, Is.GreaterThan(0));
			int terminalReplay = purge.IndexOf("RecordReplayProof(\"apply:\"", history,
				System.StringComparison.Ordinal);
			Assert.That(terminalReplay, Is.GreaterThan(history));
			Assert.That(history, Is.LessThan(purge.IndexOf("Building.RemovePart(Job)",
				System.StringComparison.Ordinal)));
			StringAssert.Contains("AllowsPhysicalCleanup", purge);
			string callers = application
				+ Read("Growth/KingdomLab.Semantic.cs");
			Assert.That(Count(callers, "PurgeApplicationReceipt("), Is.EqualTo(4));
			Assert.That(Count(callers, "Actor, System, Job"), Is.GreaterThanOrEqualTo(3));
		}

		[Test]
		public void CurrentViewReadsOnlyExactBodyAndSectionThree()
		{
			string source = Read("Core/KingdomBodyHistoryRuntime.Open.cs");
			StringAssert.Contains("TryReadCurrent", source);
			StringAssert.Contains("TryReadLoaded", source);
			StringAssert.Contains("SectionBodyHistory", source);
			StringAssert.Contains("TryReadSection", source);
			StringAssert.Contains("TryComposeWithoutHistory", source);
			Assert.That(source, Does.Not.Contain("TryCommit"));
			Assert.That(source, Does.Not.Contain("RequireSystem"));
			Assert.That(source, Does.Not.Contain("FindByID"));
			Assert.That(source, Does.Not.Contain("GetZone("));
			Assert.That(source, Does.Not.Contain("SetString"));
			Assert.That(source, Does.Not.Contain("Damage"));
			Assert.That(source, Does.Not.Contain("Scar"));
		}

		[Test]
		public void NativeAnatomySnapshotNeverMintsLazyPartIds()
		{
			string source = Read("Core/KingdomBodyHistoryRuntime.cs");
			StringAssert.Contains("GetParts()", source);
			StringAssert.Contains("NativeOrderIndex = parts.Count", source);
			StringAssert.Contains("NativePath = ReadNativePath(part)", source);
			StringAssert.Contains("BodyPartId = part._ID", source);
			Assert.That(source, Does.Not.Contain("part.ID"));
			Assert.That(source, Does.Not.Contain("part._ID <= 0"));
		}

		[Test]
		public void D5ProductionFilesRemainReviewable()
		{
			string[] files =
			{
				"Core/KingdomBodyHistoryRulerLife.cs",
				"Core/KingdomBodyHistoryRulerLifeRuntime.cs",
				"Core/KingdomBodyHistoryRules.NativeIdentity.cs",
				"Core/KingdomLabBodyHistoryContractRules.cs",
				"Core/KingdomBodyHistoryTransactions.cs",
				"Core/KingdomBodyHistoryViewRules.cs",
				"Core/KingdomBodyHistoryRuntime.Open.cs",
				"Growth/KingdomLab.BodyHistoryCommission.cs",
				"Growth/KingdomLab.BodyHistoryEvidence.cs",
				"Growth/KingdomLab.BodyHistoryCommit.cs",
				"Growth/KingdomLab.BodyHistoryDelivery.cs",
				"Growth/KingdomLab.Commission.cs",
				"Growth/KingdomLab.cs",
				"Growth/KingdomLab.Application.cs",
				"Growth/KingdomLab.Semantic.cs",
				"Growth/KingdomLabRegistryEntry.cs",
				"Growth/KingdomLabRules.Contracts.cs",
				"Growth/KingdomLabRules.RegistryCodec.cs",
				"Growth/KingdomLabRules.RegistryIdentityWire.cs",
				"Growth/r_KingdomLabJob.cs",
				"Growth/r_KingdomLabJob.BodyHistory.cs"
			};
			for (int i = 0; i < files.Length; i++)
				Assert.That(Read(files[i]).Split('\n').Length, Is.LessThan(300), files[i]);
		}

		private static string Read(string path) => TestMain.ReadRepositoryText(path);

		private static int Count(string text, string value)
		{
			int count = 0;
			for (int at = 0; (at = text.IndexOf(value, at,
				System.StringComparison.Ordinal)) >= 0; at += value.Length) count++;
			return count;
		}
	}
}
#endif
