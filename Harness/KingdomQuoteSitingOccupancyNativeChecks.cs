using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Three behaviours for the #034 quote-siting occupancy fix, all synchronous (no engine
	/// turns needed: quoting and committing are instant real production calls). SYNTHETIC
	/// SETUP, DISCLOSED: real founding/dedication, one synthetic NPC body per phase, harness-
	/// assigned raw timber -- nothing forces a rect or a snapshot directly.
	/// </summary>
	internal static partial class KingdomQuoteSitingOccupancyNativeChecks
	{
		private const string BuildKey = "fire";
		private static Frame Retained;

		internal static bool Vacant { get { return Retained == null; } }

		/// <summary>True once a check verb has ALREADY set Done on a prior call -- read by the
		/// provider BEFORE calling Run() again, so it knows a repeat check must verify the
		/// already-written report rather than the pre-completion "intent" text.</summary>
		internal static bool Completed { get { return Retained != null && Retained.Done; } }

		internal static string Run(string Verb, XRLGame Game, Zone Zone, out bool Complete)
		{
			Complete = false;
			if (Verb == KingdomQuoteSitingOccupancyNativeProvider.SetupVerb)
			{
				Require(Retained == null, "a quote-occupancy attempt is already retained");
				Retained = new Frame(Game, Zone);
				Retained.Start();
			}
			else
			{
				Require(Retained != null, "the quote-occupancy check verb arrived before its setup verb");
				Retained.Check();
			}
			Complete = Retained.Done;
			// Truthful counts: a case that threw is caught inside RunCase and counted as failed,
			// never escapes to abort this verb or the other two cases.
			return "native-quote-occupancy cases=3 passed=" + Retained.Passed
				+ " failed=" + Retained.Failed + Retained.Evidence;
		}

		internal static string Fail(Exception Error)
		{
			return "native-quote-occupancy cases=3 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(Error.GetType().Name + ": " + Error.Message)
				+ Retained?.Evidence;
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomQuoteSitingOccupancyNativeProvider.Require(Value, Failure);
		}

		/// <summary>All three behaviours run synchronously inside Start() -- quoting and
		/// committing never wait on engine turns, unlike construction/strike. Start() runs the
		/// cases but does NOT set Done: setup must still return the plain "intent" receipt so
		/// the check verb's own readback (Provider.cs) is not overwritten before it runs, the
		/// same reason KingdomDepositOverflowNativeChecks reaches Done only on its own last
		/// check. Since all three cases already finished by the time setup returns, the first
		/// Check() call is what flips Done -- idempotent afterward, like every later check
		/// call here.</summary>
		private sealed partial class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly List<GameObject> Owned = new List<GameObject>();
			internal bool Done;
			internal int Passed;
			internal int Failed;
			internal readonly StringBuilder Evidence = new StringBuilder();

			internal Frame(XRLGame Game, Zone Zone) { this.Game = Game; this.Zone = Zone; }

			internal void Start()
			{
				KingdomSystem system = KingdomNativeCampFounding.Found(Game, Zone, Require);
				KingdomNativeCampFounding.Dedicate(Game, Zone, system,
					16 * KingdomRules.DramsPerArrival, Owned.Add, Require);
				GameObject chest = GameObject.Create("Chest");
				Require(GameObject.Validate(chest) && chest.Inventory != null,
					"the container blueprint produced nothing that holds things");
				Owned.Add(chest);
				Cell seat = KingdomNativeCampFounding.Clear(Zone);
				Require(ReferenceEquals(seat.AddObject(chest, NoStack: true), chest),
					"native placement substituted the synthetic store");
				string dedicateFailure;
				Require(KingdomMaterials.DedicateStockpile(system, Zone, chest, out dedicateFailure),
					dedicateFailure ?? "the production check-in refused the synthetic store");
				Require(KingdomData.TryGetBuilding(BuildKey, out KingdomRules.BuildEntry entry),
					"the fixture design is missing from the live catalogue");
				RunCase("occupied-first-clear-alternate",
					() => OccupiedFirstClearAlternate(system, entry));
				RunCase("all-occupied-no-mutation", () => AllOccupiedNoMutation(system, entry));
				RunCase("drift-after-quote-preflight-refused",
					() => DriftAfterQuotePreflightRefusal(system, entry));
			}

			/// <summary>A Require failure (or any other exception) inside ONE case must never
			/// abort the other two or escape this verb: it is caught here, counted, and journaled
			/// by name, so the setup call's own cases=/passed=/failed= line stays truthful even
			/// when a case regresses. Fixture setup ABOVE this (founding, dedicating the store)
			/// still throws through Require -- without it no case could run at all.</summary>
			private void RunCase(string Name, Action Body)
			{
				try
				{
					Body();
					Passed++;
				}
				catch (Exception error)
				{
					Failed++;
					Evidence.Append("; case=").Append(Name).Append(" outcome=FAILED reason=")
						.Append(KingdomScenarioRules.Bounded(
							error.GetType().Name + ": " + error.Message));
				}
			}

			/// <summary>Idempotent: the cases already ran in Start(), so every check call --
			/// the first and every one after -- only confirms completion.</summary>
			internal void Check()
			{
				Done = true;
			}
		}
	}
}
