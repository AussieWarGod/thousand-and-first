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
		private bool TryQuarantineExact(Zone Zone, out string Failure)
		{
			Failure = "";
			if (Zone == null || Zone.ZoneID != TargetZoneId)
			{
				Failure = "the exact target zone is unavailable";
				return false;
			}
			string expected = ApplicationMarker ?? "";
			if (string.IsNullOrEmpty(expected))
			{
				KingdomSealRecord legacy;
				KingdomSealReceipt receipt;
				if (!TryGetReservation(out legacy, out receipt)
					|| !KingdomInheritanceStateRules.TryComposeApplicationMarker(legacy, receipt,
						TargetZoneId, KingdomInheritEngine.ReconstructionVersion, out expected))
				{
					Failure = "the exact application marker could not be recomputed";
					return false;
				}
			}
			string zoneMarker = Zone.GetZoneProperty(KingdomInheritEngine.ZoneMarkerProperty, "") ?? "";
			if (!string.IsNullOrEmpty(zoneMarker) && zoneMarker != expected)
			{
				Failure = "the target carries a different inheritance marker";
				return false;
			}
			List<GameObject> objects = Zone.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				string marker = objects[i].GetStringProperty(
					KingdomInheritEngine.ObjectMarkerProperty, "") ?? "";
				if (!string.IsNullOrEmpty(marker) && marker != expected)
				{
					Failure = "the target carries a foreign marked object";
					return false;
				}
			}
			for (int i = objects.Count - 1; i >= 0; i--)
			{
				if (objects[i].GetStringProperty(KingdomInheritEngine.ObjectMarkerProperty, "")
					== expected)
				{
					objects[i].Obliterate(null, Silent: true);
				}
			}
			if (zoneMarker == expected)
			{
				Zone.RemoveZoneProperty(KingdomInheritEngine.ZoneMarkerProperty);
			}
			RemoveLocationFinders(Zone);
			objects = Zone.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				if (!string.IsNullOrEmpty(objects[i].GetStringProperty(
					KingdomInheritEngine.ObjectMarkerProperty, "")))
				{
					Failure = "a marked inherited object survived quarantine";
					return false;
				}
			}
			if (!string.IsNullOrEmpty(Zone.GetZoneProperty(
				KingdomInheritEngine.ZoneMarkerProperty, "") ?? ""))
			{
				Failure = "the inherited zone marker survived quarantine";
				return false;
			}
			return true;
		}

		private void RemoveLocationFinders(Zone Zone)
		{
			if (Zone == null || Zone.ZoneID != TargetZoneId || string.IsNullOrEmpty(SecretId))
			{
				return;
			}
			List<GameObject> objects = Zone.GetObjects();
			for (int i = objects.Count - 1; i >= 0; i--)
			{
				LocationFinder finder = objects[i].GetPart<LocationFinder>();
				if (finder != null && finder.ID == SecretId)
				{
					objects[i].Obliterate(null, Silent: true);
				}
			}
		}

		private void SetOwnedZoneName()
		{
			if (!OwnsZoneName || !HasCompatibleOwnedZoneNameSubset())
			{
				throw new InvalidDataException("the target zone-name subset is not inheritance-owned");
			}
			if (!The.Game.HasStringGameState("ZoneName_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneDisplayName(TargetZoneId, SiteName, Sync: false);
			}
			RequireCompatibleOwnedZoneNameSubset();
			if (!The.Game.HasStringGameState("ZoneNameContext_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneNameContext(TargetZoneId, "", Sync: false);
			}
			RequireCompatibleOwnedZoneNameSubset();
			if (!The.Game.HasBooleanGameState("ZoneProperName_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneHasProperName(TargetZoneId, true);
			}
			RequireCompatibleOwnedZoneNameSubset();
			if (!The.Game.HasStringGameState("ZoneIndefiniteArticle_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneIndefiniteArticle(TargetZoneId, "");
			}
			RequireCompatibleOwnedZoneNameSubset();
			if (!The.Game.HasStringGameState("ZoneDefiniteArticle_" + TargetZoneId))
			{
				The.ZoneManager.SetZoneDefiniteArticle(TargetZoneId, "");
			}
			RequireCompatibleOwnedZoneNameSubset();
			The.ZoneManager.SynchronizeZoneName(TargetZoneId);
			if (!HasExactOwnedZoneName())
			{
				throw new InvalidDataException("the inherited zone-name install did not complete exactly");
			}
		}

		private void RequireCompatibleOwnedZoneNameSubset()
		{
			if (!HasCompatibleOwnedZoneNameSubset())
			{
				throw new InvalidDataException(
					"the target zone-name subset changed during inheritance installation");
			}
		}

		private bool HasCompatibleOwnedZoneNameSubset()
		{
			if (The.Game == null || string.IsNullOrEmpty(TargetZoneId)
				|| string.IsNullOrEmpty(SiteName))
			{
				return false;
			}
			string nameKey = "ZoneName_" + TargetZoneId;
			string contextKey = "ZoneNameContext_" + TargetZoneId;
			string indefiniteKey = "ZoneIndefiniteArticle_" + TargetZoneId;
			string definiteKey = "ZoneDefiniteArticle_" + TargetZoneId;
			string properKey = "ZoneProperName_" + TargetZoneId;
			return KingdomInheritanceStateRules.IsCompatibleOwnedZoneNameSubset(
				The.Game.HasStringGameState(nameKey), The.Game.GetStringGameState(nameKey, null),
				The.Game.HasStringGameState(contextKey),
				The.Game.GetStringGameState(contextKey, null),
				The.Game.HasStringGameState(indefiniteKey),
				The.Game.GetStringGameState(indefiniteKey, null),
				The.Game.HasStringGameState(definiteKey),
				The.Game.GetStringGameState(definiteKey, null),
				The.Game.HasBooleanGameState(properKey),
				The.Game.GetBooleanGameState(properKey), SiteName);
		}

		private bool HasExactOwnedZoneName()
		{
			if (The.Game == null || string.IsNullOrEmpty(TargetZoneId))
			{
				return false;
			}
			string contextKey = "ZoneNameContext_" + TargetZoneId;
			string indefiniteKey = "ZoneIndefiniteArticle_" + TargetZoneId;
			string definiteKey = "ZoneDefiniteArticle_" + TargetZoneId;
			string properKey = "ZoneProperName_" + TargetZoneId;
			return KingdomInheritanceStateRules.IsExactZoneNameFootprint(
				The.Game.GetStringGameState("ZoneName_" + TargetZoneId, null),
				The.Game.HasStringGameState(contextKey),
				The.Game.GetStringGameState(contextKey, null),
				The.Game.HasStringGameState(indefiniteKey),
				The.Game.GetStringGameState(indefiniteKey, null),
				The.Game.HasStringGameState(definiteKey),
				The.Game.GetStringGameState(definiteKey, null),
				The.Game.HasBooleanGameState(properKey),
				The.Game.GetBooleanGameState(properKey), SiteName);
		}

		private bool HasAnyZoneNameFootprint()
		{
			return The.Game != null && !string.IsNullOrEmpty(TargetZoneId)
				&& (The.Game.HasStringGameState("ZoneName_" + TargetZoneId)
					|| The.Game.HasStringGameState("ZoneNameContext_" + TargetZoneId)
					|| The.Game.HasStringGameState("ZoneIndefiniteArticle_" + TargetZoneId)
					|| The.Game.HasStringGameState("ZoneDefiniteArticle_" + TargetZoneId)
					|| The.Game.HasBooleanGameState("ZoneProperName_" + TargetZoneId));
		}

		private bool TryRemoveOwnedZoneName(out string Failure)
		{
			Failure = "";
			if (!HasAnyZoneNameFootprint())
			{
				if (OwnsZoneName)
				{
					try
					{
						The.ZoneManager.SynchronizeZoneName(TargetZoneId);
					}
					catch (Exception ex)
					{
						// Set/remove callbacks can throw after their base-state write. With all five
						// keys still absent, exact reproof—not callback completion—is authoritative.
						try
						{
							LogFailure("the cleared inherited zone-name synchronization threw after "
								+ "exact absence proof: " + ex.Message);
						}
						catch (Exception)
						{
						}
					}
				}
				OwnsZoneName = false;
				return true;
			}
			if (!OwnsZoneName)
			{
				// A name that appeared before our provenance bit was set is foreign. Preserve it;
				// it does not prevent proving absence of inheritance-owned artifacts.
				return true;
			}
			if (!HasCompatibleOwnedZoneNameSubset())
			{
				Failure = "the target zone-name footprint changed after inheritance installed it";
				return false;
			}
			try
			{
				The.Game.RemoveStringGameState("ZoneName_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.Game.RemoveStringGameState("ZoneNameContext_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.Game.RemoveStringGameState("ZoneIndefiniteArticle_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.Game.RemoveStringGameState("ZoneDefiniteArticle_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.Game.RemoveBooleanGameState("ZoneProperName_" + TargetZoneId);
				RequireCompatibleOwnedZoneNameSubset();
				The.ZoneManager.SynchronizeZoneName(TargetZoneId);
			}
			catch (Exception ex)
			{
				if (KingdomInheritanceStateRules.CanClearZoneNameOwnership(
					HasAnyZoneNameFootprint()))
				{
					OwnsZoneName = false;
					return true;
				}
				Failure = "the exact inherited zone-name cleanup tore: " + ex.Message;
				return false;
			}
			if (HasAnyZoneNameFootprint())
			{
				Failure = "the exact inherited zone-name footprint survived cleanup";
				return false;
			}
			OwnsZoneName = false;
			return true;
		}

	}
}
