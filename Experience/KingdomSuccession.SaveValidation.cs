using System;
using System.Collections.Generic;
using System.Reflection;
using Qud.API;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;
using XRL.World.Tinkering;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public sealed partial class KingdomSuccession
	{
		private void ValidateSavedState()
		{
			if (SuccessionDisabled)
			{
				ClearDisabledSavedState();
				return;
			}
			PendingDeathToken = PendingDeathToken ?? "";
			CompletedDeathToken = CompletedDeathToken ?? "";
			PendingSealAccessionToken = PendingSealAccessionToken ?? "";
			PendingFounderName = PendingFounderName ?? "";
			PendingFounderObjectId = PendingFounderObjectId ?? "";
			PendingFounderCause = PendingFounderCause ?? "";
			PendingHeirObjectId = PendingHeirObjectId ?? "";
			PendingHeirName = PendingHeirName ?? "";
			PendingHeirZoneId = PendingHeirZoneId ?? "";
			PendingRiteZoneId = PendingRiteZoneId ?? "";
			PendingRiteCityName = PendingRiteCityName ?? "";
			PendingRiteFixtureObjectId = PendingRiteFixtureObjectId ?? "";
			PendingRiteFixtureName = PendingRiteFixtureName ?? "";
			PendingRiteAttendeeManifest = PendingRiteAttendeeManifest ?? "";
			PendingShrineObjectId = PendingShrineObjectId ?? "";
			CompletedShrineToken = CompletedShrineToken ?? "";
			CompletedShrineObjectId = CompletedShrineObjectId ?? "";
			CompletedShrineZoneId = CompletedShrineZoneId ?? "";
			string stateFailure;
			if (!KingdomSuccessionRules.TryValidateSavedState(SuccessionOrdinal,
				PendingDeathToken, CompletedDeathToken, PendingPhase, PendingDueTick,
				PendingRoad, PendingDays, PendingAccessionRepairResidentId != 0,
				PendingSealAccessionToken, out stateFailure))
			{
				throw new InvalidOperationException("The saved succession state is invalid: "
					+ stateFailure + ".");
			}
			if (PendingSealAccessionToken != null
				&& PendingSealAccessionToken.Length > MaxSealAccessionTokenChars)
			{
				throw new InvalidOperationException("The saved profile-accession token is out of bounds.");
			}
			PendingSealRiteChronicle = PendingSealRiteChronicle ?? "";
			PendingAccessionRepairFounderName = PendingAccessionRepairFounderName ?? "";
			PendingAccessionRepairHeirName = PendingAccessionRepairHeirName ?? "";
			PendingAccessionRepairKeptCreeds = PendingAccessionRepairKeptCreeds ?? "";
			if (PendingAccessionRepairResidentId < 0
				|| PendingAccessionRepairFounderName.Length > KingdomSealRecord.MaxNameChars
				|| PendingAccessionRepairHeirName.Length > KingdomSealRecord.MaxNameChars
				|| PendingAccessionRepairKeptCreeds.Length > MaxPendingRepairCreedsChars
				|| PendingAccessionRepairArrivedTick < 0L
				|| (PendingAccessionRepairResidentId != 0
					&& (string.IsNullOrEmpty(PendingDeathToken)
						|| PendingDeathToken.Length > MaxSealAccessionTokenChars
						|| string.IsNullOrEmpty(PendingAccessionRepairHeirName))))
			{
				throw new InvalidOperationException("The saved accession repair identity is invalid.");
			}
			if (PendingAccessionRepairResidentId == 0)
			{
				PendingAccessionRepairFounderName = "";
				PendingAccessionRepairHeirName = "";
				PendingAccessionRepairSeated = false;
				PendingAccessionRepairArrivedTick = 0L;
				PendingAccessionRepairKeptCreeds = "";
			}
			if (PendingSealRiteChronicle.Length > MaxPendingRiteChronicleChars)
			{
				throw new InvalidOperationException("The saved accession rite chronicle is out of bounds.");
			}
			if (PendingSealAccessionToken.Length == 0)
			{
				PendingSealRiteChronicle = "";
				PendingSealAccessionReady = false;
			}
			else if (!PendingSealAccessionReady && PendingSealRiteChronicle.Length == 0)
			{
				// Compatibility with the first save shape: it only queued the token after the
				// rite chronicle had already been published.
				PendingSealAccessionReady = true;
			}
			else if (PendingSealAccessionReady)
			{
				PendingSealRiteChronicle = "";
			}

			bool hasPending = !string.IsNullOrEmpty(PendingDeathToken);
			if (!Enum.IsDefined(typeof(MourningRiteStage), PendingRiteStage))
			{
				throw new InvalidOperationException("The saved mourning-rite stage is invalid.");
			}
			if (!hasPending)
			{
				if (PendingRiteStage != MourningRiteStage.None)
				{
					throw new InvalidOperationException("An idle succession carries a mourning-rite stage.");
				}
				ClearPendingRiteIdentity();
			}
			else if (!LegacyPhysicalRiteUnavailable)
			{
				KingdomRiteAttendee[] attendees;
				if (PendingRiteStage < MourningRiteStage.Frozen
					|| PendingRiteStage > MourningRiteStage.BodyCrossed
					|| PendingHeirResidentId <= 0 || string.IsNullOrEmpty(PendingFounderName)
					|| string.IsNullOrEmpty(PendingFounderObjectId)
					|| string.IsNullOrEmpty(PendingFounderCause)
					|| string.IsNullOrEmpty(PendingHeirObjectId)
					|| string.IsNullOrEmpty(PendingHeirName)
					|| string.IsNullOrEmpty(PendingHeirZoneId)
					|| string.IsNullOrEmpty(PendingRiteZoneId)
					|| string.IsNullOrEmpty(PendingRiteCityName)
					|| string.IsNullOrEmpty(PendingRiteFixtureObjectId)
					|| string.IsNullOrEmpty(PendingRiteFixtureName)
					|| PendingFounderName.Length > KingdomSealRecord.MaxNameChars
					|| PendingHeirName.Length > KingdomSealRecord.MaxNameChars
					|| PendingRiteCityName.Length > KingdomSealRecord.MaxNameChars
					|| PendingRiteFixtureName.Length > KingdomSealRecord.MaxNameChars
					|| PendingFounderCause.Length > MaxPendingRiteChronicleChars
					|| PendingFounderObjectId.Length > 512
					|| PendingHeirObjectId.Length > 512
					|| PendingRiteFixtureObjectId.Length > 512
					|| PendingShrineObjectId.Length > 512
					|| PendingHeirZoneId.Length > 1024 || PendingRiteZoneId.Length > 1024
					|| PendingShrineX < 0 || PendingShrineX > 4096
					|| PendingShrineY < 0 || PendingShrineY > 4096
					|| !KingdomSuccessionRules.TryDecodeRiteManifest(
						PendingRiteAttendeeManifest, out attendees)
					|| attendees.Length == 0
					|| attendees[0].ResidentId != PendingHeirResidentId
					|| !string.Equals(attendees[0].ObjectId, PendingHeirObjectId,
						StringComparison.Ordinal)
					|| !string.Equals(attendees[0].ZoneId, PendingRiteZoneId,
						StringComparison.Ordinal)
					|| (PendingRiteStage >= MourningRiteStage.ShrinePlaced
						&& string.IsNullOrEmpty(PendingShrineObjectId))
					|| (PendingAccessionRepairResidentId != 0
						&& PendingRiteStage != MourningRiteStage.BodyCrossed))
				{
					throw new InvalidOperationException("The saved physical mourning-rite identity is invalid.");
				}
			}

			bool anyShrineReceipt = CompletedShrineToken.Length > 0
				|| CompletedShrineObjectId.Length > 0 || CompletedShrineZoneId.Length > 0;
			bool wholeShrineReceipt = CompletedShrineToken.Length > 0
				&& CompletedShrineObjectId.Length > 0 && CompletedShrineZoneId.Length > 0;
			if (anyShrineReceipt && !wholeShrineReceipt)
			{
				throw new InvalidOperationException("The in-run founder-shrine receipt is torn.");
			}
			int shrineOrdinal;
			long shrineTick;
			if (CompletedShrineToken.Length > 0
				&& (!KingdomSuccessionRules.TryReadDeathToken(CompletedShrineToken,
					out shrineOrdinal, out shrineTick)
					|| CompletedShrineObjectId.Length > 512
					|| CompletedShrineZoneId.Length > 1024))
			{
				throw new InvalidOperationException("The in-run founder-shrine receipt is invalid.");
			}
		}

		private void ClearDisabledSavedState()
		{
			int completedOrdinal;
			long completedTick;
			if (!KingdomSuccessionRules.TryReadDeathToken(CompletedDeathToken,
				out completedOrdinal, out completedTick))
			{
				CompletedDeathToken = "";
				SuccessionOrdinal = 0;
				PendingPhase = InterregnumPhase.None;
			}
			else
			{
				SuccessionOrdinal = completedOrdinal;
				PendingPhase = InterregnumPhase.Reigning;
			}
			PendingDeathToken = "";
			PendingDueTick = 0L;
			PendingRoad = NewsRoad.Seat;
			PendingDays = 0;
			PendingSealAccessionToken = "";
			PendingSealRiteChronicle = "";
			PendingSealAccessionReady = false;
			PendingAccessionRepairResidentId = 0;
			PendingAccessionRepairFounderName = "";
			PendingAccessionRepairHeirName = "";
			PendingAccessionRepairSeated = false;
			PendingAccessionRepairArrivedTick = 0L;
			PendingAccessionRepairKeptCreeds = "";
			ClearPendingRiteIdentity();
			int shrineOrdinal;
			long shrineTick;
			if (!KingdomSuccessionRules.TryReadDeathToken(CompletedShrineToken,
				out shrineOrdinal, out shrineTick)
				|| string.IsNullOrEmpty(CompletedShrineObjectId)
				|| string.IsNullOrEmpty(CompletedShrineZoneId))
			{
				CompletedShrineToken = "";
				CompletedShrineObjectId = "";
				CompletedShrineZoneId = "";
			}
		}

	}
}
