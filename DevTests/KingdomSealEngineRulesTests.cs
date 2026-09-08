#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomSealEngineRulesTests
	{
		[Test]
		public void PrimaryProofStateKeepsExactIntValues()
		{
			ClassicAssert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomSealPrimaryState)));
			ClassicAssert.AreEqual(0, (int)KingdomSealPrimaryState.Unknown);
			ClassicAssert.AreEqual(1, (int)KingdomSealPrimaryState.Absent);
			ClassicAssert.AreEqual(2, (int)KingdomSealPrimaryState.Present);
		}

		[Test]
		public void PartialReadDisableRefusesEveryProfileAuthorityAndSurvivesResaveReload()
		{
			bool currentReadFailed = true;
			bool persistedDisabled = false;
			string[] authorities = new[] { "stage", "terminal", "retire", "advance",
				"reserve", "inspect", "resume", "commit", "decline", "release", "reconcile" };
			for (int i = 0; i < authorities.Length; i++)
			{
				ClassicAssert.IsFalse(KingdomSealEngineRules.SealAuthorityEnabled(
					currentReadFailed, persistedDisabled), authorities[i]
					+ " must refuse the partial returned object");
			}

			persistedDisabled = KingdomSealEngineRules.PersistSealDisabled(
				currentReadFailed, persistedDisabled);
			ClassicAssert.IsTrue(persistedDisabled);
			ClassicAssert.IsTrue(KingdomSealEngineRules.IsCanonicalDisabledSealShape(
				"", "", "", 0, 0, 0L, "", "", ""));
			ClassicAssert.IsFalse(KingdomSealEngineRules.IsCanonicalDisabledSealShape(
				"partial-lineage", "", "", 0, 0, 0L, "", "", ""));

			currentReadFailed = false;
			for (int i = 0; i < authorities.Length; i++)
			{
				ClassicAssert.IsFalse(KingdomSealEngineRules.SealAuthorityEnabled(
					currentReadFailed, persistedDisabled), authorities[i]
					+ " must remain refused after save and reload");
			}
			ClassicAssert.IsTrue(KingdomSealEngineRules.PersistSealDisabled(
				currentReadFailed, persistedDisabled));
		}

		private static KingdomSealRecord Record(KingdomSealStatus status = KingdomSealStatus.Living,
			string legacy = "legacy-one", int generation = 1, int revision = 7)
		{
			KingdomSealRecord record = new KingdomSealRecord
			{
				WriterVersion = "test",
				EngineVersion = "test",
				Status = status,
				LineageId = "lineage",
				LegacyId = legacy,
				OriginGameId = "origin",
				Generation = generation,
				Revision = revision,
				WrittenTick = 100L,
				FounderName = "Abram",
				RealmName = "Realm",
				SettlementName = "Seat",
				SettlementId = "seat",
				GroundZoneId = "JoppaWorld.1.1.1.1.10",
				TerrainBlueprint = "TerrainSaltMarsh",
				Stage = (int)GrowthStage.Camp,
				Population = 2,
				Defence = 1,
				StoredWater = 5
			};
			record.Vigour = KingdomRules.SealedVigour((GrowthStage)record.Stage,
				record.Population, record.Defence, record.StoredWater, record.Withered);
			return KingdomSealTestIdentity.Bind(record);
		}

		[Test]
		public void DeathOwnershipNeverRacesKingdomMode()
		{
			ClassicAssert.IsTrue(KingdomSealEngineRules.ObserveDeathDirectly(false, true, false));
			ClassicAssert.IsFalse(KingdomSealEngineRules.ObserveDeathDirectly(true, true, false));
			ClassicAssert.IsFalse(KingdomSealEngineRules.ObserveDeathDirectly(false, false, false));
			ClassicAssert.IsFalse(KingdomSealEngineRules.ObserveDeathDirectly(false, true, true));

			ClassicAssert.IsTrue(KingdomSealEngineRules.AcceptSuccessionTerminal(true, true, false, true));
			ClassicAssert.IsFalse(KingdomSealEngineRules.AcceptSuccessionTerminal(true, true, false, false));
			ClassicAssert.IsFalse(KingdomSealEngineRules.AcceptSuccessionTerminal(false, true, false, true));
		}

		[Test]
		public void TerminalPromotionRequiresExactScoreAndProvedAbsence()
		{
			ClassicAssert.IsTrue(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Terminal, true,
				KingdomSealPrimaryState.Absent));
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Terminal, false,
				KingdomSealPrimaryState.Absent));
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Terminal, true,
				KingdomSealPrimaryState.Present));
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Terminal, true,
				KingdomSealPrimaryState.Unknown));
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Living, true,
				KingdomSealPrimaryState.Absent));
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Retired, true,
				KingdomSealPrimaryState.Absent));
		}

		[Test]
		public void PrimaryProofRejectsDirectoriesReparsePointsAndEmptyFiles()
		{
			ClassicAssert.IsTrue(KingdomSealEngineRules.IsRegularPrimary(FileAttributes.Normal, 1L));
			ClassicAssert.IsFalse(KingdomSealEngineRules.IsRegularPrimary(FileAttributes.Normal, 0L));
			ClassicAssert.IsFalse(KingdomSealEngineRules.IsRegularPrimary(FileAttributes.Directory, 1L));
			ClassicAssert.IsFalse(KingdomSealEngineRules.IsRegularPrimary(FileAttributes.ReparsePoint, 1L));
			ClassicAssert.IsFalse(KingdomSealEngineRules.IsRegularPrimary(
				FileAttributes.ReadOnly | FileAttributes.ReparsePoint, 1L));
			ClassicAssert.IsTrue(KingdomSealEngineRules.IsDirectDirectory(FileAttributes.Directory));
			ClassicAssert.IsFalse(KingdomSealEngineRules.IsDirectDirectory(FileAttributes.Normal));
			ClassicAssert.IsFalse(KingdomSealEngineRules.IsDirectDirectory(
				FileAttributes.Directory | FileAttributes.ReparsePoint));
		}

		[Test]
		public void PrimaryProofFindsLocalOnlyAndDeduplicatesCanonicalRoots()
		{
			string root = NewPrimaryTestRoot();
			try
			{
				string synced = Path.Combine(root, "synced", "Saves");
				string local = Path.Combine(root, "local", "Saves");
				Directory.CreateDirectory(synced);
				string localGame = Path.Combine(local, "target-game");
				Directory.CreateDirectory(localGame);
				File.WriteAllText(Path.Combine(localGame, "Primary.sav.gz"), "primary");
				string failure;

				ClassicAssert.AreEqual(KingdomSealPrimaryState.Present,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure), failure);
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Present,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { local, local, local }, 64, 64, out failure), failure);

				string syncedGame = Path.Combine(synced, "target-game");
				Directory.CreateDirectory(syncedGame);
				File.WriteAllText(Path.Combine(syncedGame, "Primary.sav.gz"), "duplicate-root-copy");
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Present,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure), failure);
			}
			finally
			{
				DeletePrimaryTestRoot(root);
			}
		}

		[Test]
		public void PrimaryProofRecognizesLegacyUncompressedSaveAndRejectsEitherAmbiguousForm()
		{
			string root = NewPrimaryTestRoot();
			string legacyLink = "";
			try
			{
				string synced = Path.Combine(root, "synced", "Saves");
				string local = Path.Combine(root, "local", "Saves");
				Directory.CreateDirectory(synced);
				string game = Path.Combine(local, "target-game");
				Directory.CreateDirectory(game);
				string legacy = Path.Combine(game, "Primary.sav");
				File.WriteAllText(legacy, "legacy-primary");
				string failure;
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Present,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure), failure);

				File.WriteAllText(Path.Combine(game, "Primary.sav.gz"), "gzip-primary");
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Present,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure), failure);
				File.Delete(legacy);
				string outside = Path.Combine(root, "outside-legacy-primary");
				File.WriteAllText(outside, "legacy-primary");
				legacyLink = legacy;
				if (!TryPrimaryFileLink(legacyLink, outside)) return;
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Unknown,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure),
					"an ambiguous legacy form must dominate a valid gzip form");
			}
			finally
			{
				DeletePrimaryLink(legacyLink);
				DeletePrimaryTestRoot(root);
			}
		}

		[Test]
		public void PrimaryAbsenceRequiresEveryCanonicalRootAndAllowsSafeMissingRoot()
		{
			string root = NewPrimaryTestRoot();
			try
			{
				string syncedParent = Path.Combine(root, "synced");
				string local = Path.Combine(root, "local", "Saves");
				Directory.CreateDirectory(syncedParent);
				Directory.CreateDirectory(local);
				string missingSynced = Path.Combine(syncedParent, "Saves");
				string failure;

				ClassicAssert.AreEqual(KingdomSealPrimaryState.Absent,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { missingSynced, local }, 64, 64, out failure), failure);

				File.WriteAllText(missingSynced, "not a Saves directory");
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Unknown,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { missingSynced, local }, 64, 64, out failure));
			}
			finally
			{
				DeletePrimaryTestRoot(root);
			}
		}

		[Test]
		public void CaseVariantOriginOrPrimaryCanNeverProveAbsence()
		{
			string root = NewPrimaryTestRoot();
			try
			{
				string synced = Path.Combine(root, "synced", "Saves");
				string local = Path.Combine(root, "local", "Saves");
				Directory.CreateDirectory(synced);
				Directory.CreateDirectory(local);
				Directory.CreateDirectory(Path.Combine(local, "TARGET-GAME"));
				string failure;
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Unknown,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure));

				Directory.Delete(Path.Combine(local, "TARGET-GAME"));
				string game = Path.Combine(local, "target-game");
				Directory.CreateDirectory(game);
				File.WriteAllText(Path.Combine(game, "primary.sav.gz"), "case alias");
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Unknown,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure));
				File.Delete(Path.Combine(game, "primary.sav.gz"));
				File.WriteAllText(Path.Combine(game, "PRIMARY.SAV"), "legacy case alias");
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Unknown,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure));
			}
			finally
			{
				DeletePrimaryTestRoot(root);
			}
		}

		[Test]
		public void RedirectedLocalRootOrPrimaryMakesCombinedProofUnknown()
		{
			string root = NewPrimaryTestRoot();
			string rootLink = "";
			string primaryLink = "";
			try
			{
				string synced = Path.Combine(root, "synced", "Saves");
				string localParent = Path.Combine(root, "local");
				string redirected = Path.Combine(root, "redirected");
				Directory.CreateDirectory(synced);
				Directory.CreateDirectory(localParent);
				Directory.CreateDirectory(redirected);
				rootLink = Path.Combine(localParent, "Saves");
				if (!TryPrimaryDirectoryLink(rootLink, redirected)) return;
				string failure;
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Unknown,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, rootLink }, 64, 64, out failure));
				DeletePrimaryLink(rootLink);
				rootLink = "";

				string local = Path.Combine(localParent, "Saves");
				string game = Path.Combine(local, "target-game");
				Directory.CreateDirectory(game);
				string target = Path.Combine(root, "outside-primary");
				File.WriteAllText(target, "primary");
				primaryLink = Path.Combine(game, "Primary.sav.gz");
				if (!TryPrimaryFileLink(primaryLink, target)) return;
				ClassicAssert.AreEqual(KingdomSealPrimaryState.Unknown,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure));
			}
			finally
			{
				DeletePrimaryLink(primaryLink);
				DeletePrimaryLink(rootLink);
				DeletePrimaryTestRoot(root);
			}
		}

		private static string NewPrimaryTestRoot()
		{
			string root = Path.Combine(Path.GetTempPath(), "TAF-Primary-"
				+ Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
			return root;
		}

		private static bool TryPrimaryDirectoryLink(string Link, string Target)
		{
			try
			{
				Directory.CreateSymbolicLink(Link, Target);
				return (File.GetAttributes(Link) & FileAttributes.ReparsePoint) != 0;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryPrimaryFileLink(string Link, string Target)
		{
			try
			{
				File.CreateSymbolicLink(Link, Target);
				return (File.GetAttributes(Link) & FileAttributes.ReparsePoint) != 0;
			}
			catch
			{
				return false;
			}
		}

		private static void DeletePrimaryLink(string Pathname)
		{
			if (string.IsNullOrEmpty(Pathname)) return;
			try
			{
				FileAttributes attributes = File.GetAttributes(Pathname);
				if ((attributes & FileAttributes.ReparsePoint) == 0) return;
				if ((attributes & FileAttributes.Directory) != 0) Directory.Delete(Pathname);
				else File.Delete(Pathname);
			}
			catch
			{
			}
		}

		private static void DeletePrimaryTestRoot(string Root)
		{
			try
			{
				if (Directory.Exists(Root)) Directory.Delete(Root, true);
			}
			catch
			{
			}
		}

		[Test]
		public void AccessionTokensAreCanonicalAndGenerationAdjacent()
		{
			string first = KingdomSuccessionRules.FounderDeathToken(1, 100L, "founder-one");
			string second = KingdomSuccessionRules.FounderDeathToken(2, 200L, "founder-two");
			string failure;
			ClassicAssert.IsTrue(KingdomSealEngineRules.TryValidateAccessionTokens(0, "", "",
				out failure), failure);
			ClassicAssert.IsTrue(KingdomSealEngineRules.TryValidateAccessionTokens(1, first, "",
				out failure), failure);
			ClassicAssert.IsTrue(KingdomSealEngineRules.TryValidateAccessionTokens(2, first, second,
				out failure), failure);
			ClassicAssert.IsFalse(KingdomSealEngineRules.TryValidateAccessionTokens(2, first,
				"v1:2:200:not-base64", out failure));
			ClassicAssert.IsFalse(KingdomSealEngineRules.TryValidateAccessionTokens(2, second, second,
				out failure));
			ClassicAssert.IsFalse(KingdomSealEngineRules.TryValidateAccessionTokens(2, "", second,
				out failure));
			ClassicAssert.IsFalse(KingdomSealEngineRules.TryValidateAccessionTokens(1, "", "",
				out failure));
			ClassicAssert.IsTrue(KingdomSealEngineRules.AccessionTokenIsOrdinal(second, 2));
			ClassicAssert.IsFalse(KingdomSealEngineRules.AccessionTokenIsOrdinal(second, 1));
		}

		[Test]
		public void PollCadenceHandlesBoundaryAndClockRestoration()
		{
			ClassicAssert.IsTrue(KingdomSealEngineRules.PollDue(0L, 1L, 1200L));
			ClassicAssert.IsFalse(KingdomSealEngineRules.PollDue(100L, 1299L, 1200L));
			ClassicAssert.IsTrue(KingdomSealEngineRules.PollDue(100L, 1300L, 1200L));
			ClassicAssert.IsTrue(KingdomSealEngineRules.PollDue(2000L, 1000L, 1200L));
			ClassicAssert.IsTrue(KingdomSealEngineRules.PollDue(long.MaxValue - 10L,
				long.MaxValue, 10L));
		}

		[Test]
		public void RevisionAndGenerationNeverWrapOrLeaveSchemaBounds()
		{
			int next;
			ClassicAssert.IsTrue(KingdomSealEngineRules.TryNextRevision(int.MaxValue - 1, out next));
			ClassicAssert.AreEqual(int.MaxValue, next);
			ClassicAssert.IsFalse(KingdomSealEngineRules.TryNextRevision(int.MaxValue, out next));
			ClassicAssert.IsFalse(KingdomSealEngineRules.TryNextRevision(-1, out next));

			ClassicAssert.IsTrue(KingdomSealEngineRules.TryNextGeneration(1023, out next));
			ClassicAssert.AreEqual(1024, next);
			ClassicAssert.IsFalse(KingdomSealEngineRules.TryNextGeneration(1024, out next));
			ClassicAssert.IsFalse(KingdomSealEngineRules.TryNextGeneration(-1, out next));
		}

		[Test]
		public void SuccessfulAccessionIsExactAdjacentLivingGeneration()
		{
			KingdomSealRecord previous = Record();
			KingdomSealRecord successor = Record(legacy: "legacy-two", generation: 2, revision: 8);
			ClassicAssert.IsTrue(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));

			previous.Status = KingdomSealStatus.Retired;
			ClassicAssert.IsTrue(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));
			previous.Status = KingdomSealStatus.Terminal;
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));

			previous.Status = KingdomSealStatus.Living;
			successor.Generation = 3;
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));
			successor.Generation = 2;
			successor.Revision = 7;
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));
			successor.Revision = 8;
			successor.LegacyId = previous.LegacyId;
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));
		}

		[Test]
		public void LoadedPrimaryRestoresOnlyItsOwnOrNewerAbandonedAttempt()
		{
			KingdomSealRecord saved = Record(legacy: "legacy-one", generation: 1, revision: 7);
			KingdomSealRecord external = Record(legacy: "legacy-one", generation: 1, revision: 20);
			ClassicAssert.IsTrue(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));

			external = Record(legacy: "legacy-four", generation: 4, revision: 30);
			ClassicAssert.IsTrue(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external.Status = KingdomSealStatus.Terminal;
			ClassicAssert.IsTrue(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));

			external.Status = KingdomSealStatus.Retired;
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external = Record(legacy: "legacy-collision", generation: 1, revision: 20);
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external = Record(legacy: "legacy-old", generation: 0, revision: 30);
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external = Record(legacy: "legacy-four", generation: 4, revision: 7);
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external.Revision = 30;
			external.LineageId = "another";
			ClassicAssert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
		}

		[Test]
		public void SnapshotComparisonIgnoresJournalMechanicsButNotKingdomFacts()
		{
			KingdomSealRecord a = Record();
			KingdomSealRecord b = KingdomSealRules.Copy(a);
			b.Revision = 999;
			b.WrittenTick = 99999L;
			ClassicAssert.IsTrue(KingdomSealEngineRules.SameLivingSnapshot(a, b));

			b.Population++;
			b.Vigour = KingdomRules.SealedVigour((GrowthStage)b.Stage, b.Population,
				b.Defence, b.StoredWater, b.Withered);
			ClassicAssert.IsFalse(KingdomSealEngineRules.SameLivingSnapshot(a, b));
			b = KingdomSealRules.Copy(a);
			b.Status = KingdomSealStatus.Terminal;
			ClassicAssert.IsFalse(KingdomSealEngineRules.SameLivingSnapshot(a, b));
		}
	}
}
#endif
