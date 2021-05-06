using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System.Xml;

// <Custom "using" statements>

using System.Linq;
using Rhino.Geometry.Intersect;
//using Eto.Forms;


// </Custom "using" statements>


#region padding (this ensures the line number of this file match with those in the code editor of the C# Script component





















#endregion

public partial class MyExternalScript : GH_ScriptInstance
{
    #region Do_not_modify_this_region
    private void Print(string text) { }
    private void Print(string format, params object[] args) { }
    private void Reflect(object obj) { }
    private void Reflect(object obj, string methodName) { }
    public override void InvokeRunScript(IGH_Component owner, object rhinoDocument, int iteration, List<object> inputs, IGH_DataAccess DA) { }
    public RhinoDoc RhinoDocument;
    public GH_Document GrasshopperDocument;
    public IGH_Component Component;
    public int Iteration;
    #endregion


    private void RunScript(List<Curve> checkCurves, List<Curve> boundaries, double toolDiameter, double finalPass, double stepDown, double stepover, double materialTop, ref object A, ref object B)
    {
        // <Custom code>
        if (checkCurves.Count != boundaries.Count)
        {
            Print("curve inputs lists contain different numbers of curves");
            return;
        }
        if (checkCurves.Count > 1)
        {
            checkCurves = checkCurves.OrderBy(c => c.PointAt(0).Z).ToList();
            boundaries = boundaries.OrderBy(c => c.PointAt(0).Z).ToList();
        }
        Polyline fullPath = new Polyline();

        for (int j = 0; j < checkCurves.Count; j++)
        {
            Curve checkCurve = checkCurves[j].DuplicateCurve();
            Curve boundaryCurve = boundaries[j].DuplicateCurve();

            double currentTop = materialTop;
            if (j < checkCurves.Count - 1) currentTop = checkCurves[j + 1].PointAt(0.5).Z;

            Print("currentTop = {0}", currentTop);

            Polyline plTemp = LevelPath(checkCurve, boundaryCurve, toolDiameter, finalPass, stepover);


            Print("currentTop", currentTop);

            int breaker = 0;
            while (plTemp[0].Z <= currentTop-0.5)
            {
                Print("plTemp[0].Z = {0}", plTemp[0].Z);

                Point3d startPt = plTemp[0];
                startPt.Z = materialTop + 20;
                fullPath.Add(startPt);
                foreach (Point3d pt in plTemp)
                    fullPath.Add(pt);
                Point3d endPt = plTemp.Last();
                endPt.Z = materialTop + 20;
                fullPath.Add(endPt);

                var moveUp = Transform.Translation(0, 0, stepDown);
                plTemp.Transform(moveUp);
                if (breaker > 10) break;

                breaker++;
            }
        }
        Print("FullPathCount = {0}", fullPath.Count);


        fullPath.Reverse();

        A = fullPath;
        B = testOut;
        // </Custom code>
    }

    // <Custom additional code>

    //Curve boundaryCurve;
    List<object> testOut = new List<object>();

    Polyline LevelPath(Curve checkCurve, Curve boundaryCurve, double toolDiameter, double finalPass, double stepover)
    {
        testOut.Clear();
        List<Curve> curves = new List<Curve>();
        bool success;
        curves.Add(checkCurve.Offset(Plane.WorldXY, toolDiameter / 2, 0.01, CurveOffsetCornerStyle.Round)[0]);

        if (!(boundaryCurve.IsClosed && boundaryCurve.Contains(curves[0].PointAtNormalizedLength(0.5), Plane.WorldXY, 0.01) == PointContainment.Inside))
        {
            checkCurve.Reverse();
            curves[0] = checkCurve.Offset(Plane.WorldXY, toolDiameter / 2, 0.01, CurveOffsetCornerStyle.Round)[0];
        }


        Curve curveTemp = offsetPath(curves.Last(), boundaryCurve, finalPass, out success);



        curves.Add(curveTemp);
        int i = 0;

        while (success)
        {
            RhinoApp.WriteLine("curves.Count = {0}", curves.Count);
            curveTemp = offsetPath(curves.Last(), boundaryCurve, stepover, out success);
            if (success)
            {
                Curve curveCopy = curveTemp.DuplicateCurve();

                //testOut.Add(curveCopy);
                curves.Add(curveCopy);
            }
            i++;
            if (i > 500) break;
        }

        Polyline plOut = new Polyline();
        bool flip = true;
        foreach (Curve c in curves)
        {
            curveTemp = c.DuplicateCurve();
            if (flip) curveTemp.Reverse();
            Polyline plTemp = curveTemp.ToPolyline(0.1, 0.1, 0.01, 2000).ToPolyline();
            foreach (Point3d p in plTemp)
                plOut.Add(p);
            flip = !flip;
        }

        return plOut;
    }
    Curve offsetPath(Curve pathIn, Curve boundaryIn, double step, out bool success)
    {
        Curve curveInput = pathIn.Extend(CurveEnd.Both, 100, CurveExtensionStyle.Line);

        curveInput = curveInput.Offset(Plane.WorldXY, step, 0.01, CurveOffsetCornerStyle.Round)[0];
        //if (curveInputs.Length > 0) ;

         var intersection = Intersection.CurveCurve(curveInput, boundaryIn, 0.01, 0.01);
        List<double> splitParams = new List<double>();
        foreach (IntersectionEvent e in intersection) splitParams.Add(e.ParameterA);
        List<Curve> subcurvesInside = new List<Curve>();

        if (intersection.Count > 0)
        {

            List<Curve> subcurves = curveInput.Split(splitParams).ToList();

            foreach (Curve c in subcurves)
            {
                if (boundaryIn.Contains(c.PointAtNormalizedLength(0.5), Plane.WorldXY, 0.01) == PointContainment.Inside)
                {
                    Curve curveCopy = c.DuplicateCurve();
                    subcurvesInside.Add(curveCopy);
                    //testOut.Add(curveCopy);
                }
            }


        }
        else success = false;


        List<Curve> curveOutput = Curve.JoinCurves(subcurvesInside, 200).ToList();


        if (curveOutput.Count > 0)
        {
            success = true;
            return curveOutput[0];
        }
        else
        {
            success = false;
            return null;
        }
    }

    // </Custom additional code>
}
