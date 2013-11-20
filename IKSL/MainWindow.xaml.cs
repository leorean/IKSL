using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Microsoft.Kinect;
using System.Globalization;
using System.IO;
//using openCV;
using System.Threading;
using Emgu.CV;


namespace IKSL
{
    /*
    public class Loop
    {
        public int counter = 0;
        int FPS = 1;
        const int MAXFPS = 30;

        public void count()
        {
            while (true)
            {
                counter = (counter + 1) % MAXFPS / FPS;
                //MainWindow.getInstance().lbCounter.Content = counter;

            }
        }

        public int getCount()
        {
            return counter;
        }
    }*/

    public partial class MainWindow : Window
    {
        const int BLUE = 0;
        const int GREEN = 1;
        const int RED = 2;

        //my kinect sensor
        private KinectSensor sensor;

        //Screen Bitmaps
        private WriteableBitmap mainScreen;
        private WriteableBitmap depthScreen;
        private WriteableBitmap colorScreen;

        //store data received from the camera
        private DepthImagePixel[] rawDepthData;
        
        //byte arrays that hold the generated frames
        private byte[] colorPx;
        private byte[] depthPx;
        private byte[] resultPx;
        
        private Skeleton[] skeletonData;
        private int HandRightX, HandRightY;

        private int frameCounter = -1;
        private int minDepth;
        private int maxDepth;

        private CoordinateMapper coordinateMapper;
        
        int FPS = 10;
        const int MAXFPS = 30;

        public MainWindow()
        {
            InitializeComponent();
            txtPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\kinect.txt";
            
        }
        
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            
            //aquire the connected sensor
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (this.sensor != null)
            {
                this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                //this.sensor.DepthStream.OpenNextFrame(6987);
                //this.sensor.SkeletonStream.Enable();
                
                this.depthPx = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                this.colorPx = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.resultPx = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                
                this.rawDepthData = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                
                this.depthScreen = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.colorScreen = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.mainScreen = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
               
                this.sensor.AllFramesReady += OnAllFramesReady;

                this.Screen1.Source = this.mainScreen;
                this.Screen2.Source = this.colorScreen;
                this.Screen3.Source = this.depthScreen;

                this.btnSave.Click += this.button_save;
                this.btnLoad.Click += this.button_load;

                try
                {
                    this.sensor.Start();

                }
                catch (IOException)
                {
                    this.sensor = null;
                }


                
            }
            else
            {
                MessageBoxResult message = MessageBox.Show("Couldn't find a connected Kinect Sensor", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                //this.Close();
                Environment.Exit(1);
            }

        }

        private void button_save(object sender, RoutedEventArgs e)
        {
            MessageBoxResult message = MessageBox.Show("Save current Data to File?", "Save", MessageBoxButton.OKCancel);
            if (message == MessageBoxResult.OK)
            {
                writeFrame(depthPx, txtPath.Text);
            }
            MessageBox.Show("done.");

        }

        private void button_load(object sender, RoutedEventArgs e)
        {

            byte[] loadArray = readFrame(txtPath.Text);

            this.mainScreen.WritePixels(
                new Int32Rect(0, 0, this.mainScreen.PixelWidth, this.mainScreen.PixelHeight),
                loadArray,
                this.mainScreen.PixelWidth * sizeof(int),
                0);
        }

        /// <summary>
        /// Loads a file and converts it into a byte array.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private byte[] readFrame(String source)
        {

            using (System.IO.StreamReader file = new System.IO.StreamReader(@source))
            {
                String line;
                string[] values;
                StringBuilder s = new StringBuilder();
                while (!file.EndOfStream)
                {
                    line = file.ReadLine();
                    s.AppendLine(line);
                }
                values = s.ToString().Split(new string[] { ";" }, StringSplitOptions.None);

                byte[] frame = new byte[values.Length * 4];

                for (int i = 0; i < values.Length - 1; i++)
                {
                    frame[4 * i] = (byte)Convert.ToInt16(values[i]);
                    frame[4 * i + 1] = (byte)Convert.ToInt16(values[i]);
                    frame[4 * i + 2] = (byte)Convert.ToInt16(values[i]);
                    frame[4 * i + 3] = 0;

                }

                return frame;
            }
        }

        /// <summary>
        /// Writes a frame in a file.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="destination"></param>
        private void writeFrame(byte[] input, String destination)
        {
            int x = 0, y = 0;
            String[,] arr = new String[this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight];

            for (int i = 0; i < input.Length; i += 4)
            {
                //input[i] looks like this: r g b 0 whereas r=g=b because it's gray.
                arr[x, y] = input[i].ToString().PadLeft(3, '0');
                x++;
                if (x == this.sensor.DepthStream.FrameWidth)
                {
                    x = 0;
                    y++;
                }
            }
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(@destination))
            {
                StringBuilder templine = new StringBuilder();
                for (int j = 0; j < arr.GetLength(1); j++)
                {
                    for (int i = 0; i < arr.GetLength(0); i++)
                    {
                        templine.Append(arr[i, j] + ";");
                    }
                    file.WriteLine(templine);
                    templine.Clear();
                }
            }
        }

        /*
        private void TrackClosestSkeleton()
        {
            if (this.sensor != null && this.sensor.SkeletonStream != null)
            {
                if (!this.sensor.SkeletonStream.AppChoosesSkeletons)
                {
                    this.sensor.SkeletonStream.AppChoosesSkeletons = true; // Ensure AppChoosesSkeletons is set
                }

                float closestDistance = 10000f; // Start with a far enough distance
                int closestID = 0;

                foreach (Skeleton skeleton in this.skeletonData.Where(s => s.TrackingState != SkeletonTrackingState.NotTracked))
                {
                    if (skeleton.Position.Z < closestDistance)
                    {
                        closestID = skeleton.TrackingId;
                        closestDistance = skeleton.Position.Z;
                    }
                }

                if (closestID > 0)
                {
                    this.sensor.SkeletonStream.ChooseSkeletons(closestID); // Track this skeleton
                }
            }
        }
        */

        /*
        private void FindPlayerInDepthPixel(short[] depthFrame)
        {
            foreach (short depthPixel in depthFrame)
            {
                int player = depthPixel & DepthImageFrame.PlayerIndexBitmask;

                if (player > 0 && this.skeletonData != null)
                {
                    Skeleton skeletonAtPixel = this.skeletonData[player - 1];   // Found the player at this pixel
                    // ...
                }
            }
        }*/
        
        void SkeletonFrameReady(object sender, AllFramesReadyEventArgs e)
        {
            
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())//Open the Skeleton frame
            {
                
                if (skeletonFrame != null)
                {
                    this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];

                    if (this.skeletonData != null)// check that a frame is available
                    {
                        skeletonFrame.CopySkeletonDataTo(this.skeletonData);// get the skeletal information in this frame

                        foreach (Skeleton s in this.skeletonData)
                        {
                            if (s.TrackingState == SkeletonTrackingState.Tracked)
                            {
                                foreach (Joint j in s.Joints)
                                {
                                    if (j.JointType == JointType.HandRight)
                                    {
                                        HandRightX = (int)j.Position.X;
                                        HandRightY = (int)j.Position.Y;
                                        lbRightHand.Content = "Right Hand Coordinates: ("
                                            + HandRightX + ", " + HandRightY + ")";
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void OnAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            try
            {
                frameCounter = (frameCounter + 1) % (MAXFPS / FPS);
                if (frameCounter != 0)
                    return;

                IList<Task> tasks = new List<Task>();

                bool colorImageDataUpdated = UpdateColorImagePixelData(e);
                bool depthImageRawDataUpdated = UpdateDepthImagePixelRawData(e);

                if (depthImageRawDataUpdated)
                {
                    Task calcDepthTask = Task.Factory.StartNew(() => CalculateDepthImageData());
                    tasks.Add(calcDepthTask);
                }

                await Task.WhenAll(tasks);

                UpdateScreens(colorImageDataUpdated, depthImageRawDataUpdated);
            }
            catch (Exception ex)
            {
                //TODO: log or user prompt or whatever
            }
        }

        private void UpdateScreens(bool colorImageDataUpdated, bool depthImageRawDataUpdated)
        {
            if (colorImageDataUpdated)
            {
                // Write the pixel data into our bitmap
                this.colorScreen.WritePixels(
                    new Int32Rect(0, 0, this.colorScreen.PixelWidth, this.colorScreen.PixelHeight),
                    this.colorPx,
                    this.colorScreen.PixelWidth * sizeof(int),
                    0);
            }

            if (depthImageRawDataUpdated)
            {
                this.depthScreen.WritePixels(
                            new Int32Rect(0, 0, this.depthScreen.PixelWidth, this.depthScreen.PixelHeight),
                            this.depthPx,
                            this.depthScreen.PixelWidth * sizeof(int),
                            0);

                this.mainScreen.WritePixels(
                            new Int32Rect(0, 0, this.mainScreen.PixelWidth, this.mainScreen.PixelHeight),
                            this.resultPx,
                            this.mainScreen.PixelWidth * sizeof(int),
                            0);
            }
        }

        private bool UpdateColorImagePixelData(AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null)
                    return false;

                // Copy the pixel data from the image to a temporary array
                colorFrame.CopyPixelDataTo(this.colorPx);
            }

            return true;
        }

        private bool UpdateDepthImagePixelRawData(AllFramesReadyEventArgs e)
        {
            using (var depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame == null)
                    return false;

                depthFrame.CopyDepthImagePixelDataTo(this.rawDepthData);

                //get min and max reliable depth for the current frame
                this.minDepth = depthFrame.MinDepth; //80 cm
                this.maxDepth = depthFrame.MaxDepth / 2; //2 m
            }

            return true;
        }

        private void CalculateDepthImageData()
        {
            int nearestPixel = 0;

            int col = 0; //index for color pixels
            for (int i = 0; i < this.rawDepthData.Length; ++i)
            {
                //get depth for each pixel
                short depth = this.rawDepthData[i].Depth;

                byte intensity = (depth < minDepth || depth > maxDepth) ? (byte)0 :
                    (byte)(255 - (byte)(((float)(depth - minDepth) / (maxDepth - minDepth)) * 255.0f));

                this.depthPx[col++] = intensity;
                this.depthPx[col++] = intensity;
                this.depthPx[col++] = intensity;
                ++col;

                if (nearestPixel < intensity)
                    nearestPixel = intensity;

            }

            for (int i = 0; i < depthPx.Length; i++)
            {
                //resultPx[i] = ((depthPx[i] > (byte)150) ? (byte)255 : (byte)0);
                resultPx[i] = 0;
                if (depthPx[i] >= nearestPixel - 20)
                    resultPx[i] = depthPx[i];
            }
        }

        //stop sensor on closing window
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
                Environment.Exit(0);
            }
        }
    }
}
