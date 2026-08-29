using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomDelveLink
	{

		private static bool TryInitializeRoot(GameObject Owner, Derived Derived, out string Failure)
		{
			Failure = null;
			try
			{
				Owner.RemoveIntProperty(SchemaProperty);
				Owner.SetStringProperty(HeadZoneProperty, Derived.HeadZoneId);
				Owner.SetStringProperty(FootZoneProperty, Derived.FootZoneId);
				Owner.SetIntProperty(XProperty, Derived.X);
				Owner.SetIntProperty(YProperty, Derived.Y);
				Owner.SetStringProperty(RootProperty, Derived.RootId);
				Owner.SetStringProperty(LotProperty, Derived.LotId);
				Owner.SetStringProperty(HashProperty, Derived.Architecture.SnapshotHash);
				Owner.SetStringProperty(DownSlotProperty, Derived.Down.Slot);
				Owner.SetStringProperty(TokenProperty, Derived.Token);
				Owner.SetStringProperty(HeadEndpointProperty, null, RemoveIfNull: true);
				Owner.SetStringProperty(FootEndpointProperty, null, RemoveIfNull: true);
				Owner.SetStringProperty(ReceiptProperty, null, RemoveIfNull: true);
				Owner.SetStringProperty(FaultProperty, null, RemoveIfNull: true);
				Owner.SetIntProperty(PhaseProperty, 0);
				Owner.SetIntProperty(StrikePhaseProperty, 0);
				Owner.SetIntProperty(SchemaProperty, LinkSchema);
			}
			catch (Exception exception)
			{
				try { Owner.RemoveIntProperty(SchemaProperty); } catch { }
				return Fail("delve link root receipt write failed: " + exception.Message, out Failure);
			}
			return TryReadRoot(Owner, Derived, out Failure);
		}

		private static bool TryReadRoot(GameObject Owner, Derived Derived, out string Failure)
		{
			Failure = null;
			if (Owner == null || !Owner.HasIntProperty(SchemaProperty)
				|| Owner.HasStringProperty(SchemaProperty)
				|| Owner.GetIntProperty(SchemaProperty) != LinkSchema)
				return Fail("delve link root receipt is absent, partial, or unknown", out Failure);
			string fault = Owner.GetStringProperty(FaultProperty);
			if (!string.IsNullOrEmpty(fault))
				return Fail("delve link is quarantined: " + Bounded(fault), out Failure);
			int phase = Owner.GetIntProperty(PhaseProperty);
			int strike = Owner.GetIntProperty(StrikePhaseProperty);
			if (phase < 0 || phase > 3 || strike < 0 || strike > 4
				|| !ExactInt(Owner, PhaseProperty, phase)
				|| !ExactInt(Owner, StrikePhaseProperty, strike)
				|| !ExactString(Owner, HeadZoneProperty, Derived.HeadZoneId)
				|| !ExactString(Owner, FootZoneProperty, Derived.FootZoneId)
				|| !ExactInt(Owner, XProperty, Derived.X)
				|| !ExactInt(Owner, YProperty, Derived.Y)
				|| !ExactString(Owner, RootProperty, Derived.RootId)
				|| !ExactString(Owner, LotProperty, Derived.LotId)
				|| !ExactString(Owner, HashProperty, Derived.Architecture.SnapshotHash)
				|| !ExactString(Owner, DownSlotProperty, Derived.Down.Slot)
				|| !ExactString(Owner, TokenProperty, Derived.Token))
				return Quarantine(Owner, "delve link root scalars disagree with frozen architecture",
					out Failure);
			string headId = Owner.GetStringProperty(HeadEndpointProperty);
			string footId = Owner.GetStringProperty(FootEndpointProperty);
			string receipt = Owner.GetStringProperty(ReceiptProperty);
			if ((phase >= 1 && !BoundedIdentity(headId, KingdomDelveLinkRules.MaxIdChars))
				|| (phase >= 2 && !BoundedIdentity(footId, KingdomDelveLinkRules.MaxIdChars))
				|| (phase >= 3 && (string.IsNullOrEmpty(receipt)
					|| receipt.Length > KingdomDelveLinkRules.MaxReceiptChars))
				|| (phase == 0 && (!string.IsNullOrEmpty(footId) || !string.IsNullOrEmpty(receipt)))
				|| (phase == 1 && !string.IsNullOrEmpty(receipt)))
				return Quarantine(Owner, "delve link phase fields are partial or ahead by more than one boundary",
					out Failure);
			return true;
		}

		private static bool TrySettleFootEndpoint(GameObject Owner, Zone Foot, Derived Derived,
			out string Failure)
		{
			Failure = null;
			GameObject endpoint;
			int count = FindEndpointByToken(Foot, Derived, FootRole, out endpoint);
			bool created = false;
			string rooted = Owner.GetStringProperty(FootEndpointProperty);
			if (count > 1 || (count == 1 && !string.IsNullOrEmpty(rooted) && rooted != endpoint.IDIfAssigned))
				return Quarantine(Owner, "paired Up identity is duplicated or conflicts with its root",
					out Failure);
			if (count == 0)
			{
				if (!string.IsNullOrEmpty(rooted))
					return Quarantine(Owner, "published paired Up vanished before settlement", out Failure);
				if (!TrySafeFoot(null, Foot, Derived, null, out Failure)) return false;
				try { endpoint = GameObject.Create(UpBlueprint); }
				catch (Exception exception)
				{
					return Fail("paired Up creation threw: " + exception.Message, out Failure);
				}
				if (!GameObject.Validate(endpoint) || endpoint.Blueprint != UpBlueprint)
					return Fail("paired Up blueprint created no exact endpoint", out Failure);
				created = true;
				StampEndpoint(endpoint, Derived, FootRole);
				try
				{
					GameObject accepted = Foot.GetCell(Derived.X, Derived.Y).AddObject(endpoint,
						NoStack: true, Silent: true);
					KingdomSurvey.ObserveAddResultInActive(Foot, endpoint, accepted);
					if (!ReferenceEquals(accepted, endpoint))
						return Quarantine(Owner, "paired Up AddObject replaced its exact output",
							out Failure);
				}
				catch (Exception exception)
				{
					count = FindEndpointByToken(Foot, Derived, FootRole, out endpoint);
					if (count != 1)
						return Fail("paired Up AddObject threw without one recoverable output: "
							+ exception.Message, out Failure);
				}
			}
			if (!ExactEndpoint(endpoint, Foot, Derived, FootRole, out Failure))
				return Quarantine(Owner, Failure ?? "paired Up failed exact world proof", out Failure);
			string endpointId = created ? endpoint.ID : endpoint.IDIfAssigned;
			if (string.IsNullOrEmpty(endpointId))
				return Fail("paired Up endpoint has no stable identity", out Failure);
			Owner.SetStringProperty(FootEndpointProperty, endpointId);
			Owner.SetIntProperty(PhaseProperty, 2);
			return true;
		}

		private static void StampEndpoint(GameObject Endpoint, Derived Derived, string Role)
		{
			Endpoint.RemoveIntProperty(EndpointSchemaProperty);
			Endpoint.SetStringProperty(EndpointTokenProperty, Derived.Token);
			Endpoint.SetStringProperty(EndpointRoleProperty, Role);
			Endpoint.SetStringProperty(EndpointRootProperty, Derived.RootId);
			Endpoint.SetStringProperty(EndpointHeadZoneProperty, Derived.HeadZoneId);
			Endpoint.SetStringProperty(EndpointFootZoneProperty, Derived.FootZoneId);
			Endpoint.SetIntProperty(EndpointXProperty, Derived.X);
			Endpoint.SetIntProperty(EndpointYProperty, Derived.Y);
			if (Role == FootRole)
			{
				Endpoint.SetIntProperty(KingdomPlots.PlotPartProperty, 1);
				Endpoint.SetStringProperty(KingdomPlots.PlotIdProperty, Derived.LotId);
				Endpoint.SetStringProperty(KingdomArchitectureStamper.ComponentSlotProperty,
					"external-up:" + Derived.Down.Slot);
				Endpoint.SetIntProperty(KingdomArchitectureStamper.ComponentLayerProperty,
					(int)ArchitectureLayer.Object);
				Endpoint.SetStringProperty(KingdomArchitectureStamper.ComponentAnchorProperty,
					"travel:up");
				Endpoint.SetStringProperty(KingdomArchitectureStamper.ComponentHashProperty,
					Derived.Architecture.SnapshotHash);
			}
			Endpoint.SetIntProperty(EndpointSchemaProperty, EndpointSchema);
		}

		private static bool ExactEndpoint(GameObject Endpoint, Zone Zone, Derived Derived,
			string Role, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Endpoint) || Endpoint.CurrentZone != Zone
				|| Endpoint.CurrentCell != Zone.GetCell(Derived.X, Derived.Y)
				|| !ExactInt(Endpoint, EndpointSchemaProperty, EndpointSchema)
				|| Endpoint.GetStringProperty(EndpointTokenProperty) != Derived.Token
				|| Endpoint.GetStringProperty(EndpointRoleProperty) != Role
				|| Endpoint.GetStringProperty(EndpointRootProperty) != Derived.RootId
				|| Endpoint.GetStringProperty(EndpointHeadZoneProperty) != Derived.HeadZoneId
				|| Endpoint.GetStringProperty(EndpointFootZoneProperty) != Derived.FootZoneId
				|| !ExactInt(Endpoint, EndpointXProperty, Derived.X)
				|| !ExactInt(Endpoint, EndpointYProperty, Derived.Y))
				return Fail("delve endpoint receipt is missing, moved, partial, or corrupt", out Failure);
			if (Role == HeadRole)
			{
				StairsDown down = Endpoint.GetPart<StairsDown>();
				if (Endpoint.Blueprint != DownBlueprint || down == null || !down.Connected
					|| down.ConnectionObject != UpBlueprint)
					return Fail("delve head is not the exact reciprocal Down wrapper", out Failure);
			}
			else
			{
				StairsUp up = Endpoint.GetPart<StairsUp>();
				if (Endpoint.Blueprint != UpBlueprint || up == null || !up.Connected
					|| up.ConnectionObject != DownBlueprint
					|| Endpoint.GetStringProperty(KingdomPlots.PlotIdProperty) != Derived.LotId
					|| Endpoint.GetStringProperty(KingdomArchitectureStamper.ComponentSlotProperty)
						!= "external-up:" + Derived.Down.Slot
					|| Endpoint.GetStringProperty(KingdomArchitectureStamper.ComponentAnchorProperty)
						!= "travel:up")
					return Fail("delve foot is not the exact reciprocal owned Up wrapper", out Failure);
			}
			return true;
		}

		private static int FindEndpointByToken(Zone Zone, Derived Derived, string Role,
			out GameObject Endpoint)
		{
			Endpoint = null;
			int count = 0;
			Cell cell = Zone?.GetCell(Derived.X, Derived.Y);
			if (cell == null) return 0;
			List<GameObject> objects = cell?.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject candidate = objects[i];
				if (!GameObject.Validate(candidate)
					|| candidate.GetStringProperty(EndpointTokenProperty) != Derived.Token
					|| candidate.GetStringProperty(EndpointRoleProperty) != Role) continue;
				count++;
				Endpoint = candidate;
			}
			if (count != 1) Endpoint = null;
			return count;
		}

		private static int CountEndpointAt(Cell Cell, string Token, string Role)
		{
			if (Cell == null) return int.MaxValue;
			int count = 0;
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject candidate = objects[i];
				if (GameObject.Validate(candidate)
					&& candidate.GetStringProperty(EndpointTokenProperty) == Token
					&& (Role == null
						|| candidate.GetStringProperty(EndpointRoleProperty) == Role)) count++;
			}
			return count;
		}

		/// <summary>Exact-ID lookup for the already-loaded half of a cross-zone link. The active
		/// ground keeps duplicate proof through its maintained survey; remote ground uses Qud's
		/// unique object-ID authority and then reproves exact zone ownership and shape at the
		/// canonical landing cell. It never starts a second classified zone snapshot.</summary>
		private static KingdomPhysicalLookupState FindExactEndpoint(Zone Zone, string Id,
			out GameObject Endpoint)
		{
			Endpoint = null;
			if (Zone == null || string.IsNullOrEmpty(Id)) return KingdomPhysicalLookupState.Absent;
			if (KingdomSurvey.ActiveFor(Zone) != null)
				return KingdomConstruction.FindExactId(Zone, Id, out Endpoint);
			GameObject candidate = GameObject.FindByID(Id);
			if (!GameObject.Validate(candidate)) return KingdomPhysicalLookupState.Absent;
			if (candidate.IDIfAssigned != Id || candidate.CurrentZone != Zone || candidate.CurrentCell == null)
				return KingdomPhysicalLookupState.Ambiguous;
			Endpoint = candidate;
			return KingdomPhysicalLookupState.Exact;
		}

		private static int CountPartAt(Cell Cell, string Part)
		{
			if (Cell == null) return int.MaxValue;
			int count = 0;
			List<GameObject> objects = Cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject candidate = objects[i];
				if (GameObject.Validate(candidate) && candidate.HasPart(Part)) count++;
			}
			return count;
		}
	}
}
