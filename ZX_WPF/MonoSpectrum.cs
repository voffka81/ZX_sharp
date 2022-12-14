using Speccy;
using System;

namespace ZX_sharp
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class MonoSpectrum : Game
    {
        private Computer _speccy;
        private AudioRender _audioRender;

        private Texture2D pixel;
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        public MonoSpectrum(Computer speccy)
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            _speccy = speccy;
            _audioRender = new AudioRender();
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        /// 

        private Array keyArray;
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            //_speccy = new Computer();
            keyArray = Enum.GetValues(typeof(Keys));
            graphics.PreferredBackBufferWidth = Display.Width * 2;  // set this value to the desired width of your window
            graphics.PreferredBackBufferHeight = Display.Height * 2;
            graphics.ApplyChanges();
            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {

            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here


            pixel = new Texture2D(GraphicsDevice, 1, 1);

            pixel.SetData(new Color[] { Color.White });
            // Task.Factory.StartNew(() => _speccy.ExecuteCycle());
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {

            _audioRender.SubmitBuffer(_speccy.AudioSamples);
            // if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            //     Exit();
            KeyboardState state = Keyboard.GetState();

            // If they hit esc, exit
            if (state.IsKeyDown(Keys.Escape))
                Exit();
            if (state.IsKeyDown(Keys.F12))
                _speccy.Reset();

            // Print to debug console currently pressed keys
            foreach (Keys key in keyArray)
            {

                if (state.IsKeyDown(key))
                {
                    _speccy.KeyInput(Map(key), true);
                }
                if (state.IsKeyUp(key))
                {
                    _speccy.KeyInput(Map(key), false);
                }
            }

            _speccy.ExecuteCycle();
            _speccy.DisplayUnit.GetDisplayBuffer();

            // TODO: Add your update logic here
            base.Update(gameTime);
        }

        public SpectrumKeyCode Map(Keys key)
        {
            switch (key)
            {
                case Keys.D1:
                    return SpectrumKeyCode.N1;
                case Keys.D2:
                    return SpectrumKeyCode.N2;
                case Keys.D3:
                    return SpectrumKeyCode.N3;
                case Keys.D4:
                    return SpectrumKeyCode.N4;
                case Keys.D5:
                    return SpectrumKeyCode.N5;
                case Keys.D6:
                    return SpectrumKeyCode.N6;
                case Keys.D7:
                    return SpectrumKeyCode.N7;
                case Keys.D8:
                    return SpectrumKeyCode.N8;
                case Keys.D9:
                    return SpectrumKeyCode.N9;
                case Keys.D0:
                    return SpectrumKeyCode.N0;

                case Keys.Q:
                    return SpectrumKeyCode.Q;
                case Keys.W:
                    return SpectrumKeyCode.W;
                case Keys.E:
                    return SpectrumKeyCode.E;
                case Keys.R:
                    return SpectrumKeyCode.R;
                case Keys.T:
                    return SpectrumKeyCode.T;
                case Keys.Y:
                    return SpectrumKeyCode.Y;
                case Keys.U:
                    return SpectrumKeyCode.U;
                case Keys.I:
                    return SpectrumKeyCode.I;
                case Keys.O:
                    return SpectrumKeyCode.O;
                case Keys.P:
                    return SpectrumKeyCode.P;

                case Keys.A:
                    return SpectrumKeyCode.A;
                case Keys.S:
                    return SpectrumKeyCode.S;
                case Keys.D:
                    return SpectrumKeyCode.D;
                case Keys.F:
                    return SpectrumKeyCode.F;
                case Keys.G:
                    return SpectrumKeyCode.G;
                case Keys.H:
                    return SpectrumKeyCode.H;
                case Keys.J:
                    return SpectrumKeyCode.J;
                case Keys.K:
                    return SpectrumKeyCode.K;
                case Keys.L:
                    return SpectrumKeyCode.L;

                case Keys.Z:
                    return SpectrumKeyCode.Z;
                case Keys.X:
                    return SpectrumKeyCode.X;
                case Keys.C:
                    return SpectrumKeyCode.C;
                case Keys.V:
                    return SpectrumKeyCode.V;
                case Keys.B:
                    return SpectrumKeyCode.B;
                case Keys.N:
                    return SpectrumKeyCode.N;
                case Keys.M:
                    return SpectrumKeyCode.M;

                case Keys.LeftShift:
                    return SpectrumKeyCode.SShift;
                case Keys.RightShift:
                    return SpectrumKeyCode.CShift;
                case Keys.Space:
                    return SpectrumKeyCode.Space;
                case Keys.Enter:
                    return SpectrumKeyCode.Enter;
                default:
                    return SpectrumKeyCode.Invalid;
            }
        }
        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here
            spriteBatch.Begin();

            for (var countX = 0; countX < Display.Width; countX++)
                for (var countY = 0; countY < Display.Height; countY++)
                {

                    spriteBatch.Draw(pixel, new Rectangle(countX * 2, countY * 2, 2, 2), ToColor(_speccy.DisplayUnit.pixelBuffer[countX, countY]));
                }
            spriteBatch.End();
            base.Draw(gameTime);
        }
        public Color ToColor(int rgb)
        {
            return new Color((byte)((rgb & 0xff0000) >> 0x10),
                                  (byte)((rgb & 0xff00) >> 8),
                                  (byte)(rgb & 0xff));
        }
    }
}
