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
using System.Drawing;

namespace IKSL
{
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
        private short[] depthData;

        //mapped depth to color fame
        private ColorImagePoint[] mappedDepthCoordinates;

        //byte arrays that hold the generated frames
        private byte[] colorData;
        private byte[] depthPx;
        private byte[] resultPx;
        private byte[] colorDepthPx;
        private byte[] bitMapBits;
        
        private int frameCounter = -1;
        private int minDepth;
        private int maxDepth;
        
        int FPS = -1;
        const int MAXFPS = 30;

        public MainWindow()
        {
            InitializeComponent();
            txtPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\kinect.txt";
            FPS = (int)sliderFPS.Value;
            lbFPS.Content = "FPS (" + FPS + "/" + MAXFPS + ")";
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

                this.depthPx = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                this.colorData = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.colorDepthPx = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.resultPx = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                
                bitMapBits = new byte[640 * 480 * 4];
                this.rawDepthData = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                this.depthScreen = new WriteableBitmap(640, 480, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.colorScreen = new WriteableBitmap(640, 480, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.mainScreen = new WriteableBitmap(640, 480, 96.0, 96.0, PixelFormats.Bgr32, null);
                
                this.sensor.AllFramesReady += OnAllFramesReady;

                this.Screen1.Source = this.mainScreen;
                this.Screen2.Source = this.colorScreen;
                this.Screen3.Source = this.depthScreen;

                this.btnSave.Click += this.button_save;
                this.btnLoad.Click += this.button_load;

                this.sliderFPS.ValueChanged += this.change_FPS;

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

        private void change_FPS(object sender, RoutedEventArgs e)
        {
            this.FPS = (int)this.sliderFPS.Value;
            lbFPS.Content = "FPS (" + FPS + "/" + MAXFPS + ")";
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

        private async void OnAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            try
            {
                frameCounter = (frameCounter + 1) % (MAXFPS / FPS);
                if (frameCounter != 0)
                    return;

                IList<Task> tasks = new List<Task>();

                bool colorImageDataUpdated = UpdateColorImagePixelData(e);
                bool allDataUpdated = UpdateDepthImagePixelRawData(e);

                if (allDataUpdated)
                {
                    Task calcDepthTask = Task.Factory.StartNew(() => CalculateDepthImageData());
                    tasks.Add(calcDepthTask);
                }

                await Task.WhenAll(tasks);

                UpdateScreens(colorImageDataUpdated, allDataUpdated);

                
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex.Message);
            }
        }

        private void UpdateScreens(bool colorImageDataUpdated, bool allDataUpdated)
        {
            if (colorImageDataUpdated)
            {
                this.colorScreen.WritePixels(
                    new Int32Rect(0, 0, this.colorScreen.PixelWidth, this.colorScreen.PixelHeight),
                    this.colorData,
                    this.colorScreen.PixelWidth * sizeof(int),
                    0);
            }

            if (allDataUpdated)
            {
                this.depthScreen.WritePixels(
                            new Int32Rect(0, 0, this.depthScreen.PixelWidth, this.depthScreen.PixelHeight),
                            this.depthPx,
                            this.depthScreen.PixelWidth * sizeof(int),
                            0);

                this.mainScreen.WritePixels(
                            new Int32Rect(0, 0, this.mainScreen.PixelWidth, this.mainScreen.PixelHeight),
                            this.bitMapBits,
                            this.mainScreen.PixelWidth * sizeof(int),
                            0);


                mainScreen.Lock();
                var b = new System.Drawing.Bitmap(mainScreen.PixelWidth, mainScreen.PixelHeight, mainScreen.BackBufferStride, System.Drawing.Imaging.PixelFormat.Format32bppArgb,
                    mainScreen.BackBuffer);

                using (var bitmapGraphics = System.Drawing.Graphics.FromImage(b))
                {
                    //DRAW STUFF ONTO THE SCREEN HERE
                    //bitmapGraphics.DrawLine(new System.Drawing.Pen(System.Drawing.Brushes.Red, 5), 0, 0, 640, 480);                    
                }
                mainScreen.AddDirtyRect(new Int32Rect(0, 0, mainScreen.PixelWidth, mainScreen.PixelHeight));
                mainScreen.Unlock();

            }
        }

        private bool UpdateColorImagePixelData(AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null)
                    return false;

                // Copy the pixel data from the image to a temporary array
                colorFrame.CopyPixelDataTo(this.colorData);

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

                depthData = new short[depthFrame.PixelDataLength];
                depthFrame.CopyPixelDataTo(this.depthData);

                this.mappedDepthCoordinates = new ColorImagePoint[depthFrame.PixelDataLength];

                //get min and max reliable depth for the current frame
                this.minDepth = depthFrame.MinDepth; //80 cm
                this.maxDepth = 2500; //depthFrame.MaxDepth / 2; //2 m

            }

            return true;
        }

        private void CalculateDepthImageData()
        {
            int[] depthHistogram = new int[256];
            Array.Clear(depthHistogram, 0, depthHistogram.Length);

            float[] cumulativeHistogram = new float[256];
            Array.Clear(cumulativeHistogram, 0, cumulativeHistogram.Length);

            int col = 0; //index for color pixels
            for (int i = 0; i < this.rawDepthData.Length; ++i)
            {
                //get depth for each pixel
                short depth = this.rawDepthData[i].Depth;

                byte intensity = (depth < minDepth || depth > maxDepth) ? (byte)0 :
                                    (byte)((((float)(depth - minDepth) / (maxDepth - minDepth)) * 255.0f));

                this.depthPx[col++] = intensity;
                this.depthPx[col++] = intensity;
                this.depthPx[col++] = intensity;
                ++col;

                depthHistogram[intensity] += 1; 
                
            }

            //iterate through all data of the histogram and sum it up in the accumulative histogram
            int temp = 0;
            for (int i = 0; i < 256; i++)
            {
                temp = temp + depthHistogram[i];
                cumulativeHistogram[i] = (int)(100 * (float)(temp) / (float)(rawDepthData.Length)); //cumulativeHistogram[i-1] + depthHistogram[i];
            }

            //calculate rise and cut image in regions
            float rise = 0;
            byte l = 1, h = 0, r = 0, maxRegion = 1;
            byte[] region = new byte[256];
            for (int i = 1; i < 256; i++)
            {
                if (cumulativeHistogram[i] == cumulativeHistogram[i-1])
                {
                    l += 1;
                }
                else 
                {
                    h = (byte)(cumulativeHistogram[i] - cumulativeHistogram[i - 1]);
                    rise = h / l;
                    if (rise > 0.9)
                        r += 1;
                    l = 1;
                }
                region[i] = r;
                maxRegion = r;
            }

            int c = 256 / (maxRegion - 1);

            byte[] resultPx = new byte[depthPx.Length];
            byte[] regionToPixel = new byte[depthPx.Length];
            for (int i = 0; i < depthPx.Length; i++)
            {
                regionToPixel[i] = region[depthPx[i]];
                this.resultPx[i] = (byte)(255 - Math.Min(c * regionToPixel[i],255));

            }

            //map color to depth data
            this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(DepthImageFormat.Resolution640x480Fps30, rawDepthData, ColorImageFormat.RgbResolution640x480Fps30, mappedDepthCoordinates);

            // Put the color image into the bitmap bits
            for (int i = 0; i < colorData.Length; i += 4)
            {
                bitMapBits[i + 3] = 255; //alpha
                bitMapBits[i + 2] = colorData[i + 2];
                bitMapBits[i + 1] = colorData[i + 1];
                bitMapBits[i] = colorData[i];
            }

            //combine depth data onto the color data
            for (int i = 0; i < depthData.Length; i++)
            {
                int depthVal = depthData[i] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                // Put in the overlay of, say, depth values < 1 meters.       
                if ((depthVal < maxDepth/2) && (depthVal > minDepth))
                {
                    ColorImagePoint point = mappedDepthCoordinates[i];

                    if ((point.X >= 0 && point.X < 640) && (point.Y >= 0 && point.Y < 480))
                    {
                        int baseIndex = (point.Y * 640 + point.X) * 4;

                        bitMapBits[baseIndex] = (byte)(0);
                        bitMapBits[baseIndex + 1] = (byte)(255);
                        bitMapBits[baseIndex + 2] = (byte)(255);                        

                    }
                }
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
