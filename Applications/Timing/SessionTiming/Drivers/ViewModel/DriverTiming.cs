﻿namespace SecondMonitor.Timing.SessionTiming.Drivers.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using DataModel.BasicProperties;
    using DataModel.Extensions;
    using DataModel.Snapshot;
    using NLog;
    using SecondMonitor.DataModel.Snapshot.Drivers;
    using SecondMonitor.Timing.SessionTiming.ViewModel;

    public class DriverTiming
    {
        private const int MaxLapsWithTelemetry = 5;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly TimeSpan MaxPendingStateWait = TimeSpan.FromSeconds(5);

        private Stopwatch _refreshBestSectorIndicationWatch;
        private readonly Velocity _maximumVelocity = Velocity.FromMs(85);
        private readonly List<LapInfo> _lapsInfo;
        private readonly List<PitStopInfo> _pitStopInfo;
        private double _previousTickLapDistance;

        public DriverTiming(DriverInfo driverInfo, SessionTiming session)
        {
            _refreshBestSectorIndicationWatch = Stopwatch.StartNew();
            _lapsInfo = new List<LapInfo>();
            _pitStopInfo = new List<PitStopInfo>();
            DriverInfo = driverInfo;
            Pace = TimeSpan.Zero;
            LapPercentage = 0;
            _previousTickLapDistance = 0;
            Session = session;
        }

        public event EventHandler<LapEventArgs> NewLapStarted;
        public event EventHandler<LapEventArgs> LapInvalidated;
        public event EventHandler<LapEventArgs> LapCompleted;

        public event EventHandler<LapInfo.SectorCompletedArgs> SectorCompletedEvent;

        public bool InvalidateFirstLap { get; set; }

        public SessionTiming Session { get; private set;}

        public DriverInfo DriverInfo { get; internal set; }

        public bool IsPlayer => DriverInfo.IsPlayer;

        public string Name => DriverInfo.DriverName;

        public int Position => DriverInfo.Position;

        public int PositionInClass => DriverInfo.PositionInClass;

        public int CompletedLaps => DriverInfo.CompletedLaps;

        public bool InPits { get; private set; }

        public TimeSpan Pace { get; private set; }

        public double TotalDistanceTraveled => DriverInfo.TotalDistance;

        public bool IsLapped => (Session.SessionType == SessionType.Race || CurrentLap == null) ? DriverInfo.IsBeingLappedByPlayer : !CurrentLap.Valid;

        public bool IsLapping => DriverInfo.IsLappingPlayer;

        public LapInfo BestLap { get; private set; }

        public int PitCount => _pitStopInfo.Count;

        public PitStopInfo LastPitStopStop => _pitStopInfo.Count != 0 ? _pitStopInfo[_pitStopInfo.Count - 1] : null;

        public double LapPercentage { get; private set; }

        public string CarName => DriverInfo.CarName;

        public string CarClassName => DriverInfo.CarClassName;

        public string CarClassId => DriverInfo.CarClassId;

        public int PaceLaps => Session.PaceLaps;

        public bool IsLastLapBestLap => BestLap != null && BestLap == LastLap;

        public bool IsActive { get; set; } = true;

        public SectorTiming BestSector1 { get; private set; }

        public SectorTiming BestSector2 { get; private set; }

        public SectorTiming BestSector3 { get; private set; }

        public bool IsLastSector1PersonalBest { get; private set; }
        public bool IsLastSector2PersonalBest { get; private set; }
        public bool IsLastSector3PersonalBest { get; private set; }

        public bool IsLastSector1SessionBest { get; private set; }
        public bool IsLastSector2SessionBest { get; private set; }
        public bool IsLastSector3SessionBest { get; private set; }

        public IReadOnlyCollection<LapInfo> Laps => _lapsInfo.AsReadOnly();

        public double DistanceToPlayer => DriverInfo.DistanceToPlayer;

        public LapInfo CurrentLap
        {
            get
            {
                if (_lapsInfo.Count == 0)
                {
                    return null;
                }

                return _lapsInfo.Last();
            }
        }

        public string LastPitInfo
        {
            get
            {
                if (DriverInfo.FinishStatus == DriverFinishStatus.Dnf)
                {
                    return string.Empty;
                }
                if (Session.SessionType != SessionType.Race)
                {
                    if (InPits)
                    {
                        return "In Pits";
                    }
                    else
                    {
                        return "Out";
                    }
                }

                if (LastPitStopStop == null)
                {
                    return "0";
                }
                else
                {
                    return PitCount + ":(" + LastPitStopStop.PitInfoFormatted + ")";
                }
            }
        }

        public LapInfo LastCompletedLap
        {
            get
            {
                return _lapsInfo.LastOrDefault(x => x.Completed && (x.Valid || Session.RetrieveAlsoInvalidLaps));
            }
        }

        public LapInfo LastLap => _lapsInfo.Count < 2 ? null : _lapsInfo[_lapsInfo.Count - 2];


        public bool IsLastLapBestSessionLap
        {
            get
            {
                if (LastLap == null)
                {
                    return false;
                }

                return LastLap == Session.BestSessionLap;
            }
        }

        public string Remark => DriverInfo.FinishStatus.ToString();

        public string Speed => DriverInfo.Speed.InKph.ToString("N0");

        public Velocity TopSpeed { get; private set; } = Velocity.Zero;



        public static DriverTiming FromModel(DriverInfo modelDriverInfo, SessionTiming session, bool invalidateFirstLap)
        {
            var driver = new DriverTiming(modelDriverInfo, session);
            driver.InvalidateFirstLap = invalidateFirstLap;
            return driver;
        }

        public bool UpdateLaps(SimulatorDataSet set)
        {
            SessionInfo sessionInfo = set.SessionInfo;
            if (!sessionInfo.IsActive)
            {
                return false;
            }

            if (sessionInfo.SessionPhase == SessionPhase.Countdown)
            {
                return false;
            }

            if (DriverInfo.FinishStatus != DriverFinishStatus.Na && DriverInfo.FinishStatus != DriverFinishStatus.None && CurrentLap != null && CurrentLap.Completed)
            {
                return false;
            }

            if (TopSpeed < DriverInfo.Speed && DriverInfo.Speed < _maximumVelocity )
            {
                TopSpeed = DriverInfo.Speed;
            }


            UpdateInPitsProperty(set);
            if (_lapsInfo.Count == 0)
            {
                LapInfo firstLap = new LapInfo(set, DriverInfo.CompletedLaps + 1, this, true, null);
                if (InvalidateFirstLap)
                {
                    firstLap.InvalidateLap(LapInvalidationReasonKind.InvalidatedFirstLap);
                }
                firstLap.SectorCompletedEvent += LapSectorCompletedEvent;
                firstLap.LapInvalidatedEvent += LapInvalidatedHandler;
                _lapsInfo.Add(firstLap);
                OnNewLapStarted(new LapEventArgs(firstLap));
            }

            if (LastLap != null && LastLap.IsPending && !LastLap.UpdatePendingState(set, DriverInfo))
            {
                FinishLap(LastLap, set);
            }

            LapInfo currentLap = CurrentLap;
            if (!currentLap.Completed)
            {
                UpdateCurrentLap(set);
            }

            if (_refreshBestSectorIndicationWatch.ElapsedMilliseconds > 2000)
            {
                UpdateBestSectorProperties();
                _refreshBestSectorIndicationWatch.Restart();
            }

            if (ShouldFinishLap(set, currentLap))
            {
                if (!currentLap.SwitchToPendingIfNecessary(set, DriverInfo))
                {
                    FinishLap(currentLap, set);
                }
                CreateNewLap(set, currentLap);
                _previousTickLapDistance = DriverInfo.LapDistance;
                return currentLap.Valid;
            }

            _previousTickLapDistance = DriverInfo.LapDistance;
            return false;
        }

        private void LapCompletedHandler(object sender, LapEventArgs e)
        {
            OnLapCompleted(e);
        }

        private void LapInvalidatedHandler(object sender, LapEventArgs e)
        {
            OnLapInvalidated(e);
        }


        private bool ShouldFinishLap(SimulatorDataSet dataSet, LapInfo currentLap)
        {
            SessionInfo sessionInfo = dataSet.SessionInfo;

            // Use completed laps indication to end lap, when we use the sim provided lap times. This gives us the biggest assurance that lap time is already properly set. But wait for lap to be at least 5 seconds in
            if (dataSet.SimulatorSourceInfo.HasLapTimeInformation && (currentLap.LapNumber < DriverInfo.CompletedLaps + 1))
            {
                currentLap.LapCompletionMethod = LapCompletionMethod.ByLapNumber;
                return true;
            }


            // Crossed line at out lap
            if (dataSet.SessionInfo.SessionType != SessionType.Race && currentLap.PitLap && (DriverInfo.LapDistance - _previousTickLapDistance < sessionInfo.TrackInfo.LayoutLength.InMeters * -0.90))
            {
                currentLap.LapCompletionMethod = LapCompletionMethod.ByCrossingTheLine;
                return true;
            }

            if ((!dataSet.SimulatorSourceInfo.HasLapTimeInformation || dataSet.SimulatorSourceInfo.SimNotReportingEndOfOutLapCorrectly) && (DriverInfo.LapDistance - _previousTickLapDistance < sessionInfo.TrackInfo.LayoutLength.InMeters * -0.90))
            {
                currentLap.LapCompletionMethod = LapCompletionMethod.ByCrossingTheLine;
                return true;
            }

            if (!dataSet.SimulatorSourceInfo.OutLapIsValid && !currentLap.Valid && DriverInfo.CurrentLapValid && DriverInfo.IsPlayer && (currentLap.FirstLap && !InvalidateFirstLap))
            {
                currentLap.LapCompletionMethod = LapCompletionMethod.ByChangingValidity;
                return true;
            }

            if (!dataSet.SimulatorSourceInfo.OutLapIsValid && !currentLap.Valid && DriverInfo.CurrentLapValid && DriverInfo.IsPlayer && currentLap.PitLap && _previousTickLapDistance < DriverInfo.LapDistance && SessionType.Race != sessionInfo.SessionType && !DriverInfo.InPits)
            {
                currentLap.LapCompletionMethod = LapCompletionMethod.ByChangingValidity2;
                return true;
            }

            if (!currentLap.Valid && DriverInfo.CurrentLapValid && SessionType.Race == sessionInfo.SessionType && !DriverInfo.IsPlayer && (currentLap.FirstLap && !InvalidateFirstLap))
            {
                currentLap.LapCompletionMethod = LapCompletionMethod.ByChangingValidity3;
                return true;
            }

            // Driver is DNF/DQ -> finish timed lap, and set it to invalid
            if (DriverInfo.FinishStatus != DriverFinishStatus.Na && DriverInfo.FinishStatus != DriverFinishStatus.None)
            {
                CurrentLap.InvalidateLap(LapInvalidationReasonKind.DriverDnf);
                return true;
            }

            return false;
        }

        private void UpdateCurrentLap(SimulatorDataSet dataSet)
        {
            CurrentLap.Tick(dataSet, DriverInfo);
            CurrentLap.InvalidBySim = !DriverInfo.CurrentLapValid;
            LapPercentage = (DriverInfo.LapDistance / dataSet.SessionInfo.TrackInfo.LayoutLength.InMeters) * 100;
            if (CurrentLap.Valid && SessionType.Race != dataSet.SessionInfo.SessionType && (InPits || !DriverInfo.CurrentLapValid) && _lapsInfo.Count >= 1)
            {
                CurrentLap.InvalidateLap(InPits ? LapInvalidationReasonKind.DriverInPits : LapInvalidationReasonKind.InvalidatedBySim);
            }
        }


        private void FinishLap(LapInfo lapToFinish, SimulatorDataSet dataSet)
        {
            if (lapToFinish.Completed)
            {
                return;
            }
            lapToFinish.FinishLap(dataSet, DriverInfo);
            lapToFinish.SectorCompletedEvent -= LapSectorCompletedEvent;
            lapToFinish.LapInvalidatedEvent -= LapInvalidatedHandler;

            if (lapToFinish.LapTime == TimeSpan.Zero)
            {
                lapToFinish.InvalidateLap(LapInvalidationReasonKind.NoValidLapTime);
                RevertSectorChanges(lapToFinish);
            }

            if (ShouldLapBeDiscarded(lapToFinish, dataSet))
            {
                _lapsInfo.Remove(lapToFinish);
                return;
            }

            if (lapToFinish.Valid && lapToFinish.LapTime != TimeSpan.Zero && (BestLap == null || lapToFinish.LapTime < BestLap.LapTime ))
            {
                BestLap = lapToFinish;
            }

            OnLapCompleted(new LapEventArgs(lapToFinish));
            Logger.Info($"Driver {DriverInfo.DriverName}, Lap {lapToFinish.LapNumber} finnished. REASON: {lapToFinish.LapCompletionMethod}");

            ComputePace();
            PurgeLapsTelemetry();
        }

        private void PurgeLapsTelemetry()
        {
            Laps.Where(x => x.Completed && !x.LapTelemetryInfo.IsPurged && x != BestLap && x.LapNumber != 1 && x != x.Driver.LastCompletedLap).Skip(MaxLapsWithTelemetry).ForEach(x => x.LapTelemetryInfo.Purge());
        }

        private bool ShouldLapBeDiscarded(LapInfo lap, SimulatorDataSet dataSet)
        {
            return !lap.IsLapDataSane(dataSet);
        }


        private void CreateNewLap(SimulatorDataSet dataSet, LapInfo lapToCreateFrom)
        {
            if ((DriverInfo.FinishStatus == DriverFinishStatus.Na
                || DriverInfo.FinishStatus == DriverFinishStatus.None) && dataSet.SessionInfo.SessionPhase != SessionPhase.Checkered  )
            {
                var newLap = new LapInfo(
                    dataSet,
                    DriverInfo.CompletedLaps + 1,
                    this,
                    lapToCreateFrom);
                newLap.SectorCompletedEvent += LapSectorCompletedEvent;
                newLap.LapInvalidatedEvent += LapInvalidatedHandler;
                _lapsInfo.Add(newLap);
                OnNewLapStarted(new LapEventArgs(newLap));
            }
        }

        private void LapSectorCompletedEvent(object sender, LapInfo.SectorCompletedArgs e)
        {
            SectorTiming completedSector = e.SectorTiming;
            if (!e.SectorTiming.Lap.Valid)
            {
                return;
            }

            switch (completedSector.SectorNumber)
            {
                case 1:
                    if ((BestSector1 == null || BestSector1.Duration > completedSector.Duration) && completedSector.Duration != TimeSpan.Zero)
                    {
                        BestSector1 = completedSector;
                    }
                    break;
                case 2:
                    if ((BestSector2 == null || BestSector2.Duration > completedSector.Duration)  && completedSector.Duration != TimeSpan.Zero)
                    {
                        BestSector2 = completedSector;
                    }
                    break;
                case 3:
                    if ((BestSector3 == null || BestSector3.Duration > completedSector.Duration)  && completedSector.Duration != TimeSpan.Zero)
                    {
                        BestSector3 = completedSector;
                    }
                    break;
            }
            OnSectorCompletedEvent(e);
            UpdateBestSectorProperties();
        }

        private void UpdateBestSectorProperties()
        {
            IsLastSector1PersonalBest = GetIsSector1PersonalBest();
            IsLastSector2PersonalBest = GetIsSector2PersonalBest();
            IsLastSector3PersonalBest = GetIsSector3PersonalBest();

            IsLastSector1SessionBest = GetIsSector1SessionBest();
            IsLastSector2SessionBest = GetIsSector2SessionBest();
            IsLastSector3SessionBest = GetIsSector3SessionBest();
        }

        private void UpdateInPitsProperty(SimulatorDataSet set)
        {
            if(InPits && !LastPitStopStop.Completed )
            {
                LastPitStopStop.Tick(set);
                if (CurrentLap != null)
                {
                    CurrentLap.PitLap = true;
                }
            }

            if (!InPits && DriverInfo.InPits)
            {
                InPits = true;
                if( CurrentLap != null)
                {
                    CurrentLap.PitLap = true;
                }

                _pitStopInfo.Add(new PitStopInfo(set, this, CurrentLap));
            }

            if(InPits && !DriverInfo.InPits)
            {
                InPits = false;
            }
        }


        private void ComputePace()
        {
            if(LastCompletedLap == null)
            {
                Pace = TimeSpan.Zero;
                return;
            }

            int totalPaceLaps = 0;
            TimeSpan pace = TimeSpan.Zero;
            for (int i = _lapsInfo.Count - 1; i >= 0 && totalPaceLaps < PaceLaps; i--)
            {
                LapInfo lap = _lapsInfo[i];
                if (!lap.Completed || (!lap.Valid && !Session.RetrieveAlsoInvalidLaps))
                {
                    continue;
                }

                pace = pace.Add(lap.LapTime);
                totalPaceLaps++;
            }

            Pace = totalPaceLaps == 0 ? TimeSpan.Zero : new TimeSpan(pace.Ticks / totalPaceLaps);
        }



        protected virtual void OnSectorCompletedEvent(LapInfo.SectorCompletedArgs e)
        {
            SectorCompletedEvent?.Invoke(this, e);
        }

        protected virtual void OnNewLapStarted(LapEventArgs e)
        {
            NewLapStarted?.Invoke(this, e);
        }

        protected virtual void OnLapInvalidated(LapEventArgs e)
        {
            RevertSectorChanges(e.Lap);
            LapInvalidated?.Invoke(this, e);
        }

        protected virtual void OnLapCompleted(LapEventArgs e)
        {
            LapCompleted?.Invoke(this, e);
        }

        private void RevertSectorChanges(LapInfo lap)
        {
            if (BestSector1 != null && BestSector1 == lap.Sector1)
            {
                BestSector1 = FindBestSector(LapInfo.Sector1SelFunc);
            }

            if (BestSector2 != null && BestSector2 == lap.Sector2)
            {
                BestSector2 = FindBestSector(LapInfo.Sector2SelFunc);
            }

            if (BestSector3 != null && BestSector3 == lap.Sector3)
            {
                BestSector3 = FindBestSector(LapInfo.Sector3SelFunc);
            }
        }

        private SectorTiming FindBestSector(Func<LapInfo, SectorTiming> sectorPickerFunc)
        {
            return Laps.Where(l => l.Valid).Select(sectorPickerFunc).Where(s => s != null && s.Duration != TimeSpan.Zero)
                .OrderBy(s => s.Duration).FirstOrDefault();
        }

        private bool GetIsSector1SessionBest()
        {
            if (CurrentLap == null)
            {
                return false;
            }

            var sector = GetSector1Timing();
            return sector != null && sector == Session.BestSector1;
        }

        private bool GetIsSector2SessionBest()
        {
            if (CurrentLap == null)
            {
                return false;
            }

            var sector = GetSector2Timing();
            return sector != null && sector == Session.BestSector2;
        }

        private bool GetIsSector3SessionBest()
        {
            if (CurrentLap == null)
            {
                return false;
            }

            var sector = GetSector3Timing();
            return sector != null && sector == Session.BestSector3;
        }

        private bool GetIsSector1PersonalBest()
        {
            if (CurrentLap == null)
            {
                return false;
            }

            var sector = GetSector1Timing();
            return sector != null && sector == BestSector1;
        }

        private bool GetIsSector2PersonalBest()
        {
            if (CurrentLap == null)
            {
                return false;
            }

            var sector = GetSector2Timing();
            return sector != null && sector == BestSector2;
        }

        private bool GetIsSector3PersonalBest()
        {
            if (CurrentLap == null)
            {
                return false;
            }

            var sector = GetSector3Timing();
            return sector != null && sector == BestSector3;
        }

        public SectorTiming GetSector1Timing()
        {
            if (CurrentLap == null)
            {
                return null;
            }
            SectorTiming sector = CurrentLap.Sector1;
            if (sector == null && CurrentLap.PreviousLap != null)
            {
                sector = CurrentLap.PreviousLap.Sector1;
            }
            return sector;
        }

        public SectorTiming GetSector2Timing()
        {
            if (CurrentLap == null)
            {
                return null;
            }
            SectorTiming sector = CurrentLap.Sector2;
            if (sector == null && CurrentLap.PreviousLap != null)
            {
                sector = CurrentLap.PreviousLap.Sector2;
            }
            return sector;
        }

        public SectorTiming GetSector3Timing()
        {
            if (CurrentLap == null)
            {
                return null;
            }
            SectorTiming sector = CurrentLap.Sector3;
            if (sector == null && CurrentLap.PreviousLap != null)
            {
                sector = CurrentLap.PreviousLap.Sector3;
            }
            return sector;
        }
    }
}
