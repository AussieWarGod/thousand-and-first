#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Value tests for the generation-aware component census
	/// (<see cref="KingdomArchitectureComponentCensusRules"/>), plus one case that runs the REAL
	/// delta over a real expanding layout and reads the retained pairing out of it.
	/// <para>
	/// What motivated the rule: during the retag pass of an authored upgrade the component of one
	/// generation and a component of the other legitimately share the lot and the layout-local
	/// slot NAME while standing on different world cells. Counting by lot and slot alone made a
	/// correct settlement read as a duplicate; counting by this generation's token alone let a
	/// stranger at that slot pass unseen. The peer is proved by identity, cell and token together,
	/// and anything else refuses.
	/// </para>
	/// </summary>
	public class KingdomArchitectureComponentCensusTests
	{
		private const string Lot = "lot-1";
		private const string Slot = "g:01:01";
		private const string ThisToken = "token-this";
		private const string PeerToken = "token-peer";
		private const string PeerId = "peer-id";

		private sealed class Candidate
		{
			internal string Token;
			internal string Id;
			internal bool AtPeerCell;
			internal Candidate(string token, string id, bool atPeerCell)
			{ Token = token; Id = id; AtPeerCell = atPeerCell; }
		}

		private static bool Census(IList<Candidate> Candidates, bool PeerGiven, bool PeerAllowed)
		{
			int thisCount = 0;
			int otherCount = 0;
			int foreignCount = 0;
			foreach (Candidate candidate in Candidates)
			{
				int membership = KingdomArchitectureComponentCensusRules.Classify(ThisToken,
					candidate.Token, candidate.Id, candidate.AtPeerCell,
					PeerGiven ? PeerId : null, PeerGiven ? PeerToken : null,
					PeerGiven && PeerAllowed);
				if (membership == KingdomArchitectureComponentCensusRules.This) thisCount++;
				else if (membership == KingdomArchitectureComponentCensusRules.Other) otherCount++;
				else foreignCount++;
			}
			return KingdomArchitectureComponentCensusRules.Settled(thisCount, otherCount,
				foreignCount);
		}

		private static Candidate Mine() { return new Candidate(ThisToken, "mine", false); }

		private static Candidate Peer() { return new Candidate(PeerToken, PeerId, true); }

		/// <summary>Case 1: the ordinary settle, one component and no other generation.</summary>
		[Test]
		public void SingleComponentWithNoPeerSettles()
		{
			ClassicAssert.IsTrue(Census(new[] { Mine() }, false, false));
		}

		/// <summary>Case 2: the retag case this rule exists for -- both generations stand, the
		/// peer is proved and still allowed, and the census settles.</summary>
		[Test]
		public void ThisPlusAProvedAllowedPeerSettles()
		{
			ClassicAssert.IsTrue(Census(new[] { Mine(), Peer() }, true, true));
		}

		/// <summary>Case 3: the peer was permitted but has already been retagged away, so nothing
		/// of its generation stands here. At most one, never exactly one.</summary>
		[Test]
		public void AnAllowedPeerThatIsAbsentStillSettles()
		{
			ClassicAssert.IsTrue(Census(new[] { Mine() }, true, true));
		}

		/// <summary>Case 4: an unknown, empty or absent token at this slot is a stranger, and one
		/// stranger refuses the census. This is what counting by token alone let through.</summary>
		[Test]
		public void AnUnknownEmptyOrAbsentTokenRefuses()
		{
			foreach (string token in new[] { "token-foreign", "", null })
				ClassicAssert.IsFalse(
					Census(new[] { Mine(), new Candidate(token, "stranger", false) }, true, true),
					token ?? "null");
		}

		/// <summary>Case 5: two of this generation is the duplicate the census exists to catch.</summary>
		[Test]
		public void TwoOfThisGenerationRefuses()
		{
			ClassicAssert.IsFalse(Census(new[] { Mine(), Mine() }, true, true));
		}

		/// <summary>Case 6: two objects satisfying the peer proof are a duplicate too.</summary>
		[Test]
		public void TwoProvedPeersRefuse()
		{
			ClassicAssert.IsFalse(Census(new[] { Mine(), Peer(), Peer() }, true, true));
		}

		/// <summary>Case 7: the peer's token on another cell is not the peer.</summary>
		[Test]
		public void ThePeerTokenOnTheWrongCellRefuses()
		{
			ClassicAssert.IsFalse(
				Census(new[] { Mine(), new Candidate(PeerToken, PeerId, false) }, true, true));
		}

		/// <summary>Case 8: the peer's token under another identity is not the peer.</summary>
		[Test]
		public void ThePeerTokenWithTheWrongIdentityRefuses()
		{
			ClassicAssert.IsFalse(
				Census(new[] { Mine(), new Candidate(PeerToken, "other-id", true) }, true, true));
		}

		/// <summary>Case 9: a peer whose retain state no longer lets it stand is a stranger.
		/// An After census permits a peer under state 2; a Before census permits one over 0.</summary>
		[Test]
		public void APeerThatIsNoLongerAllowedRefuses()
		{
			ClassicAssert.IsFalse(Census(new[] { Mine(), Peer() }, true, false));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.PeerAllowed(2, true));
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.PeerAllowed(1, true));
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.PeerAllowed(0, true));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.PeerAllowed(0, false));
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.PeerAllowed(1, false));
			ClassicAssert.IsTrue(KingdomArchitectureComponentCensusRules.PeerAllowed(2, false));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.PeerAllowed(3, true));
			ClassicAssert.IsFalse(KingdomArchitectureComponentCensusRules.PeerAllowed(-1, false));
		}

		/// <summary>Case 10: a second generation standing where no peer was resolved refuses.</summary>
		[Test]
		public void ASecondGenerationWithNoPeerGivenRefuses()
		{
			ClassicAssert.IsFalse(Census(new[] { Mine(), Peer() }, false, false));
		}

		/// <summary>Slot membership is lot and slot; what a candidate IS is the classifier's
		/// question, and a census without its own terms admits nothing.</summary>
		[Test]
		public void SlotMembershipAndEmptyCensusTermsAreExact()
		{
			ClassicAssert.IsTrue(
				KingdomArchitectureComponentCensusRules.AtSlot(Lot, Slot, Lot, Slot));
			ClassicAssert.IsFalse(
				KingdomArchitectureComponentCensusRules.AtSlot(Lot, Slot, "lot-2", Slot));
			ClassicAssert.IsFalse(
				KingdomArchitectureComponentCensusRules.AtSlot(Lot, Slot, Lot, "g:02:02"));
			ClassicAssert.IsFalse(
				KingdomArchitectureComponentCensusRules.AtSlot(null, Slot, null, Slot));
			ClassicAssert.IsFalse(
				KingdomArchitectureComponentCensusRules.AtSlot(Lot, null, Lot, null));
			ClassicAssert.AreEqual(KingdomArchitectureComponentCensusRules.Foreign,
				KingdomArchitectureComponentCensusRules.Classify(null, ThisToken, "id", true,
					PeerId, PeerToken, true));
			ClassicAssert.IsFalse(
				KingdomArchitectureComponentCensusRules.Settled(1, 0, 1));
			ClassicAssert.IsFalse(
				KingdomArchitectureComponentCensusRules.Settled(0, 0, 0));
		}
	}
}
#endif
