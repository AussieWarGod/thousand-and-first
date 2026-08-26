using System;

namespace ThousandAndFirst.Api
{
	/// <summary>
	/// Marks a class as a kingdom extension. Discovery is the engine's own idiom &mdash;
	/// <c>ModManager.GetInstancesWithAttribute&lt;T&gt;(typeof(KingdomExtension))</c>, the same call
	/// <c>WorldFactory</c> uses for <c>IWorldBuilderExtension</c>
	/// (<c>D/XRL/World/WorldFactory.cs:108</c>) and <c>WishManager</c> for wishes
	/// (<c>D/XRL/Wish/WishManager.cs:43</c>).
	/// <para>
	/// The marked class must have a public parameterless constructor: the engine's scan builds it
	/// with <c>Activator.CreateInstance</c> (<c>D/XRL/ModManager.cs:1185-1196</c>). It must also
	/// implement at least one published contract, and declare the API version it was built against
	/// through <see cref="IKingdomExtension.ApiVersion"/>. A type that does neither is refused by
	/// mod name rather than skipped.
	/// </para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class KingdomExtensionAttribute : Attribute
	{
	}

	/// <summary>
	/// What every kingdom extension declares. The version is read once, at registration, and drift
	/// is refused loudly by mod name &mdash; LIVING-CITY-ARCHITECTURE &sect;6.6: <i>"a silently
	/// inactive extension is worse than a refused one, because the player attributes the missing
	/// behaviour to us."</i>
	/// </summary>
	public interface IKingdomExtension
	{
		/// <summary>The value of <c>KingdomApiRules.Version</c> this extension was compiled
		/// against. Return the constant, never a literal: recompiling against a newer mod is what
		/// re-admits the extension.</summary>
		int ApiVersion { get; }
	}

	/// <summary>
	/// The deterministic draw handle an extension is given. LIVING-CITY-ARCHITECTURE &sect;6.6
	/// clause 1: <b>kernel draws through our API only.</b> <c>System.Random</c> in an extension is
	/// a contract violation, because a settlement that replays differently after a reload is a
	/// settlement whose whole model is unfalsifiable.
	/// <para>
	/// Every extension lane owns its own stream, so an extension's ordinals can never shift ours
	/// or another mod's (&sect;2.4).
	/// </para>
	/// </summary>
	public interface IKingdomDraws
	{
		/// <summary>
		/// A uniform integer in <c>[Low, High]</c>, drawn from this extension's own stream.
		/// <para>
		/// Deterministic in the realm's seed, the settlement, the lane and the ordinal: the same
		/// four arguments in the same city always answer the same. Nothing here reads a clock.
		/// </para>
		/// </summary>
		/// <param name="Lane">The extension's own name for this stream of draws. Slugged into
		/// <c>taf:ext:&lt;mod&gt;:&lt;lane&gt;</c>.</param>
		/// <param name="Ordinal">Where in the lane this draw sits. Reusing an ordinal returns the
		/// same value, which is the point: an ordinal is an identity, not a counter.</param>
		/// <param name="Low">Inclusive lower bound.</param>
		/// <param name="High">Inclusive upper bound.</param>
		/// <param name="Value">The draw, or <paramref name="Low"/> when this returns false.</param>
		/// <returns>False when the lane will not fit the kernel's identifier grammar, the bounds are
		/// inverted, or this callback has attempted more than
		/// <c>KingdomApiRules.MaxDrawsPerSourceCall</c> draws. Invalid attempts consume that cap; the
		/// first over-cap attempt marks the whole callback over-budget, so it publishes no result. A
		/// refused draw is never quietly replaced by a different one.</returns>
		bool TryBetween(string Lane, uint Ordinal, int Low, int High, out int Value);
	}

	/// <summary>How badly the city wants a thing. Three rungs and no fourth: a scale a founder
	/// cannot read at a glance is a number, not a feeling.</summary>
	public enum KingdomAskWeight : byte
	{
		/// <summary>Worth doing. Nothing is failing.</summary>
		Passing = 0,

		/// <summary>Something is going wrong and will keep going wrong.</summary>
		Pressing = 1,

		/// <summary>People leave, or die, if this stands.</summary>
		Grave = 2
	}

	/// <summary>
	/// One thing the city is asking its founder for. LIVING-CITY-ARCHITECTURE &sect;5: petitions and
	/// bounties <i>"issue from model state"</i>, and W5's whole visible promise is that
	/// <i>"another mod can teach it a new thing to ask for."</i>
	/// <para>
	/// An ask is READ, never pressed. It carries no reward, no timer and no accept button, because
	/// the verbs that answer it are the Charter's own and already exist. A surface a founder can
	/// only look at is the mesh condition (BUILDING-CATALOGUE-BRIEF Addendum 13) holding.
	/// </para>
	/// </summary>
	public readonly struct KingdomAsk
	{
		/// <summary>A filing key in the extension's own words &mdash; <c>haulage</c>,
		/// <c>keeper</c>. Slugged; an ask whose kind slugs away to nothing is dropped.</summary>
		public readonly string Kind;

		/// <summary>The one line the board lists. What is wrong, in the fewest words that are
		/// still true.</summary>
		public readonly string Title;

		/// <summary>What would settle it. STANDARDS &sect;7b, applied forward: an ask that cannot
		/// say what would answer it is a complaint.</summary>
		public readonly string Want;

		/// <summary>Where, or null for the city as a whole. Must be the id of a zone on the
		/// reading; anything else is read as null, because a board that named ground the city does
		/// not hold would be worse than one that named none.</summary>
		public readonly string ZoneId;

		/// <summary>
		/// How badly. A value outside the three rungs is read as
		/// <see cref="KingdomAskWeight.Passing"/> &mdash; the mildest &mdash; and never as the
		/// gravest: a malformed weight is not a claim of urgency, and clamping upward would make
		/// garbage the loudest thing on the board.
		/// </summary>
		public readonly KingdomAskWeight Weight;

		/// <summary>Builds an ask. The board strips and clamps <see cref="Kind"/>,
		/// <see cref="Title"/> and <see cref="Want"/>, checks <see cref="ZoneId"/> against the
		/// reading's own zones, and clamps <see cref="Weight"/>. Nothing here trusts its own
		/// input.</summary>
		public KingdomAsk(string Kind, string Title, string Want, string ZoneId, KingdomAskWeight Weight)
		{
			this.Kind = Kind;
			this.Title = Title;
			this.Want = Want;
			this.ZoneId = ZoneId;
			this.Weight = Weight;
		}
	}

	/// <summary>
	/// One dated thing that happened in the city. LIVING-CITY-ARCHITECTURE &sect;4.1: a happening
	/// reads a frozen snapshot and <b>may not own state</b> &mdash; it returns what occurred, and
	/// the surfaces decide who hears about it, under &sect;4.2's shared budget.
	/// </summary>
	public readonly struct KingdomNotice
	{
		/// <summary>A filing key in the extension's own words. Slugged.</summary>
		public readonly string Kind;

		/// <summary>When, in <c>The.Game.TimeTicks</c>. A notice dated ahead of the reading is
		/// dropped: the city does not report the future.</summary>
		public readonly long Tick;

		/// <summary>The chronicle line, third person and past tense, as the book will carry it.
		/// Required; a notice with none is dropped.</summary>
		public readonly string Telling;

		/// <summary>The short line a settler says out loud when the pass has a line to spare, or
		/// null to record without ever interrupting. Under &sect;4.2 this is the only budgeted
		/// half of a notice.</summary>
		public readonly string Notice;

		/// <summary>Builds a notice.
		/// <para>
		/// <b>There is no place field, deliberately.</b> The two surfaces a notice reaches &mdash;
		/// the chronicle and <c>KingdomWord</c> &mdash; are realm-wide and city-wide respectively
		/// and neither takes a zone, so a <c>ZoneId</c> here would be a published input that went
		/// nowhere. Name the place in the telling until a surface exists that can use one.
		/// </para>
		/// </summary>
		public KingdomNotice(string Kind, long Tick, string Telling, string Notice)
		{
			this.Kind = Kind;
			this.Tick = Tick;
			this.Telling = Telling;
			this.Notice = Notice;
		}
	}

	/// <summary>
	/// Teaches the city a new thing to ask for. Called when the founder opens the asks board, over
	/// a frozen reading, inside the executor seam &mdash; so a source that throws or runs past its
	/// lane's budget stalls itself and nothing else (&sect;2.5, &sect;6.6 clause 3).
	/// <para>
	/// <b>The budget is a verdict, not a timeout.</b> The seam is synchronous today: it times the
	/// call and refuses to publish a result that overran, and it cannot interrupt one. An infinite
	/// loop in here still hangs the game. Return.
	/// </para>
	/// </summary>
	public interface IKingdomAskSource : IKingdomExtension
	{
		/// <summary>
		/// Returns what this source wants the city to ask for, or null for nothing.
		/// <para>
		/// Preconditions: none; <paramref name="City"/> is never null. Side effects: none are
		/// permitted &mdash; the reading is frozen, and the board publishes nothing an
		/// implementation writes elsewhere. Failure mode: throw, and the seam catches it, logs it
		/// by mod name, and drops this source's asks for this reading only.
		/// </para>
		/// <para>
		/// At most <c>KingdomApiRules.MaxAsksPerSource</c> are kept, in the order returned.
		/// </para>
		/// </summary>
		KingdomAsk[] Ask(KingdomCityReading City, IKingdomDraws Draws);
	}

	/// <summary>
	/// Teaches the city a new thing that can happen in it. Called on the settlement pass over a
	/// frozen reading, inside the executor seam, and surfaced through the ledger, the chronicle
	/// and <c>KingdomWord</c> under the shared telling budget &mdash; an extension cannot flood
	/// the register any more than we can (&sect;6.6 clause 4).
	/// </summary>
	public interface IKingdomHappeningSource : IKingdomExtension
	{
		/// <summary>
		/// Returns what happened since <paramref name="SinceTick"/>, or null for nothing.
		/// <para>
		/// Preconditions: <paramref name="SinceTick"/> is the tick this source was last asked, and
		/// is zero the first time. Side effects: none are permitted. Failure mode: throw, and the
		/// seam catches it, logs it by mod name, and drops this source's notices for this pass
		/// only.
		/// </para>
		/// <para>
		/// At most <c>KingdomApiRules.MaxNoticesPerSource</c> are kept.
		/// </para>
		/// </summary>
		KingdomNotice[] Happen(KingdomCityReading City, long SinceTick, IKingdomDraws Draws);
	}

	/// <summary>
	/// Answers identity questions for a modded culture or species without giving third-party code a
	/// creature or a city to mutate. Each call crosses the executor seam independently: a source
	/// that throws or overruns contributes no keys, or a neutral affinity, while every other source
	/// and the city continue.
	/// <para>
	/// This contract never touches research tiers. Extra keys may satisfy ordinary node or design
	/// requirements and affinity may shade existing work, but Intelligence remains the sole tier
	/// gate (BUILDING-CATALOGUE-BRIEF Addendum 17).
	/// </para>
	/// </summary>
	public interface IKingdomIdentitySource : IKingdomExtension
	{
		/// <summary>
		/// Returns extra live roster keys this identity carries, or null for none.
		/// <para>
		/// Preconditions: none; <paramref name="Identity"/> is a bounded frozen value. Side effects
		/// are not permitted. Failure mode: throw, and this source contributes no keys for this
		/// identity. At most <c>KingdomApiRules.MaxIdentityKeysPerSource</c> valid distinct keys are
		/// kept. Unqualified keys are filed under the owning mod's slug; a qualified key in another
		/// namespace is dropped, so an extension cannot mint somebody else's knowledge.
		/// </para>
		/// </summary>
		/// <param name="Identity">The bounded frozen identity. Never contains an engine object.</param>
		/// <returns>Proposed extra keys, or null for none.</returns>
		string[] Keys(KingdomIdentityReading Identity);

		/// <summary>
		/// Returns this identity's percentage affinity for one existing work kind. One hundred means
		/// no opinion. The host clamps every answer and the composed result to
		/// <c>[KingdomApiRules.MinIdentityAffinity, KingdomApiRules.MaxIdentityAffinity]</c>.
		/// <para>
		/// Preconditions: <paramref name="WorkKind"/> is a bounded canonical slug and may be empty.
		/// Side effects are not permitted. Failure mode: throw, and this source contributes the
		/// neutral 100 for this call.
		/// </para>
		/// </summary>
		/// <param name="Identity">The bounded frozen identity. Never contains an engine object.</param>
		/// <param name="WorkKind">The canonical existing work-kind slug; it may be empty.</param>
		/// <returns>A percentage affinity. One hundred is neutral.</returns>
		int Affinity(KingdomIdentityReading Identity, string WorkKind);
	}
}
