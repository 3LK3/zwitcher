using Zwitcher.Core.Switching;

namespace Zwitcher.Core.Input;

/// <summary>What the keyboard hook does with one key event.</summary>
public readonly record struct HookDecision(SwitcherCommand? Command, bool Swallow);
