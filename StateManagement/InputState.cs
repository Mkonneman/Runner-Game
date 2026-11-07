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
            Controls.Update();
        }

        /// <summary>
        /// True only on the frame the key goes down
        /// </summary>
        /// The controller counts as the menu keys too (d-pad/stick = arrows, A = Enter/Space, B = Esc) so every menu works with it for free
        public bool IsNewKeyPress(Keys key)
        {
            if (CurrentKeyboardState.IsKeyDown(key) && LastKeyboardState.IsKeyUp(key)) return true;
            return key switch
            {
                Keys.Up => IsNewButtonPress(Buttons.DPadUp) || IsNewStick(Keys.Up),
                Keys.Down => IsNewButtonPress(Buttons.DPadDown) || IsNewStick(Keys.Down),
                Keys.Left => IsNewButtonPress(Buttons.DPadLeft) || IsNewStick(Keys.Left),
                Keys.Right => IsNewButtonPress(Buttons.DPadRight) || IsNewStick(Keys.Right),
                Keys.Enter or Keys.Space => IsNewButtonPress(Buttons.A),
                Keys.Escape => IsNewButtonPress(Buttons.B),
                _ => false,
            };
        }

        // The stick only counts as one direction at a time (whichever way it's pushed most) and has to be pushed past halfway
        // so going down the menu doesnt also bump a setting left or right
        private static Keys StickDirection(GamePadState pad)
        {
            var stick = pad.ThumbSticks.Left;
            if (stick.Length() < 0.6f) return Keys.None;
            if (System.MathF.Abs(stick.X) > System.MathF.Abs(stick.Y)) return stick.X > 0 ? Keys.Right : Keys.Left;
            return stick.Y > 0 ? Keys.Up : Keys.Down;  // stick Y is positive going up
        }

        private bool IsNewStick(Keys direction) => StickDirection(CurrentGamePadState) == direction && StickDirection(LastGamePadState) != direction;

        /// <summary>
        /// True only on the frame the button goes down
        /// </summary>
        public bool IsNewButtonPress(Buttons button)
        {
            return CurrentGamePadState.IsButtonDown(button) && LastGamePadState.IsButtonUp(button);
        }
    }

    /// <summary>
    /// The mouse in game coordinates (the window is scaled up, so window pixels get mapped back to the 854x480 game)
    /// </summary>
    public static class VirtualMouse
    {
        public static MouseState GetState()
        {
            var m = Mouse.GetState();
            var box = Game1.Letterbox;
            int x = (int)((m.X - box.X) * (float)Game1.VirtualWidth / box.Width);
            int y = (int)((m.Y - box.Y) * (float)Game1.VirtualHeight / box.Height);
            return new MouseState(x, y, m.ScrollWheelValue, m.LeftButton, m.MiddleButton, m.RightButton, m.XButton1, m.XButton2);
        }
    }
}
