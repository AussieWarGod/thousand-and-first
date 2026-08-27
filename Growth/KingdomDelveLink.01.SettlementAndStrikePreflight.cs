using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomDelveLink
	{

		/// <summary>
		/// Post-stamp/cold-load settlement. Reads only frozen owner authority; no current architecture
		/// catalogue, KingdomData entry, or selection context participates after debit.
		/// </summary>
		public static bool TrySettle(GameObject Owner, Zone Head, out string Failure)
		{
			Failure = null;
			if (Owner == null || Head == null) return Fail("delve link settlement has no exact root or head",
				out Failure);
			KingdomArchitectureIntent architecture;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (!KingdomArchitectureStamper.TryReadOwner(Owner, out architecture, out snapshot,
				out lot, out Failure)) return false;
			if (!KingdomDelveRules.IsDelve(architecture.BuildKey)) return true;
			if (Owner.GetIntProperty(KingdomArchitectureStamper.NextLayerProperty) != 3)
				return Fail("delve link cannot settle before every authored layer is complete", out Failure);
			Derived derived;
			if (!TryDerive(architecture, Head, Owner.ID, lot, out derived, out Failure)) return false;
			Zone foot;
			if (!TryLoadBuiltFoot(Head, derived, out foot, out Failure)) return false;
			GameObject headEndpoint;
			if (!TryFindHeadEndpoint(Head, derived, out headEndpoint, out Failure)) return false;

			if (!Owner.HasIntProperty(SchemaProperty))
			{
				if (HasAnyRootField(Owner))
					return Quarantine(Owner, "delve link has partial fields without its commit schema",
						out Failure);
				if (!TrySafeFoot(null, foot, derived, null, out Failure)
					|| !ConnectionCellAllows(derived.FootZoneId, derived.X, derived.Y,
						"StairsUp", UpBlueprint, true)
					|| !EmptyConnectionCell(derived.HeadZoneId, derived.X, derived.Y))
					return Failure != null ? false : Fail(
						"the lower landing changed before physical pairing", out Failure);
				if (!TryInitializeRoot(Owner, derived, out Failure)) return false;
			}
			if (!TryReadRoot(Owner, derived, out Failure)) return false;
			int phase = Owner.GetIntProperty(PhaseProperty);
			if (phase == 0)
			{
				StampEndpoint(headEndpoint, derived, HeadRole);
				KingdomSurvey.ObserveChangedInActive(Head, headEndpoint);
				if (!ExactEndpoint(headEndpoint, Head, derived, HeadRole, out Failure))
					return Quarantine(Owner, Failure ?? "delve Down endpoint receipt did not settle",
						out Failure);
				string rooted = Owner.GetStringProperty(HeadEndpointProperty);
				if (!string.IsNullOrEmpty(rooted) && rooted != headEndpoint.ID)
					return Quarantine(Owner, "delve head endpoint changed across an interrupted phase",
						out Failure);
				Owner.SetStringProperty(HeadEndpointProperty, headEndpoint.ID);
				Owner.SetIntProperty(PhaseProperty, 1);
				phase = 1;
			}
			if (phase == 1)
			{
				if (!TrySettleFootEndpoint(Owner, foot, derived, out Failure)) return false;
				phase = 2;
			}
			if (phase == 2)
			{
				if (!TrySettleConnections(Head, foot, derived, out Failure)) return false;
				if (!TryPublish(Owner, Head, foot, derived, out Failure)) return false;
				phase = 3;
			}
			if (phase != 3 || !TryProveActive(Owner, Head, foot, derived, out Failure)) return false;
			return true;
		}

		/// <summary>No-spend strike audit. Both exact endpoints and both connections must stand.</summary>
		public static bool TryPreflightStrike(GameObject Owner, Zone Head, out string Failure)
		{
			Failure = null;
			bool managed;
			if (!TryManagedStrikeLane(Owner, Head, out managed, out Failure)) return false;
			if (!managed) return true;
			Derived derived;
			Zone foot;
			if (!TryStrikeBase(Owner, Head, out derived, out foot, out Failure)) return false;
			if (derived == null) return true;
			if (Owner.GetIntProperty(StrikePhaseProperty) != 0)
				return Fail("delve strike receipt is already in flight or malformed", out Failure);
			GameObject headEndpoint;
			GameObject footEndpoint;
			if (!TryExactStoredEndpoint(Owner, Head, derived, HeadRole, out headEndpoint, out Failure)
				|| !TryExactStoredEndpoint(Owner, foot, derived, FootRole, out footEndpoint, out Failure)
				|| !TrySafeFoot(null, foot, derived, footEndpoint, out Failure)) return false;
			if (!ExactConnectionPair(derived))
				return Fail("delve strike found missing, duplicate, or foreign stair connections", out Failure);
			return true;
		}
	}
}
