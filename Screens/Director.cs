using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Runner_Game.Screens
{
    // Paces the obstacles like a song: a stretch where things show up on their own, then a hand made combo,
    // then a short breather (just diamonds) before it starts again. Early combos are simple and spaced out,
    // later ones mix everything and come closer together. The spawners still do the placing, so Fits keeps every combo beatable
    public partial class GameplayScreen
    {
        private enum Ob { Spike, Wall, Rock, Laser, Crystal, Icicle, Vine, Mushroom }

        // Name, lowest difficulty it shows up at, which cave (-1 = any), then the steps (what, seconds after the last one showed up)
        private static readonly (string Name, float From, int Cave, (Ob What, float Gap)[] Steps)[] Combos =
        {
            ("Hop scotch",    0.00f, -1, new[] { (Ob.Spike, 0f), (Ob.Spike, 1.2f), (Ob.Spike, 1.2f) }),
            ("Rock rain",     0.00f, -1, new[] { (Ob.Rock, 0f), (Ob.Rock, 0.5f), (Ob.Rock, 0.5f) }),
            ("Look up",       0.05f, -1, new[] { (Ob.Spike, 0f), (Ob.Rock, 0.6f) }),
            ("Smash and hop", 0.06f, -1, new[] { (Ob.Wall, 0f), (Ob.Spike, 1.4f) }),
            ("Hop and smash", 0.10f, -1, new[] { (Ob.Spike, 0f), (Ob.Wall, 0.8f) }),
            ("Laser trap",    0.25f, -1, new[] { (Ob.Laser, 0f), (Ob.Spike, 0.5f) }),
            ("Crossfire",     0.45f, -1, new[] { (Ob.Laser, 0f), (Ob.Rock, 0.4f), (Ob.Rock, 0.4f) }),
            ("Gauntlet",      0.60f, -1, new[] { (Ob.Spike, 0f), (Ob.Wall, 1.0f), (Ob.Rock, 0.5f), (Ob.Spike, 1.2f) }),
            // blue
            ("Icicle alley",  0.00f, 0, new[] { (Ob.Icicle, 0f), (Ob.Icicle, 0.9f), (Ob.Icicle, 0.9f) }),
            ("Cold feet",     0.05f, 0, new[] { (Ob.Spike, 0f), (Ob.Icicle, 0.6f) }),
            ("Frozen wall",   0.10f, 0, new[] { (Ob.Icicle, 0f), (Ob.Wall, 0.8f) }),
            // orange
            ("Crystal maze",  0.00f, 1, new[] { (Ob.Crystal, 0f), (Ob.Spike, 1.0f), (Ob.Crystal, 1.0f) }),
            ("Shard storm",   0.00f, 1, new[] { (Ob.Crystal, 0f), (Ob.Rock, 0.5f), (Ob.Crystal, 0.9f) }),
            ("Crystal wall",  0.00f, 1, new[] { (Ob.Wall, 0f), (Ob.Crystal, 1.2f) }),
            // green
            ("Bounce house",  0.00f, 2, new[] { (Ob.Mushroom, 0f), (Ob.Spike, 1.4f), (Ob.Mushroom, 1.4f) }),
            ("Jungle gym",    0.00f, 2, new[] { (Ob.Mushroom, 0f), (Ob.Vine, 1.6f) }),
            ("Tangled",       0.00f, 2, new[] { (Ob.Vine, 0f), (Ob.Wall, 1.4f) }),
            ("Overgrown",     0.75f, 2, new[] { (Ob.Vine, 0f), (Ob.Spike, 1.0f), (Ob.Mushroom, 1.2f) }),
        };

        // Speeds the whole world up over the run: a lazy 0.8x jog in blue up to 1.3x by the end of green
        private float Pace => MathHelper.Lerp(0.8f, 1.3f, Difficulty);

        private int _combo = -1, _lastCombo = -1, _step;
        private float _freeLeft = 18f;     // free play before the next combo (the first one waits a bit)
        private float _breather;           // calm after a combo, only diamonds
        private float _gapLeft, _stepTimeout;
        private int _spawns, _spawnsAtAsk; // every obstacle that shows up counts, so a step knows its thing arrived
        private bool? _forceWall;          // the spike spawner makes a wall (true) or a spike (false) for a combo
        private bool _swingHold;           // the vine swing pit is coming, it gets the screen to itself

        private void Direct(float dt)
        {
            _forceWall = null;
            if (Settings.OnlySpawn != Settings.Obstacle.Off || _endPhase != 0 || _swingHold || SweepQuiet) { _combo = -1; return; }

            if (_combo < 0)
            {
                if (_breather > 0) { _breather -= dt; Steer(null); return; }
                if ((_freeLeft -= dt) > 0) return;  // spawners run on their own timers
                PickCombo();
                if (_combo < 0) { _freeLeft = 2f; return; }
            }

            var (what, _) = Combos[_combo].Steps[_step];
            if (_gapLeft > 0)
            {
                Steer(null);
                if ((_gapLeft -= dt) <= 0) { _spawnsAtAsk = _spawns; _stepTimeout = 3f; }
                return;
            }
            if (_spawns > _spawnsAtAsk || (_stepTimeout -= dt) <= 0) { NextStep(); return; }  // it showed up (or couldn't fit for a while, skip it)
            Steer(what);
        }

        private void PickCombo()
        {
            int cave = CaveZones.Kind(CaveZones.At(_scrollTime).From);
            var options = new List<int>();
            for (int i = 0; i < Combos.Length; i++)
            {
                var c = Combos[i];
                if (i == _lastCombo || c.From > Difficulty || (c.Cave >= 0 && c.Cave != cave)) continue;
                options.Add(i);
                if (c.Cave >= 0) options.Add(i);  // the cave's own combos come up more
            }
            if (options.Count == 0) return;
            _combo = _lastCombo = options[RandomHelper.Next(0, options.Count - 1)];
            _step = 0; _gapLeft = 0.01f;
        }

        private void NextStep()
        {
            var steps = Combos[_combo].Steps;
            if (++_step < steps.Length) { _gapLeft = steps[_step].Gap * MathHelper.Lerp(1.25f, 0.8f, Difficulty); return; }  // tighter later on
            _combo = -1;
            _breather = MathHelper.Lerp(3f, 1.2f, Difficulty);
            _freeLeft = RandomHelper.NextFloat(MathHelper.Lerp(8f, 3f, Difficulty), MathHelper.Lerp(12f, 5f, Difficulty));
        }

        // Holds every spawner except the one the combo wants right now (null = hold them all)
        private void Steer(Ob? want)
        {
            float Hold(float t, bool on) => on ? MathF.Min(t, 0f) : MathF.Max(t, 1f);
            _spawnTimer = want == Ob.Rock ? SpawnInterval : 0;
            _spikeTimer = Hold(_spikeTimer, want is Ob.Spike or Ob.Wall);
            _laserTimer = Hold(_laserTimer, want == Ob.Laser);
            _gemTimer = Hold(_gemTimer, want == Ob.Crystal);
            _iceTimer = Hold(_iceTimer, want == Ob.Icicle);
            _vineTimer = Hold(_vineTimer, want == Ob.Vine);
            _mushTimer = Hold(_mushTimer, want == Ob.Mushroom);
            _forceWall = want == Ob.Wall ? true : want == Ob.Spike ? false : null;
        }
    }
}
