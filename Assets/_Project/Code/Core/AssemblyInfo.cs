using System.Runtime.CompilerServices;

// The rules engine keeps its mutators internal so that only RulesEngine can drive state
// transitions. Tests need to arrange states directly — granting a card, setting Sparks — without
// playing a whole match to reach them, so the test assemblies are let in.
[assembly: InternalsVisibleTo("Game.EditModeTests")]
[assembly: InternalsVisibleTo("Game.PlayModeTests")]
