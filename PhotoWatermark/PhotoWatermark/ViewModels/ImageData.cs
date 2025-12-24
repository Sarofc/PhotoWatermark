using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using System.Globalization;

namespace PhotoWatermark.ViewModels
{
    public partial class ImageData : ViewModelBase
    {
        [ObservableProperty]
        private Task<Bitmap?> _image;

        [ObservableProperty]
        private string _filePath;

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private string _make;

        [ObservableProperty]
        private string _model;

        [ObservableProperty]
        private string _lensModel;

        [ObservableProperty]
        private double _fNumber;

        [ObservableProperty]
        private double _exposureTime;

        [ObservableProperty]
        private ushort _iSOSpeedRatings;

        [ObservableProperty]
        private double _focalLength;

        [ObservableProperty]
        private DateTime _date;

        public ImageData(string path)
        {
            Model = "Unkown";
            LensModel = "Unkown";

            Image = LoadAsync(path);
            FilePath = path;
            Name = Path.GetFileName(path);
        }

        async Task<Bitmap?> LoadAsync(string path)
        {
            return await Task.Run(async () =>
            {
                //Console.WriteLine($"load {path} {Thread.CurrentThread.ManagedThreadId}");
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(path);
                image.Mutate(ctx =>
                {
                    ctx.Resize(800, 0);
                });

                // get exif
                var exif = image.Metadata.ExifProfile;
                if (exif != null)
                {
                    if (exif.TryGetValue(ExifTag.Make, out var make))
                    {
                        Make = make.Value!;
                    }
                    if (exif.TryGetValue(ExifTag.Model, out var model))
                    {
                        Model = NicifyModel(model.Value!);
                    }
                    if (exif.TryGetValue(ExifTag.LensModel, out var lensModel))
                    {
                        LensModel = lensModel.Value!;
                    }
                    if (exif.TryGetValue(ExifTag.FocalLength, out var focalLength))
                    {
                        FocalLength = focalLength.Value.ToDouble();
                    }
                    if (exif.TryGetValue(ExifTag.FNumber, out var fNumber))
                    {
                        FNumber = fNumber.Value.ToDouble();
                    }
                    if (exif.TryGetValue(ExifTag.ExposureTime, out var exposureTime))
                    {
                        ExposureTime = exposureTime.Value.ToDouble();
                    }
                    if (exif.TryGetValue(ExifTag.ISOSpeedRatings, out var iso))
                    {
                        ISOSpeedRatings = iso.Value![0];
                    }
                    if (exif.TryGetValue(ExifTag.DateTimeOriginal, out var dateTime))
                    {
                        // 定义格式模式
                        string format = "yyyy:MM:dd HH:mm:ss";

                        // 解析字符串
                        DateTime result = DateTime.ParseExact(
                            dateTime.Value!,
                            format,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None
                        );

                        Date = result;
                    }
                }

                using var ms = new MemoryStream();
                await image.SaveAsJpegAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);
                return new Bitmap(ms);
            });
        }

        static string NicifyModel(string model)
        {
            return model
                .Replace("Z5_2", "ℤ5Ⅱ");
        }
    }
}
