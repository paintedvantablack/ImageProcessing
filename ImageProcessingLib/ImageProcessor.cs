using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing.Imaging;

namespace ImageProcessingLib
{
    public class ImageProcessor
    {
        private int _shades; // Количество оттенков серого на третьем изображении
        public int shades 
        { 
            get
            {
                return _shades;
            }
            set
            {
                _shades = value;
                if (_arrayNormalised != null)
                {
                    shadesChanged();
                }
            }
        }
        private double _regularSize;
        public double RegularSize 
        { 
            get 
            {
                return _regularSize;
            } 
            set 
            {
                _regularSize = value;
                regularPartChanged();
            } 
        }

        private short[,] _arrayOriginal;    // Массив оттенков изначального изображения
        private short[,] _arrayNormalised;  // ... нормализованного изображения
        private short[,] _arraySimplified;  // ... упрощенного изображения

        private int[,] _adjacencyMatrix;    // Матрица смежности
        private List<Pair> orderedPairs;    // Упорядоченный по распространенности список пар пикселей
        private bool[,] _regularPartMask;   // Маска для отображение регулярной части изображения

        public bool isIrregular { get; set; }        // Отображать маской нерегулярную часть?

        // Делегаты
        public delegate void ChangedSomething();
        private event ChangedSomething imageLoaded;
        public void RegisterImageLoaded(params ChangedSomething[] dels)
        {
            foreach(var del in dels)
            {
                imageLoaded += del;
            }
        }

        private event ChangedSomething shadesChanged;
        public void RegisterShadesChanged(params ChangedSomething[] dels)
        {
            foreach (var del in dels)
            {
                shadesChanged += del;
            }
        }

        private event ChangedSomething regularPartChanged;
        public void RegisterRegularPartChanged(params ChangedSomething[] dels)
        {
            foreach(var del in dels)
            {
                regularPartChanged += del;
            }
        }


        public ImageProcessor(string path, int shades)
        {
            RegisterRegularPartChanged(BuildRegularPartMask);
            RegisterShadesChanged(BuildArraySimplified, BuildAdjacencyMatrix, BuildOrderedPairs, regularPartChanged);
            RegisterImageLoaded(BuildArrayNormalised, shadesChanged);

            Bitmap img = new Bitmap(path);
            this.shades = shades;
            BuildArrayBegin(img);
            imageLoaded();
        }

        private void BuildArrayBegin(Bitmap img)
        {
            _arrayOriginal = new short[img.Height, img.Width];

            unsafe
            {
                BitmapData bitmapData = img.LockBits(
                    new Rectangle(0, 0, img.Width, img.Height),
                    ImageLockMode.ReadOnly,
                    img.PixelFormat);
                int bytesPerPixel = Bitmap.GetPixelFormatSize(img.PixelFormat) / 8;
                int heightInPixels = bitmapData.Height;
                int widthInBytes = bitmapData.Width * bytesPerPixel;

                byte* PtrFirstPixel = (byte*)bitmapData.Scan0;
                Parallel.For(0, heightInPixels, i =>
                {
                    byte* currentLine = PtrFirstPixel + (i * bitmapData.Stride);
                    for(int j = 0; j < widthInBytes; j += bytesPerPixel)
                        _arrayOriginal[i, j / bytesPerPixel] = (short)((currentLine[j] + currentLine[j + 1] + currentLine[j + 2]) / 3 + 1);
                });
            }
        }

        // Построение нормализованного массива
        private void BuildArrayNormalised()
        {
            _arrayNormalised = (short[,])_arrayOriginal.Clone();
            short max = 0;
            short min = 256;
            foreach(var element in _arrayNormalised)
            {
                if (element > max)
                    max = element;
                if (element < min)
                    min = element;
            }

            int processors = Environment.ProcessorCount;

            Parallel.For(0, processors, p =>
            {
                for (int i = _arrayNormalised.GetLength(0) * p / processors; i < _arrayNormalised.GetLength(0) * (p + 1) / processors; ++i)
                {
                    for (int j = 0; j < _arrayNormalised.GetLength(1); ++j)
                    {
                        _arrayNormalised[i, j] = (short)((_arrayNormalised[i, j] - min) * 255 / (max - min));
                        if (_arrayNormalised[i, j] == 0)
                            ++_arrayNormalised[i, j];
                    }
                }
            });
        }

        // Построение упрощенного массива
        private void BuildArraySimplified()
        {
            int fragmentation = 256 / (shades - 1);
            _arraySimplified = (short[,])_arrayNormalised.Clone();

            
            int processors = Environment.ProcessorCount;

            Parallel.For(0, processors, p =>
            {
                int buf;
                for (int i = _arraySimplified.GetLength(0) * p / processors; i < _arraySimplified.GetLength(0) * (p+1) / processors ; ++i)
                {
                    for (int j = 0; j < _arraySimplified.GetLength(1); ++j)
                    {
                        buf = _arraySimplified[i, j] % fragmentation;
                        if (buf >= fragmentation / 2)
                            _arraySimplified[i, j] += (short)(fragmentation - buf);
                        else
                            _arraySimplified[i, j] -= (short)buf;

                        if (_arraySimplified[i, j] < 1)
                            _arraySimplified[i, j] = 1;
                        else if (_arraySimplified[i, j] > 256)
                            _arraySimplified[i, j] = 256;
                    }
                }
            });
        }

        // Построение матрицы смежности
        private void BuildAdjacencyMatrix()
        {
            _adjacencyMatrix = new int[shades, shades];
            if (shades > 8)
            {
                int processors = Environment.ProcessorCount;

                Parallel.For(0, processors, p =>
                {
                    for (int i = _arraySimplified.GetLength(0) * p / processors; i < _arraySimplified.GetLength(0) * (p + 1) / processors; ++i)
                    {
                        for (int j = 1; j < _arraySimplified.GetLength(1); ++j)
                        {
                            _adjacencyMatrix[
                                _arraySimplified[i, j] * (shades - 1) / 256,
                                _arraySimplified[i, j - 1] * (shades - 1) / 256
                                ]++;
                        }
                    }
                });
            }
            else
                for (int i = 0; i < _arraySimplified.GetLength(0); ++i)
                {
                    for (int j = 1; j < _arraySimplified.GetLength(1); ++j)
                    {
                        _adjacencyMatrix[
                            _arraySimplified[i, j] * (shades - 1) / 256,
                            _arraySimplified[i, j - 1] * (shades - 1) / 256
                            ]++;
                    }
                }
        }

        private void BuildOrderedPairs()
        {
            orderedPairs = new List<Pair>();
            for(int i = 0; i < _adjacencyMatrix.GetLength(0); ++i)
            {
                for (int j = 0; j < _adjacencyMatrix.GetLength(1); ++j)
                {
                    orderedPairs.Add(new Pair(_adjacencyMatrix[i, j], i, j));
                }
            }
            orderedPairs.Sort();
        }

        // Построение маски регулирной части
        private void BuildRegularPartMask()
        {
            _regularPartMask = new bool[_arraySimplified.GetLength(0), _arraySimplified.GetLength(1)];
            int part = (int)(orderedPairs.Count * RegularSize / 100);
            Parallel.For(0, _arraySimplified.GetLength(0), i =>
            {
                for (int j = 1; j < _arraySimplified.GetLength(1); ++j)
                {
                    for (int k = 0; k < part; ++k)
                    {
                        if (orderedPairs[orderedPairs.Count - 1 - k].I == _arraySimplified[i, j] * (shades - 1) / 256 &&
                            orderedPairs[orderedPairs.Count - 1 - k].J == _arraySimplified[i, j - 1] * (shades - 1) / 256)
                        {
                            _regularPartMask[i, j - 1] = true;
                            _regularPartMask[i, j] = true;
                        }
                    }
                }
            });
        }

        //Возврат битмапов
        public Bitmap GetBitmapOriginal()
        { return GetBitmap(0); }
        public Bitmap GetBitmapNormalised()
        { return GetBitmap(1); }
        public Bitmap GetBitmapSimplified()
        { return GetBitmap(2); }

        //private Bitmap GetBitmap2(byte stage)
        //{
        //    short[,] arrBufer = default;
        //    switch (stage)
        //    {
        //        case 0: // Изначальное изображение
        //            arrBufer = _arrayOriginal;
        //            break;
        //        case 1: // Нормализованное изображение
        //            arrBufer = _arrayNormalised;
        //            break;
        //        case 2: // Упрощенное изображение
        //            arrBufer = _arraySimplified;
        //            break;
        //    }
        //    Bitmap result = new Bitmap(arrBufer.GetLength(1), arrBufer.GetLength(0));
        //    for (int i = 0; i < arrBufer.GetLength(0); ++i)
        //        for (int j = 0; j < arrBufer.GetLength(1); ++j)
        //            result.SetPixel(j, i, Color.FromArgb(255, arrBufer[i, j] - 1, arrBufer[i, j] - 1, arrBufer[i, j] - 1));
        //    return result;
        //}

        private Bitmap GetBitmap(byte stage)
        {
            short[,] arrBufer = default;
            switch (stage)
            {
                case 0: // Изначальное изображение
                    arrBufer = _arrayOriginal;
                    break;
                case 1: // Нормализованное изображение
                    arrBufer = _arrayNormalised;
                    break;
                case 2: // Упрощенное изображение
                    arrBufer = _arraySimplified;
                    break;
            }

            unsafe
            {
                Bitmap result = new Bitmap(arrBufer.GetLength(1), arrBufer.GetLength(0));
                BitmapData bitmapData = result.LockBits(
                    new Rectangle(0, 0, result.Width, result.Height),
                    ImageLockMode.ReadWrite,
                    result.PixelFormat);
                int bytesPerPixel = Bitmap.GetPixelFormatSize(result.PixelFormat) / 8;
                int heightInPixels = bitmapData.Height;
                int widthInBytes = bitmapData.Width * bytesPerPixel;

                byte* PtrFirstPixel = (byte*)bitmapData.Scan0;
                Parallel.For(0, heightInPixels, i =>
                {
                    byte* currentLine = PtrFirstPixel + (i * bitmapData.Stride);
                    for (int j = 0; j < widthInBytes; j += bytesPerPixel)
                    {
                        byte color = (byte)(arrBufer[i, j / bytesPerPixel] - 1);
                        currentLine[j] = color;
                        currentLine[j + 1] = color;
                        currentLine[j + 2] = color;
                        currentLine[j + 3] = 255;
                    }
                });

                return result;
            }
        }

        public Bitmap GetBitmapAdjacency()
        {
            double max = 0;
            foreach (int element in _adjacencyMatrix)
                if (element > max)
                    max = element;

            Bitmap result = new Bitmap(shades, shades);

            // Раскрашивание битмапа матрицы смежности: МИНИМУМ> черный-синий-голубой-зелёный-жёлтый-красный-белый <МАКСИМУМ
            for (int i = 0; i < _adjacencyMatrix.GetLength(0); ++i)
            { 
                for (int j = 0; j < _adjacencyMatrix.GetLength(1); ++j)
                {
                    if (_adjacencyMatrix[i, j] <= max / 6)          //BLACK to BLUE
                        result.SetPixel(j, i, Color.FromArgb(255, 
                            0, 
                            0,
                            (int)(_adjacencyMatrix[i, j] * 255 / max * 6)));
                    else if (_adjacencyMatrix[i, j] <= max * 2 / 6) //BLUE to LIGHT BLUE
                        result.SetPixel(j, i, Color.FromArgb(255, 
                            0,
                            (int)((_adjacencyMatrix[i, j] - max /6) * 255 / max * 6),
                            255));
                    else if (_adjacencyMatrix[i, j] <= max * 3 / 6) //LIGHT BLUE to GREEN
                        result.SetPixel(j, i, Color.FromArgb(255, 
                            0,
                            255,
                            (int)((max * 3 / 6 - _adjacencyMatrix[i, j]) *255 / max * 6)));
                    else if (_adjacencyMatrix[i, j] <= max * 4 / 6) //GREEN to YELLOW
                        result.SetPixel(j, i, Color.FromArgb(255,
                            (int)((_adjacencyMatrix[i, j] - max * 3 / 6) * 255 / max * 6), 
                            255, 
                            0));
                    else if (_adjacencyMatrix[i, j] <= max * 5 / 6) //YELLOW to RED
                        result.SetPixel(j, i, Color.FromArgb(255,
                           255, 
                           (int)((max * 5 / 6 - _adjacencyMatrix[i, j]) * 255 / max * 6), 
                           0));
                    else                                            //RED to WHITE
                        result.SetPixel(j, i, Color.FromArgb(255,
                            255,
                            (int)((_adjacencyMatrix[i, j] - max * 5 / 6) * 255 / max * 6),
                            (int)((_adjacencyMatrix[i, j] - max * 5 / 6) * 255 / max * 6)));
                }
            }

            //black to green gradation
            //int currentColor;
            //for(int i = 0; i < _adjacencyMatrix.GetLength(0); ++i)
            //    for(int j = 0; j < _adjacencyMatrix.GetLength(1); ++j)
            //    {
            //        currentColor = _adjacencyMatrix[i, j] * 255 / max;
            //        result.SetPixel(j, i, 
            //            Color.FromArgb(255, 0, currentColor, currentColor/4));
            //    }

            return result;
        }

        public Bitmap GetBitmapMask()
        {
            unsafe
            {
                Bitmap result = new Bitmap(_regularPartMask.GetLength(1), _regularPartMask.GetLength(0));
                BitmapData bitmapData = result.LockBits(
                    new Rectangle(0, 0, result.Width, result.Height),
                    ImageLockMode.ReadWrite,
                    result.PixelFormat);
                int bytesPerPixel = Bitmap.GetPixelFormatSize(result.PixelFormat) / 8;
                int heightInPixels = bitmapData.Height;
                int widthInBytes = bitmapData.Width * bytesPerPixel;

                int col = 2;
                if (isIrregular)
                    col = 1;

                byte* PtrFirstPixel = (byte*)bitmapData.Scan0;
                Parallel.For(0, heightInPixels, i =>
                {
                    byte* currentLine = PtrFirstPixel + (i * bitmapData.Stride);
                    for (int j = 0; j < widthInBytes; j += bytesPerPixel)
                    {
                        if(_regularPartMask[i, j / bytesPerPixel] != isIrregular)
                        {
                            currentLine[j + col] = 220;
                            currentLine[j + 3] = 255;
                        }
                    }
                    
                });
                return result;
            }
        }
    }
}
