using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IndieGame.Core
{
    /// <summary>
    /// Single-key polling for development overlays.
    ///
    /// The legacy <c>UnityEngine.Input</c> class throws when Active Input Handling is
    /// set to "Input System Package (New)", which would make the HUD hotkeys a
    /// runtime exception rather than a missing feature. This routes to whichever
    /// backend the project is actually configured for.
    /// </summary>
    public static class HotKey
    {
        public static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            Key mapped = Map(key);
            if (mapped == Key.None) return false;

            return keyboard[mapped].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(key);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static Key Map(KeyCode key)
        {
            if (key >= KeyCode.A && key <= KeyCode.Z)
                return Key.A + (key - KeyCode.A);
            if (key >= KeyCode.F1 && key <= KeyCode.F12)
                return Key.F1 + (key - KeyCode.F1);
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return Key.Digit0 + (key - KeyCode.Alpha0);

            switch (key)
            {
                case KeyCode.Space: return Key.Space;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.BackQuote: return Key.Backquote;
                default: return Key.None;
            }
        }
#endif
    }
}
