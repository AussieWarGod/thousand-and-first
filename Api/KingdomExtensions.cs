using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

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
	/// <see cref="Draws"/> and the kernel (clause 1). Every call crosses
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
	public static class KingdomExtensions
	{
		/// <summary>
		/// One admitted extension and the mod that owns it. The mod name is captured once, at
		/// registration, because it is what every later fault line is attributed to.
		/// </summary>
		internal sealed class Binding
		{
			internal readonly string ModName;

			internal readonly string TypeName;

			internal readonly IKingdomExtension Extension;

			internal Binding(string modName, string typeName, IKingdomExtension extension)
			{
				ModName = modName;
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
				return (mod != 0) ? mod : string.CompareOrdinal(a.TypeName, b.TypeName);
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
				bool contract = extension is IKingdomAskSource || extension is IKingdomHappeningSource;
				KingdomExtensionVerdict verdict = (asked && extension != null)
					? KingdomApiRules.Judge(owner, declared, contract)
					: KingdomExtensionVerdict.RefusedThrew;
				if (verdict != KingdomExtensionVerdict.Accepted)
				{
					refused.Add(KingdomApiRules.RefusalLine(verdict, owner, declared));
					continue;
				}
				bound.Add(new Binding(owner, type.FullName ?? type.Name, extension));
			}
		}

		private static string OwnerOf(Type type)
		{
			if (type == null)
			{
				return "";
			}
			ModInfo mod = (type.Assembly == null) ? null : ModManager.GetMod(type.Assembly);
			if (mod != null && !string.IsNullOrEmpty(mod.DisplayTitleStripped))
			{
				return mod.DisplayTitleStripped;
			}
			string assembly = (type.Assembly == null) ? null : type.Assembly.GetName().Name;
			return string.IsNullOrEmpty(assembly) ? "" : assembly;
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

		// ==================================================================================
		// The draw handle (§6.6 clause 1)
		// ==================================================================================

		/// <summary>
		/// The kernel, wearing the published face. Every draw is
		/// <c>CounterRandom</c> over a <c>SemanticEventKey</c> on the extension's own stream, so an
		/// extension's chance is as replayable as ours and cannot shift ours.
		/// </summary>
		internal sealed class Draws : IKingdomDraws
		{
			private readonly KernelSeed128 seed;

			private readonly string settlementId;

			private readonly string modName;

			internal Draws(KernelSeed128 seed, string settlementId, string modName)
			{
				this.seed = seed;
				this.settlementId = settlementId;
				this.modName = modName;
			}

			/// <summary>Rules version pinned into every extension draw's key. It moves only if the
			/// draw is redefined in a way that must not compare equal to what came before.</summary>
			private const int ExtensionRulesVersion = 1;

			/// <summary>One kind code for the whole lane: domain separation comes from the stream,
			/// which already carries the mod and its own lane name.</summary>
			private const uint ExtensionKind = 1u;

			public bool TryBetween(string Lane, uint Ordinal, int Low, int High, out int Value)
			{
				Value = Low;
				if (High < Low)
				{
					return false;
				}
				string stream;
				if (!KingdomApiRules.TryStream(modName, Lane, out stream))
				{
					return false;
				}
				SemanticEventKey key;
				KernelFaultCode fault;
				if (!SemanticEventKey.TryCreate(ExtensionRulesVersion, settlementId, stream, ExtensionKind, Ordinal, out key, out fault))
				{
					return false;
				}
				ulong span = (ulong)((long)High - (long)Low + 1L);
				ulong drawn;
				if (!CounterRandom.TryDrawBelow(seed, key, 0u, span, out drawn, out fault))
				{
					return false;
				}
				Value = (int)((long)Low + (long)drawn);
				return true;
			}
		}

		// ==================================================================================
		// The two published lanes
		// ==================================================================================

		/// <summary>
		/// Every extension-taught ask, clamped and attributed.
		/// <para>
		/// Preconditions: <paramref name="Reading"/> is the frozen reading the board is being built
		/// from. Side effects: none beyond a log line per faulted source. Failure mode: a source
		/// that throws or runs past its lane's budget contributes nothing, is logged by mod name,
		/// and does not stop the rest.
		/// </para>
		/// </summary>
		internal static List<KingdomAsk> Asks(KingdomSystem System, KingdomCityReading Reading, List<string> Stalled)
		{
			List<KingdomAsk> asks = new List<KingdomAsk>();
			if (System == null || Reading == null)
			{
				return asks;
			}
			foreach (Binding binding in Registry())
			{
				IKingdomAskSource source = binding.Extension as IKingdomAskSource;
				if (source == null)
				{
					continue;
				}
				asks.AddRange(Run(System, Reading, source, binding.ModName, false, Stalled));
			}
			return asks;
		}

		/// <summary>
		/// One ask source across the seam, clamped and attributed. The city's own source goes
		/// through this call too, so a gap in the published contract is a gap in our own board
		/// first (&sect;6.6's reason for opening at W5 rather than W1).
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Reading">The frozen reading.</param>
		/// <param name="Source">The source. Null yields nothing.</param>
		/// <param name="Owner">Who to attribute a fault to, and whose slug prefixes the kinds.</param>
		/// <param name="Own">True for the city's own source: its kinds are already the board's own
		/// vocabulary, so they are not prefixed, and it answers to the board's cap rather than to
		/// the per-extension one.</param>
		/// <param name="Stalled">Collects the owner of a source that faulted, so the board can say
		/// so out loud. STANDARDS &sect;7b: a source that contributed nothing because it broke is
		/// applicable-but-blocked, and a log line is not somewhere the founder will see it.</param>
		internal static List<KingdomAsk> Run(KingdomSystem System, KingdomCityReading Reading, IKingdomAskSource Source, string Owner, bool Own = false, List<string> Stalled = null)
		{
			List<KingdomAsk> asks = new List<KingdomAsk>();
			if (System == null || Reading == null || Source == null)
			{
				return asks;
			}
			bool own = Own;
			AskJob job = new AskJob(Source, new Draws(System.SimulationSeed, Reading.SettlementId, Owner), Owner, own);
			KingdomComputeResult<KingdomAsk[]> result = KingdomCity.Seam.Submit(Reading, job);
			if (!result.Published)
			{
				Fault(Owner, "asks", result.Status.ToString());
				if (Stalled != null && !Stalled.Contains(Owner))
				{
					Stalled.Add(Owner);
				}
				return asks;
			}
			Keep(result.Value, own ? KingdomAskRules.MaxAsks : KingdomApiRules.MaxAsksPerSource, own ? null : Owner, Reading, asks);
			return asks;
		}

		/// <summary>
		/// Every extension-taught happening since the city last asked, recorded to the chronicle
		/// and pushed only while the pass's shared telling budget has a line to spare.
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Reading">The frozen reading.</param>
		/// <param name="Label">The city's name, for the word surface.</param>
		/// <param name="Here">Whether the founder is standing in this city.</param>
		/// <param name="SinceTick">The tick this lane was last asked.</param>
		/// <param name="NowTick">The pass's own clock, and the ceiling a notice may be dated at.
		/// Passed in rather than read off the reading: the book's processed-through tick can lag
		/// the pass by the part of a day it has not integrated yet, and a source that honestly
		/// dated a notice "now" would have it silently dropped as the future.</param>
		/// <param name="Spare">Told lines still unspent on this pass.</param>
		/// <returns>Lines actually pushed. Recording is unbudgeted; only the push is.</returns>
		internal static int Happenings(KingdomSystem System, KingdomCityReading Reading, string Label, bool Here, long SinceTick, long NowTick, int Spare)
		{
			int pushed = 0;
			if (System == null || Reading == null)
			{
				return 0;
			}
			foreach (Binding binding in Registry())
			{
				IKingdomHappeningSource source = binding.Extension as IKingdomHappeningSource;
				if (source == null)
				{
					continue;
				}
				HappeningJob job = new HappeningJob(source, SinceTick,
					new Draws(System.SimulationSeed, Reading.SettlementId, binding.ModName), binding.ModName);
				KingdomComputeResult<KingdomNotice[]> result = KingdomCity.Seam.Submit(Reading, job);
				if (!result.Published)
				{
					Fault(binding.ModName, "happenings", result.Status.ToString());
					continue;
				}
				pushed += Record(System, binding, result.Value, Label, Here, NowTick, SinceTick, Spare - pushed);
			}
			return pushed;
		}

		private static int Record(KingdomSystem system, Binding binding, KingdomNotice[] notices, string label, bool here, long nowTick, long sinceTick, int spare)
		{
			int pushed = 0;
			int kept = 0;
			for (int i = 0; notices != null && i < notices.Length && kept < KingdomApiRules.MaxNoticesPerSource; i++)
			{
				KingdomNotice notice = notices[i];
				string telling = KingdomApiRules.Trim(notice.Telling);
				string kind = KingdomApiRules.Kind(notice.Kind);
				if (string.IsNullOrEmpty(telling) || string.IsNullOrEmpty(kind))
				{
					continue;
				}
				// The city does not report the future, and it does not re-report what it already
				// told: a notice outside the window this lane was asked about is dropped rather
				// than filed with a wrong date.
				if (notice.Tick > nowTick || (sinceTick > 0L && notice.Tick < sinceTick))
				{
					continue;
				}
				kept++;
				KingdomChronicle.Record(system, telling);
				string spoken = KingdomApiRules.Trim(notice.Notice);
				if (pushed < spare && !string.IsNullOrEmpty(spoken))
				{
					KingdomWord.Ambient(system, label, here, spoken);
					pushed++;
				}
				KingdomLog.Log("extension happening: " + binding.ModName + " kind=" + kind + " tick=" + notice.Tick);
			}
			return pushed;
		}

		private static void Keep(KingdomAsk[] source, int limit, string modName, KingdomCityReading reading, List<KingdomAsk> into)
		{
			string prefix = string.IsNullOrEmpty(modName) ? "" : (KingdomApiRules.Slug(modName) + ":");
			int kept = 0;
			for (int i = 0; source != null && i < source.Length && kept < limit; i++)
			{
				KingdomAsk ask = source[i];
				string kind = KingdomApiRules.Kind(ask.Kind);
				string title = KingdomApiRules.Trim(ask.Title);
				if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(title))
				{
					continue;
				}
				kept++;
				into.Add(new KingdomAsk(
					prefix + kind,
					title,
					KingdomApiRules.Trim(ask.Want),
					Held(reading, ask.ZoneId),
					// Clamped DOWN, never up: an undefined weight is not a claim of urgency, and
					// clamping garbage to Grave would make it the loudest line on the board.
					(ask.Weight > KingdomAskWeight.Grave) ? KingdomAskWeight.Passing : ask.Weight));
			}
		}

		/// <summary>The ask's ground when the city actually holds it, and null otherwise. A board
		/// that named ground the city does not hold would send the founder somewhere that is not
		/// theirs, and the name would be fetched from the world to do it.</summary>
		private static string Held(KingdomCityReading reading, string zoneId)
		{
			if (reading == null || string.IsNullOrEmpty(zoneId))
			{
				return null;
			}
			for (int i = 0; i < reading.ZoneCount; i++)
			{
				KingdomZoneReading zone;
				if (reading.TryZone(i, out zone) && zone.ZoneId == zoneId)
				{
					return zoneId;
				}
			}
			return null;
		}

		private static void Fault(string owner, string lane, string status)
		{
			string line = owner + " stalled its own " + lane + " (" + status + "). The city is unaffected.";
			MetricsManager.LogError("ThousandAndFirst API: " + line);
			KingdomLog.Log("extension fault: " + owner + " lane=" + lane + " status=" + status);
		}

		// ==================================================================================
		// The jobs. Both cross the seam, so both inherit budget, timeout and error isolation
		// from the same contract our own computations do (§2.5).
		// ==================================================================================

		private sealed class AskJob : IKingdomComputation<KingdomCityReading, KingdomAsk[]>
		{
			private readonly IKingdomAskSource source;

			private readonly IKingdomDraws draws;

			private readonly string label;

			internal AskJob(IKingdomAskSource source, IKingdomDraws draws, string modName, bool own)
			{
				this.source = source;
				this.draws = draws;
				// The receipt distinguishes the city's own asks from an extension's, because
				// §6.5's whole point is that a regression has an owner.
				label = (own ? "asks:" : "ext:asks:") + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomCityReading input, out KingdomAsk[] output, out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Ask(input, draws);
				counters = KingdomComputeCounters.None;
				fault = KingdomCityFault.None;
				return true;
			}
		}

		private sealed class HappeningJob : IKingdomComputation<KingdomCityReading, KingdomNotice[]>
		{
			private readonly IKingdomHappeningSource source;

			private readonly long sinceTick;

			private readonly IKingdomDraws draws;

			private readonly string label;

			internal HappeningJob(IKingdomHappeningSource source, long sinceTick, IKingdomDraws draws, string modName)
			{
				this.source = source;
				this.sinceTick = sinceTick;
				this.draws = draws;
				label = "ext:happenings:" + KingdomApiRules.Slug(modName);
			}

			public string Label
			{
				get { return label; }
			}

			public KingdomBudgetLane Lane
			{
				get { return KingdomBudgetLane.Reckon; }
			}

			public bool TryRun(KingdomCityReading input, out KingdomNotice[] output, out KingdomComputeCounters counters, out KingdomCityFault fault)
			{
				output = source.Happen(input, sinceTick, draws);
				counters = KingdomComputeCounters.None;
				fault = KingdomCityFault.None;
				return true;
			}
		}
	}
}
