using opensis.data.Interface;
using opensis.data.Models;
using opensis.report.report.data.Interface;
using opensis.report.report.data.ViewModels.AttendanceReport;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using opensis.data.ViewModels.CourseManager;
using opensis.data.Helper;

namespace opensis.report.report.data.Repository
{
    public class AttendanceReportRepository : IAttendanceReportRepository
    {
        private readonly CRMContext? context;
        private static readonly string NORECORDFOUND = "No Record Found";
        public AttendanceReportRepository(IDbContextFactory dbContextFactory)
        {
            this.context = dbContextFactory.Create();
        }


        /// <summary>
        /// Get Student Attendanced  Report
        /// </summary>
        /// <param name="pageResult"></param>
        /// <returns></returns>
        public StudentAttendanceReport GetStudentAttendanceReport(PageResult pageResult)
        {
            StudentAttendanceReport studentAttendanceList = new StudentAttendanceReport();
            studentAttendanceList.TenantId = pageResult.TenantId;
            studentAttendanceList.SchoolId = pageResult.SchoolId;
            studentAttendanceList._userName = pageResult._userName;
            studentAttendanceList.MarkingPeriodStartDate = pageResult.MarkingPeriodStartDate;
            studentAttendanceList.MarkingPeriodEndDate = pageResult.MarkingPeriodEndDate;
            studentAttendanceList._tenantName = pageResult._tenantName;
            studentAttendanceList._userName = pageResult._userName;
            IQueryable<StudendAttendanceViewModelForReport>? transactionIQ = null;
            List<StudentAttendanceViewForReport> studentAttendanceViewForReportList = new();
            List<StudendAttendanceViewModelForReport> attendanceData = new List<StudendAttendanceViewModelForReport>();
            try
            {
                var studentAttendanceData = this.context?.StudentAttendance.Include(s => s.BlockPeriod).Include(s => s.AttendanceCodeNavigation).Include(s => s.StudentCoursesectionSchedule).ThenInclude(s => s.StudentMaster).ThenInclude(s => s.StudentEnrollment).Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.AttendanceDate <= pageResult.MarkingPeriodEndDate).Select(x => new StudentAttendanceViewForReport()
                {

                    TenantId = x.TenantId,
                    SchoolId = x.SchoolId,
                    StudentId = x.StudentCoursesectionSchedule.StudentId,

                    StudentInternalId = x.StudentCoursesectionSchedule.StudentInternalId,
                    FirstGivenName = x.StudentCoursesectionSchedule.FirstGivenName,
                    MiddleName = x.StudentCoursesectionSchedule.MiddleName,
                    LastFamilyName = x.StudentCoursesectionSchedule.LastFamilyName,
                    GradeLevelTitle = x.StudentCoursesectionSchedule.StudentMaster.StudentEnrollment.Where(x => x.IsActive == true).Select(s => s.GradeLevelTitle).FirstOrDefault(),
                    AttendanceDate = x.AttendanceDate,
                    AttendanceCategoryId = x.AttendanceCategoryId,
                    AttendanceCode = x.AttendanceCode,
                    AttendanceCodeTitle = x.AttendanceCodeNavigation.StateCode,
                    BlockId = x.BlockId,
                    PeriodName = x.BlockPeriod.PeriodTitle,
                    PeriodId = x.PeriodId,
                    PeriodSortOrder = x.BlockPeriod.PeriodSortOrder,
                    CourseId = x.CourseId,
                    CourseSectionId = x.CourseSectionId
                }).ToList();

                var studentCourseSectionIds = this.context?.StudentMissingAttendances.Include(s => s.BlockPeriod).Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.MissingAttendanceDate >= pageResult.MarkingPeriodStartDate && x.MissingAttendanceDate <= pageResult.MarkingPeriodEndDate).Select(x => x.CourseSectionId).ToList();



                var studentMissingAttendanceList = this.context?.StudentCoursesectionSchedule.Include(s => s.StudentMaster).ThenInclude(s => s.StudentEnrollment).Join(this.context?.StudentMissingAttendances, scs => scs.CourseSectionId, sma => sma.CourseSectionId, (scs, sma) => new { scs, sma }).Join(this.context?.BlockPeriod, ms => ms.sma.PeriodId, bp => bp.PeriodId, (ms, bp) => new { ms, bp })
                    .Where(x => x.ms.scs.SchoolId == pageResult.SchoolId && x.ms.scs.TenantId == pageResult.TenantId && studentCourseSectionIds.Contains(x.ms.scs.CourseSectionId) && x.bp.TenantId == pageResult.TenantId && x.bp.SchoolId == pageResult.SchoolId && x.ms.sma.MissingAttendanceDate >= pageResult.MarkingPeriodStartDate && x.ms.sma.MissingAttendanceDate <= pageResult.MarkingPeriodEndDate)
                    .Select(x => new StudentAttendanceViewForReport()
                    {

                        TenantId = x.ms.sma.TenantId,
                        SchoolId = x.ms.sma.SchoolId,
                        StudentId = x.ms.scs.StudentId,
                        StudentInternalId = x.ms.scs.StudentInternalId,
                        FirstGivenName = x.ms.scs.FirstGivenName,
                        MiddleName = x.ms.scs.MiddleName,
                        LastFamilyName = x.ms.scs.LastFamilyName,
                        GradeLevelTitle = x.ms.scs.StudentMaster.StudentEnrollment.Where(x => x.IsActive == true).Select(s => s.GradeLevelTitle).FirstOrDefault(),
                        AttendanceDate = x.ms.sma.MissingAttendanceDate,
                        BlockId = x.ms.sma.BlockId,
                        PeriodId = x.ms.sma.PeriodId,
                        PeriodSortOrder = x.bp.PeriodSortOrder,
                        PeriodName = x.bp.PeriodTitle,
                        CourseId = x.ms.sma.CourseId,
                        CourseSectionId = x.ms.sma.CourseSectionId

                    }).ToList();

                var attendanceMissingData = studentAttendanceData.Concat(studentMissingAttendanceList);

                attendanceData = attendanceMissingData.GroupBy(c => new
                {
                    c.StudentId,
                    c.AttendanceDate
                }).Select(eg => new StudendAttendanceViewModelForReport()
                {
                    TenantId = eg.First().TenantId,
                    SchoolId = eg.First().SchoolId,
                    StudentId = eg.First().StudentId,
                    AttendanceDate = eg.First().AttendanceDate,
                    FirstGivenName = eg.First().FirstGivenName,
                    MiddleName = eg.First().MiddleName,
                    LastFamilyName = eg.First().LastFamilyName,
                    GradeLevelTitle = eg.First().GradeLevelTitle,
                    StudentInternalId = eg.First().StudentInternalId,
                    PeriodsName = string.Join(",", eg.Select(i => i.PeriodName + "|" + i.AttendanceCodeTitle))

                }).ToList();

                if (attendanceData.Count() > 0)
                {
                    if (pageResult.GradeLevel != null)
                    {
                        attendanceData = attendanceData.Where(x => x.GradeLevelTitle == pageResult.GradeLevel).ToList();
                    }
                    attendanceData = attendanceData.OrderBy(x => x.AttendanceDate).ToList();
                    if (pageResult.FilterParams == null || pageResult.FilterParams.Count == 0)
                    {
                        transactionIQ = attendanceData.AsQueryable();
                    }
                    else
                    {
                        string Columnvalue = pageResult.FilterParams.ElementAt(0).FilterValue;

                        if (pageResult.FilterParams != null && pageResult.FilterParams.ElementAt(0).ColumnName == null && pageResult.FilterParams.Count == 1)
                        {
                            transactionIQ = attendanceData.Where(x => x.FirstGivenName != null && x.FirstGivenName.ToLower().Contains(Columnvalue.ToLower()) || x.MiddleName != null && x.MiddleName.ToLower().Contains(Columnvalue.ToLower()) || x.LastFamilyName != null && x.LastFamilyName.ToLower().Contains(Columnvalue.ToLower()) || x.StudentInternalId != null && x.StudentInternalId.ToLower().Contains(Columnvalue.ToLower()) ||
                            x.GradeLevelTitle != null && x.GradeLevelTitle.ToLower().Contains(Columnvalue.ToLower())).AsQueryable();
                        }
                        else
                        {
                            transactionIQ = Utility.FilteredData(pageResult.FilterParams!, attendanceData).AsQueryable();
                        }
                    }

                    if (pageResult.SortingModel != null)
                    {
                        switch (pageResult.SortingModel.SortColumn!.ToLower())
                        {
                            default:
                                transactionIQ = Utility.Sort(transactionIQ, pageResult.SortingModel.SortColumn, pageResult.SortingModel.SortDirection!.ToLower());
                                break;
                        }
                    }

                    if (transactionIQ != null)
                    {
                        int? totalCount = transactionIQ.Count();
                        if (pageResult.PageNumber > 0 && pageResult.PageSize > 0)
                        {
                            transactionIQ = transactionIQ.Skip((pageResult.PageNumber - 1) * pageResult.PageSize).Take(pageResult.PageSize);
                            studentAttendanceList.PageNumber = pageResult.PageNumber;
                            studentAttendanceList._pageSize = pageResult.PageSize;
                        }
                        studentAttendanceList.studendAttendanceAdministrationList = transactionIQ.ToList();
                        studentAttendanceList.TotalCount = totalCount;
                    }
                    else
                    {
                        studentAttendanceList.TotalCount = 0;
                        studentAttendanceList._failure = true;
                        studentAttendanceList._message = NORECORDFOUND;
                    }
                }
                else
                {
                    studentAttendanceList._failure = true;
                    studentAttendanceList._message = NORECORDFOUND;
                }
            }
            catch (Exception es)
            {
                studentAttendanceList._failure = true;
                studentAttendanceList._message = es.Message;
            }
            return studentAttendanceList;

        }

        /// <summary>
        /// Get Student Attendanced Excel Report
        /// </summary>
        /// <param name="pageResult"></param>
        /// <returns></returns>
        public StudentAttendanceReport GetStudentAttendanceExcelReport(PageResult pageResult)
        {
            StudentAttendanceReport studentAttendanceList = new StudentAttendanceReport();
            studentAttendanceList.TenantId = pageResult.TenantId;
            studentAttendanceList.SchoolId = pageResult.SchoolId;
            studentAttendanceList._userName = pageResult._userName;
            studentAttendanceList.MarkingPeriodStartDate = pageResult.MarkingPeriodStartDate;
            studentAttendanceList.MarkingPeriodEndDate = pageResult.MarkingPeriodEndDate;
            studentAttendanceList._tenantName = pageResult._tenantName;
            studentAttendanceList._userName = pageResult._userName;
            List<StudendAttendanceViewModelForReport> attendanceData = new List<StudendAttendanceViewModelForReport>();
            try
            {
                var studentAttendanceData = this.context?.StudentAttendance.Include(x => x.BlockPeriod).Include(s => s.AttendanceCodeNavigation).Include(x => x.StudentCoursesectionSchedule).ThenInclude(x => x.StudentMaster).ThenInclude(x => x.StudentEnrollment).Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.AttendanceDate <= pageResult.MarkingPeriodEndDate).Select(x => new AttendanceExcelReport()
                {
                    StudentId = x.StudentId,
                    AttendanceDate = x.AttendanceDate,
                    StudentInternalId = x.StudentCoursesectionSchedule.StudentInternalId,
                    StudentName = x.StudentCoursesectionSchedule.StudentMaster.FirstGivenName + " " + (x.StudentCoursesectionSchedule.StudentMaster.MiddleName != null ? x.StudentCoursesectionSchedule.StudentMaster.MiddleName + " " : null) + x.StudentCoursesectionSchedule.StudentMaster.LastFamilyName,
                    PeriodName = x.BlockPeriod.PeriodTitle,
                    AttendanceCode = x.AttendanceCodeNavigation.Title,
                    GradeLevelTitle = x.StudentCoursesectionSchedule.StudentMaster.StudentEnrollment.Where(x => x.IsActive == true).Select(s => s.GradeLevelTitle).FirstOrDefault()
                }).ToList();

                var studentCourseSectionIds = this.context?.StudentMissingAttendances.Include(s => s.BlockPeriod).Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.MissingAttendanceDate >= pageResult.MarkingPeriodStartDate && x.MissingAttendanceDate <= pageResult.MarkingPeriodEndDate).Select(x => x.CourseSectionId).ToList();

                var studentMissingAttendanceList = this.context?.StudentCoursesectionSchedule.Include(s => s.StudentMaster).ThenInclude(s => s.StudentEnrollment).Join(this.context?.StudentMissingAttendances, scs => scs.CourseSectionId, sma => sma.CourseSectionId, (scs, sma) => new { scs, sma }).Join(this.context?.BlockPeriod, ms => ms.sma.PeriodId, bp => bp.PeriodId, (ms, bp) => new { ms, bp })
                    .Where(x => x.ms.scs.SchoolId == pageResult.SchoolId && x.ms.scs.TenantId == pageResult.TenantId && studentCourseSectionIds.Contains(x.ms.scs.CourseSectionId) && x.bp.TenantId == pageResult.TenantId && x.bp.SchoolId == pageResult.SchoolId && x.ms.sma.MissingAttendanceDate >= pageResult.MarkingPeriodStartDate && x.ms.sma.MissingAttendanceDate <= pageResult.MarkingPeriodEndDate)
                    .Select(x => new AttendanceExcelReport()
                    {
                        StudentId = x.ms.scs.StudentId,
                        AttendanceDate = x.ms.sma.MissingAttendanceDate,
                        StudentInternalId = x.ms.scs.StudentInternalId,
                        StudentName = x.ms.scs.FirstGivenName + " " + (x.ms.scs.MiddleName != null ? x.ms.scs.MiddleName + " " : null) + x.ms.scs.LastFamilyName,
                        PeriodName = x.bp.PeriodTitle,
                        AttendanceCode = "Not Taken",
                        GradeLevelTitle = x.ms.scs.StudentMaster.StudentEnrollment.Where(x => x.IsActive == true).Select(s => s.GradeLevelTitle).FirstOrDefault()
                    }).ToList();

                var studentAllAttendance = studentAttendanceData.Concat(studentMissingAttendanceList);

                if (studentAllAttendance != null && studentAllAttendance.Any())
                {
                    if (pageResult.GradeLevel != null)
                    {
                        studentAllAttendance = studentAllAttendance.Where(x => x.GradeLevelTitle == pageResult.GradeLevel);
                    }
                    studentAllAttendance = studentAllAttendance.OrderBy(x => x.AttendanceDate);
                    var attendancedExcelData = studentAllAttendance.ToPivotTable(
                item => item.PeriodName,
                item => new { item.AttendanceDate, item.StudentName, item.StudentInternalId, item.GradeLevelTitle },
                items => items.Any() ? items.First().AttendanceCode : null);
                    studentAttendanceList.StudentAttendanceReportForExcel = attendancedExcelData;
                }
            }
            catch (Exception es)
            {
                studentAttendanceList._failure = true;
                studentAttendanceList._message = es.Message;
            }
            return studentAttendanceList;

        }

        /// <summary>
        /// GetAverageDailyAttendanceReport
        /// </summary>
        /// <param name="pageResult"></param>
        /// <returns></returns>
        public AverageDailyAttendanceViewModel GetAverageDailyAttendanceReport(PageResult pageResult)
        {
            AverageDailyAttendanceViewModel averageDailyAttendance = new();
            List<DateTime> holidayList = new List<DateTime>();
            List<DateTime> missingAttendanceDateList = new List<DateTime>();
            List<DateTime> dateList = new List<DateTime>();
            List<int> courseSectionList = new List<int>();

            try
            {
                averageDailyAttendance.FromDate = pageResult.MarkingPeriodStartDate;
                averageDailyAttendance.ToDate = pageResult.MarkingPeriodEndDate;
                averageDailyAttendance.TenantId = pageResult.TenantId;
                averageDailyAttendance.SchoolId = pageResult.SchoolId;
                averageDailyAttendance.AcademicYear = pageResult.AcademicYear;
                averageDailyAttendance._tenantName = pageResult._tenantName;
                averageDailyAttendance._token = pageResult._token;

                var studentAttendanceData = this.context?.StudentAttendance.Include(a => a.AttendanceCodeNavigation).Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.AttendanceDate <= pageResult.MarkingPeriodEndDate);

                if (studentAttendanceData?.Any() == true)
                {
                    courseSectionList.AddRange(studentAttendanceData.Select(s => s.CourseSectionId));
                }

                var studentMissingAttendanceData = this.context?.StudentMissingAttendances.Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.MissingAttendanceDate >= pageResult.MarkingPeriodStartDate && x.MissingAttendanceDate <= pageResult.MarkingPeriodEndDate);

                if (studentMissingAttendanceData?.Any() == true)
                {
                    courseSectionList.AddRange(studentMissingAttendanceData.Select(s => (int)s.CourseSectionId));
                }

                var studentCourseSectionScheduleData = this.context?.StudentCoursesectionSchedule.Include(x => x.StudentMaster).ThenInclude(e => e.StudentEnrollment).Where(e => e.TenantId == pageResult.TenantId && e.SchoolId == pageResult.SchoolId && courseSectionList.Contains(e.CourseSectionId)).ToList();

                var gradeLevels = studentCourseSectionScheduleData.Select(d => d.StudentMaster.StudentEnrollment.FirstOrDefault(e => e.IsActive == true || e.ExitCode == "Transferred Out")).Select(s => s.GradeLevelTitle).Distinct().ToList();

                var calenderData = this.context?.SchoolCalendars.FirstOrDefault(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.AcademicYear == pageResult.AcademicYear && x.SessionCalendar == true);

                //fetch holidays
                var CalendarEventsData = this.context?.CalendarEvents.Where(e => e.TenantId == pageResult.TenantId && e.CalendarId == calenderData.CalenderId && e.IsHoliday == true && e.StartDate <= pageResult.MarkingPeriodEndDate && e.EndDate >= pageResult.MarkingPeriodStartDate && (e.SchoolId == pageResult.SchoolId || e.ApplicableToAllSchool == true)).ToList();

                if (CalendarEventsData != null && CalendarEventsData.Any())
                {
                    foreach (var calender in CalendarEventsData)
                    {
                        if (calender.EndDate!.Value.Date > calender.StartDate!.Value.Date)
                        {
                            var date = Enumerable.Range(0, 1 + (calender.EndDate.Value.Date - calender.StartDate.Value.Date).Days)
                               .Select(i => calender.StartDate.Value.Date.AddDays(i))
                               .ToList();
                            holidayList.AddRange(date);
                        }
                        holidayList.Add(calender.StartDate.Value.Date);
                    }
                }

                //fetch calender days & weekoff days
                List<char> daysValue = new List<char> { '0', '1', '2', '3', '4', '5', '6' };
                var calenderDays = calenderData.Days.ToCharArray();
                var WeekOffDays = daysValue.Except(calenderDays);
                var WeekOfflist = new List<string>();
                foreach (var WeekOffDay in WeekOffDays)
                {
                    Days days = new Days();
                    var Day = Enum.GetName(days.GetType(), Convert.ToInt32(WeekOffDay.ToString()));
                    WeekOfflist.Add(Day!);
                }

                //fetch all dates from user selected date range
                var allDates = Enumerable.Range(0, 1 + pageResult.MarkingPeriodEndDate.Value.Date.Subtract(pageResult.MarkingPeriodStartDate.Value.Date).Days).Select(d => pageResult.MarkingPeriodStartDate.Value.Date.AddDays(d)).ToList();

                //remove holidays & weekoffdays from 
                var wrokingDates = allDates.Where(s => !holidayList.Contains(s.Date) && !WeekOfflist.Contains(s.Date.DayOfWeek.ToString()) && (s.Date >= pageResult.MarkingPeriodStartDate && s.Date <= pageResult.MarkingPeriodEndDate)).ToList();

                foreach (var gradeLevel in gradeLevels)
                {
                    AverageDailyAttendanceReport averageDailyAttendanceReport = new AverageDailyAttendanceReport();

                    int totalPeriods = 0;
                    int present = 0;
                    int absent = 0;
                    int other = 0;
                    int studentCount = 0;
                    int notTaken = 0;
                    int missingPeriods = 0;

                    //var studentIds = studentCourseSectionScheduleData.Select(d => d.StudentMaster.StudentEnrollment.FirstOrDefault(e => e.GradeLevelTitle == gradeLevel)).Select(s => s.StudentId).Distinct().ToList();
                    var studentIds = studentCourseSectionScheduleData.Where(d => d.StudentMaster.StudentEnrollment.FirstOrDefault().GradeLevelTitle == gradeLevel).Select(s => s.StudentId).Distinct().ToList();

                    foreach (var studentId in studentIds)
                    {
                        var courseSectionIds = studentCourseSectionScheduleData.Where(x => x.StudentId == studentId && x.EffectiveStartDate <= pageResult.MarkingPeriodEndDate && x.EffectiveDropDate >= pageResult.MarkingPeriodStartDate).Select(x => x.CourseSectionId);

                        if (courseSectionIds?.Any() == true)
                        {
                            studentCount++;
                            foreach (var courseSectionId in courseSectionIds)
                            {
                                var studentAttendance = studentAttendanceData.Where(x => x.StudentId == studentId && x.CourseSectionId == courseSectionId);

                                if (studentAttendance?.Any() == true)
                                {
                                    totalPeriods += studentAttendance.Count();
                                    present += studentAttendance.Where(x => x.AttendanceCodeNavigation.StateCode.ToLower() == "present").Count();
                                    absent += studentAttendance.Where(x => x.AttendanceCodeNavigation.StateCode.ToLower() == "absent").Count();
                                    other += studentAttendance.Where(x => x.AttendanceCodeNavigation.StateCode.ToLower() != "present" && x.AttendanceCodeNavigation.StateCode.ToLower() != "absent").Count();
                                }

                                var studentMissingAttendance = studentMissingAttendanceData.Where(x => x.CourseSectionId == courseSectionId);

                                if (studentMissingAttendance?.Any() == true)
                                {
                                    missingPeriods += studentMissingAttendance.Count();
                                    notTaken = missingPeriods;
                                }
                            }
                        }
                    }

                    averageDailyAttendanceReport.GradeLevel = gradeLevel;
                    averageDailyAttendanceReport.Students = studentCount;
                    averageDailyAttendanceReport.DaysPossible = wrokingDates.Count;
                    averageDailyAttendanceReport.AttendancePossible = totalPeriods + missingPeriods;
                    averageDailyAttendanceReport.NotTaken = notTaken;
                    averageDailyAttendanceReport.Present = present;
                    averageDailyAttendanceReport.Absent = absent;
                    averageDailyAttendanceReport.Other = other;

                    if (averageDailyAttendanceReport.AttendancePossible > 0)
                    {
                        averageDailyAttendanceReport.ADA = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Present) / Convert.ToDecimal(averageDailyAttendanceReport.AttendancePossible) * 100, 2);
                        averageDailyAttendanceReport.AvgAttendance = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Present) / Convert.ToDecimal(averageDailyAttendanceReport.AttendancePossible), 2);
                        averageDailyAttendanceReport.AvgAbsent = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Absent) / Convert.ToDecimal(averageDailyAttendanceReport.AttendancePossible), 2);
                    }

                    averageDailyAttendance.averageDailyAttendanceReport.Add(averageDailyAttendanceReport);
                }

                if (pageResult.FilterParams != null && pageResult.FilterParams.ElementAt(0).ColumnName == null && pageResult.FilterParams.Count == 1)
                {
                    string searchValue = pageResult.FilterParams.ElementAt(0).FilterValue;
                    averageDailyAttendance.averageDailyAttendanceReport = averageDailyAttendance.averageDailyAttendanceReport.Where(x => x.GradeLevel.ToLower().Contains(searchValue.ToLower()) || x.Students.ToString() == searchValue || x.DaysPossible.ToString() == searchValue || x.Present.ToString() == searchValue || x.Absent.ToString() == searchValue || x.Other.ToString() == searchValue || x.ADA.ToString() == searchValue || x.AvgAttendance.ToString() == searchValue || x.AvgAbsent.ToString() == searchValue).ToList();
                }
            }
            catch (Exception es)
            {
                averageDailyAttendance._failure = true;
                averageDailyAttendance._message = es.Message;
            }
            return averageDailyAttendance;
        }

        /// <summary>
        /// GetAverageAttendancebyDayReport
        /// </summary>
        /// <param name="pageResult"></param>
        /// <returns></returns>
        public AverageDailyAttendanceViewModel GetAverageAttendancebyDayReport(PageResult pageResult)
        {
            AverageDailyAttendanceViewModel averageDailyAttendance = new();
            List<DateTime> missingAttendanceDateList = new List<DateTime>();
            List<DateTime> dateList = new List<DateTime>();
            List<int> courseSectionList = new List<int>();
            try
            {
                averageDailyAttendance.FromDate = pageResult.MarkingPeriodStartDate;
                averageDailyAttendance.ToDate = pageResult.MarkingPeriodEndDate;
                averageDailyAttendance.TenantId = pageResult.TenantId;
                averageDailyAttendance.SchoolId = pageResult.SchoolId;
                averageDailyAttendance.AcademicYear = pageResult.AcademicYear;
                averageDailyAttendance._tenantName = pageResult._tenantName;
                averageDailyAttendance._token = pageResult._token;
                averageDailyAttendance.PageNumber = pageResult.PageNumber;
                averageDailyAttendance._pageSize = pageResult.PageSize;

                var studentAttendanceData = this.context?.StudentAttendance.Include(a => a.AttendanceCodeNavigation).Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.AttendanceDate <= pageResult.MarkingPeriodEndDate);

                if (studentAttendanceData?.Any() == true)
                {
                    courseSectionList.AddRange(studentAttendanceData.Select(s => s.CourseSectionId));
                    dateList.AddRange(studentAttendanceData.Select(s => s.AttendanceDate));
                }

                var studentMissingAttendanceData = this.context?.StudentMissingAttendances.Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.MissingAttendanceDate >= pageResult.MarkingPeriodStartDate && x.MissingAttendanceDate <= pageResult.MarkingPeriodEndDate);

                if (studentMissingAttendanceData?.Any() == true)
                {
                    courseSectionList.AddRange(studentMissingAttendanceData.Select(s => (int)s.CourseSectionId));
                    dateList.AddRange(studentMissingAttendanceData.Select(s => (DateTime)s.MissingAttendanceDate));
                }

                var studentCourseSectionScheduleData = this.context?.StudentCoursesectionSchedule.Include(x => x.StudentMaster).ThenInclude(e => e.StudentEnrollment).Where(e => e.TenantId == pageResult.TenantId && e.SchoolId == pageResult.SchoolId && courseSectionList.Contains(e.CourseSectionId)).ToList();

                var gradeLevels = studentCourseSectionScheduleData.Select(d => d.StudentMaster.StudentEnrollment.FirstOrDefault(e => e.IsActive == true || e.ExitCode == "Transferred Out")).Select(s => s.GradeLevelTitle).Distinct().ToList();

                dateList = dateList.Distinct().ToList();

                foreach (var date in dateList)
                {
                    foreach (var gradeLevel in gradeLevels)
                    {
                        AverageDailyAttendanceReport averageDailyAttendanceReport = new AverageDailyAttendanceReport();

                        int totalPeriods = 0;
                        int present = 0;
                        int absent = 0;
                        int other = 0;
                        int studentCount = 0;
                        int notTaken = 0;
                        int missingPeriods = 0;

                        var studentIds = studentCourseSectionScheduleData.Where(d => d.StudentMaster.StudentEnrollment.FirstOrDefault().GradeLevelTitle == gradeLevel).Select(s => s.StudentId).Distinct().ToList();

                        foreach (var studentId in studentIds)
                        {
                            var courseSectionIds = studentCourseSectionScheduleData.Where(x => x.StudentId == studentId && x.EffectiveStartDate <= pageResult.MarkingPeriodEndDate && x.EffectiveDropDate >= pageResult.MarkingPeriodStartDate).Select(x => x.CourseSectionId);

                            if (courseSectionIds?.Any() == true)
                            {
                                var studentAttendance = studentAttendanceData.Where(x => x.AttendanceDate == date && x.StudentId == studentId && courseSectionIds.Contains(x.CourseSectionId));

                                if (studentAttendance?.Any() == true)
                                {
                                    totalPeriods += studentAttendance.Count();
                                    present += studentAttendance.Where(x => x.AttendanceCodeNavigation.StateCode.ToLower() == "present").Count();
                                    absent += studentAttendance.Where(x => x.AttendanceCodeNavigation.StateCode.ToLower() == "absent").Count();
                                    other += studentAttendance.Where(x => x.AttendanceCodeNavigation.StateCode.ToLower() != "present" && x.AttendanceCodeNavigation.StateCode.ToLower() != "absent").Count();
                                }

                                var studentMissingAttendance = studentMissingAttendanceData.Where(x => x.MissingAttendanceDate == date && courseSectionIds.Contains((int)x.CourseSectionId));

                                if (studentMissingAttendance?.Any() == true)
                                {
                                    missingPeriods += studentMissingAttendance.Count();
                                    notTaken = missingPeriods;
                                }

                                if (studentAttendance?.Any() == true || studentMissingAttendance?.Any() == true)
                                {
                                    studentCount++;
                                }
                            }
                        }

                        averageDailyAttendanceReport.Date = date;
                        averageDailyAttendanceReport.GradeLevel = gradeLevel;
                        averageDailyAttendanceReport.Students = studentCount;
                        averageDailyAttendanceReport.DaysPossible = 1;
                        averageDailyAttendanceReport.AttendancePossible = totalPeriods + missingPeriods;
                        averageDailyAttendanceReport.NotTaken = notTaken;
                        averageDailyAttendanceReport.Present = present;
                        averageDailyAttendanceReport.Absent = absent;
                        averageDailyAttendanceReport.Other = other;

                        if (averageDailyAttendanceReport.AttendancePossible > 0)
                        {
                            averageDailyAttendanceReport.ADA = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Present) / Convert.ToDecimal(averageDailyAttendanceReport.AttendancePossible) * 100, 2);
                            averageDailyAttendanceReport.AvgAttendance = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Present) / Convert.ToDecimal(averageDailyAttendanceReport.AttendancePossible), 2);
                            averageDailyAttendanceReport.AvgAbsent = Math.Round(Convert.ToDecimal(averageDailyAttendanceReport.Absent) / Convert.ToDecimal(averageDailyAttendanceReport.AttendancePossible), 2);
                        }

                        averageDailyAttendance.averageDailyAttendanceReport.Add(averageDailyAttendanceReport);
                    }
                }

                if (averageDailyAttendance.averageDailyAttendanceReport?.Any() == true)
                {
                    averageDailyAttendance.averageDailyAttendanceReport = averageDailyAttendance.averageDailyAttendanceReport.OrderBy(s => s.Date).ToList();

                    if (pageResult.FilterParams != null && pageResult.FilterParams.ElementAt(0).ColumnName == null && pageResult.FilterParams.Count == 1)
                    {
                        string searchValue = pageResult.FilterParams.ElementAt(0).FilterValue;
                        averageDailyAttendance.averageDailyAttendanceReport = averageDailyAttendance.averageDailyAttendanceReport.Where(x => x.GradeLevel.ToLower().Contains(searchValue.ToLower()) || x.Students.ToString() == searchValue || x.DaysPossible.ToString() == searchValue || x.Present.ToString() == searchValue || x.Absent.ToString() == searchValue || x.Other.ToString() == searchValue || x.ADA.ToString() == searchValue || x.AvgAttendance.ToString() == searchValue || x.AvgAbsent.ToString() == searchValue).ToList();
                    }

                    int totalCount = averageDailyAttendance.averageDailyAttendanceReport.Count;
                    if (pageResult.PageNumber > 0 && pageResult.PageSize > 0)
                    {
                        averageDailyAttendance.averageDailyAttendanceReport = averageDailyAttendance.averageDailyAttendanceReport.Skip((pageResult.PageNumber - 1) * pageResult.PageSize).Take(pageResult.PageSize).ToList();
                    }
                    averageDailyAttendance.TotalCount = totalCount;
                }
                else
                {
                    averageDailyAttendance._failure = true;
                    averageDailyAttendance._message = NORECORDFOUND;
                }
            }
            catch (Exception es)
            {
                averageDailyAttendance._failure = true;
                averageDailyAttendance._message = es.Message;
            }
            return averageDailyAttendance;
        }


        /// <summary>
        /// Get All Student Attendance List Administration
        /// </summary>
        /// <param name="pageResult"></param>
        /// <returns></returns>
        public StudentListForAbsenceSummary GetAllStudentAbsenceList(PageResult pageResult)
        {
            StudentListForAbsenceSummary studentAbsenceList = new StudentListForAbsenceSummary();
            studentAbsenceList.TenantId = pageResult.TenantId;
            studentAbsenceList.SchoolId = pageResult.SchoolId;
            studentAbsenceList._userName = pageResult._userName;
            studentAbsenceList._tenantName = pageResult._tenantName;
            studentAbsenceList._userName = pageResult._userName;
            IQueryable<StudentListView>? transactionIQ = null;
            IQueryable<StudentListForAbsence>? attendanceIQ = null;
            List<StudentListForAbsence> attendanceData = new List<StudentListForAbsence>();
            try
            {
                if (pageResult.PeriodId != null)
                {
                    var studentAbsence = this.context?.StudentAttendance.Include(s => s.BlockPeriod).Include(s => s.AttendanceCodeNavigation).Join(this.context?.StudentListView, sa => sa.StudentId, slv => slv.StudentId, (sa, slv) => new { sa, slv }).Where(x => x.sa.SchoolId == pageResult.SchoolId && x.sa.TenantId == pageResult.TenantId && x.sa.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.sa.AttendanceDate <= pageResult.MarkingPeriodEndDate && x.sa.PeriodId == pageResult.PeriodId && x.sa.BlockPeriod.SchoolId == pageResult.SchoolId && x.sa.BlockPeriod.TenantId == pageResult.TenantId && (x.sa.AttendanceCodeNavigation.StateCode == "Half Day" || x.sa.AttendanceCodeNavigation.StateCode == "Absent") && x.slv.SchoolId == pageResult.SchoolId && x.slv.TenantId == pageResult.TenantId);

                    if (studentAbsence != null && studentAbsence.Any())
                    {
                        if (pageResult.FilterParams == null || pageResult.FilterParams.Count == 0)
                        {
                            transactionIQ = studentAbsence.Select(x => x.slv).AsQueryable();
                        }
                        else
                        {
                            string Columnvalue = pageResult.FilterParams.ElementAt(0).FilterValue;

                            if (pageResult.FilterParams != null && pageResult.FilterParams.ElementAt(0).ColumnName == null && pageResult.FilterParams.Count == 1)
                            {
                                transactionIQ = studentAbsence.Select(x => x.slv).Where(x => x.FirstGivenName != null && x.FirstGivenName.ToLower().Contains(Columnvalue.ToLower()) || x.MiddleName != null && x.MiddleName.ToLower().Contains(Columnvalue.ToLower()) || x.LastFamilyName != null && x.LastFamilyName.ToLower().Contains(Columnvalue.ToLower()) || x.StudentInternalId != null && x.StudentInternalId.ToLower().Contains(Columnvalue.ToLower()) ||
                                x.GradeLevelTitle != null && x.GradeLevelTitle.ToLower().Contains(Columnvalue.ToLower()) ||
                                x.HomePhone != null && x.HomePhone.ToLower().Contains(Columnvalue.ToLower())).AsQueryable();
                            }
                            else
                            {
                                transactionIQ = Utility.FilteredData(pageResult.FilterParams!, studentAbsence.Select(x => x.slv)).AsQueryable();

                                //medical advance search
                                var studentGuids = transactionIQ.Select(s => s.StudentGuid).ToList();
                                if (studentGuids.Count > 0)
                                {
                                    var filterStudentIds = Utility.MedicalAdvancedSearch(this.context!, pageResult.FilterParams!, pageResult.TenantId, pageResult.SchoolId, studentGuids);

                                    if (filterStudentIds?.Count > 0)
                                    {
                                        transactionIQ = transactionIQ.Where(x => filterStudentIds.Contains(x.StudentGuid));
                                    }
                                    else
                                    {
                                        transactionIQ = null;
                                    }
                                }
                            }
                        }
                    }


                    if (transactionIQ != null && transactionIQ.Any())
                    {
                        var studentIds = transactionIQ.Select(a => a.StudentId).Distinct().ToList();
                        var blockId = studentAbsence.FirstOrDefault().sa.BlockId;
                        foreach (var ide in studentIds)
                        {
                            StudentListForAbsence studentListForAbsence = new();
                            var studentAttendanceData = studentAbsence.Where(x => x.sa.TenantId == pageResult.TenantId && x.sa.SchoolId == pageResult.SchoolId && x.sa.StudentId == ide).ToList();

                            studentListForAbsence.TenantId = studentAttendanceData.FirstOrDefault().sa.TenantId;
                            studentListForAbsence.SchoolId = studentAttendanceData.FirstOrDefault().sa.SchoolId;
                            studentListForAbsence.StudentId = studentAttendanceData.FirstOrDefault().sa.StudentId;
                            studentListForAbsence.StudentInternalId = studentAttendanceData.FirstOrDefault().slv?.StudentInternalId;
                            studentListForAbsence.StudentGuid = studentAttendanceData.FirstOrDefault().slv?.StudentGuid;
                            studentListForAbsence.FirstGivenName = studentAttendanceData.FirstOrDefault().slv?.FirstGivenName;
                            studentListForAbsence.MiddleName = studentAttendanceData.FirstOrDefault().slv?.MiddleName;
                            studentListForAbsence.LastFamilyName = studentAttendanceData.FirstOrDefault().slv?.LastFamilyName;
                            studentListForAbsence.AbsentCount = studentAttendanceData.Where(x => x.sa.AttendanceCodeNavigation?.StateCode == "Absent").Count();
                            studentListForAbsence.HalfDayCount = studentAttendanceData.Where(x => x.sa.AttendanceCodeNavigation?.StateCode == "Half Day").Count();
                            studentListForAbsence.GradeLevelTitle = studentAttendanceData.FirstOrDefault().slv.GradeLevelTitle;
                            studentListForAbsence.HomePhone = studentAttendanceData.FirstOrDefault().slv?.HomePhone;

                            attendanceData.Add(studentListForAbsence);


                        }
                    }
                }
                else
                {
                    var studentAbsence = this.context?.StudentDailyAttendance.Join(this.context?.StudentAttendance, sda => sda.AttendanceDate, sa => sa.AttendanceDate, (sda, sa) => new { sda, sa }).Join(this.context?.StudentListView, ssa => ssa.sda.StudentId, slv => slv.StudentId, (ssa, slv) => new { ssa, slv }).Where(x => x.ssa.sda.SchoolId == pageResult.SchoolId && x.ssa.sda.TenantId == pageResult.TenantId && x.ssa.sa.SchoolId == pageResult.SchoolId && x.ssa.sa.TenantId == pageResult.TenantId && x.ssa.sda.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.ssa.sda.AttendanceDate <= pageResult.MarkingPeriodEndDate && x.slv.SchoolId == pageResult.SchoolId && x.slv.TenantId == pageResult.TenantId);


                    if (studentAbsence != null && studentAbsence.Any())
                    {
                        if (pageResult.FilterParams == null || pageResult.FilterParams.Count == 0)
                        {
                            transactionIQ = studentAbsence.Select(x => x.slv).AsQueryable();
                        }
                        else
                        {
                            string Columnvalue = pageResult.FilterParams.ElementAt(0).FilterValue;

                            if (pageResult.FilterParams != null && pageResult.FilterParams.ElementAt(0).ColumnName == null && pageResult.FilterParams.Count == 1)
                            {
                                transactionIQ = studentAbsence.Select(x => x.slv).Where(x => x.FirstGivenName != null && x.FirstGivenName.ToLower().Contains(Columnvalue.ToLower()) || x.MiddleName != null && x.MiddleName.ToLower().Contains(Columnvalue.ToLower()) || x.LastFamilyName != null && x.LastFamilyName.ToLower().Contains(Columnvalue.ToLower()) || x.StudentInternalId != null && x.StudentInternalId.ToLower().Contains(Columnvalue.ToLower()) ||
                                x.GradeLevelTitle != null && x.GradeLevelTitle.ToLower().Contains(Columnvalue.ToLower()) ||
                                x.HomePhone != null && x.HomePhone.ToLower().Contains(Columnvalue.ToLower())).AsQueryable();
                            }
                            else
                            {
                                transactionIQ = Utility.FilteredData(pageResult.FilterParams!, studentAbsence.Select(x => x.slv)).AsQueryable();
                            }
                        }
                    }

                    if (transactionIQ != null && transactionIQ.Any())
                    {
                        var studentIds = transactionIQ.Select(a => a.StudentId).Distinct().ToList();
                        var blockId = studentAbsence.FirstOrDefault()!.ssa.sa.BlockId;
                        foreach (var ide in studentIds)
                        {
                            int halfdayCount = 0;
                            int absentCount = 0;
                            StudentListForAbsence studentListForAbsence = new();
                            var studentDailyAttendanceData = this.context?.StudentDailyAttendance.Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.StudentId == ide && x.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.AttendanceDate <= pageResult.MarkingPeriodEndDate).ToList();
                            var studentAttendance = transactionIQ.Where(x => x.StudentId == ide).FirstOrDefault();

                            foreach (var attendance in studentDailyAttendanceData)
                            {
                                var blockData = this.context?.Block.FirstOrDefault(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.BlockId == blockId);
                                if (attendance.AttendanceMinutes >= blockData?.HalfDayMinutes && attendance.AttendanceMinutes < blockData?.FullDayMinutes)
                                {
                                    halfdayCount += 1;
                                }
                                if (attendance.AttendanceMinutes < blockData?.HalfDayMinutes)
                                {
                                    absentCount += 1;
                                }
                            }
                            studentListForAbsence.TenantId = studentAttendance.TenantId;
                            studentListForAbsence.SchoolId = studentAttendance.SchoolId;
                            studentListForAbsence.StudentId = studentAttendance.StudentId;
                            studentListForAbsence.StudentInternalId = studentAttendance?.StudentInternalId;
                            studentListForAbsence.StudentGuid = studentAttendance?.StudentGuid;
                            studentListForAbsence.FirstGivenName = studentAttendance?.FirstGivenName;
                            studentListForAbsence.MiddleName = studentAttendance?.MiddleName;
                            studentListForAbsence.LastFamilyName = studentAttendance?.LastFamilyName;
                            studentListForAbsence.AbsentCount = absentCount;
                            studentListForAbsence.HalfDayCount = halfdayCount;
                            studentListForAbsence.GradeLevelTitle = studentAttendance?.GradeLevelTitle;
                            studentListForAbsence.HomePhone = studentAttendance?.HomePhone;

                            attendanceData.Add(studentListForAbsence);
                        }
                    }
                }


                if (attendanceData.Count() > 0)
                {
                    attendanceIQ = attendanceData.AsQueryable();

                    if (attendanceIQ != null)
                    {
                        int? totalCount = attendanceIQ.Count();
                        if (pageResult.PageNumber > 0 && pageResult.PageSize > 0)
                        {
                            attendanceIQ = attendanceIQ.Skip((pageResult.PageNumber - 1) * pageResult.PageSize).Take(pageResult.PageSize);
                            studentAbsenceList.PageNumber = pageResult.PageNumber;
                            studentAbsenceList._pageSize = pageResult.PageSize;
                        }
                        studentAbsenceList.studendAttendanceList = attendanceIQ.ToList();
                        studentAbsenceList.TotalCount = totalCount;
                    }
                    else
                    {
                        studentAbsenceList.TotalCount = 0;
                    }
                }
                else
                {
                    studentAbsenceList._failure = true;
                    studentAbsenceList._message = NORECORDFOUND;
                }
            }
            catch (Exception ex)
            {
                studentAbsenceList._failure = true;
                studentAbsenceList._message = ex.Message;
            }

            return studentAbsenceList;
        }


        /// <summary>
        /// Get Attendance by Student Id
        /// </summary>
        /// <param name="pageResult"></param>
        /// <returns></returns>
        public AbsenceListByStudent GetAbsenceListByStudent(PageResult pageResult)
        {
            AbsenceListByStudent studentAbsenceList = new();
            studentAbsenceList.TenantId = pageResult.TenantId;
            studentAbsenceList.SchoolId = pageResult.SchoolId;
            studentAbsenceList._userName = pageResult._userName;
            studentAbsenceList._tenantName = pageResult._tenantName;
            studentAbsenceList._userName = pageResult._userName;
            IQueryable<AbsenceStudentModel>? transactionIQ = null;
            List<AbsenceStudentModel> attendanceStudentList = new List<AbsenceStudentModel>();
            try
            {
                var teacherMembershipIds = this.context?.Membership.Where(x => x.ProfileType == "Teacher" || x.ProfileType == "Homeroom Teacher").Select(x => x.MembershipId).ToList();
                var adminMembershipIds = this.context?.Membership.Where(x => x.ProfileType == "Super Administrator" || x.ProfileType == "School Administrator" || x.ProfileType == "Admin Assistant").Select(x => x.MembershipId).ToList();


                if (pageResult.PeriodId != null)
                {
                    var studentAbsence = this.context?.StudentAttendance.Include(s => s.StudentAttendanceComments).ThenInclude(s => s.Membership).Include(s => s.BlockPeriod).Include(s => s.AttendanceCodeNavigation).Include(s => s.StudentCoursesectionSchedule).ThenInclude(s => s.StudentMaster).ThenInclude(s => s.StudentEnrollment).Include(s => s.StudentCoursesectionSchedule.StudentMaster.Sections).Where(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.AttendanceDate <= pageResult.MarkingPeriodEndDate && x.StudentId == pageResult.StudentId && x.PeriodId == pageResult.PeriodId && (x.AttendanceCodeNavigation.StateCode == "Half Day" || x.AttendanceCodeNavigation.StateCode == "Absent")).ToList();

                    if (studentAbsence != null && studentAbsence.Any())
                    {
                        studentAbsenceList.StudentPhoto = studentAbsence.FirstOrDefault().StudentCoursesectionSchedule.StudentMaster.StudentPhoto;
                        studentAbsenceList.FirstGivenName = studentAbsence.FirstOrDefault().StudentCoursesectionSchedule.StudentMaster.FirstGivenName;
                        studentAbsenceList.MiddleName = studentAbsence.FirstOrDefault().StudentCoursesectionSchedule.StudentMaster.MiddleName;
                        studentAbsenceList.LastFamilyName = studentAbsence.FirstOrDefault().StudentCoursesectionSchedule.StudentMaster.LastFamilyName;
                        studentAbsenceList.StudentInternalId = studentAbsence.FirstOrDefault().StudentCoursesectionSchedule.StudentMaster.StudentInternalId;
                        studentAbsenceList.GradeLevelTitle = studentAbsence.FirstOrDefault().StudentCoursesectionSchedule.StudentMaster.StudentEnrollment.Where(x => x.IsActive == true).Select(s => s.GradeLevelTitle).FirstOrDefault();
                        var attendanceDates = studentAbsence.Select(a => a.AttendanceDate).Distinct().ToList();
                        var blockId = studentAbsence.FirstOrDefault().BlockId;
                        foreach (var date in attendanceDates)
                        {
                            AbsenceStudentModel absenceStudentModel = new();
                            var studentDailyAttendanceData = studentAbsence.FirstOrDefault(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.StudentId == pageResult.StudentId && x.AttendanceDate == date);

                            absenceStudentModel.Attendance = studentDailyAttendanceData.AttendanceCodeNavigation?.StateCode;
                            absenceStudentModel.AbsenceDate = date;
                            absenceStudentModel.TeacherComment = studentDailyAttendanceData?.StudentAttendanceComments.Where(x => teacherMembershipIds.Contains(x.MembershipId.Value))?.FirstOrDefault()?.Comment;
                            absenceStudentModel.AdminComment = studentDailyAttendanceData?.StudentAttendanceComments.Where(x => adminMembershipIds.Contains(x.MembershipId.Value))?.FirstOrDefault()?.Comment;

                            attendanceStudentList.Add(absenceStudentModel);
                        }
                    }
                }
                else
                {
                    var studentAbsence = this.context?.StudentDailyAttendance.Include(s => s.StudentMaster).ThenInclude(s => s.StudentEnrollment).Join(this.context?.StudentAttendance, sda => sda.AttendanceDate, sa => sa.AttendanceDate, (sda, sa) => new { sda, sa }).Join(this.context?.StudentAttendanceComments, sa => sa.sa.StudentAttendanceId, sac => sac.StudentAttendanceId, (sa, sac) => new { sa, sac }).Where(x => x.sa.sda.SchoolId == pageResult.SchoolId && x.sa.sda.TenantId == pageResult.TenantId && x.sa.sa.SchoolId == pageResult.SchoolId && x.sa.sa.TenantId == pageResult.TenantId && x.sa.sda.AttendanceDate >= pageResult.MarkingPeriodStartDate && x.sa.sda.AttendanceDate <= pageResult.MarkingPeriodEndDate && x.sa.sda.StudentId == pageResult.StudentId);


                    if (studentAbsence != null && studentAbsence.Any())
                    {
                        studentAbsenceList.StudentPhoto = studentAbsence.FirstOrDefault().sa.sda.StudentMaster.StudentPhoto;
                        studentAbsenceList.FirstGivenName = studentAbsence.FirstOrDefault().sa.sda.StudentMaster.FirstGivenName;
                        studentAbsenceList.MiddleName = studentAbsence.FirstOrDefault().sa.sda.StudentMaster.MiddleName;
                        studentAbsenceList.LastFamilyName = studentAbsence.FirstOrDefault().sa.sda.StudentMaster.LastFamilyName;
                        studentAbsenceList.StudentInternalId = studentAbsence.FirstOrDefault().sa.sda.StudentMaster.StudentInternalId;
                        studentAbsenceList.GradeLevelTitle = studentAbsence.FirstOrDefault().sa.sda.StudentMaster.StudentEnrollment.Where(x => x.IsActive == true).Select(s => s.GradeLevelTitle).FirstOrDefault();
                        var attendanceDates = studentAbsence.Select(a => a.sa.sda.AttendanceDate).Distinct().ToList();
                        var blockId = studentAbsence.FirstOrDefault()!.sa.sa.BlockId;
                        foreach (var date in attendanceDates)
                        {
                            AbsenceStudentModel absenceStudentModel = new();
                            var studentDailyAttendanceData = this.context?.StudentDailyAttendance.FirstOrDefault(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.StudentId == pageResult.StudentId && x.AttendanceDate == date);

                            if (studentDailyAttendanceData != null)
                            {
                                var blockData = this.context?.Block.FirstOrDefault(x => x.TenantId == pageResult.TenantId && x.SchoolId == pageResult.SchoolId && x.BlockId == blockId);

                                if (studentDailyAttendanceData.AttendanceMinutes >= blockData?.HalfDayMinutes && studentDailyAttendanceData.AttendanceMinutes < blockData?.FullDayMinutes)
                                {
                                    absenceStudentModel.Attendance = "Half-Day";
                                }
                                if (studentDailyAttendanceData.AttendanceMinutes < blockData?.HalfDayMinutes)
                                {
                                    absenceStudentModel.Attendance = "Absent";
                                }
                            }
                            absenceStudentModel.AbsenceDate = date;
                            absenceStudentModel.TeacherComment = null; /// daily attendance cannot have teacher comment
                            absenceStudentModel.AdminComment = studentDailyAttendanceData.AttendanceComment;
                            attendanceStudentList.Add(absenceStudentModel);
                        }
                    }
                }


                if (attendanceStudentList.Count() > 0)
                {
                    attendanceStudentList = attendanceStudentList.OrderBy(x => x.AbsenceDate).ToList();

                    if (pageResult.FilterParams == null || pageResult.FilterParams.Count == 0)
                    {
                        transactionIQ = attendanceStudentList.AsQueryable();
                    }
                    if (transactionIQ != null)
                    {
                        int? totalCount = transactionIQ.Count();
                        if (pageResult.PageNumber > 0 && pageResult.PageSize > 0)
                        {
                            transactionIQ = transactionIQ.Skip((pageResult.PageNumber - 1) * pageResult.PageSize).Take(pageResult.PageSize);
                            studentAbsenceList.PageNumber = pageResult.PageNumber;
                            studentAbsenceList._pageSize = pageResult.PageSize;
                        }
                        studentAbsenceList.studendList = transactionIQ.ToList();
                        studentAbsenceList.TotalCount = totalCount;
                    }
                    else
                    {
                        studentAbsenceList.TotalCount = 0;
                    }
                }
                else
                {
                    studentAbsenceList._failure = true;
                    studentAbsenceList._message = NORECORDFOUND;
                }


            }
            catch (Exception ex)
            {
                studentAbsenceList._failure = true;
                studentAbsenceList._message = ex.Message;
            }

            return studentAbsenceList;
        }


    }
}
