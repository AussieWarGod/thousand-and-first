using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomFoundingHeartRetirementChecks
	{
		private const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		internal static void Run(KingdomFoundingHeartLifecycleWorld world, GameObject predecessor,
			GameObject final, StringBuilder rows, ref int passed, ref string current)
		{
			current = "retirement-negative-baseline";
			Type contextType = typeof(KingdomPlots).GetNestedType("FoundingHeartContext", BindingFlags.NonPublic);
			Check(contextType != null, "retired authority context type missing");
			MethodInfo authority = Method("ExactFoundingHeartRetiredAuthority", typeof(bool),
				typeof(Zone), typeof(string), contextType.MakeByRefType());
			MethodInfo proof = Method("ExactFoundingHeartRetirementProof", typeof(bool),
				typeof(Zone), contextType, typeof(string));
			MethodInfo lookup = Method("FindGraveyardTombstone", typeof(KingdomPhysicalLookupState),
				typeof(string), typeof(GameObject).MakeByRefType());
			MethodInfo collector = Method("TryLoadedPlotTombstones", typeof(bool),
				typeof(List<GameObject>).MakeByRefType());
			// Prime only loaded-zone graveyard getters before snapshots; they allocate empty queues lazily.
			Check((bool)collector.Invoke(null, new object[] { null }), "native tombstone baseline unreadable");
			Check(ReferenceEquals(world.Completed(predecessor), final), "completed baseline changed");
			string id = predecessor.IDIfAssigned;
			object[] baseline = { world.Zone, id, null };
			Check((bool)authority.Invoke(null, baseline) && baseline[2] != null, "retired baseline authority refused");
			object context = baseline[2];
			Check((bool)proof.Invoke(null, new object[] { world.Zone, context, id }), "retired baseline proof refused");
			string[] kinds = { "duplicate", "null-collection", "overbound", "wrong-owner", "wrong-slot", "live-conflict" };
			foreach (string kind in kinds)
			{
				current = "retirement-" + kind + "-refused";
				var fault = new KingdomFoundingHeartRetirementFault(world, predecessor, final);
				fault.VerifyBaseline();
				fault.Install(kind);
				fault.VerifyInstalled();
				bool readable = (bool)collector.Invoke(null, new object[] { null });
				Check(readable == (kind != "null-collection" && kind != "overbound"),
					"collector did not distinguish unreadable or overbound native collections");
				object[] found = { id, null };
				var state = (KingdomPhysicalLookupState)lookup.Invoke(null, found);
				bool ambiguous = kind == "duplicate" || kind == "null-collection" || kind == "overbound";
				Check(state == (ambiguous ? KingdomPhysicalLookupState.Ambiguous : KingdomPhysicalLookupState.Exact)
					&& (ambiguous ? found[1] == null : ReferenceEquals(found[1], predecessor)),
					"native tombstone lookup admitted ambiguity or lost its exact original");
				bool inner = (bool)proof.Invoke(null, new object[] { world.Zone, context, id });
				// The exact-tombstone branch requires its caller's live-custody guard.
				Check(inner == (kind == "live-conflict"), "retirement proof returned the wrong native verdict");
				Check(!(bool)authority.Invoke(null, new object[] { world.Zone, id, null }),
					"full retired authority admitted the injected conflict");
				fault.VerifyInstalled();
				// No finally restore: an unexpected result retains both original and injected evidence.
				fault.VerifyAndRestore();
				fault.VerifyBaseline();
				Check((bool)authority.Invoke(null, new object[] { world.Zone, id, null })
					&& ReferenceEquals(world.Completed(predecessor), final), "restored completed authority refused");
				passed++;
				rows.Append('\n').Append(current).Append("=PASS");
			}
		}

		private static MethodInfo Method(string name, Type result, params Type[] arguments)
		{
			MethodInfo method = typeof(KingdomPlots).GetMethod(name, Hidden, null, arguments, null);
			Check(method != null && method.ReturnType == result, "native observation signature missing: " + name);
			return method;
		}

		private static void Check(bool condition, string failure)
		{
			KingdomFoundingHeartAllocationNativeCases.Check(condition, failure);
		}
	}
}
