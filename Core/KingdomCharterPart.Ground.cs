using System;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public sealed partial class KingdomCharterPart
	{
		/// <summary>
		/// Takes the ground the founder is standing on into the seated city.
		/// <para>
		/// The one verb the whole multi-zone half of the design was waiting on. Everything behind
		/// it was already built and already tested and had nothing to exercise it: growth,
		/// commission, plots, yards, sockets, faith and the locus all gate on
		/// <c>ClaimedZones.Contains</c>, districts are a real per-zone map, eight designs gate on
		/// <c>MinZones</c>, and the wall line is recomputed against the whole claim on every
		/// placement. In normal play a city held exactly one parasang forever, because
		/// <c>KingdomFounding.ClaimZone</c>'s only callers were the founding rite and two debug
		/// wishes. This is the founder's own claim.
		/// </para>
		/// <para>
		/// <b>It costs nothing, and that is a decision.</b> The brief prices the founding rite (a
		/// basin of fresh water) and prices every building, and names no price at all for a
		/// claim: expansion direction is "entirely the founder's". What a claim actually costs is
		/// paid afterwards and in kind &mdash; a new parasang is a new wall line to raise, a new
		/// budget of ground to lay, and a stage that must have been earned first. Inventing a
		/// dram price here would be inventing a ruling nobody made.
		/// </para>
		/// <para>
		/// Every refusal names what would lift it, and a claim that goes through says what it did
		/// to the wall line &mdash; including when the answer is "nothing", which is the honest
		/// answer for ground taken diagonally across a corner or straight down into the rock.
		/// </para>
		/// </summary>
		public void ClaimGround(KingdomSystem System)
		{
			Zone zone = ParentObject.CurrentZone;
			KingdomZoningRules.ClaimVerdict verdict = KingdomFounding.JudgeClaim(System, zone);
			if (verdict != KingdomZoningRules.ClaimVerdict.Allowed)
			{
				Popup.Show(KingdomZoningRules.ClaimRefusal(verdict,
					KingdomPresentation.Rich(System.SeatName), System.Stage));
				return;
			}
			// The wall line is measured on the ground the city ALREADY holds, before and after,
			// with the same zones asked both times: what changes is which neighbours exist, and
			// an edge that stops facing the world is an edge that stops being wall ground.
			// Measuring the new zone's own edges instead would answer nothing - a zone is not its
			// own neighbour, so its frontier reads the same either side of the claim.
			System.Collections.Generic.List<string> held = new System.Collections.Generic.List<string>(System.ClaimedZones);
			int before = WorldFacingEdges(held, System.ClaimedZones);
			if (Popup.ShowYesNo("Take " + XRL.Language.Grammar.GetProsaicZoneName(zone) + " into {{C|" + KingdomPresentation.Rich(System.SeatName)
				+ "}}? Nothing is spent. This ground becomes the city's: its plots, its wall line, its district to name.") != DialogResult.Yes)
			{
				return;
			}
			if (!KingdomFounding.ClaimZone(zone))
			{
				// The primitive judges the same ground a second time and is entitled to disagree
				// - it is the one that actually writes the zone property. Refused rather than
				// reported as done.
				Popup.Show("The claim did not hold. This ground is not the city's.");
				return;
			}
			KingdomGovernanceScope.Commit("claim ground");
			int after = WorldFacingEdges(held, System.ClaimedZones);
			Popup.Show("{{G|" + KingdomPresentation.Rich(System.SeatName) + " holds " + XRL.Language.Grammar.GetProsaicZoneName(zone) + ".}}\n\n"
				+ KingdomZoningRules.ClaimedWallClause(before, after,
					KingdomPresentation.Rich(System.SeatName))
				+ "\n\n" + KingdomZoningRules.ClaimHoldingLine(System.ClaimedZones.Count, KingdomZoningRules.ZonesForStage(System.Stage)));
		}

		/// <summary>How many zone edges the named ground turns to the world, summed, judged
		/// against a claim. Asked either side of a claim with the same ground and a widened claim,
		/// which is the difference the founder is told about.</summary>
		private static int WorldFacingEdges(System.Collections.Generic.IList<string> Held, System.Collections.Generic.List<string> Claim)
		{
			int edges = 0;
			for (int i = 0; (Held != null) && i < Held.Count; i++)
			{
				edges += KingdomZoningRules.EdgeCount(KingdomRules.FrontierEdges(Held[i], Claim));
			}
			return edges;
		}

		/// <summary>
		/// The water detail: who walks to the river, and how many of them.
		/// <para>
		/// A settlement fetches nothing until the founder says so. Before this, water arrived by
		/// itself from the moment of founding, which was automation nobody chose and made a
		/// riverside site self-sustaining forever. Now the founder either carries water in
		/// themselves, or spends people on carrying it - and those people are not manning
		/// anything else while they do.
		/// </para>
		/// </summary>
		public void SetWaterDetail(KingdomSystem System)
		{
			int most = System.Population;
			if (most <= 0)
			{
				Popup.Show("There is nobody here to send. Pour what the settlement needs yourself, or wait for settlers.");
				return;
			}
			string[] options = new string[most + 1];
			options[0] = "{{K|Nobody}} — the settlement drinks what you bring it";
			for (int i = 1; i <= most; i++)
			{
				options[i] = i + ((i == 1) ? " settler" : " settlers")
					+ " {{K|(" + (i * KingdomRules.FetchDramsPerSettler) + " drams a day, if there is open water here)}}"
					+ ((i == System.WaterCrew) ? " {{G|[current]}}" : "");
			}
			int num = Popup.PickOption(
				Title: "The water detail of " + KingdomPresentation.Rich(System.SeatName),
				Intro: "Everyone you put on the water is one you cannot put on a work. " + System.Population
					+ ((System.Population == 1) ? " settler lives here." : " settlers live here."),
				Options: options, AllowEscape: true);
			if (num < 0 || num == System.WaterCrew)
			{
				return;
			}
			System.WaterCrew = num;
			KingdomGovernanceScope.Commit("set water detail");
			KingdomChronicle.Record(System, (num == 0)
				? ("the water detail of " + KingdomPresentation.Rich(System.SeatName) + " was stood down")
				: (num + ((num == 1) ? " settler was" : " settlers were") + " set to carrying water for " + KingdomPresentation.Rich(System.SeatName)));
			Popup.Show((num == 0)
				? "The buckets are hung up. " + KingdomPresentation.Rich(System.SeatName) + " will drink what you bring it."
				: (num + ((num == 1) ? " settler walks" : " settlers walk") + " to the water now. The works have " + (System.Population - num) + " left to draw on."));
		}

	}
}
