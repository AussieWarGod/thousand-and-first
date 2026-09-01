using System;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Attended navigation among already-authorized hosted-arcology zones. This owns no authority:
	/// it refuses gallery shells and unpaid floors, asks vanilla to realize the exact native zone,
	/// and moves only the tester without force.
	/// </summary>
	internal static class KingdomScenarioHostedArcology
	{
		internal const string Verb = "arcology";

		internal static string Run(string Argument, out bool Ok)
		{
			Ok = false;
			int x, y, z;
			KingdomArcologyProgramme programme;
			string lotKey;
			if (!TryTarget((Argument ?? "").Trim().ToLowerInvariant(), out x, out y, out z,
				out programme, out lotKey))
				return Refused("use arcology entry, teaching, terrace, or ward");
			GameObject player = The.Player;
			Zone zone = player?.CurrentZone;
			if (!GameObject.Validate(player) || zone == null || player.CurrentCell == null)
				return Refused("stand beside the production shell or inside its loaded interior");
			Cell originCell = player.CurrentCell;
			Zone originZone = zone;
			if (GalleryInCurrentContext(zone))
				return Refused("gallery authority cannot host production interior evidence");

			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			KingdomHostedArcologyAuthority authority;
			bool missing;
			string failure;
			if (!KingdomHostedArcology.TryReadAuthorityIdentityForJointView(system,
				out authority, out missing, out failure)) return Refused(failure);
			if (missing || authority == null)
				return Refused("the current realm has no hosted-shell authority");
			if (authority.Phase != KingdomHostedAuthorityPhase.Active)
				return Refused("the current hosted-shell authority is not active");

			GameObject root;
			if (!TryLoadedRoot(player, authority, out root, out failure)) return Refused(failure);
			if (KingdomScenarioGallerySlice.CarriesGalleryAuthority(root))
				return Refused("gallery authority cannot host production interior evidence");
			r_KingdomArcology hosted = root.GetPart<r_KingdomArcology>();
			if (hosted == null || !string.IsNullOrEmpty(hosted.QuarantineReason)
				|| KingdomUpgrade.DesignKeyOf(root) != KingdomHostedArcology.ArcologyKey)
				return Refused("the loaded carrier is not an operational production arcology");
			string authorityWire;
			if (!TryExactAuthority(system, root, authority, out authorityWire, out failure))
				return Refused(failure);
			if (!KingdomCrown.CrownedOn(system, root.CurrentZone.ZoneID))
				return Refused("the loaded shell is not the current crowned capital");

			string receiptWire;
			if (!TryPaidReceipt(hosted, lotKey, out receiptWire, out failure))
				return Refused(failure);
			Interior interior = root.GetPart<Interior>();
			string targetZoneId;
			if (!TryTargetZoneId(interior, root, x, y, z, out targetZoneId, out failure))
				return Refused(failure);

			if (!(zone is InteriorZone)
				&& !interior.CanEnter(player, Action: true, ShowMessage: false))
				return Refused("vanilla refused entry into the exact production shell");

			InteriorZone target;
			try { target = The.ZoneManager.GetZone(targetZoneId) as InteriorZone; }
			catch (Exception exception)
			{
				return Refused("vanilla could not realize the target zone: "
					+ KingdomScenarioRules.Bounded(exception.Message));
			}
			r_KingdomArcologyZoneAnchor anchor;
			if (!TryExactZone(target, root, x, y, z, programme, lotKey, out anchor, out failure))
				return Refused(failure);
			if (!string.IsNullOrEmpty(lotKey) && !anchor.FixturesRealized)
				return Refused("the active paid floor did not realize its exact fixture manifest");
			Cell destination = target.GetPullDownLocation(player);
			if (!SafeDestination(destination, player))
				return Refused("the authored target arrival cell is no longer safely walkable");
			string readyReceipt;
			if (!TryPaidReceipt(hosted, lotKey, out readyReceipt, out failure)
				|| !string.Equals(receiptWire, readyReceipt, StringComparison.Ordinal))
				return Refused(failure ?? "the hosted-lot receipt changed while realizing the target");
			string readyAuthority;
			if (!TryExactAuthority(system, root, authority, out readyAuthority, out failure)
				|| !string.Equals(authorityWire, readyAuthority, StringComparison.Ordinal)
				|| !KingdomCrown.CrownedOn(system, root.CurrentZone.ZoneID))
				return Refused(failure ?? "the shell authority changed while realizing the target");
			if (!ReferenceEquals(player.CurrentZone, originZone)
				|| !ReferenceEquals(player.CurrentCell, originCell))
				return Refused("the tester moved while the target evidence was being proved");
			if (!ReferenceEquals(player.CurrentCell, destination)
				&& !player.SystemLongDistanceMoveTo(destination, 0,
					forced: false, ignoreCombat: false))
				return Refused("the engine declined the non-forced move to the target zone");

			if (!ReferenceEquals(player.CurrentZone, target)
				|| !ReferenceEquals(player.CurrentCell, destination)
				|| !ReferenceEquals(KingdomHostedArcology.RootOf(player.CurrentZone), root)
				|| !TryExactZone(target, root, x, y, z, programme, lotKey,
					out anchor, out failure))
				return Refused(failure ?? "the target zone changed during navigation");
			if (!string.IsNullOrEmpty(lotKey) && !anchor.FixturesRealized)
				return Refused("the active paid floor did not realize its exact fixture manifest");
			string afterReceipt;
			if (!TryPaidReceipt(hosted, lotKey, out afterReceipt, out failure)
				|| !string.Equals(receiptWire, afterReceipt, StringComparison.Ordinal))
				return Refused(failure ?? "the hosted-lot receipt changed during navigation");
			string afterAuthority;
			if (!TryExactAuthority(system, root, authority, out afterAuthority, out failure)
				|| !string.Equals(authorityWire, afterAuthority, StringComparison.Ordinal))
				return Refused(failure ?? "the shell authority changed during navigation");

			Ok = true;
			return "Entered the production hosted arcology's "
				+ KingdomHostedArcologyTopology.ProgrammeName(programme) + " at "
				+ x + "," + y + "," + z
				+ "; shell authority and paid receipts remained exact.";
		}

		private static bool TryTarget(string Token, out int X, out int Y, out int Z,
			out KingdomArcologyProgramme Programme, out string LotKey)
		{
			X = Y = Z = -1; Programme = 0; LotKey = "";
			switch (Token)
			{
				case "entry": X = 1; Y = 1; Z = 10;
					Programme = KingdomArcologyProgramme.InheritedCourt; break;
				case "teaching": X = 1; Y = 0; Z = 10;
					Programme = KingdomArcologyProgramme.TeachingHall; break;
				case "terrace": X = 1; Y = 1; Z = 9;
					Programme = KingdomArcologyProgramme.HydroponicTerrace;
					LotKey = KingdomHostedArcologyTopology.TerraceLotKey; break;
				case "ward": X = 0; Y = 1; Z = 11;
					Programme = KingdomArcologyProgramme.LodgingWard;
					LotKey = KingdomHostedArcologyTopology.WardLotKey; break;
				default: return false;
			}
			return KingdomHostedArcologyTopology.ProgrammeAt(X, Y, Z) == Programme
				&& KingdomHostedArcologyTopology.HostedLotAt(X, Y, Z) == LotKey;
		}

		private static bool GalleryInCurrentContext(Zone Zone)
		{
			GameObject host = KingdomHostedArcology.RootOf(Zone);
			if (KingdomScenarioGallerySlice.CarriesGalleryAuthority(host)) return true;
			if (Zone is InteriorZone) return false;
			foreach (GameObject item in Zone.GetObjects())
				if (item.GetPart<r_KingdomArcology>() != null
					&& KingdomScenarioGallerySlice.CarriesGalleryAuthority(item)) return true;
			return false;
		}

		private static bool TryLoadedRoot(GameObject Player,
			KingdomHostedArcologyAuthority Authority, out GameObject Root, out string Failure)
		{
			Root = KingdomHostedArcology.RootOf(Player.CurrentZone); Failure = null;
			if (Root != null)
				return Root.IDIfAssigned == Authority.CarrierId || Fail(
					"this interior belongs to another hosted-shell carrier", out Failure);
			int count = 0;
			foreach (GameObject item in Player.CurrentZone.GetObjects())
				if (item.IDIfAssigned == Authority.CarrierId
					&& item.GetPart<r_KingdomArcology>() != null) { Root = item; count++; }
			return count == 1 || Fail(
				"stand in the exact loaded capital zone or inside its hosted shell", out Failure);
		}

		private static bool TryExactAuthority(KingdomSystem System, GameObject Root,
			KingdomHostedArcologyAuthority Expected, out string Wire, out string Failure)
		{
			Wire = null; Failure = null;
			KingdomHostedArcologyAuthority proved;
			string report;
			bool missing;
			if (!KingdomHostedArcology.TryReadAuthorityForJointView(System, Root, out proved,
				out report, out missing, out Failure) || missing || proved == null
				|| proved.Phase != KingdomHostedAuthorityPhase.Active
				|| proved.RealmId != System.RealmId
				|| proved.SettlementId != System.SettlementIdForOwnedZone(proved.ZoneId)
				|| proved.CarrierId != Root.IDIfAssigned || proved.ZoneId != Root.CurrentZone.ZoneID)
				return Fail(Failure ?? "the exact active shell authority is unavailable", out Failure);
			Wire = KingdomHostedArcologyReceiptCodec.EncodeAuthority(proved);
			return !string.IsNullOrEmpty(Wire)
				&& Wire == KingdomHostedArcologyReceiptCodec.EncodeAuthority(Expected)
				|| Fail("the loaded shell differs from the current realm authority", out Failure);
		}

		private static bool TryPaidReceipt(r_KingdomArcology Root, string LotKey,
			out string Wire, out string Failure)
		{
			Wire = ""; Failure = null;
			if (string.IsNullOrEmpty(LotKey)) return true;
			KingdomHostedLotReceipt receipt;
			if (!KingdomHostedArcology.TryReceipt(Root, LotKey, out receipt, out Failure)
				|| receipt == null || receipt.Phase != KingdomHostedLotPhase.Active)
				return Fail(Failure ?? "the requested paid floor is not active", out Failure);
			Wire = KingdomHostedArcologyReceiptCodec.EncodeLot(receipt);
			return !string.IsNullOrEmpty(Wire)
				|| Fail("the requested paid-floor receipt is malformed", out Failure);
		}

		private static bool TryTargetZoneId(Interior Part, GameObject Root,
			int X, int Y, int Z, out string Target, out string Failure)
		{
			Target = null; Failure = null;
			return Part != null && Part.ParentObject == Root
				&& KingdomHostedArcology.TryNativeInteriorTarget(
					Root, X, Y, Z, out Target, out Failure)
				|| Fail(Failure ?? "the production shell has no exact native interior declaration",
					out Failure);
		}

		private static bool TryExactZone(InteriorZone Zone, GameObject Root,
			int X, int Y, int Z, KingdomArcologyProgramme Programme, string LotKey,
			out r_KingdomArcologyZoneAnchor Anchor, out string Failure)
		{
			Anchor = null; Failure = null;
			if (Zone == null || Zone.Schema != KingdomHostedArcologyTopology.Schema
				|| Zone.Instance != Root.IDIfAssigned || Zone.X != X || Zone.Y != Y || Zone.Z != Z
				|| !ReferenceEquals(KingdomHostedArcology.RootOf(Zone), Root)
				|| Zone.GetZoneProperty("TAFArcologyProgramme", null) != Programme.ToString()
				|| Zone.BaseDisplayName != KingdomHostedArcologyTopology.ProgrammeName(Programme))
				return Fail("the realized zone does not match its exact topology programme", out Failure);
			string id = KingdomHostedArcologyRules.StableChildId(Root.IDIfAssigned,
				KingdomHostedArcologyTopology.StableRole(X, Y, Z, "anchor"));
			GameObject found = null;
			int count = 0;
			foreach (GameObject item in Zone.GetObjects())
				if (item.IDIfAssigned == id) { found = item; count++; }
			Anchor = found?.GetPart<r_KingdomArcologyZoneAnchor>();
			return count == 1 && found.Blueprint == "r_KingdomArcologyZoneAnchor"
				&& found.CurrentCell == Zone.GetCell(40, 3) && Anchor != null
				&& Anchor.ZoneX == X && Anchor.ZoneY == Y && Anchor.ZoneZ == Z
				&& (Anchor.LotKey ?? "") == LotKey
				|| Fail("the realized zone has no exact stable authority anchor", out Failure);
		}

		private static bool SafeDestination(Cell Cell, GameObject Player)
		{
			if (Cell == null) return false;
			if (ReferenceEquals(Cell, Player.CurrentCell)) return true;
			if (Cell.HasOpenLiquidVolume() || !Cell.IsEmptyOfSolid()
				|| !Cell.IsPassable(Player, false)) return false;
			foreach (GameObject item in Cell.GetObjects())
				if (!ReferenceEquals(item, Player) && item.IsCreature) return false;
			return true;
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}

		private static string Refused(string Message)
		{
			return "{{R|Arcology navigation refused}}: "
				+ KingdomScenarioRules.Bounded(Message ?? "unknown refusal");
		}
	}
}
