using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using PhotoWatermark.Converters;
using PhotoWatermark.ViewModels;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

using VFont = SixLabors.Fonts.Font;
using VFontFamily = SixLabors.Fonts.FontFamily;
using VImage = SixLabors.ImageSharp.Image;
using VPoint = SixLabors.ImageSharp.Point;
using VSize = SixLabors.ImageSharp.Size;
using VColor = SixLabors.ImageSharp.Color;

namespace PhotoWatermark.Views
{
    public partial class MainView : UserControl
    {
        public MainViewModel VM => DataContext as MainViewModel;

        public MainView()
        {
            InitializeComponent();
        }

        protected override void OnDataContextBeginUpdate()
        {
            base.OnDataContextBeginUpdate();
            LoadFonts();
        }

        const string FontFile = "font.txt";

        void LoadFonts()
        {
            VM.FontFamilies = SystemFonts.Collection.Families
                .OrderBy(f => f.Name)
                .ToList();
            //Console.WriteLine(string.Join(", ", VM.FontFamilies));

            var fontFamiliyName = LoadSelectFontName();
            VM.SelectedFont = (VM.FontFamilies.FirstOrDefault(f => f.Name == fontFamiliyName));
        }

        static string LoadSelectFontName()
        {
            if (File.Exists(FontFile))
            {
                return File.ReadAllText(FontFile);
            }
            return "Arial Unicode MS";
        }

        static void SaveSelectFontName(string fontName)
        {
            File.WriteAllText(FontFile, fontName);
        }

        void FontFamilies_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is VFontFamily fontFamily)
            {
                //Console.WriteLine($"FontFamilies_SelectionChanged {fontFamily}");
                SaveSelectFontName(fontFamily.Name);
            }
        }

        private async Task<List<string>> GetImageFilesFromFolderAsync()
        {
            var files = new List<string>();

            // 获取当前窗口的 TopLevel 实例
            var topLevel = TopLevel.GetTopLevel(this); // 'this' 指向你的窗口或用户控件

            // 配置文件夹选择选项
            var folderPickerOptions = new FolderPickerOpenOptions
            {
                Title = "选择图片文件夹",
                AllowMultiple = false // 一次只选择一个文件夹
            };

            // 打开文件夹选择器
            var selectedFolders = await topLevel.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);

            if (selectedFolders.Count == 1)
            {
                var selectedFolder = selectedFolders[0];
                // 获取文件夹中的所有文件
                var folderFiles = selectedFolder.GetItemsAsync();

                // 定义支持的图片扩展名
                var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
                };

                await foreach (var file in folderFiles)
                {
                    if (file is IStorageFile storageFile)
                    {
                        var extension = System.IO.Path.GetExtension(file.Name);
                        if (imageExtensions.Contains(extension))
                        {
                            // 获取文件的本地路径
                            files.Add(file.Path.LocalPath);
                        }
                    }
                }
            }
            return files;
        }

        // 处理按钮点击事件
        private async void OnLoadImagesClick(object? sender, RoutedEventArgs e)
        {
            var imagePaths = await GetImageFilesFromFolderAsync();
            var bitmaps = VM.Images;

            bitmaps.Clear();

            foreach (var path in imagePaths)
            {
                try
                {
                    // 从文件路径加载位图
                    bitmaps.Add(new ImageData(path));
                }
                catch (Exception ex)
                {
                    // 处理图片加载错误（例如文件损坏）
                    Console.WriteLine($"加载图片 {path} 时出错: {ex.Message}");
                }
            }
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            var imageToSave = VM.SelectedImage;
            var fontFamily = VM.SelectedFont;
            if (imageToSave != null)
            {
                var path = imageToSave.FilePath;
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                var directory = System.IO.Path.GetDirectoryName(path);
                directory = $"{directory}_watermark";
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                var output = $"{directory}/{fileName}.watermark.JPG";

                await SaveImageAsync(imageToSave, fontFamily, output);

                // TODO notification
            }
        }

        private async void OnSaveAllClick(object? sender, RoutedEventArgs e)
        {
            var iamges = VM.Images;
            var fontFamily = VM.SelectedFont;
            if (Images != null)
            {
                var tasks = new Task[iamges.Count];
                for (int i = 0; i < iamges.Count; i++)
                {
                    var item = iamges[i];

                    var path = item.FilePath;
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                    var directory = System.IO.Path.GetDirectoryName(path);
                    directory = $"{directory}_watermark";
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);
                    var output = $"{directory}/{fileName}.watermark.JPG";

                    tasks[i] = Task.Run(() => SaveImageAsync(item, fontFamily, output));
                }

                await Task.WhenAll(tasks);

                Console.WriteLine("save done");
            }
            else
            {
                throw new Exception("TODO notification OnSaveAllClick error");
            }
        }

        private async void Images_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            //Console.WriteLine(e.ToString());

            if (e.AddedItems.Count > 0)
            {
#if true
                var imageData = e.AddedItems[0] as ImageData;

                // 1. 加载图片
                using var vimage = await VImage.LoadAsync(imageData.FilePath);

                vimage.Mutate(ctx =>
                {
                    ctx.Resize(800, 0);
                });

                // 3. 添加水印
                //AddWatermarkGaussianBlur(vimage, imageData);
                AddWatermarkClassic(vimage, imageData, VM.SelectedFont);

                using var ms = new MemoryStream();
                ms.Seek(0, SeekOrigin.Begin);
                await vimage.SaveAsJpegAsync(ms);

                VM.PreviewImage?.Dispose();
                ms.Seek(0, SeekOrigin.Begin);
                VM.PreviewImage = new Bitmap(ms);
#endif
            }
        }

        static async Task SaveImageAsync(ImageData imageData, VFontFamily fontFamily, string output)
        {
            // 1. 加载图片
            using var vimage = await VImage.LoadAsync(imageData.FilePath);

            // 3. 添加水印
            //AddWatermarkGaussianBlur(vimage, imageData);
            AddWatermarkClassic(vimage, imageData, fontFamily);

            //4.保存图片
            vimage.Save(output, new JpegEncoder
            {
                Quality = 98,
                //Interleaved = true,
            });

            Console.WriteLine($"save image: {output}");
        }


        static void AddWatermarkClassic(VImage image, ImageData imageData, VFontFamily fontFamily)
        {
            System.Diagnostics.Debug.Assert(imageData != null);

            var paddingX = 0.015f;
            var paddingY = 0.08f;

            var paddingLeft = (int)MathF.Round(image.Height * paddingX);
            var paddingRight = (int)MathF.Round(image.Height * paddingX);
            var paddingTop = (int)MathF.Round(image.Height * paddingX);
            var paddingBottom = (int)MathF.Round(image.Height * paddingY);

            var paddingWidth = paddingLeft + paddingRight;
            var paddingHeight = paddingTop + paddingBottom;

            using var foreground = image.Clone(ctx => { });

            image.Mutate(async ctx =>
            {
                var newSize = new VSize(image.Width + paddingWidth, image.Height + paddingHeight);
                ctx.Resize(newSize);

                ctx.Fill(VColor.White);

                ctx.DrawImage(foreground, new VPoint(paddingLeft, paddingTop), 1f);

                var dpi = (float)image.Metadata.VerticalResolution;

                var fondSize = image.Height / 56;

                //                Console.WriteLine(@$"
                //paddingLeft   = {paddingLeft}
                //paddingRight  = {paddingRight}
                //paddingTop    = {paddingTop}
                //paddingBottom = {paddingBottom}
                //fondSize      = {fondSize}
                //                ");

                var fontModel = new VFont(fontFamily, fondSize);
                var fontLensModel = new VFont(fontFamily, fondSize * 0.8f);
                var fontParams = new VFont(fontFamily, fondSize);
                var fontDate = new VFont(fontFamily, fondSize * 0.75f);

                // 相机
                // 镜头
                {
                    var modelOptions = new RichTextOptions(fontModel)
                    {
                        //Dpi = dpi,
                        VerticalAlignment = SixLabors.Fonts.VerticalAlignment.Center,
                    };
                    var modelText = $"{imageData.Model}";
                    var modelTextSize = TextMeasurer.MeasureSize(modelText, modelOptions);
                    var modelPosition = new Vector2(paddingLeft, image.Height - paddingBottom / 2f - modelTextSize.Height / 1.5f);
                    modelOptions.Origin = modelPosition;
                    ctx.DrawText(modelOptions, modelText, VColor.Black);

                    var lensModelTextOptions = new RichTextOptions(fontLensModel)
                    {
                        //Dpi = dpi,
                        VerticalAlignment = SixLabors.Fonts.VerticalAlignment.Center,
                    };
                    modelPosition.Y += modelTextSize.Height * 1.6f;
                    lensModelTextOptions.Origin = modelPosition;
                    var lensModeltext = $"{imageData.LensModel}";
                    ctx.DrawText(lensModelTextOptions, lensModeltext, VColor.Gray);
                }

                // 焦距 光圈 快门 iso
                // 日期
                {
                    var rightParamsOptions = new RichTextOptions(fontParams)
                    {
                        //Dpi = dpi,
                        VerticalAlignment = SixLabors.Fonts.VerticalAlignment.Center,
                    };
                    var rightExifText = $"{imageData.FocalLength}mm  f/{imageData.FNumber}  {ExposureTimeConverter.FormatExposureTime(imageData.ExposureTime)}s  ISO{imageData.ISOSpeedRatings}";
                    var rightExifSize = TextMeasurer.MeasureSize(rightExifText, rightParamsOptions);
                    var rightPosition = new Vector2(image.Width - rightExifSize.Width - paddingRight, image.Height - paddingBottom / 2 - rightExifSize.Height / 1.5f);
                    rightParamsOptions.Origin = rightPosition;
                    //Console.WriteLine($"{rightPosition} {rightExifSize}");
                    ctx.DrawText(rightParamsOptions, rightExifText, VColor.Black);

                    var dateTimeTextOptions = new RichTextOptions(fontDate)
                    {
                        //Dpi = dpi,
                        VerticalAlignment = SixLabors.Fonts.VerticalAlignment.Center,
                    };
                    rightPosition.Y += rightExifSize.Height * 1.5f;
                    dateTimeTextOptions.Origin = rightPosition;
                    var dateText = $"{imageData.Date}";
                    ctx.DrawText(dateTimeTextOptions, dateText, VColor.Gray);

                    // logo
#if true
                    using var stream = AssetLoader.Open(new Uri("avares://PhotoWatermark/Assets/nikon.png"));
                    using var logo = VImage.Load(stream);
                    logo.Mutate(ctx =>
                    {
                        var logoSize = (int)MathF.Round(paddingBottom / 1.5f);
                        ctx.Resize(logoSize, logoSize);
                    });
                    var logoLocation = new VPoint(
                        image.Width - (int)rightExifSize.Width - paddingRight - (int)(logo.Width * 1.2f),
                        image.Height - paddingBottom / 2 - logo.Height / 2
                    );
                    ctx.DrawImage(logo, logoLocation, 1f);
#endif
                }
            });
        }

#if false // TODO 支持模糊背景
        static void AddWatermarkGaussianBlur(VImage image, ImageData imageData)
        {
            System.Diagnostics.Debug.Assert(imageData != null);

            //var aspect = (float)image.Width / image.Height;
            var aspect = 1;

            var extendHeight = 200;
            var extendWidth = (int)MathF.Ceiling(extendHeight * aspect);

            var rect = new Rectangle(extendWidth, extendHeight, image.Width - extendWidth * 2, image.Height - extendHeight * 2);

            //Console.WriteLine(rect);

            // 绘制水印
            var foreground = image.Clone(ctx =>
            {
                ctx.Crop(rect);

                // TODO 圆角
                //var cornerRadius = 50f;
                //var size = ctx.GetCurrentSize();

                //var polygon = new RectangularPolygon(0, 0, rect.Width, rect.Height);
                //var cornerShape = new EllipsePolygon(rect.Width / 2, rect.Height / 2, cornerRadius);
                //var rounded = polygon.Clip(cornerShape);
                //ctx.Clip(rounded, ctx1 =>
                //{

                //});
            });

            Console.WriteLine($"foreground {foreground.Width}x{foreground.Height}");

            image.Mutate(ctx =>
            {
                // 高斯模糊
                ctx.GaussianBlur(66);

                var bottomOffset = 80;
                rect.Y -= bottomOffset;
                ctx.DrawImage(foreground, new SixLabors.ImageSharp.Point(rect.X, rect.Y), 1f);

                // TODO 字体选择
                //var fonts = new FontCollection();
                //var fontFamily = fonts.Add("Arial");
                //var fontFamily = SystemFonts.Get("Arial");
                //var fontFamily = SystemFonts.Get("Times New Roman");
                var fontFamily = SystemFonts.Get("Microsoft YaHei");


                var fontModel = new Font(fontFamily, 30);
                var fontLensModel = new Font(fontFamily, 24, SixLabors.Fonts.FontStyle.Regular);
                var fontParams = new Font(fontFamily, 26);
                var fontDate = new Font(fontFamily, 20, SixLabors.Fonts.FontStyle.Regular);

                var fontDpi = (float)image.Metadata.HorizontalResolution;

                // 相机
                // 镜头
                {
                    var modelOptions = new RichTextOptions(fontModel)
                    {
                        Dpi = fontDpi,
                    };
                    var modelText = $"{imageData.Model}";
                    var modelTextSize = TextMeasurer.MeasureSize(modelText, modelOptions);
                    var modelPosition = new PointF(extendWidth, image.Height - extendHeight - bottomOffset / 2);
                    modelOptions.Origin = modelPosition;
                    ctx.DrawText(modelOptions, modelText, Color.White);

                    var lensModelTextOptions = new RichTextOptions(fontLensModel)
                    {
                        Dpi = fontDpi,
                    };
                    modelPosition.Y += modelTextSize.Height + 30f;
                    lensModelTextOptions.Origin = modelPosition;
                    var lensModeltext = $"{imageData.LensModel}";
                    ctx.DrawText(lensModelTextOptions, lensModeltext, Color.Gray);
                }

                // 焦距 光圈 快门 iso
                // 日期
                {
                    var rightParamsOptions = new RichTextOptions(fontParams)
                    {
                        Dpi = fontDpi,
                    };
                    var rightExifText = $"{imageData.FocalLength}mm f/{imageData.FNumber} {ExposureTimeConverter.FormatExposureTime(imageData.ExposureTime)}s ISO{imageData.Iso}";
                    var rightExifSize = TextMeasurer.MeasureSize(rightExifText, rightParamsOptions);
                    var rightPosition = new PointF(image.Width - rightExifSize.Width - extendWidth, image.Height - extendHeight - bottomOffset / 2);
                    rightParamsOptions.Origin = rightPosition;
                    //Console.WriteLine($"{rightPosition} {rightExifSize}");
                    ctx.DrawText(rightParamsOptions, rightExifText, Color.White);

                    var dateTimeTextOptions = new RichTextOptions(fontDate)
                    {
                        Dpi = fontDpi,
                    };
                    rightPosition.Y += rightExifSize.Height + 30f;
                    dateTimeTextOptions.Origin = rightPosition;
                    ctx.DrawText(dateTimeTextOptions, imageData.DateTime, Color.Gray);
                }
            });
        }
#endif
    }
}