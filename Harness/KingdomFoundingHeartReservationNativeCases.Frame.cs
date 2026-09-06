using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomFoundingHeartReservationNativeCases
	{
		private sealed class Frame
		{
			internal readonly string[] Canonical = new string[7];
			private readonly string[] Keys = new string[7], Texts = new string[7];
			private readonly int[] Masks = new int[7];
			private readonly object[] Values = { null, 0, 0L, new object(), false };
			private readonly IDictionary[] Tables;
			private readonly Dictionary<string, object>[] Outside = new Dictionary<string, object>[5];
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly KingdomSystem System;
			private readonly KingdomFoundingHeartPlan Plan;
			private readonly KingdomSubsidenceNativeFixture Fixture;
			private readonly GameObject Player;
			private readonly Cell PlayerCell;
			private readonly object Manager, City, Ledger, Bindings, Callback;
			private readonly string GameId, Realm, Settlement, Wire, Seal;
			private readonly long Tick;
			private readonly int Mints;
			private readonly List<GameObject> Bodies;
			private readonly MethodInfo EnsureMethod;

			internal Frame(XRLGame game, Zone zone, KingdomFoundingHeartPlan plan)
			{
				Game = game; Zone = zone; Plan = plan; Fixture = KingdomSubsidenceNativeFixture.LastAttempt;
				Require(game != null && zone != null && Fixture != null && ReferenceEquals(Fixture.Game, game)
					&& ReferenceEquals(Fixture.Zone, zone) && KingdomFoundingHeartRules.Complete(plan),
					"retained owned native fixture and completed real heart required");
				System = game.GetSystem<KingdomSystem>(); Player = The.Player; PlayerCell = Player?.CurrentCell;
				Require(System != null && ReferenceEquals(System, Fixture.System) && GameObject.Validate(Player), "world owner absent");
				Manager = game.ZoneManager; City = System.City; Ledger = System.PolityLedger; Bindings = System.Bindings;
				GameId = game.GameID; Realm = System.CurrentRealmId; Settlement = System.CurrentSettlementId; Tick = game.TimeTicks;
				Wire = KingdomFoundingHeartRules.Encode(plan); Seal = KingdomFoundingHeartRules.CompletionSeal(plan);
				Callback = r_TAF_FoundingHeartMintProbe.Callback; Mints = r_TAF_FoundingHeartMintProbe.Count;
				Require(Callback != null && r_TAF_FoundingHeartMintProbe.Error == null, "armed clean native mint probe required");
				Tables = new IDictionary[] { game.StringGameState, game.IntGameState, game.Int64GameState,
					game.ObjectGameState, game.BooleanGameState };
				Require(Ordinal(game.StringGameState) && Ordinal(game.IntGameState) && Ordinal(game.Int64GameState)
					&& Ordinal(game.ObjectGameState) && Ordinal(game.BooleanGameState), "exact ordinal durable tables required");
				HashSet<string> owned = new HashSet<string>(StringComparer.Ordinal);
				for (int slot = 0; slot < 7; slot++)
				{
					string role = slot == 6 ? "final" : "slot-" + slot;
					string id = KingdomFoundingHeartRules.StableId(plan.TransactionId, plan.ZoneId, role);
					Keys[slot] = KingdomFoundingHeartReservationRules.Prefix + id;
					Canonical[slot] = Texts[slot] = KingdomFoundingHeartReservationRules.Encode(plan, id, role);
					Masks[slot] = 1;
					Require(owned.Add(Keys[slot]) && KingdomFoundingHeartReservationRules.TryRead(Keys[slot], Canonical[slot],
						out string transaction, out string ownerZone, out string readId)
						&& transaction == plan.TransactionId && ownerZone == zone.ZoneID && readId == id, "canonical reservation refused");
				}
				for (int table = 0; table < 5; table++)
				{
					Outside[table] = new Dictionary<string, object>(StringComparer.Ordinal);
					foreach (DictionaryEntry row in Tables[table])
						if (!owned.Contains((string)row.Key)) Outside[table].Add((string)row.Key, row.Value);
				}
				Bodies = new List<GameObject>(zone.GetObjects()); EnsureMethod = ExactEnsure();
				Check();
			}

			internal void Check()
			{
				Require(ReferenceEquals(The.Game, Game) && Game.GameID == GameId && Game.TimeTicks == Tick
					&& ReferenceEquals(Game.ZoneManager, Manager) && ReferenceEquals(The.ZoneManager, Manager)
					&& ReferenceEquals(The.ZoneManager.ActiveZone, Zone) && ReferenceEquals(The.Player, Player)
					&& GameObject.Validate(Player) && ReferenceEquals(Player.CurrentZone, Zone) && ReferenceEquals(Player.CurrentCell, PlayerCell)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && ReferenceEquals(Fixture.System, System)
					&& ReferenceEquals(KingdomSubsidenceNativeFixture.LastAttempt, Fixture) && System.Founded && System.OwnedZone(Zone.ZoneID)
					&& ReferenceEquals(System.City, City) && ReferenceEquals(System.PolityLedger, Ledger) && ReferenceEquals(System.Bindings, Bindings)
					&& System.CurrentRealmId == Realm && System.CurrentSettlementId == Settlement
					&& System.SettlementIdentityFirstClaimedZone == Zone.ZoneID
					&& System.SettlementIdentityOrigin == KingdomIdentityOrigin.FoundingTransaction
					&& System.SettlementIdentityTransactionId == Plan.TransactionId && Plan.ZoneId == Zone.ZoneID
					&& Wire != null && KingdomFoundingHeartRules.Encode(Plan) == Wire
					&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null) == Wire
					&& Zone.GetZoneProperty(KingdomPlots.FoundingHeartSealProperty, null) == Seal,
					"world/heart identity changed; unknown state retained");
				Require(ReferenceEquals(Game.StringGameState, Tables[0]) && ReferenceEquals(Game.IntGameState, Tables[1])
					&& ReferenceEquals(Game.Int64GameState, Tables[2]) && ReferenceEquals(Game.ObjectGameState, Tables[3])
					&& ReferenceEquals(Game.BooleanGameState, Tables[4]), "durable table identity changed; state retained");
				Require(ReferenceEquals(r_TAF_FoundingHeartMintProbe.Callback, Callback)
					&& r_TAF_FoundingHeartMintProbe.Count == Mints && r_TAF_FoundingHeartMintProbe.Error == null,
					"factory allocation/probe changed; state retained");
				List<GameObject> current = Zone.GetObjects();
				Require(current != null && current.Count == Bodies.Count, "zone object count changed; state retained");
				for (int i = 0; i < Bodies.Count; i++)
					Require(ReferenceEquals(current[i], Bodies[i]), "zone object references changed; state retained");
				for (int table = 0; table < 5; table++)
				{
					int count = Outside[table].Count;
					foreach (KeyValuePair<string, object> row in Outside[table])
						Require(Tables[table].Contains(row.Key) && Same(table, Tables[table][row.Key], row.Value),
							"outside row changed; state retained");
					for (int slot = 0; slot < 7; slot++)
					{
						bool present = (Masks[slot] & (1 << table)) != 0;
						Require(Tables[table].Contains(Keys[slot]) == present
							&& (!present || Same(table, Tables[table][Keys[slot]], Value(table, slot))),
							"injected row changed slot=" + slot + " table=" + table + "; state retained");
						if (present) count++;
					}
					Require(Tables[table].Count == count, "unexpected durable row added; state retained");
				}
			}

			internal void Inject(int slot, int mask, string raw)
			{
				Check();
				Require(slot >= 0 && slot < 7 && mask >= 0 && mask < 32
					&& Masks[slot] == 1 && Texts[slot] == Canonical[slot], "injection target is not exact original authority");
				SetRow(slot, mask, raw);
			}

			internal bool Ensure()
			{
				Check();
				try { return (bool)EnsureMethod.Invoke(null, new object[] { Plan }); }
				catch (TargetInvocationException error)
				{ throw new InvalidOperationException("native reservation production Ensure threw", error.InnerException ?? error); }
			}

			internal void AcceptCreated(int slot)
			{
				Require(Masks[slot] == 0 && Tables[0].Contains(Keys[slot])
					&& Same(0, Tables[0][Keys[slot]], Canonical[slot]), "absent reservation was not created exactly");
				Masks[slot] = 1; Texts[slot] = Canonical[slot];
				Check();
			}

			internal void RefuseAudit()
			{
				Check();
				bool refused = !KingdomPlots.AuditFoundingHeartReservations(System, Zone);
				Check();
				Require(refused, "native audit accepted malformed reservation state");
			}

			internal void Restore()
			{
				// A failed identity/value check leaves all evidence retained, never overwritten.
				Check();
				for (int slot = 0; slot < 7; slot++) SetRow(slot, 1, Canonical[slot]);
				Check();
			}

			private void SetRow(int slot, int mask, string raw)
			{
				Check();
				for (int table = 0; table < 5; table++)
				{
					int bit = 1 << table;
					bool before = (Masks[slot] & bit) != 0, after = (mask & bit) != 0;
					object desired = table == 0 ? raw : Values[table];
					if (before == after && (!before || Same(table, Value(table, slot), desired))) continue;
					Check();
					if (before) { Tables[table].Remove(Keys[slot]); Masks[slot] &= ~bit; }
					if (table == 0) Texts[slot] = raw;
					Check();
					if (after) { Tables[table].Add(Keys[slot], desired); Masks[slot] |= bit; }
					Check();
				}
			}

			private object Value(int table, int slot) { return table == 0 ? Texts[slot] : Values[table]; }
			private static bool Same(int table, object left, object right)
			{ return table == 3 ? ReferenceEquals(left, right) : Equals(left, right); }
			private static bool Ordinal<T>(Dictionary<string, T> table)
			{ return table != null && (ReferenceEquals(table.Comparer, EqualityComparer<string>.Default)
				|| ReferenceEquals(table.Comparer, StringComparer.Ordinal)); }
		}
	}
}
