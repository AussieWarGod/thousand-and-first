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
		private void TryCompletePendingSealAccession(string Context)
		{
			if (string.IsNullOrEmpty(PendingSealAccessionToken))
			{
				return;
			}
			if (!PendingSealAccessionReady && !TryPublishPendingAccessionRite(Context))
			{
				return;
			}
			string token = PendingSealAccessionToken;
			string failure;
			if (KingdomSeal.TryStartSuccessorGeneration(token, out failure))
			{
				PendingSealAccessionToken = "";
				PendingSealRiteChronicle = "";
				PendingSealAccessionReady = false;
				return;
			}
			KingdomLog.Log("succession: pending profile accession remains after " + Context + " ("
				+ (string.IsNullOrEmpty(failure) ? "unknown failure" : failure) + ")");
		}

		private bool TryPublishPendingAccessionRite(string Context)
		{
			if (PendingSealAccessionReady)
			{
				return true;
			}
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (system == null || !system.Founded || string.IsNullOrEmpty(PendingSealRiteChronicle))
			{
				KingdomLog.Log("succession: pending accession rite cannot publish during " + Context);
				return false;
			}
			try
			{
				string eventId = KingdomSuccessionRules.AccessionRiteEventId(
					PendingSealAccessionToken);
				if (string.IsNullOrEmpty(eventId) || !KingdomChronicle.RecordOnce(system, eventId,
					PendingSealRiteChronicle))
				{
					KingdomLog.Log("succession: pending accession Chronicle receipt remains after "
						+ Context);
					return false;
				}
				PendingSealRiteChronicle = "";
				PendingSealAccessionReady = true;
				return true;
			}
			catch (Exception ex)
			{
				MetricsManager.LogError("ThousandAndFirst: pending accession rite failed", ex);
				KingdomLog.Log("succession: pending accession rite remains after " + Context
					+ " (" + ex.GetType().Name + ")");
				return false;
			}
		}

		private static string BoundPendingRite(string Text)
		{
			string value = string.IsNullOrEmpty(Text)
				? "the charter passed to the successor after the founder's death." : Text;
			return value.Length <= MaxPendingRiteChronicleChars
				? value : value.Substring(0, MaxPendingRiteChronicleChars);
		}

		private void MigrateSavedState(int Version)
		{
			if (Version >= 2) return;
			LegacyPhysicalRiteUnavailable = true;
			ClearPendingRiteIdentity();
			CompletedShrineToken = "";
			CompletedShrineObjectId = "";
			CompletedShrineZoneId = "";
			if (!string.IsNullOrEmpty(PendingDeathToken)
				&& PendingAccessionRepairResidentId == 0)
			{
				// Version 1 could describe a clock jump but had no frozen body/locus/fixture
				// proof. It cannot be upgraded by inventing those facts. Quarantine only this
				// system so the enclosing save remains loadable.
				SuccessionDisabled = true;
				ClearDisabledSavedState();
			}
		}

	}
}
