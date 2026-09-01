#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomSystemLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomSystem.cs",
			"Core/KingdomSystem.z01.State.Foundation.cs",
			"Core/KingdomSystem.z02.State.City.cs",
			"Core/KingdomSystem.z03.State.Realm.cs",
			"Core/KingdomSystem.z03a.State.Relationships.cs",
			"Core/KingdomSystem.z04.Identity.Read.cs",
			"Core/KingdomSystem.z05.Identity.Founding.cs",
			"Core/KingdomSystem.z06.Identity.Topology.cs",
			"Core/KingdomSystem.z07.Identity.Pending.cs",
			"Core/KingdomSystem.z08.Settlements.cs",
			"Core/KingdomRealmChronicleIntentRules.cs",
			"Core/KingdomSystem.z09.Exile.Dispatch.cs",
			"Core/KingdomSystem.z09b.Exile.ChronicleDispatch.cs",
			"Core/KingdomSystem.z10.Exile.Mirrors.cs",
			"Core/KingdomSystem.z11.Return.Begin.cs",
			"Core/KingdomSystem.z12.Return.Callback.cs",
			"Core/KingdomSystem.z13.Return.Seat.cs",
			"Core/KingdomSystem.z14.Return.AbilityProof.cs",
			"Core/KingdomSystem.z15.Return.AbilityReferences.cs",
			"Core/KingdomSystem.z16.Return.Feelings.cs",
			"Core/KingdomSystem.z17.Return.Chronicle.cs",
			"Core/KingdomSystem.z18.Return.Restore.cs",
			"Core/KingdomSystem.z19.PersistenceAndCallbacks.cs",
			"Core/KingdomSystem.z19a.Serialization.cs",
			"Core/KingdomSystem.z19b.SaveGuard.cs",
			"Core/KingdomSystem.z20.Events.cs",
			"Core/KingdomSystem.z21.SemanticPass.cs",
			"Core/KingdomSystem.z21a.Guard.cs",
			"Core/KingdomSystem.z22.Standings.cs",
			"Core/KingdomSystem.z22a.DirectionalStandings.cs",
			"Core/KingdomSystem.z22c.DirectionalStandingSpillover.cs",
			"Core/KingdomSystem.z23.Normalization.cs",
			"Core/KingdomSystem.z24.Normalization.Collections.cs",
			"Core/KingdomSystem.z25.IdentityNormalization.cs",
			"Core/KingdomSystem.z26.TradeNormalization.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Files.Length; i++)
			{
				source.Append(TestMain.ReadRepositoryText(Files[i]));
			}
			return source.ToString();
		}
	}
}
#endif
