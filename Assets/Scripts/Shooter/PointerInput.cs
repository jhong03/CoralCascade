using UnityEngine;
using UnityEngine.InputSystem;

namespace CoralCascade
{
    /// <summary>
    /// Thin wrapper over the new Input System (this project has legacy input disabled:
    /// activeInputHandler = 1). Unifies mouse and touch into a simple pressed / released /
    /// position model so the launcher doesn't care which device is driving it.
    /// </summary>
    public static class PointerInput
    {
        public static bool IsPressed
        {
            get
            {
                var touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.isPressed) return true;
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.isPressed;
            }
        }

        public static bool PressedThisFrame
        {
            get
            {
                var touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasPressedThisFrame;
            }
        }

        public static bool ReleasedThisFrame
        {
            get
            {
                var touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.wasReleasedThisFrame) return true;
                var mouse = Mouse.current;
                return mouse != null && mouse.leftButton.wasReleasedThisFrame;
            }
        }

        /// <summary>Pointer position in screen pixels, or screen center if no device.</summary>
        public static Vector2 ScreenPosition
        {
            get
            {
                var touch = Touchscreen.current;
                // Include the release frame: isPressed is already false then, but the shot
                // fires on release and must aim at where the finger lifted, not a stale mouse.
                if (touch != null && (touch.primaryTouch.press.isPressed ||
                                      touch.primaryTouch.press.wasReleasedThisFrame))
                    return touch.primaryTouch.position.ReadValue();
                var mouse = Mouse.current;
                if (mouse != null) return mouse.position.ReadValue();
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
        }
    }
}
