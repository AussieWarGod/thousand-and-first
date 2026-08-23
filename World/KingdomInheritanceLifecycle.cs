using System;
using System.IO;
using HarmonyLib;
using XRL;
using XRL.Core;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Save/load and completed-zone hooks absent from IGameStateSingleton. Persisted with target
	/// save; it reacquires reservations on load and cleans a failed build before activation.
	/// </summary>
	[Serializable]
	public sealed class KingdomInheritanceLifecycle : IPlayerSystem
	{
		public override void Register(XRLGame Game, IEventRegistrar Registrar)
		{
			Registrar.Register(AfterGameLoadedEvent.ID);
			Registrar.Register(ZoneBuiltEvent.ID);
		}

		public override void AfterLoad(XRLGame Game)
		{
			base.AfterLoad(Game);
			KingdomInheritanceLeaseOwner.BeginGame(Game == null ? "" : Game.GameID);
		}

		public override bool HandleEvent(AfterGameLoadedEvent E)
		{
			string sourceFailure;
			KingdomInheritanceLoadKind loadKind = KingdomInheritancePrimaryLoad.TryConsume(
				The.Game, out sourceFailure);
			KingdomInheritanceState.Instance?.ResumeAfterLoad(loadKind, sourceFailure);
			return base.HandleEvent(E);
		}

		public override bool HandleEvent(ZoneBuiltEvent E)
		{
			KingdomInheritanceState.Instance?.HandleTargetZoneBuilt(E?.Zone);
			return base.HandleEvent(E);
		}
	}

	/// <summary>Prefix-only: LoadGame is async, and its AfterGameLoaded event occurs after awaits.
	/// AsyncLocal keeps overlapping coda or save-menu loads isolated until that event consumes it.</summary>
	[HarmonyPatch(typeof(XRLGame), "LoadGame")]
	internal static class KingdomInheritanceLoadGamePatch
	{
		private static void Prefix(string Path)
		{
			KingdomInheritanceLoadSourceFlow.Record(Path);
		}
	}

	internal static class KingdomInheritancePrimaryLoad
	{
		internal static KingdomInheritanceLoadKind TryConsume(XRLGame Game, out string Failure)
		{
			Failure = "";
			string source;
			if (!KingdomInheritanceLoadSourceFlow.TryConsume(out source))
			{
				Failure = "the successful load had no captured XRLGame.LoadGame source";
				return KingdomInheritanceLoadKind.Unknown;
			}
			if (Game == null || !KingdomSealReceipt.ValidId(Game.GameID))
			{
				Failure = "the successful load had no canonical target game identity";
				return KingdomInheritanceLoadKind.Unknown;
			}
			try
			{
				string stemName = Path.GetFileName(source);
				if (stemName != "Primary" && stemName != "Quick"
					&& stemName != "Checkpoint" && stemName != "Precognition")
				{
					Failure = "the successful load was not an exact supported target-save stem";
					return KingdomInheritanceLoadKind.Unknown;
				}
				string[] roots = new string[2]
				{
					DataManager.SyncedPath("Saves"),
					DataManager.SavePath("Saves")
				};
				string seenRoot = "";
				StringComparison rootComparison = Path.DirectorySeparatorChar == '\\'
					? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
				string lastFailure = "the source was outside both canonical save roots";
				for (int i = 0; i < roots.Length; i++)
				{
					string savesRoot = System.IO.Path.GetFullPath(roots[i])
						.TrimEnd(System.IO.Path.DirectorySeparatorChar,
							System.IO.Path.AltDirectorySeparatorChar);
					if (seenRoot.Length > 0 && string.Equals(seenRoot, savesRoot,
						rootComparison))
					{
						continue;
					}
					seenRoot = savesRoot;
					string gameDirectory = System.IO.Path.Combine(savesRoot, Game.GameID);
					string selectedGzip = System.IO.Path.Combine(gameDirectory,
						stemName + ".sav.gz");
					string selectedLegacy = System.IO.Path.Combine(gameDirectory,
						stemName + ".sav");
					if (!Directory.Exists(savesRoot) || !Directory.Exists(gameDirectory)
						|| (!File.Exists(selectedGzip) && !Directory.Exists(selectedGzip)
							&& !File.Exists(selectedLegacy) && !Directory.Exists(selectedLegacy)))
					{
						lastFailure = "a canonical root lacked the exact captured save source";
						continue;
					}
					DirectoryInfo rootInfo = new DirectoryInfo(savesRoot);
					DirectoryInfo gameInfo = new DirectoryInfo(gameDirectory);
					bool gzipExists = File.Exists(selectedGzip) || Directory.Exists(selectedGzip);
					bool legacyExists = File.Exists(selectedLegacy) || Directory.Exists(selectedLegacy);
					FileAttributes gzipAttributes = gzipExists
						? File.GetAttributes(selectedGzip) : (FileAttributes)0;
					FileAttributes legacyAttributes = legacyExists
						? File.GetAttributes(selectedLegacy) : (FileAttributes)0;
					long gzipLength = File.Exists(selectedGzip)
						? new FileInfo(selectedGzip).Length : 0L;
					long legacyLength = File.Exists(selectedLegacy)
						? new FileInfo(selectedLegacy).Length : 0L;
					KingdomInheritanceLoadKind kind =
						KingdomInheritanceStateRules.ClassifyExactLoadSource(source, savesRoot,
						Game.GameID, rootInfo.Attributes, gameInfo.Attributes, gzipExists,
						gzipAttributes, gzipLength, legacyExists, legacyAttributes, legacyLength,
						out lastFailure);
					if (kind != KingdomInheritanceLoadKind.Unknown)
					{
						return kind;
					}
				}
				Failure = lastFailure;
				return KingdomInheritanceLoadKind.Unknown;
			}
			catch (Exception ex)
			{
				Failure = "the successful load source could not prove a direct target-save file: "
					+ ex.Message;
				return KingdomInheritanceLoadKind.Unknown;
			}
		}
	}

	/// <summary>
	/// One process can own one active target game. Strong owner prevents GC from silently dropping
	/// an unsaved reservation; a new GameID deterministically abandons and unlocks the old game.
	/// </summary>
	internal static class KingdomInheritanceLeaseOwner
	{
		private static readonly object Sync = new object();

		private static string TargetGameId = "";

		private static KingdomSealReservationLease Lease;

		internal static void BeginGame(string GameId)
		{
			lock (Sync)
			{
				string next = GameId ?? "";
				if (Lease != null && TargetGameId != next)
				{
					Lease.Dispose();
					Lease = null;
				}
				if (Lease == null)
				{
					TargetGameId = next;
				}
			}
		}

		internal static KingdomSealReservationLease Hold(string GameId,
			KingdomSealReceipt Receipt, KingdomSealReservationLease Candidate)
		{
			if (Candidate == null || !Candidate.IsHeld || Receipt == null
				|| !Candidate.Matches(Receipt) || Receipt.TargetGameId != (GameId ?? ""))
			{
				throw new InvalidOperationException("the inheritance lease did not match its exact receipt");
			}
			lock (Sync)
			{
				if (Lease != null && Lease.IsHeld)
				{
					if (TargetGameId == GameId && Lease.Matches(Receipt))
					{
						if (!ReferenceEquals(Lease, Candidate))
						{
							Candidate.Dispose();
						}
						return Lease;
					}
					throw new InvalidOperationException("another live inheritance reservation is already owned");
				}
				Lease = Candidate;
				TargetGameId = GameId ?? "";
				return Lease;
			}
		}

		internal static KingdomSealReservationLease HoldUnknown(string GameId,
			KingdomSealReservationLease Candidate)
		{
			if (Candidate == null || !Candidate.IsHeld)
			{
				throw new InvalidOperationException("the inheritance lease is not live");
			}
			lock (Sync)
			{
				if (Lease != null && Lease.IsHeld && !ReferenceEquals(Lease, Candidate))
				{
					throw new InvalidOperationException(
						"another live inheritance reservation is already owned");
				}
				Lease = Candidate;
				TargetGameId = GameId ?? "";
				return Lease;
			}
		}

		internal static KingdomSealReservationLease Get(string GameId,
			KingdomSealReceipt Receipt)
		{
			lock (Sync)
			{
				return Lease != null && Lease.IsHeld && TargetGameId == (GameId ?? "")
					&& Receipt != null && Lease.Matches(Receipt) ? Lease : null;
			}
		}

		internal static void Forget(KingdomSealReservationLease Exact)
		{
			lock (Sync)
			{
				if (ReferenceEquals(Lease, Exact))
				{
					Lease = null;
					TargetGameId = "";
				}
			}
		}

		internal static void Finish(string GameId, KingdomSealReceipt Receipt)
		{
			lock (Sync)
			{
				if (Lease != null && TargetGameId == (GameId ?? "")
					&& Receipt != null && Lease.Matches(Receipt))
				{
					Lease.Dispose();
					Lease = null;
					TargetGameId = "";
				}
			}
		}
	}
}
