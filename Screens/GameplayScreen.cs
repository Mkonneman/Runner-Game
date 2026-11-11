using System.Linq;
using CollisionExercise.Collisions;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// The actual game (pushed on top of the title screen)
    /// </summary>
    public class GameplayScreen : GameScreen
    {
        private ContentManager _content;

        // The Player
        private Runner _runner;


        // Audio Handling for the game
        private Song _runnerMusic;
        private SoundEffect _crashSound;
        private SoundEffect _swingSound;   // whoosh on every hammer swing, hit or miss
        private SoundEffect _laserChargeSound, _laserFireSound, _wallWarningSound;

        // Makes multiple bolders
        private List<Bolders> _bolders = new();

        // Rocks drop anywhere across the screen between these X positions
        private const float RockMinX = 80f;
        private const float RockMaxX = 774f;
        private float _spawnTimer;

        // Crystal spikes stuck in the floor that slide in from the right (jump over them)
        private Texture2D _spikeTexture;
        private Texture2D _wallTexture;
        private Texture2D _hammerTexture;

        // Dash cooldown icon (top right)
        private Texture2D _dashIcon;
        private Texture2D _smashIcon;

        // How visible the smash range ring is (0 turns it off), set in the settings menu
        private static float SmashRingOpacity => Settings.SmashRingOpacity;

        // F1 shows the hit boxes (for checking unfair hits)

        // The lantern he carries swings as he runs and drops embers (Makes him way easier to see)
        private Texture2D _lanternTexture;
        private Texture2D _glowTexture;
        private SparkParticleSystem _embers;
        private float _lanternTime;
        private Texture2D _pixel;
        private readonly List<(float X, bool IsWall)> _spikes = new();  // X of each obstacle and whether it's a breakable wall or a crystal spike
        private float _spikeTimer = 4f;                   // seconds until the next one
        private const float SpikeSpeed = 260f;            // same as the floor so they look stuck in it
        private const float SpikeBottom = 471f;           // floor line where his feet touch
        private static readonly Rectangle SpikeSource = new Rectangle(27, 0, 74, 80); // top of the crystal (the rest is "in the floor")
        private const float WallWarningDistance = 267f;   // how far off screen walls start (about 0.8s of ! warning at 260px/s)
        private const float WallReach = 80f;              // how close the wall has to be for the smash to hit it
        private float _swipeReach = WallReach;            // the swipe keeps the reach it was swung with
        private float SmashCooldown;                      // seconds between smashes, from the equipped weapon (a missed smash still uses it)

        // Gear from the Gear screen, one weapon, one ability and one armor
        private ItemId? _weapon, _ability, _armor;  // null = nothing in that slot
        private Texture2D _weaponIcon, _abilityIcon;
        private float _abilityCooldown, _abilityCooldownLeft;  // abilities other than Dash keep their own cooldown here
        private float _bubbleLeft;                        // Bubble Shield is up
        private float _slowLeft;                          // Time Slow is running
        private float _safeTime;                          // can't be touched at all (bubble just popped or Phoenix Heart saved you)
        private bool _revived;                            // Phoenix Heart only works once a run
        private readonly List<float> _quakes = new();     // X of each Quake Hammer shockwave sliding along the floor
        private readonly List<(Vector2 Pos, float Angle)> _rockets = new();  // rockets and the way they point, flying right (placeholder look till the art is in)
        private const float QuakeSpeed = 520f;
        private const float RocketSpeed = 700f;
        private const int SpikePoints = 25;
        // Gemstones, breakable crystals on the floor (and hanging from the roof in the middle cave)
        private readonly List<(float X, bool Top, Color Tint, float Tip)> _gems = new();  // Tip = how far down a hanging one reaches (bigger gem = lower)
        private static readonly Color[] GemColors = { new(255, 90, 200), new(170, 90, 255), new(70, 230, 120), new(255, 70, 70), new(255, 210, 60), new(80, 220, 255) };  // ruby, emerald, topaz...
        private float _gemTimer = 10f;
        private const int GemPoints = 75;
        private const float GemDamage = 0.35f;
        private const float GemTipMin = 120f, GemTipMax = 185f;  // how far down a hanging gem reaches (his head gets to ~165 at the top of a jump, so the big ones block a full jump)
        private const float GemBuried = 0.3f;              // this much of the gem is up past the top of the screen so the curved rocky top looks stuck in the roof
        private Texture2D _gemTexture, _diamondTexture;
        // Diamonds to grab, they come in little patterns
        private readonly List<Vector2> _diamonds = new();
        private readonly List<(Vector2 Pos, float Age)> _coinPops = new();  // pixel +1 that floats up off a grabbed diamond
        private const float CoinPopLife = 0.6f;
        private static readonly string[] PlusOne = { ".....X.", ".X..XX.", "XXX..X.", ".X...X.", "....XXX" };  // 7x5 pixel "+1"
        private float _diamondTimer = 4f;
        private const int DiamondPoints = 10;
        private SoundEffect _pickupSound;
        // Drill cracks whatever is in front of it, it breaks once this hits 1
        private float _drillProgress;
        private (int Kind, int Index) _drillLast = (-1, -1);
        private const float DrillReach = 110f;
        private float _mirrorFlash;                       // the bounced laser shooting right off the Mirror Helm
        private SoundEffect _shockwaveSound, _bubbleSound, _reviveSound, _timeSlowSound;
        private SoundEffectInstance _drillLoop;
        private Texture2D _disc;
        private Texture2D _pickaxeTexture;
        private Texture2D _turretTexture, _beamTexture;  // ceiling turret that fires the laser                // swung instead of the hammer when the pickaxe is on                          // soft filled circle for the bubble
        private float _fuel, _fuelMax;                    // Drill fuel, empty = overheated till it's full again
        private bool _overheated;
        private bool _hovering;                           // Hover Boots over a hole right now
        private float _reviveGlow;                        // golden glow after the Phoenix Heart kicks in
        private float _healPulse;                         // soft green ring growing out of him after a heal

        // Bar fill + rising pluses on any heal: bar grows to a pale ghost of the new health over 0.4s, flashes, mint + signs float up
        private const float HealFillTime = 0.4f, HealFlashTime = 0.15f, PlusLife = 0.9f;
        private float _healFrom, _healFill, _healFlash;
        private readonly List<(Vector2 Pos, float Age)> _pluses = new();
        // Same thing for damage: the bar drops right away and a pale ghost of the old health drains down to it
        private float _dmgFrom, _dmgFill;
        private float DamageGhost() { float t = 1 - _dmgFill / HealFillTime; return MathHelper.Lerp(_dmgFrom, _health, 1 - (1 - t) * (1 - t)); }
        private static readonly Color Mint = new(150, 255, 200);

        private void Heal(float newHealth)
        {
            _healFrom = _healFill > 0 ? ShownHealth() : _health;  // a heal mid-fill keeps going from where the bar is
            _health = newHealth;
            _healFill = HealFillTime;
            var middle = new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + _runner.Bounds.Height / 2);
            for (int k = 0; k < 8; k++)
            {
                float a = k * MathHelper.TwoPi / 8 + RandomHelper.NextFloat(-0.3f, 0.3f);  // spread evenly around him
                _pluses.Add((middle + new Vector2(MathF.Cos(a) * 40, MathF.Sin(a) * 55), -k * 0.04f));  // little stagger
            }
        }

        // Health the bar shows right now (eases from the old value up to the new one while filling)
        private float ShownHealth()
        {
            float t = 1 - _healFill / HealFillTime;
            return MathHelper.Lerp(_healFrom, _health, 1 - (1 - t) * (1 - t));
        }
        private Vector2 _mirrorFrom;
        private float _mirrorTime, _mirrorAngle;          // how long the bounce beam lasts, and its aim (max level only)
        private float _smashCooldownLeft;
        private bool _smashPressed;                       // set in HandleInput, used in the next Update

        // Swipe drawn when he smashes, an arc that sweeps down in front of him then fades
        private const float SwipeTime = 0.2f;     // how long the swipe lasts
        private float SwipeLength => _weapon == ItemId.QuakeHammer ? 0.35f : SwipeTime;  // the big swing around him is a lil slower
        private float _swipeLeft;

        // Difficulty ramps up from 0 to 1 over the first RampTime seconds so the start is chill and it gets crazy later
        private const float RampTime = CaveZones.ZoneLength * 3;  // ramps across all 3 caves, peaks at the end of green (blue easy, orange medium, green is the real struggle)
        private float Difficulty => MathHelper.Clamp(_scrollTime / RampTime, 0, 1);
        private float SpawnInterval => MathHelper.Lerp(2.6f, 1.2f, Difficulty);  // seconds between rocks
        private const float WallStartTime = 12f;          // just spikes at first, walls come in after this
        private bool _seenWall;                           // has this run had its first wall yet
        private const float GapStartTime = 20f;           // floor holes stay filled in until this

        // Bool for ending the game
        private bool _isGameOver;

        // Health (walls kill you, spikes take half and rocks take a quarter)
        private const float RockDamage = 0.25f;
        private const float SpikeDamage = 0.5f;
        private float _maxHealth;
        private float _health;
        private float _hurtTime;       // can't be hurt again until this runs out (he blinks)
        private float _flashTime;      // red flash right after a hit
        private float _flashStrength;  // bigger hits flash redder
        private static readonly Color PlayerTint = new Color(185, 205, 255); // a light blue so he matches the cave but still stands out

        // Points from smashing (replaces the run timer)
        private int _score;
        private const int BoulderPoints = 100;            // falling rocks are harder to time
        private const int WallPoints = 50;

        // Paralax Scrolling for the Inside Cave
        private static readonly (string Name, float Speed, float Y)[] CaveLayerInfo =
        {
            ("Images/Cave/cave_wall", 30f, 0f),
            ("Images/Cave/cave_pillars", 80f, -52f), // pulled up so the ceiling touches the top of the screen
        };
        private Texture2D[] _caveLayers;

        // Zone colored copies (blue, orange, green) of everything that changes color deeper in. Boulders keep their color
        private Texture2D[][] _caveLayerZones;
        private Texture2D[] _floorZones, _wallZones, _spikeZones;

        // The floor is a tilemap now (cut from the old floor art) so it can have gaps in it
        private Tilemap _floor;
        private const float FloorY = 452f;                // top of the floor tiles, same spot the old floor art sat
        private const int GapTile = 11;
        private bool _falling;                            // fell in a gap, he's a goner
        private float FloorScroll => _scrollTime * SpikeSpeed;

        // Laser beams that fire down from the ceiling, a thin warning line first then ZAP
        private readonly List<(float X, float Time, bool Bounced)> _lasers = new();  // Bounced = it already hit the Mirror Helm
        private float _laserTimer = 30f;                  // first one well into the run
        private const float LaserFollowSpeed = 140f;      // how fast the warning line slides after him (slower than he runs so you can outrun it)
        private const float LaserWarn = 1f;               // how long the warning line shows
        private const float LaserFire = 0.45f;            // how long the beam is on
        private const float LaserWidth = 18f;
        private const float TurretSlide = 0.25f;           // seconds for the turret to drop in and pull back out
        private const int TurretBottom = 40;               // where the beam comes out of the turret art
        private const float LaserDamage = 0.5f;

        // How long the cave has been scrolling (stops when he crashes)
        private float _scrollTime;
        // End of the level, obstacles stop, the door slides in and stops in the middle, then he walks in (boss arena goes behind it later)
        private const float EndTime = CaveZones.ZoneLength * 3;
        private const float ClearTime = 7f;               // nothing new spawns this long before the door so the screen is empty
        private const int DoorClearBonus = 500;
        private int _endPhase;                            // 0 running, 1 clearing, 2 door sliding in, 3 walking to it, 4 asking if ready, 5 rumbling, 6 opening, 7 walking in, 8 done
        private const float RumbleTime = 1.4f;            // door + screen shake before it starts opening
        private const float RattleTime = 0.25f;           // last bit of the rumble the door rocks +-6 degrees
        private const float StarLife = 0.6f;
        private readonly List<(Vector2 Pos, Vector2 Vel, float Age)> _stars = new();  // burst when the door flies open
        private const float DoorFrameTime = 0.08f;        // closed, half open, open, it bursts once the shaking stops
        private float _scrollSpeed = 1f;                  // slows to 0 as the door rolls into the middle
        private float _doorX, _doorStartX, _endTimer;
        private float _noGapFrom = float.MaxValue;        // floor past here has no holes (so the door area is solid)
        private Texture2D _doorTexture;
        private Texture2D _rocketTexture;                 // foam rocket from the Kenney Blaster Kit (CC0)                   // 6 frames, closed -> lit -> opening -> open
        private float _startScroll;                       // floor scroll the run started at (later caves start further in)

        // Lets add some "Screen" shake
        private float _shakeTime;
        private float _shakeDuration;
        private float _shakeIntensity;
        private Vector2 _shakeOffset;

        // Particle systems
        private DashParticleSystem _dash;
        private RockDustParticleSystem _rockDust;
        private SkinFx _skinFx;          // the equipped skin's particle flair
        private bool _smashedLastFrame;
        private RockDustParticleSystem _hoverDust;  // drawn behind him
        private CrashParticleSystem _crash;

        public GameplayScreen()
        {
            // Fades in and out over half a second
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        /// <summary>
        /// Starts a screen shake. Intensity is the max pixel offset.
        /// </summary>
        // Rock break kick, the whole frame shoves 22px and tilts 1 degree toward the hit, then settles
        private const float KickTime = 0.35f;
        private float _kickLeft;
        private Vector2 _kickDir;
        private void Kick(Vector2 toward)
        {
            var dir = toward - new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + _runner.Bounds.Height / 2);
            _kickDir = dir == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(dir);
            _kickLeft = KickTime;
        }
        private Matrix KickMatrix(Viewport viewport)
        {
            if (_kickLeft <= 0) return Matrix.Identity;
            float e = MathF.Pow(_kickLeft / KickTime, 3);  // full shove right away, eases back to rest
            var center = new Vector3(viewport.Width / 2f, viewport.Height / 2f, 0);
            return Matrix.CreateTranslation(-center) * Matrix.CreateRotationZ(MathHelper.ToRadians(1.5f) * MathF.Sign(_kickDir.X) * e)
                 * Matrix.CreateTranslation(center + new Vector3(_kickDir * 22f * e, 0));
        }

        private void Shake(float intensity, float duration)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTime = duration;
        }

        private void UpdateShake(float dt)
        {
            if (_shakeTime > 0)
            {
                _shakeTime -= dt;
                float strength = _shakeIntensity * (_shakeTime / _shakeDuration); // fades out
                _shakeOffset = new Vector2(RandomHelper.NextFloat(-1, 1), RandomHelper.NextFloat(-1, 1)) * strength;
            }
            else
            {
                _shakeOffset = Vector2.Zero;
            }
        }

        public override void Activate()
        {
            _content = ScreenManager.Game.Content;

            // Loads the various cave layers
            _caveLayers = Array.ConvertAll(CaveLayerInfo, layer => _content.Load<Texture2D>(layer.Name));

            // Loads the music and sounds for the game
            _runnerMusic = _content.Load<Song>("Audio/Music/court_death");
            _crashSound = _content.Load<SoundEffect>("Audio/Sfx/crash");
            _swingSound = _content.Load<SoundEffect>("Audio/Sfx/swing");
            _notReadySound = _content.Load<SoundEffect>("Audio/Sfx/not_ready");
            _laserChargeSound = _content.Load<SoundEffect>("Audio/Sfx/laser_charge");
            _laserFireSound = _content.Load<SoundEffect>("Audio/Sfx/laser4");
            _wallWarningSound = _content.Load<SoundEffect>("Audio/Sfx/wall_alert");
            _shockwaveSound = _content.Load<SoundEffect>("Audio/Sfx/shockwave");
            _pickupSound = _content.Load<SoundEffect>("Audio/Sfx/buy");
            _gemTexture = _content.Load<Texture2D>("Images/Gameplay/gem_spike");
            _diamondTexture = _content.Load<Texture2D>("Images/Gameplay/diamond");
            _bubbleSound = _content.Load<SoundEffect>("Audio/Sfx/bubble");
            _reviveSound = _content.Load<SoundEffect>("Audio/Sfx/revive");
            _timeSlowSound = _content.Load<SoundEffect>("Audio/Sfx/time_slow");
            _drillLoop = _content.Load<SoundEffect>("Audio/Sfx/drill").CreateInstance();
            _drillLoop.IsLooped = true;
            _drillLoop.Volume = 0.5f;
            // Soft disc, solid in the middle and fading at the edge
            const int D = 64;
            var disc = new Color[D * D];
            for (int i = 0; i < disc.Length; i++)
            {
                float d = Vector2.Distance(new Vector2(i % D, i / D), new Vector2(D / 2f - 0.5f)) / (D / 2f);
                disc[i] = Color.White * MathHelper.Clamp((1 - d) * 4, 0, 1) * (0.5f + 0.5f * d);
            }
            _disc = new Texture2D(ScreenManager.GraphicsDevice, D, D);
            _disc.SetData(disc);
            _spikeTexture = _content.Load<Texture2D>("Images/Gameplay/stalactite");
            _wallTexture = _content.Load<Texture2D>("Images/Gameplay/wall_stone");
            _hammerTexture = _content.Load<Texture2D>("Images/Gameplay/hammer");
            _pickaxeTexture = _content.Load<Texture2D>("Images/Gameplay/pickaxe");
            _turretTexture = _content.Load<Texture2D>("Images/Gameplay/turret");
            _doorTexture = _content.Load<Texture2D>("Images/Gameplay/door");
            _rocketTexture = _content.Load<Texture2D>("Images/Gameplay/rocket");
            _beamTexture = _content.Load<Texture2D>("Images/Gameplay/beam");
            _lanternTexture = _content.Load<Texture2D>("Images/Gameplay/lantern");
            _glowTexture = _content.Load<Texture2D>("Images/Particles/particle");
            _dashIcon = _content.Load<Texture2D>("Images/HUD/CooldownShiftButton");
            _smashIcon = _content.Load<Texture2D>("Images/HUD/SmashCooldownButton");
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _floor = new Tilemap(_content, "Images/Cave/floor_tiles", "Maps/cave_floor.txt");

            // Made once per launch, the spikes get their blue tint baked in first
            _caveLayerZones = Array.ConvertAll(_caveLayers, layer => CaveZones.Recolor(layer));
            // Back wall gets its own art in the later caves, lava cracks in orange and dripping acid in green (built off the recolored original)
            _caveLayerZones[0] = new[] { _caveLayerZones[0][0], _content.Load<Texture2D>("Images/Cave/cave_wall_lava"), _content.Load<Texture2D>("Images/Cave/cave_wall_acid") };
            _floorZones = CaveZones.Recolor(_floor.Tileset);
            _wallZones = CaveZones.Recolor(_wallTexture);
            _spikeZones = CaveZones.Recolor(_spikeTexture, new Color(150, 200, 255));

            // Fresh player for each run
            _runner = new();
            _runner.LoadContent(_content);


            _scrollTime = Settings.TestStart switch { 1 => CaveZones.ZoneLength, 2 => CaveZones.ZoneLength * 2, 3 => EndTime - ClearTime - 1f, _ => 0 };  // testing start from Settings, normally the blue cave
            _startScroll = FloorScroll;

            // Whatever is equipped sets up the run
            _weapon = Shop.Equipped(Slot.Weapon);
            _ability = Shop.Equipped(Slot.Ability);
            _armor = Shop.Equipped(Slot.Armor);
            SmashCooldown = _weapon is ItemId weapon && weapon != ItemId.Drill ? Shop.Value(weapon) : 0f;
            _fuelMax = _fuel = Shop.Value(ItemId.Drill);
            if (_armor == ItemId.MirrorHelm) _laserTimer = 15f;  // lasers come sooner (and more often) when you can bounce them
            _runner.CanDash = _ability == ItemId.Dash;
            _runner.DashCooldown = Shop.Value(ItemId.Dash);
            if (_armor == ItemId.SpringBoots) _runner.JumpBoost = Shop.Value(ItemId.SpringBoots);
            _abilityCooldown = _ability switch { ItemId.PatchUp => 20f, ItemId.BubbleShield => 15f, ItemId.TimeSlow => 20f, ItemId.RocketLauncher => MathF.Abs(Shop.Value(ItemId.RocketLauncher)), _ => 1f };
            _weaponIcon = _smashIcon;  // HUD always shows the blue smash button, whatever weapon is on
            _abilityIcon = _ability == null ? null : _ability == ItemId.Dash ? _dashIcon : _content.Load<Texture2D>("Images/HUD/ability_" + System.IO.Path.GetFileName(Shop.Get(_ability.Value).Icon));  // item art in the same blue frame as the smash button
            if (_weapon == null) _weaponIcon = null;  // empty hands, no smash

            // Full health (more with the Miner's Vest)
            _maxHealth = _armor == ItemId.MinersVest ? Shop.Value(ItemId.MinersVest) : 1f;
            _health = _maxHealth;

            // Particle systems are updated/drawn by this screen instead of Game.Components, this allows the shakes to be applyed
            _dash = new DashParticleSystem(ScreenManager.Game, _runner);
            _dash.Initialize();
            _skinFx = new SkinFx();
            _rockDust = new RockDustParticleSystem(ScreenManager.Game);
            _rockDust.Initialize();
            _hoverDust = new RockDustParticleSystem(ScreenManager.Game) { Size = 0.45f };
            _hoverDust.Initialize();
            _crash = new CrashParticleSystem(ScreenManager.Game, 5);
            _crash.Initialize();
            // Embers float up and back from the lantern as he runs
            _embers = new SparkParticleSystem(ScreenManager.Game, new Color(255, 150, 40), 0.8f, 0.3f, new Vector2(-90, -70), new Vector2(-40, -30));
            _embers.Initialize();

            // Start the music from the beginning
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = Settings.MusicVolume;  // the title may still be fading in
            MediaPlayer.Play(_runnerMusic);
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            // Shake and particles keep running so after the crash they can settle
            UpdateShake((float)gameTime.ElapsedGameTime.TotalSeconds);
            _dash.Update(gameTime);
            _rockDust.Update(gameTime);
            _hoverDust.Update(gameTime);
            _crash.Update(gameTime);
            _embers.Update(gameTime);

            // Only run the game while this screen is on top and the run is going
            if (!IsActive || _isGameOver) return;

            // Time Slow halves the world (rocks, spikes, lasers, scrolling) but he still moves full speed
            float realDt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float dt = realDt * (_slowLeft > 0 ? 0.5f : 1f);
            var worldTime = new GameTime(gameTime.TotalGameTime, TimeSpan.FromSeconds(dt));
            _slowLeft -= realDt;
            _bubbleLeft -= realDt;
            _safeTime -= realDt;
            _mirrorFlash -= realDt;
            if (_mirrorFlash > 0)
            {
                // Max level lets you sweep the beam up and down, it keeps wrecking stuff the whole time it's out
                if (Shop.IsMaxed(ItemId.MirrorHelm))
                {
                    // Holding Left tilts it up from flat to 75 degrees, let go and it drops back flat
                    float target = Controls.IsDown(PlayerAction.Left) ? -MathHelper.ToRadians(75) : 0f;
                    _mirrorAngle = MathHelper.Lerp(_mirrorAngle, target, MathF.Min(1, 8f * realDt));
                }
                var body = _runner.Bounds;
                _mirrorFrom = Shop.IsMaxed(ItemId.MirrorHelm) ? new Vector2(body.X + body.Width / 2, body.Y + body.Height * 0.3f) : new Vector2(_mirrorFrom.X, body.Y + 20);  // max level fires from his chest and follows him
                BreakAlong(_mirrorFrom, _mirrorAngle);
            }
            _reviveGlow -= realDt;
            _healPulse -= realDt;
            for (int k = _coinPops.Count - 1; k >= 0; k--)
            {
                _coinPops[k] = (_coinPops[k].Pos, _coinPops[k].Age + realDt);
                if (_coinPops[k].Age > CoinPopLife) _coinPops.RemoveAt(k);
            }
            for (int k = _stars.Count - 1; k >= 0; k--)
            {
                var (pos, vel, age) = _stars[k];
                _stars[k] = (pos + vel * realDt, vel * 0.92f, age + realDt);  // fast out, slows down
                if (age > StarLife) _stars.RemoveAt(k);
            }
            _kickLeft = MathF.Max(0, _kickLeft - realDt);
            if (_healFill > 0 && (_healFill -= realDt) <= 0) { _healFill = 0; _healFlash = HealFlashTime; }
            _healFlash = MathF.Max(0, _healFlash - realDt);
            _dmgFill = MathF.Max(0, _dmgFill - realDt);
            for (int k = _pluses.Count - 1; k >= 0; k--)
            {
                _pluses[k] = (_pluses[k].Pos, _pluses[k].Age + realDt);
                if (_pluses[k].Age > PlusLife) _pluses.RemoveAt(k);
            }
            MediaPlayer.Volume = Settings.MusicVolume * (_slowLeft > 0 ? 0.35f : 1f);  // music goes quiet and far away during Time Slow

            _scrollTime += dt * _scrollSpeed;
            UpdateEnding(dt);
            if (_isGameOver) return;
            if (_scrollTime >= CaveZones.ZoneLength) Achievements.Unlock("orange");
            if (_scrollTime >= CaveZones.ZoneLength * 2) Achievements.Unlock("green");
            UpdateLantern(realDt);

            _runner.Update(gameTime);
            _skinFx.Update(realDt, _runner, FloorY + 22, _smashedLastFrame);
            _smashedLastFrame = false;
            if (_endPhase < 3) UpdateAbility(realDt);
            GearSparkles();

            // Gaps in the floor, step in one while on the ground and down he goes (a dash carries him over though! and Hover Boots just float)
            if (!_falling && _safeTime <= 0 && _armor != ItemId.HoverBoots && _runner.IsOnGround && !_runner.IsDashing && _floor.TileAtLooping(0, FloorScroll, _runner.Position.X, FloorTile) == GapTile)
            {
                // Phoenix Heart catches him right at the edge, otherwise the world pauses while he drops off screen
                if (_armor == ItemId.PhoenixHeart && !_revived) Crash(new Color(20, 30, 60), DeathCause.Hole);
                else
                {
                    _runner.Fall();
                    _falling = true;
                }
            }
            // Hover Boots, little blue sparks under his feet while he floats over a hole
            _hovering = !_falling && _armor == ItemId.HoverBoots && _runner.IsOnGround && _floor.TileAtLooping(0, FloorScroll, _runner.Position.X, FloorTile) == GapTile;
            if (_hovering) _hoverDust.PlaceDust(new Vector2(_runner.Position.X + RandomHelper.NextFloat(-8, 8), FloorY), new Color(120, 220, 255));
            if (_falling)
            {
                if (_runner.Position.Y > ScreenManager.GraphicsDevice.Viewport.Height + 150) Crash(new Color(20, 30, 60), DeathCause.Hole);
                return;
            }

            UpdateLasers(dt);
            if (_isGameOver) return;
            UpdateQuakesAndRockets(realDt);

            // Spawn a new bolder at a random X every SpawnInterval seconds
            _spawnTimer += dt;
            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0f;
                float x = RandomHelper.NextFloat(RockMinX, RockMaxX);
                SpawnBolder(x);

                // Rare double rock (about 1 in 6) on the far side so they never overlap, only once things heat up
                if (Difficulty > 0.35f && RandomHelper.Next(0, 5) == 0)
                    SpawnBolder(x < 427 ? RandomHelper.NextFloat(x + 300, RockMaxX + 60) : RandomHelper.NextFloat(RockMinX - 60, x - 300));
            }

            // Spikes slide in from the right, every 6-9 seconds at the start down to 3-5 later
            _spikeTimer -= dt;
            _hurtTime -= realDt;
            _flashTime -= realDt;
            if (_spikeTimer <= 0)
            {
                // About 1 in 3 is a wall (half of them crystal), walls start further out so the ! warning shows first
                // The first one after WallStartTime is always a wall so you meet one early, then it's random
                bool isWall = _scrollTime > WallStartTime && (!_seenWall || RandomHelper.Next(0, 2) == 0);
                float x = ScreenManager.GraphicsDevice.Viewport.Width + (isWall ? WallWarningDistance : SpikeSource.Width);

                // Never put one over a gap (looks weird), just try again a moment later once the gap has moved past
                if (OverGap(x, isWall ? _wallTexture.Width : SpikeSource.Width) || _gems.Exists(g => MathF.Abs(g.X - x) < 120 + (isWall ? _wallTexture.Width / 2f : 0))
                    || _diamonds.Exists(d => MathF.Abs(d.X - x) < 60 + (isWall ? _wallTexture.Width / 2f : 0))) _spikeTimer = 0.2f;  // not on holes, gems or diamonds
                else
                {
                    _spikes.Add((x, isWall));
                    _seenWall |= isWall;
                    if (isWall) _wallWarningSound.Play(0.8f, 0f, 0.6f);  // beep beep from the right side, same time the ! shows
                    _spikeTimer = RandomHelper.NextFloat(MathHelper.Lerp(6f, 3f, Difficulty), MathHelper.Lerp(9f, 5f, Difficulty));
                }
            }
            // Down or S smashes a wall in reach, then cools down (even on a miss, so timing it takes skill)
            _smashCooldownLeft = MathF.Max(0, _smashCooldownLeft - realDt);
            _swipeLeft = MathF.Max(0, _swipeLeft - realDt);
            bool smash = _smashPressed;
            _smashPressed = false;
            // Drill, hold to keep breaking stuff, burns fuel and overheats when it runs dry
            if (_weapon == ItemId.Drill)
            {
                bool drilling = Controls.IsDown(PlayerAction.Smash) && !_overheated;
                if (drilling)
                {
                    _fuel -= realDt;
                    if (_fuel <= 0) { _fuel = 0; _overheated = true; }
                    UpdateDrill(realDt);
                }
                else
                {
                    _fuel = MathF.Min(_fuelMax, _fuel + realDt * 0.5f);
                    if (_fuel >= _fuelMax) _overheated = false;
                    _drillProgress = MathF.Max(0, _drillProgress - realDt * 1.5f);  // cracks heal if you let go
                }
                if (drilling && _drillLoop.State != SoundState.Playing) _drillLoop.Play();
                if (!drilling) _drillLoop.Stop();
                smash = false;
            }
            if (smash && _smashCooldownLeft > 0 && _weapon != null && _weapon != ItemId.Drill && _endPhase < 3) NotReady(_weaponIcon);
            if (_smashCooldownLeft == 0 && smash && _weapon != null && _endPhase < 3)
            {
                _smashCooldownLeft = SmashCooldown;
                _smashedLastFrame = true;
                {
                    _swipeLeft = SwipeLength;
                    _swipeReach = SmashReach();
                    _swingSound.Play(0.8f, RandomHelper.NextFloat(-0.15f, 0.15f), 0f);  // little pitch change so it doesnt get repetitive
                    SmashInReach();
                    if (_weapon == ItemId.QuakeHammer && _runner.IsOnGround)  // shockwave only when his feet are planted
                    {
                        _quakes.Add(_runner.Bounds.Right);
                        _shockwaveSound.Play(0.8f, 0f, 0f);
                        Shake(5f, 0.3f);
                    }
                }
            }

            for (int i = _spikes.Count - 1; i >= 0; i--)
            {
                _spikes[i] = (_spikes[i].X - SpikeSpeed * dt, _spikes[i].IsWall);
                var spikeBounds = SpikeBounds(_spikes[i]);
                if (_safeTime <= 0 && _runner.Bounds.CollidesWith(spikeBounds))
                {
                    // Walls kill outright (unless the bubble eats it), spikes only hurt
                    if (_spikes[i].IsWall)
                    {
                        if (Blocked()) { BreakObstacle(i); continue; }
                        Crash(new Color(70, 96, 170), DeathCause.Wall);
                        return;
                    }
                    float spikeDamage = _armor == ItemId.CrystalPlate ? Shop.Value(ItemId.CrystalPlate) : SpikeDamage;
                    if (_hurtTime <= 0 && spikeDamage > 0)
                    {
                        if (Blocked()) { BreakObstacle(i); continue; }
                        Hurt(spikeDamage, new Color(150, 200, 255), DeathCause.Spike);
                        if (_isGameOver || _safeTime > 0) return;
                    }
                }
                if (_spikes[i].X < -SpikeSource.Width) _spikes.RemoveAt(i);
            }
            UpdateGems(dt);
            if (_isGameOver) return;
            UpdateDiamonds(dt);

            int screenHeight = ScreenManager.GraphicsDevice.Viewport.Height;

            // Move every bolder, then remove ones that leave the screen or hit the player.
            for (int i = _bolders.Count - 1; i >= 0; i--)
            {
                _bolders[i].Update(worldTime, screenHeight);

                // Dust kicked off the top of each falling rock, in the rock's color
                // (top of the hit circle, so the dust follows the rock's size)
                if (RandomHelper.Next(0, 1) == 0)
                {
                    var rock = _bolders[i].Bounds;
                    _rockDust.PlaceDust(rock.Center - new Vector2(0, rock.Radius), _bolders[i].Tint);
                }

                if (_hurtTime <= 0 && _safeTime <= 0 && _bolders[i].HitBounds.CollidesWith(_runner.Bounds))
                {
                    var tint = _bolders[i].Tint;
                    if (!Blocked())
                    {
                        Hurt(_armor == ItemId.HazardHat ? Shop.Value(ItemId.HazardHat) : RockDamage, tint, DeathCause.Rock);
                        if (_isGameOver) return; // the rock stays on screen so you can see what got you
                    }

                    // Survived it so the rock breaks on him
                    _crash.PlaceCrash(_bolders[i].Bounds.Center, tint);
                    _bolders.RemoveAt(i);
                    continue;
                }

                if (_bolders[i].IsOffScreen)
                {
                    _bolders.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Spawns a laser every so often that follows him while it warns, then hurts him if he's in the beam when it fires
        /// </summary>
        private void UpdateLasers(float dt)
        {
            _laserTimer -= dt;
            if (_laserTimer <= 0)
            {
                _lasers.Add((_runner.Position.X, 0, false));
                _laserChargeSound.Play(0.6f, 0f, 0f);  // whine climbs for the whole warning
                _laserTimer = RandomHelper.NextFloat(MathHelper.Lerp(14f, 7f, Difficulty), MathHelper.Lerp(20f, 11f, Difficulty));  // rare at first, more often later
                _laserTimer *= new[] { 2.5f, 1.6f, 1f }[Math.Min(2, CaveZones.At(_scrollTime).From)];  // rare in blue, more in orange, full rate in green
                if (_armor == ItemId.MirrorHelm) _laserTimer *= 0.55f;
            }

            for (int i = _lasers.Count - 1; i >= 0; i--)
            {
                var (x, time, bounced) = _lasers[i];
                if (time < LaserWarn && time + dt >= LaserWarn) _laserFireSound.Play(0.8f, 0f, 0f);  // ZAP right when it fires
                time += dt;

                // While warning it slides toward him, then locks in place when it fires
                if (time < LaserWarn) x += MathHelper.Clamp(_runner.Position.X - x, -LaserFollowSpeed * dt, LaserFollowSpeed * dt);
                var r = _runner.Bounds;
                bool firing = time >= LaserWarn && time < LaserWarn + LaserFire;
                if (firing && !bounced && _safeTime <= 0 && MathF.Abs(r.X + r.Width / 2 - x) < r.Width / 2 + LaserWidth / 2)
                {
                    if (_armor == ItemId.MirrorHelm)
                    {
                        // PING, it bounces off his helmet to the right and wrecks whatever is ahead
                        bounced = true;
                        _mirrorFlash = _mirrorTime = Shop.Value(ItemId.MirrorHelm);
                        _mirrorAngle = 0;
                        _mirrorFrom = new Vector2(x, r.Y + 20);
                    }
                    else if (Blocked()) bounced = true;
                    else
                    {
                        Hurt(LaserDamage, Color.Red, DeathCause.Laser);
                        if (_isGameOver || _safeTime > 0) return;
                    }
                }
                _lasers[i] = (x, time, bounced);
                if (time > LaserWarn + LaserFire + TurretSlide) _lasers.RemoveAt(i);  // gone once the turret has pulled back up
            }
        }

        // Holes only show up once they come in from off screen after GapStartTime, before that they're plain floor (so one never pops open under him)
        private int FloorTile(int column, int id) => id >= 9 && (column * Tilemap.TileSize < _startScroll + GapStartTime * SpikeSpeed + 800 || column * Tilemap.TileSize >= _noGapFrom) ? 1 : id;  // counts from where this run started so a checkpoint start isnt over a hole

        // True if any floor tile under this spot is a gap or a gap's crumbly edge (spikes slide with the floor so it stays true)
        private bool OverGap(float centerX, float width)
        {
            for (float x = centerX - width / 2 - Tilemap.TileSize; x <= centerX + width / 2 + Tilemap.TileSize; x += Tilemap.TileSize / 2)
                if (_floor.TileAtLooping(0, FloorScroll, x, FloorTile) >= 9) return true;
            return false;
        }

        // Where the lantern hangs, clipped to his back hip
        private Vector2 LanternHandle() => new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2 - 14, _runner.Bounds.Y + 62);

        // The lantern's swing angle (radians), a small bob against his hip
        private float LanternSwing() => 0.12f * MathF.Sin(_lanternTime * 9f);

        /// <summary>
        /// Swings the lantern and moves the embers (a few new ones each second, drifting up and back as he runs)
        /// </summary>
        private void UpdateLantern(float dt)
        {
            _lanternTime += dt;

            // The flame is about 26px below the handle, so embers start there (following the swing)
            var flame = LanternHandle() + new Vector2(-MathF.Sin(LanternSwing()), MathF.Cos(LanternSwing())) * 26;
            if (RandomHelper.Next(0, 3) == 0) _embers.Emit(flame);
        }

        /// <summary>
        /// Uses the equipped ability (Dash is handled by the Runner itself)
        /// </summary>
        private void UpdateAbility(float dt)
        {
            _abilityCooldownLeft = MathF.Max(0, _abilityCooldownLeft - dt);
            if (_ability != null && _endPhase < 3 && Controls.IsPressed(PlayerAction.Dash)
                && (_ability == ItemId.Dash ? _runner.DashCooldownLeft > 0 && _runner.DashCooldownLeft < _runner.DashCooldown - 0.05f : _abilityCooldownLeft > 0))  // (a dash that just fired isn't "not ready")
                NotReady(_abilityIcon);
            if (_ability == null || _ability == ItemId.Dash || _abilityCooldownLeft > 0 || !Controls.IsPressed(PlayerAction.Dash)) return;

            switch (_ability)
            {
                case ItemId.PatchUp:
                    if (_health >= _maxHealth) return;  // full already, dont waste it
                    Heal(MathF.Min(_maxHealth, _health + Shop.Value(ItemId.PatchUp) * _maxHealth));
                    _healPulse = 0.5f;
                    for (int k = 0; k < 14; k++) _hoverDust.PlaceDust(_runner.Position + new Vector2(RandomHelper.NextFloat(-25, 25), RandomHelper.NextFloat(-110, -10)), new Color(120, 255, 140));
                    _swingSound.Play(0.5f, 0.5f, 0f);
                    break;
                case ItemId.BubbleShield:
                    _bubbleLeft = Shop.Value(ItemId.BubbleShield);
                    _bubbleSound.Play(0.7f, 0f, 0f);
                    break;
                case ItemId.TimeSlow:
                    _slowLeft = Shop.Value(ItemId.TimeSlow);
                    _timeSlowSound.Play(0.8f, 0f, 0f);
                    break;
                case ItemId.RocketLauncher:
                    // Fwoosh, a rocket out of the launcher on his shoulder flies right till it hits something
                    _rockets.Add((_runner.Muzzle, 0f));
                    _runner.ShootTime = Runner.ShootPose;  // pulls the launcher out just for the shot
                    _swingSound.Play(0.9f, -0.6f, 0f);
                    break;
            }
            _abilityCooldownLeft = _abilityCooldown;
        }

        /// <summary>
        /// If the bubble is up it pops instead of him getting hit (true = blocked)
        /// </summary>
        private bool Blocked()
        {
            if (_bubbleLeft <= 0) return false;
            _bubbleLeft = 0;
            _safeTime = 0.6f;
            _crash.PlaceCrash(_runner.Position - new Vector2(0, 60), new Color(120, 220, 255));
            Shake(5f, 0.2f);
            _crashSound.Play(0.6f, 0.4f, 0f);
            return true;
        }

        // Breaks a wall or spike for points (smash, quake, rocket, bubble, mirror all use this)
        private void BreakObstacle(int i)
        {
            var spike = _spikes[i];
            _crash.PlaceCrash(new Vector2(spike.X, spike.IsWall ? SpikeBottom - 100 : SpikeBottom - 40), new Color(70, 96, 170));
            _crashSound.Play();
            Shake(6f, 0.25f);
            _score += spike.IsWall ? WallPoints : SpikePoints;
            if (spike.IsWall) Achievements.Add("walls");
            _spikes.RemoveAt(i);
        }

        private void BreakRock(int b)
        {
            _crash.PlaceCrash(_bolders[b].Bounds.Center, _bolders[b].Tint);
            Achievements.Add("rocks");
            Kick(_bolders[b].Bounds.Center);
            _crashSound.Play();
            Shake(6f, 0.25f);
            _score += BoulderPoints;
            _bolders.RemoveAt(b);
        }

        // Mirror Helm bounce, everything the beam passes through to his right gets wrecked
        private void BreakAlong(Vector2 from, float angle)
        {
            float slope = MathF.Tan(angle);
            float Y(float x) => from.Y + slope * (x - from.X);  // beam height at that x
            for (int b = _bolders.Count - 1; b >= 0; b--)
            {
                var rock = _bolders[b].Bounds;
                if (rock.Center.X > from.X && MathF.Abs(rock.Center.Y - Y(rock.Center.X)) < rock.Radius + 14) BreakRock(b);
            }
            for (int i = _spikes.Count - 1; i >= 0; i--)
            {
                var box = SpikeBounds(_spikes[i]);
                if (_spikes[i].X > from.X && Y(_spikes[i].X) + 14 > box.Y) BreakObstacle(i);
            }
            for (int g = _gems.Count - 1; g >= 0; g--)
            {
                var box = GemBounds(_gems[g]);
                if (_gems[g].X > from.X && Y(_gems[g].X) + 14 > box.Top && Y(_gems[g].X) - 14 < box.Bottom) BreakGem(g);
            }
        }

        /// <summary>
        /// Shockwaves slide right along the floor breaking walls and spikes, rockets fly right and blow up the first wall or rock
        /// </summary>
        private void UpdateQuakesAndRockets(float dt)
        {
            float screenWidth = ScreenManager.GraphicsDevice.Viewport.Width;
            for (int q = _quakes.Count - 1; q >= 0; q--)
            {
                _quakes[q] += QuakeSpeed * dt;
                if (_floor.TileAtLooping(0, FloorScroll, _quakes[q], FloorTile) == GapTile) { _quakes.RemoveAt(q); continue; }  // falls into the hole
                // Rocks low enough to the floor get knocked apart by the wave too
                for (int b = _bolders.Count - 1; b >= 0; b--)
                {
                    var rock = _bolders[b].Bounds;
                    if (MathF.Abs(rock.Center.X - _quakes[q]) < rock.Radius + 10 && rock.Center.Y + rock.Radius > FloorY - 120) BreakRock(b);
                }
                for (int i = _spikes.Count - 1; i >= 0; i--)
                {
                    var box = SpikeBounds(_spikes[i]);
                    if (_quakes[q] >= box.X && _quakes[q] <= box.X + box.Width) BreakObstacle(i);
                }
                for (int g = _gems.Count - 1; g >= 0; g--)
                    if (!_gems[g].Top && MathF.Abs(_gems[g].X - _quakes[q]) < 20) BreakGem(g);
                if (RandomHelper.Next(0, 1) == 0) _rockDust.PlaceDust(new Vector2(_quakes[q], FloorY), new Color(150, 170, 220));
                if (_quakes[q] > screenWidth + 40) _quakes.RemoveAt(q);
            }

            for (int r = _rockets.Count - 1; r >= 0; r--)
            {
                var (rocket, angle) = _rockets[r];
                if (Shop.Value(ItemId.RocketLauncher) < 0)
                {
                    // Homing, turns its nose toward the closest thing ahead (walls are aimed at the middle), can't turn on a dime
                    var targets = _bolders.Select(b => b.Bounds.Center).Concat(_spikes.Where(s => s.IsWall).Select(s => new Vector2(SpikeBounds(s).X + SpikeBounds(s).Width / 2, SpikeBottom - 100)));
                    var ahead = targets.Where(p => p.X > rocket.X).OrderBy(p => p.X - rocket.X).ToList();
                    float want = ahead.Count > 0 ? MathF.Atan2(ahead[0].Y - rocket.Y, ahead[0].X - rocket.X) : 0f;
                    want = MathHelper.Clamp(want, -1.2f, 1.2f);  // never flips around backwards
                    angle += MathHelper.Clamp(want - angle, -4f * dt, 4f * dt);
                }
                rocket += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * RocketSpeed * dt;
                _rockets[r] = (rocket, angle);
                _rockDust.PlaceDust(rocket - new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 28, new Color(170, 160, 150));  // smoke trail
                bool hit = false;
                int wall = _spikes.FindIndex(s => s.IsWall && rocket.X >= SpikeBounds(s).X && rocket.X <= SpikeBounds(s).X + SpikeBounds(s).Width);
                if (wall >= 0) { BreakObstacle(wall); hit = true; }
                int gem = _gems.FindIndex(g => GemBounds(g).CollidesWith(new BoundingRectangle(rocket.X - 4, rocket.Y - 4, 8, 8)));
                if (!hit && gem >= 0) { BreakGem(gem); hit = true; }
                for (int b = _bolders.Count - 1; b >= 0 && !hit; b--)
                    if (Vector2.Distance(_bolders[b].Bounds.Center, rocket) < _bolders[b].Bounds.Radius + 8) { BreakRock(b); hit = true; }
                if (hit)
                {
                    _crash.PlaceCrash(rocket, new Color(255, 150, 40));  // KABOOM
                    Shake(8f, 0.3f);
                }
                if (hit || rocket.X > screenWidth + 40 || rocket.Y < -40 || rocket.Y > FloorY + 40) _rockets.RemoveAt(r);
            }
        }

        // Top-center of his head, where the smash reaches out from
        private Vector2 SmashPoint() => _weapon == ItemId.QuakeHammer
            ? new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2 + 10, _runner.Bounds.Y + _runner.Bounds.Height / 3)  // big swing centered on him
            : new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2 + 30, _runner.Bounds.Y);

        /// <summary>
        /// Breaks the nearest wall in reach and every falling rock in reach, for points
        /// </summary>
        private void SmashInReach()
        {
            float runnerRight = _runner.Bounds.Right;
            int wall = _spikes.FindIndex(s => s.IsWall && SpikeBounds(s).Left - runnerRight < SmashReach() && SpikeBounds(s).Right > _runner.Bounds.Left);
            if (wall >= 0)
            {
                _crash.PlaceCrash(new Vector2(_spikes[wall].X, SpikeBottom - 100), new Color(70, 96, 170));
                Shake(6f, 0.25f);
                _crashSound.Play();
                _spikes.RemoveAt(wall);
                _score += Points(WallPoints);
            }

            // Gems in reach, floor ones like walls, hanging ones by the tip
            for (int g = _gems.Count - 1; g >= 0; g--)
            {
                var box = GemBounds(_gems[g]);
                bool reach = _gems[g].Top
                    ? Vector2.Distance(new Vector2(_gems[g].X, _gems[g].Tip), SmashPoint()) < SmashReach() + 16
                    : box.Left - runnerRight < SmashReach() && box.Right > _runner.Bounds.Left;
                if (reach) BreakGem(g);
            }

            // Also smashes any falling rock right above him
            for (int b = _bolders.Count - 1; b >= 0; b--)
            {
                var rock = _bolders[b].Bounds;
                if (InSmashRange(_bolders[b]))
                {
                    _crash.PlaceCrash(rock.Center, _bolders[b].Tint);
                    Shake(6f, 0.25f);
                    _crashSound.Play();
                    _bolders.RemoveAt(b);
                    _score += Points(BoulderPoints);
                }
            }
        }

        // A rock can be smashed when its hit circle comes within reach of his head
        private bool InSmashRange(Bolders bolder) => Vector2.Distance(bolder.Bounds.Center, SmashPoint()) < bolder.Bounds.Radius + SmashReach();

        private float SmashReach() => _weapon switch { ItemId.Pickaxe => 68f, ItemId.QuakeHammer => 120f, _ => WallReach };

        // Hit box of a gemstone (the thin middle so it feels fair)
        private BoundingRectangle GemBounds((float X, bool Top, Color Tint, float Tip) gem) => gem.Top
            ? new BoundingRectangle(gem.X - gem.Tip * 0.1f, 0, gem.Tip * 0.2f, gem.Tip)  // thin middle, the whole length from the roof to the tip
            : new BoundingRectangle(gem.X - 20, SpikeBottom - 90, 40, 90);  // the middle of the rock pile, a real jump to clear

        // Anything already near this X that a gem would end up inside (walls are wide so they get extra room)
        private bool TooCloseToObstacle(float x) => _spikes.Exists(s => MathF.Abs(s.X - x) < 120 + (s.IsWall ? _wallTexture.Width / 2f : 0));

        // Little sparkles so the power ups pop while they're running
        private void GearSparkles()
        {
            var middle = new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + _runner.Bounds.Height / 2);
            float angle = RandomHelper.NextFloat(0, MathHelper.TwoPi);
            var around = middle + new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            if (_bubbleLeft > 0 && RandomHelper.Next(0, 3) == 0) _hoverDust.PlaceDust(middle + (around - middle) * 76, new Color(140, 220, 255));
            if (_slowLeft > 0 && RandomHelper.Next(0, 2) == 0) _hoverDust.PlaceDust(new Vector2(RandomHelper.NextFloat(0, Game1.VirtualWidth), RandomHelper.NextFloat(60, FloorY)), new Color(120, 160, 255));
            if (_reviveGlow > 0) _hoverDust.PlaceDust(middle + (around - middle) * RandomHelper.NextFloat(20, 60), new Color(255, 220, 110));
        }

        /// <summary>
        /// Runs the end of the level, clears obstacles, rolls the door in, then walks him through it
        /// </summary>
        private void UpdateEnding(float dt)
        {
            float width = ScreenManager.GraphicsDevice.Viewport.Width;
            if (_endPhase == 0 && _scrollTime >= EndTime - ClearTime)
            {
                _endPhase = 1;
                _noGapFrom = FloorScroll + width + Tilemap.TileSize * 2;  // only floor that's not on screen yet goes solid
            }
            if (_endPhase == 0) return;

            // Nothing new shows up from here on
            _spawnTimer = 0; _spikeTimer = 99; _laserTimer = 99; _gemTimer = 99; _diamondTimer = 99;

            switch (_endPhase)
            {
                case 1:
                    if (_scrollTime >= EndTime) { _endPhase = 2; _doorX = _doorStartX = width + _doorTexture.Width / 12f; }
                    break;
                case 2:
                    // Slides in with the floor, slowing down (constant braking) so it stops dead center
                    _doorX -= SpikeSpeed * dt * _scrollSpeed;
                    float left = _doorX - width / 2;
                    _scrollSpeed = MathF.Max(0.06f, MathF.Sqrt(MathHelper.Clamp(left / (_doorStartX - width / 2), 0, 1)));
                    if (left <= 1)
                    {
                        _doorX = width / 2;
                        _scrollSpeed = 0;
                        _endPhase = 3;
                        _runner.AutoWalkTo = _doorX;
                    }
                    break;
                case 3:
                    if (MathF.Abs(_runner.Position.X - _doorX) <= 3)
                    {
                        _endPhase = 4;
                        ScreenManager.AddScreen(new PromptScreen("Something big waits inside...", "Are you ready, or shop first?", "Enter", "Shop first", () => { _endPhase = 5; _endTimer = 0; _shockwaveSound.Play(0.6f, -0.6f, 0f); }, runMusic: true));  // low rumble once you say go
                    }
                    break;
                case 5:
                    // Rumble, the door and the whole screen shake before it gives
                    _endTimer += dt;
                    Shake(3f + 4f * _endTimer / RumbleTime, 0.1f);
                    if (RandomHelper.Next(0, 3) == 0) _rockDust.PlaceDust(new Vector2(_doorX + RandomHelper.NextFloat(-120, 120), SpikeBottom - RandomHelper.NextFloat(0, 180)), new Color(120, 110, 100));
                    if (_endTimer > RumbleTime)
                    {
                        _endPhase = 6; _endTimer = 0; Shake(12f, 0.35f); _crashSound.Play(0.9f, -0.4f, 0f);  // BOOM it flies open
                        var middle = new Vector2(_doorX, FloorY - _doorTexture.Height / 2f);
                        for (int k = 0; k < 14; k++)
                        {
                            float a = k * MathHelper.TwoPi / 14 + RandomHelper.NextFloat(-0.2f, 0.2f);
                            _stars.Add((middle, new Vector2(MathF.Cos(a), MathF.Sin(a)) * RandomHelper.NextFloat(220, 360), 0));
                        }
                    }
                    break;
                case 6:
                    _endTimer += dt;
                    Shake(10f, 0.1f);  // keeps rattling while it opens
                    if (_endTimer > DoorFrameTime * 2f) { _endPhase = 7; _endTimer = 0; }
                    break;
                case 7:
                    _endTimer += dt;
                    if (_endTimer > 1.1f)
                    {
                        // In he goes, down the hallway into the boss arena (gems get paid out at the end of the arena)
                        _endPhase = 8;
                        _score += DoorClearBonus;
                        _isGameOver = true;
                        _dash.IsEmitting = false;
                        _drillLoop.Stop();
                        ExitScreen();
                        ScreenManager.AddScreen(new ArenaScreen(_score));
                    }
                    break;
            }
        }

        private void BreakGem(int g)
        {
            var box = GemBounds(_gems[g]);
            _crash.PlaceCrash(new Vector2(_gems[g].X, box.Top + box.Height / 2), _gems[g].Tint);
            _crashSound.Play(0.8f, 0.5f, 0f);  // higher pitch, sounds glassy
            Shake(4f, 0.2f);
            _score += GemPoints;
            _gems.RemoveAt(g);
        }

        /// <summary>
        /// Gemstones slide in like spikes, floor ones anywhere, hanging ones only in the middle cave
        /// </summary>
        private void UpdateGems(float dt)
        {
            float width = ScreenManager.GraphicsDevice.Viewport.Width;
            _gemTimer -= dt;
            if (_gemTimer <= 0)
            {
                // Floor gems come from the spike slot now, this timer only hangs gems from the roof in the middle cave
                bool top = true;
                float x = width + 40;
                if (CaveZones.At(_scrollTime).From < 1) _gemTimer = 1f;  // orange and green caves only
                else if (TooCloseToObstacle(x)) _gemTimer = 0.3f;  // no stacking on spikes or walls
                else
                {
                    _gems.Add((x, top, GemColors[RandomHelper.Next(0, GemColors.Length - 1)], RandomHelper.NextFloat(GemTipMin, GemTipMax)));  // random size, never huge
                    _gemTimer = RandomHelper.NextFloat(MathHelper.Lerp(5f, 3f, Difficulty), MathHelper.Lerp(8f, 5f, Difficulty));  // orange cave is the hard one, these come often
                }
            }

            for (int g = _gems.Count - 1; g >= 0; g--)
            {
                _gems[g] = (_gems[g].X - SpikeSpeed * dt, _gems[g].Top, _gems[g].Tint, _gems[g].Tip);
                if (_safeTime <= 0 && _hurtTime <= 0 && _runner.Bounds.CollidesWith(GemBounds(_gems[g])))
                {
                    if (Blocked()) { BreakGem(g); continue; }
                    Hurt(GemDamage, new Color(255, 90, 200), DeathCause.Spike);
                    if (_isGameOver || _safeTime > 0) return;
                }
                if (_gems[g].X < -60) _gems.RemoveAt(g);
            }
        }

        /// <summary>
        /// Diamonds show up in a line, a jump arc or a zigzag, grab them for points
        /// </summary>
        private void UpdateDiamonds(float dt)
        {
            float width = ScreenManager.GraphicsDevice.Viewport.Width;
            _diamondTimer -= dt;
            if (_diamondTimer <= 0)
            {
                _diamondTimer = RandomHelper.NextFloat(4f, 7f);  // a lil more often
                // Arcs only go over holes (centered on the hole so following them jumps you over), otherwise a line or zigzag
                float start = width + 30;
                float ground = FloorY - 40;
                bool arc = false;
                bool HoleAt(float sx) => _floor.TileAtLooping(0, FloorScroll, sx, FloorTile) >= 9;  // gap plus its crumbly edges
                for (float ahead = 0; ahead < 700 && !arc; ahead += 4)
                {
                    if (!HoleAt(width + ahead) || (ahead > 0 && HoleAt(width + ahead - 4))) continue;  // start of a hole
                    float end = ahead;
                    while (HoleAt(width + end + 4)) end += 4;
                    float center = width + (ahead + end) / 2;
                    if (center - 3 * 38 >= width + 30) { arc = true; start = center - 3 * 38; }  // middle diamond lands right over the middle
                    else ahead = end;  // too close to center an arc on, skip past it
                }
                if (!arc && HoleAt(width + 60)) { _diamondTimer = 0.5f; return; }  // dont lay floor diamonds into a hole, try again in a sec
                foreach (var (dx, dy) in arc ? DiamondArc() : DiamondPatterns[RandomHelper.Next(0, DiamondPatterns.Length - 1)])
                {
                    float y = ground + dy;
                    var spot = new BoundingRectangle(start + dx - 20, y - 20, 40, 40);
                    if (_spikes.Exists(sp => SpikeBounds(sp).CollidesWith(spot))) continue;  // never inside a spike or wall, near hanging gems is fine (risky grab)
                    _diamonds.Add(new Vector2(start + dx, y));
                }
            }

            var me = _runner.Bounds;
            for (int d = _diamonds.Count - 1; d >= 0; d--)
            {
                _diamonds[d] -= new Vector2(SpikeSpeed * dt, 0);
                if (RandomHelper.Next(0, 40) == 0) _hoverDust.PlaceDust(_diamonds[d] + new Vector2(RandomHelper.NextFloat(-8, 8), -6), new Color(220, 250, 255));  // twinkle
                if (me.CollidesWith(new BoundingRectangle(_diamonds[d].X - 10, _diamonds[d].Y - 10, 20, 20)))
                {
                    _score += DiamondPoints;
                    _hoverDust.PlaceDust(_diamonds[d], new Color(150, 240, 255));
                    _pickupSound.Play(0.35f, 0.6f, 0f);  // little ding
                    _coinPops.Add((_diamonds[d], 0));
                    Achievements.Add("diamonds");
                    _diamonds.RemoveAt(d);
                }
                else if (_diamonds[d].X < -30) _diamonds.RemoveAt(d);
            }
        }

        // Diamond shapes as offsets from the first one (x along, y up from the floor line), the jump arc is saved for holes
        private static (float, float)[] DiamondArc() => Enumerable.Range(0, 7).Select(k => (k * 38f, -150 * MathF.Sin(MathHelper.Pi * k / 6))).ToArray();
        private static readonly (float, float)[][] DiamondPatterns =
        {
            Enumerable.Range(0, 7).Select(k => (k * 38f, 0f)).ToArray(),                                   // line along the floor
            Enumerable.Range(0, 7).Select(k => (k * 38f, k % 2 == 0 ? 0f : -80f)).ToArray(),               // zigzag, hop hop
            Enumerable.Range(0, 6).Select(k => (k * 38f, -k * 22f)).ToArray(),                             // stairs going up, jump at the end
            Enumerable.Range(0, 10).Select(k => (k % 5 * 38f, k < 5 ? 0f : -45f)).ToArray(),               // two rows stacked
            Enumerable.Range(0, 8).Select(k => (k * 38f, -40 - 35 * MathF.Sin(k * 0.9f))).ToArray(),       // wave
            new (float, float)[] { (0, -40), (38, -80), (38, 0), (76, -120), (76, -40), (114, -80), (114, 0), (152, -40) },  // big diamond shape (heh)
        };

        // Flat band in front of his chest, the drill only gets what's in it (long but thin, so aim matters)
        private BoundingRectangle DrillBand()
        {
            var r = _runner.Bounds;
            return new BoundingRectangle(r.X + r.Width / 2, r.Y + 22, r.Width / 2 + DrillReach, 26);
        }

        // Closest thing in the band, kind 0 = wall, 1 = rock, 2 = gem (crystal spikes are too hard for it)
        private (int Kind, int Index) DrillTarget()
        {
            var band = DrillBand();
            (int, int) best = (-1, -1);
            float bestX = float.MaxValue;
            void Try(int kind, int index, float x, bool hit) { if (hit && x < bestX) { bestX = x; best = (kind, index); } }
            for (int i = 0; i < _spikes.Count; i++) Try(0, i, SpikeBounds(_spikes[i]).Left, _spikes[i].IsWall && band.CollidesWith(SpikeBounds(_spikes[i])));
            for (int b = 0; b < _bolders.Count; b++) Try(1, b, _bolders[b].Bounds.Center.X, band.CollidesWith(_bolders[b].Bounds));
            for (int g = 0; g < _gems.Count; g++) Try(2, g, GemBounds(_gems[g]).Left, band.CollidesWith(GemBounds(_gems[g])));
            return best;
        }

        // Where the cracks go on the target
        private Vector2 DrillPoint((int Kind, int Index) t)
        {
            var band = DrillBand();
            return t.Kind switch
            {
                0 => new Vector2(SpikeBounds(_spikes[t.Index]).Left + 6, band.Y + band.Height / 2),
                1 => _bolders[t.Index].Bounds.Center,
                _ => new Vector2(_gems[t.Index].X, GemBounds(_gems[t.Index]).Top + GemBounds(_gems[t.Index]).Height / 2),
            };
        }

        /// <summary>
        /// Holding the drill on something cracks it more and more till it breaks (walls take the longest)
        /// </summary>
        private void UpdateDrill(float dt)
        {
            var target = DrillTarget();
            if (target != _drillLast) _drillProgress = 0;  // moved onto something new, start over
            _drillLast = target;
            if (target.Kind < 0) return;

            bool maxed = Shop.IsMaxed(ItemId.Drill);
            float time = target.Kind switch { 0 => 0.7f, 1 => 0.3f, _ => 0.4f } * (maxed ? 0.5f : 1f);
            _drillProgress += dt / time;
            if (RandomHelper.Next(0, 2) == 0) _rockDust.PlaceDust(DrillPoint(target), new Color(200, 190, 170));  // grinding dust
            if (_drillProgress < 1) return;

            if (target.Kind == 0) BreakObstacle(target.Index);
            else if (target.Kind == 1) BreakRock(target.Index);
            else BreakGem(target.Index);
            _drillProgress = 0;
            _drillLast = (-1, -1);
        }

        // Jagged cracks spreading from the drill point, more of them and longer as it gets close to breaking
        private void DrawDrillCracks()
        {
            if (_drillProgress <= 0 || _drillLast.Kind < 0) return;
            var target = DrillTarget();
            if (target != _drillLast) return;
            var center = DrillPoint(target);
            int cracks = 2 + (int)(_drillProgress * 5);
            for (int c = 0; c < cracks; c++)
            {
                // same angles every frame so the cracks grow instead of jumping around
                float angle = c * 2.4f + 0.7f;
                var p = center;
                for (int seg = 0; seg < 3; seg++)
                {
                    angle += (c * 7 + seg * 3) % 5 * 0.25f - 0.5f;
                    float length = 6 + 10 * _drillProgress;
                    var next = p + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length;
                    var line = next - p;
                    ScreenManager.SpriteBatch.Draw(_pixel, p, null, new Color(20, 15, 10) * 0.9f * TransitionAlpha, MathF.Atan2(line.Y, line.X), new Vector2(0, 0.5f), new Vector2(line.Length(), 2), SpriteEffects.None, 0f);
                    p = next;
                }
            }
        }


        // Circle outline made of small square dots
        private void DrawSwipe()
        {
            // 0 to 1 over the swipe, the arc grows from above his head down to in front of his feet
            float t = 1 - _swipeLeft / SwipeLength;
            float start = -MathHelper.PiOver2;
            bool around = _weapon == ItemId.QuakeHammer;  // big swing goes all the way around him so it hits behind too
            float end = start + MathHelper.Pi * (around ? 2f : 0.85f) * MathF.Min(1, t * 2);
            float fade = MathF.Min(1, (1 - t) * 2);   // fades out over the second half
            var center = SmashPoint();

            // The hammer swings from his shoulder along the arc (art points up-right, about -56 degrees)
            var shoulder = around ? center : new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + 40);
            var tip = center + new Vector2(MathF.Cos(end), MathF.Sin(end)) * _swipeReach;
            float swing = MathF.Atan2(tip.Y - shoulder.Y, tip.X - shoulder.X) + MathHelper.ToRadians(56);
            bool pick = _weapon == ItemId.Pickaxe;
            ScreenManager.SpriteBatch.Draw(pick ? _pickaxeTexture : _hammerTexture, shoulder, null, Color.White * fade * TransitionAlpha, swing, pick ? new Vector2(9, 90) : new Vector2(6, 92), around ? _swipeReach / 92f : 1f, SpriteEffects.None, 0f);  // stretched to reach the bigger circle

            // Thick dots along the arc, thinner and dimmer toward the tail
            for (float a = start; a <= end; a += 0.04f)
            {
                float along = (a - start) / (end - start + 0.001f);
                int size = (int)(4 + 6 * along);
                var dot = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * _swipeReach;
                ScreenManager.SpriteBatch.Draw(_pixel, new Rectangle((int)dot.X - size / 2, (int)dot.Y - size / 2, size, size), Color.White * along * fade * TransitionAlpha);
            }
        }

        // Thin hollow ring (lots of 2px dots packed so close it reads as a solid line)
        private float _ringAlpha = 1f;  // smash ring fade
        private void DrawDottedCircle(Vector2 center, float radius, Color color)
        {
            int steps = Math.Max(48, (int)(radius * 3.2f));  // about 2px apart
            for (int a = 0; a < steps; a++)
            {
                var dot = center + new Vector2(MathF.Cos(a * MathHelper.TwoPi / steps), MathF.Sin(a * MathHelper.TwoPi / steps)) * radius;
                ScreenManager.SpriteBatch.Draw(_pixel, new Rectangle((int)dot.X - 1, (int)dot.Y - 1, 2, 2), color);
            }
        }

        /// <summary>
        /// Draws an ability icon, dim while recharging and refilling from the bottom with the seconds left on it, its key under it when ready
        /// </summary>
        private SoundEffect _notReadySound;
        private readonly Dictionary<Texture2D, Rectangle> _iconRects = new();  // where each HUD icon got drawn, so a press can squash it

        // Pressed while it's still recharging, squash the icon anyway and bonk
        private void NotReady(Texture2D icon)
        {
            if (icon != null && _iconRects.TryGetValue(icon, out var rect)) MenuButton.Trigger(rect);
            _notReadySound.Play(0.6f, 0f, 0f);
        }

        private readonly Dictionary<float, float> _lastCooldown = new();  // icon x -> cooldown last frame, a jump up means it just got used
        private void DrawCooldownIcon(Texture2D icon, Vector2 position, float cooldownLeft, float cooldown, string key)
        {
            if (icon == null) return;  // empty slot
            var full = new Rectangle((int)position.X, (int)position.Y, 48, 48);
            _iconRects[icon] = full;
            if (_lastCooldown.TryGetValue(position.X, out float before) && cooldownLeft > before + 0.01f) MenuButton.Trigger(full);  // just used, squash it
            _lastCooldown[position.X] = cooldownLeft;
            var squashed = MenuButton.Squash(full, false);
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            float iconScale = 48f / icon.Width;  // HUD art is 64px and item art is 48px, both end up 48 on screen
            float size = icon.Width * iconScale;

            int fillHeight = (int)(icon.Height * (1 - cooldownLeft / cooldown));
            var fill = new Rectangle(0, icon.Height - fillHeight, icon.Width, fillHeight);
            var squashScale = new Vector2(squashed.Width / (float)icon.Width, squashed.Height / (float)icon.Height);
            spriteBatch.Draw(icon, squashed.Location.ToVector2(), null, Color.White * 0.3f * TransitionAlpha, 0f, Vector2.Zero, squashScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(icon, squashed.Location.ToVector2() + new Vector2(0, fill.Y * squashScale.Y), fill, Color.White * TransitionAlpha, 0f, Vector2.Zero, squashScale, SpriteEffects.None, 0f);

            string label = cooldownLeft > 0 ? cooldownLeft.ToString("0.0") : key;
            float labelScale = cooldownLeft > 0 ? 0.45f : 0.35f;
            Vector2 labelSize = font.MeasureString(label) * labelScale;
            float labelY = cooldownLeft > 0 ? position.Y + (size - labelSize.Y) / 2 : position.Y + size + 2;
            spriteBatch.DrawString(font, label, new Vector2(position.X + (size - labelSize.X) / 2, labelY), Color.White * TransitionAlpha, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        }

        // Where a pillar is drawn, stretched from the top of the screen to the floor
        private Rectangle WallRectangle(float x) => new Rectangle((int)(x - _wallTexture.Width / 2f), 0, _wallTexture.Width, (int)SpikeBottom + 8);

        /// <summary>
        /// Hit box of a ground obstacle (the whole wall, or just the thin middle of a crystal)
        /// </summary>
        private BoundingRectangle SpikeBounds((float X, bool IsWall) spike)
        {
            return spike.IsWall
                ? new BoundingRectangle(WallRectangle(spike.X).X, 0, _wallTexture.Width, SpikeBottom)
                : new BoundingRectangle(spike.X - 24, SpikeBottom - 66, 48, 66);
        }

        /// <summary>
        /// Drops a rock at an X position, unless a rock close beside it is still near the top (so they don't stack)
        /// </summary>
        private void SpawnBolder(float x)
        {
            x = MathHelper.Clamp(x, RockMinX, RockMaxX);
            var ahead = _bolders.FindLast(b => MathF.Abs(b.Position.X - x) < 130);
            if (ahead != null && ahead.Position.Y < 250) return;

            // Never faster than the rock ahead in its path, so it can't catch up and overlap
            var bolder = new Bolders(x, ahead?.FallSpeed ?? float.MaxValue);
            bolder.LoadContent(_content);
            _bolders.Add(bolder);
        }

        /// <summary>
        /// Takes damage (unless still blinking from the last hit) and flashes red by how much it hurt, ending the run at 0
        /// </summary>
        private void Hurt(float damage, Color color, DeathCause cause)
        {
            if (_hurtTime > 0) return;
            _dmgFrom = _dmgFill > 0 ? DamageGhost() : _healFill > 0 ? ShownHealth() : _health;
            _healFill = 0;  // a hit cancels a heal still filling
            _health -= damage;
            _dmgFill = HealFillTime;
            if (_health <= 0.001f)
            {
                Crash(color, cause);
                return;
            }
            _hurtTime = 1f;
            _flashTime = 0.4f;
            _flashStrength = MathHelper.Clamp(damage * 2, 0.3f, 1f);
            Shake(4f + 8f * damage, 0.3f);
            _crashSound.Play();
        }

        // Points per smash (the Lucky Charm boosts gems instead now)
        private static int Points(int points) => points;

        /// <summary>
        /// Ends the run with a burst in the color of what hit him, shake, crash sound and music off
        /// </summary>
        private void Crash(Color color, DeathCause cause)
        {
            // Phoenix Heart, back up once with some health and a moment to get clear
            if (_armor == ItemId.PhoenixHeart && !_revived)
            {
                _revived = true;
                Heal(MathF.Max(0.05f, _maxHealth * Shop.Value(ItemId.PhoenixHeart)));
                _safeTime = 2f;
                _hurtTime = 0;
                _falling = false;
                _runner.Revive();
                _crash.PlaceCrash(_runner.Position - new Vector2(0, 60), new Color(255, 220, 90));
                Shake(8f, 0.4f);
                _reviveSound.Play(0.8f, 0f, 0f);  // aaaaah
                _reviveGlow = 2f;
                _spikes.RemoveAll(s => _runner.Bounds.CollidesWith(SpikeBounds(s)));  // dont come back inside a wall lol
                return;
            }

            _crash.PlaceCrash(_runner.Position - new Vector2(0, 60), color);
            _dash.IsEmitting = false; // Stop the trail when he's hit
            Shake(12f, 0.5f);         // Big shake on crash
            _isGameOver = true;       // End the game
            _drillLoop.Stop();
            _crashSound.Play();       // Play Crash sound
            MediaPlayer.Stop();       // Stop the music
            _health = 0;              // walls kill outright
            _flashTime = 1f;          // stays red while frozen
            _flashStrength = 1f;
            Shop.AddGems(Shop.GemsFromScore(_score));
            ScreenManager.AddScreen(new GameOverScreen(this, _score, cause));
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {

            // Smash (Update uses it)
            if (Controls.IsPressed(PlayerAction.Smash)) _smashPressed = true;

        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            // The world that will get shaked
            var shake = Matrix.CreateTranslation(_shakeOffset.X, _shakeOffset.Y, 0) * KickMatrix(ScreenManager.GraphicsDevice.Viewport);
            spriteBatch.Begin(transformMatrix: shake);

            // Which cave color we're in, during a fade the next color gets drawn over the old one a lil more each frame
            var (zoneFrom, zoneTo, zoneBlend) = CaveZones.At(_scrollTime);
            var zonePasses = zoneBlend > 0 ? new[] { (zoneFrom, 1f), (zoneTo, zoneBlend) } : new[] { (zoneFrom, 1f) };

            // Cave layers scroll left so he looks like he's running deeper in. The screen is still a lil big to allow for shake to look good
            for (int i = 0; i < _caveLayers.Length; i++)
            {
                float offset = _scrollTime * CaveLayerInfo[i].Speed % _caveLayers[i].Width;
                foreach (var (zone, weight) in zonePasses)
                {
                    var texture = _caveLayerZones[i][zone];
                    var color = (i == 0 ? Color.White * 0.6f : Color.White) * weight * TransitionAlpha;
                    for (float x = -MathF.Floor(offset) - 16; x < viewport.Width + 16; x += texture.Width)
                    {
                        spriteBatch.Draw(texture, new Vector2(x, CaveLayerInfo[i].Y), color);
                    }
                }
            }

            // The tilemap floor scrolls at the same speed the spikes slide so everything looks stuck to it
            foreach (var (zone, weight) in zonePasses)
            {
                _floor.Tileset = _floorZones[zone];
                _floor.DrawLooping(spriteBatch, 0, FloorScroll, FloorY, Color.White * weight * TransitionAlpha, viewport.Width, FloorTile);
            }

            // Laser warnings, a thin blinking line from the emitter on the ceiling
            foreach (var (x, time, _) in _lasers)
            {
                // Turret drops down out of the roof, slides along after him, then fires straight down
                float drop = MathF.Min(1, time / TurretSlide);
                float lift = MathHelper.Clamp((time - LaserWarn - LaserFire) / TurretSlide, 0, 1);  // after firing it goes back up the way it came
                float hidden = MathF.Max((1 - drop) * (1 - drop), lift * lift);
                int turretY = (int)(-_turretTexture.Height * hidden);  // eases in so it settles
                if (time < LaserWarn && drop >= 1 && (int)(time * 12) % 2 == 0)
                    spriteBatch.Draw(_pixel, new Rectangle((int)x - 1, TurretBottom, 2, (int)FloorY - TurretBottom), new Color(80, 200, 255) * 0.6f * TransitionAlpha);
                spriteBatch.Draw(_turretTexture, new Vector2(x - _turretTexture.Width / 2f, turretY), Color.White * TransitionAlpha);
            }

            // The end door with the cave wall closing everything off on the far side
            if (_endPhase >= 2)
            {
                int frameWidth = _doorTexture.Width / 6;
                int frame = _endPhase < 6 ? 0 : _endPhase == 6 && _endTimer < DoorFrameTime ? 3 : 5;  // skips the in between frames
                int jitter = _endPhase == 5 ? RandomHelper.Next(-2, 2) : _endPhase == 6 ? RandomHelper.Next(-5, 5) : 0;  // rattles while it rumbles
                // Right before it opens it rocks back and forth on its bottom +-6 degrees
                float rattle = _endPhase == 5 && _endTimer > RumbleTime - RattleTime ? MathHelper.ToRadians(6f) * MathF.Sin((_endTimer - (RumbleTime - RattleTime)) / RattleTime * MathHelper.TwoPi * 3) : 0f;
                spriteBatch.Draw(_doorTexture, new Vector2(_doorX + jitter, FloorY + 10), new Rectangle(frame * frameWidth, 0, frameWidth, _doorTexture.Height),
                    Color.White * TransitionAlpha, rattle, new Vector2(frameWidth / 2f, _doorTexture.Height), 1f, SpriteEffects.None, 0f);
                // Burst of stars, little 4 point sparkles that shrink as they fade
                foreach (var (pos, _, age) in _stars)
                {
                    float t = age / StarLife;
                    int arm = (int)(10 * (1 - t)) + 2;
                    var c = Color.Lerp(Color.White, new Color(255, 220, 120), t) * (1 - t) * TransitionAlpha;
                    spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - arm, (int)pos.Y - 1, arm * 2, 3), c);
                    spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 1, (int)pos.Y - arm, 3, arm * 2), c);
                }
            }

            // Draw the ground obstacles, walls and crystals take on the cave color too
            foreach (var spike in _spikes)
            {
                foreach (var (zone, weight) in zonePasses)
                {
                    if (spike.IsWall)
                        spriteBatch.Draw(_wallZones[zone], WallRectangle(spike.X), Color.White * weight * TransitionAlpha);
                    else
                        spriteBatch.Draw(_spikeZones[zone], new Vector2(spike.X - SpikeSource.Width / 2f, SpikeBottom - SpikeSource.Height), SpikeSource, Color.White * weight * TransitionAlpha);
                }
            }

            // Gemstones, floor ones point up, roof ones hang down (flipped)
            foreach (var gem in _gems)
            {
                var box = GemBounds(gem);
                if (gem.Top)
                {
                    // Scaled evenly (no stretching), hangs from the roof down to its tip so bigger ones reach lower
                    int h = (int)(gem.Tip / (1 - GemBuried)), w = h * _gemTexture.Width / _gemTexture.Height;
                    spriteBatch.Draw(_gemTexture, new Rectangle((int)gem.X - w / 2, (int)gem.Tip - h, w, h), null, Color.White * TransitionAlpha, 0f, Vector2.Zero, SpriteEffects.FlipVertically, 0f);
                }
                else
                    spriteBatch.Draw(_gemTexture, new Rectangle((int)gem.X - _gemTexture.Width / 2, (int)SpikeBottom - _gemTexture.Height + 4, _gemTexture.Width, _gemTexture.Height), Color.White * TransitionAlpha);
            }
            // Diamonds bob a little so they catch your eye
            foreach (var d in _diamonds)
                spriteBatch.Draw(_diamondTexture, new Rectangle((int)d.X - 12, (int)(d.Y - 12 + 3 * MathF.Sin(_scrollTime * 5 + d.X * 0.05f)), 24, 24), Color.White * TransitionAlpha);
            DrawDrillCracks();

            // Blinking red ! at the right edge while a wall is still off screen
            foreach (var spike in _spikes)
            {
                if (spike.IsWall && WallRectangle(spike.X).Left > viewport.Width && (int)(_scrollTime * 10) % 2 == 0)
                {
                    spriteBatch.DrawString(font, "!", new Vector2(viewport.Width - 40, 200), Color.Red * TransitionAlpha, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
                }
            }

            // Quake shockwaves, a jagged bump running along the floor
            foreach (var x in _quakes)
            {
                for (int k = 0; k < 5; k++)
                    spriteBatch.Draw(_pixel, new Rectangle((int)x - k * 14 - 6, (int)FloorY - 22 + k * 4, 10, 22 - k * 4), new Color(190, 210, 255) * (0.5f - k * 0.09f) * TransitionAlpha);
            }

            // Rockets, the foam rocket pointing where it flies (the flame glows in the light pass)
            foreach (var (rocket, angle) in _rockets)
            {
                // Rotated to where it's flying, body trails behind the tip
                spriteBatch.Draw(_rocketTexture, rocket, null, Color.White * TransitionAlpha, angle, new Vector2(_rocketTexture.Width, _rocketTexture.Height / 2f), 1.3f, SpriteEffects.None, 0f);
            }

            // Smash swipe in front of him
            if (_swipeLeft > 0) DrawSwipe();

            // Smash range ring around his head (bright when ready), rocks inside it get a yellow ring
            if (!_isGameOver && SmashRingOpacity > 0)
            {
                var head = SmashPoint();
                // Sits at 15% while recharging, then slowly fades up to full once it's ready (all scaled by the ring setting)
                float target = _smashCooldownLeft > 0 ? 0.15f : 1f;
                _ringAlpha = target < _ringAlpha ? target : MathHelper.Min(target, _ringAlpha + (float)gameTime.ElapsedGameTime.TotalSeconds * 1.5f);
                float ready = _ringAlpha * SmashRingOpacity;
                DrawDottedCircle(head, SmashReach(), Color.White * ready * TransitionAlpha);
                foreach (var bolder in _bolders)
                {
                    if (InSmashRange(bolder))
                        DrawDottedCircle(bolder.Bounds.Center, bolder.Bounds.Radius, Color.Yellow * ready * TransitionAlpha);
                }
            }

            // Draw Runner (aka the player)
            // He gets his own batch with PointClamp so his pixel art stays sharp (the rest stays smooth)
            spriteBatch.End();
            _hoverDust.TransformMatrix = shake;
            _hoverDust.Draw(gameTime);
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: shake);

            // The lantern hangs on his back hip, drawn first so he covers part of it
            float walkIn = _endPhase >= 7 ? MathF.Max(0, 1 - _endTimer) : 1f;  // fades into the tunnel
            spriteBatch.Draw(_lanternTexture, LanternHandle(), null, Color.White * walkIn * TransitionAlpha, LanternSwing(), new Vector2(_lanternTexture.Width / 2f, 0), 1f, SpriteEffects.None, 0f);

            // Red flash after a hit (stronger for bigger hits), then blinks while he can't be hurt
            float hurtRed = _hurtTime > 0 ? MathF.Min(1, _hurtTime * 1.3f) : 0;  // full red on the hit, fades back to normal
            if (_flashTime > 0 && _hurtTime <= 0) _runner.Color = Color.Lerp(PlayerTint, Color.Red, _flashStrength) * TransitionAlpha;
            else if (_hurtTime > 0) _runner.Color = Color.Lerp(PlayerTint, Color.Red, hurtRed) * ((int)(_hurtTime * 10) % 2 == 0 ? 0.35f : 1f) * TransitionAlpha;  // red fading back = damage, the blink = can't be hit yet
            else if (_reviveGlow > 0) _runner.Color = ((int)(_reviveGlow * 8) % 2 == 0 ? new Color(255, 230, 140) : PlayerTint * 0.5f) * TransitionAlpha;  // gold blink after coming back
            else if (_safeTime > 0) _runner.Color = PlayerTint * 0.5f * TransitionAlpha;  // See through while untouchable so you know its working
            else if (_mirrorFlash > 0) _runner.Color = Color.Lerp(PlayerTint, new Color(80, 200, 255), _mirrorFlash / _mirrorTime) * TransitionAlpha;  // laser blue soaked up by the Mirror Helm, fades as the beam lets go
            else _runner.Color = PlayerTint * TransitionAlpha;
            _runner.Color *= walkIn;
            _skinFx.DrawBehind(spriteBatch, _runner, walkIn * TransitionAlpha);
            _runner.Draw(_isGameOver || (_endPhase >= 3 && _runner.AutoWalkTo is float stop && MathF.Abs(stop - _runner.Position.X) <= 3) ? new GameTime() : gameTime, spriteBatch); // zero time after a crash (or standing at the door) freezes his animation
            _skinFx.Draw(spriteBatch, _pixel, TransitionAlpha);
            spriteBatch.End();
            spriteBatch.Begin(transformMatrix: shake);

            // Health bar over his head while he's hurt (green to red as it drops, longer with the Tough Heart)
            if (_health < _maxHealth || _healFill > 0 || _healFlash > 0 || _dmgFill > 0)
            {
                int barWidth = (int)(60 * _maxHealth);
                float barAlpha = TransitionAlpha;
                int barX = (int)(_runner.Bounds.X + _runner.Bounds.Width / 2 - barWidth / 2f);
                int barY = (int)_runner.Bounds.Y - 16;
                spriteBatch.Draw(_pixel, new Rectangle(barX - 2, barY - 2, barWidth + 4, 9), Color.Black * 0.7f * barAlpha);
                float shown = _healFill > 0 ? ShownHealth() : _health;
                if (_healFill > 0) spriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barWidth * _health / _maxHealth), 5), Mint * 0.35f * barAlpha);  // pale ghost of where it's filling to
                if (_dmgFill > 0) spriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barWidth * DamageGhost() / _maxHealth), 5), new Color(255, 200, 200) * 0.6f * barAlpha);  // pale ghost draining down
                var barColor = _healFlash > 0 ? Color.White : Color.Lerp(Color.Red, Color.LimeGreen, shown / _maxHealth);
                spriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barWidth * shown / _maxHealth), 5), barColor * barAlpha);
            }

            // +1 popping off grabbed diamonds, rises 36px and fades
            foreach (var (pos, age) in _coinPops)
            {
                float t = age / CoinPopLife;
                var at = pos - new Vector2(10, 36 * t + 8);
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 7; x++)
                        if (PlusOne[y][x] == 'X') spriteBatch.Draw(_pixel, new Rectangle((int)at.X + x * 3, (int)at.Y + y * 3, 3, 3), Color.White * (1 - t) * TransitionAlpha);
            }

            // Mint pixel + signs rising 96px off him and fading
            foreach (var (pos, age) in _pluses)
            {
                if (age < 0) continue;
                float t = age / PlusLife;
                var at = pos - new Vector2(0, 96 * t);
                var c = Mint * (1 - t) * TransitionAlpha;
                spriteBatch.Draw(_pixel, new Rectangle((int)at.X - 6, (int)at.Y - 2, 12, 4), c);
                spriteBatch.Draw(_pixel, new Rectangle((int)at.X - 2, (int)at.Y - 6, 4, 12), c);
            }

            // Draw the bolders
            foreach (var bolder in _bolders)
            {
                bolder.Draw(gameTime, spriteBatch);
            }

            spriteBatch.End();

            // Lantern light, a flickering warm halo around the flame (looks really cool in the dark)
            spriteBatch.Begin(blendState: BlendState.Additive, transformMatrix: shake);
            var flame = LanternHandle() + new Vector2(-MathF.Sin(LanternSwing()), MathF.Cos(LanternSwing())) * 26;
            float flicker = (0.85f + 0.1f * MathF.Sin(_lanternTime * 13) + 0.05f * MathF.Sin(_lanternTime * 29)) * walkIn;
            spriteBatch.Draw(_glowTexture, flame, null, new Color(255, 150, 50) * 0.6f * flicker * TransitionAlpha, 0f, new Vector2(8), 14f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_glowTexture, flame, null, new Color(255, 190, 90) * flicker * TransitionAlpha, 0f, new Vector2(8), 6f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_glowTexture, flame, null, new Color(255, 235, 150) * flicker * TransitionAlpha, 0f, new Vector2(8), 2.5f, SpriteEffects.None, 0f);

            // ZAP, firing lasers are added light so they glow (red outside, white hot in the middle)
            // Bubble Shield, a glowing ring around him (blinks when it's about to run out)
            if (_bubbleLeft > 0 && (_bubbleLeft > 1 || (int)(_bubbleLeft * 8) % 2 == 0))
            {
                var middle = new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + _runner.Bounds.Height / 2);
                spriteBatch.Draw(_disc, middle, null, new Color(60, 150, 230) * 0.4f * TransitionAlpha, 0f, new Vector2(32), 78 / 32f, SpriteEffects.None, 0f);
            }

            // Heal Locket, a soft green glow that swells out and fades
            if (_healPulse > 0)
            {
                float t = 1 - _healPulse / 0.5f;
                var center = new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + _runner.Bounds.Height / 2);
                spriteBatch.Draw(_disc, center, null, new Color(90, 255, 130) * (1 - t) * 0.6f * TransitionAlpha, 0f, new Vector2(32), (30 + 60 * t) / 32f, SpriteEffects.None, 0f);
            }

            // Phoenix Heart, a golden halo while he's coming back
            if (_reviveGlow > 0)
                spriteBatch.Draw(_glowTexture, new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + _runner.Bounds.Height / 2), null, new Color(255, 200, 80) * MathF.Min(1, _reviveGlow) * (0.6f + 0.3f * MathF.Sin(_reviveGlow * 20)) * TransitionAlpha, 0f, new Vector2(8), 10f, SpriteEffects.None, 0f);

            // Hover Boots glow under his feet over a hole
            if (_hovering)
                spriteBatch.Draw(_glowTexture, new Vector2(_runner.Position.X, FloorY), null, new Color(80, 180, 255) * 0.8f * TransitionAlpha, 0f, new Vector2(8), new Vector2(4f, 1.5f), SpriteEffects.None, 0f);

            // Rocket flames
            foreach (var (rocket, angle) in _rockets)
                spriteBatch.Draw(_glowTexture, rocket - new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 26, null, new Color(255, 150, 40) * TransitionAlpha, 0f, new Vector2(8), 1.6f, SpriteEffects.None, 0f);

            // Mirror Helm bounce, a red beam shooting right from his helmet
            if (_mirrorFlash > 0)
            {
                float h = 14 * MathF.Min(1, _mirrorFlash / 0.35f) + 2;  // thins out at the end
                var length = new Vector2(viewport.Width * 1.5f, 1);
                spriteBatch.Draw(_pixel, _mirrorFrom, null, new Color(80, 200, 255) * 0.5f * TransitionAlpha, _mirrorAngle, new Vector2(0, 0.5f), length * new Vector2(1, h * 2), SpriteEffects.None, 0f);
                spriteBatch.Draw(_pixel, _mirrorFrom, null, new Color(220, 245, 255) * TransitionAlpha, _mirrorAngle, new Vector2(0, 0.5f), length * new Vector2(1, MathF.Max(2, h * 2 / 3)), SpriteEffects.None, 0f);
            }

            foreach (var (x, time, _) in _lasers)
            {
                if (time < LaserWarn || time >= LaserWarn + LaserFire) continue;  // beam only while firing
                float beam = 1 - (time - LaserWarn) / LaserFire;  // thins out as it ends
                int w = (int)(LaserWidth * (0.4f + 0.6f * beam));
                spriteBatch.Draw(_beamTexture, new Rectangle((int)x - w, TurretBottom, w * 2, (int)FloorY - TurretBottom), Color.White * TransitionAlpha);
                spriteBatch.Draw(_glowTexture, new Vector2(x, FloorY), null, new Color(60, 160, 255) * beam * TransitionAlpha, 0f, new Vector2(8), 4f, SpriteEffects.None, 0f);
                spriteBatch.Draw(_glowTexture, new Vector2(x, TurretBottom), null, new Color(120, 210, 255) * beam * TransitionAlpha, 0f, new Vector2(8), 2.5f, SpriteEffects.None, 0f);
                if (RandomHelper.Next(0, 2) == 0) _hoverDust.PlaceDust(new Vector2(x + RandomHelper.NextFloat(-w, w), FloorY), new Color(120, 210, 255));  // splash where it hits
            }
            spriteBatch.End();

            _embers.TransformMatrix = shake;
            _embers.Opacity = TransitionAlpha;
            _embers.Draw(gameTime);

            // Lit by the lantern, draws him again as added light so he pops off the cave (zero time so his animation doesnt run twice)
            if (!(_hurtTime > 0 && (int)(_hurtTime * 10) % 2 == 0) && !(_flashTime > 0 && _hurtTime <= 0))
            {
                spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp, transformMatrix: shake);
                float red = _hurtTime > 0 ? MathF.Min(1, _hurtTime * 1.3f) : 0;
                _runner.Color = Color.Lerp(new Color(110, 100, 80), new Color(170, 0, 0), red) * walkIn * TransitionAlpha;  // lantern light goes red while hurt
                _runner.Draw(new GameTime(), spriteBatch);
                spriteBatch.End();
            }

            // Partical Stuff
            _rockDust.TransformMatrix = shake;
            _dash.TransformMatrix = shake;
            _crash.TransformMatrix = shake;

            _rockDust.Draw(gameTime); // AlphaBlend first
            _dash.Draw(gameTime);     // Additive on top
            _crash.Draw(gameTime);

            // Text dosnt need to shake
            spriteBatch.Begin();

            // Time Slow tints everything a lil blue so you can tell it's on
            if (_slowLeft > 0) spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(40, 80, 160) * 0.18f * TransitionAlpha);

            // Ability and weapon icons in the top right (whatever is equipped)
            int iconSize = 48;
            bool dash = _ability == ItemId.Dash;
            DrawCooldownIcon(_abilityIcon, new Vector2(viewport.Width - 2 * iconSize - 30, 62), dash ? _runner.DashCooldownLeft : _abilityCooldownLeft, dash ? _runner.DashCooldown : _abilityCooldown, Controls.Label(PlayerAction.Dash));
            DrawCooldownIcon(_weaponIcon, new Vector2(viewport.Width - iconSize - 16, 62), _smashCooldownLeft, SmashCooldown, Controls.Label(PlayerAction.Smash));
            if (_weapon == ItemId.Drill)
            {
                // Fuel bar under the drill, orange while it's overheated
                var bar = new Rectangle(viewport.Width - iconSize - 16, 62 + iconSize + 22, iconSize, 6);
                spriteBatch.Draw(_pixel, new Rectangle(bar.X - 1, bar.Y - 1, bar.Width + 2, bar.Height + 2), Color.Black * 0.7f * TransitionAlpha);
                spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * _fuel / _fuelMax), bar.Height), (_overheated ? Color.OrangeRed : Color.Gold) * TransitionAlpha);
            }
            // Biome progress bar at the top, one chunk per cave color, fills as he runs deeper
            {
                const int barWidth = 300, barHeight = 10;
                int barX = viewport.Width / 2 - barWidth / 2, barY = 18;
                int chunk = barWidth / CaveZones.Count;
                float depth = (_scrollTime) / CaveZones.ZoneLength;  // 1.5 = halfway through the 2nd cave
                int current = Math.Min((int)depth, CaveZones.Count - 1);
                spriteBatch.Draw(_pixel, new Rectangle(barX - 2, barY - 2, barWidth + 4, barHeight + 4), Color.Black * 0.7f * TransitionAlpha);
                for (int z = 0; z < CaveZones.Count; z++)
                {
                    float fill = MathHelper.Clamp(depth - z, 0f, 1f);
                    var color = CaveZones.BarColors[z];
                    spriteBatch.Draw(_pixel, new Rectangle(barX + z * chunk + 1, barY, chunk - 2, barHeight), color * 0.25f * TransitionAlpha);
                    spriteBatch.Draw(_pixel, new Rectangle(barX + z * chunk + 1, barY, (int)((chunk - 2) * fill), barHeight), color * TransitionAlpha);
                }
                string name = CaveZones.Names[current];
                var size = ScreenManager.MenuFont.MeasureString(name) * 0.35f;
                spriteBatch.DrawString(ScreenManager.MenuFont, name, new Vector2(viewport.Width / 2 - size.X / 2, barY + barHeight + 4), CaveZones.BarColors[current] * TransitionAlpha, 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);
            }
            spriteBatch.DrawString(ScreenManager.BigFont, $"Score: {_score}", new Vector2(25, 20), Color.Yellow * TransitionAlpha, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(ScreenManager.BigFont, $"Gems: {Shop.GemsFromScore(_score)}", new Vector2(25, 58), Color.Cyan * TransitionAlpha, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);  // what this run pays out


            spriteBatch.End();
        }
    }
}
