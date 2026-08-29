#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst.Treaty;

namespace ThousandAndFirst.Tests
{
	[TestFixture] public sealed class KingdomTreatyRulesTests
	{
		[Test] public void LifecycleEffectsAndMissingFactionAreCasAndIdempotent()
		{
			var l=new KingdomTreatyLedger();var p=Propose(l);string f;
			Assert.AreEqual(PactPhase.Proposed,p.Phase);Assert.IsTrue(KingdomTreatyRules.Ratify(l,l.Revision,p.PactId,"sig-a","sig-b",2,out f),f);
			Assert.IsTrue(KingdomTreatyRules.Activate(l,l.Revision,p.PactId,3,100,true,out f),f);
			long r=l.Revision;Assert.IsFalse(KingdomTreatyRules.ApplyEffect(l,r,p.PactId,"effect-a",false,out f));
			Assert.IsTrue(l.Pacts[0].EffectsSuspended);Assert.IsTrue(KingdomTreatyRules.ApplyEffect(l,l.Revision,p.PactId,"effect-a",true,out f),f);
			r=l.Revision;Assert.IsTrue(KingdomTreatyRules.ApplyEffect(l,r,p.PactId,"effect-a",true,out f),f);Assert.AreEqual(r,l.Revision);
			Assert.IsTrue(KingdomTreatyRules.Fulfill(l,l.Revision,p.PactId,4,out f),f);Assert.IsTrue(KingdomTreatyRules.Dissolve(l,l.Revision,p.PactId,5,out f),f);
		}
		[Test] public void WitnessClassificationNeverInventsMoralActorAndReissueKeepsTreaty()
		{
			var l=new KingdomTreatyLedger();var p=Propose(l);string f;KingdomTreatyRules.Ratify(l,l.Revision,p.PactId,"a","b",2,out f);KingdomTreatyRules.Activate(l,l.Revision,p.PactId,3,-1,true,out f);
			Assert.IsTrue(KingdomTreatyRules.Reissue(l,l.Revision,p.PactId,"projection-a","zone.1.1.1.1.1",out f),f);
			Assert.IsTrue(KingdomTreatyRules.ObserveWitness(l,l.Revision,p.PactId,"projection-a",PactWitnessEventKind.WitnessStolen,"event-1","actor","untyped loss",4,false,out f),f);
			Assert.AreEqual(PactWitnessEventKind.WitnessLost,l.Pacts[0].WitnessEvents[0].Kind);Assert.AreEqual(PactPhase.Active,l.Pacts[0].Phase);
			Assert.IsTrue(KingdomTreatyRules.ObserveWitness(l,l.Revision,p.PactId,"copy",PactWitnessEventKind.WitnessDamaged,"event-2",null,"debug copy",5,false,out f),f);
			Assert.AreEqual(PactWitnessEventKind.DuplicateInert,l.Pacts[0].WitnessEvents[1].Kind);Assert.AreEqual("projection-a",l.Pacts[0].ProjectionId);
			Assert.IsTrue(KingdomTreatyRules.ObserveWitness(l,l.Revision,p.PactId,"projection-a",PactWitnessEventKind.DeliberateBreach,"event-3","player","confirmed",6,true,out f),f);
			Assert.AreEqual(PactPhase.Breached,l.Pacts[0].Phase);Assert.AreEqual("player",l.Pacts[0].BreachActorId);
			Assert.IsTrue(KingdomTreatyRules.Reissue(l,l.Revision,p.PactId,"projection-b","zone.1.1.1.1.1",out f),f);Assert.AreEqual(PactPhase.Breached,l.Pacts[0].Phase);
		}
		[Test] public void CapacityAndCrashCutsMutateNothing()
		{
			var l=new KingdomTreatyLedger();var p=Propose(l);byte[] before;string f;Assert.IsTrue(KingdomTreatyCodec.TryEncode(l,out before,out f),f);
			Assert.IsFalse(KingdomTreatyRules.Ratify(l,l.Revision+1,p.PactId,"a","b",2,out f));CollectionAssert.AreEqual(before,Encode(l));
			Assert.IsFalse(KingdomTreatyRules.Reissue(l,l.Revision,p.PactId,"projection",
				new string('z',KingdomTreatyRules.MaxLocatorBytes+1),out f));CollectionAssert.AreEqual(before,Encode(l));
			var many=new List<string>();for(int i=0;i<=KingdomTreatyRules.MaxClauses;i++)many.Add("clause-"+i);
			Assert.IsNull(KingdomTreatyRules.Propose(l,l.Revision,"pact-2","a","b",many,new string[0],new string[0],"water",2,out f));
		}
		private static KingdomTreatyRecord Propose(KingdomTreatyLedger l){string f;return KingdomTreatyRules.Propose(l,l.Revision,"pact-1","faction-a","realm",new[]{"mutual passage"},new[]{"answer one typed call"},new[]{"one disclosed favor"},"water",1,out f);}
		private static byte[] Encode(KingdomTreatyLedger l){KingdomTreatyCodec.TryEncode(l,out var b,out _);return b;}
	}
}
#endif
