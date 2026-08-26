using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Api
{
	/// <summary>
	/// The registry: who is extending the city, and under what terms.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;6.6. Discovery is the engine's own idiom &mdash;
	/// <c>ModManager.GetInstancesWithAttribute&lt;T&gt;(typeof(A))</c>
	/// (<c>D/XRL/ModManager.cs:1185-1196</c>), the same call <c>WorldFactory</c> makes for world
	/// builders (<c>D/XRL/World/WorldFactory.cs:108</c>) &mdash; so a third-party mod needs no hard
	/// reference beyond the contract namespace and no line of ours.
	/// </para>
	/// <para>
	/// <b>The five invariants, enforced here rather than trusted.</b> Draws go through
	/// <see cref="KingdomExtensionDraws"/> and the kernel (clause 1). Every call crosses
	/// <c>KingdomExecutor.Submit</c>, so a frozen reading goes in, a frozen result comes out, and a
	/// source that throws or runs long stalls itself and nothing else (clauses 2 and 3). Telling
	/// goes through the ledger, the chronicle and <c>KingdomWord</c> under &sect;4.2's shared
	/// budget (clause 4). Everything an extension returns is clamped by
	/// <see cref="KingdomApiRules"/> before it reaches a surface (clause 5).
	/// </para>
	/// <para>
	/// <b>Refused loudly.</b> Version drift, a missing contract or a nameless owner is a refusal by
	/// mod name, in the log and in the message queue, with the version it wanted and the version we
	/// are. Never silently skipped, because a player attributes missing behaviour to us.
	/// </para>
	/// </summary>
	[HasModSensitiveStaticCache]
	public static partial class KingdomExtensions
	{
		/// <summary>
		/// One admitted extension and the mod that owns it. The immutable manifest ID is captured
		/// once at registration; display titles are presentation and may rename or collide.
		/// </summary>
		internal sealed class Binding
		{
			internal readonly string ModName;

			internal readonly string AssemblyName;

			internal readonly string TypeName;

			internal readonly IKingdomExtension Extension;

			internal Binding(string modName, string assemblyName, string typeName,
				IKingdomExtension extension)
			{
				ModName = modName;
				AssemblyName = assemblyName;
				TypeName = typeName;
				Extension = extension;
			}
		}

		/// <summary>
		/// Reset when the mod list changes, exactly as the engine resets its own
		/// (<c>ModManager.ResetModSensitiveStaticCaches</c>, <c>D/XRL/ModManager.cs:340-355</c>).
		/// A stale registry after a mod is disabled would go on running code the player removed.
		/// <para>
		/// Reset to <b>null</b> and not to an empty list: the reset writes
		/// <c>CreateEmptyInstance ? Activator.CreateInstance(fieldType) : null</c>
		/// (<c>D/XRL/ModManager.cs:351-352</c>), and an empty list is indistinguishable from "built
		/// and found nothing" &mdash; which would leave the registry permanently empty after any
		/// mod-list change instead of rebuilding it.
		/// </para>
		/// </summary>
		[ModSensitiveStaticCache]
		private static List<Binding> Bound;

		[ModSensitiveStaticCache]
		private static List<string> Refused;

		/// <summary>Runtime fault notices already shown this mod-list generation. Logging happens on
		/// every fault; the screen names each owner/lane once so a broken identity source cannot
		/// turn a daily reconciliation into message spam.</summary>
		[ModSensitiveStaticCache]
		private static HashSet<string> AnnouncedFaults;

		private const int MaxRuntimeFaultAnnouncements = 64;

		/// <summary>What <see cref="Enabled"/> read when the registry was built. The option is a
		/// checkbox the player can flip mid-session, and a registry that never noticed would go on
		/// running third-party code after the player switched it off.</summary>
		private static bool BuiltEnabled;

		/// <summary>Whether the behaviour lane is open at all. Off means the data lane still works
		/// &mdash; XML merge-by-key never depended on this &mdash; and no third-party C# runs.</summary>
		public static bool Enabled
		{
			get { return XRL.UI.Options.GetOption("r_TAF_OptionExtensions", "Yes") != "No"; }
		}

		/// <summary>The published API version, restated where a modder will look for it.</summary>
		public static int Version
		{
			get { return KingdomApiRules.Version; }
		}

		/// <summary>
		/// How many extensions are admitted, and what they are called. Reads the registry, building
		/// it on first ask.
		/// </summary>
		/// <returns>Mod names, one per admitted extension, in registration order. Empty when the
		/// lane is off or nothing is installed.</returns>
		public static List<string> Admitted()
		{
			List<string> names = new List<string>();
			foreach (Binding binding in Registry())
			{
				names.Add(binding.ModName);
			}
			return names;
		}

		/// <summary>
		/// Whether any admitted extension wants to be asked what happened.
		/// <para>
		/// Asked before the settlement pass reads the book at all: projecting a reading costs three
		/// array allocations, and a city with no extensions installed &mdash; which is nearly every
		/// city &mdash; must not pay them once a pass for nobody.
		/// </para>
		/// </summary>
		internal static bool AnyHappeningSource()
		{
			foreach (Binding binding in Registry())
			{
				if (binding.Extension is IKingdomHappeningSource)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Refusal lines from the last registration, for the founder's own report and for
		/// a bug report. Empty when nothing was refused.</summary>
		public static List<string> Refusals()
		{
			Registry();
			return new List<string>(Refused ?? new List<string>());
		}

	}
}
