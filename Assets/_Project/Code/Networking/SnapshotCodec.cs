using System.Text;
using Game.Core;
using Newtonsoft.Json;

namespace Game.Networking
{
    /// <summary>
    /// Serializes <see cref="GameStateSnapshot"/> to/from bytes for replication over NGO RPCs.
    /// Using JSON keeps the wire format decoupled from NGO's INetworkSerializable and avoids
    /// hand-writing serializers for the snapshot's nested arrays. For a turn-based game the
    /// snapshot is small and sent only on state changes, so JSON overhead is negligible.
    /// (Newtonsoft ships auto-referenced via com.unity.nuget.newtonsoft-json.)
    /// </summary>
    public static class SnapshotCodec
    {
        public static byte[] Encode(in GameStateSnapshot snapshot)
        {
            var json = JsonConvert.SerializeObject(snapshot);
            return Encoding.UTF8.GetBytes(json);
        }

        public static GameStateSnapshot Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return default;
            var json = Encoding.UTF8.GetString(bytes);
            return JsonConvert.DeserializeObject<GameStateSnapshot>(json);
        }
    }
}
