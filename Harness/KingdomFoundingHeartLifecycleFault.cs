using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Retained native callback evidence for one real founding or advancement attempt.
	/// A failed proof leaves objects and injected state untouched; this helper never cleans objects.</summary>
	internal sealed class KingdomFoundingHeartLifecycleFault
	{
		private static readonly List<KingdomFoundingHeartLifecycleFault> Retained =
			new List<KingdomFoundingHeartLifecycleFault>();
		private readonly List<GameObject> Bodies = new List<GameObject>();
		private readonly XRLGame Game;
		private readonly Zone Zone;
		private readonly string ZoneId;
		private readonly string Blueprint;
		private readonly bool Typed;
		private readonly Dictionary<string, string> Strings;
		private readonly Dictionary<string, int> Ints;
		private readonly Dictionary<string, long> Longs;
		private readonly Dictionary<string, object> Objects;
		private readonly Dictionary<string, bool> Booleans;
		private readonly Action<GameObject, BeforeObjectCreatedEvent> Callback;
		private readonly string[] Keys = new string[7];
		private readonly string[] Wires = new string[7];
		private GameObject Foreign;
		private BodyState ForeignBefore;
		private BodyState OriginalBefore;
		private string PlanWire;
		private int Targets;
		private bool Armed;
		private bool Injected;
		private bool Retired;
		private bool Failed;
		internal GameObject Observed { get { return OriginalBefore == null ? null : OriginalBefore.Body; } }

		internal KingdomFoundingHeartLifecycleFault(XRLGame game, Zone zone, string blueprint, bool typed)
		{
			Retained.Add(this);
			Game = game; Zone = zone; ZoneId = zone == null ? null : zone.ZoneID;
			Blueprint = blueprint; Typed = typed; Callback = OnMint;
			Strings = game == null ? null : game.StringGameState;
			Ints = game == null ? null : game.IntGameState;
			Longs = game == null ? null : game.Int64GameState;
			Objects = game == null ? null : game.ObjectGameState;
			Booleans = game == null ? null : game.BooleanGameState;
			Guard(() => {
				Disarmed(); Tables();
				Check(blueprint == "r_KingdomFirstBasin" || blueprint == "r_KingdomHeartStake"
					|| blueprint == "r_KingdomPlotWorks" || blueprint == "r_KingdomRiteGround",
					"lifecycle fault blueprint is not instrumented");
				if (typed) return;
				GameObject captured = null;
				int count = 0;
				Foreign = GameObject.Create(blueprint, BeforeObjectCreated: body => {
					Bodies.Add(body); captured = body; count++;
					Guard(() => {
						Disarmed(); Tables();
						Check(count == 1, "foreign factory produced multiple original references");
					});
				});
				Disarmed(); Tables();
				Check(count == 1 && ReferenceEquals(Foreign, captured), "foreign factory replaced its captured original");
				ForeignBefore = new BodyState(Foreign, Blueprint);
			});
		}

		internal void Arm()
		{
			Guard(() => {
				Disarmed(); Tables();
				Check(!Armed && Targets == 0 && !Injected && !Retired, "lifecycle fault cannot be rearmed");
				if (!Typed) ForeignBefore.Exact();
				Armed = true;
				r_TAF_FoundingHeartMintProbe.Callback = Callback;
			});
		}

		private void OnMint(GameObject body, BeforeObjectCreatedEvent creation)
		{
			Guard(() => {
				if (body != null && body.Blueprint != Blueprint) return;
				Bodies.Add(body); Targets++;
				Check(Targets == 1 && Armed && ReferenceEquals(r_TAF_FoundingHeartMintProbe.Callback, Callback)
					&& creation != null && ReferenceEquals(creation.Object, body)
					&& creation.ReplacementObject == null, "targeted lifecycle callback is not one exact original");
				Tables();
				OriginalBefore = new BodyState(body, Blueprint);
				PlanWire = Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null);
				Check(KingdomFoundingHeartRules.TryDecode(PlanWire, out KingdomFoundingHeartPlan plan)
					&& KingdomFoundingHeartRules.Valid(plan) && plan.ZoneId == ZoneId
					&& KingdomFoundingHeartRules.Encode(plan) == PlanWire, "target callback lacks an exact zone heart");
				Check(KingdomFoundingHeartRules.SlotCount + 1 == Keys.Length, "heart reservation cardinality changed");
				for (int slot = 0; slot < Keys.Length; slot++)
				{
					string role = slot == KingdomFoundingHeartRules.SlotCount ? "final"
						: "slot-" + slot.ToString(CultureInfo.InvariantCulture);
					string id = KingdomFoundingHeartRules.StableId(plan.TransactionId, plan.ZoneId, role);
					Keys[slot] = KingdomPlots.FoundingHeartReservationPrefix + id;
					Wires[slot] = KingdomFoundingHeartReservationRules.Encode(plan, id, role);
				}
				Reservations(false); OriginalBefore.Exact();
				if (Typed)
				{
					Ints.Add(Keys[0], 701);
					Injected = true;
					Reservations(true);
				}
				else
				{
					ForeignBefore.Exact();
					Check(!ReferenceEquals(body, Foreign), "foreign replacement is the original allocation");
					creation.ReplacementObject = Foreign;
					Injected = true;
				}
			});
		}

		internal void VerifyRefusal()
		{
			Guard(() => {
				Disarmed();
				Check(Armed && Targets == 1 && Injected && !Retired && OriginalBefore != null,
					"targeted lifecycle fault did not run exactly once");
				Reservations(Typed); OriginalBefore.Exact();
				if (!Typed) ForeignBefore.Exact();
			});
		}

		internal void RetireInjection()
		{
			Guard(() => {
				Check(Typed, "foreign replacement evidence cannot be retired");
				VerifyRefusal();
				Check(Ints.Remove(Keys[0]), "exact owned integer injection could not be removed");
				Reservations(false); OriginalBefore.Exact(); Disarmed();
				Retired = true;
			});
		}

		private void Reservations(bool injected)
		{
			Tables();
			Check(PlanWire != null && Zone.GetZoneProperty(KingdomPlots.FoundingHeartReceiptProperty, null) == PlanWire,
				"target callback plan changed; retained evidence cannot be retired");
			for (int slot = 0; slot < Keys.Length; slot++)
			{
				Check(Wires[slot] != null && Strings.TryGetValue(Keys[slot], out string wire) && wire == Wires[slot],
					"canonical reservation string changed");
				if (injected && slot == 0)
					Check(Ints.TryGetValue(Keys[slot], out int value) && value == 701
						&& !Longs.ContainsKey(Keys[slot]) && !Objects.ContainsKey(Keys[slot])
						&& !Booleans.ContainsKey(Keys[slot]), "owned typed injection changed");
				else Check(KingdomScenarioDurableState.ProvesExactText(Keys[slot], Wires[slot]),
					"seven exact reservation authorities do not stand");
			}
			Tables();
		}

		private void Tables()
		{
			Check(Game != null && ReferenceEquals(The.Game, Game) && Zone != null && Zone.ZoneID == ZoneId
				&& !string.IsNullOrEmpty(ZoneId) && The.ZoneManager != null
				&& ReferenceEquals(The.ZoneManager.ActiveZone, Zone)
				&& Strings != null && ReferenceEquals(Game.StringGameState, Strings)
				&& Ints != null && ReferenceEquals(Game.IntGameState, Ints)
				&& Longs != null && ReferenceEquals(Game.Int64GameState, Longs)
				&& Objects != null && ReferenceEquals(Game.ObjectGameState, Objects)
				&& Booleans != null && ReferenceEquals(Game.BooleanGameState, Booleans)
				&& r_TAF_FoundingHeartMintProbe.Error == null, "game, zone, typed tables or probe evidence changed");
		}

		private static void Disarmed()
		{
			Check(r_TAF_FoundingHeartMintProbe.Callback == null && r_TAF_FoundingHeartMintProbe.Error == null,
				"lifecycle fault requires a disarmed, error-free probe");
		}

		private void Guard(Action action)
		{
			Check(!Failed, "prior lifecycle fault proof failed; unknown evidence retained");
			try { action(); }
			catch { Failed = true; throw; }
		}

		private sealed class BodyState
		{
			internal readonly GameObject Body;
			private readonly string Blueprint;
			private readonly string Id;
			private readonly Physics Physics;
			private readonly Dictionary<string, string> Strings;
			private readonly Dictionary<string, int> Ints;
			private readonly Dictionary<string, string> StringRows;
			private readonly Dictionary<string, int> IntRows;

			internal BodyState(GameObject body, string blueprint)
			{
				Unplaced(body, blueprint);
				Body = body; Blueprint = blueprint; Id = body.IDIfAssigned; Physics = body.Physics;
				Strings = body.Property; Ints = body.IntProperty;
				StringRows = Strings == null ? null : new Dictionary<string, string>(Strings);
				IntRows = Ints == null ? null : new Dictionary<string, int>(Ints);
				Exact();
			}

			internal void Exact()
			{
				Unplaced(Body, Blueprint);
				Check(Body.IDIfAssigned == Id && ReferenceEquals(Body.Physics, Physics)
					&& ReferenceEquals(Body.Property, Strings) && ReferenceEquals(Body.IntProperty, Ints),
					"retained allocation identity or property dictionaries changed");
				Rows(StringRows, Strings); Rows(IntRows, Ints);
			}

			private static void Rows<T>(Dictionary<string, T> before, Dictionary<string, T> after)
			{
				Check(before == null ? after == null : after != null && before.Count == after.Count,
					"retained allocation property shape changed");
				if (before == null) return;
				foreach (KeyValuePair<string, T> row in before)
					Check(after.TryGetValue(row.Key, out T value) && EqualityComparer<T>.Default.Equals(row.Value, value),
						"retained allocation property changed: " + row.Key);
			}

			private static void Unplaced(GameObject body, string blueprint)
			{
				Check(GameObject.Validate(body) && body.Blueprint == blueprint && body.CurrentCell == null
					&& body.CurrentZone == null && body.InInventory == null && body.Equipped == null
					&& !(body.IDIfAssigned ?? "").StartsWith("taf-heart-v1-", StringComparison.Ordinal)
					&& !body.HasStringProperty(KingdomPlots.FoundingHeartOwnerProperty)
					&& !body.HasIntProperty(KingdomPlots.FoundingHeartOwnerProperty)
					&& !body.HasStringProperty(KingdomPlots.FoundingHeartSlotProperty)
					&& !body.HasIntProperty(KingdomPlots.FoundingHeartSlotProperty),
					"retained allocation acquired heart identity, invalidity or custody");
			}
		}

		private static void Check(bool condition, string failure)
		{
			KingdomFoundingHeartAllocationNativeCases.Check(condition, failure);
		}
	}
}
