using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomDelveLink
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
	}
}
