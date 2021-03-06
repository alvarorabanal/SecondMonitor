﻿using System.Windows;
using SecondMonitor.WindowsControls.WPF.CarSettingsControl;

namespace SecondMonitor.SimdataManagement.ViewModel
{
    using DataModel.BasicProperties;
    using DataModel.OperationalRange;
    using ViewModels;
    public class TyreCompoundPropertiesViewModel : AbstractViewModel<TyreCompoundProperties>, ITyreSettingsViewModel
    {
        private static readonly DependencyProperty NoWearLimitProperty = DependencyProperty.Register("NoWearLimit", typeof(double), typeof(TyreCompoundPropertiesViewModel));
        private static readonly DependencyProperty LowWearLimitProperty = DependencyProperty.Register("LowWearLimit", typeof(double), typeof(TyreCompoundPropertiesViewModel));
        private static readonly DependencyProperty HeavyWearLimitProperty = DependencyProperty.Register("HeavyWearLimit", typeof(double), typeof(TyreCompoundPropertiesViewModel));

        private string _compoundName;

        private Pressure _minimumIdealTyrePressure;
        private Pressure _maximumIdealTyrePressure;

        private Temperature _minimumIdealTyreTemperature;
        private Temperature _maximumIdealTyreTemperature;

        private bool _isGlobalCompound;

        public bool IsGlobalCompound
        {
            get => _isGlobalCompound;
            set
            {
                _isGlobalCompound = value;
                NotifyPropertyChanged();
            }
        }

        public string CompoundName
        {
            get => _compoundName;
            set
            {
                _compoundName = value;
                NotifyPropertyChanged();
            }
        }

        public Pressure MinimalIdealTyrePressure
        {
            get => _minimumIdealTyrePressure;
            set
            {
                _minimumIdealTyrePressure = value;
                NotifyPropertyChanged();
            }
        }

        public Pressure MaximumIdealTyrePressure
        {
            get => _maximumIdealTyrePressure;
            set
            {
                _maximumIdealTyrePressure = value;
                NotifyPropertyChanged();
            }
        }

        public double NoWearLimit
        {
            get => (double)GetValue(NoWearLimitProperty);
            set => SetValue(NoWearLimitProperty, value);
        }

        public double LowWearLimit
        {
            get => (double)GetValue(LowWearLimitProperty);
            set => SetValue(LowWearLimitProperty, value);
        }

        public double HeavyWearLimit
        {
            get => (double)GetValue(HeavyWearLimitProperty);
            set => SetValue(HeavyWearLimitProperty, value);
        }

        public Temperature MinimalIdealTyreTemperature
        {
            get => _minimumIdealTyreTemperature;
            set
            {
                _minimumIdealTyreTemperature = value;
                NotifyPropertyChanged();
            }
        }

        public Temperature MaximumIdealTyreTemperature
        {
            get => _maximumIdealTyreTemperature;
            set
            {
                _maximumIdealTyreTemperature = value;
                NotifyPropertyChanged();
            }
        }

        protected override void ApplyModel(TyreCompoundProperties model)
        {
            CompoundName = model.CompoundName;
            NoWearLimit = model.NoWearLimit * 100.0;
            LowWearLimit = model.LowWearLimit * 100.0;
            HeavyWearLimit = model.HeavyWearLimit * 100.0;

            MinimalIdealTyrePressure = Pressure.FromKiloPascals(model.IdealPressure.InKpa - model.IdealPressureWindow.InKpa);
            MaximumIdealTyrePressure = Pressure.FromKiloPascals(model.IdealPressure.InKpa + model.IdealPressureWindow.InKpa);

            MinimalIdealTyreTemperature = Temperature.FromCelsius(model.IdealTemperature.InCelsius - model.IdealTemperatureWindow.InCelsius);
            MaximumIdealTyreTemperature = Temperature.FromCelsius(model.IdealTemperature.InCelsius + model.IdealTemperatureWindow.InCelsius);
        }

        public override TyreCompoundProperties SaveToNewModel()
        {
            return new TyreCompoundProperties()
                       {
                           CompoundName = CompoundName,
                           LowWearLimit =  LowWearLimit / 100.0,
                           NoWearLimit = NoWearLimit / 100.0,
                           HeavyWearLimit = HeavyWearLimit / 100.0,
                           IdealPressure = Pressure.FromKiloPascals((MinimalIdealTyrePressure.InKpa + MaximumIdealTyrePressure.InKpa) * 0.5),
                           IdealPressureWindow = Pressure.FromKiloPascals((MaximumIdealTyrePressure.InKpa - MinimalIdealTyrePressure.InKpa) * 0.5),

                           IdealTemperature = Temperature.FromCelsius((MinimalIdealTyreTemperature.InCelsius + MaximumIdealTyreTemperature.InCelsius) * 0.5),
                           IdealTemperatureWindow = Temperature.FromCelsius((MaximumIdealTyreTemperature.InCelsius - MinimalIdealTyreTemperature.InCelsius) * 0.5),
                        };
        }
    }
}