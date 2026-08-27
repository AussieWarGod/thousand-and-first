using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomDelveLink
	{

		private static bool TrySettleConnections(Zone Head, Zone Foot, Derived Derived,
			out string Failure)
		{
			Failure = null;
			if (!ConnectionCellAllows(Derived.FootZoneId, Derived.X, Derived.Y,
				"StairsUp", UpBlueprint, true)
				|| !ConnectionCellAllows(Derived.HeadZoneId, Derived.X, Derived.Y,
					"StairsDown", DownBlueprint, true))
				return Fail("paired stairs acquired a foreign or duplicate zone connection", out Failure);
			try
			{
				if (CountExactConnection(Derived.FootZoneId, Derived.X, Derived.Y,
					"StairsUp", UpBlueprint) == 0)
					Head.AddZoneConnection("d", Derived.X, Derived.Y, "StairsUp", UpBlueprint);
				if (CountExactConnection(Derived.HeadZoneId, Derived.X, Derived.Y,
					"StairsDown", DownBlueprint) == 0)
					Foot.AddZoneConnection("u", Derived.X, Derived.Y, "StairsDown", DownBlueprint);
			}
			catch (Exception exception)
			{
				return Fail("paired stair connection publication threw: " + exception.Message,
					out Failure);
			}
			if (!ExactConnectionPair(Derived))
				return Fail("paired stair connections did not settle exactly once", out Failure);
			return true;
		}

		private static bool TryPublish(GameObject Owner, Zone Head, Zone Foot, Derived Derived,
			out string Failure)
		{
			Failure = null;
			GameObject headEndpoint;
			GameObject footEndpoint;
			if (!TryExactStoredEndpoint(Owner, Head, Derived, HeadRole, out headEndpoint, out Failure)
				|| !TryExactStoredEndpoint(Owner, Foot, Derived, FootRole, out footEndpoint, out Failure)
				|| !ExactConnectionPair(Derived)) return false;
			KingdomDelveLinkReceipt receipt;
			if (!KingdomDelveLinkRules.TryCreate(Derived.HeadZoneId, Derived.FootZoneId,
				Derived.X, Derived.Y, Derived.RootId, Derived.LotId,
				Derived.Architecture.SnapshotHash, Derived.Down.Slot,
				headEndpoint.ID, footEndpoint.ID, out receipt, out Failure)) return false;
			string encoded;
			if (!KingdomDelveLinkRules.TryEncode(receipt, out encoded, out Failure)) return false;
			string rooted = Owner.GetStringProperty(ReceiptProperty);
			if (!string.IsNullOrEmpty(rooted) && rooted != encoded)
				return Quarantine(Owner, "delve root carries a conflicting canonical link receipt",
					out Failure);
			if (The.Game == null) return Fail("no game can publish a delve physical link", out Failure);
			string state = ReadState(Derived.HeadZoneId);
			if (state != null && state != Tombstone && state != encoded)
				return Quarantine(Owner, "global delve link authority conflicts with exact endpoints",
					out Failure);
			The.Game.SetStringGameState(LinkState + Derived.HeadZoneId, encoded);
			if (ReadState(Derived.HeadZoneId) != encoded)
				return Fail("global delve link receipt did not persist", out Failure);
			Owner.SetStringProperty(ReceiptProperty, encoded);
			The.Game.SetIntGameState(KingdomDelve.ShaftState + Derived.HeadZoneId, 1);
			Owner.SetIntProperty(PhaseProperty, 3);
			return true;
		}

		private static bool TryProveActive(GameObject Owner, Zone Head, Zone Foot,
			Derived Derived, out string Failure)
		{
			Failure = null;
			GameObject headEndpoint;
			GameObject footEndpoint;
			if (!TryReadRoot(Owner, Derived, out Failure)
				|| Owner.GetIntProperty(PhaseProperty) != 3
				|| !TryExactStoredEndpoint(Owner, Head, Derived, HeadRole, out headEndpoint, out Failure)
				|| !TryExactStoredEndpoint(Owner, Foot, Derived, FootRole, out footEndpoint, out Failure)
				|| !ExactConnectionPair(Derived)) return false;
			KingdomDelveLinkReceipt receipt;
			string encoded = Owner.GetStringProperty(ReceiptProperty);
			if (!KingdomDelveLinkRules.TryDecode(encoded, out receipt, out Failure)
				|| receipt.HeadZoneId != Derived.HeadZoneId || receipt.FootZoneId != Derived.FootZoneId
				|| receipt.HeadEndpointId != headEndpoint.ID || receipt.FootEndpointId != footEndpoint.ID
				|| receipt.RootId != Derived.RootId || receipt.Token != Derived.Token
				|| ReadState(Derived.HeadZoneId) != encoded)
				return Fail("published delve receipt disagrees with physical endpoints", out Failure);
			return true;
		}

		private static bool TryManagedStrikeLane(GameObject Owner, Zone Head,
			out bool Managed, out string Failure)
		{
			Managed = false;
			Failure = null;
			if (Owner == null || Head == null)
				return Fail("delve strike has no exact root or head", out Failure);
			bool rootEvidence = Owner.HasIntProperty(SchemaProperty) || HasAnyRootField(Owner);
			if (rootEvidence)
			{
				Managed = true;
				return true;
			}
			string buildKey = Owner.GetStringProperty(KingdomUpgrade.BuildKeyProperty);
			string frozenBuildKey = Owner.GetStringProperty(KingdomArchitectureRuntime.BuildKeyProperty);
			if (!KingdomDelveRules.IsDelve(buildKey)
				&& !KingdomDelveRules.IsDelve(frozenBuildKey)) return true;
			string state = ReadState(Head.ZoneID);
			if (state != null && state != Tombstone)
			{
				Managed = true;
				return true;
			}
			bool architectureMarker = Owner.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Owner.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty);
			string encoded = Owner.GetStringProperty(KingdomArchitectureRuntime.SnapshotProperty);
			if (!architectureMarker
				&& !KingdomArchitectureRules.IsCurrentSnapshotEncoding(encoded)) return true;
			KingdomArchitectureIntent architecture;
			if (!KingdomArchitectureRuntime.TryRead(Owner, out architecture, out Failure)) return false;
			if (!KingdomDelveRules.IsDelve(architecture.BuildKey))
				return Fail("delve strike identities disagree with frozen architecture", out Failure);
			if (KingdomArchitectureRules.IsCurrentSnapshotEncoding(architecture.EncodedSnapshot))
				return Fail("current authored delve is missing its physical-link root", out Failure);
			// Explicit read-only legacy architecture remains in the legacy strike lane.
			return true;
		}

		private static bool TryStrikeBase(GameObject Owner, Zone Head, out Derived Derived,
			out Zone Foot, out string Failure)
		{
			Derived = null;
			Foot = null;
			Failure = null;
			if (Owner == null || Head == null) return Fail("delve strike has no exact root or head",
				out Failure);
			KingdomArchitectureIntent architecture;
			ArchitectureLayoutSnapshot snapshot;
			string lot;
			if (!KingdomArchitectureStamper.TryReadOwner(Owner, out architecture, out snapshot,
				out lot, out Failure)) return false;
			if (!KingdomDelveRules.IsDelve(architecture.BuildKey)) return true;
			if (!TryDerive(architecture, Head, Owner.ID, lot, out Derived, out Failure)
				|| !TryReadRoot(Owner, Derived, out Failure)
				|| !TryLoadBuiltFoot(Head, Derived, out Foot, out Failure)) return false;
			if (Owner.GetIntProperty(PhaseProperty) != 3)
				return Fail("delve physical link is incomplete", out Failure);
			string encoded = Owner.GetStringProperty(ReceiptProperty);
			if (Owner.GetIntProperty(StrikePhaseProperty) < 3
				&& ReadState(Derived.HeadZoneId) != encoded)
				return Fail("global delve receipt changed before exact pair removal", out Failure);
			return true;
		}

		private static bool TryExactStoredEndpoint(GameObject Owner, Zone Zone, Derived Derived,
			string Role, out GameObject Endpoint, out string Failure)
		{
			Endpoint = null;
			string id = Owner.GetStringProperty(Role == HeadRole
				? HeadEndpointProperty : FootEndpointProperty);
			if (string.IsNullOrEmpty(id) || id.Length > KingdomDelveLinkRules.MaxIdChars
				|| FindExactEndpoint(Zone, id, out Endpoint)
					!= KingdomPhysicalLookupState.Exact)
				return Fail("stored delve " + Role + " endpoint is absent or ambiguous", out Failure);
			return ExactEndpoint(Endpoint, Zone, Derived, Role, out Failure);
		}

		private static bool ExactConnectionPair(Derived Derived)
		{
			return CountConnectionCell(Derived.FootZoneId, Derived.X, Derived.Y) == 1
				&& CountExactConnection(Derived.FootZoneId, Derived.X, Derived.Y,
					"StairsUp", UpBlueprint) == 1
				&& CountConnectionCell(Derived.HeadZoneId, Derived.X, Derived.Y) == 1
				&& CountExactConnection(Derived.HeadZoneId, Derived.X, Derived.Y,
					"StairsDown", DownBlueprint) == 1;
		}

		private static bool ConnectionCellAllows(string ZoneId, int X, int Y,
			string Type, string Object, bool AllowMissing)
		{
			int total = CountConnectionCell(ZoneId, X, Y);
			int exact = CountExactConnection(ZoneId, X, Y, Type, Object);
			return (AllowMissing && total == 0) || (total == 1 && exact == 1);
		}

		private static bool EmptyConnectionCell(string ZoneId, int X, int Y)
		{
			return CountConnectionCell(ZoneId, X, Y) == 0;
		}

		private static int CountConnectionCell(string ZoneId, int X, int Y)
		{
			if (The.ZoneManager == null) return int.MaxValue;
			int count = 0;
			List<ZoneConnection> connections = The.ZoneManager.GetZoneConnections(ZoneId);
			for (int i = 0; i < connections.Count; i++)
			{
				ZoneConnection connection = connections[i];
				if (connection != null && connection.X == X && connection.Y == Y) count++;
			}
			return count;
		}

		private static int CountExactConnection(string ZoneId, int X, int Y,
			string Type, string Object)
		{
			if (The.ZoneManager == null) return 0;
			int count = 0;
			List<ZoneConnection> connections = The.ZoneManager.GetZoneConnections(ZoneId);
			for (int i = 0; i < connections.Count; i++)
			{
				ZoneConnection connection = connections[i];
				if (connection != null && connection.X == X && connection.Y == Y
					&& connection.Type == Type && connection.Object == Object) count++;
			}
			return count;
		}

		private static bool HasAnyRootField(GameObject Owner)
		{
			return Owner.HasStringProperty(SchemaProperty)
				|| Owner.HasStringProperty(HeadZoneProperty) || Owner.HasStringProperty(FootZoneProperty)
				|| Owner.HasIntProperty(XProperty) || Owner.HasIntProperty(YProperty)
				|| Owner.HasStringProperty(RootProperty) || Owner.HasStringProperty(LotProperty)
				|| Owner.HasStringProperty(HashProperty) || Owner.HasStringProperty(DownSlotProperty)
				|| Owner.HasStringProperty(TokenProperty) || Owner.HasStringProperty(HeadEndpointProperty)
				|| Owner.HasStringProperty(FootEndpointProperty) || Owner.HasStringProperty(ReceiptProperty)
				|| Owner.HasIntProperty(PhaseProperty) || Owner.HasIntProperty(StrikePhaseProperty);
		}

		private static bool ExactString(GameObject Object, string Property, string Expected)
		{
			return Object.HasStringProperty(Property) && !Object.HasIntProperty(Property)
				&& Object.GetStringProperty(Property) == Expected;
		}

		private static bool ExactInt(GameObject Object, string Property, int Expected)
		{
			return Object.HasIntProperty(Property) && !Object.HasStringProperty(Property)
				&& Object.GetIntProperty(Property) == Expected;
		}

		private static bool BoundedIdentity(string Value, int Max)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > Max) return false;
			for (int i = 0; i < Value.Length; i++) if (char.IsControl(Value[i])) return false;
			return true;
		}

		private static string ReadState(string HeadZoneId)
		{
			return string.IsNullOrEmpty(HeadZoneId) || The.Game == null
				? null : The.Game.GetStringGameState(LinkState + HeadZoneId, null);
		}

		private static bool Quarantine(GameObject Owner, string Message, out string Failure)
		{
			string bounded = Bounded(Message);
			try { if (Owner != null) Owner.SetStringProperty(FaultProperty, bounded); } catch { }
			Failure = bounded;
			return false;
		}

		private static string Bounded(string Message)
		{
			if (string.IsNullOrEmpty(Message)) return "unknown delve link fault";
			return Message.Length <= MaxFailureChars ? Message : Message.Substring(0, MaxFailureChars);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Bounded(Message);
			return false;
		}
	}
}
