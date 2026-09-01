using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		private static bool TryObserveGrant(Zone Zone, KingdomQuickstartReceipt Receipt,
			KingdomQuickstartPhase Target, int X, int Y, bool EmptyRequired,
			out GameObject Existing, out KingdomQuickstartGrantObservation Observation,
			out string Failure)
		{
			Existing = null;
			Observation = KingdomQuickstartGrantObservation.ForeignOrMalformed;
			Failure = "";
			string expected = KingdomQuickstartRules.GrantMarker(Receipt, Target);
			Cell role = RoleCell(Zone, X, Y);
			if (Zone == null || role == null || string.IsNullOrEmpty(expected))
			{
				Failure = "The quickstart grant reservation was malformed.";
				return false;
			}

			List<GameObject> objects = Zone.GetObjects();
			int matches = 0;
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item)) continue;
				if (item.HasIntProperty(KingdomQuickstartRules.GrantMarkerProperty))
				{
					Failure = "A quickstart grant carried a marker of the wrong type.";
					return false;
				}
				if (!item.HasStringProperty(KingdomQuickstartRules.GrantMarkerProperty)) continue;
				string marker = item.GetStringProperty(
					KingdomQuickstartRules.GrantMarkerProperty, "");
				if (!KnownGrantMarker(Receipt, marker))
				{
					Failure = "A quickstart grant carried a foreign or malformed reservation.";
					return false;
				}
				if (item.CurrentCell == role && !string.Equals(marker, expected,
					StringComparison.Ordinal))
				{
					Failure = "The reserved quickstart role cell held a foreign grant marker.";
					return false;
				}
				if (!string.Equals(marker, expected, StringComparison.Ordinal)) continue;
				matches++;
				Existing = item;
			}
			if (matches > 1 || (matches == 1 && (Existing.CurrentZone != Zone
				|| Existing.CurrentCell != role || string.IsNullOrEmpty(Existing.IDIfAssigned))))
			{
				Failure = "The quickstart grant reservation was duplicated, moved, or lost identity.";
				return false;
			}
			if (matches == 1)
			{
				Observation = KingdomQuickstartGrantObservation.ExactPlaced;
				return true;
			}
			if (EmptyRequired && !EmptyRoleCell(role))
			{
				Failure = "The reserved quickstart role cell acquired a foreign obstruction.";
				return false;
			}
			Observation = KingdomQuickstartGrantObservation.Absent;
			return true;
		}

		private static bool TryPrepareGrant(GameObject Grant,
			KingdomQuickstartReceipt Receipt, KingdomQuickstartPhase Target,
			out string Failure)
		{
			Failure = "";
			string marker = KingdomQuickstartRules.GrantMarker(Receipt, Target);
			if (!GameObject.Validate(Grant) || string.IsNullOrEmpty(marker)
				|| Grant.CurrentCell != null
				|| Grant.HasIntProperty(KingdomQuickstartRules.GrantMarkerProperty)
				|| Grant.HasStringProperty(KingdomQuickstartRules.GrantMarkerProperty))
			{
				Failure = "A fresh quickstart grant was not private and unmarked.";
				return false;
			}
			Grant.SetStringProperty(KingdomQuickstartRules.GrantMarkerProperty, marker);
			string identity = Grant.RequireID();
			if (string.IsNullOrEmpty(identity)
				|| !string.Equals(Grant.GetStringProperty(
					KingdomQuickstartRules.GrantMarkerProperty, ""), marker,
					StringComparison.Ordinal))
			{
				Failure = "A fresh quickstart grant did not retain its reservation and identity.";
				return false;
			}
			return true;
		}

		private static bool TryPlaceGrant(Zone Zone, GameObject Grant, int X, int Y,
			out string Failure)
		{
			Failure = "";
			Cell cell = RoleCell(Zone, X, Y);
			if (!GameObject.Validate(Grant) || Grant.CurrentCell != null
				|| !EmptyRoleCell(cell))
			{
				Failure = "The prepared quickstart grant had no exact empty role cell.";
				return false;
			}
			GameObject accepted = null;
			try { accepted = cell.AddObject(Grant, NoStack: true, Silent: true); }
			catch
			{
				// A callback can throw after completing placement. Physical custody decides.
			}
			if (!ReferenceEquals(accepted, Grant) && Grant.CurrentCell != cell)
			{
				Failure = "The prepared quickstart grant did not land in its role cell.";
				return false;
			}
			if (Grant.CurrentZone != Zone || Grant.CurrentCell != cell)
			{
				Failure = "The quickstart placement callback left ambiguous custody.";
				return false;
			}
			return true;
		}

		private static bool ExactGrantMarker(GameObject Grant,
			KingdomQuickstartReceipt Receipt, KingdomQuickstartPhase Target)
		{
			string marker = KingdomQuickstartRules.GrantMarker(Receipt, Target);
			if (!GameObject.Validate(Grant) || Grant.CurrentZone == null
				|| string.IsNullOrEmpty(marker)
				|| !Grant.HasStringProperty(KingdomQuickstartRules.GrantMarkerProperty)
				|| Grant.HasIntProperty(KingdomQuickstartRules.GrantMarkerProperty)
				|| !string.Equals(Grant.GetStringProperty(
					KingdomQuickstartRules.GrantMarkerProperty, ""), marker,
					StringComparison.Ordinal)) return false;
			int matches = 0;
			List<GameObject> objects = Grant.CurrentZone.GetObjects();
			for (int i = 0; i < objects.Count; i++)
				if (GameObject.Validate(objects[i])
					&& string.Equals(objects[i].GetStringProperty(
						KingdomQuickstartRules.GrantMarkerProperty, ""), marker,
						StringComparison.Ordinal)) matches++;
			return matches == 1;
		}

		private static bool KnownGrantMarker(KingdomQuickstartReceipt Receipt, string Marker)
		{
			if (string.IsNullOrEmpty(Marker)) return false;
			for (int phase = (int)KingdomQuickstartPhase.WaterStocked;
				phase <= (int)KingdomQuickstartPhase.AdvisorResolved; phase++)
				if (string.Equals(Marker, KingdomQuickstartRules.GrantMarker(Receipt,
					(KingdomQuickstartPhase)phase), StringComparison.Ordinal)) return true;
			return false;
		}

		private static bool ReceiptOwns(GameObject Grant, string ExpectedId)
		{
			return GameObject.Validate(Grant) && !string.IsNullOrEmpty(ExpectedId)
				&& string.Equals(Grant.IDIfAssigned, ExpectedId, StringComparison.Ordinal);
		}
	}
}
