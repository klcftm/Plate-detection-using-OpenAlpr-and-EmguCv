using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using openalprnet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;
using System.Data.SqlClient;

namespace DenemeProje
{
    public partial class Form1 : Form
    {
        private VideoCapture capture;
        private string MediaFile;
        static String config_file = Path.Combine(AssemblyDirectory, "openalpr.conf");
        static String runtime_data_dir = Path.Combine(AssemblyDirectory, "runtime_data");
        AlprNet alpr = new AlprNet("eu", config_file, runtime_data_dir);
        string plaka;
      
        Image<Bgr, byte> ınputImg;
        public Form1()
        {
            InitializeComponent();
            
        }

        public static string AssemblyDirectory
        {
            get
            {
                var codeBase = Assembly.GetExecutingAssembly().CodeBase;
                var uri = new UriBuilder(codeBase);
                var path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public Rectangle boundingRectangle(List<Point> points)
        {
            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxX = points.Max(p => p.X);
            var maxY = points.Max(p => p.Y);

            return new Rectangle(new Point(minX, minY), new Size(maxX - minX, maxY - minY));
        }

         private static Image<Bgr,Byte> cropImage(Image img, Rectangle cropArea)
        
        {
            var bmpImage = new Bitmap(img);
             Image<Bgr,Byte> im = new Image<Bgr, Byte>(bmpImage.Clone(cropArea, bmpImage.PixelFormat));
             return im;
            
        }

        public static Bitmap combineImages(List<Image> images)
        {
            Bitmap finalImage = null;

            try
            {
                var width = 0;
                var height = 0;

                foreach (var bmp in images)
                {
                    width += bmp.Width;
                    height = bmp.Height > height ? bmp.Height : height;
                }
                finalImage = new Bitmap(width, height);
                using (var g = Graphics.FromImage(finalImage))
                {
                    g.Clear(Color.Black);
                    var offset = 0;
                    foreach (Bitmap image in images)
                    {
                        g.DrawImage(image,
                                    new Rectangle(offset, 0, image.Width, image.Height));
                        offset += image.Width;
                    }
                }

                return finalImage;
            }
            catch (Exception ex)
            {
                if (finalImage != null)
                    finalImage.Dispose();

                throw ex;
            }
            finally
            {
                foreach (var image in images)
                {
                    image.Dispose();
                }
            }
        }

        private void processImageFile(Emgu.CV.Image<Bgr,Byte> framepic) 
        {

            using (var alpr = new AlprNet("eu", config_file, runtime_data_dir)) { 
            
                if (!alpr.IsLoaded())
                {
                    txtPlaka.Text = "Yüklenirken hata oldu.Tekrar deneyin";
                    return;
                }
            
                Image<Bgr, byte> resultx = null;
                resultx = framepic.Copy(); 
                var results = alpr.Recognize(framepic.Bitmap);
                bool bulundu = false;

                var images = new List<Image>(results.Plates.Count());
                
                foreach (var result in results.Plates)
                {
                    
                    var rect = boundingRectangle(result.PlatePoints);
                    Image<Bgr, Byte> img = framepic; 
                    Image<Bgr, Byte> cropped = cropImage(img.Bitmap, rect); 
                    images.Add(cropped.Bitmap);
                
                //Plakanın etrafına kırmızı dikdörtgen çiziyor
               
                Point p = new Point(result.PlatePoints[0].X-4, result.PlatePoints[0].Y-25);
                p.Offset(0, cropped.Size.Height);
                resultx.Draw(new Rectangle(p, cropped.Size), new Bgr(12, 12, 214), 3);
                resultx.ROI = new Rectangle(p, cropped.Size);
               // picOriginResim.Image = resultx;  //if içinden aldım
                try
                {
                    cropped.CopyTo(resultx);
                }
                catch (Exception e)
                {
                    continue;
                }
                              
                resultx.ROI = Rectangle.Empty;
                
                String t= GetMatchedPlate(result.TopNPlates);
                picOriginResim.Image = resultx;
                Regex regex = new Regex(@"^(0[0-9]|[1-7][0-9]|8[01])(([A-Z])(\d{4,5})|([A-Z]{2})(\d{3,4})|([A-Z]{3})(\d{2,3}))$");
                
                Match match = regex.Match(t.Replace(" ", ""));
                if (match.Success)
                {

                    txtPlaka.Text = t;
                    picOriginResim.Image = resultx;
                    bulundu = true;
                }
                
              
            }

                if (images.Any())
                {
                    picPlakaResmi.Image = combineImages(images);
                    picOriginResim.Image = resultx;
            }
            
               if (!bulundu){ picOriginResim.Image = framepic;
                }

            }
        }
        
        private string GetMatchedPlate(List<AlprPlateNet> plakalar)
        {
            foreach (var item in plakalar)
            {
                return item.Characters.PadRight(12);
            }
            return "";
        }

        private void resetControls()
        {
            picOriginResim.Image = null;
            picPlakaResmi.Image = null;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            resetControls();
            if (!alpr.IsLoaded())
            {
                txtPlaka.Text = "Yüklenirken hata oldu.Tekrar deneyin";
                return;
            }
        }


        /// <summary>
        /// Video loop, parses 1 frame every tick.
        /// </summary>
        /// <param name="sender"> event sender </param>
        /// <param name="e"> args </param>
        /// 
        private void LoopVideo(object sender, EventArgs e)
        {
            // Get frame
            Mat buffer = capture.QueryFrame();

            // Handle 'null' frame at EOF
            if (buffer == null || buffer.IsEmpty)
            {
                timer.Enabled = false;
               
                return;
            }
            
            Image<Bgr, byte> src = new Image<Bgr, byte>(buffer.Bitmap); //gray yaptım 
            
            processImageFile(src);
            
        }
        private  void btnPlakayiBul_Click_1(object sender, EventArgs e)
        {
            
            try
            {
                // openfile diyalog oluştur
                OpenFileDialog fileDia = new OpenFileDialog();
                fileDia.InitialDirectory = Environment.CurrentDirectory;
                fileDia.Filter = "Video files (*.mp4)|*.mp4|All files (*.*)|*.*";
                if (fileDia.ShowDialog() == DialogResult.OK)
                {
                    capture = new VideoCapture(fileDia.FileName);
                    MediaFile = fileDia.FileName;
                    
                    if (capture.QueryFrame() != null && capture.GetCaptureProperty(CapProp.Fps) > 0.0)
                    {
                        double fps = capture.GetCaptureProperty(CapProp.Fps);

                        // Reset capture & init. timer
                        capture = new VideoCapture(MediaFile);
                        
                        timer.Enabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "File error");
            }

  

        }

        private void btnPlaka_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                ınputImg = new Image<Bgr, byte>(ofd.FileName);
                picOriginResim.Image = ınputImg;
            }
            processImageFile(ınputImg);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
           if (txtPlaka.Text != null)
            {

            plaka = txtPlaka.Text;
            Form2 form2 = new Form2();
            SqlConnection baglanti = new SqlConnection("Data Source=LAPTOP-F1OAJ4IM\\SQLEXPRESS; Initial Catalog=plaka; integrated security=true;");
            baglanti.Open();

                           

                SqlCommand sorgu = new SqlCommand("Select * from arac,person where kayıtlı_tc=tc and plaka=@1", baglanti);
                sorgu.Parameters.AddWithValue("@1", plaka);
                SqlDataReader rd = sorgu.ExecuteReader();
                if (rd.Read())
                {
                    form2.plk.Text = rd["plaka"].ToString();
                    form2.ad.Text = rd["ad"].ToString();
                    form2.soyad.Text = rd["soyad"].ToString();
                    form2.marka.Text = rd["marka"].ToString();
                    form2.model.Text = rd["model"].ToString();
                    form2.yıl.Text = rd["yıl"].ToString();

                }

                form2.Show();
            }
            else {
                MessageBox.Show("Plaka tespit edilemedi.");
            }
        }
    }
}
