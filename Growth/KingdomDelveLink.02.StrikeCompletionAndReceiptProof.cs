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
		/// Completes pair removal after ordinary authored strike removed the Down, but before the
		/// behavior root is destroyed. The receipt is tombstoned only after exact absence proof.
		/// </summary>
		public static bool TryFinishStrike(GameObject Owner, Zone Head, out string Failure)
		{
			Failure = null;
			bool managed;
			if (!TryManagedStrikeLane(Owner, Head, out managed, out Failure)) return false;
			if (!managed) return true;
			Derived derived;
			Zone foot;
			if (!TryStrikeBase(Owner, Head, out derived, out foot, out Failure)) return false;
			if (derived == null) return true;
			if (Owner.GetIntProperty(PhaseProperty) != 3)
				return Fail("only a complete physical delve link may finish strike", out Failure);
			int phase = Owner.GetIntProperty(StrikePhaseProperty);
			if (phase < 0 || phase > 4)
				return Quarantine(Owner, "delve strike phase is outside its bounded state machine",
					out Failure);
			GameObject ignored;
			if (FindExactEndpoint(Head,
				Owner.GetStringProperty(HeadEndpointProperty), out ignored)
				!= KingdomPhysicalLookupState.Absent)
				return Fail("authored strike has not yet removed the exact delve Down endpoint",
					out Failure);
			if (phase == 0)
			{
				GameObject footEndpoint;
				if (!TryExactStoredEndpoint(Owner, foot, derived, FootRole, out footEndpoint,
					out Failure) || !TrySafeFoot(null, foot, derived, footEndpoint, out Failure)) return false;
				Owner.SetIntProperty(StrikePhaseProperty, 1);
				phase = 1;
			}
			if (phase == 1)
			{
				string footId = Owner.GetStringProperty(FootEndpointProperty);
				GameObject footEndpoint;
				KingdomPhysicalLookupState state = FindExactEndpoint(foot, footId,
					out footEndpoint);
				if (state == KingdomPhysicalLookupState.Exact)
				{
					if (!ExactEndpoint(footEndpoint, foot, derived, FootRole, out Failure)) return false;
					bool removed;
					try { removed = footEndpoint.Destroy(null, Silent: true); }
					catch (Exception exception)
					{
						removed = false;
						Failure = "paired Up removal threw after strike intent: " + exception.Message;
					}
					KingdomPhysicalLookupState after = FindExactEndpoint(foot, footId,
						out ignored);
					if ((!removed && after != KingdomPhysicalLookupState.Absent)
						|| after != KingdomPhysicalLookupState.Absent)
						return Fail(Failure ?? "paired Up removal was vetoed", out Failure);
					Failure = null;
				}
				else if (state != KingdomPhysicalLookupState.Absent)
					return Quarantine(Owner, "paired Up identity is ambiguous during strike", out Failure);
				Owner.SetIntProperty(StrikePhaseProperty, 2);
				phase = 2;
			}
			if (phase == 2)
			{
				try
				{
					Head.RemoveZoneConnection("d", derived.X, derived.Y, "StairsUp", UpBlueprint);
					foot.RemoveZoneConnection("u", derived.X, derived.Y, "StairsDown", DownBlueprint);
				}
				catch (Exception exception)
				{
					return Fail("paired stair connection removal threw: " + exception.Message, out Failure);
				}
				if (!EmptyConnectionCell(derived.HeadZoneId, derived.X, derived.Y)
					|| !EmptyConnectionCell(derived.FootZoneId, derived.X, derived.Y))
					return Fail("paired stair connections survived exact removal", out Failure);
				Owner.SetIntProperty(StrikePhaseProperty, 3);
				phase = 3;
			}
			if (phase == 3)
			{
				if (The.Game == null) return Fail("no game can publish delve strike completion", out Failure);
				The.Game.SetStringGameState(LinkState + derived.HeadZoneId, Tombstone);
				if (ReadState(derived.HeadZoneId) != Tombstone)
					return Fail("delve physical-link tombstone did not persist", out Failure);
				The.Game.SetIntGameState(KingdomDelve.ShaftState + derived.HeadZoneId, 0);
				Owner.SetIntProperty(StrikePhaseProperty, 4);
				phase = 4;
			}
			return phase == 4 && ReadState(derived.HeadZoneId) == Tombstone
				&& EmptyConnectionCell(derived.HeadZoneId, derived.X, derived.Y)
				&& EmptyConnectionCell(derived.FootZoneId, derived.X, derived.Y);
		}

		/// <summary>Strict canonical state read used by KingdomDelve reach.</summary>
		public static bool TryReadPhysicalReceipt(string HeadZoneId,
			out KingdomDelveLinkReceipt Receipt)
		{
			Receipt = null;
			string encoded = ReadState(HeadZoneId);
			string failure;
			return encoded != null && encoded != Tombstone
				&& KingdomDelveLinkRules.TryDecode(encoded, out Receipt, out failure)
				&& Receipt.HeadZoneId == HeadZoneId;
		}

		/// <summary>
		/// Save/reload proof used by reach. A canonical state string alone is never a shaft: both
		/// already-built zones, root, endpoints, reciprocal parts, and connection records must agree.
		/// </summary>
		public static bool PhysicalLinkStands(string HeadZoneId)
		{
			KingdomDelveLinkReceipt receipt;
			if (!TryReadPhysicalReceipt(HeadZoneId, out receipt) || The.ZoneManager == null)
				return false;
			// Never let a reach query generate a zone while trying to prove its stairs.
			if (!The.ZoneManager.IsZoneBuilt(receipt.HeadZoneId)
				|| !The.ZoneManager.IsZoneBuilt(receipt.FootZoneId)) return false;
			Zone head;
			Zone foot;
			try
			{
				head = The.ZoneManager.GetZone(receipt.HeadZoneId);
				foot = The.ZoneManager.GetZone(receipt.FootZoneId);
			}
			catch { return false; }
			return ExactPhysicalLinkStands(receipt, head, foot);
		}

		/// <summary>
		/// The same exact proof for optional civic observation, but only when both endpoints are
		/// already cached. It never calls <c>GetZone</c>, so a Charter read cannot thaw remote ground.
		/// </summary>
		public static bool PhysicalLinkStandsLoaded(string HeadZoneId)
		{
			if (!TryReadPhysicalReceipt(HeadZoneId, out KingdomDelveLinkReceipt receipt)
				|| The.ZoneManager?.CachedZones == null
				|| !The.ZoneManager.CachedZones.TryGetValue(receipt.HeadZoneId, out Zone head)
				|| !The.ZoneManager.CachedZones.TryGetValue(receipt.FootZoneId, out Zone foot))
				return false;
			return ExactPhysicalLinkStands(receipt, head, foot);
		}

		private static bool ExactPhysicalLinkStands(KingdomDelveLinkReceipt receipt,
			Zone head, Zone foot)
		{
			if (head == null || foot == null || !head.Built || !foot.Built
				|| head.ZoneID != receipt.HeadZoneId
				|| foot.ZoneID != receipt.FootZoneId || head.Width != foot.Width
				|| head.Height != foot.Height || receipt.X < 0 || receipt.X >= head.Width
				|| receipt.Y < 0 || receipt.Y >= head.Height) return false;
			GameObject root;
			GameObject down;
			GameObject up;
			if (FindExactEndpoint(head, receipt.RootId, out root)
					!= KingdomPhysicalLookupState.Exact
				|| FindExactEndpoint(head, receipt.HeadEndpointId, out down)
					!= KingdomPhysicalLookupState.Exact
				|| FindExactEndpoint(foot, receipt.FootEndpointId, out up)
					!= KingdomPhysicalLookupState.Exact) return false;
			string encoded = ReadState(receipt.HeadZoneId);
			StairsDown downPart = down.GetPart<StairsDown>();
			StairsUp upPart = up.GetPart<StairsUp>();
			return root.CurrentZone == head
				&& ExactInt(root, SchemaProperty, LinkSchema)
				&& ExactInt(root, PhaseProperty, 3)
				&& ExactInt(root, StrikePhaseProperty, 0)
				&& ExactString(root, HeadZoneProperty, receipt.HeadZoneId)
				&& ExactString(root, FootZoneProperty, receipt.FootZoneId)
				&& ExactInt(root, XProperty, receipt.X)
				&& ExactInt(root, YProperty, receipt.Y)
				&& ExactString(root, RootProperty, receipt.RootId)
				&& ExactString(root, LotProperty, receipt.LotId)
				&& ExactString(root, HashProperty, receipt.SnapshotHash)
				&& ExactString(root, DownSlotProperty, receipt.DownSlot)
				&& ExactString(root, ReceiptProperty, encoded)
				&& ExactString(root, TokenProperty, receipt.Token)
				&& down.CurrentCell == head.GetCell(receipt.X, receipt.Y)
				&& up.CurrentCell == foot.GetCell(receipt.X, receipt.Y)
				&& down.Blueprint == DownBlueprint && up.Blueprint == UpBlueprint
				&& ExactInt(down, EndpointSchemaProperty, EndpointSchema)
				&& ExactInt(up, EndpointSchemaProperty, EndpointSchema)
				&& ExactString(down, EndpointTokenProperty, receipt.Token)
				&& ExactString(up, EndpointTokenProperty, receipt.Token)
				&& ExactString(down, EndpointRoleProperty, HeadRole)
				&& ExactString(up, EndpointRoleProperty, FootRole)
				&& ExactString(down, EndpointRootProperty, receipt.RootId)
				&& ExactString(up, EndpointRootProperty, receipt.RootId)
				&& ExactString(down, EndpointHeadZoneProperty, receipt.HeadZoneId)
				&& ExactString(up, EndpointHeadZoneProperty, receipt.HeadZoneId)
				&& ExactString(down, EndpointFootZoneProperty, receipt.FootZoneId)
				&& ExactString(up, EndpointFootZoneProperty, receipt.FootZoneId)
				&& ExactInt(down, EndpointXProperty, receipt.X)
				&& ExactInt(up, EndpointXProperty, receipt.X)
				&& ExactInt(down, EndpointYProperty, receipt.Y)
				&& ExactInt(up, EndpointYProperty, receipt.Y)
				&& ExactString(up, KingdomPlots.PlotIdProperty, receipt.LotId)
				&& ExactString(up, KingdomArchitectureStamper.ComponentSlotProperty,
					"external-up:" + receipt.DownSlot)
				&& ExactString(up, KingdomArchitectureStamper.ComponentAnchorProperty, "travel:up")
				&& ExactString(up, KingdomArchitectureStamper.ComponentHashProperty,
					receipt.SnapshotHash)
				&& CountEndpointAt(head.GetCell(receipt.X, receipt.Y), receipt.Token, null) == 1
				&& CountEndpointAt(foot.GetCell(receipt.X, receipt.Y), receipt.Token, null) == 1
				&& CountEndpointAt(head.GetCell(receipt.X, receipt.Y), receipt.Token, HeadRole) == 1
				&& CountEndpointAt(foot.GetCell(receipt.X, receipt.Y), receipt.Token, FootRole) == 1
				&& CountPartAt(head.GetCell(receipt.X, receipt.Y), "StairsDown") == 1
				&& CountPartAt(head.GetCell(receipt.X, receipt.Y), "StairsUp") == 0
				&& CountPartAt(foot.GetCell(receipt.X, receipt.Y), "StairsUp") == 1
				&& CountPartAt(foot.GetCell(receipt.X, receipt.Y), "StairsDown") == 0
				&& head.GetCell(receipt.X, receipt.Y).IsPassable(null, false)
				&& foot.GetCell(receipt.X, receipt.Y).IsPassable(null, false)
				&& !head.GetCell(receipt.X, receipt.Y).HasOpenLiquidVolume()
				&& !foot.GetCell(receipt.X, receipt.Y).HasOpenLiquidVolume()
				&& downPart != null && downPart.Connected
				&& downPart.ConnectionObject == UpBlueprint
				&& upPart != null && upPart.Connected
				&& upPart.ConnectionObject == DownBlueprint
				&& CountConnectionCell(receipt.FootZoneId, receipt.X, receipt.Y) == 1
				&& CountExactConnection(receipt.FootZoneId, receipt.X, receipt.Y,
					"StairsUp", UpBlueprint) == 1
				&& CountConnectionCell(receipt.HeadZoneId, receipt.X, receipt.Y) == 1
				&& CountExactConnection(receipt.HeadZoneId, receipt.X, receipt.Y,
					"StairsDown", DownBlueprint) == 1;
		}

		/// <summary>Whether a new-format state key exists, including corrupt data or tombstone.</summary>
		public static bool HasPhysicalState(string HeadZoneId)
		{
			return ReadState(HeadZoneId) != null;
		}
	}
}
