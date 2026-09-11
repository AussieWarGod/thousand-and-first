using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			// The real founding walk stops outside the SMALL footprint. The first enlargement
			// includes that cell: prove the player blocks it, then use normal movement to leave.
			private void ProveFounderAndWalkClear()
			{
				GameObject player = The.Player;
				Require(GameObject.Validate(player) && player.CurrentZone == Zone
					&& player.CurrentCell != null, "taf-camp-founder-absent: no exact founder cell");
				Require(KingdomArchitectureRuntime.TryRead(Heart, out var before, out string failure),
					failure ?? "taf-camp-founder-no-layout");
				Require(KingdomArchitectureRuntime.TryPrepareSuccessor(System, Zone, before,
					SecondRungKey, out var after, out failure), failure ?? "taf-camp-founder-no-successor");
				Cell start = player.CurrentCell;
				Require(!before.Rect.Contains(start.X, start.Y) && after.Rect.Contains(start.X, start.Y),
					"taf-camp-founder-negative-unreached: founder is not on newly annexed ground");
				Require(!KingdomArchitectureStamper.TryProveEnvelopeGrowth(System, Zone, Heart,
					null, after, false, out failure) && failure != null
					&& failure.StartsWith("a living occupant stands on plot-envelope growth ground at ",
						StringComparison.Ordinal),
					"taf-camp-founder-negative-wrong: occupied expansion must refuse for a living occupant; "
						+ KingdomScenarioRules.Bounded(failure));
				Evidence.Append("\nfounder-occupied-ground-refused=true; cell=")
					.Append(start.X).Append(',').Append(start.Y);
				int moves = 0;
				while (after.Rect.Contains(player.CurrentCell.X, player.CurrentCell.Y))
				{
					int x = player.CurrentCell.X, y = player.CurrentCell.Y;
					Require(x > 1 && ++moves <= 32
						&& player.Move("W", AllowDashing: false, DoConfirmations: false),
						"taf-camp-founder-walk-refused: founder could not walk clear");
					Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(The.Player, player)
						&& player.CurrentZone == Zone && player.CurrentCell != null
						&& player.CurrentCell.X == x - 1 && player.CurrentCell.Y == y,
						"taf-camp-founder-walk-changed: movement changed unexpected ownership or ground");
				}
				Require(KingdomArchitectureStamper.TryProveEnvelopeGrowth(System, Zone, Heart,
					null, after, false, out failure),
					"taf-camp-founder-clear-ground-refused: " + KingdomScenarioRules.Bounded(failure));
				RequireStoreIdentity();
				Evidence.Append("\nfounder-normal-west-moves=").Append(moves)
					.Append("; cleared-ground-proved=true; cell=")
					.Append(player.CurrentCell.X).Append(',').Append(player.CurrentCell.Y);
			}
		}
	}
