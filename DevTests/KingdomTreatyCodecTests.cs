#if TAF_TESTS
using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using ThousandAndFirst.Treaty;

namespace ThousandAndFirst.Tests
{
	[TestFixture] public sealed class KingdomTreatyCodecTests
	{
		[Test] public void AuthenticatedRoundTripTamperAndSurrogateAreBounded()
		{
			var l=new KingdomTreatyLedger();string f;Assert.NotNull(KingdomTreatyRules.Propose(l,0,"p","a","b",new[]{"c"},new string[0],new string[0],"water",1,out f));
			Assert.IsTrue(KingdomTreatyCodec.TryEncode(l,out byte[] bytes,out f),f);Assert.IsFalse(KingdomTreatyCodec.Decode(bytes).Quarantined);
			byte[] corrupt=(byte[])bytes.Clone();corrupt[12]^=1;Assert.IsTrue(KingdomTreatyCodec.Decode(corrupt).Quarantined);
			l.Pacts[0].Clauses[0]="bad\ud800";Assert.IsFalse(KingdomTreatyCodec.TryEncode(l,out _,out f));Assert.IsNotEmpty(f);
		}
		[Test] public void UnknownAuthenticatedFutureIsPreservedByteExact()
		{
			var l=new KingdomTreatyLedger();string f;KingdomTreatyRules.Propose(l,0,"p","a","b",new[]{"c"},new string[0],new string[0],"water",1,out f);
			KingdomTreatyCodec.TryEncode(l,out byte[] current,out f);byte[] future=(byte[])current.Clone();
			future[4]=2;Authenticate(future);var q=KingdomTreatyCodec.Decode(future);
			Assert.IsTrue(q.Quarantined);Assert.AreEqual("future treaty payload preserved",q.Fault);
			CollectionAssert.AreEqual(future,q.OpaquePayload);
		}
		[Test] public void FullLawfulMaximumIsExactAndCapPlusOneCannotPublish()
		{
			var l=new KingdomTreatyLedger();for(int i=0;i<KingdomTreatyLedger.MaxPacts;i++)l.Pacts.Add(MaxPact(i));
			Assert.IsTrue(KingdomTreatyCodec.TryEncode(l,out byte[] bytes,out string failure),failure);
			Assert.AreEqual(241384,KingdomTreatyCodec.MaxEnvelopeBytes);Assert.AreEqual(241384,bytes.Length);
			l.Pacts[0].Clauses[0]=new string('x',257);
			Assert.IsFalse(KingdomTreatyCodec.TryEncode(l,out _,out failure));Assert.IsNotEmpty(failure);
			var q=KingdomTreatyCodec.Decode(new byte[]{1,2,3});Assert.AreEqual(KingdomTreatyStoreState.Quarantined,q.StoreState);
			Assert.IsFalse(KingdomTreatyCodec.TryEncode(q,out _,out failure));
		}
		private static KingdomTreatyRecord MaxPact(int n)
		{
			string pact=Pad("p"+n,128), projection=Pad("j"+n,128);var p=new KingdomTreatyRecord
			{PactId=pact,PartyA=Pad("a"+n,128),PartyB=Pad("b"+n,128),Phase=PactPhase.Breached,
				RitualLiquid=Pad("l",64),SignatureA=Pad("s"+n,128),SignatureB=Pad("t"+n,128),
				ProposedTick=1,StartTick=2,ExpiryTick=-1,ProjectionId=projection,
				ProjectionLocator=Pad("z",192),WitnessStatus=PactWitnessStatus.Projected,
				BreachActorId=Pad("r"+n,128),BreachCause=Pad("c",128)};
			for(int i=0;i<8;i++){p.Clauses.Add(Pad("c"+i,256));p.Obligations.Add(Pad("o"+i,256));p.Favors.Add(Pad("f"+i,256));
				p.WitnessEvents.Add(new PactWitnessEvent{EventId=Pad("e"+n+"-"+i,128),PactId=pact,
					ProjectionId=projection,ActorId=Pad("x"+i,128),Cause=Pad("w",128),Tick=3+i,Kind=PactWitnessEventKind.WitnessDamaged});}
			for(int i=0;i<16;i++)p.AppliedEffectIds.Add(Pad("q"+i,128));return p;
		}
		private static string Pad(string prefix,int length)=>prefix+new string('x',length-prefix.Length);
		private static void Authenticate(byte[] envelope)
		{
			int version=BitConverter.ToInt32(envelope,4), n=BitConverter.ToInt32(envelope,8);
			using(var sha=SHA256.Create())using(var s=new MemoryStream())
			{byte[] v=BitConverter.GetBytes(version);s.Write(v,0,4);s.Write(envelope,12,n);
				byte[] hash=sha.ComputeHash(s.ToArray());Buffer.BlockCopy(hash,0,envelope,12+n,32);}
		}
	}
}
#endif
