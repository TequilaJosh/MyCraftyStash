using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyCraftyStash.Services;
using MyCraftyStash.Views;

namespace MyCraftyStash.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private readonly InventoryService _service;
        private readonly WishlistService _wishlistService;
        private readonly PurchaseReportService _reportService;
        private readonly ExportService _exportService;

        [ObservableProperty] private object? _currentView;
        [ObservableProperty] private string _currentPage = "Home";

        public HomeViewModel           HomeVM           { get; }
        public InventoryViewModel      InventoryVM      { get; }
        public ProjectsViewModel       ProjectsVM       { get; }
        public InspirationViewModel    InspirationVM    { get; }
        public SentimentSearchViewModel SentimentSearchVM { get; }
        public StockTrackingViewModel  StockTrackingVM  { get; }
        public SocialViewModel         SocialVM         { get; }
        public ColorMatchViewModel     ColorMatchVM     { get; }
        public WishlistViewModel       WishlistVM       { get; }
        public PurchaseReportViewModel PurchaseReportVM { get; }
        public MyCraftyStash.Views.EnvelopeExpertView EnvelopeExpertView { get; private set; }

        public MainViewModel()
        {
            _service        = new InventoryService();
            _wishlistService = new WishlistService(() => new Data.InventoryDbContext());
            _reportService  = new PurchaseReportService(() => new Data.InventoryDbContext());
            _exportService  = new ExportService(() => new Data.InventoryDbContext());

            HomeVM           = new HomeViewModel(_service, this);
            InventoryVM      = new InventoryViewModel(_service, this);
            ProjectsVM       = new ProjectsViewModel(_service, this);
            InspirationVM    = new InspirationViewModel(_service, this);
            SentimentSearchVM = new SentimentSearchViewModel(_service, this);
            StockTrackingVM  = new StockTrackingViewModel(_service, this);
            SocialVM         = new SocialViewModel(this);
            ColorMatchVM     = new ColorMatchViewModel();
            WishlistVM       = new WishlistViewModel(_wishlistService, _service, this);
            PurchaseReportVM = new PurchaseReportViewModel(_reportService, _exportService);
            EnvelopeExpertView = new MyCraftyStash.Views.EnvelopeExpertView();

            // Ensure wishlist table exists on startup
            _ = _wishlistService.EnsureTableAsync();

            // Kick off TE event scraping in the background. Failures are
            // logged but never block startup; the calendar overlay just
            // shows the previous cache.
            _ = new TeEventScraperService().RefreshIfStaleAsync();

            CurrentView = HomeVM;
            _ = HomeVM.LoadAsync();
        }

        [RelayCommand]
        private async Task NavigateToHome()
        {
            CurrentPage = "Home";
            CurrentView = HomeVM;
            await HomeVM.RefreshAsync();
        }

        [RelayCommand]
        private void NavigateToInventory()
        {
            CurrentPage = "Inventory";
            EnvelopeExpertView = new MyCraftyStash.Views.EnvelopeExpertView();
            CurrentView = InventoryVM;
            InventoryVM.BackToList();
        }

        [RelayCommand]
        private void NavigateToStockTracking()
        {
            CurrentPage = "Stock";
            CurrentView = StockTrackingVM;
            StockTrackingVM.LoadStockCommand.Execute(null);
        }

        [RelayCommand]
        private async Task NavigateToSocial()
        {
            CurrentPage = "Social";
            CurrentView = SocialVM;
            await SocialVM.LoadAllAsync();
        }

        [RelayCommand]
        private void NavigateToColorMatch()
        {
            CurrentPage = "ColorMatch";
            CurrentView = ColorMatchVM;
            ColorMatchVM.Load();
        }

        [RelayCommand]
        private void NavigateToProjects()
        {
            CurrentPage = "Projects";
            CurrentView = ProjectsVM;
            ProjectsVM.LoadProjectsCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToInspiration()
        {
            CurrentPage = "Inspiration";
            CurrentView = InspirationVM;
            InspirationVM.LoadImagesCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToSentimentSearch()
        {
            CurrentPage = "SentimentSearch";
            CurrentView = SentimentSearchVM;
        }

        [RelayCommand]
        private void NavigateToEnvelopeExpert()
        {
            CurrentPage = "EnvelopeExpert";
            CurrentView = EnvelopeExpertView;
        }

        [RelayCommand]
        private async Task NavigateToAddItem()
        {
            CurrentPage = "AddItem";
            await InventoryVM.StartAddItem();
            EnvelopeExpertView = new MyCraftyStash.Views.EnvelopeExpertView();
            CurrentView = InventoryVM;
        }

        [RelayCommand]
        private async Task NavigateToAddProject()
        {
            CurrentPage = "AddProject";
            await ProjectsVM.StartAddProject();
            CurrentView = ProjectsVM;
        }

        [RelayCommand]
        private void NavigateToWishlist()
        {
            CurrentPage = "Wishlist";
            CurrentView = WishlistVM;
            WishlistVM.LoadItemsCommand.Execute(null);
        }

        [RelayCommand]
        private void NavigateToPurchaseReport()
        {
            CurrentPage = "PurchaseReport";
            CurrentView = PurchaseReportVM;
        }

        [RelayCommand]
        private void OpenExportDialog()
        {
            var dialog = new ExportDialog(_exportService)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }

        [RelayCommand]
        private void OpenSettings()
        {
            var dialog = new SettingsDialog();
            if (dialog.ShowDialog() == true && dialog.WasSaved)
            {
                InventoryVM.RefreshConfigLists();
                InventoryService.ReloadTrackedTypes();
            }
        }

        [RelayCommand]
        private void OpenAbout()
        {
            var dialog = new AboutDialog
            {
                Owner = Application.Current.MainWindow,
            };
            dialog.ShowDialog();
        }
    }
}
