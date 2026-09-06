using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomFoundingHeartProbeBlueprints
	{
		private const string ProbeName = "r_TAF_FoundingHeartMintProbe";
		private const int MaximumOriginalParts = 512;
		private static KingdomFoundingHeartProbeBlueprints Retained;
		private readonly XRLGame Game;
		private readonly GameObjectFactory Factory;
		private readonly Dictionary<string, GameObjectBlueprint> Blueprints;
		private readonly Binding[] Bindings;

		private KingdomFoundingHeartProbeBlueprints(bool lifecycle)
		{
			Game = The.Game;
			Factory = GameObjectFactory.Factory;
			Blueprints = Factory == null ? null : Factory.Blueprints;
			Require(Game != null && Factory != null && Blueprints != null,
				"game or blueprint factory is absent");
			string[] names = lifecycle
				? new[] { "r_KingdomHeartStake", "r_KingdomFirstBasin", "r_KingdomPlotWorks", "r_KingdomRiteGround" }
				: new[] { "r_KingdomHeartStake", "r_KingdomFirstBasin", "r_KingdomPlotWorks" };
			Bindings = new Binding[names.Length];
			for (int i = 0; i < names.Length; i++)
			{
				GameObjectBlueprint blueprint;
				Require(Blueprints.TryGetValue(names[i], out blueprint) && blueprint != null && blueprint.Name == names[i],
					"required founding-heart blueprint is missing: " + names[i]);
				Bindings[i] = new Binding(names[i], blueprint);
				for (int j = 0; j < i; j++)
					Require(!ReferenceEquals(Bindings[j].Blueprint, blueprint)
						&& !ReferenceEquals(Bindings[j].Parts, Bindings[i].Parts),
						"founding-heart blueprints share mutable part authority");
			}
			CheckState(0);
		}

		internal static KingdomFoundingHeartProbeBlueprints Install(bool lifecycle = false)
		{
			Require(Retained == null && r_TAF_FoundingHeartMintProbe.Callback == null
				&& r_TAF_FoundingHeartMintProbe.Count == 0 && r_TAF_FoundingHeartMintProbe.Error == null,
				"probe installation is not fresh and disarmed");
			Require(lifecycle
				? KingdomScenarioDurableState.ProvesExactText(KingdomFoundingHeartLifecycleProvider.Receipt, "intent")
				: KingdomScenarioDurableState.ProvesExactText(KingdomFoundingHeartNativeProvider.Receipt, "intent"),
				"native founding-heart fixture intent is not exact");
			KingdomFoundingHeartProbeBlueprints binding = new KingdomFoundingHeartProbeBlueprints(lifecycle);
			Require(Retained == null, "probe installation changed during preflight");
			// Retain the complete before-state even if an add or its following proof fails.
			Retained = binding;
			for (int i = 0; i < binding.Bindings.Length; i++)
			{
				binding.CheckState(i);
				binding.Bindings[i].Parts.Add(ProbeName, binding.Bindings[i].Probe);
				binding.CheckState(i + 1);
			}
			binding.Check();
			return binding;
		}

		internal void Check()
		{
			Require(ReferenceEquals(Retained, this), "probe installation lost its retained binding");
			CheckState(Bindings.Length);
		}

		private void CheckState(int added)
		{
			Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(GameObjectFactory.Factory, Factory)
				&& ReferenceEquals(Factory.Blueprints, Blueprints), "game or blueprint dictionary changed");
			for (int i = 0; i < Bindings.Length; i++)
			{
				Binding row = Bindings[i];
				GameObjectBlueprint current;
				Require(row != null && Blueprints.TryGetValue(row.Name, out current)
					&& ReferenceEquals(current, row.Blueprint) && current.Name == row.Name
					&& ReferenceEquals(current.Parts, row.Parts) && ReferenceEquals(current.allparts, row.Parts),
					"exact founding-heart blueprint or part dictionary changed");
				row.Check(i < added);
			}
		}

		private sealed class Binding
		{
			internal readonly string Name;
			internal readonly GameObjectBlueprint Blueprint;
			internal readonly Dictionary<string, GamePartBlueprint> Parts;
			internal readonly GamePartBlueprint Probe;
			private readonly Dictionary<string, GamePartBlueprint> Original;
			private readonly GamePartBlueprint.PartReflectionCache Reflector;

			internal Binding(string name, GameObjectBlueprint blueprint)
			{
				Name = name; Blueprint = blueprint; Parts = blueprint.Parts;
				Require(Parts != null && Parts.Count <= MaximumOriginalParts
					&& ReferenceEquals(blueprint.allparts, Parts) && !Parts.ContainsKey(ProbeName),
					"blueprint part table is unavailable, oversized or already instrumented: " + name);
				Original = new Dictionary<string, GamePartBlueprint>(StringComparer.Ordinal);
				foreach (KeyValuePair<string, GamePartBlueprint> part in Parts)
				{
					Require(!string.IsNullOrEmpty(part.Key) && part.Value != null && part.Value.T != null
						&& typeof(IPart).IsAssignableFrom(part.Value.T)
						&& part.Value.Name != ProbeName && part.Value.T != typeof(r_TAF_FoundingHeartMintProbe),
						"blueprint contains a malformed or pre-existing probe part: " + name);
					Original.Add(part.Key, part.Value);
				}
				Probe = new GamePartBlueprint(ProbeName);
				Reflector = Probe.Reflector;
				Check(false);
			}

			internal void Check(bool added)
			{
				Require(Parts.Count == Original.Count + (added ? 1 : 0), "blueprint part count changed: " + Name);
				foreach (KeyValuePair<string, GamePartBlueprint> part in Original)
				{
					GamePartBlueprint current;
					Require(Parts.TryGetValue(part.Key, out current) && ReferenceEquals(current, part.Value),
						"original blueprint part entry changed: " + Name);
				}
				GamePartBlueprint installed;
				Require(added ? Parts.TryGetValue(ProbeName, out installed) && ReferenceEquals(installed, Probe)
					: !Parts.ContainsKey(ProbeName), "probe part entry changed: " + Name);
				Require(Probe.Name == ProbeName && Probe.Namespace == "XRL.World.Parts" && Probe.ChanceOneIn == 1
					&& Probe.T == typeof(r_TAF_FoundingHeartMintProbe) && Reflector != null
					&& ReferenceEquals(Probe.Reflector, Reflector) && Reflector.T == typeof(r_TAF_FoundingHeartMintProbe),
					"probe blueprint does not resolve to the exact native part");
				using (IEnumerator<KeyValuePair<string, string>> parameters = Probe.GetParameterStrings().GetEnumerator())
					Require(!parameters.MoveNext(), "probe blueprint acquired unexpected parameters");
			}
		}

		private static void Require(bool condition, string detail)
		{
			if (!condition) throw new InvalidOperationException("founding-heart probe installation: " + detail);
		}
	}
}
