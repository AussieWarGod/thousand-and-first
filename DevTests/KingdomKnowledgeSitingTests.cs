#if TAF_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The knowledge siting (Addendum 22 B1-B8), tested where it actually lives: on the settlement
	/// container that secession, rejoin, exile and return all move.
	/// <para>
	/// The four state moves are driven through <c>ReadFrom</c>/<c>WriteTo</c> directly rather than
	/// through the exile and secession code, because that is precisely the claim being tested: those
	/// four paths carry knowledge <b>with no knowledge-specific code of their own</b>. If the rolls
	/// ride the container, secession and exile are already correct; if they do not, no amount of
	/// code in either file would fix it.
	/// </para>
	/// </summary>
	public class KingdomKnowledgeSitingTests
	{
		private const string FoundryRolls = "disk:smelter|machine:glass furnace|node:kiln|node:cruciblesteel";

		private const string TrunkRolls = "disk:tent|node:notes";

		// --- The fields are on the container at all, and are carried ---------------------------

		[Test]
		public void ACityCarriesItsOwnRollsAndItsOwnBench()
		{
			// Named rather than reflected, because the whole siting ruling is the claim that THESE
			// facts belong to a city. A future refactor that moves one of them back onto the realm
			// should fail here and be made to argue for itself.
			List<string> carried = new List<string>();
			foreach (FieldInfo field in KingdomSettlement.CarriedFields())
			{
				carried.Add(field.Name);
			}
			Assert.Contains("KeepersRoster", carried);
			Assert.Contains("ResearchSubject", carried);
			Assert.Contains("ResearchAccrued", carried);
			Assert.Contains("ResearchTakenUpTick", carried);
			Assert.Contains("ResearchShelf", carried);
			Assert.Contains("ResearchBestMind", carried);
			Assert.Contains("ResearchStalledAnnounced", carried);
		}

		// --- Secession and rejoin (B2, B6) ------------------------------------------------------

		[Test]
		public void Secession_TakesTheLeaversRollsWithTheLeaver()
		{
			KingdomSettlement seat = Foundry();
			// Secede: the leaving city's whole container is moved out of the realm's flat fields,
			// and a blank city takes the seat. Nothing about knowledge appears in either step.
			KingdomSettlement seceded = new KingdomSettlement();
			seceded.ReadFrom(seat);
			new KingdomSettlement().WriteTo(seat);

			Assert.AreEqual(FoundryRolls, seceded.KeepersRoster, "the leaver walks off with what its own keepers learned");
			Assert.AreEqual("cruciblesteel", seceded.ResearchSubject);
			Assert.AreEqual(4200, seceded.ResearchAccrued);
			Assert.IsTrue(string.IsNullOrEmpty(seat.KeepersRoster), "the realm no longer holds what only the leaver knew");
			Assert.IsNull(seat.ResearchSubject);
			Assert.AreEqual(0, seat.ResearchAccrued);
		}

		[Test]
		public void Rejoin_RestoresTheLeaversRollsWholeAndFree()
		{
			// B6: the Charter's own promise -- "it still keeps everything it kept" -- holds for
			// knowledge in both directions, with no re-learning tax anywhere.
			KingdomSettlement seat = Foundry();
			KingdomSettlement seceded = new KingdomSettlement();
			seceded.ReadFrom(seat);
			new KingdomSettlement().WriteTo(seat);

			seceded.WriteTo(seat);

			Assert.AreEqual(FoundryRolls, seat.KeepersRoster);
			Assert.AreEqual("cruciblesteel", seat.ResearchSubject);
			Assert.AreEqual(4200, seat.ResearchAccrued);
			Assert.AreEqual(19, seat.ResearchBestMind);
			Assert.AreEqual(600, seat.ResearchShelf["thevat"]);
		}

		[Test]
		public void SecessionAndRejoin_RoundTripTheRollsExactly()
		{
			KingdomSettlement seat = Foundry();
			string before = seat.KeepersRoster;
			for (int i = 0; i < 3; i++)
			{
				KingdomSettlement away = new KingdomSettlement();
				away.ReadFrom(seat);
				new KingdomSettlement().WriteTo(seat);
				away.WriteTo(seat);
			}
			Assert.AreEqual(before, seat.KeepersRoster, "three quarrels and three reconciliations cost the city nothing");
		}

		// --- Exile and return (B3) --------------------------------------------------------------

		[Test]
		public void Exile_TakesTheRollsWithTheRealmAndLeavesTheFounderNothingFinished()
		{
			// The behaviour this whole wave exists to fix: before the siting, the roster was one
			// string on the GAME, so exile could not touch it and a founder put out at arclight
			// founded their next camp already holding every design the old realm had earned.
			KingdomSettlement seat = Foundry();
			KingdomSettlement away = Trunk();

			KingdomSettlement exiledSeat = new KingdomSettlement();
			exiledSeat.ReadFrom(seat);
			KingdomSettlement exiledAway = away;
			new KingdomSettlement().WriteTo(seat);

			Assert.IsTrue(string.IsNullOrEmpty(seat.KeepersRoster),
				"an exiled founder carries leads and seeds, never holdings");
			Assert.AreEqual(FoundryRolls, exiledSeat.KeepersRoster);
			Assert.AreEqual(TrunkRolls, exiledAway.KeepersRoster);
		}

		[Test]
		public void Return_FindsBothCitiesRollsIntact()
		{
			KingdomSettlement seat = Foundry();
			KingdomSettlement exiledSeat = new KingdomSettlement();
			exiledSeat.ReadFrom(seat);
			new KingdomSettlement().WriteTo(seat);

			exiledSeat.WriteTo(seat);

			Assert.AreEqual(FoundryRolls, seat.KeepersRoster);
			Assert.AreEqual(4200, seat.ResearchAccrued);
		}

		[Test]
		public void RefoundingAfterExile_StartsFromNothingLearned()
		{
			// "Doors, never rooms": a blank city is a blank city, and whatever the founder
			// remembers lives in the journal rather than in any container here.
			KingdomSettlement refounded = new KingdomSettlement();
			Assert.IsTrue(string.IsNullOrEmpty(refounded.KeepersRoster));
			Assert.IsNull(refounded.ResearchSubject);
			Assert.AreEqual(0, refounded.ResearchAccrued);
			Assert.AreEqual(0, refounded.ResearchShelf.Count);
		}

		// --- Seat-only reading (B4) -------------------------------------------------------------

		[Test]
		public void TwoCitiesHoldTwoDifferentSetsOfRolls()
		{
			KingdomSettlement seat = Foundry();
			KingdomSettlement away = Trunk();
			Assert.AreNotEqual(seat.KeepersRoster, away.KeepersRoster);

			List<string> here = KingdomZoningRules.DecodeRoster(seat.KeepersRoster);
			List<string> there = KingdomZoningRules.DecodeRoster(away.KeepersRoster);
			Assert.IsTrue(KingdomZoningRules.Knows(here, "node:cruciblesteel"));
			Assert.IsFalse(KingdomZoningRules.Knows(there, "node:cruciblesteel"),
				"knowledge is where it was taught; the other city has to be taught too");
		}

		[Test]
		public void WalkingBetweenCitiesSwapsWhichRollsAreRead()
		{
			// The seat swap in miniature: the founder walks, the containers exchange, and the
			// question "what do the keepers know" answers about the city they are standing in.
			KingdomSettlement seat = Foundry();
			KingdomSettlement away = Trunk();

			KingdomSettlement wasSeated = new KingdomSettlement();
			wasSeated.ReadFrom(seat);
			away.WriteTo(seat);
			away = wasSeated;

			Assert.AreEqual(TrunkRolls, seat.KeepersRoster);
			Assert.AreEqual(FoundryRolls, away.KeepersRoster);
			Assert.IsFalse(KingdomZoningRules.Knows(KingdomZoningRules.DecodeRoster(seat.KeepersRoster), "node:cruciblesteel"));
		}

		// --- Craft is per city, and MinTech is judged against the city being built in (B7) -------

		[Test]
		public void CraftIsDerivedPerCityFromThatCitysOwnRolls()
		{
			KingdomSettlement foundry = Foundry();
			KingdomSettlement trunk = Trunk();
			int here = KingdomZoningRules.TechPoints(KingdomZoningRules.DecodeRoster(foundry.KeepersRoster));
			int there = KingdomZoningRules.TechPoints(KingdomZoningRules.DecodeRoster(trunk.KeepersRoster));
			Assert.Greater(here, there, "the city that certified a machine builds better than the one that did not");
			Assert.AreEqual(TechLevel.Salvage, KingdomZoningRules.LevelForPoints(here));
			Assert.AreEqual(TechLevel.Hands, KingdomZoningRules.LevelForPoints(there));
		}

		[Test]
		public void AResearchNodeIsWorthNoCraftAtAll()
		{
			// RR7: craft is what the settlement LEARNED and CERTIFIED, never a readout of the
			// research system. Two ladders, orthogonal, and this is the assertion that keeps them so.
			Assert.AreEqual(0, KingdomZoningRules.PointsForKind(KingdomZoningRules.KindNode));
			Assert.AreEqual(0, KingdomZoningRules.TechPointsPerNode);
			List<string> onlyNodes = KingdomZoningRules.DecodeRoster("node:notes|node:kiln|node:cruciblesteel|node:arclight");
			Assert.AreEqual(0, KingdomZoningRules.TechPoints(onlyNodes));
			Assert.AreEqual(TechLevel.Hands, KingdomZoningRules.LevelForPoints(KingdomZoningRules.TechPoints(onlyNodes)));
		}

		// --- The node kind speaks the shipped gate vocabulary and nothing else (verdict 1) -------

		[Test]
		public void ANodeKeyIsMatchedByTheGateMachineryThatAlreadyShips()
		{
			List<string> roster = KingdomZoningRules.DecodeRoster(FoundryRolls);
			Assert.IsTrue(KingdomZoningRules.Knows(roster, "node:kiln"));
			Assert.IsFalse(KingdomZoningRules.Knows(roster, "node:arclight"));
			// A bare name matches any kind, exactly as it does for a disk or a certification, so an
			// author who writes Knowledge="kiln" is satisfied by the node.
			Assert.IsTrue(KingdomZoningRules.Knows(roster, "kiln"));
		}

		[Test]
		public void AThirdPartyBuildingGatesOnANodeWithNoCodeAtAll()
		{
			// The whole extensibility claim in one assertion: the gate parser reads node: exactly
			// as it reads every other kind, and Judge refuses and permits on it unchanged.
			string error;
			ZoneGate gate = KingdomZoningRules.ParseGateAttributes("theirmod_annexe", null, null,
				"node:theirthing", null, out error);
			Assert.IsNull(error);
			Assert.AreEqual("node:theirthing", gate.Knowledge);
			ZoningJudgement without = KingdomZoningRules.Judge(gate, null, "craft", 0,
				KingdomZoningRules.DecodeRoster(FoundryRolls));
			Assert.IsFalse(without.Permitted);
			Assert.AreEqual(ZoningVerdict.RefusedUnlearned, without.Verdict);
			ZoningJudgement with = KingdomZoningRules.Judge(gate, null, "craft", 0,
				KingdomZoningRules.DecodeRoster(FoundryRolls + "|node:theirthing"));
			Assert.IsTrue(with.Permitted);
		}

		// --- The roster string survives everything a container does to it -----------------------

		[Test]
		public void RollsRoundTripThroughTheStoreExactly()
		{
			List<string> roster = KingdomZoningRules.DecodeRoster(FoundryRolls);
			Assert.AreEqual(FoundryRolls, KingdomZoningRules.EncodeRoster(roster));
		}

		[Test]
		public void AnUnreadableStoreCostsACityNothingButTheUnreadableKey()
		{
			// Hostile-input discipline: a corrupt roll disables one key, never the whole city.
			List<string> roster = KingdomZoningRules.DecodeRoster("node:notes||   |node:kiln");
			Assert.AreEqual(2, roster.Count);
			Assert.IsTrue(KingdomZoningRules.Knows(roster, "node:notes"));
			Assert.IsTrue(KingdomZoningRules.Knows(roster, "node:kiln"));
		}

		// --- Fixtures ---------------------------------------------------------------------------

		private static KingdomSettlement Foundry()
		{
			KingdomSettlement city = new KingdomSettlement();
			city.SettlementName = "Sotham's Rest";
			city.KeepersRoster = FoundryRolls;
			city.ResearchSubject = "cruciblesteel";
			city.ResearchAccrued = 4200;
			city.ResearchTakenUpTick = 90000L;
			city.ResearchBestMind = 19;
			city.ResearchShelf["thevat"] = 600;
			return city;
		}

		private static KingdomSettlement Trunk()
		{
			KingdomSettlement city = new KingdomSettlement();
			city.SettlementName = "Kavvat";
			city.KeepersRoster = TrunkRolls;
			return city;
		}
	}
}
#endif
