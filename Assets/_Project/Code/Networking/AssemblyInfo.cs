using System.Runtime.CompilerServices;

// The PlayMode netcode suite (STORY-2.1) drives the controller's server API directly and reads
// its test seams (received state bytes, the seat-key override), so it is let in.
[assembly: InternalsVisibleTo("Game.PlayModeTests")]
