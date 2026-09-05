using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using ThousandAndFirst;

namespace XRL.World.Parts
{
	/// <summary>One frozen observation of an actual raid-mint creation event. Immutable: every
	/// field is copied once from the raw entry snapshot the handler froze at callback entry, because
	/// the operation and its projections keep moving after the callback returns.</summary>
	public sealed class r_TAF_RaidMintObservation
	{
		public readonly int Sequence;
		public readonly string RequestedBlueprint;
		public readonly string Context;
		public readonly KingdomLifecycleOperation Raid;
		public readonly string OperationId;
		public readonly KingdomLifecyclePhase Phase;
		public readonly int ProjectionCount;
		public readonly string ProjectionStates;
		public readonly int MintIndex;
		public readonly int ProvedBefore;
		public readonly KingdomLifecyclePhysicalState MintState;
		public readonly string MintBlueprint;
		public readonly string MintObjectId;
		/// <summary>Callback-time physical snapshot of every actor an earlier callback returned.
		/// The array is built once at entry and never written after construction.</summary>
		public readonly r_TAF_RaidMintPriorActor[] Priors;
		public readonly GameObject Original;
		public readonly GameObject Substitute;
		public readonly string SubstituteBlueprint;
		public readonly string Fault;
		public readonly long Tick;
		public readonly DateTime Utc;

		/// <summary>Copies the frozen entry, then attaches the substitute the callback made. Only
		/// the last three fields can have changed after entry.</summary>
		internal r_TAF_RaidMintObservation(r_TAF_RaidMintEntry Entry)
		{
			Sequence = Entry.Sequence;
			RequestedBlueprint = Entry.RequestedBlueprint;
			Context = Entry.Context;
			Raid = Entry.Raid;
			OperationId = Entry.OperationId;
			Phase = Entry.Phase;
			ProjectionCount = Entry.ProjectionCount;
			ProjectionStates = Entry.ProjectionStates;
			MintIndex = Entry.MintIndex;
			ProvedBefore = Entry.ProvedBefore;
			MintState = Entry.MintState;
			MintBlueprint = Entry.MintBlueprint;
			MintObjectId = Entry.MintObjectId;
			Priors = Entry.Priors;
			Original = Entry.Original;
			Tick = Entry.Tick;
			Utc = Entry.Utc;
			Substitute = Entry.Substitute;
			SubstituteBlueprint = Entry.SubstituteBlueprint;
			Fault = Entry.Fault;
		}
	}

	/// <summary>Dev-only raid-mint probe, attached to the two shipped Snapjaw raider blueprints by
	/// Harness/ObjectBlueprints.xml (<c>Load="Merge"</c>). That overlay is copied into a throwaway
	/// scenario profile by Tools/prepare-scenario.sh and reaches no staged or Workshop package.
	/// <para>Inert unless a native fixture arms it: with <see cref="Armed"/> false every handler
	/// returns immediately, so ordinary play is unaffected.</para>
	/// <para>Namespace and the Register/WantEvent/HandleEvent shape mirror the shipped mod part
	/// Raids/r_KingdomRaiderObjective.cs:12-15,34-43 &mdash; an XML <c>&lt;part Name="..." /&gt;</c>
	/// row resolves under <c>XRL.World.Parts</c>, which is why this dev shard declares that
	/// namespace rather than ThousandAndFirst.Harness.</para>
	/// <para><c>BeforeObjectCreatedEvent</c> is the creation event that exposes a writable
	/// <c>ReplacementObject</c> to the new object's own parts: it carries public
	/// <c>Object</c>/<c>Context</c>/<c>ReplacementObject</c> fields inherited from
	/// <c>IObjectCreationEvent</c>, and <c>Process(GameObject, string, ref GameObject)</c> reads the
	/// replacement back after dispatching to those parts (design rev2 sec 1, citing decompiled
	/// BeforeObjectCreatedEvent.cs:5,42-45,56-76, IObjectCreationEvent.cs:6-10 and
	/// GameObjectFactory.cs:1445,1482-1499,1625-1635). The event is pooled, so nothing here retains
	/// <c>E</c>; only <c>E.Object</c>, its blueprint name and the context string are copied out.
	/// <c>ObjectCreatedEvent</c>/<c>AfterObjectCreatedEvent</c> expose the same field but fire after
	/// the factory has already read the replacement, so the Before event is the one used.</para>
	/// <para>No handler here ever throws. An assertion raised inside the factory's own callback
	/// region is swallowed (GameObjectFactory.cs:1163-1169) and silently becomes an
	/// "[invalid blueprint:...]" substitute, so this part records and the fixture asserts
	/// afterwards, outside engine dispatch.</para>
	/// </summary>
	[Serializable]
	public sealed class r_TAF_RaidMintProbe : IPart
	{
		private const int MaxObservations = 32;
		/// <summary>The one live handle. False makes every handler a no-op.</summary>
		internal static volatile bool Armed;
		/// <summary>Lifecycle book whose open Raid operation each observation records by
		/// reference. Set by the fixture once the profile is actually founded.</summary>
		internal static KingdomLifecycleBook Book;
		/// <summary>1-based creation index to substitute at; 0 never substitutes.</summary>
		internal static int SubstituteAtSequence;
		/// <summary>Blueprint of the harmless, probe-free substitute object.</summary>
		internal static string SubstituteBlueprint;
		/// <summary>The raw entry snapshots, retained in creation order. An entry is added BEFORE
		/// its callback substitutes anything, so every body, substitute and abandoned original the
		/// probe ever saw stays strongly reachable through this static list.</summary>
		private static readonly List<r_TAF_RaidMintEntry> Entries = new List<r_TAF_RaidMintEntry>();
		private static readonly List<r_TAF_RaidMintObservation> Ledger =
			new List<r_TAF_RaidMintObservation>();
		private static int Sequence;
		private static bool Substituting;

		/// <summary>Arms the plan. Creation dispatch is synchronous on the creating thread
		/// (design rev2 sec 1), so the ledger below needs no lock.</summary>
		internal static void Arm(int SubstituteAtSequence, string SubstituteBlueprint)
		{
			r_TAF_RaidMintProbe.SubstituteAtSequence = SubstituteAtSequence;
			r_TAF_RaidMintProbe.SubstituteBlueprint = SubstituteBlueprint;
			Sequence = 0;
			Substituting = false;
			Armed = true;
		}

		/// <summary>True only while nothing is retained, so a ResetProbe would erase no evidence. The
		/// verb refuses rather than resets when this is false.</summary>
		internal static bool Vacant { get { return Ledger.Count == 0 && Entries.Count == 0; } }

		/// <summary>Raw entry snapshots retained, which the ledger must match exactly.</summary>
		internal static int Retained { get { return Entries.Count; } }

		/// <summary>ENTRY-ONLY, and only over an empty ledger: the caller must prove
		/// <see cref="Vacant"/> first. Teardown after a run is disarming and nothing else, so no
		/// object, body, substitute, abandoned original or recorded observation is ever removed.
		/// </summary>
		internal static void ResetProbe()
		{
			Armed = false;
			Entries.Clear();
			Ledger.Clear();
			Sequence = 0;
			Substituting = false;
			SubstituteAtSequence = 0;
			SubstituteBlueprint = null;
			Book = null;
		}

		internal static r_TAF_RaidMintObservation[] Snapshot()
		{
			return Ledger.ToArray();
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == BeforeObjectCreatedEvent.ID;
		}

		public override bool HandleEvent(BeforeObjectCreatedEvent E)
		{
			Observe(E);
			return base.HandleEvent(E);
		}

		private void Observe(BeforeObjectCreatedEvent E)
		{
			if (!Armed || Substituting || E == null || E.Object == null) return;
			int sequence = 0;
			r_TAF_RaidMintEntry entry = null;
			try
			{
				// Sequence counts every probed-blueprint creation while armed: a foreign Snapjaw
				// minted mid-activation shifts the B1 substitution target and fails loudly via
				// VerifyMint, rather than silently substituting the wrong mint.
				sequence = ++Sequence;
				// ENTRY FREEZE, before anything else this callback does: the authority and the
				// callback-time physical placement of every actor an earlier callback returned are
				// read here, so no later substitution can move what this row reports.
				entry = new r_TAF_RaidMintEntry(sequence, E.Object, E.Context,
					Book == null ? null : Book.Raid, r_TAF_RaidMintSnapshots.Priors(Entries));
				if (!ReferenceEquals(E.Object, ParentObject))
					entry.Fault = Append(entry.Fault,
						"creation event object is not the probe's own parent");
				if (Entries.Count >= MaxObservations)
				{
					entry.Fault = Append(entry.Fault, "native observation ledger is over bound");
					Entries.Add(entry);
				}
				else
				{
					// Retained BEFORE the substitution runs and BEFORE the immutable observation is
					// built from it, so a refusal in either still leaves the bodies reachable.
					Entries.Add(entry);
					if (SubstituteAtSequence > 0 && sequence == SubstituteAtSequence
						&& !string.IsNullOrEmpty(SubstituteBlueprint))
					{
						GameObject made = null;
						string blueprint = null;
						try { entry.Fault = Append(entry.Fault, Substitute(E, out made, out blueprint)); }
						finally
						{
							entry.Substitute = made;
							entry.SubstituteBlueprint = blueprint;
						}
					}
				}
			}
			catch (Exception error)
			{
				try { entry = Fault(entry, sequence, E.Object, Describe(error)); }
				catch (Exception) { }
			}
			try
			{
				if (entry != null && Ledger.Count < MaxObservations)
					Ledger.Add(new r_TAF_RaidMintObservation(entry));
			}
			catch (Exception error)
			{
				// The record is never dropped: the entry keeps every reference, and the fault is
				// appended to it so the checks can see that an observation went missing.
				try { Fault(entry, sequence, E.Object, Describe(error)); } catch (Exception) { }
			}
		}

		/// <summary>Appends a fault to the retained entry, retaining a fault-only entry first when
		/// the frozen snapshot itself refused, so the body is reachable either way.</summary>
		private static r_TAF_RaidMintEntry Fault(r_TAF_RaidMintEntry Entry, int Sequence,
			GameObject Body, string Detail)
		{
			r_TAF_RaidMintEntry entry = Entry;
			if (entry == null && Entries.Count < MaxObservations)
			{
				entry = new r_TAF_RaidMintEntry(Sequence, Body);
				Entries.Add(entry);
			}
			if (entry != null) entry.Fault = Append(entry.Fault, Detail);
			return entry;
		}

		/// <summary>Writes the event's own ReplacementObject field. The substitute is a different,
		/// deliberately probe-free blueprint, so it fires no nested observation; the guard flag is
		/// belt and braces. GameObject.Create precedent: Raids/KingdomRaids.09.*.cs:74 and
		/// Harness/KingdomQuickstartBootstrap.NativeCreators.cs:113.</summary>
		private static string Substitute(BeforeObjectCreatedEvent E, out GameObject Made,
			out string Blueprint)
		{
			Made = null;
			Blueprint = null;
			if (E.ReplacementObject != null)
				return "another handler already claimed the replacement slot";
			Substituting = true;
			try { Made = GameObject.Create(SubstituteBlueprint); }
			finally { Substituting = false; }
			if (Made == null) return "the configured substitute blueprint refused creation";
			Blueprint = SubstituteBlueprint;
			E.ReplacementObject = Made;
			return null;
		}

		private static string Describe(Exception Error)
		{
			string kind = Error.GetType().Name;
			try { return kind + ": " + Error.Message; }
			catch (Exception) { return kind; }
		}

		private static string Append(string Fault, string Detail)
		{
			if (string.IsNullOrEmpty(Detail)) return Fault;
			return string.IsNullOrEmpty(Fault) ? Detail : Fault + "; " + Detail;
		}
	}
}
