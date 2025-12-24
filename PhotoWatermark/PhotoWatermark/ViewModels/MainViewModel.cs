using Avalonia.Controls;
using System.Collections.Generic;
using Avalonia.Interactivity;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media.Imaging;
using SixLabors.Fonts;

namespace PhotoWatermark.ViewModels
{

    public partial class MainViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<ImageData> _images = new();

        [ObservableProperty]
        private ImageData _selectedImage;

        [ObservableProperty]
        private Bitmap _previewImage;

        public FontFamily SelectedFont { get; set; }

        public List<FontFamily> FontFamilies { get; set; }
    }
}
