using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Runner_Game.StateManagement
{
    /// <summary>
    /// Keeps track of the keyboard and gamepad between frames
    /// so screens can tell when a button was just pressed
    /// </summary>
    public class InputState
    {
        public KeyboardState CurrentKeyboardState { get; private set; }
        public KeyboardState LastKeyboardState { get; private set; }

        public GamePadState CurrentGamePadState { get; private set; }
        public GamePadState LastGamePadState { get; private set; }

        /// <summary>
        /// Reads the latest input (called once per frame by the ScreenManager)
        /// </summary>
        public void Update()
        {
            LastKeyboardState = CurrentKeyboardState;
            LastGamePadState = CurrentGamePadState;

            CurrentKeyboardState = Keyboard.GetState();
            CurrentGamePadState = GamePad.GetState(PlayerIndex.One);
        }

        /// <summary>
        /// True only on the frame the key goes down
        /// </summary>
        public bool IsNewKeyPress(Keys key)
        {
            return CurrentKeyboardState.IsKeyDown(key) && LastKeyboardState.IsKeyUp(key);
        }

        /// <summary>
        /// True only on the frame the button goes down
        /// </summary>
        public bool IsNewButtonPress(Buttons button)
        {
            return CurrentGamePadState.IsButtonDown(button) && LastGamePadState.IsButtonUp(button);
        }
    }
}
