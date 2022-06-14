using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Tetris
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        /*---------------------------------------------*/
        private KinectSensor kinectSensor = null;
        int count = 0;
        private ColorFrameReader colorFrameReader = null;
        private WriteableBitmap colorBitmap = null;
        MultiSourceFrameReader msfr;
        Body[] bodies;

        int times = 0;
        bool isPause = false;
        /*---------------------------------------------*/
        private readonly ImageSource[] tileImages = new ImageSource[]
        {
            new BitmapImage(new Uri("Assets/TileEmpty.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/TileCyan.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/TileBlue.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/TileOrange.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/TileYellow.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/TileGreen.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/TilePurple.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/TileRed.png", UriKind.Relative))
        };

        private readonly ImageSource[] blockImages = new ImageSource[]
        {
            new BitmapImage(new Uri("Assets/Block-Empty.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/Block-I.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/Block-J.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/Block-L.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/Block-O.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/Block-S.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/Block-T.png", UriKind.Relative)),
            new BitmapImage(new Uri("Assets/Block-Z.png", UriKind.Relative))
        };

        private readonly Image[,] imageControls;
        private readonly int maxDelay = 1000;
        private readonly int minDelay = 75;
        private readonly int delayDecrease = 25;

        private GameState gameState = new GameState();

        public MainWindow()
        {
            InitializeComponent();
            imageControls = SetupGameCanvas(gameState.GameGrid);

            /*---------------------------------------------*/
            bodies = new Body[6];
            this.kinectSensor = KinectSensor.GetDefault();
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();

            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            msfr = kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Body | FrameSourceTypes.Color);
            msfr.MultiSourceFrameArrived += msfr_MultiSourceFrameArrived;
            this.kinectSensor.Open();
            /*---------------------------------------------*/

        }


        private Image[,] SetupGameCanvas(GameGrid grid)
        {
            Image[,] imageControls = new Image[grid.Rows, grid.Columns];
            int cellSize = 25;

            for (int r = 0; r < grid.Rows; r++)
            {
                for (int c = 0; c < grid.Columns; c++)
                {
                    Image imageControl = new Image
                    {
                        Width = cellSize,
                        Height = cellSize
                    };

                    Canvas.SetTop(imageControl, (r - 2) * cellSize + 10);
                    Canvas.SetLeft(imageControl, c * cellSize);
                    GameCanvas.Children.Add(imageControl);
                    imageControls[r, c] = imageControl;
                }
            }

            return imageControls;
        }

        private void DrawGrid(GameGrid grid)
        {
            for (int r = 0; r < grid.Rows; r++)
            {
                for (int c = 0; c < grid.Columns; c++)
                {
                    int id = grid[r, c];
                    imageControls[r, c].Opacity = 1;
                    imageControls[r, c].Source = tileImages[id];
                }
            }
        }

        private void DrawBlock(Block block)
        {
            foreach (Position p in block.TilePositions())
            {
                imageControls[p.Row, p.Column].Opacity = 1;
                imageControls[p.Row, p.Column].Source = tileImages[block.Id];
            }
        }

        private void DrawNextBlock(BlockQueue blockQueue)
        {
            Block next = blockQueue.NextBlock;
            NextImage.Source = blockImages[next.Id];
        }

        private void DrawHeldBlock(Block heldBlock)
        {
            if (heldBlock == null)
            {
                HoldImage.Source = blockImages[0];
            }
            else
            {
                HoldImage.Source = blockImages[heldBlock.Id];
            }
        }

        private void DrawGhostBlock(Block block)
        {
            int dropDistance = gameState.BlockDropDistance();

            foreach (Position p in block.TilePositions())
            {
                imageControls[p.Row + dropDistance, p.Column].Opacity = 0.25;
                imageControls[p.Row + dropDistance, p.Column].Source = tileImages[block.Id];
            }
        }

        private void Draw(GameState gameState)
        {
            DrawGrid(gameState.GameGrid);
            DrawGhostBlock(gameState.CurrentBlock);
            DrawBlock(gameState.CurrentBlock);
            DrawNextBlock(gameState.BlockQueue);
            DrawHeldBlock(gameState.HeldBlock);
            ScoreText.Text = $"Score: {gameState.Score}";
        }

        private async Task GameLoop()
        {
            Draw(gameState);

            while (!gameState.GameOver)
            {
                int delay = Math.Max(minDelay, maxDelay - (gameState.Score * delayDecrease));
                await Task.Delay(delay);
                gameState.MoveBlockDown();
                Draw(gameState);
            }

            GameOverMenu.Visibility = Visibility.Visible;
            FinalScoreText.Text = $"Score: {gameState.Score}";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (gameState.GameOver)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Left:
                    gameState.MoveBlockLeft();
                    break;
                case Key.Right:
                    gameState.MoveBlockRight();
                    break;
                case Key.Down:
                    gameState.MoveBlockDown();
                    break;
                case Key.Up:
                    gameState.RotateBlockCW();
                    break;
                case Key.Z:
                    gameState.RotateBlockCCW();
                    break;
                case Key.C:
                    gameState.HoldBlock();
                    break;
                case Key.Space:
                    gameState.DropBlock();
                    break;
                default:
                    return;
            }

            Draw(gameState);
        }

        private async void GameCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            await GameLoop();
        }

        private async void PlayAgain_Click(object sender, RoutedEventArgs e)
        {
            gameState = new GameState();
            GameOverMenu.Visibility = Visibility.Hidden;
            await GameLoop();
        }
        private void msfr_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame msf = e.FrameReference.AcquireFrame();
            if (msf != null)
            {
                using (BodyFrame bodyFrame = msf.BodyFrameReference.AcquireFrame())
                {
                    using (ColorFrame colorFrame = msf.ColorFrameReference.AcquireFrame())
                    {
                        if (bodyFrame != null && colorFrame != null)
                        {
                            FrameDescription colorFrameDescription = colorFrame.FrameDescription;
                            using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                            {
                                this.colorBitmap.Lock();
                                if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                                {
                                    colorFrame.CopyConvertedFrameDataToIntPtr(this.colorBitmap.BackBuffer, (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4), ColorImageFormat.Bgra);
                                    this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                                }
                                this.colorBitmap.Unlock();
                                bodyFrame.GetAndRefreshBodyData(bodies);
                                count = 0;

                                for (int i = 0; i < bodies.Length; i++)
                                {
                                    if (bodies[i].IsTracked)
                                    {
                                        Joint headJoint = bodies[i].Joints[JointType.Head];
                                        Joint neck = bodies[i].Joints[JointType.Neck];
                                        Joint handLeft = bodies[i].Joints[JointType.HandLeft];
                                        Joint handRight = bodies[i].Joints[JointType.HandRight];
                                        Joint shoulderLeft = bodies[i].Joints[JointType.ShoulderLeft];
                                        Joint shoulderRight = bodies[i].Joints[JointType.ShoulderRight];
                                        Joint kneeLeft = bodies[i].Joints[JointType.KneeLeft];
                                        if (headJoint.TrackingState == TrackingState.Tracked)
                                        {
                                            if (times == 0)
                                            {
                                                if (handLeftUp(headJoint, handLeft) == true)
                                                {
                                                    //Container.ActivityBox.ChangeShape();
                                                    //MessageBox.Show(Convert.ToString(headJoint.Position.Y));
                                                    gameState.RotateBlockCCW();
                                                }
                                                if (handLeftLeft(headJoint, handLeft, handRight) == true)
                                                {
                                                    gameState.MoveBlockLeft();
                                                    //MessageBox.Show("handLeftLeft");
                                                }
                                                if (handRightRight(headJoint, handLeft, handRight) == true)
                                                {
                                                    gameState.MoveBlockRight();
                                                    //Container.ActivityBox.MoveRight();
                                                    // MessageBox.Show("handRightRight");
                                                }
                                                if (handRightUp(headJoint, handRight) == true)
                                                {
                                                    // Container.ActivityBox.FastDown();
                                                    //  MessageBox.Show("handRightUp");
                                                    gameState.RotateBlockCW();

                                                }
                                                if (sneak(handRight, kneeLeft) == true)
                                                {
                                                    gameState.DropBlock();
                                                }

                                                if (leftrighttrack(handLeft, handRight) == true)
                                                {
                                                    gameState.HoldBlock();

                                                }
                                                times++;
                                            }

                                            else if (times >= 10)
                                            {
                                                times = 0;
                                            }
                                            else
                                            {
                                                times++;
                                            }

                                            count++;
                                        }
                                    }
                                    else
                                    {
                                        //nothing
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private bool leftrighttrack(Joint handLeft, Joint handRight)
        {
            if (Math.Abs(handLeft.Position.Y - handRight.Position.Y) < 0.1 && Math.Abs(handLeft.Position.X - handRight.Position.X) < 0.1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private bool sneak(Joint handRight,Joint kneeLeft)
        {
            if (handRight.Position.Y <= kneeLeft.Position.Y)       //我不知道高度  
                return true;
            else
                return false;
        }
        private bool handLeftUp(Joint headJoint, Joint handLeft)
        {
            if (handLeft.Position.Y - headJoint.Position.Y > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool handLeftLeft(Joint headJoint, Joint handLeft, Joint handRight)
        {
            if ((handLeft.Position.X < headJoint.Position.X - 0.45) && (handRight.Position.X <= headJoint.Position.X + 0.45))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool handRightRight(Joint headJoint, Joint handLeft, Joint handRight)
        {
            if ((handRight.Position.X > headJoint.Position.X + 0.45) && (handLeft.Position.X >= headJoint.Position.X - 0.45))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool handRightUp(Joint headJoint, Joint handRight)
        {
            if (handRight.Position.Y - headJoint.Position.Y > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
