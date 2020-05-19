﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Sdl.Community.XLIFF.Manager.Commands;
using Sdl.Community.XLIFF.Manager.Common;
using Sdl.Community.XLIFF.Manager.Model;

namespace Sdl.Community.XLIFF.Manager.Wizard.ViewModel
{
	public class WizardViewModel : INotifyPropertyChanged, IDisposable
	{		
		private Window _window;
		private ObservableCollection<WizardPageViewModelBase> _pages;
		private WizardPageViewModelBase _currentPage;
		
		private RelayCommand _moveNextCommand;
		private RelayCommand _moveBackCommand;
		private RelayCommand _finishCommand;
		private RelayCommand _cancelCommand;

		public WizardViewModel(Window window, ObservableCollection<WizardPageViewModelBase> pages, 
			TransactionModel transctionModel, Enumerators.Action action)
		{
			SetWindow(window);		
			Pages = pages;
			Action = action;
			TransctionModel = transctionModel;
			UpdateWizardHeader(_window.ActualWidth);
			SetCurrentPage(Pages[0]);
		}
		public Enumerators.Action Action { get; set; }

		public TransactionModel TransctionModel { get; set; }

		private void SetWindow(Window window)
		{
			if (_window != null)
			{
				_window.SizeChanged -= Window_SizeChanged;
			}

			_window = window;

			if (_window != null)
			{
				_window.SizeChanged += Window_SizeChanged;
			}
		}

		private void SetCurrentPage(WizardPageViewModelBase currentPage)
		{
			CurrentPage = currentPage;
			_window.Dispatcher.Invoke(delegate { }, DispatcherPriority.ContextIdle);
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			UpdateWizardHeader(_window.ActualWidth);
		}

		private void UpdateWizardHeader(double actualWidth)
		{
			if (_window == null || actualWidth <= 0 || Pages == null)
			{
				return;
			}

			// (ICON)[LINE]
			// [TEXT------]

			// [LINE](ICON)[LINE]
			// [------TEXT------]

			// [LINE](ICON)
			// [------TEXT]

			var windowMargin = 40;
			var iconSize = new Size(18, 18);

			var totalItems = Pages.Count;

			var controlActualWidth = actualWidth - windowMargin;
			var totalIconWidth = iconSize.Width * totalItems;

			var totalLineWidth = controlActualWidth - totalIconWidth;
			var fixedLineWidth = totalLineWidth / (Pages.Count - 1);

			foreach (var page in Pages)
			{
				if (page.IsOnFirstPage || page.IsOnLastPage)
				{
					page.LabelLineWidth = fixedLineWidth - fixedLineWidth / 2;
					page.LabelTextWidth = (fixedLineWidth - fixedLineWidth / 2) + iconSize.Width;
				}
				else
				{
					page.LabelLineWidth = fixedLineWidth / 2;
					page.LabelTextWidth = fixedLineWidth + iconSize.Width;
				}
			}
		}

		public string WindowTitle { get; private set; }

		public WizardPageViewModelBase CurrentPage
		{
			get => _currentPage;
			private set
			{
				if (value == _currentPage)
				{
					return;
				}

				if (_currentPage != null)
				{
					_currentPage.IsCurrentPage = false;
				}

				_currentPage = value;

				WindowTitle = string.Format("{0} {1} Wizard - {2}", PluginResources.XLIFFManager_Name, Action, CurrentPage.DisplayName);

				// move focus to the page in the wizard early
				OnPropertyChanged(nameof(CurrentPage));

				if (_currentPage != null)
				{
					_currentPage.IsVisited = true;
					_currentPage.IsCurrentPage = true;
				}

				OnPropertyChanged(nameof(CurrentPage));
				OnPropertyChanged(nameof(IsOnLastPage));
				OnPropertyChanged(nameof(WindowTitle));
				OnPropertyChanged(nameof(CompletedSteps));
			}
		}

		public ObservableCollection<WizardPageViewModelBase> Pages
		{
			get => _pages;
			set
			{
				if (_pages == value)
				{
					return;
				}

				if (_pages != null)
				{
					RemoveEventhandlers(_pages);
				}

				_pages = value;

				AddEventhandlers(_pages);

				OnPropertyChanged(nameof(Pages));
			}
		}

		private void AddEventhandlers(ObservableCollection<WizardPageViewModelBase> pages)
		{
			foreach (var viewModelBase in pages)
			{
				viewModelBase.PropertyChanged += Pages_PropertyChanged;
			}

			pages.CollectionChanged += Pages_CollectionChanged;
		}

		private void RemoveEventhandlers(ObservableCollection<WizardPageViewModelBase> pages)
		{
			foreach (var viewModelBase in pages)
			{
				viewModelBase.PropertyChanged -= Pages_PropertyChanged;
			}

			pages.CollectionChanged -= Pages_CollectionChanged;
		}

		private void Pages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			foreach (WizardPageViewModelBase viewModelBase in e.OldItems)
			{
				viewModelBase.PropertyChanged -= Pages_PropertyChanged;
			}

			foreach (WizardPageViewModelBase viewModelBase in e.NewItems)
			{
				viewModelBase.PropertyChanged += Pages_PropertyChanged;
			}
		}

		private void Pages_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "IsValid")
			{
				OnPropertyChanged(nameof(CompletedSteps));
			}
		}

		private int CurrentPageIndex
		{
			get
			{
				if (CurrentPage == null)
				{
					return 0;
					Debug.Fail("The current page is null!");
				}

				return Pages.IndexOf(CurrentPage);
			}
		}

		public bool IsOnLastPage => CurrentPageIndex == Pages.Count - 1;

		public bool IsComplete => IsOnLastPage && CurrentPage.IsComplete;

		public string CompletedSteps =>
			string.Format("{0} of {1} completed", Pages.Count(page => page.IsVisited), Pages.Count);

		public ICommand CancelCommand => _cancelCommand ?? (_cancelCommand = new RelayCommand(CancelWizard, () => CanCancel));

		private void CancelWizard()
		{
			//HelixModel = null;
			OnRequestClose();
		}

		private bool CanCancel
		{
			get
			{
				return CurrentPage != null
					   && (IsOnLastPage && CurrentPage.IsComplete) ? false : true;
			}
		}

		public ICommand FinishCommand => _finishCommand ?? (_finishCommand = new RelayCommand(FinishWizard, () => CanFinish));

		private bool CanFinish
		{
			get
			{
				return CurrentPage != null
					   && ((CurrentPageIndex == 0 && CurrentPage.IsValid)
							|| (IsOnLastPage && CurrentPage.IsComplete)
							|| (CurrentPageIndex > 0 && !IsOnLastPage));
			}
		}

		private void FinishWizard()
		{
			if (!IsOnLastPage)
			{
				var lastPage = Pages[Pages.Count - 1];
				for (var i = CurrentPageIndex; i < lastPage.PageIndex; i++)
				{
					Pages[i].PreviousIsVisited = true;
					Pages[i].IsVisited = true;
					Pages[i].NextIsVisited = true;
				}

				SetCurrentPage(Pages[Pages.Count - 1]);
			}

			else
			{
				OnRequestClose();
			}
		}

		public ICommand MoveNextCommand
		{
			get
			{
				return _moveNextCommand ??
					   (_moveNextCommand = new RelayCommand(
						   MoveToNextPage,
						   () => CanMoveToNextPage));
			}
		}

		private void MoveToNextPage()
		{
			if (!CanMoveToNextPage)
			{
				return;
			}

			if (CurrentPageIndex < Pages.Count - 1)
			{
				_currentPage.NextIsVisited = true;
				OnPropertyChanged(nameof(CurrentPage));

				SetCurrentPage(Pages[CurrentPageIndex + 1]);
			}
			else
			{
				OnRequestClose();
			}
		}

		private bool CanMoveToNextPage => CurrentPage != null && CurrentPage.IsValid && !CurrentPage.IsOnLastPage;

		public ICommand MoveBackCommand
		{
			get
			{
				return _moveBackCommand ?? (_moveBackCommand = new RelayCommand(
						   MoveToPreviousPage,
						   () => CanMoveToPreviousPage));
			}
		}

		private bool CanMoveToPreviousPage
		{
			get
			{
				return (CurrentPageIndex > 0 && !CurrentPage.IsOnLastPage)
					   || (CurrentPage.IsOnLastPage && !CurrentPage.IsComplete);
			}
		}

		private void MoveToPreviousPage()
		{
			if (CanMoveToPreviousPage)
			{
				_currentPage.PreviousIsVisited = true;
				OnPropertyChanged(nameof(CurrentPage));

				SetCurrentPage(Pages[CurrentPageIndex - 1]);
			}
		}

		public event EventHandler RequestClose;

		private void OnRequestClose()
		{
			RequestClose?.Invoke(this, EventArgs.Empty);
		}

		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		public void Dispose()
		{
			if (_window != null)
			{
				_window.SizeChanged -= Window_SizeChanged;
			}

			if (Pages != null)
			{
				RemoveEventhandlers(Pages);
			}
		}
	}
}