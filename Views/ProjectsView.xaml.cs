using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using JandH.Core.Models;
using JandH.Core.ViewModels;
using MyCraftyStash.Models;
using JandH.Core.Models;
using JandH.Core.ViewModels;
using MyCraftyStash.ViewModels;

using JandH.Core.Services;

namespace MyCraftyStash.Views
{
    public partial class ProjectsView : UserControl
    {
        public ProjectsView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ProjectsViewModel oldVm)
                oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is ProjectsViewModel newVm)
                newVm.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectsViewModel.ViewProjectItems))
                Dispatcher.InvokeAsync(() => ItemsUsedScrollViewer.ScrollToBottom(), DispatcherPriority.Loaded);
        }
        
        private void ProjectCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Project project)
            {
                if (DataContext is ProjectsViewModel vm)
                {
                    vm.ViewProjectDetailsCommand.Execute(project);
                }
            }
        }

        private void ProjectSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ProjectsViewModel vm)
                _ = vm.SearchCommand.ExecuteAsync(null);
        }
        
        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (sender is Border border)
                {
                    border.BorderBrush = (Brush)FindResource("PrimaryBrush");
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }
        
        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = (Brush)FindResource("BorderBrush");
            }
        }
        
        private void ProjectDropZone_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = (Brush)FindResource("BorderBrush");
            }
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                ProcessProjectImageFiles(files);
            }
        }
        
        private void ProjectDropZone_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button) return;
            
            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files (*.*)|*.*"
            };
            
            if (dialog.ShowDialog() == true)
            {
                ProcessProjectImageFiles(dialog.FileNames);
            }
        }
        
        private void ProcessProjectImageFiles(string[] files)
        {
            if (DataContext is ProjectsViewModel vm)
            {
                foreach (string file in files)
                {
                    if (IsImageFile(file))
                    {
                        string base64 = ConvertToBase64(file);
                        if (!string.IsNullOrEmpty(base64))
                        {
                            vm.AddNewProjectImage(base64);
                        }
                    }
                }
            }
        }
        
        private void ItemUsedCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is ProjectItem projectItem)
            {
                if (DataContext is ProjectsViewModel vm)
                {
                    vm.NavigateToItemCommand.Execute(projectItem);
                }
            }
        }
        
        private void GalleryThumb_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ProjectImage image)
            {
                if (DataContext is ProjectsViewModel vm)
                {
                    var index = vm.ProjectImages.IndexOf(image);
                    var offset = !string.IsNullOrEmpty(vm.SelectedProject?.ImageUrl) ? 1 : 0;
                    vm.SelectGalleryImageCommand.Execute(index + offset);
                }
            }
        }
        
        private void EditProjectDropZone_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = (Brush)FindResource("BorderBrush");
            }
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                ProcessProjectImageFiles(files);
            }
        }
        
        private void EditProjectDropZone_Click(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button) return;
            
            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files (*.*)|*.*"
            };
            
            if (dialog.ShowDialog() == true)
            {
                ProcessProjectImageFiles(dialog.FileNames);
            }
        }
        
        private void RemoveProjectImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string imageUrl)
            {
                if (DataContext is ProjectsViewModel vm)
                {
                    vm.RemoveNewProjectImage(imageUrl);
                }
            }
            e.Handled = true;
        }
        
        private bool IsImageFile(string path) =>
            JandH.Core.Services.ImageLoadService.IsSupported(path);

        private string ConvertToBase64(string filePath)
        {
            try
            {
                return JandH.Core.Services.ImageLoadService.LoadAsDataUri(filePath);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
