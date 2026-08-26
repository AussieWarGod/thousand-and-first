using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Canonical authority reserved globally and on the exact site before publication. Names and
	/// water details stay in the receipt; their digest is the last member of this tuple.
	/// </summary>
	public struct KingdomFoundingAuthority
	{
		public KingdomFoundingKind Kind;
		public string TransactionID;
		public KingdomFoundingOwnerKind OwnerKind;
		public string OwnerNonce;
		public string RealmFaction;
		public string ZoneID;
		public int RiteX;
		public int RiteY;
		public string PayloadDigest;
	}

}
