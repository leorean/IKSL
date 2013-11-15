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

        //store depth data received from the camera
        private DepthImagePixel[] rawDepthData;
       
        //this holds a generated RGBA-frame made of depth data
        private byte[] colorPx;
        private byte[] depthPx;
        private byte[] result;

        public MainWindow()
        {
            InitializeComponent();

            txtPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\";
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
                
                this.rawDepthData = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                
                this.depthPx = new byte[this.sensor.DepthStream.FramePixelDataLength * sizeof(int)];
                this.colorPx = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.depthScreen = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.Screen3.Source = this.depthScreen;

                this.colorScreen = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.Screen2.Source = this.colorScreen;

                this.mainScreen = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.Screen1.Source = this.mainScreen;


                //event that fires every time a frame is calculated
                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;
                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                this.btnSave.Click += this.button_save;
                
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
                writeFrame(result, txtPath.Text + "kinect.txt");
            }
            MessageBox.Show("done.");

        }

        private void writeFrame(byte[] input, String destination)
        {
            int x = 0, y = 0;
            byte[,] arr = new byte[this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight];
            for (int i = 0; i < input.Length; i+=4)
            {
                arr[x, y] = input[i]; // input[i] looks like this: r g b 0 whereas r=g=b because it's gray.
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
                        templine.Append(arr[i,j] + ";");
                    }
                    file.WriteLine(templine);
                    templine.Clear();
                }
                //file.WriteLine("TEST");
            }
        }

        //depth event handler that is called each tick
        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    //copy pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.rawDepthData);

                    //get min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth; //80 cm
                    int maxDepth = depthFrame.MaxDepth; //4 m

                    for (int depthIndex = 0, colorIndex = 0;
                        depthIndex < rawDepthData.Length && colorIndex < depthPx.Length;
                        depthIndex++, colorIndex += 4)
                    {
                        //get depth for each pixel
                        short depth = (short)(rawDepthData[depthIndex].Depth);

                        byte intensity = 0;
                        if (depth >= minDepth && depth <= maxDepth) 
                            intensity = (byte)(Math.Floor(1 - ((double)(depth - minDepth) / (maxDepth - minDepth)) * 255));

                        depthPx[colorIndex + BLUE] = intensity;
                        depthPx[colorIndex + GREEN] = intensity;
                        depthPx[colorIndex + RED] = intensity;
                        
                    }

                    this.depthScreen.WritePixels(
                        new Int32Rect(0, 0, this.depthScreen.PixelWidth, this.depthScreen.PixelHeight),
                        this.depthPx,
                        this.depthScreen.PixelWidth * sizeof(int),
                        0);


                    //now pixels[] holds one depth information frame.
                    this.result = calculateResultFrame(this.depthPx);

                    //write the pixeldata into bitmap
                    this.mainScreen.WritePixels(
                        new Int32Rect(0, 0, this.mainScreen.PixelWidth, this.mainScreen.PixelHeight),
                        this.result,
                        this.mainScreen.PixelWidth * sizeof(int),
                        0);
                    
                }
            }
        }

        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPx);

                    // Write the pixel data into our bitmap
                    this.colorScreen.WritePixels(
                        new Int32Rect(0, 0, this.colorScreen.PixelWidth, this.colorScreen.PixelHeight),
                        this.colorPx,
                        this.colorScreen.PixelWidth * sizeof(int),
                        0);
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

        //todo: implement
        private byte[] calculateResultFrame(byte[] bitMap)
        {

            return bitMap;
        }
    }
}
