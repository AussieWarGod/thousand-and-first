using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The four second-city cases, one per scripted check call. Each reads production's own
	/// answer: KingdomFounding.JudgeSite for the verdicts, KingdomFounding.FoundSecond for the
	/// transaction, and the ZoneActivatedEvent handler's own KingdomSystem.TrySeat for the seat
	/// exchange on return. Nothing here seats, claims or publishes anything itself.
	/// </summary>
	internal static class KingdomSecondCityNativeCases
	{
		internal const string TravelCase = "travel-out";
		internal const string FoundCase = "found-second";
		internal const string RefusedCase = "refused-already-ours";
		internal const string ReturnCase = "return-seat";

		internal static void Run(int Index, XRLGame Game, StringBuilder Detail)
		{
			KingdomSystem system = Game == null ? null : Game.GetSystem<KingdomSystem>();
			Require(system != null && system.Founded, "the realm went missing between cases");
			switch (Index)
			{
			case 0: TravelOut(Game, system, Detail); return;
			case 1: FoundSecond(Game, system, Detail); return;
			case 2: RefuseAlreadyOurs(Game, system, Detail); return;
			case 3: ReturnSeat(Game, system, Detail); return;
			}
			Require(false, "the second-city script has no case " + Index);
		}

		/// <summary>
		/// Case one. The founder reaches the distant parasang and it becomes the active ground.
		/// The seat must NOT move: that ground answers to nobody yet.
		/// </summary>
		private static void TravelOut(XRLGame Game, KingdomSystem System, StringBuilder Detail)
		{
			GameObject player = The.Player;
			Require(player != null && ReferenceEquals(player.CurrentCell,
				KingdomSecondCityNativeChecks.HomeCell),
				"the founder is not on the recorded first-city cell");
			Require(player.SystemMoveTo(KingdomSecondCityNativeChecks.SiteCell, energyCost: 0,
				forced: false, ignoreCombat: true, ignoreGravity: false, noStack: true),
				"the controlled founder transfer to the second site was refused");
			The.ZoneManager.SetActiveZone(KingdomSecondCityNativeChecks.SiteZone);
			The.ZoneManager.ProcessGoToPartyLeader();
			Require(ReferenceEquals(player.CurrentZone, KingdomSecondCityNativeChecks.SiteZone)
				&& ReferenceEquals(The.ZoneManager.ActiveZone,
					KingdomSecondCityNativeChecks.SiteZone),
				"the second site is not the founder's active ground");
			Require(System.SettlementCount == 1 && System.NonSeatSettlementCount == 0
				&& System.City.SettlementId == KingdomSecondCityNativeChecks.FirstSettlementId,
				"arriving on unheld ground moved the realm's seat");
			Still(Game);
			Detail.Append("; case=").Append(TravelCase).Append(" active=")
				.Append(KingdomSecondCityNativeChecks.SiteZoneId).Append(" seat=")
				.Append(KingdomSecondCityNativeChecks.FirstSettlementId).Append(" settlements=1");
		}

		/// <summary>
		/// Case two. The production second-city transaction, with Force false so the adjacency
		/// law stands. The new city takes the seat; the first city enters the authoritative
		/// non-seat topology keeping its own identity and its own claims.
		/// </summary>
		private static void FoundSecond(XRLGame Game, KingdomSystem System, StringBuilder Detail)
		{
			Require(KingdomFounding.JudgeSite(System, KingdomSecondCityNativeChecks.SiteZone)
				== KingdomSettlement.SecondFoundingVerdict.Allowed,
				"the second site stopped reading as Allowed before the pour");
			Require(KingdomFounding.FoundSecond(KingdomSecondCityNativeChecks.SecondCityName,
				KingdomSecondCityNativeChecks.SecondVocation,
				KingdomSecondCityNativeChecks.SiteZone, Force: false),
				"the production second-city transaction refused an Allowed site");
			Require(System.SettlementCount == 2 && System.NonSeatSettlementCount == 1,
				"the realm did not end the founding holding exactly two cities");
			string seat = System.City == null ? null : System.City.SettlementId;
			Require(!string.IsNullOrEmpty(seat)
				&& seat != KingdomSecondCityNativeChecks.FirstSettlementId,
				"the second city did not take the seat with its own identity");
			KingdomSecondCityNativeChecks.SecondSettlementId = seat;
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			Require(nonSeat != null && nonSeat.Count == 1 && nonSeat[0].City != null
				&& nonSeat[0].City.SettlementId
					== KingdomSecondCityNativeChecks.FirstSettlementId,
				"the first city is not the unique non-seat settlement");
			Require(System.ClaimedZones.Contains(KingdomSecondCityNativeChecks.SiteZoneId)
				&& !System.ClaimedZones.Contains(KingdomSecondCityNativeChecks.HomeZoneId),
				"the seated second city does not hold exactly its own ground");
			Require(nonSeat[0].ClaimedZones.Contains(KingdomSecondCityNativeChecks.HomeZoneId)
				&& !nonSeat[0].ClaimedZones.Contains(KingdomSecondCityNativeChecks.SiteZoneId),
				"the non-seat first city lost or gained ground during the founding");
			Require(System.KingdomFactionName == KingdomSecondCityNativeChecks.RealmFactionName,
				"the second founding changed the realm faction");
			Still(Game);
			Require(KingdomScenarioJournal.Append(KingdomSecondCityScript.TopologyRow, true,
				"settlements=2; seat=" + seat + " seat-zone="
				+ KingdomSecondCityNativeChecks.SiteZoneId + "; non-seat="
				+ KingdomSecondCityNativeChecks.FirstSettlementId + " non-seat-zone="
				+ KingdomSecondCityNativeChecks.HomeZoneId + "; realm="
				+ KingdomSecondCityNativeChecks.RealmFactionName
				+ " vocation=" + KingdomSecondCityNativeChecks.SecondVocation) == null,
				"second-city-topology journal unavailable");
			Detail.Append("; case=").Append(FoundCase).Append(" seat=").Append(seat)
				.Append(" non-seat=").Append(KingdomSecondCityNativeChecks.FirstSettlementId)
				.Append(" settlements=2");
		}

		/// <summary>
		/// Case three, the negative. Ground the realm now holds refuses a further founding by
		/// name and spends nothing: the topology must be byte-identical afterwards.
		/// </summary>
		private static void RefuseAlreadyOurs(XRLGame Game, KingdomSystem System,
			StringBuilder Detail)
		{
			Require(KingdomFounding.JudgeSite(System, KingdomSecondCityNativeChecks.SiteZone)
				== KingdomSettlement.SecondFoundingVerdict.GroundIsAlreadyOurs,
				"held ground did not read as GroundIsAlreadyOurs");
			string before = Topology(System);
			Require(!KingdomFounding.FoundSecond(KingdomSecondCityNativeChecks.RefusedCityName,
				KingdomSecondCityNativeChecks.RefusedVocation,
				KingdomSecondCityNativeChecks.SiteZone, Force: false),
				"a second founding on held ground was accepted");
			Require(Topology(System) == before,
				"the refused founding changed the realm's settlement topology");
			Require(System.SettlementCount == 2 && System.NonSeatSettlementCount == 1,
				"the refused founding changed the realm's city count");
			Still(Game);
			Detail.Append("; case=").Append(RefusedCase)
				.Append(" verdict=GroundIsAlreadyOurs settlements=2");
		}

		/// <summary>
		/// Case four. The founder returns; activating the first city's ground is what production
		/// itself acts on - KingdomSystem.HandleEvent(ZoneActivatedEvent) calls TrySeat before
		/// the claim guard - so the seat must come back without this shard seating anything.
		/// </summary>
		private static void ReturnSeat(XRLGame Game, KingdomSystem System, StringBuilder Detail)
		{
			GameObject player = The.Player;
			Require(player != null && player.SystemMoveTo(
				KingdomSecondCityNativeChecks.HomeCell, energyCost: 0, forced: false,
				ignoreCombat: true, ignoreGravity: false, noStack: true),
				"the founder could not return to the first city cell");
			Zone home = KingdomSecondCityNativeChecks.HomeCell.ParentZone;
			The.ZoneManager.SetActiveZone(home);
			The.ZoneManager.ProcessGoToPartyLeader();
			Require(ReferenceEquals(player.CurrentZone, home)
				&& ReferenceEquals(The.ZoneManager.ActiveZone, home)
				&& home.ZoneID == KingdomSecondCityNativeChecks.HomeZoneId,
				"the first city's ground is not the founder's active ground again");
			Require(System.City != null && System.City.SettlementId
				== KingdomSecondCityNativeChecks.FirstSettlementId,
				"returning to the first city did not bring its seat back");
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			Require(nonSeat != null && nonSeat.Count == 1 && nonSeat[0].City != null
				&& nonSeat[0].City.SettlementId
					== KingdomSecondCityNativeChecks.SecondSettlementId,
				"the second city is not the unique non-seat settlement after the return");
			Require(System.SettlementCount == 2
				&& System.ClaimedZones.Contains(KingdomSecondCityNativeChecks.HomeZoneId)
				&& nonSeat[0].ClaimedZones.Contains(KingdomSecondCityNativeChecks.SiteZoneId),
				"the exchanged topology does not hold both cities on their own ground");
			Still(Game);
			Detail.Append("; case=").Append(ReturnCase).Append(" seat=")
				.Append(KingdomSecondCityNativeChecks.FirstSettlementId).Append(" non-seat=")
				.Append(KingdomSecondCityNativeChecks.SecondSettlementId)
				.Append(" settlements=2 seat-exchange=production");
		}

		/// <summary>Seat identity plus every settlement's ordered claims, as one exact string.</summary>
		private static string Topology(KingdomSystem System)
		{
			StringBuilder shape = new StringBuilder();
			shape.Append(System.City == null ? "-" : System.City.SettlementId).Append('|');
			foreach (string zoneId in System.ClaimedZones) shape.Append(zoneId).Append(',');
			List<KingdomSettlement> nonSeat = System.NonSeatSettlements();
			for (int i = 0; nonSeat != null && i < nonSeat.Count; i++)
			{
				shape.Append('|').Append(nonSeat[i].City == null
					? "-" : nonSeat[i].City.SettlementId).Append('/');
				foreach (string zoneId in nonSeat[i].ClaimedZones) shape.Append(zoneId).Append(',');
			}
			return shape.ToString();
		}

		/// <summary>No case may spend a game turn: every transition here is a transaction.</summary>
		private static void Still(XRLGame Game)
		{
			Require(Game.Turns == KingdomSecondCityNativeChecks.Turns
				&& Game.TimeTicks == KingdomSecondCityNativeChecks.Ticks,
				"a second-city case advanced world time");
		}

		private static void Require(bool Value, string Failure)
		{
			KingdomSecondCityNativeProvider.Require(Value, Failure);
		}
	}
}
