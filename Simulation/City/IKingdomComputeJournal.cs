using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Where receipts go. W1 binds one that writes <c>KingdomLog</c>'s <c>[TAF]</c> lines;
	/// W0 ships the ring, so the seam has somewhere to put a receipt from its first commit.</summary>
	internal interface IKingdomComputeJournal
	{
		void Record(KingdomPerfReceipt receipt);
	}
}
