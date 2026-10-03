using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// What the heartmoot DESIGN guarantees after the second climb, read off the authored
	/// catalogue rather than carried over from the camp (issue #159, native run 51 @ f208b825).
	///
	/// <para>THE MOOT YARD HAS NO HEARTH, BY DESIGN. In
	/// <c>Architecture/KingdomArchitectures-CivicFaith.xml</c> the heartbasin and heartwaterstone
	/// tiers each require <c>fixture:hearth</c> and place the camp fire at glyph <c>h</c>; the
	/// <c>civic-heartmoot-l2</c> map has no <c>h</c> glyph, the <c>civic-heart-moot</c> palette
	/// has no hearth slot, and the heartmoot tier requires <c>seat:moot1</c>/<c>seat:moot2</c>,
	/// two <c>light:moot</c> torch-posts and the retained <c>fixture:first-basin</c> /
	/// <c>fixture:storage</c> instead: a roofed, walled charter hall keeps no open cooking fire.
	/// The renovate-expand delta therefore lists the waterstone's hearth slot in
	/// <c>Removed</c>, and the stamper removes it as an exact receipted, stateless, empty
	/// component before the successor settles (<c>KingdomArchitectureStamper.UpgradeApplication</c>
	/// TryRemovableComponent -> TryRemoveUpgradeSlot, receipted per slot in UpgradeReceipts). The
	/// generation-aware component census carries only PAIRED slots across a tier delta
	/// (<c>KingdomArchitectureComponentCensusRules.TryPeerPlacement</c>); an unpaired slot has no
	/// peer to carry. Native run 51 climbed lawfully and was refused by a check that wanted the
	/// camp fire inside the moot yard; that check was stale, and this shard replaces it.</para>
	///
	/// <para>Everything is read before anything is asserted, and every read is journaled. The
	/// rung-2 fire's identity is looked up globally: absent, or a graveyard tombstone, is the
	/// removal the design asks for; a fire still LIVE anywhere is the stamper leaking a removed
	/// slot and refuses. This shard drives nothing.</para>
	/// </summary>
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private void RequireMootHasNoHearthByDesign(GameObject Standing)
			{
				GameObject fire = FireIn(Standing);
				GameObject liveFire;
				KingdomPhysicalLookupState fireLookup = KingdomConstruction.FindGlobalLiveId(FireId,
					out liveFire);
				int torchposts = CountInRect(Standing, TorchpostBlueprint);
				int rostrums = CountInRect(Standing, RostrumBlueprint);
				GameObject basin;
				string basinFailure;
				bool basinAnchored = KingdomArchitectureStamper.TryExactAnchoredComponent(Standing,
					Zone, FirstBasinRole, out basin, out basinFailure);
				Evidence.Append("\nphase3 hearth=none-by-design; camp fire in rect=")
					.Append(fire == null ? "none" : fire.IDIfAssigned)
					.Append("; rung-2 fire=").Append(FireId).Append(" lookup=").Append(fireLookup)
					.Append(liveFire != null && liveFire.CurrentCell != null
						? "@" + liveFire.CurrentCell.X + "," + liveFire.CurrentCell.Y : "")
					.Append("; torchposts=").Append(torchposts)
					.Append("; rostrum=").Append(rostrums)
					.Append("; first-basin=").Append(basinAnchored
						? basin.IDIfAssigned + "@" + Offset(basin.CurrentCell)
						: "unanchored: " + basinFailure);
				Require(fire == null, "taf-camp-rung3-hearth-present: a camp fire stands inside the "
					+ "moot yard, which the heartmoot tier does not author: "
					+ (fire == null ? "" : fire.IDIfAssigned));
				Require(fireLookup != KingdomPhysicalLookupState.Exact,
					"taf-camp-rung3-hearth-unremoved: the rung-2 camp fire " + FireId
						+ " still reads live after the renovate-expand delta that removes its slot");
				Require(torchposts >= 2, "taf-camp-rung3-lights-short: the heartmoot tier requires "
					+ "two light:moot torch-posts; " + torchposts + " stand inside the moot yard");
				Require(rostrums == 1, "taf-camp-rung3-rostrum-miscount: the heartmoot tier authors "
					+ "one moot rostrum; " + rostrums + " stand inside the moot yard");
				Require(basinAnchored, "taf-camp-rung3-basin-unanchored: the first basin is no "
					+ "longer the moot yard's exact anchored fixture: " + basinFailure);
			}

			/// <summary>Objects of one blueprint physically inside a heart root's own rect.</summary>
			private int CountInRect(GameObject Root, string Blueprint)
			{
				KingdomPlotRules.PlotRect rect;
				Require(KingdomPlots.TryReadRect(Root, out rect),
					"taf-camp-rect-unreadable: the standing heart's rect could not be read");
				int count = 0;
				for (int y = rect.Y1; y <= rect.Y2; y++)
					for (int x = rect.X1; x <= rect.X2; x++)
					{
						Cell cell = Zone.GetCell(x, y);
						if (cell == null) continue;
						foreach (GameObject item in cell.GetObjects())
							if (GameObject.Validate(item) && item.Blueprint == Blueprint) count++;
					}
				return count;
			}
		}
	}
}
