using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Anatomy;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		// The coordinator proves the owned boot and successful original Run; this bridge only reads.
		internal static bool NativeVerifyFreshBoot(XRLGame Game, GameObject Founder, Zone Zone,
			KingdomQuickstartProfile Profile, bool Advisor, out string Failure)
		{
			Failure = "Native fresh boot authority did not match the captured owner.";
			try
			{
				if (Game == null || Founder == null || Zone == null || Profile == null
					|| Game.StringGameState == null || Game.IntGameState == null
					|| Game.Int64GameState == null || Game.ObjectGameState == null
					|| Game.BooleanGameState == null) return false;
				NativeBootOwner owner = new NativeBootOwner(Game, Founder, Zone, Profile);
				if (!owner.Current()) return false;
				string raw = Game.GetStringGameState(KingdomQuickstartRules.ReceiptState, null);
				if (!NativeBootString(Game, KingdomQuickstartRules.ReceiptState, raw)
					|| !KingdomQuickstartRules.TryDecode(raw, out KingdomQuickstartReceipt receipt)
					|| receipt.Phase != KingdomQuickstartPhase.Complete
					|| KingdomQuickstartRules.Encode(receipt) != raw
					|| receipt.ProfileKey != Profile.Key || receipt.ZoneId != Profile.ZoneId
					|| receipt.AdvisorDisposition != (Advisor ? KingdomQuickstartAdvisorDisposition.Included
						: KingdomQuickstartAdvisorDisposition.Omitted))
				{
					Failure = "Native boot lacks its canonical Complete receipt or exact advisor decision.";
					return false;
				}
				if (!NativeBootRoster(Zone, Founder, receipt, out GameObject[] grants, out Failure)) return false;
				if (!owner.Current())
				{
					Failure = "Native boot ownership changed during its first roster scan.";
					return false;
				}
				if (!VerifyComplete(owner.System, Zone, receipt, out Failure)
					|| !VerifyWaterGrant(Zone, grants[0], receipt, true, out Failure)
					|| !VerifyLarderGrant(Zone, grants[1], receipt, true, out Failure)
					|| !VerifyMaterialsGrant(Zone, grants[2], receipt, true, out Failure)) return false;
				if (!NativeBootRoster(Zone, Founder, receipt, out GameObject[] after, out Failure)) return false;
				for (int i = 0; i < grants.Length; i++)
					if (!ReferenceEquals(grants[i], after[i]))
					{
						Failure = "A native boot grant reference changed while being verified.";
						return false;
					}
				if (!owner.Current() || !NativeBootString(Game, KingdomQuickstartRules.ReceiptState, raw)
					|| KingdomQuickstartRules.Encode(receipt) != raw)
				{
					Failure = "Native boot ownership or receipt changed during verification.";
					return false;
				}
				Failure = null;
				return true;
			}
			catch (Exception ex)
			{
				Failure = "Native fresh boot verification threw " + ex.GetType().Name + ".";
				return false;
			}
		}

		private sealed class NativeBootOwner
		{
			private readonly XRLGame Game;
			private readonly GamePlayer Player;
			private readonly GameObject Founder;
			private readonly Physics Physics;
			private readonly ZoneManager Manager;
			private readonly Zone Zone;
			private readonly Cell Start;
			private readonly KingdomQuickstartProfile Profile;
			private readonly Dictionary<string, string> Strings;
			private readonly Dictionary<string, int> Ints;
			private readonly Dictionary<string, long> Longs;
			private readonly Dictionary<string, object> Objects;
			private readonly Dictionary<string, bool> Booleans;
			private readonly string HeartReceipt, HeartSeal, HeartTerminal;
			private readonly long Turns, TimeTicks, ActionTicks, PlayerActionTicks;
			internal readonly KingdomSystem System;

			internal NativeBootOwner(XRLGame game, GameObject founder, Zone zone, KingdomQuickstartProfile profile)
			{
				Game = game; Player = game.Player; Founder = founder; Physics = founder.Physics;
				Manager = game.ZoneManager; Zone = zone; Profile = profile;
				Start = zone.GetCell(KingdomQuickstartRules.StartCellX, KingdomQuickstartRules.StartCellY);
				System = game.GetSystem<KingdomSystem>();
				Strings = game.StringGameState; Ints = game.IntGameState; Longs = game.Int64GameState;
				Objects = game.ObjectGameState; Booleans = game.BooleanGameState;
				HeartReceipt = zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null);
				HeartSeal = zone.GetZoneProperty(KingdomPlots.FoundingHeartSealProperty, null);
				HeartTerminal = zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null);
				Turns = game.Turns; TimeTicks = game.TimeTicks;
				ActionTicks = game.ActionTicks; PlayerActionTicks = game.PlayerActionTicks;
			}

			internal bool Current()
			{
				return ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.Player, Player)
					&& Player != null && ReferenceEquals(Player._Body, Founder) && ReferenceEquals(The.Player, Founder)
					&& ReferenceEquals(Game.ZoneManager, Manager) && ReferenceEquals(The.ZoneManager, Manager)
					&& Manager != null && ReferenceEquals(Manager.ActiveZone, Zone)
					&& Zone.Width == 80 && Zone.Height == 25 && Zone.ZoneID == Profile.ZoneId
					&& Start != null && ReferenceEquals(Start.ParentZone, Zone)
					&& ReferenceEquals(Zone.GetCell(KingdomQuickstartRules.StartCellX, KingdomQuickstartRules.StartCellY), Start)
					&& GameObject.Validate(Founder) && Physics != null && ReferenceEquals(Founder.Physics, Physics)
					&& ReferenceEquals(Physics._ParentObject, Founder) && ReferenceEquals(Physics._CurrentCell, Start)
					&& Physics._InInventory == null && Physics._Equipped == null
					&& System != null && System.Founded && ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
					&& System.SeatName == Profile.CityName && System.KingdomDisplayName == Profile.CityName
					&& System.ClaimedZones != null && System.ClaimedZones.Contains(Profile.ZoneId)
					&& System.SettlementIdentityFirstClaimedZone == Profile.ZoneId
					&& ReferenceEquals(Game.StringGameState, Strings) && NativeBootOrdinal(Strings)
					&& ReferenceEquals(Game.IntGameState, Ints) && NativeBootOrdinal(Ints)
					&& ReferenceEquals(Game.Int64GameState, Longs) && NativeBootOrdinal(Longs)
					&& ReferenceEquals(Game.ObjectGameState, Objects) && NativeBootOrdinal(Objects)
					&& ReferenceEquals(Game.BooleanGameState, Booleans) && NativeBootOrdinal(Booleans)
					&& KingdomQuickstartRules.TryProfile(Profile.Key, out KingdomQuickstartProfile canonical)
					&& ReferenceEquals(canonical, Profile)
					&& NativeBootString(Game, "GameMode", KingdomQuickstartRules.ModeId)
					&& NativeBootString(Game, KingdomQuickstartRules.ProfileState, Profile.Key)
					&& NativeBootString(Game, KingdomQuickstartRules.WorldReservationState,
						KingdomQuickstartRules.WorldReservation(Profile))
					&& Booleans.TryGetValue("r_TAF_KingdomMode", out bool kingdom) && kingdom
					&& !Strings.ContainsKey("r_TAF_KingdomMode") && !Ints.ContainsKey("r_TAF_KingdomMode")
					&& !Longs.ContainsKey("r_TAF_KingdomMode") && !Objects.ContainsKey("r_TAF_KingdomMode")
					&& !GrantQuarantined(Game) && KingdomMaster.ConfiguredEnabled
					&& Game.Turns == Turns && Game.TimeTicks == TimeTicks
					&& Game.ActionTicks == ActionTicks && Game.PlayerActionTicks == PlayerActionTicks
					&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null) == HeartReceipt
					&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartSealProperty, null) == HeartSeal
					&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartTerminalProperty, null) == HeartTerminal;
			}
		}

		// Count occurrences, not just distinct references: duplicated custody is not a second proof.
		private static bool NativeBootRoster(Zone Zone, GameObject Founder, KingdomQuickstartReceipt Receipt,
			out GameObject[] Grants, out string Failure)
		{
			const int maximum = 65536;
			Grants = null;
			Failure = "Native boot grant IDs, markers, or loaded-zone custody were not exact and unique.";
			if (Zone.Width != 80 || Zone.Height != 25) return false;
			string[] ids = { Receipt.WaterObjectId, Receipt.LarderObjectId, Receipt.StockpileObjectId, Receipt.AdvisorObjectId };
			string[] markers = new string[4]; int[] counts = new int[4]; GameObject[] found = new GameObject[4];
			bool advisor = Receipt.AdvisorDisposition == KingdomQuickstartAdvisorDisposition.Included;
			HashSet<string> distinct = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < 4; i++)
			{
				markers[i] = KingdomQuickstartRules.GrantMarker(Receipt, (KingdomQuickstartPhase)(i + 2));
				if (string.IsNullOrEmpty(markers[i]) || (i < 3 || advisor) && (string.IsNullOrEmpty(ids[i]) || !distinct.Add(ids[i]))) return false;
			}
			Stack<GameObject> pending = new Stack<GameObject>();
			Dictionary<GameObject, Cell> roots = new Dictionary<GameObject, Cell>();
			for (int y = 0; y < 25; y++)
				for (int x = 0; x < 80; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (cell == null || !ReferenceEquals(cell.ParentZone, Zone) || cell.Objects.Count > maximum - pending.Count) return false;
					for (int i = 0; i < cell.Objects.Count; i++)
					{
						GameObject item = cell.Objects[i]; pending.Push(item);
						if (item != null && !roots.ContainsKey(item)) roots.Add(item, cell);
					}
				}
			HashSet<GameObject> expanded = new HashSet<GameObject>(); int visited = 0, founders = 0;
			while (pending.Count > 0)
			{
				if (++visited > maximum) return false;
				GameObject item = pending.Pop();
				if (item == null) continue;
				if (ReferenceEquals(item, Founder) && (++founders != 1 || !roots.TryGetValue(item, out Cell founderCell)
					|| Founder.Physics == null || !ReferenceEquals(founderCell, Founder.Physics._CurrentCell))) return false;
				string id = item.IDIfAssigned;
				bool tagged = item.HasStringProperty(KingdomQuickstartRules.GrantMarkerProperty);
				if (item.HasIntProperty(KingdomQuickstartRules.GrantMarkerProperty)) return false;
				string marker = tagged ? item.GetStringProperty(KingdomQuickstartRules.GrantMarkerProperty) : null;
				int role = -1, idRole = -1;
				for (int i = 0; i < 4; i++)
				{
					if (tagged && marker == markers[i]) role = i;
					if (!string.IsNullOrEmpty(ids[i]) && id == ids[i]) idRole = i;
				}
				if (tagged || idRole >= 0)
				{
					if (role < 0 || role != idRole || role == 3 && !advisor || ++counts[role] != 1
						|| ReferenceEquals(item, Founder) || !GameObject.Validate(item) || item.Physics == null
						|| !ReferenceEquals(item.Physics._ParentObject, item) || item.Physics._InInventory != null
						|| item.Physics._Equipped != null || item.Physics._CurrentCell == null
						|| !ReferenceEquals(item.Physics._CurrentCell.ParentZone, Zone)
						|| !roots.TryGetValue(item, out Cell placed) || !ReferenceEquals(placed, item.Physics._CurrentCell)) return false;
					found[role] = item;
				}
				if (!expanded.Add(item)) continue;
				if (!NativeBootChildren(item, pending, ref visited, maximum)) return false;
			}
			if (Zone.Width != 80 || Zone.Height != 25 || founders != 1 || counts[0] != 1 || counts[1] != 1
				|| counts[2] != 1 || counts[3] != (advisor ? 1 : 0)) return false;
			Grants = found; Failure = null;
			return true;
		}

		// Inventory.GetObjectsDirect flushes caches; traverse retained lists and anatomy fields instead.
		private static bool NativeBootChildren(GameObject Item, Stack<GameObject> Pending, ref int Visited, int Maximum)
		{
			if (Item.Inventory != null)
			{
				List<GameObject> inventory = Item.Inventory.Objects;
				if (inventory == null || inventory.Count > Maximum - Visited - Pending.Count) return false;
				for (int i = 0; i < inventory.Count; i++) Pending.Push(inventory[i]);
			}
			if (Item.Body == null) return true;
			if (Item.Body._Body == null) return false;
			Stack<BodyPart> parts = new Stack<BodyPart>(); parts.Push(Item.Body._Body);
			HashSet<BodyPart> expanded = new HashSet<BodyPart>();
			HashSet<GameObject> equipped = new HashSet<GameObject>();
			while (parts.Count > 0)
			{
				if (++Visited > Maximum) return false;
				BodyPart part = parts.Pop();
				if (part == null) continue;
				if (!expanded.Add(part)) return false;
				foreach (GameObject held in new[] { part._Equipped, part._Cybernetics })
					if (held != null && equipped.Add(held))
					{
						if (Pending.Count >= Maximum - Visited) return false;
						Pending.Push(held);
					}
				if (part.Parts == null) continue;
				if (part.Parts.Count > Maximum - Visited - Pending.Count - parts.Count) return false;
				for (int i = 0; i < part.Parts.Count; i++) parts.Push(part.Parts[i]);
			}
			return true;
		}

		private static bool NativeBootString(XRLGame Game, string Key, string Expected)
		{
			return Expected != null && Game.StringGameState.TryGetValue(Key, out string value) && value == Expected
				&& !Game.IntGameState.ContainsKey(Key) && !Game.Int64GameState.ContainsKey(Key)
				&& !Game.ObjectGameState.ContainsKey(Key) && !Game.BooleanGameState.ContainsKey(Key);
		}

		private static bool NativeBootOrdinal<T>(Dictionary<string, T> Table)
		{
			return Table != null && (ReferenceEquals(Table.Comparer, EqualityComparer<string>.Default)
				|| ReferenceEquals(Table.Comparer, StringComparer.Ordinal));
		}
	}
}
