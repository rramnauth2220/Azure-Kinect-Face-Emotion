using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System.IO;
using System.ComponentModel;

namespace Azure_Kinect_AI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window //, INotifyPropertyChanged
    {
        private readonly Device device = null;
        private readonly WriteableBitmap bitmap = null;
        private readonly int colorWidth = 0;
        private readonly int colorHeight = 0;

        private string statusText = null;

        private const string faceClientKey = "/* INCLUDE HERE YOUR SERVICE  KEY */";
        private FaceClient faceclient = null;
        private DetectedFace detectedFace = null;
        private static readonly List<FaceAttributeType> atributes = new List<FaceAttributeType>(){FaceAttributeType.Emotion};

        public event PropertyChangedEventHandler PropertyChanged;

        const string ENDPOINT_BASE_URL = "https://Azure Kinect AI.cognitiveservices.azure.com/";

        public MainWindow()
        {
            InitializeComponent();
            device = Device.Open();
            device.StartCameras(new DeviceConfiguration
            {
                ColorFormat = ImageFormat.ColorBGRA32,
                ColorResolution = ColorResolution.R720p,
                DepthMode = DepthMode.NFOV_2x2Binned,
                SynchronizedImagesOnly = true
            });

            colorWidth = device.GetCalibration().ColorCameraCalibration.ResolutionWidth;
            colorHeight = device.GetCalibration().ColorCameraCalibration.ResolutionHeight;

            bitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgra32, null);

            this.DataContext = this;

            faceclient = new FaceClient(new Microsoft.Azure.CognitiveServices.Vision.Face.ApiKeyServiceClientCredentials(faceClientKey))
            {
                Endpoint = ENDPOINT_BASE_URL
            };
        }

        private Stream StreamFromBitmapSource(BitmapSource bitmap)
        {
            Stream jpeg = new MemoryStream();

            BitmapEncoder enc = new JpegBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bitmap));
            enc.Save(jpeg);
            jpeg.Position = 0;

            return jpeg;
        }

        public static bool IsWindowOpen<T>(string name = "") where T : Window
        {
            return string.IsNullOrEmpty(name)
               ? Application.Current.Windows.OfType<T>().Any()
               : Application.Current.Windows.OfType<T>().Any(w => w.Name.Equals(name));
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            MessageBox.Show("Closing");
            if (device != null)
            {
                device.Dispose();
            }
        }

        private async void Window_LoadedAsync(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Loaded");
            int count = 0;
            while (IsWindowOpen<Window>("MainWindow"))
            {
                {
                    using (Capture capture = await Task.Run(() => { return device.GetCapture(); }))
                    {

                        count++;

                        this.bitmap.Lock();
                        var color = capture.Color;
                        var region = new Int32Rect(0, 0, color.WidthPixels, color.HeightPixels);
                        unsafe
                        {

                            using (var pin = color.Memory.Pin())
                            {
                                bitmap.WritePixels(region, (IntPtr)pin.Pointer, (int)color.Size, color.StrideBytes);
                            }
                            if (detectedFace != null)
                            {
                                this.StatusText = getEmotion(detectedFace.FaceAttributes.Emotion).ToString(); //We display the result of our method GetEmotion that we coded before.

                            }

                            bitmap.AddDirtyRect(region);
                            bitmap.Unlock();

                            if (count % 30 == 0)
                            {
                                var stream = StreamFromBitmapSource(this.bitmap);
                                _ = faceclient.Face.DetectWithStreamAsync(stream, true, false, MainWindow.atributes).ContinueWith(responseTask =>   //In here we are getting our Detected Images.

                                {
                                    try
                                    {
                                        foreach (var face in responseTask.Result)
                                        {
                                            detectedFace = face;

                                        }
                                    }
                                    catch (System.Exception ex)
                                    {
                                        this.StatusText = ex.ToString();
                                    }
                                }, TaskScheduler.FromCurrentSynchronizationContext());
                            }
                        }
                    }

                }
            }
        }

        public ImageSource ImageSource
        {
            get
            {
                return this.bitmap;
            }
        }

        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("StatusText"));
                }
            }
        }
        static (string Emotion, double Value) getEmotion(Emotion emotion)
        {
            var emotionProperties = emotion.GetType().GetProperties();
            (string Emotion, double Value) highestEmotion = ("Anger", emotion.Anger);
            foreach (var e in emotionProperties)
            {
                if (((double)e.GetValue(emotion, null)) > highestEmotion.Value)
                {
                    highestEmotion.Emotion = e.Name;
                    highestEmotion.Value = (double)e.GetValue(emotion, null);
                }
            }
            return highestEmotion;
        }
    }
}
