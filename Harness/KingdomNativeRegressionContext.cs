using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Native fixture assertions and exact-reference disposal, confined to dev profiles.</summary>
	internal sealed class KingdomNativeRegressionContext
	{
		private readonly Func<GameObject, bool> RemoveFixture;
		private readonly List<GameObject> Owned = new List<GameObject>();
		private readonly HashSet<string> Cases = new HashSet<string>(StringComparer.Ordinal);
		private readonly StringBuilder Results = new StringBuilder();
		private bool Poisoned;
		internal XRLGame Game { get; private set; }
		internal Zone Zone { get; private set; }
		internal int Passed { get; private set; }
		internal int Failed { get; private set; }
		internal int Count { get { return Cases.Count; } }

		internal KingdomNativeRegressionContext(XRLGame Game, Zone Zone,
			Func<GameObject, bool> RemoveFixture)
		{
			this.Game = Game;
			this.Zone = Zone;
			this.RemoveFixture = RemoveFixture;
		}

		internal void Check(bool Condition, string Detail)
		{
			if (!Condition) throw new InvalidOperationException(Detail);
		}

		internal GameObject Track(GameObject Exact)
		{
			if (Exact != null)
			{
				for (int i = 0; i < Owned.Count; i++)
					if (ReferenceEquals(Owned[i], Exact)) return Exact;
				Owned.Add(Exact);
			}
			return Exact;
		}

		internal void Case(string Id, Action Body)
		{
			if (string.IsNullOrEmpty(Id) || !Cases.Add(Id))
				throw new InvalidOperationException("Native case identity is missing or repeated.");
			string failure = Poisoned ? "preceding fixture left unproved state" : null;
			try
			{
				if (failure == null) Body();
			}
			catch (Exception ex) { failure = ex.GetType().Name + ": " + ex.Message; }
			finally
			{
				for (int i = Owned.Count - 1; i >= 0; i--)
				{
					bool removed = false;
					try { removed = RemoveFixture(Owned[i]); }
					catch (Exception) { }
					if (!removed)
					{
						Poisoned = true;
						failure = (failure == null ? "" : failure + "; ")
							+ "fixture disposal unproved at allocation " + i;
					}
				}
				Owned.Clear();
			}
			if (HasQuickstartState(Game) || (Game.GetSystem<KingdomSystem>()?.Founded ?? false))
			{
				Poisoned = true;
				failure = (failure == null ? "" : failure + "; ") + "fixture changed durable authority";
			}
			if (failure == null) Passed++;
			else Failed++;
			Results.Append('\n').Append(Id).Append(failure == null ? "=PASS" : "=FAIL ")
				.Append(failure == null ? "" : KingdomScenarioRules.Bounded(failure));
		}

		internal string Report()
		{
			return "native-quickstart cases=" + Count + " passed=" + Passed + " failed=" + Failed
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested" + Results;
		}

		internal static bool HasQuickstartState(XRLGame Game)
		{
			if (Game == null) return true;
			foreach (string key in new[] { KingdomQuickstartRules.ProfileState,
				KingdomQuickstartRules.ReceiptState, KingdomQuickstartRules.WorldReservationState,
				KingdomQuickstartRules.QuarantineState })
				if (HasAnyState(Game, key)) return true;
			return false;
		}

		internal static bool HasAnyState(XRLGame Game, string Key)
		{
			return Game == null || (Game.StringGameState?.ContainsKey(Key) ?? false)
				|| (Game.IntGameState?.ContainsKey(Key) ?? false)
				|| (Game.Int64GameState?.ContainsKey(Key) ?? false)
				|| (Game.BooleanGameState?.ContainsKey(Key) ?? false)
				|| (Game.ObjectGameState?.ContainsKey(Key) ?? false);
		}
	}
}
