using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ManagerIV.ViewModels;

namespace ManagerIV.Views;

public partial class MusicView : UserControl
{
    public MusicView()
    {
        InitializeComponent();
    }

    private void UserControl_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            DragOverOverlay.Visibility = Visibility.Visible;
            if (Resources["FadeInOverlay"] is System.Windows.Media.Animation.Storyboard fadeStoryboard)
            {
                fadeStoryboard.Begin(this, true);
            }
            if (Resources["MarchingAnts"] is System.Windows.Media.Animation.Storyboard storyboard)
            {
                storyboard.Begin(this, true);
            }
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void UserControl_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void UserControl_DragLeave(object sender, DragEventArgs e)
    {
        HideDragOverlay();
        e.Handled = true;
    }

    private async void UserControl_Drop(object sender, DragEventArgs e)
    {
        HideDragOverlay();
        
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            
            // Filter audio files
            var audioFiles = files.Where(f => 
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext == ".mp3" || ext == ".wma" || ext == ".m4a";
            }).ToArray();

            if (audioFiles.Length > 0 && DataContext is MainViewModel mainVm)
            {
                await mainVm.Music.ImportFilesAsync(audioFiles);
            }
        }
        e.Handled = true;
    }

    private void HideDragOverlay()
    {
        DragOverOverlay.Visibility = Visibility.Collapsed;
        DragOverOverlay.Opacity = 0;
        if (Resources["MarchingAnts"] is System.Windows.Media.Animation.Storyboard storyboard)
        {
            storyboard.Stop(this);
        }
    }
}
