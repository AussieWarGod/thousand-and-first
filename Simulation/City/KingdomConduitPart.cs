using System;

using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

// XRL.World.Parts, for the reason r_KingdomPlot states: GamePartBlueprint resolves a part named in
// XML as exactly "XRL.World.Parts.<Name>" and tries no other name (GamePartBlueprint.cs:178, :240).
namespace XRL.World.Parts
{
	/// <summary>
	/// One segment of a typed liquid line.
	/// <para>
	/// <b>Why this part exists at all, on the record.</b> Vanilla's hydraulic family does not move
	/// liquid. <c>HydraulicPowerTransmission</c> carries <b>joules</b> through liquid, not liquid
	/// through pipes: its constructor sets <c>Substance = "power"</c>, <c>Unit = "joule"</c>,
	/// <c>DependsOnLiquid = "water"</c> and <c>WrongLiquidFactor = 0.2</c>
	/// (<c>D/XRL/World/Parts/HydraulicPowerTransmission.cs:13-20</c>), each segment holds a real
	/// <c>LiquidVolume</c> as working fluid, and its only liquid motion is
	/// <c>MingleAdjacent</c> (<c>D/XRL/World/Parts/IPowerTransmission.cs:1605</c>) which
	/// <i>equalises with directly adjacent volumes and routes nothing</i>. Dram movement is
	/// <c>LiquidPump</c>'s job, and <c>LiquidPump</c> has no live carrier: all three references to
	/// it in shipped content are inside XML comments
	/// (<c>B/ObjectBlueprints/Furniture.xml:1380, :2134, :2217</c>). So liquid piping is tier 3 of
	/// Addendum 11(c) &mdash; <i>fill in, in vanilla's idiom</i>.
	/// </para>
	/// <para>
	/// <b>And this is that idiom, extended by exactly one verb.</b> The blueprint carries the
	/// hydraulic family's own segment shape &mdash; a sealed <c>LiquidVolume</c> of a few drams,
	/// which is what <c>BaseHydraulicPipe</c> declares (<c>Furniture.xml:858-870</c>:
	/// <c>MaxVolume="8" StartVolume="8" InitialLiquid="water-1000" Sealed="true"</c>) &mdash; and
	/// this part adds the missing verb: <b>transfer along declared topology</b>. Nothing else is
	/// invented. Wrong-liquid-in-the-line already has vanilla vocabulary and vanilla consequence,
	/// which is what makes typed lines cheap.
	/// </para>
	/// <para>
	/// <b>The LIQUID LAW</b> (BUILDING-CATALOGUE-BRIEF, 2026-08-22): connection is DECLARED, never
	/// inferred. <see cref="Liquid"/> types the line and <see cref="Joins"/> declares which faces
	/// offer a join. Two segments meet only when both declare toward each other, and two lines
	/// carrying different liquids <b>refuse by name</b> rather than merging. Mixtures are a future
	/// mixing work; nothing here mixes anything.
	/// </para>
	/// </summary>
	[Serializable]
	public partial class r_KingdomLiquidConduit : IPart
	{
		/// <summary>
		/// What this line carries, by vanilla liquid id &mdash; <c>water</c>, <c>salt</c>,
		/// <c>blood</c> (<c>D/XRL/Liquids/LiquidWater.cs:17</c> and siblings). Untrusted from XML:
		/// a segment that never says what it carries joins nothing at all, rather than joining
		/// whatever it happens to touch.
		/// </summary>
		public string Liquid = "water";

		/// <summary>
		/// Which faces offer a join, as any of <c>N</c>, <c>S</c>, <c>E</c>, <c>W</c>. An
		/// unreadable value declares nothing, which caps the segment rather than opening it.
		/// </summary>
		public string Joins = "EW";

		/// <summary>Set once the founder has been told this segment would not join what it touches,
		/// so a refused main says so once rather than at every homecoming (STANDARDS 7b).</summary>
		public bool RefusalAnnounced;

		/// <summary>The declaration as a mask, or nothing when the XML could not be read.</summary>
		public int JoinMask
		{
			get
			{
				int mask;
				return ThousandAndFirst.Simulation.City.KingdomNetworkRules.TryParseJoins(Joins, out mask) ? mask : 0;
			}
		}

		/// <summary>
		/// A conduit's arrival or departure changes the SHAPE of a network, which is the one thing
		/// that may invalidate a graph. Topology never changes on time and never on stock
		/// (LIVING-CITY-ARCHITECTURE &sect;3.11, and the identical discipline
		/// <c>KingdomDistanceMatrix.MarkDirty</c> keeps for the distance matrix).
		/// </summary>
		public override void Attach()
		{
			KingdomNetworks.MarkTopologyChanged();
			base.Attach();
		}

		public override void Remove()
		{
			KingdomNetworks.MarkTopologyChanged();
			base.Remove();
		}
	}

	/// <summary>
	/// The crossover: two lines through one tile, and they never meet.
	/// <para>
	/// The second clause of the LIQUID LAW made physical. Because joining is declared and not
	/// inferred, a piece that pairs opposite faces &mdash; north to south, east to west &mdash; and
	/// pairs nothing else lets a brine main and a fresh main share ground with no possibility of a
	/// merge. It carries <b>no liquid of its own</b>, deliberately: a piece that typed anything
	/// could be the place two liquids met, and this one cannot be, because it never holds a
	/// declaration to disagree with.
	/// </para>
	/// </summary>
	[Serializable]
	public partial class r_KingdomLiquidCrossover : IPart
	{
		/// <summary>
		/// Which faces are paired through. <c>NSEW</c> is the ordinary cross: north-south is one
		/// route and east-west is the other. <c>NS</c> alone is a piece that only carries the one
		/// way, which is a legal and occasionally useful thing to lay.
		/// </summary>
		public string Pairs = "NSEW";

		public int PairMask
		{
			get
			{
				int mask;
				return ThousandAndFirst.Simulation.City.KingdomNetworkRules.TryParseJoins(Pairs, out mask) ? mask : 0;
			}
		}

		public override void Attach()
		{
			KingdomNetworks.MarkTopologyChanged();
			base.Attach();
		}

		public override void Remove()
		{
			KingdomNetworks.MarkTopologyChanged();
			base.Remove();
		}
	}

	/// <summary>
	/// The tap: where a line meets a vessel.
	/// <para>
	/// A line by itself moves nothing, because a network's nodes are works and containers rather
	/// than pipes. A tap is the declared join between a length of main and whatever dedicated
	/// vessel stands in its own cell &mdash; so the founder's act of tapping a cistern is what puts
	/// that cistern on the line, and standing near one is not.
	/// </para>
	/// <para>
	/// It is a conduit in every other respect, and carries the same typed declaration, so a tap on
	/// a brine main refuses a fresh cistern by name exactly as two mains would refuse each other.
	/// </para>
	/// </summary>
	[Serializable]
	public partial class r_KingdomLiquidTap : IPart
	{
		/// <summary>What this tap's line carries. Same vocabulary, same untrusted parse.</summary>
		public string Liquid = "water";

		/// <summary>Which faces offer a join to the main.</summary>
		public string Joins = "EW";

		/// <summary>Set once the founder has been told this tap would not take from what it
		/// touches (STANDARDS 7b).</summary>
		public bool RefusalAnnounced;

		public int JoinMask
		{
			get
			{
				int mask;
				return ThousandAndFirst.Simulation.City.KingdomNetworkRules.TryParseJoins(Joins, out mask) ? mask : 0;
			}
		}

		public override void Attach()
		{
			KingdomNetworks.MarkTopologyChanged();
			base.Attach();
		}

		public override void Remove()
		{
			KingdomNetworks.MarkTopologyChanged();
			base.Remove();
		}
	}
}
