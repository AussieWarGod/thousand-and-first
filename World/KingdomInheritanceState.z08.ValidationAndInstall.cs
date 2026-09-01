using System;
using System.Collections.Generic;
using System.IO;
using Genkit;
using Qud.API;
using XRL;
using XRL.CharacterBuilds.Qud;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using XRL.World.WorldBuilders;

namespace ThousandAndFirst
{
	public sealed partial class KingdomInheritanceState
	{
		private bool TryProveDirectRepairPrecondition(Zone Zone, KingdomSealRecord Legacy,
			KingdomSealReceipt Reserved, out string Failure,
			bool RequireRetryAuthorization = true)
		{
			Failure = "";
			if ((RequireRetryAuthorization && !RetryAuthorized)
				|| Zone == null || Zone.ZoneID != TargetZoneId
				|| Legacy == null || Reserved == null
				|| !HasOnlyOwnedBuilders(TargetZoneId, Legacy.LegacyId, Reserved.TargetGameId,
					KingdomInheritEngine.ReconstructionVersionFor(Legacy), out Failure)
				|| The.ZoneManager.CountPartsFor(TargetZoneId) != 0
				|| Zone.GetObjects().Count != 0)
			{
				Failure = Nonempty(Failure,
					"the loaded target was not still exact-owned, part-free, and object-free");
				return false;
			}
			string tile;
			string color;
			string render;
			if (!TryGroundPaint(TargetZoneId, out tile, out color, out render, out Failure))
			{
				return false;
			}
			for (int y = 0; y < Zone.Height; y++)
			{
				for (int x = 0; x < Zone.Width; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || cell.PaintTile != tile || cell.PaintTileColor != color
						|| cell.PaintColorString != color || cell.PaintRenderString != render)
					{
						Failure = "the loaded direct-repair ground was changed or not inheritance-painted";
						return false;
					}
				}
			}
			return true;
		}

		private void ValidateAfterWorlds()
		{
			try
			{
				if (Phase == KingdomInheritancePhase.Reserved)
				{
					ReleaseReservation("the Joppa world extension did not reserve a compatible site");
					return;
				}
				if (Phase != KingdomInheritancePhase.SiteSelected)
				{
					return;
				}
				string failure;
				if (!KingdomInheritanceWorldRuntime.ValidateSelected(TargetZoneId,
					TargetTerrainBlueprint, TargetX, TargetY, ReservedMap, ReservedWorldInfo,
					requireRemovedMap: true, out failure))
				{
					ReleaseReservation("post-world validation refused the inherited site: " + failure);
					return;
				}
				Transition(KingdomInheritancePhase.WorldValidated);
			}
			catch (Exception ex)
			{
				ReleaseReservation("post-world validation failed: " + ex.Message);
			}
		}

		private void ValidateStartAndInstall(GlobalLocation Start)
		{
			if (Phase != KingdomInheritancePhase.WorldValidated)
			{
				return;
			}
			try
			{
				KingdomInheritanceStartFault startFault = KingdomInheritanceStateRules.ValidateStart(
					TargetZoneId, Start == null || Start.IsClear() ? "" : Start.World,
					Start == null || Start.IsClear() ? "" : Start.ZoneID);
				if (startFault != KingdomInheritanceStartFault.None)
				{
					ReleaseReservation("the inherited site is incompatible with this start: "
						+ startFault.ToString());
					return;
				}
				string failure;
				if (!KingdomInheritanceWorldRuntime.ValidateSelected(TargetZoneId,
					TargetTerrainBlueprint, TargetX, TargetY, ReservedMap, ReservedWorldInfo,
					requireRemovedMap: true, out failure))
				{
					ReleaseReservation("final world validation refused the inherited site: " + failure);
					return;
				}
				InstallArtifacts();
			}
			catch (Exception ex)
			{
				string cleanupFailure;
				if (TryRemoveInstalledArtifacts(out cleanupFailure))
				{
					ReleaseReservation("the inherited site's discoverability could not be installed: "
						+ ex.Message);
				}
				else
				{
					ReleasePending = true;
					SetRepair("artifact installation failed and exact cleanup was unresolved: "
						+ ex.Message + "; " + cleanupFailure);
				}
			}
		}

		private void InstallArtifacts()
		{
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			if (!TryGetReservation(out legacy, out receipt))
			{
				throw new InvalidDataException("the target no longer carries its exact reservation");
			}
			int reconstruction = KingdomInheritEngine.ReconstructionVersionFor(legacy);
			if (reconstruction <= 0)
				throw new InvalidDataException("the external seal's spatial shape is unsupported");
			SecretId = "taf.inherit." + legacy.LegacyId;
			if (JournalAPI.GetMapNote(SecretId) != null)
			{
				throw new InvalidDataException("the inherited site's secret id is already in use");
			}
			SiteName = KingdomInheritanceStateRules.ComposeSiteName(legacy);
			if (HasAnyZoneNameFootprint())
			{
				throw new InvalidDataException("the target already has an explicit zone-name footprint");
			}
			if (The.ZoneManager.HasZoneProperty(TargetZoneId, "SkipTerrainBuilders")
				|| The.ZoneManager.HasZoneProperty(TargetZoneId, "NoBiomes"))
			{
				throw new InvalidDataException(
					"the target's reserved generation property is already owned");
			}
			The.ZoneManager.SetZoneProperty(TargetZoneId, "SkipTerrainBuilders", true);
			OwnsSkipTerrainBuilders = true;
			The.ZoneManager.SetZoneProperty(TargetZoneId, "NoBiomes", "Yes");
			OwnsNoBiomes = true;
			The.ZoneManager.AddZoneBuilder(TargetZoneId, 6000, BuilderClass,
				"LegacyId", legacy.LegacyId,
				"TargetGameId", receipt.TargetGameId,
				"TargetZoneId", TargetZoneId,
				"ReconstructionVersion", reconstruction);
			// ZoneBuilderCollection copies its member count before running. A custom finder must
			// therefore no-op unless the preceding builder published exact success; removing a generic
			// AddLocationFinder from persistence cannot suppress that same-attempt local copy.
			The.ZoneManager.AddZoneBuilder(TargetZoneId, 6100,
				"KingdomInheritanceLocationFinderBuilder",
				"LegacyId", legacy.LegacyId,
				"TargetGameId", receipt.TargetGameId,
				"TargetZoneId", TargetZoneId,
				"ReconstructionVersion", reconstruction);
			JournalAPI.AddMapNote(TargetZoneId, ComposeMapNote(legacy), Category(legacy),
				new string[4] { "settlement", "historic", "taf", "inheritance" },
				SecretId, revealed: true, sold: false, 0L, silent: true);
			OwnsZoneName = true;
			SetOwnedZoneName();
			Transition(KingdomInheritancePhase.Installed);
		}

	}
}
