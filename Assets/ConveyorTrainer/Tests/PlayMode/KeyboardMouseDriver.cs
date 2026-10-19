using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ConveyorTrainer.Tests
{
    // Feed virtual devices in the player loop, before the official simulator consumes their controls.
    // This avoids the editor/test-runner input buffer being mistaken for the game input buffer.
    [DefaultExecutionOrder(-32000)]
    public sealed class KeyboardMouseDriver : MonoBehaviour
    {
        public Keyboard Keyboard { get; set; }
        public Mouse Mouse { get; set; }
        public bool SelectRight { get; set; }
        public bool Grip { get; set; }
        public bool Trigger { get; set; }
        public bool AppliedMousePress { get; private set; }
        private void Update()
        {
            var keyboard = SelectRight ? new KeyboardState(Key.Y) : Grip ? new KeyboardState(Key.G) : new KeyboardState();
            InputState.Change(Keyboard, keyboard);
            InputState.Change(Mouse, new MouseState().WithButton(MouseButton.Left, Trigger));
            AppliedMousePress = Mouse.leftButton.isPressed;
        }
    }
}
