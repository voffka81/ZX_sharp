using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Speccy;
using System;
using System.Threading.Tasks;

namespace ZX_sharp
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class MonoSpectrum : Game
    {
        private Computer _speccy;

        private Texture2D pixel;
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        public MonoSpectrum()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            _speccy = new Computer() ;
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
            return key switch
            {
                Keys.D1 => SpectrumKeyCode.N1,
                Keys.D2 => SpectrumKeyCode.N2,
                Keys.D3 => SpectrumKeyCode.N3,
                Keys.D4 => SpectrumKeyCode.N4,
                Keys.D5 => SpectrumKeyCode.N5,
                Keys.D6 => SpectrumKeyCode.N6,
                Keys.D7 => SpectrumKeyCode.N7,
                Keys.D8 => SpectrumKeyCode.N8,
                Keys.D9 => SpectrumKeyCode.N9,
                Keys.D0 => SpectrumKeyCode.N0,
                Keys.Q => SpectrumKeyCode.Q,
                Keys.W => SpectrumKeyCode.W,
                Keys.E => SpectrumKeyCode.E,
                Keys.R => SpectrumKeyCode.R,
                Keys.T => SpectrumKeyCode.T,
                Keys.Y => SpectrumKeyCode.Y,
                Keys.U => SpectrumKeyCode.U,
                Keys.I => SpectrumKeyCode.I,
                Keys.O => SpectrumKeyCode.O,
                Keys.P => SpectrumKeyCode.P,
                Keys.A => SpectrumKeyCode.A,
                Keys.S => SpectrumKeyCode.S,
                Keys.D => SpectrumKeyCode.D,
                Keys.F => SpectrumKeyCode.F,
                Keys.G => SpectrumKeyCode.G,
                Keys.H => SpectrumKeyCode.H,
                Keys.J => SpectrumKeyCode.J,
                Keys.K => SpectrumKeyCode.K,
                Keys.L => SpectrumKeyCode.L,
                Keys.Z => SpectrumKeyCode.Z,
                Keys.X => SpectrumKeyCode.X,
                Keys.C => SpectrumKeyCode.C,
                Keys.V => SpectrumKeyCode.V,
                Keys.B => SpectrumKeyCode.B,
                Keys.N => SpectrumKeyCode.N,
                Keys.M => SpectrumKeyCode.M,
                Keys.LeftShift => SpectrumKeyCode.SShift,
                Keys.RightShift => SpectrumKeyCode.CShift,
                Keys.Space => SpectrumKeyCode.Space,
                Keys.Enter => SpectrumKeyCode.Enter,
                _ => SpectrumKeyCode.Invalid,
            };
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
       
        private Color ToColor(int rgb)
        {
            return new Color((byte)((rgb & 0xff0000) >> 0x10),
                                  (byte)((rgb & 0xff00) >> 8),
                                  (byte)(rgb & 0xff));
        }
    }
}
