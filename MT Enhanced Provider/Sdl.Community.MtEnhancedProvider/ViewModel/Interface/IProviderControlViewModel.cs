﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Sdl.Community.MtEnhancedProvider.Model;
using Sdl.Community.MtEnhancedProvider.Model.Interface;

namespace Sdl.Community.MtEnhancedProvider.ViewModel.Interface
{
	public interface IProviderControlViewModel
	{
		IModelBase ViewModel { get; set; }
		ICommand ShowSettingsCommand { get; set; }
	}
}