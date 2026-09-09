namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// Whether the menu this adapter reads is DRAWN right now. Every screen's <c>IsActive</c> asks
    /// its slot exactly this, so the slot's own type says the question can be asked
    /// (<c>LiveScreen&lt;TAdapter&gt;</c>, AGENTS.md "Screen Resolution").
    ///
    /// Readiness, not existence: the answer gates on the end state the menu's own coroutine leaves
    /// behind - a title written, a canvas at full alpha, a container activated, the entries
    /// instantiated - and never on the object merely being there.
    /// </summary>
    public interface IPresent
    {
        bool IsPresent();
    }
}
