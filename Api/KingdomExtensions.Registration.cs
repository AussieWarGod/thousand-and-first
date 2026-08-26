using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	public static partial class KingdomExtensions
	{
		// ==================================================================================
		// Registration
		// ==================================================================================

		private static List<Binding> Registry()
		{
			bool enabled = Enabled;
			if (Bound != null && BuiltEnabled == enabled)
			{
				return Bound;
			}
			List<Binding> bound = new List<Binding>();
			List<string> refused = new List<string>();
			if (enabled)
			{
				KingdomSystem.Guard("kingdom extension registration", delegate
				{
					Collect(bound, refused);
					RefuseNamespaceCollisions(bound, refused);
				});
			}
			// Sorted, and not left in scan order. ModManager walks ActiveTypes, whose order is the
			// player's mod list -- so two installs with the same mods in a different order would
			// otherwise run the same extensions in a different sequence, and any draw either made
			// would land on a different ordinal. Determinism is not a property we get to leave to
			// a load order.
			bound.Sort(delegate(Binding a, Binding b)
			{
				int mod = string.CompareOrdinal(a.ModName, b.ModName);
				if (mod != 0) return mod;
				int assembly = string.CompareOrdinal(a.AssemblyName, b.AssemblyName);
				return (assembly != 0) ? assembly : string.CompareOrdinal(a.TypeName, b.TypeName);
			});
			refused.Sort(StringComparer.Ordinal);
			Bound = bound;
			Refused = refused;
			BuiltEnabled = enabled;
			Announce(refused);
			return Bound;
		}

		/// <summary>
		/// Finds every marked type and admits the ones that qualify.
		/// <para>
		/// <b>The scan is the engine's, the construction is ours</b>, and the split is deliberate.
		/// <c>ModManager.GetInstancesWithAttribute</c> (<c>D/XRL/ModManager.cs:1185-1196</c>) does
		/// both in one call, but its <c>Activator.CreateInstance</c> runs unguarded over every
		/// marked type &mdash; so one third-party class with no parameterless constructor would
		/// throw out of the middle of the loop and take every other mod's extension down with it.
		/// That is exactly the failure &sect;6.6 clause 3 forbids. The cached attribute scan is
		/// still the engine's own; only the per-type construction moved inside a guard.
		/// </para>
		/// </summary>
		private static void Collect(List<Binding> bound, List<string> refused)
		{
			foreach (Type type in ModManager.GetTypesWithAttribute(typeof(KingdomExtensionAttribute)))
			{
				if (type == null)
				{
					continue;
				}
				string owner = OwnerOf(type);
				if (!typeof(IKingdomExtension).IsAssignableFrom(type))
				{
					refused.Add(KingdomApiRules.RefusalLine(KingdomExtensionVerdict.RefusedNoContract, owner, 0));
					continue;
				}
				IKingdomExtension extension = null;
				int declared = 0;
				bool asked = false;
				// Third-party code, running before it has been admitted: the constructor and the
				// version getter are both asked inside the guard, so either throwing is a refusal
				// of THAT extension and not a crash of the registry.
				KingdomSystem.Guard("kingdom extension " + (type.FullName ?? type.Name), delegate
				{
					extension = Activator.CreateInstance(type) as IKingdomExtension;
					if (extension != null)
					{
						declared = extension.ApiVersion;
						asked = true;
					}
				});
				bool behaviour = extension is IResourceKind || extension is IJobKind
					|| extension is ICarrierKind || extension is INetworkKind
					|| extension is IWorkBehaviour;
				bool identity = extension is IKingdomIdentitySource;
				bool contract = extension is IKingdomAskSource || extension is IKingdomHappeningSource
					|| identity || behaviour;
				int required = behaviour ? KingdomApiRules.BehaviourVersion : (identity ? 2 : 1);
				KingdomExtensionVerdict verdict = (asked && extension != null)
					? KingdomApiRules.Judge(owner, declared, contract, required)
					: KingdomExtensionVerdict.RefusedThrew;
				if (verdict != KingdomExtensionVerdict.Accepted)
				{
					refused.Add(KingdomApiRules.RefusalLine(verdict, owner, declared, required));
					continue;
				}
				bound.Add(new Binding(owner, AssemblyNameOf(type),
					type.FullName ?? type.Name, extension));
			}
		}

		private static string OwnerOf(Type type)
		{
			if (type == null)
			{
				return "";
			}
			ModInfo mod = (type.Assembly == null) ? null : ModManager.GetMod(type.Assembly);
			if (mod != null)
			{
				return mod.ID ?? "";
			}
			string assembly = AssemblyNameOf(type);
			return string.IsNullOrEmpty(assembly) ? "" : assembly;
		}

		private static string AssemblyNameOf(Type type)
		{
			return type == null || type.Assembly == null ? "" : type.Assembly.GetName().Name ?? "";
		}

		/// <summary>Refuses every owner in a lossy canonical-namespace collision. First-wins would
		/// make mod load order transfer durable rows, identity keys, and draw streams across mods.</summary>
		private static void RefuseNamespaceCollisions(List<Binding> bound, List<string> refused)
		{
			Dictionary<string, string> firstByNamespace =
				new Dictionary<string, string>(StringComparer.Ordinal);
			HashSet<string> collidedOwners = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < bound.Count; i++)
			{
				string owner = bound[i].ModName;
				string ownerNamespace = KingdomApiRules.Kind(owner);
				string first;
				if (firstByNamespace.TryGetValue(ownerNamespace, out first)
					&& !string.Equals(first, owner, StringComparison.Ordinal))
				{
					collidedOwners.Add(first);
					collidedOwners.Add(owner);
				}
				else
				{
					firstByNamespace[ownerNamespace] = owner;
				}
			}
			if (collidedOwners.Count == 0) return;
			bound.RemoveAll(delegate(Binding binding)
			{
				return collidedOwners.Contains(binding.ModName);
			});
			foreach (string owner in collidedOwners)
				refused.Add(KingdomApiRules.RefusalLine(
					KingdomExtensionVerdict.RefusedNamespaceCollision, owner, 0));
		}

		private static void Announce(List<string> refused)
		{
			for (int i = 0; i < refused.Count; i++)
			{
				MetricsManager.LogError("ThousandAndFirst API: " + refused[i]);
				KingdomLog.Log("extension refused: " + refused[i]);
				if (The.Game != null)
				{
					MessageQueue.AddPlayerMessage("{{R|" + refused[i] + "}}");
				}
			}
		}

	}
}
