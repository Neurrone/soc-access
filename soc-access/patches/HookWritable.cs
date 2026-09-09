using System;

namespace SongsOfConquestAccess
{
    /// <summary>
    /// THE ANNOUNCEMENT SIDE OF A SCREEN OR ADAPTER: the members a Harmony patch is allowed to call.
    ///
    /// A patch may deliver an event or alter game behaviour, and it is never the source of truth for
    /// anything a <c>Build</c>, an <c>IsActive</c>, a <c>ScreenName</c> or a tooltip reads (AGENTS.md,
    /// Screen Resolution). What that permits a hook to reach for is narrow: a narrator queue, a
    /// refresh-and-announce, a pure reader that composes the event's payload, and the one capture the
    /// game hands over as an argument and keeps nowhere readable. Everything else a screen has to be
    /// able to arrive at by looking at the game, because a hot reload or a missed call is otherwise a
    /// screen that is right only the first time.
    ///
    /// The mark sits on the DECLARATION rather than in a list, so it survives a rename, greps in one
    /// line, and is read by whoever is about to add a caller. <c>HookCalledMemberLintTests</c> fails
    /// on any call a patch makes to an unmarked member of a type declared under <c>screens/</c> or
    /// <c>adapters/</c>; reads of a property or field are free, since a read cannot be the state a
    /// hook pushed in.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = false)]
    public sealed class HookWritableAttribute : Attribute
    {
    }
}
