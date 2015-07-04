using YALV.Common;
using YALV.Common.Interfaces;
using YALV.Core.Domain;

namespace YALV.ViewModel
{
    public class SelectColumnsVM
        : BindableObject
    {
        private readonly IWinSimple _callingWin;
        private readonly FilteredGridManager _filteredGrid;
        private readonly ColumnVisibilitySettings _settings;

        public SelectColumnsVM(IWinSimple win, FilteredGridManager filteredGrid)
        {
            _callingWin = win;
            _filteredGrid = filteredGrid;
            _settings = filteredGrid.GetColumnVisibilitySettings();
            CommandOk = new CommandRelay(SaveSettings, _ => true);
            CommandCancel = new CommandRelay(Exit, _ => true);
        }

        #region Commands

        public ICommandAncestor CommandOk { get; private set; }
        private object SaveSettings(object parameter)
        {
            try
            {
                _filteredGrid.UpdateColumVisibilitySettings(Settings);
            }
            finally
            {
                _callingWin.Close();
            }
            return null;
        }

        public ICommandAncestor CommandCancel { get; private set; }
        private object Exit(object parameter)
        {
            _callingWin.Close();
            return null;
        }

        #endregion

        #region Public properties

        public ColumnVisibilitySettings Settings
        {
            get { return _settings; }
        }

        public bool ShowId
        {
            get { return Settings.ShowId; }
            set
            {
                if (value != Settings.ShowId)
                {
                    Settings.ShowId = value;
                    RaisePropertyChanged(() => ShowId);
                }
            }
        }

        public bool ShowTimestamp
        {
            get { return Settings.ShowTimestamp; }
            set
            {
                if (value != Settings.ShowTimestamp)
                {
                    Settings.ShowTimestamp = value;
                    RaisePropertyChanged(() => ShowTimestamp);
                }
            }
        }

        public bool ShowThread
        {
            get { return Settings.ShowThread; }
            set
            {
                if (value != Settings.ShowThread)
                {
                    Settings.ShowThread = value;
                    RaisePropertyChanged(() => ShowThread);
                }
            }
        }

        public bool ShowLevel
        {
            get { return Settings.ShowLevel; }
            set
            {
                if (value != Settings.ShowLevel)
                {
                    Settings.ShowLevel = value;
                    RaisePropertyChanged(() => ShowLevel);
                }
            }
        }

        public bool ShowLogger
        {
            get { return Settings.ShowLogger; }
            set
            {
                if (value != Settings.ShowLogger)
                {
                    Settings.ShowLogger = value;
                    RaisePropertyChanged(() => ShowLogger);
                }
            }
        }

        public bool ShowMessage
        {
            get { return Settings.ShowMessage; }
            set
            {
                if (value != Settings.ShowMessage)
                {
                    Settings.ShowMessage = value;
                    RaisePropertyChanged(() => ShowMessage);
                }
            }
        }

        public bool ShowApplication
        {
            get { return Settings.ShowApplication; }
            set
            {
                if (value != Settings.ShowApplication)
                {
                    Settings.ShowApplication = value;
                    RaisePropertyChanged(() => ShowApplication);
                }
            }
        }

        public bool ShowUserName
        {
            get { return Settings.ShowUserName; }
            set
            {
                if (value != Settings.ShowUserName)
                {
                    Settings.ShowUserName = value;
                    RaisePropertyChanged(() => ShowUserName);
                }
            }
        }

        public bool ShowMachineName
        {
            get { return Settings.ShowMachineName; }
            set
            {
                if (value != Settings.ShowMachineName)
                {
                    Settings.ShowMachineName = value;
                    RaisePropertyChanged(() => ShowMachineName);
                }
            }
        }

        public bool ShowHostName
        {
            get { return Settings.ShowHostName; }
            set
            {
                if (value != Settings.ShowHostName)
                {
                    Settings.ShowHostName = value;
                    RaisePropertyChanged(() => ShowHostName);
                }
            }
        }

        public bool ShowClass
        {
            get { return Settings.ShowClass; }
            set
            {
                if (value != Settings.ShowClass)
                {
                    Settings.ShowClass = value;
                    RaisePropertyChanged(() => ShowClass);
                }
            }
        }

        public bool ShowMethod
        {
            get { return Settings.ShowMethod; }
            set
            {
                if (value != Settings.ShowMethod)
                {
                    Settings.ShowMethod = value;
                    RaisePropertyChanged(() => ShowMethod);
                }
            }
        }

        #endregion
    }
}