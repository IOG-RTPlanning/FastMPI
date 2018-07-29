using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows;



namespace VMS.TPS
{

    // lets to check wether provided value of seights for structures is from 0.0 to 1.0
    public class MyValidationRule : ValidationRule
    {
        public double Min { get; set; }
        public double Max { get; set; }

        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            ValidationResult vResult = ValidationResult.ValidResult;
            double parameter = 0;

            try
            {
                if (((string)value).Length > 0) //Check if there is a input in the textbox
                {
                    parameter = Double.Parse((String)value);
                }
            }

            catch (Exception e)
            {
                return new ValidationResult(false, "Illegal characters or " + e.Message);
            }

            if ((parameter < this.Min) || (parameter > this.Max))
            {
                return new ValidationResult(false, "Please enter value in the range: " + this.Min + " - " + this.Max + ".");
            }
            return vResult;
        }

    }


    public static class DvhExtensions
    {
        public static DoseValue GetDoseAtVolume(this PlanningItem pitem, Structure structure, double volume, VolumePresentation volumePresentation, DoseValuePresentation requestedDosePresentation)
        {
            if (pitem is PlanSetup)
            {
                return ((PlanSetup)pitem).GetDoseAtVolume(structure, volume, volumePresentation, requestedDosePresentation);
            }
            else
            {
                if (requestedDosePresentation != DoseValuePresentation.Absolute)
                    throw new ApplicationException("Only absolute dose supported for Plan Sums");
                DVHData dvh = pitem.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, volumePresentation, 0.001);
                return DvhExtensions.DoseAtVolume(dvh, volume);
            }
        }
        public static double GetVolumeAtDose(this PlanningItem pitem, Structure structure, DoseValue dose, VolumePresentation requestedVolumePresentation)
        {
            if (pitem is PlanSetup)
            {
                return ((PlanSetup)pitem).GetVolumeAtDose(structure, dose, requestedVolumePresentation);
            }
            else
            {
                DVHData dvh = pitem.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, requestedVolumePresentation, 0.001);
                return DvhExtensions.VolumeAtDose(dvh, dose.Dose);
            }
        }

        public static DoseValue DoseAtVolume(DVHData dvhData, double volume)
        {
            if (dvhData == null || dvhData.CurveData.Count() == 0)
                return DoseValue.UndefinedDose();
            double absVolume = dvhData.CurveData[0].VolumeUnit == "%" ? volume * dvhData.Volume * 0.01 : volume;
            if (volume < 0.0 || absVolume > dvhData.Volume)
                return DoseValue.UndefinedDose();

            DVHPoint[] hist = dvhData.CurveData;
            for (int i = 0; i < hist.Length; i++)
            {
                if (hist[i].Volume < volume)
                    return hist[i].DoseValue;
            }
            return DoseValue.UndefinedDose();
        }

        public static double VolumeAtDose(DVHData dvhData, double dose)
        {
            if (dvhData == null)
                return Double.NaN;

            DVHPoint[] hist = dvhData.CurveData;
            int index = (int)(hist.Length * dose / dvhData.MaxDose.Dose);
            if (index < 0 || index > hist.Length)
                return 0.0;//Double.NaN;
            else
                return hist[index].Volume;
        }
    }

    // The following class is prepared to build collections of courses/plans and associated with each of them structures set
    // for a patient in context of whom the plugin is used
    public class CoursePlan
    {
        public Course Course { get; set; }
        public PlanSetup Plan { get; set; }
        public string CoursePlanId { get; set; }
        public StructureSet StructureSet { get; set; }
        public bool IsCheckedPlan { get; set; }

        public CoursePlan(Course Course, PlanSetup Plan, string CoursePlanId, StructureSet StructureSet, bool IsCheckedPlan)
        {
            this.Course = Course;
            this.Plan = Plan;
            this.CoursePlanId = CoursePlanId;
            this.StructureSet = StructureSet;
            this.IsCheckedPlan = IsCheckedPlan;
        }
    }

    // The StructureTarget class defines base record for collections which let manipulate Structures
    public class StructureTarget
    {
        public Structure Structure { get; set; }
        public bool IsCheckedStructure { get; set; }
        public bool Target { get; set; }
        public double MaxDose { get; set; }
        public double Weight { get; set; }

        //two constuctors: first is used to define joint subset of structures
        public StructureTarget(Structure Structure, bool IsCheckedStructure, bool Target)
        {
            this.Structure = Structure;
            this.IsCheckedStructure = IsCheckedStructure;
            this.Target = Target;
            this.Weight = 1.00;
        }

        // the second is used when a user has marked interesting structures and assigned them weights
        // (or changed them);
        // lets to build a collection with the info on max dose of all plans for every structure
        public StructureTarget(Structure Structure, bool IsCheckedStructure, bool Target, double MaxDose, double Weight)
        {
            this.Structure = Structure;
            this.IsCheckedStructure = IsCheckedStructure;
            this.Target = Target;
            this.MaxDose = MaxDose;
            this.Weight = Weight;
        }
    }


    // The class Result lets build collections with plans and calculated indices values
    // the collection is a base for final table with results
    // The class is prepared in such a way that - dependently on used constructor - lets to present results in two forms:
    // first one is reacher, but now the second simpler form is used
    public class Result
    {
        public Course Course { get; set; }
        public PlanSetup Plan { get; set; }
        public string CoursePlanId { get; set; }
        public IEnumerable<Structure> Structures { get; set; }
        public double IndexResult { get; set; }

        public Result(Course Course, PlanSetup Plan, string CoursePlanId, IEnumerable<Structure> Structures, double IndexResult)
        {
            this.Course = Course;
            this.Plan = Plan;
            this.CoursePlanId = CoursePlanId;
            this.Structures = Structures;
            this.IndexResult = IndexResult;
        }

        public Result(string CoursePlanId, double IndexResult)
        {
            this.CoursePlanId = CoursePlanId;
            this.IndexResult = IndexResult;
        }
    }



public class Script
{


    public Script()
    {
    }


    // variables
    List<CoursePlan> CoursePlanList { get; set; }

    ObservableCollection<CoursePlan> CollCoursePlan { get; set; }
    public static ObservableCollection<CoursePlan> StaticCollCoursePlan;
    ObservableCollection<StructureTarget> CollStructureTarget { get; set; }
    public static ObservableCollection<StructureTarget> StaticCollStructureTarget;
    ObservableCollection<Result> CollResult { get; set; }
    public static ObservableCollection<Result> StaticCollResult;


    // methods
    // the method Execute() is being released automatically when the plugin is loaded
    public void Execute(ScriptContext context, System.Windows.Window window)
    {

        CollCoursePlan = new ObservableCollection<CoursePlan>();
        IEnumerable<Course> courses = context.Patient.Courses;

        // the nested loops over courses and plans to collect all valid plans (i.e. with non-null dose) and structures sets
        foreach (Course course in courses)
        {
            IEnumerable<PlanSetup> planSetups = course.PlanSetups;
            foreach (PlanSetup planSetup in planSetups)
            {
                if (planSetup.Dose != null & planSetup.StructureSet != null)
                {
                    string courseplanid = course.Id + " / " + planSetup.Id;
                    CollCoursePlan.Add(new CoursePlan(course, planSetup, courseplanid, planSetup.StructureSet, false));
                }
            }
        }
        // binding to lists shown in GUI can be done only using static variable...
        StaticCollCoursePlan = CollCoursePlan;
        StaticCollStructureTarget = null;
        StaticCollResult = null;

        var mainControl = new FastMPI.MainControl();

        // main window settings
        window.Content = mainControl;
        window.Background = System.Windows.Media.Brushes.Cornsilk;
        window.Height = 660;
        window.Width = 820;
        window.Title = "Multiple Planning Indices for " + context.Patient.Name;
    }


// the method PrepareStructures() is being released every time when a user checks radiobutton for RPI Index
// or when there is a change in the lists of checked plans which are to be analysed
    public void PrepareStructures()
    {
        StaticCollStructureTarget = null;

        // check if after unchecking plans by a user remains at least a single plan for analysis
        bool checkIfIsChecked = false;
        foreach (CoursePlan Plan in StaticCollCoursePlan)
        {
            if (Plan.IsCheckedPlan)
            {
                checkIfIsChecked = true;
                break;
            }
        }

        if (!checkIfIsChecked)
        {
            StaticCollStructureTarget = null;
            StaticCollResult = null; // czy potrzebne?
            return;

            CollStructureTarget = new ObservableCollection<StructureTarget>();
            var StructuresTech = StructuresIntersect();
            foreach (Structure slIntersect in StructuresTech)
            {
                // check if the structure is nor empty nither "Support"-type
                if (slIntersect.IsEmpty | slIntersect.DicomType == "SUPPORT")
                    continue;
                CollStructureTarget.Add(new StructureTarget(slIntersect, false, false));
            }

            StaticCollStructureTarget = CollStructureTarget;
        }

        // find the intersection of structures for all plans
        private IEnumerable<Structure> StructuresIntersect()
        {
            IEnumerable<CoursePlan> SelectedPlans = StaticCollCoursePlan.Where(plan => plan.IsCheckedPlan == true);
            IEnumerable<Structure> Structures;
            List<IEnumerable<Structure>> ListOfStructureLists = new List<IEnumerable<Structure>>();

            foreach (CoursePlan plan in SelectedPlans)
            {
                Structures = plan.StructureSet.Structures;
                ListOfStructureLists.Add(Structures);
            }

            List<IEnumerable<Structure>> SL = ListOfStructureLists;
            IEnumerable<Structure> SLIntersect = SL.First();
            if (SL.Count() > 1)
            {
                IEnumerable<string> SLIntersectID = from structure in SLIntersect select structure.Id;

                foreach (IEnumerable<Structure> list in SL)
                {
                    IEnumerable<string> listID = from structure in list select structure.Id;
                    SLIntersectID = listID.Intersect(SLIntersectID);
                    SLIntersect = from structure in SLIntersect where SLIntersectID.Contains(structure.Id) select structure;
                }
            }
            return (SLIntersect);
        }

// the method is released when a user clicks the "Submit" button when the radiobutton "GI" is checked
    public void CalculateGIResults()
    {
        var SelectedPlans = StaticCollCoursePlan.Where(plan => plan.IsCheckedPlan == true);
        CollResult = new ObservableCollection<Result>();
        double GI;

        foreach (CoursePlan selectedPlan in SelectedPlans)
        {
            DoseValue prescribedDose = selectedPlan.Plan.TotalPrescribedDose;
            DoseValue halfPrescribedDose = new DoseValue(prescribedDose.Dose / 2.0, prescribedDose.Unit);
            Structure BODY = selectedPlan.StructureSet.Structures.Where(structure => structure.DicomType == "EXTERNAL").First();

            double BODYwithPrescribedDoseVolume = selectedPlan.Plan.GetVolumeAtDose(BODY, prescribedDose, VolumePresentation.AbsoluteCm3);
            double BODYwithHalfPrescribedDoseVolume = selectedPlan.Plan.GetVolumeAtDose(BODY, halfPrescribedDose, VolumePresentation.AbsoluteCm3);

            if(BODYwithPrescribedDoseVolume < 0.001)
            {
                MessageBox.Show("For the plan " + selectedPlan.CoursePlanId + " the prescription dose is not achieved. The plan will not be taken into account in results.");
            }
            else
            {
                GI = BODYwithHalfPrescribedDoseVolume / BODYwithPrescribedDoseVolume;
                GI = Math.Round(GI, 4);
                CollResult.Add(new Result(selectedPlan.CoursePlanId, GI));
            }

        }

        StaticCollResult = CollResult;
    }

// the method is released when a user clicks the "Submit" button when the radiobutton "CN" is checked
    public void CalculateCNResults()
    {
        var SelectedPlans = StaticCollCoursePlan.Where(plan => plan.IsCheckedPlan == true);
        CollResult = new ObservableCollection<Result>();
        double CN;

        foreach (CoursePlan selectedPlan in SelectedPlans)
        {
            DoseValue prescribedDose = selectedPlan.Plan.TotalPrescribedDose;
            Structure BODY = selectedPlan.StructureSet.Structures.Where(structure => structure.DicomType == "EXTERNAL").First();
            Structure TARGET = selectedPlan.StructureSet.Structures.Where(structure => structure.Id == selectedPlan.Plan.TargetVolumeID).First();
            double TARGETvolume = TARGET.Volume;
            double BODYwithPrescribedDoseVolume = selectedPlan.Plan.GetVolumeAtDose(BODY, prescribedDose, VolumePresentation.AbsoluteCm3);
            double TARGETwithPrescribedDoseVolume = selectedPlan.Plan.GetVolumeAtDose(TARGET, prescribedDose, VolumePresentation.AbsoluteCm3);
            double dvalue1 = BODYwithPrescribedDoseVolume / TARGETvolume;
            CN = (TARGETwithPrescribedDoseVolume / TARGETvolume) * (TARGETwithPrescribedDoseVolume / BODYwithPrescribedDoseVolume);
            CN = Math.Round(CN, 4);
            CollResult.Add(new Result(selectedPlan.CoursePlanId, CN));
        }

        StaticCollResult = CollResult;
    }


// the method is released when a user clicks the "Submit" button when the radiobutton "RPI" is checked
    public void CalculateResults(bool AreChangedWeights)
    {
        // the RPI index calculation is being done in two steps:
        // - firstly for every structure the max dose among all plans is determined
        // - secondly surface under DVH curve is calculated and divided by surface of rectangle which is defined by
        //   max dose from first step

        // first step
        ObservableCollection<StructureTarget> StructuresWithMaxDose = new ObservableCollection<StructureTarget>();
        var SelectedPlans = StaticCollCoursePlan.Where(plan => plan.IsCheckedPlan == true);
        var SelectedStructures = StaticCollStructureTarget.Where(structure => structure.IsCheckedStructure == true);

        double maxDose;

        foreach (StructureTarget selectedStructure in SelectedStructures)
        {
            string structureID = selectedStructure.Structure.Id;
            maxDose = 0.0;
            foreach (CoursePlan selectedPlan in SelectedPlans)
            {
                // identification of a stucture is made on a base of its name
                // - even if other parameters of given structure are different
                Structure selectedStructureInSelectedPlan = selectedPlan.StructureSet.Structures.Where(structure => structure.Id == structureID).First();
                DVHData dvhData = selectedPlan.Plan.GetDVHCumulativeData(selectedStructureInSelectedPlan, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                maxDose = maxDose > dvhData.MaxDose.Dose ? maxDose : dvhData.MaxDose.Dose;
                // following code would let to take into account for target structue(s) the prescribed dose
                // if it is larger then max dose
                /*
                if (selectedStructure.Target)
                {
                    prescribedDose = selectedPlan.Plan.TotalPrescribedDose.Dose;
                    maxDose = maxDose > prescribedDose ? maxDose : prescribedDose;
                }
                */
            }
            StructuresWithMaxDose.Add(new StructureTarget(selectedStructure.Structure, selectedStructure.IsCheckedStructure, selectedStructure.Target, maxDose, selectedStructure.Weight));
        }


        // second step - calculate index contribiutions from all stuctures and finally calculate RPI

        bool target;
        double RPI;
        double stdDev;
        double iRPI;

        CollResult = new ObservableCollection<Result>();

        int Max = SelectedPlans.Count() * StructuresWithMaxDose.Count();
        int planCounter = 0;

        foreach (CoursePlan selectedPlan in SelectedPlans)
        {
            RPI = 1.0;
            int structureCounter = 0;
            planCounter++;

            foreach (StructureTarget selectedStructure in StructuresWithMaxDose)
            {
                structureCounter++;
                string structureID = selectedStructure.Structure.Id;
                Structure selectedStructureInSelectedPlan = selectedPlan.StructureSet.Structures.Where(structure => structure.Id == structureID).First();
                DVHData dvhData = selectedPlan.Plan.GetDVHCumulativeData(selectedStructureInSelectedPlan, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                DVHPoint[] curveData = dvhData.CurveData;
                target = selectedStructure.Target;
                stdDev = target ? dvhData.StdDev : 0.0;

                if (!AreChangedWeights)
                {
                    iRPI = Calculate_iRPI(curveData, selectedStructure.MaxDose, target, stdDev);
                    RPI = RPI * iRPI;
                }
                else
                {
                    double weight = selectedStructure.Weight;
                    iRPI = Calculate_iRPI_ChangedWeights(curveData, selectedStructure.MaxDose, target, stdDev, weight);
                    RPI = RPI * iRPI;
                }
            }

            RPI = Math.Pow(RPI, 1.0 / (double)SelectedStructures.Count());
            RPI = Math.Round(RPI, 4);
            CollResult.Add(new Result(selectedPlan.CoursePlanId, RPI));
        }

        StaticCollResult = CollResult;
    }


    // iRPI contribution for the case with no changed weights
    private double Calculate_iRPI(DVHPoint[] curveData, double maxDoseInPlan, bool target, double stdDev)
    {
        int N = curveData.Length;
        double[,] dvhCurve = new double[2, N];
        for (int i = 0; i < N; i++)
        {
            dvhCurve[0, i] = curveData[i].DoseValue.Dose;
            dvhCurve[1, i] = curveData[i].Volume;
        }

        double AUC = Integrate(N, dvhCurve);
        double AUC_Dose_Volume_ratio = AUC / maxDoseInPlan / 100.0; // divided by 100.0 because value of Volume is persentage
        double iRPI = target ? AUC_Dose_Volume_ratio * (1.0 - stdDev / maxDoseInPlan) : 1 - AUC_Dose_Volume_ratio;
        return (iRPI);
    }

    // iRPI contribution for the case with user-changed weights
    private double Calculate_iRPI_ChangedWeights(DVHPoint[] curveData, double maxDoseInPlan, bool target, double stdDev, double weight)
    {
        int N = curveData.Length;
        double[,] dvhCurve = new double[2, N];
        for (int i = 0; i < N; i++)
        {
            dvhCurve[0, i] = curveData[i].DoseValue.Dose;
            dvhCurve[1, i] = curveData[i].Volume;
        }

        double AUC = Integrate(N, dvhCurve);
        double AUC_Dose_Volume_ratio = AUC / maxDoseInPlan / 100.0; // divided by 100.0 because value of Volume is persentage
        double iRPI = target ? (AUC_Dose_Volume_ratio * weight * (1.0 - stdDev / maxDoseInPlan)) : ((1 - weight * AUC_Dose_Volume_ratio));
        return (iRPI);
    }

    // DVH-curve integration
    public static double Integrate(int N, double[,] y)
    {
        double sum = (y[1, 0] + y[1, N - 1]) / 2;
        for (int i = 1; i < N - 1; i++)
        {
            sum += y[1, i];
        }

        double h = (y[0, N - 1] - y[0, 0]) / (double)N;
        return h * sum;
    }
    }
}
