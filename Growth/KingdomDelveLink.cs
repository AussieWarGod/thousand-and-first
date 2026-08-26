using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Mutation-free result naming the exact lower landing a frozen delve will use.</summary>
	public sealed class KingdomDelveLinkIntent
	{
		public string HeadZoneId { get; internal set; }
		public string FootZoneId { get; internal set; }
		public int X { get; internal set; }
		public int Y { get; internal set; }
		public string SnapshotHash { get; internal set; }
		public string DownSlot { get; internal set; }
	}

	/// <summary>
	/// Engine-coupled paired-shaft transaction. Architecture owns one Down in the head map. This
	/// class proves an already-built claimed foot before debit, then creates exactly one reciprocal
	/// Up using Qud's native connection idiom. Named receipts make every callback boundary retryable.
	/// </summary>
	public static class KingdomDelveLink
	{
		public const string DownBlueprint = "r_KingdomDelveDown";
		public const string UpBlueprint = "r_KingdomDelveUp";
		public const string LinkState = "r_TAF_DelveLink:";
		public const string Tombstone = "0";

		public const int LinkSchema = 1;
		public const string SchemaProperty = "r_TAF_DelveLinkSchema";
		public const string PhaseProperty = "r_TAF_DelveLinkPhase";
		public const string StrikePhaseProperty = "r_TAF_DelveLinkStrikePhase";
		public const string FaultProperty = "r_TAF_DelveLinkFault";
		public const string HeadZoneProperty = "r_TAF_DelveLinkHeadZone";
		public const string FootZoneProperty = "r_TAF_DelveLinkFootZone";
		public const string XProperty = "r_TAF_DelveLinkX";
		public const string YProperty = "r_TAF_DelveLinkY";
		public const string RootProperty = "r_TAF_DelveLinkRoot";
		public const string LotProperty = "r_TAF_DelveLinkLot";
		public const string HashProperty = "r_TAF_DelveLinkHash";
		public const string DownSlotProperty = "r_TAF_DelveLinkDownSlot";
		public const string TokenProperty = "r_TAF_DelveLinkToken";
		public const string HeadEndpointProperty = "r_TAF_DelveLinkHeadEndpoint";
		public const string FootEndpointProperty = "r_TAF_DelveLinkFootEndpoint";
		public const string ReceiptProperty = "r_TAF_DelveLinkReceipt";

		public const int EndpointSchema = 1;
		public const string EndpointSchemaProperty = "r_TAF_DelveEndpointSchema";
		public const string EndpointTokenProperty = "r_TAF_DelveEndpointToken";
		public const string EndpointRoleProperty = "r_TAF_DelveEndpointRole";
		public const string EndpointRootProperty = "r_TAF_DelveEndpointRoot";
		public const string EndpointHeadZoneProperty = "r_TAF_DelveEndpointHeadZone";
		public const string EndpointFootZoneProperty = "r_TAF_DelveEndpointFootZone";
		public const string EndpointXProperty = "r_TAF_DelveEndpointX";
		public const string EndpointYProperty = "r_TAF_DelveEndpointY";
		private const string HeadRole = "head";
		private const string FootRole = "foot";
		private const int MaxFailureChars = 512;

		private sealed class Derived
		{
			internal KingdomArchitectureIntent Architecture;
			internal ArchitectureLayoutSnapshot Snapshot;
			internal ArchitecturePlacement Down;
			internal string HeadZoneId;
			internal string FootZoneId;
			internal string RootId;
			internal string LotId;
			internal string Token;
			internal int X;
			internal int Y;
		}

		/// <summary>
		/// No-spend proof. IsZoneBuilt is deliberately checked before GetZone: a refused commission
		/// never generates the lower world. Null Link means this architecture is not a delve.
		/// </summary>
		public static bool TryPreflight(KingdomSystem System, Zone Head,
			KingdomArchitectureIntent Architecture, out KingdomDelveLinkIntent Link,
			out string Failure)
		{
			Link = null;
			Failure = null;
			if (Architecture == null || !KingdomDelveRules.IsDelve(Architecture.BuildKey)) return true;
			if (System == null || !System.Founded || Head == null || The.ZoneManager == null)
				return Fail("delve link preflight needs the founded settlement and exact loaded head",
					out Failure);
			Derived derived;
			if (!TryDerive(Architecture, Head, null, null, out derived, out Failure)) return false;
			if (System.ClaimedZones == null || !System.ClaimedZones.Contains(derived.FootZoneId))
				return Fail("the exact rock below the authored shaft landing is not claimed", out Failure);
			if (!The.ZoneManager.IsZoneBuilt(derived.FootZoneId))
				return Fail("the claimed rock below has never been visited and built; visit it before sinking the shaft",
					out Failure);
			Zone foot;
			try { foot = The.ZoneManager.GetZone(derived.FootZoneId); }
			catch (Exception exception)
			{
				return Fail("the already-built lower zone could not be loaded: " + exception.Message,
					out Failure);
			}
			if (!ExactZonePair(Head, foot, derived) || !TrySafeFoot(System, foot, derived, null,
				out Failure)) return Failure != null ? false : Fail(
					"the lower landing does not match the authored shaft column", out Failure);
			if (!EmptyConnectionCell(derived.HeadZoneId, derived.X, derived.Y)
				|| !EmptyConnectionCell(derived.FootZoneId, derived.X, derived.Y))
				return Fail("the shaft column already carries a zone connection", out Failure);
			string state = ReadState(derived.HeadZoneId);
			if (state != null && state != Tombstone)
				return Fail("the shaft column carries an existing or corrupt physical-link receipt",
					out Failure);
			if (KingdomDelve.ShaftStands(derived.HeadZoneId))
				return Fail("a finished shaft already stands in this head zone", out Failure);
			Link = new KingdomDelveLinkIntent
			{
				HeadZoneId = derived.HeadZoneId,
				FootZoneId = derived.FootZoneId,
				X = derived.X,
				Y = derived.Y,
				SnapshotHash = Architecture.SnapshotHash,
				DownSlot = derived.Down.Slot
			};
			return true;
		}

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
			string encoded = ReadState(HeadZoneId);
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

		private static bool TryDerive(KingdomArchitectureIntent Architecture, Zone Head,
			string RootId, string LotId, out Derived Result, out string Failure)
		{
			Result = null;
			Failure = null;
			if (Architecture == null || Head == null || Head.ZoneID == null
				|| !KingdomDelveRules.IsDelve(Architecture.BuildKey))
				return Fail("delve link has no frozen delve architecture or exact head zone", out Failure);
			ArchitectureLayoutSnapshot snapshot;
			if (!KingdomArchitectureRuntime.TryDecode(Architecture, out snapshot, out Failure)) return false;
			ArchitecturePlacement down = null;
			for (int i = 0; i < snapshot.Placements.Count; i++)
			{
				ArchitecturePlacement placement = snapshot.Placements[i];
				bool anyStair = placement.Blueprint == DownBlueprint
					|| placement.Blueprint == UpBlueprint || placement.Blueprint == "StairsDown"
					|| placement.Blueprint == "StairsUp";
				if (!anyStair) continue;
				if (down != null || placement.Blueprint != DownBlueprint
					|| placement.Layer != ArchitectureLayer.Object
					|| !(placement.StatefulAnchor == "travel:down"
						|| (placement.StatefulAnchor != null
							&& placement.StatefulAnchor.StartsWith("travel:down@",
								StringComparison.Ordinal))))
					return Fail("frozen delve must own exactly one stateful Down and no same-map Up",
						out Failure);
				down = placement;
			}
			if (down == null)
				return Fail("frozen delve has no runtime-owned Down placement", out Failure);
			int x;
			int y;
			if (!KingdomArchitectureRuntime.TryWorldPlacement(snapshot, Architecture.Rect, down,
				out x, out y, out Failure)) return false;
			string foot;
			if (!KingdomDelveRules.TryFootZoneId(Head.ZoneID, out foot))
				return Fail("head zone has no canonical one-stratum foot", out Failure);
			Derived result = new Derived
			{
				Architecture = Architecture,
				Snapshot = snapshot,
				Down = down,
				HeadZoneId = Head.ZoneID,
				FootZoneId = foot,
				RootId = RootId,
				LotId = LotId,
				X = x,
				Y = y
			};
			if (RootId != null)
			{
				if (!KingdomDelveLinkRules.TryToken(result.HeadZoneId, result.FootZoneId,
					result.X, result.Y, RootId, LotId, Architecture.SnapshotHash,
					down.Slot, out result.Token, out Failure)) return false;
			}
			Result = result;
			return true;
		}

		private static bool TryLoadBuiltFoot(Zone Head, Derived Derived, out Zone Foot,
			out string Failure)
		{
			Foot = null;
			Failure = null;
			if (The.ZoneManager == null || !The.ZoneManager.IsZoneBuilt(Derived.FootZoneId))
				return Fail("the exact lower zone is no longer built", out Failure);
			try { Foot = The.ZoneManager.GetZone(Derived.FootZoneId); }
			catch (Exception exception)
			{
				return Fail("the already-built lower zone could not be loaded: " + exception.Message,
					out Failure);
			}
			if (!ExactZonePair(Head, Foot, Derived))
				return Fail("loaded lower zone does not match the frozen shaft column", out Failure);
			return true;
		}

		private static bool ExactZonePair(Zone Head, Zone Foot, Derived Derived)
		{
			return Head != null && Foot != null && Head.ZoneID == Derived.HeadZoneId
				&& Foot.ZoneID == Derived.FootZoneId
				&& Head.Built && Foot.Built
				&& Head.Width == Foot.Width && Head.Height == Foot.Height
				&& Derived.X >= 0 && Derived.X < Head.Width && Derived.X < Foot.Width
				&& Derived.Y >= 0 && Derived.Y < Head.Height && Derived.Y < Foot.Height
				&& KingdomDelveRules.IsShaftPair(Head.ZoneID, Foot.ZoneID);
		}

		private static bool TrySafeFoot(KingdomSystem System, Zone Foot, Derived Derived,
			GameObject ExpectedEndpoint, out string Failure)
		{
			Failure = null;
			if (Foot == null) return Fail("lower landing zone is absent", out Failure);
			Cell cell = Foot.GetCell(Derived.X, Derived.Y);
			if (cell == null || !cell.IsPassable() || cell.HasOpenLiquidVolume() || cell.HasWall()
				|| cell.HasObjectWithPart("StairsDown")
				|| (ExpectedEndpoint == null && cell.HasObjectWithPart("StairsUp")))
				return Fail("the exact lower landing contains wall, liquid, or foreign stairs",
					out Failure);
			if (System != null && KingdomConstruction.HasActiveAt(System, Foot, cell))
				return Fail("active paid construction reserves the exact lower landing", out Failure);
			List<GameObject> objects = cell.GetObjects();
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item) || ReferenceEquals(item, ExpectedEndpoint)) continue;
				if (item.IsPlayer() || item.IsCreature)
					return Fail("a living occupant stands on the exact lower landing", out Failure);
				if (item.Inventory != null || item.GetPart<LiquidVolume>() != null
					|| item.IsTakeable() || item.IsOwned() || item.IsWall() || item.IsDoor()
					|| item.GetIntProperty("KingdomBuilt") == 1
					|| item.GetIntProperty("KingdomCitizen") == 1
					|| item.GetIntProperty("KingdomStores") == 1
					|| item.GetIntProperty("KingdomLarder") == 1
					|| item.GetIntProperty(KingdomMaterials.StockpileProperty) == 1
					|| item.GetIntProperty(KingdomPlots.PlotPartProperty) == 1)
					return Fail("protected, stateful, liquid, or third-party property occupies the lower landing",
						out Failure);
				GameObjectBlueprint blueprint = item.GetBlueprint();
				if (blueprint == null || !blueprint.InheritsFrom("Floor"))
					return Fail("the lower landing contains non-floor object "
						+ (item.Blueprint ?? "<unknown>"), out Failure);
			}
			return true;
		}

		private static bool TryFindHeadEndpoint(Zone Head, Derived Derived,
			out GameObject Endpoint, out string Failure)
		{
			Endpoint = null;
			Failure = null;
			int count = 0;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Head) ?? KingdomSurvey.Take(Head);
			List<GameObject> objects = survey.ArchitectureComponents;
			for (int i = 0; i < objects.Count; i++)
			{
				GameObject item = objects[i];
				if (!GameObject.Validate(item)
					|| item.GetStringProperty(KingdomPlots.PlotIdProperty) != Derived.LotId
					|| item.GetStringProperty(KingdomArchitectureStamper.ComponentSlotProperty)
						!= Derived.Down.Slot) continue;
				count++;
				Endpoint = item;
			}
			StairsDown stairs = Endpoint == null ? null : Endpoint.GetPart<StairsDown>();
			if (count != 1 || Endpoint.Blueprint != DownBlueprint
				|| Endpoint.CurrentCell != Head.GetCell(Derived.X, Derived.Y)
				|| Endpoint.GetIntProperty(KingdomArchitectureStamper.ComponentSchemaProperty)
					!= KingdomArchitectureStamper.ComponentSchema
				|| Endpoint.GetIntProperty(KingdomArchitectureStamper.ComponentLayerProperty)
					!= (int)ArchitectureLayer.Object
				|| Endpoint.GetStringProperty(KingdomArchitectureStamper.ComponentAnchorProperty)
					!= Derived.Down.StatefulAnchor
				|| Endpoint.GetStringProperty(KingdomArchitectureStamper.ComponentHashProperty)
					!= Derived.Architecture.SnapshotHash
				|| stairs == null || !stairs.Connected || stairs.ConnectionObject != UpBlueprint)
			{
				Endpoint = null;
				return Fail("authored delve Down is absent, duplicated, moved, or changed", out Failure);
			}
			return true;
		}

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
			string rooted = Owner.GetStringProperty(FootEndpointProperty);
			if (count > 1 || (count == 1 && !string.IsNullOrEmpty(rooted) && rooted != endpoint.ID))
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
			Owner.SetStringProperty(FootEndpointProperty, endpoint.ID);
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
			if (candidate.ID != Id || candidate.CurrentZone != Zone || candidate.CurrentCell == null)
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
