#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomSealEngineRulesTests
	{
		[Test]
		public void PrimaryProofStateKeepsExactIntValues()
		{
			Assert.AreEqual(typeof(int), Enum.GetUnderlyingType(typeof(KingdomSealPrimaryState)));
			Assert.AreEqual(0, (int)KingdomSealPrimaryState.Unknown);
			Assert.AreEqual(1, (int)KingdomSealPrimaryState.Absent);
			Assert.AreEqual(2, (int)KingdomSealPrimaryState.Present);
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
				Assert.IsFalse(KingdomSealEngineRules.SealAuthorityEnabled(
					currentReadFailed, persistedDisabled), authorities[i]
					+ " must refuse the partial returned object");
			}

			persistedDisabled = KingdomSealEngineRules.PersistSealDisabled(
				currentReadFailed, persistedDisabled);
			Assert.IsTrue(persistedDisabled);
			Assert.IsTrue(KingdomSealEngineRules.IsCanonicalDisabledSealShape(
				"", "", "", 0, 0, 0L, "", "", ""));
			Assert.IsFalse(KingdomSealEngineRules.IsCanonicalDisabledSealShape(
				"partial-lineage", "", "", 0, 0, 0L, "", "", ""));

			currentReadFailed = false;
			for (int i = 0; i < authorities.Length; i++)
			{
				Assert.IsFalse(KingdomSealEngineRules.SealAuthorityEnabled(
					currentReadFailed, persistedDisabled), authorities[i]
					+ " must remain refused after save and reload");
			}
			Assert.IsTrue(KingdomSealEngineRules.PersistSealDisabled(
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
			Assert.IsTrue(KingdomSealEngineRules.ObserveDeathDirectly(false, true, false));
			Assert.IsFalse(KingdomSealEngineRules.ObserveDeathDirectly(true, true, false));
			Assert.IsFalse(KingdomSealEngineRules.ObserveDeathDirectly(false, false, false));
			Assert.IsFalse(KingdomSealEngineRules.ObserveDeathDirectly(false, true, true));

			Assert.IsTrue(KingdomSealEngineRules.AcceptSuccessionTerminal(true, true, false, true));
			Assert.IsFalse(KingdomSealEngineRules.AcceptSuccessionTerminal(true, true, false, false));
			Assert.IsFalse(KingdomSealEngineRules.AcceptSuccessionTerminal(false, true, false, true));
		}

		[Test]
		public void TerminalPromotionRequiresExactScoreAndProvedAbsence()
		{
			Assert.IsTrue(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Terminal, true,
				KingdomSealPrimaryState.Absent));
			Assert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Terminal, false,
				KingdomSealPrimaryState.Absent));
			Assert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Terminal, true,
				KingdomSealPrimaryState.Present));
			Assert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Terminal, true,
				KingdomSealPrimaryState.Unknown));
			Assert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Living, true,
				KingdomSealPrimaryState.Absent));
			Assert.IsFalse(KingdomSealEngineRules.MayPromote(KingdomSealStatus.Retired, true,
				KingdomSealPrimaryState.Absent));
		}

		[Test]
		public void PrimaryProofRejectsDirectoriesReparsePointsAndEmptyFiles()
		{
			Assert.IsTrue(KingdomSealEngineRules.IsRegularPrimary(FileAttributes.Normal, 1L));
			Assert.IsFalse(KingdomSealEngineRules.IsRegularPrimary(FileAttributes.Normal, 0L));
			Assert.IsFalse(KingdomSealEngineRules.IsRegularPrimary(FileAttributes.Directory, 1L));
			Assert.IsFalse(KingdomSealEngineRules.IsRegularPrimary(FileAttributes.ReparsePoint, 1L));
			Assert.IsFalse(KingdomSealEngineRules.IsRegularPrimary(
				FileAttributes.ReadOnly | FileAttributes.ReparsePoint, 1L));
			Assert.IsTrue(KingdomSealEngineRules.IsDirectDirectory(FileAttributes.Directory));
			Assert.IsFalse(KingdomSealEngineRules.IsDirectDirectory(FileAttributes.Normal));
			Assert.IsFalse(KingdomSealEngineRules.IsDirectDirectory(
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

				Assert.AreEqual(KingdomSealPrimaryState.Present,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure), failure);
				Assert.AreEqual(KingdomSealPrimaryState.Present,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { local, local, local }, 64, 64, out failure), failure);

				string syncedGame = Path.Combine(synced, "target-game");
				Directory.CreateDirectory(syncedGame);
				File.WriteAllText(Path.Combine(syncedGame, "Primary.sav.gz"), "duplicate-root-copy");
				Assert.AreEqual(KingdomSealPrimaryState.Present,
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
				Assert.AreEqual(KingdomSealPrimaryState.Present,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure), failure);

				File.WriteAllText(Path.Combine(game, "Primary.sav.gz"), "gzip-primary");
				Assert.AreEqual(KingdomSealPrimaryState.Present,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure), failure);
				File.Delete(legacy);
				string outside = Path.Combine(root, "outside-legacy-primary");
				File.WriteAllText(outside, "legacy-primary");
				legacyLink = legacy;
				if (!TryPrimaryFileLink(legacyLink, outside)) return;
				Assert.AreEqual(KingdomSealPrimaryState.Unknown,
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

				Assert.AreEqual(KingdomSealPrimaryState.Absent,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { missingSynced, local }, 64, 64, out failure), failure);

				File.WriteAllText(missingSynced, "not a Saves directory");
				Assert.AreEqual(KingdomSealPrimaryState.Unknown,
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
				Assert.AreEqual(KingdomSealPrimaryState.Unknown,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure));

				Directory.Delete(Path.Combine(local, "TARGET-GAME"));
				string game = Path.Combine(local, "target-game");
				Directory.CreateDirectory(game);
				File.WriteAllText(Path.Combine(game, "primary.sav.gz"), "case alias");
				Assert.AreEqual(KingdomSealPrimaryState.Unknown,
					KingdomSealEngineRules.ExactPrimaryAcrossRoots("target-game",
						new[] { synced, local }, 64, 64, out failure));
				File.Delete(Path.Combine(game, "primary.sav.gz"));
				File.WriteAllText(Path.Combine(game, "PRIMARY.SAV"), "legacy case alias");
				Assert.AreEqual(KingdomSealPrimaryState.Unknown,
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
				Assert.AreEqual(KingdomSealPrimaryState.Unknown,
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
				Assert.AreEqual(KingdomSealPrimaryState.Unknown,
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
			Assert.IsTrue(KingdomSealEngineRules.TryValidateAccessionTokens(0, "", "",
				out failure), failure);
			Assert.IsTrue(KingdomSealEngineRules.TryValidateAccessionTokens(1, first, "",
				out failure), failure);
			Assert.IsTrue(KingdomSealEngineRules.TryValidateAccessionTokens(2, first, second,
				out failure), failure);
			Assert.IsFalse(KingdomSealEngineRules.TryValidateAccessionTokens(2, first,
				"v1:2:200:not-base64", out failure));
			Assert.IsFalse(KingdomSealEngineRules.TryValidateAccessionTokens(2, second, second,
				out failure));
			Assert.IsFalse(KingdomSealEngineRules.TryValidateAccessionTokens(2, "", second,
				out failure));
			Assert.IsFalse(KingdomSealEngineRules.TryValidateAccessionTokens(1, "", "",
				out failure));
			Assert.IsTrue(KingdomSealEngineRules.AccessionTokenIsOrdinal(second, 2));
			Assert.IsFalse(KingdomSealEngineRules.AccessionTokenIsOrdinal(second, 1));
		}

		[Test]
		public void PollCadenceHandlesBoundaryAndClockRestoration()
		{
			Assert.IsTrue(KingdomSealEngineRules.PollDue(0L, 1L, 1200L));
			Assert.IsFalse(KingdomSealEngineRules.PollDue(100L, 1299L, 1200L));
			Assert.IsTrue(KingdomSealEngineRules.PollDue(100L, 1300L, 1200L));
			Assert.IsTrue(KingdomSealEngineRules.PollDue(2000L, 1000L, 1200L));
			Assert.IsTrue(KingdomSealEngineRules.PollDue(long.MaxValue - 10L,
				long.MaxValue, 10L));
		}

		[Test]
		public void RevisionAndGenerationNeverWrapOrLeaveSchemaBounds()
		{
			int next;
			Assert.IsTrue(KingdomSealEngineRules.TryNextRevision(int.MaxValue - 1, out next));
			Assert.AreEqual(int.MaxValue, next);
			Assert.IsFalse(KingdomSealEngineRules.TryNextRevision(int.MaxValue, out next));
			Assert.IsFalse(KingdomSealEngineRules.TryNextRevision(-1, out next));

			Assert.IsTrue(KingdomSealEngineRules.TryNextGeneration(1023, out next));
			Assert.AreEqual(1024, next);
			Assert.IsFalse(KingdomSealEngineRules.TryNextGeneration(1024, out next));
			Assert.IsFalse(KingdomSealEngineRules.TryNextGeneration(-1, out next));
		}

		[Test]
		public void SuccessfulAccessionIsExactAdjacentLivingGeneration()
		{
			KingdomSealRecord previous = Record();
			KingdomSealRecord successor = Record(legacy: "legacy-two", generation: 2, revision: 8);
			Assert.IsTrue(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));

			previous.Status = KingdomSealStatus.Retired;
			Assert.IsTrue(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));
			previous.Status = KingdomSealStatus.Terminal;
			Assert.IsFalse(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));

			previous.Status = KingdomSealStatus.Living;
			successor.Generation = 3;
			Assert.IsFalse(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));
			successor.Generation = 2;
			successor.Revision = 7;
			Assert.IsFalse(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));
			successor.Revision = 8;
			successor.LegacyId = previous.LegacyId;
			Assert.IsFalse(KingdomSealEngineRules.MayAdvanceGeneration(previous, successor));
		}

		[Test]
		public void LoadedPrimaryRestoresOnlyItsOwnOrNewerAbandonedAttempt()
		{
			KingdomSealRecord saved = Record(legacy: "legacy-one", generation: 1, revision: 7);
			KingdomSealRecord external = Record(legacy: "legacy-one", generation: 1, revision: 20);
			Assert.IsTrue(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));

			external = Record(legacy: "legacy-four", generation: 4, revision: 30);
			Assert.IsTrue(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external.Status = KingdomSealStatus.Terminal;
			Assert.IsTrue(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));

			external.Status = KingdomSealStatus.Retired;
			Assert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external = Record(legacy: "legacy-collision", generation: 1, revision: 20);
			Assert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external = Record(legacy: "legacy-old", generation: 0, revision: 30);
			Assert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external = Record(legacy: "legacy-four", generation: 4, revision: 7);
			Assert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
			external.Revision = 30;
			external.LineageId = "another";
			Assert.IsFalse(KingdomSealEngineRules.MayRestoreLoadedPrimary(external, saved));
		}

		[Test]
		public void SnapshotComparisonIgnoresJournalMechanicsButNotKingdomFacts()
		{
			KingdomSealRecord a = Record();
			KingdomSealRecord b = KingdomSealRules.Copy(a);
			b.Revision = 999;
			b.WrittenTick = 99999L;
			Assert.IsTrue(KingdomSealEngineRules.SameLivingSnapshot(a, b));

			b.Population++;
			b.Vigour = KingdomRules.SealedVigour((GrowthStage)b.Stage, b.Population,
				b.Defence, b.StoredWater, b.Withered);
			Assert.IsFalse(KingdomSealEngineRules.SameLivingSnapshot(a, b));
			b = KingdomSealRules.Copy(a);
			b.Status = KingdomSealStatus.Terminal;
			Assert.IsFalse(KingdomSealEngineRules.SameLivingSnapshot(a, b));
		}
	}
}
#endif
