#if TAF_TESTS
using System;
using System.IO;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomPolityConflictSourceTests
	{
		private static string Read(string Name)
		{
			return TestMain.ReadRepositoryText(Path.Combine("Polity", Name));
		}

		[Test]
		public void InterventionRequiresEveryExactBodyInCurrentLoadedEndpoint()
		{
			string runtime = Read("KingdomPolityEndpointRuntime.Intervention.cs");
			StringAssert.Contains("TryAdmit(System", runtime);
				StringAssert.Contains("TryObserve(zone, admitted.RealmId, cohort, receipt", runtime);
			StringAssert.Contains("cohort.Phase != KingdomPolityCohortPhase.Materialized", runtime);
			StringAssert.Contains("projection is physically incomplete", runtime);
			StringAssert.DoesNotContain("ZoneManager", runtime);
			StringAssert.DoesNotContain("GetZone", runtime);
			StringAssert.DoesNotContain("GameObject.Create", runtime);
		}

		[Test]
		public void PlayerChoosesStanceAndOnlyExplicitMediationCreatesPeace()
		{
			string interaction = Read("KingdomPolityVisitInteraction.Conflict.cs");
			StringAssert.Contains("Mediate a ceasefire", interaction);
			StringAssert.Contains("Stand with the settlement", interaction);
			StringAssert.Contains("Stand with the visitors", interaction);
			StringAssert.Contains("Observe without taking a side", interaction);
			StringAssert.Contains("TryRecordCurrentEndpointIntervention", interaction);
			StringAssert.Contains("TryConcludeCurrentEndpointClash", interaction);
			StringAssert.Contains("KingdomPolityRelationBand.Truce", interaction);
			StringAssert.DoesNotContain("ZoneManager", interaction);
			StringAssert.DoesNotContain("GetZone", interaction);
		}

		[Test]
		public void AftermathIsNeutralProofNotHiddenBattleSimulation()
		{
			string models = Read("KingdomPolityConflictModels.cs");
			string fold = Read("KingdomPolityClashRules.cs");
			string validation = Read("KingdomPolityConflictRules.Validation.cs");
			StringAssert.Contains("Ceasefire", models);
			StringAssert.Contains("WitnessedWithdrawal", models);
			StringAssert.Contains("TryCreateAftermath", fold);
			StringAssert.Contains("TryValidateGraph", validation);
			StringAssert.DoesNotContain("WinnerId", models + fold);
			StringAssert.DoesNotContain("CasualtyCount", models + fold);
			StringAssert.DoesNotContain("ConqueredSettlement", models + fold);
			StringAssert.DoesNotContain("Random", models + fold);
		}

		[Test]
		public void CurrentWirePersistsConflictRowsAndPriorWireCannotDropThem()
		{
			string incidents = Read("KingdomPolityCodec.IncidentRows.cs");
			string envelope = Read("KingdomPolityCodec.Envelope.cs");
			StringAssert.Contains("WriteNullable(W, V.Intervention, WriteIntervention)", incidents);
			StringAssert.Contains("WriteNullable(W, V.Aftermath, WriteAftermath)", incidents);
			StringAssert.Contains("RequireNoV5IncidentTransactions", envelope);
			StringAssert.Contains("Ledger.Incidents[i].Intervention != null", envelope);
			StringAssert.Contains("Ledger.Incidents[i].Aftermath != null", envelope);
		}
	}
}
#endif
