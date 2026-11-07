using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Runner_Game
{
    /// <summary>
    /// Everything the player can do in a run (append new ones at the end so old saves still line up)
    /// </summary>
    public enum PlayerAction { Left, Right, Jump, Dash, Smash, Heal }

    /// <summary>
    /// Rebindable keys plus controller buttons for every action, the Runner and GameplayScreen both ask this instead of the keyboard
    /// </summary>
    public static class Controls
    {
        public static readonly Keys[] DefaultKeys = { Keys.Left, Keys.Right, Keys.Up, Keys.LeftShift, Keys.Down, Keys.H };
        private static readonly Keys[] OldDefaultKeys = { Keys.A, Keys.D, Keys.Space, Keys.LeftShift, Keys.S, Keys.H };  // what the first version had

        // The key picked in the Controls menu for each action
        public static Keys[] Bindings { get; private set; } = (Keys[])DefaultKeys.Clone();


        // Controller buttons (not rebindable for now, might add that later)
        private static readonly Buttons[][] PadButtons =
        {
            new[] { Buttons.DPadLeft, Buttons.LeftThumbstickLeft },
            new[] { Buttons.DPadRight, Buttons.LeftThumbstickRight },
            new[] { Buttons.A },
            new[] { Buttons.X, Buttons.LeftTrigger },
            new[] { Buttons.B, Buttons.RightTrigger },
            new[] { Buttons.Y },
        };
        private static readonly string[] PadNames = { "Left", "Right", "A", "X", "B", "Y" };

        private static KeyboardState _keyboard, _lastKeyboard;
        private static GamePadState _pad, _lastPad;
        private static Point _lastMousePosition;

        /// <summary>
        /// Reads the keyboard and controller once a frame (InputState calls this)
        /// </summary>
        public static void Update()
        {
            _lastKeyboard = _keyboard;
            _lastPad = _pad;
            _keyboard = Keyboard.GetState();
            _pad = GamePad.GetState(PlayerIndex.One);

            // Whatever got touched last wins, so the button prompts flip back and forth (oooh fancy)
            bool padTouched = _pad.IsConnected && (_pad.Buttons != default(GamePadButtons) || _pad.DPad != default(GamePadDPad)
                || _pad.ThumbSticks.Left.Length() > 0.3f || _pad.ThumbSticks.Right.Length() > 0.3f || _pad.Triggers.Left > 0.3f || _pad.Triggers.Right > 0.3f);
            var mouse = Mouse.GetState();
            bool mouseTouched = mouse.Position != _lastMousePosition || mouse.LeftButton == ButtonState.Pressed;
            _lastMousePosition = mouse.Position;
            if (padTouched) UsingPad = true;
            else if (_keyboard.GetPressedKeyCount() > 0 || mouseTouched || !_pad.IsConnected) UsingPad = false;
        }

        // True when the controller was the last thing used (prompts show Xbox buttons)
        public static bool UsingPad { get; private set; }

        /// <summary>
        /// True while the action is held
        /// </summary>
        public static bool IsDown(PlayerAction action) => Held(action, _keyboard, _pad);

        /// <summary>
        /// True only on the frame the action gets pressed
        /// </summary>
        public static bool IsPressed(PlayerAction action) => Held(action, _keyboard, _pad) && !Held(action, _lastKeyboard, _lastPad);

        private static bool Held(PlayerAction action, KeyboardState keyboard, GamePadState pad)
        {
            int i = (int)action;
            if (keyboard.IsKeyDown(Bindings[i])) return true;
            foreach (var button in PadButtons[i])
                if (pad.IsButtonDown(button)) return true;
            return false;
        }

        /// <summary>
        /// Binds a key, if another action already had it they just swap (no doubled up keys)
        /// </summary>
        public static void Bind(PlayerAction action, Keys key)
        {
            int other = Array.IndexOf(Bindings, key);
            if (other >= 0) Bindings[other] = Bindings[(int)action];
            Bindings[(int)action] = key;
        }

        public static void ResetToDefaults() => Bindings = (Keys[])DefaultKeys.Clone();

        /// <summary>
        /// Loads saved key names, anything missing or broken stays default
        /// </summary>
        public static void Load(string[] names)
        {
            ResetToDefaults();
            if (names == null) return;
            for (int i = 0; i < Math.Min(names.Length, Bindings.Length); i++)
                if (Enum.TryParse(names[i], out Keys key)) Bindings[i] = key;
            // Saves from before the arrow key defaults just get the new defaults
            if (Bindings.AsSpan().SequenceEqual(OldDefaultKeys)) ResetToDefaults();
        }

        public static string[] Save() => Array.ConvertAll(Bindings, k => k.ToString());

        /// <summary>
        /// Short name for the HUD, shows the controller button when one is plugged in
        /// </summary>
        public static string Label(PlayerAction action) => UsingPad ? PadNames[(int)action] : KeyName(Bindings[(int)action]);

        // LeftShift -> Shift, D1 -> 1, etc (oooh much nicer to read)
        public static string KeyName(Keys key)
        {
            string name = key.ToString();
            if (name.StartsWith("Left") && name.Length > 4) name = name[4..];
            else if (name.StartsWith("Right") && name.Length > 5) name = name[5..];
            if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) name = name[1..];
            return name;
        }
    }
}
