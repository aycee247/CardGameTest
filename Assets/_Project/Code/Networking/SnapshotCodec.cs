using System.Text;
using Game.Core;
using Newtonsoft.Json;

namespace Game.Networking
{
    /// <summary>
    /// Serializes a <see cref="MatchSnapshot"/> to and from bytes for replication over NGO RPCs.
    ///
    /// JSON keeps the wire format decoupled from NGO's INetworkSerializable and avoids hand-writing
    /// serializers for the snapshot's nested arrays. Snapshots are sent on state changes rather than
    /// per frame, and a six-player snapshot is a few kilobytes, so the overhead is not worth
    /// optimising away before there is a measurement saying otherwise.
    ///
    /// Note that snapshots are per-recipient: the server encodes one *per player* (NET-2), so this
    /// runs once per client per broadcast, not once per broadcast.
    /// (Newtonsoft ships auto-referenced via com.unity.nuget.newtonsoft-json.)
    /// </summary>
    public static class SnapshotCodec
    {
        public static byte[] Encode(in MatchSnapshot snapshot)
        {
            var json = JsonConvert.SerializeObject(snapshot);
            return Encoding.UTF8.GetBytes(json);
        }

        public static MatchSnapshot Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return default;
            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<MatchSnapshot>(json);
        }
    }
}
