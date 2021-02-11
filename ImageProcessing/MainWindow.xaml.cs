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
using ImageProcessingLib;

using System.Drawing;
using Microsoft.Win32;
using System.IO;
using System.Drawing.Imaging;
using System.Diagnostics;


namespace ImageProcessing
{
    public partial class MainWindow : Window
    {
        ImageProcessor imageProcessing;
        private bool show = true;
        public MainWindow()
        {
            InitializeComponent();
            // Отключение сглаживания изображений
            image1.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
            image2.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
            image3.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
            imageMask.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            imageMask.Opacity = 0.5;
            imageAdjacency.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.NearestNeighbor);
            
            
            MenuItem_Click(this.Open, null);
        }

        public BitmapImage BitmapToBitmapImage(Bitmap img)
        {
            BitmapImage res = new BitmapImage();
            using (MemoryStream memStream = new MemoryStream())
            {
                img.Save(memStream, ImageFormat.Png);
                memStream.Position = 0;
                res.BeginInit();
                res.StreamSource = memStream;
                res.CacheOption = BitmapCacheOption.OnLoad;
                res.EndInit();
            }


            return res;
        }
        public bool SelectFile()
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "Image Files(*.jpg; *.jpeg; *.gif; *.bmp, *.png)|*.jpg; *.jpeg; *.gif; *.bmp; *.png";
            if (open.ShowDialog() == true)
            {
                imageProcessing = new ImageProcessor(open.FileName, Convert.ToInt32(textBox1.Text));

                // Registering delegates
                imageProcessing.RegisterRegularPartChanged(() => SetMask(imageProcessing.GetBitmapMask()));
                imageProcessing.RegisterShadesChanged(()=>
                {
                    if (textBox1 != null)
                    {
                        textBox1.Text = $"{imageProcessing.shades}";
                        image3.Source = BitmapToBitmapImage(imageProcessing.GetBitmapSimplified());
                        imageAdjacency.Source = BitmapToBitmapImage(imageProcessing.GetBitmapAdjacency());
                        SetMask(imageProcessing.GetBitmapMask());
                    }
                });
                return true;
            }
            return false;
        }

        private void SetMask(Bitmap mask)
        {
            if (show)
                imageMask.Source = BitmapToBitmapImage(mask);
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            switch(menuItem.Name)
            {
                case "Open":
                    if (SelectFile())
                    {
                        image1.Source = BitmapToBitmapImage(imageProcessing.GetBitmapOriginal());
                        image2.Source = BitmapToBitmapImage(imageProcessing.GetBitmapNormalised());
                        image3.Source = BitmapToBitmapImage(imageProcessing.GetBitmapSimplified());
                        SetMask(imageProcessing.GetBitmapMask());
                        imageAdjacency.Source = BitmapToBitmapImage(imageProcessing.GetBitmapAdjacency());
                    }
                    break;
            }
        }

        private void Shades_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = (Slider)sender;
            imageProcessing.shades = (int)slider.Value + 2;
        }
        private void RegularPart_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = (Slider)sender;
            if(show)
                imageProcessing.RegularSize = slider.Value;
            
        }

        private void ShowHide_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            show = !show;
            if (show)
            {
                button.Content = "Hide highlighting";
                imageProcessing.RegularSize = sliderRegularSize.Value;
                imageMask.Source = BitmapToBitmapImage(imageProcessing.GetBitmapMask());
                imageMask.Opacity = 0.5;
            }
            else
            {
                button.Content = "Show highlighting";
                imageMask.Opacity = 0;
            }
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                try
                {
                    sliderShades.Value = Convert.ToInt32(textBox1.Text) - 2;
                }
                catch
                {
                    textBox1.Text = (sliderShades.Value + 2).ToString();
                }
            }
        }

        private void cbIrregular_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            imageProcessing.isIrregular = (bool)cb.IsChecked;
            SetMask(imageProcessing.GetBitmapMask());
        }

        private void buttonDicreaseShades_Click(object sender, RoutedEventArgs e)
        {
            sliderShades.Value--;
        }

        private void buttonIncreaseShades_Click(object sender, RoutedEventArgs e)
        {
            sliderShades.Value++;
        }

        private void buttonDicreasePart_Click(object sender, RoutedEventArgs e)
        {
            sliderRegularSize.Value -= 0.1;
        }

        private void buttonIncreasePart_Click(object sender, RoutedEventArgs e)
        {
            sliderRegularSize.Value += 0.1;
        }
    }
}
