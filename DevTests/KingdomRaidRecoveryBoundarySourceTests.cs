#if TAF_TESTS
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	/// <summary>Protocol tripwires; native scenarios prove physical recovery outcomes.</summary>
	[TestFixture]
	public sealed class KingdomRaidRecoveryBoundarySourceTests
	{
		[Test]
		public void AutomaticRecoveryRunsAfterResumeBeforeNewThreatsAndReprovesOwner()
		{
			string wake = Method(Read("Raids/KingdomRaids.00.ProvocationAndWake.cs"), "public static void OnWorldWake(");
			Ordered(wake, "ReconcileRecoveryQuestProjection(system)", "CurrentRaidOwner(game, system, book)",
				"ResumeOpen(system, currentZone ?? The.Player?.CurrentZone)", "CurrentRaidOwner(game, system, book)",
				"ReconcileRecoveryAtSeat(system, currentZone ?? The.Player?.CurrentZone)", "CurrentRaidOwner(game, system, book)",
				"TryNaturalSnapjawProvocation(system)");
			string activation = Method(Read("Raids/KingdomRaids.01.ActivationAndRecovery.cs"), "public static void OnZoneActivated(");
			StringAssert.DoesNotContain("ReconcileRecoveryAtSeat(", activation);
			string ready = Method(Read("Raids/KingdomRaids.04.RecoveryAndFortify.cs"), "private static void ReconcileRecoveryAtSeat(");
			Ordered(ready, "!Enabled", "!KingdomMaster.AutomaticWorkAllowed(system)", "KingdomSurvey.HasBoundPass",
				"FindRecovery(", "RecoveryState != KingdomRaidRecoveryState.Active", "TryCapture(system, zone, recovery, true",
				"ResponseOperation(", "authority.DraftMatches(op)", "authority.ProvesFreshAbsence()", "PublishSimple(system, op)");
			StringAssert.DoesNotContain("excluded", ready);
		}

		[Test]
		public void ExplicitReadyTurnInRequiresFreshAbsenceAndQuestWithoutProducerGate()
		{
			string source = Method(Read("Raids/KingdomRaids.01.ActivationAndRecovery.cs"), "public static bool TryResolveRecovery(");
			Ordered(source, "RecoveryState != KingdomRaidRecoveryState.Ready", "ExactActiveRecoveryQuest(", "KingdomSurvey.HasBoundPass",
				"TryCapture(system, zone, recovery, false", "ResponseOperation(", "authority.DraftMatches(op)",
				"authority.ProvesFreshAbsence()", "authority.QuestStillExact(quest)", "PublishSimple(system, op)",
				"authority.TryPublishedRecovery(quest, out var resolved)", "FinishRecoveryQuest(resolved, quest)");
			foreach (string forbidden in new[] { "NewWorkAllowed(", "AutomaticWorkAllowed(", "!Enabled", "RecoveryState =" })
				StringAssert.DoesNotContain(forbidden, source);
		}

		[Test]
		public void RecoverySurveyRefusesAnyBoundPassAndIncompleteOrDeduplicatedCapture()
		{
			string source = Read("Growth/KingdomSurvey.11.CustodyOnly.cs");
			StringAssert.Contains("BoundSurvey != null || BoundDepth != 0", source);
			string capture = Method(source, "internal static bool TryTakeUnboundRecovery(");
			Ordered(capture, "if (zone == null || HasBoundPass) return false", "TakeCustodyOnly(zone)",
				"if (HasBoundPass || !captured.TryLoaded(out _)", "captured.ClassifiedRoots != captured.Objects.Count",
				"survey = captured; return true");
			StringAssert.Contains("catch { return false; }", capture);
			StringAssert.DoesNotContain("Take(zone)", capture);
			StringAssert.DoesNotContain("ActiveFor(zone)", capture);
		}

		[Test]
		public void AbsenceUsesCompleteFreshRootsAndDoesNotExcludeDyingOrTrustMarkerIndex()
		{
			string source = Read("Raids/KingdomRaids.RecoverySeatAuthority.cs");
			string absent = Method(source, "internal bool ProvesFreshAbsence()");
			Ordered(absent, "!Exact()", "KingdomSurvey.TryTakeUnboundRecovery(Zone, out KingdomSurvey survey)", "!Exact()",
				"foreach (GameObject actor in survey.Objects)", "GameObject.Validate(actor)", "part.OperationId == AttackId && actor.IsAlive",
				"return Exact()");
			foreach (string forbidden in new[] { "IsDying", "survey.Raiders", "CountLiveRaiders", "ObjectsFor(", ".GetObjects(", ".Map" })
				StringAssert.DoesNotContain(forbidden, absent);
			string exact = Method(source, "private bool Exact()");
			foreach (string required in new[] { "!KingdomSurvey.HasBoundPass", "CurrentRaidOwner(Game, System, Book)",
				"ReferenceEquals(Book.RaidLedger, Ledger)", "Ledger.StateRevision == Revision", "Book.RaidNextSequence == Sequence",
				"ReferenceEquals(FindRecovery(Ledger, SettlementId), Recovery)", "Recovery.RecoveryState == State",
				"!Automatic || (Enabled && KingdomMaster.AutomaticWorkAllowed(System))" }) StringAssert.Contains(required, exact);
		}

		private static string Read(string path) { return TestMain.ReadRepositoryText(path); }
		private static string Method(string source, string signature)
		{
			int start = source.IndexOf(signature, StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0, signature); int open = source.IndexOf('{', start), depth = 0;
			for (int i = open; i < source.Length; i++)
			{
				if (source[i] == '{') depth++;
				else if (source[i] == '}' && --depth == 0) return Regex.Replace(source.Substring(start, i - start + 1), @"\s+", " ");
			}
			Assert.Fail("Unclosed method: " + signature); return null;
		}
		private static void Ordered(string source, params string[] tokens)
		{
			int cursor = 0;
			foreach (string token in tokens)
			{
				int at = source.IndexOf(token, cursor, StringComparison.Ordinal);
				Assert.GreaterOrEqual(at, cursor, "Missing or reordered: " + token); cursor = at + token.Length;
			}
		}
	}
}
#endif
