using System;
using System.Collections.Generic;
using System.Reflection;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Real engine callbacks through the shared production factory guard. Calling the
	/// private guard on a completed fixture is synthetic; this does not complete a terminal work.</summary>
	internal static class KingdomFoundingHeartAllocationNativeCases
	{
		private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Public;
		private static readonly List<GameObject> Retained = new List<GameObject>();
		private static readonly string[] Blueprints = {
			"r_KingdomFirstBasin", "r_KingdomHeartStake", "r_KingdomPlotWorks" };

		internal static void GenuineFactory(XRLGame game, Zone zone, KingdomFoundingHeartPlan plan)
		{
			foreach (string blueprint in Blueprints)
			{
				object fence = Fence(zone, plan);
				int count = r_TAF_FoundingHeartMintProbe.Count;
				GameObject observed = null;
				r_TAF_FoundingHeartMintProbe.Callback = (body, e) => observed = body;
				GameObject made;
				try { made = Create(fence, blueprint); }
				finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
				Check(GameObject.Validate(made) && ReferenceEquals(made, observed)
					&& made.Blueprint == blueprint && made.CurrentCell == null && made.InInventory == null
					&& r_TAF_FoundingHeartMintProbe.Count == count + 1 && r_TAF_FoundingHeartMintProbe.Error == null,
					"genuine factory reference was refused or changed");
				Retained.Add(made);
				Unassigned(made);
			}
		}

		internal static void TypedCallback(XRLGame game, Zone zone, KingdomFoundingHeartPlan plan)
		{
			string key = KingdomPlots.FoundingHeartReservationPrefix + KingdomFoundingHeartRules.SlotId(plan, 0);
			foreach (string blueprint in Blueprints)
			{
				object fence = Fence(zone, plan);
				Check(KingdomScenarioDurableState.ProvesExactText(key, game.StringGameState[key]), "callback key is not exact");
				string wire = game.StringGameState[key];
				Dictionary<string, int> ints = game.IntGameState;
				Dictionary<string, string> strings = game.StringGameState;
				int count = r_TAF_FoundingHeartMintProbe.Count;
				GameObject observed = null;
				string originalId = null;
				r_TAF_FoundingHeartMintProbe.Callback = (body, e) => {
					observed = body; originalId = body.IDIfAssigned;
					Check(ReferenceEquals(The.Game, game) && ReferenceEquals(game.IntGameState, ints)
						&& !ints.ContainsKey(key), "callback state changed before injection");
					ints.Add(key, 701);
				};
				GameObject made;
				try { made = Create(fence, blueprint); }
				finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
				Check(made == null && GameObject.Validate(observed) && observed.IDIfAssigned == originalId
					&& observed.CurrentCell == null && observed.InInventory == null
					&& r_TAF_FoundingHeartMintProbe.Count == count + 1 && r_TAF_FoundingHeartMintProbe.Error == null,
					"typed callback corruption acquired an ID or factory return");
				Unassigned(observed);
				Check(ReferenceEquals(The.Game, game) && ReferenceEquals(game.IntGameState, ints)
					&& ReferenceEquals(game.StringGameState, strings) && strings.TryGetValue(key, out string current)
					&& current == wire && ints.TryGetValue(key, out int value) && value == 701
					&& !game.HasInt64GameState(key) && !game.HasObjectGameState(key) && !game.HasBooleanGameState(key),
					"injected state changed; unknown evidence retained");
				Check(ints.Remove(key) && KingdomScenarioDurableState.ProvesExactText(key, wire),
					"exact fixture injection could not be retired");
			}
		}

		internal static void ForeignReplacement(XRLGame game, Zone zone, KingdomFoundingHeartPlan plan)
		{
			foreach (string blueprint in Blueprints)
			{
				Check(r_TAF_FoundingHeartMintProbe.Callback == null, "foreign factory callback already armed");
				GameObject foreign = GameObject.Create(blueprint, BeforeObjectCreated: Retained.Add);
				Check(GameObject.Validate(foreign) && Retained.Contains(foreign) && foreign.Blueprint == blueprint
					&& foreign.CurrentCell == null && foreign.InInventory == null, "foreign fixture custody changed");
				foreign.SetStringProperty("TAFNativeForeignSentinel", "retain-this-reference");
				string id = foreign.IDIfAssigned;
				Dictionary<string, string> strings = new Dictionary<string, string>(foreign.Property);
				Dictionary<string, int> ints = new Dictionary<string, int>(foreign.IntProperty);
				object fence = Fence(zone, plan);
				int count = r_TAF_FoundingHeartMintProbe.Count;
				GameObject observed = null;
				r_TAF_FoundingHeartMintProbe.Callback = (body, e) => { observed = body; e.ReplacementObject = foreign; };
				GameObject made;
				try { made = Create(fence, blueprint); }
				finally { r_TAF_FoundingHeartMintProbe.Callback = null; }
				Check(made == null && GameObject.Validate(observed) && !ReferenceEquals(observed, foreign)
					&& GameObject.Validate(foreign) && foreign.IDIfAssigned == id
					&& foreign.CurrentCell == null && foreign.InInventory == null && foreign.Blueprint == blueprint
					&& r_TAF_FoundingHeartMintProbe.Count == count + 1 && r_TAF_FoundingHeartMintProbe.Error == null,
					"foreign replacement was adopted, destroyed or transferred");
				Exact(strings, foreign.Property); Exact(ints, foreign.IntProperty);
				Unassigned(observed); Unassigned(foreign);
			}
		}

		private static object Fence(Zone zone, KingdomFoundingHeartPlan plan)
		{
			MethodInfo reader = typeof(KingdomPlots).GetMethod("TryReadFoundingHeartContext", Hidden | BindingFlags.Static);
			Check(reader != null, "private heart context reader absent");
			object[] args = { zone, plan, null };
			Check((bool)reader.Invoke(null, args), "current heart context refused");
			Type type = typeof(KingdomPlots).GetNestedType("FoundingHeartAllocationFence", Hidden);
			Check(type != null, "shared production factory guard absent");
			object fence = Activator.CreateInstance(type, Hidden | BindingFlags.Instance, null,
				new[] { (object)zone, args[2] }, null);
			Check(fence != null && (bool)type.GetProperty("Current", Hidden | BindingFlags.Instance).GetValue(fence, null),
				"current real founding authority refused the guard");
			return fence;
		}

		private static GameObject Create(object fence, string blueprint)
		{
			MethodInfo method = fence.GetType().GetMethod("Create", Hidden | BindingFlags.Instance);
			Check(method != null, "production guard factory absent");
			return (GameObject)method.Invoke(fence, new object[] { blueprint });
		}

		private static void Unassigned(GameObject body)
		{
			Check(GameObject.Validate(body) && !(body.IDIfAssigned ?? "").StartsWith("taf-heart-v1-", StringComparison.Ordinal)
				&& !body.HasStringProperty(KingdomPlots.FoundingHeartOwnerProperty)
				&& !body.HasIntProperty(KingdomPlots.FoundingHeartOwnerProperty)
				&& !body.HasStringProperty(KingdomPlots.FoundingHeartSlotProperty)
				&& !body.HasIntProperty(KingdomPlots.FoundingHeartSlotProperty), "unowned object acquired heart identity");
		}

		private static void Exact<T>(Dictionary<string, T> before, Dictionary<string, T> after)
		{
			Check(after != null && before.Count == after.Count, "foreign property count changed");
			foreach (KeyValuePair<string, T> row in before)
				Check(after.TryGetValue(row.Key, out T value) && EqualityComparer<T>.Default.Equals(row.Value, value),
					"foreign property changed: " + row.Key);
		}

		internal static void Check(bool condition, string failure)
		{
			if (!condition) throw new InvalidOperationException(failure);
		}
	}
}
