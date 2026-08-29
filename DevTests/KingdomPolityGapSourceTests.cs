#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityGapSourceTests
	{
		private static string Read(string Name)
		{
			return TestMain.ReadRepositoryText(Path.Combine("Polity", Name));
		}

		[Test]
		public void EveryAuthoredGrievanceSourceHasExactProductionReachability()
		{
			string ingress = Read("KingdomPolityDiplomacyRules.GrievanceIngress.cs") +
				Read("KingdomPolityDiplomacyRules.GrievanceSources.cs");
			StringAssert.Contains("ClaimDeparture", ingress);
			StringAssert.Contains("WitnessedTrespass", ingress);
			StringAssert.Contains("BrokenPact", ingress);
			StringAssert.Contains("ResourceRefusal", ingress);
			StringAssert.Contains("RefusedTerms", ingress);
			StringAssert.Contains("WitnessedEnvoyHarm", ingress);
			StringAssert.Contains("no exact authored theft/custody receipt authority exists", ingress);
			StringAssert.DoesNotContain("Standing", ingress);
			StringAssert.Contains("TryIngestExactGrievance",
				Read("KingdomPolityVisitRuntime.Dispute.cs"));
			StringAssert.Contains("TryDeclineConsignmentWithExactGrievance",
				Read("KingdomPolityVisitInteraction.Consignment.cs"));
			StringAssert.Contains("TryRecordWitnessedTrespass",
				Read("KingdomPolityEndpointRuntime.Intervention.cs"));
			StringAssert.Contains("TryInsertBrokenPactGrievances",
				Read("KingdomPolityDiplomacyRules.Answer.cs"));
			StringAssert.Contains("TryInsertBrokenPactGrievances",
				Read("KingdomPolityClashRules.cs"));
			string harm = Read("KingdomPolityDiplomacyRules.WitnessedHarm.cs");
			StringAssert.Contains("TryRecordWitnessedEnvoyHarm", harm);
			StringAssert.Contains("KingdomPolityRules.Clone(Ledger)", harm);
			Assert.AreEqual(3, Count(harm, "KingdomPolityAuthority.Commit("),
				"initial, correspondence recovery, and capacity recovery each own one CAS");
			string harmRuntime = Read("KingdomPolityVisitInteraction.Harm.cs");
			StringAssert.Contains("TryRecordWitnessedEnvoyHarm", harmRuntime);
			// Attribution and visibility are frozen once, at death time, into the durable intent;
			// the replay above reads only Intent.Attribution and must never re-observe the body.
			string freeze = Read("KingdomPolityEndpointRuntime.Death.cs");
			StringAssert.Contains("Killer.IsPlayer()", freeze);
			StringAssert.Contains("CurrentCell.IsVisible()", freeze);
			StringAssert.Contains("KingdomPolityDeathAttribution.PlayerWitnessed", freeze);
			StringAssert.DoesNotContain("Killer.IsPlayer()", harmRuntime);
			StringAssert.Contains("Intent.Attribution ==", harmRuntime);
			StringAssert.Contains("E.Killer", Read("r_KingdomPolityCohortBody.cs"));
			StringAssert.Contains("internal static bool TryOpenGrievance",
				Read("KingdomPolityDiplomacyRules.cs"));
		}

		[Test]
		public void DeclineAndSupportCompositeBeforeTheirSingleCommit()
		{
			string decline = Read("KingdomPolityCorrespondenceRules.ConsignmentGrievance.cs");
			string trespass = Read("KingdomPolityConflictRules.TrespassGrievance.cs");
			foreach (string source in new[] { decline, trespass })
			{
				Assert.AreEqual(1, Count(source, "KingdomPolityAuthority.Commit("));
				StringAssert.Contains("KingdomPolityRules.Clone(Ledger)", source);
				StringAssert.Contains("TryDeriveExactGrievance(candidate", source);
				StringAssert.Contains("MaxGrievances", source);
				Assert.Less(source.IndexOf("TryDeriveExactGrievance(candidate",
					StringComparison.Ordinal), source.IndexOf("KingdomPolityAuthority.Commit(",
					StringComparison.Ordinal));
			}
			StringAssert.Contains("internal static bool TryDeclineConsignment",
				Read("KingdomPolityCorrespondenceRules.Consignment.cs"));
			StringAssert.Contains("internal static bool TryRecordWitnessedIntervention",
				Read("KingdomPolityConflictRules.Transactions.cs"));
		}

		[Test]
		public void EscrowSelectionIsExplicitNonMintingAndLoadedGroundOnly()
		{
			string ui = Read("KingdomPolityVisitInteraction.Escrow.cs");
			string snapshot = Read("KingdomPolityConsentedEscrowRuntime.Snapshot.cs");
			StringAssert.Contains("Choose exact collateral", ui);
			StringAssert.Contains("I understand and consent", ui);
			StringAssert.Contains("one-count object already designated as realm property", ui);
			StringAssert.Contains("IDIfAssigned", snapshot);
			StringAssert.DoesNotContain("Item.ID;", snapshot);
			StringAssert.DoesNotContain("actor?.ID;", snapshot);
			Assert.AreEqual(1, Count(snapshot, "IsTakeable()"),
				"takeability is initial eligibility, never leased reproval");
			StringAssert.Contains("RequireNearby && !Item.IsTakeable()", snapshot);
			StringAssert.Contains("Item.Holder != null", snapshot);
			StringAssert.Contains("Item.InInventory != null", snapshot);
			StringAssert.Contains("property.OwnerRealmId != realm", snapshot);
			StringAssert.Contains("zone != player?.CurrentZone", snapshot);
			StringAssert.Contains("TryObjectAvailableForLocalDebit", snapshot);
		}

		[Test]
		public void EscrowMarkerAndRecoveryCannotForcePickupOrLoadRemoteState()
		{
			string marker = Read("r_KingdomPolityEscrow.cs");
			string runtime = Read("KingdomPolityConsentedEscrowRuntime.Transactions.cs") +
				Read("KingdomPolityConsentedEscrowRuntime.Snapshot.cs");
			StringAssert.Contains("E.ID == \"CanBeTaken\") return false", marker);
			StringAssert.DoesNotContain("HasFlag(\"Forced\")", marker);
			StringAssert.Contains("BeforeApplyDamageEvent", marker);
			StringAssert.Contains("BeforeDestroyObjectEvent", marker);
			StringAssert.Contains("CanBeReplicatedEvent", marker);
			StringAssert.Contains("CanBeInvoluntarilyMovedEvent", marker);
			StringAssert.Contains("WriteNamedFields", marker);
			StringAssert.Contains("TryRecover", runtime);
			StringAssert.Contains("The.Player.CurrentZone", runtime);
			StringAssert.Contains("TryReproveMarker", runtime);
			StringAssert.Contains("TryRemoveMarker", runtime);
			StringAssert.DoesNotContain("ZoneManager", runtime);
			StringAssert.DoesNotContain("GetZone(", runtime);
			StringAssert.DoesNotContain("ObjectGameState", runtime);
			StringAssert.DoesNotContain("Obliterate", runtime);
			StringAssert.DoesNotContain("Destroy(", runtime);
			StringAssert.DoesNotContain("RemoveObject", runtime);
		}

		[Test]
		public void NewProductionPartialsStayUnderPhysicalLineCap()
		{
			string[] files =
			{
				"KingdomPolityGrievanceIngestionModels.cs",
				"KingdomPolityDiplomacyRules.GrievanceIngress.cs",
				"KingdomPolityDiplomacyRules.GrievanceSources.cs",
				"KingdomPolityDiplomacyRules.WitnessedHarm.cs",
				"KingdomPolityCorrespondenceRules.ConsignmentGrievance.cs",
				"KingdomPolityConflictRules.TrespassGrievance.cs",
				"KingdomPolityConsentedEscrowModels.cs",
				"KingdomPolityConflictRules.ConsentedEscrowPrepare.cs",
				"KingdomPolityConflictRules.ConsentedEscrowCustody.cs",
				"KingdomPolityConflictRules.ConsentedEscrowConclusion.cs",
				"KingdomPolityConflictRules.ConsentedEscrowRefund.cs",
				"KingdomPolityEndpointRuntime.Escrow.cs",
				"KingdomPolityConsentedEscrowRuntime.Snapshot.cs",
				"KingdomPolityConsentedEscrowRuntime.Transactions.cs",
				"KingdomPolityVisitInteraction.Escrow.cs",
				"KingdomPolityVisitInteraction.Harm.cs", "r_KingdomPolityEscrow.cs"
			};
			for (int i = 0; i < files.Length; i++)
			{
				int lines = Read(files[i]).Split(new[] { '\n' }).Length;
				Assert.Less(lines, 300, files[i] + " exceeds the production line cap");
			}
		}

		private static int Count(string Source, string Needle)
		{
			int count = 0, at = 0;
			while ((at = Source.IndexOf(Needle, at, StringComparison.Ordinal)) >= 0)
			{
				count++; at += Needle.Length;
			}
			return count;
		}
	}
}
#endif
