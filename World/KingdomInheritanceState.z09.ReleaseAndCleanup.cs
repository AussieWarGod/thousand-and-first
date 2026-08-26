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
		private void ReleaseReservation(string Detail, bool RestoreMutable = true)
		{
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			KingdomSealReservationLease lease;
			if (!TryGetReservation(out legacy, out receipt)
				|| (lease = GetReservationLease(receipt)) == null)
			{
				ReleasePending = true;
				SetRepair(Detail + "; the exact live reservation was unavailable for release");
				return;
			}
			string restoreFailure;
			if (RestoreMutable && !RestoreMutableReservation(out restoreFailure))
			{
				ReleasePending = true;
				SetRepair(Detail + "; the removed mutable site could not be restored: "
					+ restoreFailure);
				return;
			}
			KingdomSeal seal = The.Game == null ? null : The.Game.GetSystem<KingdomSeal>();
			string failure = "";
			if (seal != null && seal.TryReleaseImport(receipt, lease, out failure))
			{
				KingdomInheritanceLeaseOwner.Forget(lease);
				ReservationLease = null;
				ReleasePending = false;
				FailureDetail = Bound(Detail, MaxFailureChars);
				Transition(KingdomInheritancePhase.Refused);
				LogFailure(FailureDetail);
				return;
			}
			ReleasePending = true;
			SetRepair(Detail + "; the reservation could not be released: "
				+ Nonempty(failure, "the seal coordinator was unavailable"));
		}

		private void ReleaseExact(KingdomSeal Seal, KingdomSealReceipt Receipt,
			KingdomSealReservationLease Lease, string Detail)
		{
			string failure = "";
			HoldUnreleased(The.Game == null ? "" : The.Game.GameID, Receipt, Lease);
			if (Seal != null && Receipt != null && Lease != null
				&& Seal.TryReleaseImport(Receipt, Lease, out failure))
			{
				KingdomInheritanceLeaseOwner.Forget(Lease);
				ReservationLease = null;
				ReleasePending = false;
				FailureDetail = Bound(Detail, MaxFailureChars);
				PhaseValue = (int)KingdomInheritancePhase.Refused;
				LogFailure(FailureDetail);
				return;
			}
			ReleasePending = true;
			SetRepair(Detail + "; the exact reservation could not be released: "
				+ Nonempty(failure, "unknown release failure"));
		}

		private bool TryRemoveInstalledArtifacts(out string Failure)
		{
			Failure = "";
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			if (RecoveryDisabled || The.Game == null || The.ZoneManager == null
				|| string.IsNullOrEmpty(TargetZoneId)
				|| !TryGetReservation(out legacy, out receipt))
			{
				Failure = "cleanup lacked a trusted exact target reservation";
				return false;
			}
			try
			{
				The.ZoneManager.RemoveZoneBuilders(TargetZoneId, delegate(ZoneBuilderBlueprint builder)
				{
					if (builder == null)
					{
						return false;
					}
					return KingdomInheritanceStateRules.IsExactSiteBuilder(builder.Class,
						builder.GetParameter<string>("LegacyId", ""),
						builder.GetParameter<string>("TargetGameId", ""),
						builder.GetParameter<string>("TargetZoneId", ""),
						builder.GetParameter<int>("ReconstructionVersion", -1),
						legacy.LegacyId, receipt.TargetGameId, TargetZoneId,
						KingdomInheritEngine.ReconstructionVersion)
						|| KingdomInheritanceStateRules.IsExactLocationFinderBuilder(builder.Class,
							builder.GetParameter<string>("LegacyId", ""),
							builder.GetParameter<string>("TargetGameId", ""),
							builder.GetParameter<string>("TargetZoneId", ""),
							builder.GetParameter<int>("ReconstructionVersion", -1),
							legacy.LegacyId, receipt.TargetGameId, TargetZoneId,
							KingdomInheritEngine.ReconstructionVersion);
				});
			}
			catch (Exception ex)
			{
				Failure = AppendFailure(Failure, "builder removal threw: " + ex.Message);
			}
			try
			{
				JournalMapNote note = string.IsNullOrEmpty(SecretId)
					? null : JournalAPI.GetMapNote(SecretId);
				if (note != null && note.ZoneID == TargetZoneId)
				{
					JournalAPI.DeleteMapNote(note);
				}
			}
			catch (Exception ex)
			{
				Failure = AppendFailure(Failure, "map-note removal threw: " + ex.Message);
			}
			string nameFailure;
			if (!TryRemoveOwnedZoneName(out nameFailure))
			{
				Failure = AppendFailure(Failure, nameFailure);
			}
			try
			{
				object skip = The.ZoneManager.GetZoneProperty(TargetZoneId, "SkipTerrainBuilders");
				if (OwnsSkipTerrainBuilders && skip is bool && (bool)skip)
				{
					The.ZoneManager.RemoveZoneProperty(TargetZoneId, "SkipTerrainBuilders");
				}
				if (!The.ZoneManager.HasZoneProperty(TargetZoneId, "SkipTerrainBuilders"))
				{
					OwnsSkipTerrainBuilders = false;
				}
				if (OwnsNoBiomes
					&& (The.ZoneManager.GetZoneProperty(TargetZoneId, "NoBiomes") as string) == "Yes")
				{
					The.ZoneManager.RemoveZoneProperty(TargetZoneId, "NoBiomes");
				}
				if (!The.ZoneManager.HasZoneProperty(TargetZoneId, "NoBiomes"))
				{
					OwnsNoBiomes = false;
				}
			}
			catch (Exception ex)
			{
				Failure = AppendFailure(Failure, "zone-property removal threw: " + ex.Message);
			}
			string proofFailure;
			if (!TryProveInstalledArtifactsAbsent(legacy, receipt, out proofFailure))
			{
				Failure = AppendFailure(Failure, proofFailure);
				return false;
			}
			return string.IsNullOrEmpty(Failure);
		}

		private bool TryProveInstalledArtifactsAbsent(KingdomSealRecord Legacy,
			KingdomSealReceipt Receipt, out string Failure)
		{
			Failure = "";
			try
			{
				ZoneBuilderCollection collection = The.ZoneManager.GetBuilderCollection(TargetZoneId);
				if (collection != null && collection.Members != null)
				{
					for (int i = 0; i < collection.Members.Count; i++)
					{
						ZoneBuilderBlueprint builder = collection.Members[i].Blueprint;
						if (builder != null && (KingdomInheritanceStateRules.IsExactSiteBuilder(
							builder.Class, builder.GetParameter<string>("LegacyId", ""),
							builder.GetParameter<string>("TargetGameId", ""),
							builder.GetParameter<string>("TargetZoneId", ""),
							builder.GetParameter<int>("ReconstructionVersion", -1),
							Legacy.LegacyId, Receipt.TargetGameId, TargetZoneId,
							KingdomInheritEngine.ReconstructionVersion)
							|| KingdomInheritanceStateRules.IsExactLocationFinderBuilder(
								builder.Class, builder.GetParameter<string>("LegacyId", ""),
								builder.GetParameter<string>("TargetGameId", ""),
								builder.GetParameter<string>("TargetZoneId", ""),
								builder.GetParameter<int>("ReconstructionVersion", -1),
								Legacy.LegacyId, Receipt.TargetGameId, TargetZoneId,
								KingdomInheritEngine.ReconstructionVersion)))
						{
							Failure = "an exact inherited persistent builder survived cleanup";
							return false;
						}
					}
				}
				JournalMapNote note = string.IsNullOrEmpty(SecretId)
					? null : JournalAPI.GetMapNote(SecretId);
				if (note != null)
				{
					Failure = note.ZoneID == TargetZoneId
						? "the exact inherited map note survived cleanup"
						: "the inherited secret id now belongs to a foreign map note";
					return false;
				}
				if (OwnsZoneName)
				{
					Failure = "owned zone-name cleanup authority survived artifact cleanup";
					return false;
				}
				if (The.ZoneManager.HasZoneProperty(TargetZoneId, "SkipTerrainBuilders")
					|| The.ZoneManager.HasZoneProperty(TargetZoneId, "NoBiomes")
					|| OwnsSkipTerrainBuilders || OwnsNoBiomes)
				{
					Failure = "a reserved generation property or ownership bit survived cleanup";
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Failure = "artifact-absence reproof threw: " + ex.Message;
				return false;
			}
		}

	}
}
