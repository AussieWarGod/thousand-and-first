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
		public void HandleEvent(EmbarkEvent E)
		{
			if (E == null)
			{
				return;
			}
			if (E.EventID == QudGameBootModule.BOOTEVENT_AFTERINITIALIZEWORLDS)
			{
				ValidateAfterWorlds();
			}
			else if (E.EventID == QudGameBootModule.BOOTEVENT_BOOTSTARTINGLOCATION)
			{
				ValidateStartAndInstall(E.Element as GlobalLocation);
			}
			else if (E.EventID == QudGameBootModule.BOOTEVENT_AFTERBOOTPLAYEROBJECT)
			{
				AnnounceFailure();
			}
		}

		public void Write(SerializationWriter Writer)
		{
			Writer.Write(SerializationMagic);
			Writer.Write(CurrentSerializationVersion);
			Writer.Write(PhaseValue);
			Writer.Write(LegacyText ?? "");
			Writer.Write(ReceiptText ?? "");
			Writer.Write(CommittedReceiptText ?? "");
			Writer.Write(TargetZoneId ?? "");
			Writer.Write(TargetTerrainBlueprint ?? "");
			Writer.Write(TargetTerrainRank);
			Writer.Write(SecretId ?? "");
			Writer.Write(SiteName ?? "");
			Writer.Write(FailureDetail ?? "");
			Writer.Write(ApplyStatusValue);
			Writer.Write(ApplyFaultValue);
			Writer.Write(ApplicationMarker ?? "");
			Writer.Write(FailureAnnounced);
			Writer.Write(ReleasePending);
			Writer.Write(OwnsSkipTerrainBuilders);
			Writer.Write(OwnsNoBiomes);
			Writer.Write(RecoveryDisabled);
			Writer.Write(RetryAuthorized);
			Writer.Write(OwnsZoneName);
		}

		public void Read(SerializationReader Reader)
		{
			try
			{
			int magic = Reader.ReadInt32();
			SerializationVersion = Reader.ReadInt32();
			if (!KingdomInheritanceStateRules.IsSupportedSerializationHeader(magic,
				SerializationVersion, SerializationMagic, CurrentSerializationVersion))
			{
				// DeserializeComposite can realign only when Read throws and its catch executes
				// SkipBlock. Returning after a future/invalid short schema would under-consume
				// this composite and corrupt every following read in the save block.
				DisableRecovery("the inherited target state had unsupported framing");
				throw new InvalidDataException("unsupported inheritance state magic or version");
			}
			PhaseValue = Reader.ReadInt32();
			LegacyText = Reader.ReadString() ?? "";
			ReceiptText = Reader.ReadString() ?? "";
			CommittedReceiptText = Reader.ReadString() ?? "";
			TargetZoneId = Reader.ReadString() ?? "";
			TargetTerrainBlueprint = Reader.ReadString() ?? "";
			TargetTerrainRank = Reader.ReadInt32();
			SecretId = Reader.ReadString() ?? "";
			SiteName = Reader.ReadString() ?? "";
			FailureDetail = Reader.ReadString() ?? "";
			ApplyStatusValue = Reader.ReadInt32();
			ApplyFaultValue = Reader.ReadInt32();
			ApplicationMarker = Reader.ReadString() ?? "";
			FailureAnnounced = Reader.ReadBoolean();
			ReleasePending = Reader.ReadBoolean();
			OwnsSkipTerrainBuilders = Reader.ReadBoolean();
			OwnsNoBiomes = Reader.ReadBoolean();
			RecoveryDisabled = SerializationVersion >= 2 && SerializationVersion <=
				CurrentSerializationVersion && Reader.ReadBoolean();
			RetryAuthorized = SerializationVersion >= 3
				&& SerializationVersion <= CurrentSerializationVersion
				&& Reader.ReadBoolean();
			OwnsZoneName = SerializationVersion >= 4
				&& SerializationVersion <= CurrentSerializationVersion
				&& Reader.ReadBoolean();
			if (SerializationVersion >= 1 && SerializationVersion < 4)
			{
				// Older versions predate explicit name-write provenance. Migrate only a complete exact
				// footprint; partial or mismatching old state remains authority-free.
				try
				{
					OwnsZoneName = HasExactOwnedZoneName();
				}
				catch (Exception)
				{
					OwnsZoneName = false;
				}
			}
			bool invalid = LegacyText.Length > KingdomSealFormat.MaxFileChars
				|| ReceiptText.Length > KingdomSealFormat.MaxFileChars
				|| CommittedReceiptText.Length > KingdomSealFormat.MaxFileChars
				|| TargetZoneId.Length > KingdomSealRecord.MaxIdChars
				|| TargetTerrainBlueprint.Length > KingdomSealRecord.MaxIdChars
				|| (TargetTerrainRank < -1
					|| TargetTerrainRank > KingdomInheritanceSiteRules.MaxTerrainRank)
				|| SecretId.Length > KingdomSealRecord.MaxIdChars + 32
				|| SiteName.Length > KingdomSealRecord.MaxNameChars + 32
				|| FailureDetail.Length > MaxFailureChars
				|| ApplicationMarker.Length > 1000
				|| !Enum.IsDefined(typeof(KingdomInheritancePhase), PhaseValue);
			string shapeFailure = "";
			if (!invalid)
			{
				KingdomInheritanceSavedShape shape = new KingdomInheritanceSavedShape
				{
					PhaseValue = PhaseValue,
					LegacyText = LegacyText,
					ReceiptText = ReceiptText,
					CommittedReceiptText = CommittedReceiptText,
					TargetZoneId = TargetZoneId,
					TargetTerrainBlueprint = TargetTerrainBlueprint,
					TargetTerrainRank = TargetTerrainRank,
					SecretId = SecretId,
					SiteName = SiteName,
					ApplyStatus = ApplyStatusValue,
					ApplyFault = ApplyFaultValue,
					ApplicationMarker = ApplicationMarker,
					ReleasePending = ReleasePending,
					OwnsSkipTerrainBuilders = OwnsSkipTerrainBuilders,
					OwnsNoBiomes = OwnsNoBiomes,
					OwnsZoneName = OwnsZoneName,
					RecoveryDisabled = RecoveryDisabled,
					RetryAuthorized = RetryAuthorized
				};
				invalid = !KingdomInheritanceStateRules.TryValidateSavedShape(shape,
					The.Game == null ? "" : The.Game.GameID,
					KingdomInheritEngine.ReconstructionVersion, out shapeFailure);
			}
			if (invalid)
			{
				DisableRecovery("the inherited target state could not be validated on load"
					+ (string.IsNullOrEmpty(shapeFailure) ? "" : ": " + shapeFailure));
			}
			}
			catch (Exception ex)
			{
				// DeserializeComposite may return this partially-read object after recording the
				// exception. Strip every identifier and ownership bit before it can escape.
				DisableRecovery("the inherited target state was truncated on load: " + ex.Message);
				throw;
			}
		}

		internal bool TrySelectionInputs(out string LegacyId, out string OldGroundZoneId,
			out string PreferredTerrainBlueprint)
		{
			LegacyId = "";
			OldGroundZoneId = "";
			PreferredTerrainBlueprint = "";
			KingdomSealRecord legacy;
			KingdomSealReceipt receipt;
			if (Phase != KingdomInheritancePhase.Reserved
				|| !TryGetReservation(out legacy, out receipt))
			{
				return false;
			}
			LegacyId = legacy.LegacyId;
			OldGroundZoneId = legacy.GroundZoneId ?? "";
			PreferredTerrainBlueprint = legacy.TerrainBlueprint ?? "";
			return true;
		}

	}
}
