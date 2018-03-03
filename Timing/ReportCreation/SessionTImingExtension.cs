﻿namespace SecondMonitor.Timing.ReportCreation
{
    using System;
    using System.Linq;

    using SecondMonitor.DataModel.Summary;
    using SecondMonitor.Timing.SessionTiming.Drivers.ModelView;
    using SecondMonitor.Timing.SessionTiming.ViewModel;

    public static class SessionTimingExtension
    {
        public static SessionSummary ToSessionSummary(this SessionTiming timing)
        {
            SessionSummary summary = new SessionSummary();
            FillSessionInfo(summary, timing);
            AddDrivers(summary, timing);
            return summary;
        }

        private static void FillSessionInfo(SessionSummary summary, SessionTiming timing)
        {
            summary.SessionType = timing.SessionType;
            summary.TrackInfo = timing.LastSet.SessionInfo.TrackInfo;
            summary.Simulator = timing.LastSet.Source;
            summary.SessionLength = TimeSpan.FromSeconds(timing.TotalSessionLength);
            summary.SessionLengthType = timing.LastSet.SessionInfo.SessionLengthType;
            summary.TotalNumberOfLaps = timing.LastSet.SessionInfo.TotalNumberOfLaps;
        }

        private static void AddDrivers(SessionSummary summary, SessionTiming timing)
        {
           summary.Drivers.AddRange(timing.Drivers.Select(d => ConvertToSummaryDriver(d.Value.DriverTiming)));
        }

        private static Driver ConvertToSummaryDriver(DriverTiming driverTiming)
        {
            Driver driverSummary = new Driver()
                                       {
                                           DriverName = driverTiming.Name,
                                           CarName = driverTiming.CarName,
                                           Finished = driverTiming.IsActive,
                                           FinishingPosition = driverTiming.Position,
                                           TopSpeed = driverTiming.TopSpeed,
                                           
                                       };
            driverSummary.Laps.AddRange(driverTiming.Laps.Where(l => l.Completed).Select(l => ConvertToSummaryLap(driverSummary, l)));
            driverSummary.TotalLaps = driverSummary.Laps.Count;
            return driverSummary;
        }

        private static Lap ConvertToSummaryLap(Driver summaryDriver,  LapInfo lapInfo)
        {
            Lap summaryLap = new Lap(summaryDriver)
                                 {
                                     LapNumber = lapInfo.LapNumber,
                                     LapTime = lapInfo.LapTime,
                                     Sector1 = lapInfo.Sector1?.Duration ?? TimeSpan.Zero,
                                     Sector2 = lapInfo.Sector2?.Duration ?? TimeSpan.Zero,
                                     Sector3 = lapInfo.Sector3?.Duration ?? TimeSpan.Zero
                                 };
            return summaryLap;
        }
    }
}